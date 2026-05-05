using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using RevitMCPBridge2026;
using RevitMCPBridge.Helpers;

namespace RevitMCPBridge
{
    /// <summary>
    /// Group and assembly methods for MCP Bridge
    /// </summary>
    public static class GroupMethods
    {
        /// <summary>
        /// Get all groups in the model
        /// </summary>
        [MCPMethod("getGroups", Category = "Group", Description = "Get all groups in the model")]
        public static string GetGroups(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var groups = new FilteredElementCollector(doc)
                    .OfClass(typeof(Group))
                    .Cast<Group>()
                    .Select(g => new
                    {
                        groupId = g.Id.Value,
                        name = g.Name,
                        groupTypeId = g.GroupType?.Id.Value ?? -1,
                        groupTypeName = g.GroupType?.Name ?? "Unknown",
                        memberCount = g.GetMemberIds().Count
                    })
                    .ToList();

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    groupCount = groups.Count,
                    groups = groups
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Get all group types in the model
        /// </summary>
        [MCPMethod("getGroupTypes", Category = "Group", Description = "Get all group types in the model")]
        public static string GetGroupTypes(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var groupTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(GroupType))
                    .Cast<GroupType>()
                    .Select(gt => new
                    {
                        groupTypeId = gt.Id.Value,
                        name = gt.Name,
                        category = gt.Category?.Name ?? "Unknown",
                        isDetailGroup = (gt.Category != null && gt.Category.Name != null && gt.Category.Name.IndexOf("Detail", StringComparison.OrdinalIgnoreCase) >= 0)
                    })
                    .ToList();

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    groupTypeCount = groupTypes.Count,
                    groupTypes = groupTypes
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Get group members
        /// </summary>
        [MCPMethod("getGroupMembers", Category = "Group", Description = "Get group members")]
        public static string GetGroupMembers(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var groupId = parameters["groupId"]?.Value<int>();

                if (!groupId.HasValue)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "groupId is required" });
                }

                var group = doc.GetElement(new ElementId(groupId.Value)) as Group;
                if (group == null)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "Group not found" });
                }

                var members = group.GetMemberIds().Select(id =>
                {
                    var elem = doc.GetElement(id);
                    return new
                    {
                        elementId = (int)id.Value,
                        name = elem?.Name ?? "Unknown",
                        category = elem?.Category?.Name ?? "Unknown"
                    };
                }).ToList();

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    groupId = groupId.Value,
                    groupName = group.Name,
                    memberCount = members.Count,
                    members = members
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Create a group from selected elements
        /// </summary>
        [MCPMethod("createGroup", Category = "Group", Description = "Create a group from selected elements")]
        public static string CreateGroup(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var elementIds = parameters["elementIds"]?.ToObject<int[]>();
                var groupName = parameters["groupName"]?.ToString();

                if (elementIds == null || elementIds.Length == 0)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "elementIds are required" });
                }

                using (var trans = new Transaction(doc, "Create Group"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    var ids = elementIds.Select(id => new ElementId(id)).ToList();
                    var group = doc.Create.NewGroup(ids);

                    if (!string.IsNullOrEmpty(groupName))
                    {
                        group.GroupType.Name = groupName;
                    }

                    trans.Commit();

                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        groupId = group.Id.Value,
                        groupTypeId = group.GroupType.Id.Value,
                        groupTypeName = group.GroupType.Name
                    });
                }
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Place an instance of a group type
        /// </summary>
        [MCPMethod("placeGroup", Category = "Group", Description = "Place an instance of a group type")]
        public static string PlaceGroup(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var groupTypeId = parameters["groupTypeId"]?.Value<int>();
                var x = parameters["x"]?.Value<double>();
                var y = parameters["y"]?.Value<double>();
                var z = parameters["z"]?.Value<double>() ?? 0;

                // Also accept location:[x,y,z] array format
                if (!x.HasValue || !y.HasValue)
                {
                    var loc = parameters["location"]?.ToObject<double[]>();
                    if (loc != null && loc.Length >= 2)
                    {
                        x = loc[0];
                        y = loc[1];
                        if (loc.Length >= 3) z = loc[2];
                    }
                }

                if (!groupTypeId.HasValue)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "groupTypeId is required" });
                }

                if (!x.HasValue || !y.HasValue)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "x and y are required" });
                }

                var groupType = doc.GetElement(new ElementId(groupTypeId.Value)) as GroupType;
                if (groupType == null)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "Group type not found" });
                }

                // Detail groups are view-specific. PlaceGroup only works if the correct view
                // is active in the UI. Check and warn if there's a mismatch.
                bool isDetailGroup = groupType.Category?.Name?.IndexOf("Detail", StringComparison.OrdinalIgnoreCase) >= 0;
                var viewId = parameters["viewId"]?.Value<int>();

                if (isDetailGroup && viewId.HasValue)
                {
                    var activeView = uiApp.ActiveUIDocument.ActiveView;
                    if (activeView == null || activeView.Id.Value != viewId.Value)
                    {
                        return JsonConvert.SerializeObject(new
                        {
                            success = false,
                            error = $"Detail group placement requires view {viewId.Value} to be the active view in Revit. " +
                                    $"Currently active view is '{activeView?.Name ?? "none"}' (id {activeView?.Id.Value}). " +
                                    "Switch to the target view and retry, or use copyElements from an existing instance in that view."
                        });
                    }
                }

                using (var trans = new Transaction(doc, "Place Group"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    var location = new XYZ(x.Value, y.Value, z);
                    var group = doc.Create.PlaceGroup(location, groupType);

                    if (group == null)
                    {
                        trans.RollBack();
                        var hint = isDetailGroup
                            ? "Detail groups must be placed while their target view is active. Ensure the correct view is open in Revit, or use copyElements from an existing group instance in that view."
                            : "PlaceGroup returned null. Verify the groupTypeId is correct and the document is in an editable state.";
                        return JsonConvert.SerializeObject(new { success = false, error = hint });
                    }

                    trans.Commit();

                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        groupId = group.Id.Value,
                        isDetailGroup,
                        location = new { x = x.Value, y = y.Value, z = z }
                    });
                }
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Ungroup a group
        /// </summary>
        [MCPMethod("ungroupGroup", Category = "Group", Description = "Ungroup a group")]
        public static string UngroupGroup(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var groupId = parameters["groupId"]?.Value<int>();

                if (!groupId.HasValue)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "groupId is required" });
                }

                var group = doc.GetElement(new ElementId(groupId.Value)) as Group;
                if (group == null)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "Group not found" });
                }

                using (var trans = new Transaction(doc, "Ungroup"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    var memberIds = group.UngroupMembers();

                    trans.Commit();

                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        ungroupedElementCount = memberIds.Count,
                        ungroupedElementIds = memberIds.Select(id => (int)id.Value).ToList()
                    });
                }
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Delete a group type
        /// </summary>
        [MCPMethod("deleteGroupType", Category = "Group", Description = "Delete a group type")]
        public static string DeleteGroupType(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var groupTypeId = parameters["groupTypeId"]?.Value<int>();

                if (!groupTypeId.HasValue)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "groupTypeId is required" });
                }

                using (var trans = new Transaction(doc, "Delete Group Type"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    doc.Delete(new ElementId(groupTypeId.Value));
                    trans.Commit();

                    return JsonConvert.SerializeObject(new { success = true, deletedGroupTypeId = groupTypeId.Value });
                }
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Rename a group type
        /// </summary>
        [MCPMethod("renameGroupType", Category = "Group", Description = "Rename a group type")]
        public static string RenameGroupType(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var groupTypeId = parameters["groupTypeId"]?.Value<int>();
                var newName = parameters["newName"]?.ToString();

                if (!groupTypeId.HasValue || string.IsNullOrEmpty(newName))
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "groupTypeId and newName are required" });
                }

                var groupType = doc.GetElement(new ElementId(groupTypeId.Value)) as GroupType;
                if (groupType == null)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "Group type not found" });
                }

                using (var trans = new Transaction(doc, "Rename Group Type"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    groupType.Name = newName;

                    trans.Commit();

                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        groupTypeId = groupTypeId.Value,
                        newName = newName
                    });
                }
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Duplicate a group type
        /// </summary>
        [MCPMethod("duplicateGroupType", Category = "Group", Description = "Duplicate a group type")]
        public static string DuplicateGroupType(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var groupTypeId = parameters["groupTypeId"]?.Value<int>();
                var newName = parameters["newName"]?.ToString();

                if (!groupTypeId.HasValue)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "groupTypeId is required" });
                }

                var groupType = doc.GetElement(new ElementId(groupTypeId.Value)) as GroupType;
                if (groupType == null)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "Group type not found" });
                }

                using (var trans = new Transaction(doc, "Duplicate Group Type"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    var duplicatedType = groupType.Duplicate(newName ?? (groupType.Name + " Copy"));

                    trans.Commit();

                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        originalGroupTypeId = groupTypeId.Value,
                        newGroupTypeId = duplicatedType.Id.Value,
                        newName = duplicatedType.Name
                    });
                }
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Get all model groups (excludes detail groups)
        /// </summary>
        [MCPMethod("getModelGroups", Category = "Group", Description = "Get all model groups (excludes detail groups)")]
        public static string GetModelGroups(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var groups = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_IOSModelGroups)
                    .WhereElementIsNotElementType()
                    .Cast<Group>()
                    .Select(g => new
                    {
                        groupId = g.Id.Value,
                        name = g.Name,
                        groupTypeId = g.GroupType?.Id.Value ?? -1,
                        groupTypeName = g.GroupType?.Name ?? "Unknown",
                        memberCount = g.GetMemberIds().Count
                    })
                    .ToList();

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    modelGroupCount = groups.Count,
                    modelGroups = groups
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Get all detail groups
        /// </summary>
        [MCPMethod("getDetailGroups", Category = "Group", Description = "Get all detail groups")]
        public static string GetDetailGroups(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var groups = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_IOSDetailGroups)
                    .WhereElementIsNotElementType()
                    .Cast<Group>()
                    .Select(g => new
                    {
                        groupId = g.Id.Value,
                        name = g.Name,
                        groupTypeId = g.GroupType?.Id.Value ?? -1,
                        groupTypeName = g.GroupType?.Name ?? "Unknown",
                        memberCount = g.GetMemberIds().Count,
                        ownerViewId = (int?)g.OwnerViewId?.Value ?? -1
                    })
                    .ToList();

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    detailGroupCount = groups.Count,
                    detailGroups = groups
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Create a group from elements and replicate in a pattern (grid, linear, radial).
        /// </summary>
        [MCPMethod("createGroupAndReplicatePattern", Category = "Group", Description = "Create a group from elements and replicate in a pattern")]
        public static string CreateGroupAndReplicatePattern(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var elementIds = parameters?["elementIds"]?.ToObject<List<int>>();
                var groupName = parameters?["groupName"]?.ToString() ?? $"Group_{DateTime.Now:yyyyMMdd_HHmmss}";
                var patternType = parameters?["patternType"]?.ToString() ?? "grid"; // grid, linear, radial
                var countX = parameters?["countX"]?.ToObject<int>() ?? 2;
                var countY = parameters?["countY"]?.ToObject<int>() ?? 2;
                var spacingX = parameters?["spacingX"]?.ToObject<double>() ?? 10.0; // feet
                var spacingY = parameters?["spacingY"]?.ToObject<double>() ?? 10.0; // feet
                var radialCount = parameters?["radialCount"]?.ToObject<int>() ?? 6;
                var radius = parameters?["radius"]?.ToObject<double>() ?? 10.0; // feet

                if (elementIds == null || elementIds.Count == 0)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "elementIds are required"
                    });
                }

                var ids = elementIds.Select(id => new ElementId(id)).ToList();
                var idCollection = new List<ElementId>(ids);

                Group createdGroup = null;
                var placedInstances = new List<object>();

                using (var trans = new Transaction(doc, "Create Group and Replicate"))
                {
                    trans.Start();

                    // Create the group
                    createdGroup = doc.Create.NewGroup(idCollection);
                    if (!string.IsNullOrEmpty(groupName))
                    {
                        createdGroup.GroupType.Name = groupName;
                    }

                    // Get group center point
                    var bb = createdGroup.get_BoundingBox(null);
                    var origin = bb != null ? (bb.Min + bb.Max) / 2 : XYZ.Zero;
                    var baseZ = origin.Z;

                    placedInstances.Add(new
                    {
                        index = 0,
                        groupId = createdGroup.Id.Value,
                        position = new { x = Math.Round(origin.X, 2), y = Math.Round(origin.Y, 2) },
                        isOriginal = true
                    });

                    // Create pattern instances
                    if (patternType.ToLower() == "grid")
                    {
                        for (int i = 0; i < countX; i++)
                        {
                            for (int j = 0; j < countY; j++)
                            {
                                if (i == 0 && j == 0) continue; // Skip original position

                                var offset = new XYZ(i * spacingX, j * spacingY, 0);
                                var newPos = origin + offset;

                                var instance = doc.Create.PlaceGroup(newPos, createdGroup.GroupType);
                                placedInstances.Add(new
                                {
                                    index = placedInstances.Count,
                                    groupId = instance.Id.Value,
                                    position = new { x = Math.Round(newPos.X, 2), y = Math.Round(newPos.Y, 2) },
                                    isOriginal = false
                                });
                            }
                        }
                    }
                    else if (patternType.ToLower() == "linear")
                    {
                        for (int i = 1; i < countX; i++)
                        {
                            var offset = new XYZ(i * spacingX, 0, 0);
                            var newPos = origin + offset;

                            var instance = doc.Create.PlaceGroup(newPos, createdGroup.GroupType);
                            placedInstances.Add(new
                            {
                                index = i,
                                groupId = instance.Id.Value,
                                position = new { x = Math.Round(newPos.X, 2), y = Math.Round(newPos.Y, 2) },
                                isOriginal = false
                            });
                        }
                    }
                    else if (patternType.ToLower() == "radial")
                    {
                        double angleStep = 2 * Math.PI / radialCount;
                        for (int i = 0; i < radialCount; i++)
                        {
                            double angle = i * angleStep;
                            var offset = new XYZ(radius * Math.Cos(angle), radius * Math.Sin(angle), 0);
                            var newPos = origin + offset;

                            var instance = doc.Create.PlaceGroup(newPos, createdGroup.GroupType);
                            placedInstances.Add(new
                            {
                                index = i,
                                groupId = instance.Id.Value,
                                position = new { x = Math.Round(newPos.X, 2), y = Math.Round(newPos.Y, 2) },
                                angleDegrees = Math.Round(angle * 180 / Math.PI, 1),
                                isOriginal = false
                            });
                        }
                    }

                    trans.Commit();
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    groupTypeId = createdGroup.GroupType.Id.Value,
                    groupTypeName = createdGroup.GroupType.Name,
                    patternType = patternType,
                    totalInstances = placedInstances.Count,
                    memberCount = createdGroup.GetMemberIds().Count,
                    instances = placedInstances
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        // ── traceDetailGroupBoundary ─────────────────────────────────────────────

        [MCPMethod("traceDetailGroupBoundary", Category = "Groups",
            Description = "Draw a detail line rectangle around the bounding box of a detail group to visually highlight its outside edge. Useful for clarifying detail and elevation drawings. Parameters: groupId (required), viewId (optional — defaults to active view), lineStyle (optional — line style name, default 'Medium Lines'), offsetInches (optional — expand box by this many inches on each side, default 1/16\").")]
        public static string TraceDetailGroupBoundary(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var activeView = uiApp.ActiveUIDocument.ActiveView;

                int groupIdVal = parameters?["groupId"]?.ToObject<int>()
                    ?? throw new ArgumentException("groupId is required.");

                var group = doc.GetElement(new ElementId(groupIdVal)) as Group;
                if (group == null)
                    return JsonConvert.SerializeObject(new { success = false, error = $"No group found with id {groupIdVal}." });
                var groupCategoryName = group.GroupType?.Category?.Name ?? "";
                bool isDetailGroup = groupCategoryName.IndexOf("Detail", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isDetailGroup)
                    return JsonConvert.SerializeObject(new { success = false, error = $"Element {groupIdVal} is not a detail group (category={groupCategoryName})." });

                // Resolve view
                View view = activeView;
                int? viewIdParam = parameters?["viewId"]?.ToObject<int?>();
                if (viewIdParam.HasValue)
                {
                    view = doc.GetElement(new ElementId(viewIdParam.Value)) as View;
                    if (view == null)
                        return JsonConvert.SerializeObject(new { success = false, error = $"No view found with id {viewIdParam}." });
                }

                var bb = group.get_BoundingBox(view);
                if (bb == null)
                    return JsonConvert.SerializeObject(new { success = false, error = "Could not get bounding box for this group in the specified view." });

                double offsetFt = (parameters?["offsetInches"]?.ToObject<double>() ?? (1.0 / 16.0)) / 12.0;

                // Project all 8 world corners of the bounding box onto the view's sketch plane,
                // then reconstruct the 4 rectangle corners using the plane's own XVec/YVec.
                // This is the only approach guaranteed to produce on-plane points for any view type
                // (plan, elevation, section, drafting) without "Curve must be in the plane" errors.
                XYZ xVec, yVec, planeOrigin;
                if (view.SketchPlane != null)
                {
                    var plane = view.SketchPlane.GetPlane();
                    xVec = plane.XVec;
                    yVec = plane.YVec;
                    planeOrigin = plane.Origin;
                }
                else
                {
                    // Elevation/section views have no SketchPlane — derive the drawing plane
                    // from CropBox.Transform: BasisX=right, BasisY=up, Origin=on the view plane.
                    var ct = view.CropBox.Transform;
                    xVec = ct.BasisX;
                    yVec = ct.BasisY;
                    planeOrigin = ct.Origin;
                }
                var bbT = bb.Transform;

                double uMin = double.MaxValue, uMax = double.MinValue;
                double vMin = double.MaxValue, vMax = double.MinValue;
                foreach (int xi in new[] { 0, 1 })
                foreach (int yi in new[] { 0, 1 })
                foreach (int zi in new[] { 0, 1 })
                {
                    var localPt = new XYZ(
                        xi == 0 ? bb.Min.X : bb.Max.X,
                        yi == 0 ? bb.Min.Y : bb.Max.Y,
                        zi == 0 ? bb.Min.Z : bb.Max.Z);
                    var wp = bbT.OfPoint(localPt) - planeOrigin;
                    double u = wp.DotProduct(xVec);
                    double v = wp.DotProduct(yVec);
                    if (u < uMin) uMin = u; if (u > uMax) uMax = u;
                    if (v < vMin) vMin = v; if (v > vMax) vMax = v;
                }
                uMin -= offsetFt; uMax += offsetFt;
                vMin -= offsetFt; vMax += offsetFt;

                var corners = new[]
                {
                    planeOrigin + uMin * xVec + vMin * yVec,
                    planeOrigin + uMax * xVec + vMin * yVec,
                    planeOrigin + uMax * xVec + vMax * yVec,
                    planeOrigin + uMin * xVec + vMax * yVec
                };

                var styleNameParam = parameters?["lineStyle"]?.ToString() ?? "Medium Lines";
                var style = new FilteredElementCollector(doc)
                    .OfClass(typeof(GraphicsStyle))
                    .Cast<GraphicsStyle>()
                    .Where(gs => gs.GraphicsStyleType == GraphicsStyleType.Projection)
                    .FirstOrDefault(gs => gs.Name.Equals(styleNameParam, StringComparison.OrdinalIgnoreCase))
                    ?? new FilteredElementCollector(doc)
                        .OfClass(typeof(GraphicsStyle))
                        .Cast<GraphicsStyle>()
                        .FirstOrDefault(gs => gs.GraphicsStyleType == GraphicsStyleType.Projection
                            && gs.Name.IndexOf("Medium", StringComparison.OrdinalIgnoreCase) >= 0);

                var createdIds = new List<int>();
                using (var tx = new Transaction(doc, "Trace Detail Group Boundary"))
                {
                    tx.Start();
                    for (int i = 0; i < 4; i++)
                    {
                        var from = corners[i];
                        var to = corners[(i + 1) % 4];
                        if (from.DistanceTo(to) < 0.001) continue;
                        var dl = doc.Create.NewDetailCurve(view, Line.CreateBound(from, to));
                        if (style != null) dl.LineStyle = style;
                        createdIds.Add((int)dl.Id.Value);
                    }
                    tx.Commit();
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    groupId = groupIdVal,
                    viewId = (int)view.Id.Value,
                    linesCreated = createdIds.Count,
                    lineIds = createdIds,
                    lineStyle = style?.Name ?? "default",
                    message = $"Boundary rectangle drawn around group {groupIdVal} in '{view.Name}'."
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }
    }
}
