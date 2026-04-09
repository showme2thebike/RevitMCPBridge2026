using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPBridge;
using RevitMCPBridge.Helpers;

namespace RevitMCPBridge2026
{
    /// <summary>
    /// MCP Server Methods for Revit Annotations
    /// Handles keynotes, symbols, revision clouds, dimensions, and other annotation elements
    /// </summary>
    public static class AnnotationMethods
    {
        #region Keynotes

        /// <summary>
        /// Places a keynote annotation
        /// </summary>
        /// <param name="uiApp">The Revit UI Application</param>
        /// <param name="parameters">JSON parameters containing reference, location, keynoteKey</param>
        /// <returns>JSON response with success status and keynote ID</returns>
        [MCPMethod("placeKeynote", Category = "Annotation", Description = "Places a keynote annotation tag")]
        public static string PlaceKeynote(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["viewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "viewId is required"
                    });
                }

                if (parameters["referenceId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "referenceId is required"
                    });
                }

                if (parameters["location"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "location is required (array of [x, y, z])"
                    });
                }

                // Parse parameters
                int viewIdInt = parameters["viewId"].ToObject<int>();
                ElementId viewId = new ElementId(viewIdInt);
                View view = doc.GetElement(viewId) as View;

                if (view == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {viewIdInt} not found"
                    });
                }

                int referenceIdInt = parameters["referenceId"].ToObject<int>();
                ElementId referenceId = new ElementId(referenceIdInt);
                Element referenceElement = doc.GetElement(referenceId);

                if (referenceElement == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Reference element with ID {referenceIdInt} not found"
                    });
                }

                var locationArray = parameters["location"].ToObject<double[]>();
                XYZ location = new XYZ(locationArray[0], locationArray[1], locationArray[2]);

                using (var trans = new Transaction(doc, "Place Keynote"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Get reference from element
                    Reference reference = new Reference(referenceElement);

                    // Create keynote tag - use MULTICATEGORY for keynote tags
                    var tagMode = parameters["tagMode"]?.ToString()?.ToLower() == "category"
                        ? TagMode.TM_ADDBY_CATEGORY
                        : TagMode.TM_ADDBY_MULTICATEGORY;

                    IndependentTag keynote = IndependentTag.Create(
                        doc,
                        view.Id,
                        reference,
                        true,
                        tagMode,
                        TagOrientation.Horizontal,
                        location
                    );

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        keynoteId = (int)keynote.Id.Value,
                        viewId = viewIdInt,
                        referenceId = referenceIdInt,
                        location = new { x = location.X, y = location.Y, z = location.Z }
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Loads keynote file
        /// </summary>
        [MCPMethod("loadKeynoteFile", Category = "Annotation", Description = "Loads a keynote file into the project")]
        public static string LoadKeynoteFile(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["keynotePath"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "keynotePath is required"
                    });
                }

                string keynotePath = parameters["keynotePath"].ToString();

                // Check if file exists
                if (!System.IO.File.Exists(keynotePath))
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Keynote file not found: {keynotePath}"
                    });
                }

                // Note: Direct keynote file loading is limited in Revit 2026 API
                // Keynote table path is typically set in Revit UI or through application settings
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = "LoadKeynoteFile is not fully supported in Revit 2026 API - keynote file path must be set through Revit UI settings. Use GetKeynotesInView to work with placed keynotes."
                });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Gets all keynote entries
        /// </summary>
        [MCPMethod("getKeynoteEntries", Category = "Annotation", Description = "Gets all keynote entries from the loaded file")]
        public static string GetKeynoteEntries(UIApplication uiApp, JObject parameters)
        {
            try
            {
                // Note: Revit 2026 API does not provide direct access to iterate KeynoteTable entries
                // The KeynoteTable class is not enumerable and doesn't expose individual entries
                // Keynote data must be accessed through placed keynote tags or by reading the external file
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = "GetKeynoteEntries is not supported in Revit 2026 API - KeynoteTable does not provide enumeration of entries. Use GetKeynotesInView to retrieve placed keynotes, or read the keynote file directly."
                });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Gets keynotes in a view
        /// </summary>
        [MCPMethod("getKeynotesInView", Category = "Annotation", Description = "Gets all keynote tags in a view")]
        public static string GetKeynotesInView(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["viewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "viewId is required"
                    });
                }

                int viewIdInt = parameters["viewId"].ToObject<int>();
                ElementId viewId = new ElementId(viewIdInt);
                View view = doc.GetElement(viewId) as View;

                if (view == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {viewIdInt} not found"
                    });
                }

                // Collect keynote tags in view
                FilteredElementCollector collector = new FilteredElementCollector(doc, viewId)
                    .OfClass(typeof(IndependentTag))
                    .WhereElementIsNotElementType();

                var keynotes = new List<object>();

                foreach (IndependentTag tag in collector)
                {
                    // Check if it's a keynote tag (has keynote text)
                    try
                    {
                        var taggedElementId = tag.GetTaggedLocalElementIds().FirstOrDefault();
                        var taggedElement = taggedElementId != null ? doc.GetElement(taggedElementId) : null;

                        keynotes.Add(new
                        {
                            keynoteId = (int)tag.Id.Value,
                            taggedElementId = taggedElementId != null ? (int)taggedElementId.Value : -1,
                            tagText = tag.TagText,
                            hasLeader = tag.HasLeader,
                            location = new
                            {
                                x = tag.TagHeadPosition.X,
                                y = tag.TagHeadPosition.Y,
                                z = tag.TagHeadPosition.Z
                            }
                        });
                    }
                    catch
                    {
                        // Skip tags that don't have proper references
                        continue;
                    }
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = true,
                    viewId = viewIdInt,
                    keynotesCount = keynotes.Count,
                    keynotes = keynotes
                });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        #endregion

        #region Symbols and Detail Items

        /// <summary>
        /// Places an annotation symbol
        /// </summary>
        [MCPMethod("placeAnnotationSymbol", Category = "Annotation", Description = "Places an annotation symbol in a view")]
        public static string PlaceAnnotationSymbol(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["symbolTypeId"] == null || parameters["viewId"] == null || parameters["location"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "symbolTypeId, viewId, and location are required"
                    });
                }

                int symbolTypeIdInt = parameters["symbolTypeId"].ToObject<int>();
                ElementId symbolTypeId = new ElementId(symbolTypeIdInt);
                FamilySymbol symbolType = doc.GetElement(symbolTypeId) as FamilySymbol;

                if (symbolType == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Annotation symbol type with ID {symbolTypeIdInt} not found"
                    });
                }

                int viewIdInt = parameters["viewId"].ToObject<int>();
                ElementId viewId = new ElementId(viewIdInt);
                View view = doc.GetElement(viewId) as View;

                if (view == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {viewIdInt} not found"
                    });
                }

                var locationArray = parameters["location"].ToObject<double[]>();
                XYZ location = new XYZ(locationArray[0], locationArray[1], locationArray[2]);

                using (var trans = new Transaction(doc, "Place Annotation Symbol"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Activate the symbol if not already active
                    if (!symbolType.IsActive)
                    {
                        symbolType.Activate();
                    }

                    // Create the annotation symbol instance
                    FamilyInstance symbol = doc.Create.NewFamilyInstance(location, symbolType, view);

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        symbolId = (int)symbol.Id.Value,
                        symbolType = symbolType.Name,
                        viewId = viewIdInt,
                        location = new { x = location.X, y = location.Y, z = location.Z }
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Gets all annotation symbol types
        /// </summary>
        [MCPMethod("getAnnotationSymbolTypes", Category = "Annotation", Description = "Gets all annotation symbol types in the document")]
        public static string GetAnnotationSymbolTypes(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Collect all annotation symbol types
                FilteredElementCollector collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_GenericAnnotation);

                var symbolTypes = new List<object>();

                foreach (FamilySymbol symbol in collector)
                {
                    symbolTypes.Add(new
                    {
                        typeId = (int)symbol.Id.Value,
                        typeName = symbol.Name,
                        familyName = symbol.FamilyName,
                        isActive = symbol.IsActive
                    });
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = true,
                    count = symbolTypes.Count,
                    symbolTypes = symbolTypes
                });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        #endregion

        #region Revision Clouds

        /// <summary>
        /// Creates a revision cloud
        /// </summary>
        [MCPMethod("createRevisionCloud", Category = "Annotation", Description = "Creates a revision cloud in a view")]
        public static string CreateRevisionCloud(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["viewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "viewId is required"
                    });
                }

                if (parameters["boundaryPoints"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "boundaryPoints is required"
                    });
                }

                // Parse parameters
                int viewIdInt = parameters["viewId"].ToObject<int>();
                ElementId viewId = new ElementId(viewIdInt);
                View view = doc.GetElement(viewId) as View;

                if (view == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {viewIdInt} not found"
                    });
                }

                // Parse boundary points
                var boundaryPointsArray = parameters["boundaryPoints"].ToObject<double[][]>();
                var curveList = new List<Curve>();

                for (int i = 0; i < boundaryPointsArray.Length; i++)
                {
                    var pt1 = new XYZ(boundaryPointsArray[i][0], boundaryPointsArray[i][1], 0);
                    var pt2 = new XYZ(
                        boundaryPointsArray[(i + 1) % boundaryPointsArray.Length][0],
                        boundaryPointsArray[(i + 1) % boundaryPointsArray.Length][1],
                        0
                    );

                    curveList.Add(Line.CreateBound(pt1, pt2));
                }

                // Optional revision ID
                ElementId revisionId = null;
                if (parameters["revisionId"] != null)
                {
                    int revisionIdInt = parameters["revisionId"].ToObject<int>();
                    revisionId = new ElementId(revisionIdInt);
                }

                using (var trans = new Transaction(doc, "Create Revision Cloud"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Create revision cloud
                    RevisionCloud cloud = RevisionCloud.Create(doc, view, revisionId, curveList);

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        cloudId = (int)cloud.Id.Value,
                        viewId = viewIdInt,
                        revisionId = revisionId != null ? (int)revisionId.Value : -1,
                        segmentCount = curveList.Count
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Gets all revision clouds in a view
        /// </summary>
        [MCPMethod("getRevisionCloudsInView", Category = "Annotation", Description = "Gets all revision clouds in a view")]
        public static string GetRevisionCloudsInView(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["viewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "viewId is required"
                    });
                }

                int viewIdInt = parameters["viewId"].ToObject<int>();
                ElementId viewId = new ElementId(viewIdInt);
                View view = doc.GetElement(viewId) as View;

                if (view == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {viewIdInt} not found"
                    });
                }

                // Collect revision clouds in view
                FilteredElementCollector collector = new FilteredElementCollector(doc, viewId)
                    .OfClass(typeof(RevisionCloud));

                var clouds = new List<object>();

                foreach (RevisionCloud cloud in collector)
                {
                    var revision = doc.GetElement(cloud.RevisionId) as Revision;

                    clouds.Add(new
                    {
                        cloudId = (int)cloud.Id.Value,
                        revisionId = cloud.RevisionId != null ? (int)cloud.RevisionId.Value : -1,
                        revisionDescription = revision != null ? revision.Description : "None",
                        revisionNumber = revision != null ? revision.RevisionNumber : ""
                    });
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = true,
                    viewId = viewIdInt,
                    cloudsCount = clouds.Count,
                    clouds = clouds
                });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Modifies a revision cloud boundary
        /// </summary>
        [MCPMethod("modifyRevisionCloud", Category = "Annotation", Description = "Modifies an existing revision cloud")]
        public static string ModifyRevisionCloud(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["cloudId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "cloudId is required"
                    });
                }

                if (parameters["newBoundaryPoints"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "newBoundaryPoints is required"
                    });
                }

                int cloudIdInt = parameters["cloudId"].ToObject<int>();
                ElementId cloudId = new ElementId(cloudIdInt);
                RevisionCloud cloud = doc.GetElement(cloudId) as RevisionCloud;

                if (cloud == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Revision cloud with ID {cloudIdInt} not found"
                    });
                }

                // Parse new boundary points
                var boundaryPointsArray = parameters["newBoundaryPoints"].ToObject<double[][]>();
                var curveList = new List<Curve>();

                for (int i = 0; i < boundaryPointsArray.Length; i++)
                {
                    var pt1 = new XYZ(boundaryPointsArray[i][0], boundaryPointsArray[i][1], 0);
                    var pt2 = new XYZ(
                        boundaryPointsArray[(i + 1) % boundaryPointsArray.Length][0],
                        boundaryPointsArray[(i + 1) % boundaryPointsArray.Length][1],
                        0
                    );

                    curveList.Add(Line.CreateBound(pt1, pt2));
                }

                using (var trans = new Transaction(doc, "Modify Revision Cloud"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Delete old cloud and create new one with same revision
                    ElementId revisionId = cloud.RevisionId;
                    ElementId viewId = cloud.OwnerViewId;
                    View view = doc.GetElement(viewId) as View;

                    doc.Delete(cloudId);

                    // Create new cloud with updated boundary
                    RevisionCloud newCloud = RevisionCloud.Create(doc, view, revisionId, curveList);

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        cloudId = (int)newCloud.Id.Value,
                        revisionId = revisionId != null ? (int)revisionId.Value : -1,
                        segmentCount = curveList.Count,
                        message = "Revision cloud boundary updated"
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Deletes a revision cloud
        /// </summary>
        [MCPMethod("deleteRevisionCloud", Category = "Annotation", Description = "Deletes a revision cloud from the document")]
        public static string DeleteRevisionCloud(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["cloudId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "cloudId is required"
                    });
                }

                int cloudIdInt = parameters["cloudId"].ToObject<int>();
                ElementId cloudId = new ElementId(cloudIdInt);
                RevisionCloud cloud = doc.GetElement(cloudId) as RevisionCloud;

                if (cloud == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Revision cloud with ID {cloudIdInt} not found"
                    });
                }

                using (var trans = new Transaction(doc, "Delete Revision Cloud"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    doc.Delete(cloudId);

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        deletedCloudId = cloudIdInt,
                        message = "Revision cloud deleted successfully"
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        #endregion

        #region Revisions

        /// <summary>
        /// Creates a new revision
        /// </summary>
        [MCPMethod("createRevision", Category = "Annotation", Description = "Creates a new revision in the project")]
        public static string CreateRevision(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["description"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "description is required"
                    });
                }

                string description = parameters["description"].ToString();

                using (var trans = new Transaction(doc, "Create Revision"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Create new revision
                    Revision revision = Revision.Create(doc);

                    // Set description
                    revision.Description = description;

                    // Set optional parameters if provided
                    if (parameters["date"] != null)
                    {
                        string dateStr = parameters["date"].ToString();
                        revision.RevisionDate = dateStr;
                    }

                    if (parameters["issuedTo"] != null)
                    {
                        string issuedTo = parameters["issuedTo"].ToString();
                        revision.IssuedTo = issuedTo;
                    }

                    if (parameters["issuedBy"] != null)
                    {
                        string issuedBy = parameters["issuedBy"].ToString();
                        revision.IssuedBy = issuedBy;
                    }

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        revisionId = (int)revision.Id.Value,
                        sequenceNumber = revision.SequenceNumber,
                        revisionNumber = revision.RevisionNumber,
                        description = revision.Description,
                        date = revision.RevisionDate,
                        issuedTo = revision.IssuedTo,
                        issuedBy = revision.IssuedBy,
                        issued = revision.Issued
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Gets all revisions in the project
        /// </summary>
        [MCPMethod("getAllRevisions", Category = "Annotation", Description = "Gets all revisions in the project")]
        public static string GetAllRevisions(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Collect all revisions
                FilteredElementCollector collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(Revision));

                var revisions = new List<object>();

                foreach (Revision revision in collector)
                {
                    revisions.Add(new
                    {
                        revisionId = (int)revision.Id.Value,
                        sequenceNumber = revision.SequenceNumber,
                        revisionNumber = revision.RevisionNumber,
                        description = revision.Description,
                        date = revision.RevisionDate,
                        issuedTo = revision.IssuedTo,
                        issuedBy = revision.IssuedBy,
                        issued = revision.Issued,
                        visibility = revision.Visibility.ToString()
                    });
                }

                // Sort by sequence number
                revisions = revisions.OrderBy(r => ((dynamic)r).sequenceNumber).ToList();

                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = true,
                    revisionsCount = revisions.Count,
                    revisions = revisions
                });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Modifies revision properties
        /// </summary>
        [MCPMethod("modifyRevision", Category = "Annotation", Description = "Modifies revision properties")]
        public static string ModifyRevision(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["revisionId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "revisionId is required"
                    });
                }

                int revisionIdInt = parameters["revisionId"].ToObject<int>();
                ElementId revisionId = new ElementId(revisionIdInt);
                Revision revision = doc.GetElement(revisionId) as Revision;

                if (revision == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Revision with ID {revisionIdInt} not found"
                    });
                }

                using (var trans = new Transaction(doc, "Modify Revision"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Update properties if provided
                    if (parameters["description"] != null)
                    {
                        revision.Description = parameters["description"].ToString();
                    }

                    if (parameters["date"] != null)
                    {
                        revision.RevisionDate = parameters["date"].ToString();
                    }

                    if (parameters["issuedTo"] != null)
                    {
                        revision.IssuedTo = parameters["issuedTo"].ToString();
                    }

                    if (parameters["issuedBy"] != null)
                    {
                        revision.IssuedBy = parameters["issuedBy"].ToString();
                    }

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        revisionId = revisionIdInt,
                        sequenceNumber = revision.SequenceNumber,
                        revisionNumber = revision.RevisionNumber,
                        description = revision.Description,
                        date = revision.RevisionDate,
                        issuedTo = revision.IssuedTo,
                        issuedBy = revision.IssuedBy,
                        issued = revision.Issued,
                        message = "Revision updated successfully"
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Issues or unissues a revision
        /// </summary>
        [MCPMethod("setRevisionIssued", Category = "Annotation", Description = "Sets the issued state of a revision")]
        public static string SetRevisionIssued(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["revisionId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "revisionId is required"
                    });
                }

                if (parameters["issued"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "issued is required (boolean)"
                    });
                }

                int revisionIdInt = parameters["revisionId"].ToObject<int>();
                ElementId revisionId = new ElementId(revisionIdInt);
                Revision revision = doc.GetElement(revisionId) as Revision;

                if (revision == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Revision with ID {revisionIdInt} not found"
                    });
                }

                bool issued = parameters["issued"].ToObject<bool>();

                using (var trans = new Transaction(doc, "Set Revision Issued Status"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    revision.Issued = issued;

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        revisionId = revisionIdInt,
                        issued = revision.Issued,
                        revisionNumber = revision.RevisionNumber,
                        description = revision.Description,
                        message = issued ? "Revision marked as issued" : "Revision marked as not issued"
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        #endregion

        #region Dimensions (Additional)

        /// <summary>
        /// Places an angular dimension
        /// </summary>
        [MCPMethod("placeAngularDimension", Category = "Annotation", Description = "Places an angular dimension in a view")]
        public static string PlaceAngularDimension(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["viewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "viewId is required"
                    });
                }

                if (parameters["dimensionLine"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "dimensionLine is required (array of two points [[x1,y1,z1], [x2,y2,z2]])"
                    });
                }

                if (parameters["referenceIds"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "referenceIds is required (array of element IDs)"
                    });
                }

                // Parse parameters
                int viewIdInt = parameters["viewId"].ToObject<int>();
                ElementId viewId = new ElementId(viewIdInt);
                View view = doc.GetElement(viewId) as View;

                if (view == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {viewIdInt} not found"
                    });
                }

                // Parse reference IDs
                var referenceIdsArray = parameters["referenceIds"].ToObject<int[]>();
                ReferenceArray references = new ReferenceArray();

                foreach (int refId in referenceIdsArray)
                {
                    Element elem = doc.GetElement(new ElementId(refId));
                    if (elem != null)
                    {
                        references.Append(new Reference(elem));
                    }
                }

                // Parse dimension line location
                var lineArray = parameters["dimensionLine"].ToObject<double[][]>();
                XYZ point1 = new XYZ(lineArray[0][0], lineArray[0][1], lineArray[0][2]);
                XYZ point2 = new XYZ(lineArray[1][0], lineArray[1][1], lineArray[1][2]);
                Line dimensionLine = Line.CreateBound(point1, point2);

                using (var trans = new Transaction(doc, "Place Angular Dimension"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Create angular dimension
                    Dimension dimension = doc.Create.NewDimension(view, dimensionLine, references);

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        dimensionId = (int)dimension.Id.Value,
                        viewId = viewIdInt,
                        referencesCount = references.Size
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Places a radial dimension
        /// </summary>
        [MCPMethod("placeRadialDimension", Category = "Annotation", Description = "Places a radial dimension in a view")]
        public static string PlaceRadialDimension(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["viewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "viewId is required"
                    });
                }

                if (parameters["referenceId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "referenceId is required"
                    });
                }

                if (parameters["center"] == null || parameters["point"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "center and point are required (arrays of [x, y, z])"
                    });
                }

                // Parse parameters
                int viewIdInt = parameters["viewId"].ToObject<int>();
                ElementId viewId = new ElementId(viewIdInt);
                View view = doc.GetElement(viewId) as View;

                if (view == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {viewIdInt} not found"
                    });
                }

                int referenceIdInt = parameters["referenceId"].ToObject<int>();
                ElementId referenceId = new ElementId(referenceIdInt);
                Element referenceElement = doc.GetElement(referenceId);

                if (referenceElement == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Reference element with ID {referenceIdInt} not found"
                    });
                }

                var centerArray = parameters["center"].ToObject<double[]>();
                XYZ center = new XYZ(centerArray[0], centerArray[1], centerArray[2]);

                var pointArray = parameters["point"].ToObject<double[]>();
                XYZ point = new XYZ(pointArray[0], pointArray[1], pointArray[2]);

                using (var trans = new Transaction(doc, "Place Radial Dimension"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Create line for radial dimension
                    Line line = Line.CreateBound(center, point);

                    // Create reference
                    Reference reference = new Reference(referenceElement);
                    ReferenceArray references = new ReferenceArray();
                    references.Append(reference);

                    // Create radial dimension
                    Dimension dimension = doc.Create.NewDimension(view, line, references);

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        dimensionId = (int)dimension.Id.Value,
                        viewId = viewIdInt,
                        referenceId = referenceIdInt
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Places a diameter dimension
        /// </summary>
        [MCPMethod("placeDiameterDimension", Category = "Annotation", Description = "Places a diameter dimension in a view")]
        public static string PlaceDiameterDimension(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["viewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "viewId is required"
                    });
                }

                if (parameters["referenceId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "referenceId is required"
                    });
                }

                if (parameters["center"] == null || parameters["point"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "center and point are required (arrays of [x, y, z])"
                    });
                }

                // Parse parameters
                int viewIdInt = parameters["viewId"].ToObject<int>();
                ElementId viewId = new ElementId(viewIdInt);
                View view = doc.GetElement(viewId) as View;

                if (view == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {viewIdInt} not found"
                    });
                }

                int referenceIdInt = parameters["referenceId"].ToObject<int>();
                ElementId referenceId = new ElementId(referenceIdInt);
                Element referenceElement = doc.GetElement(referenceId);

                if (referenceElement == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Reference element with ID {referenceIdInt} not found"
                    });
                }

                var centerArray = parameters["center"].ToObject<double[]>();
                XYZ center = new XYZ(centerArray[0], centerArray[1], centerArray[2]);

                var pointArray = parameters["point"].ToObject<double[]>();
                XYZ point = new XYZ(pointArray[0], pointArray[1], pointArray[2]);

                using (var trans = new Transaction(doc, "Place Diameter Dimension"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Create line for diameter dimension (goes through center)
                    XYZ oppositePoint = center + (center - point);
                    Line line = Line.CreateBound(point, oppositePoint);

                    // Create reference
                    Reference reference = new Reference(referenceElement);
                    ReferenceArray references = new ReferenceArray();
                    references.Append(reference);

                    // Create diameter dimension
                    Dimension dimension = doc.Create.NewDimension(view, line, references);

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        dimensionId = (int)dimension.Id.Value,
                        viewId = viewIdInt,
                        referenceId = referenceIdInt
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Places an arc length dimension
        /// </summary>
        [MCPMethod("placeArcLengthDimension", Category = "Annotation", Description = "Places an arc length dimension in a view")]
        public static string PlaceArcLengthDimension(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["viewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "viewId is required"
                    });
                }

                if (parameters["referenceId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "referenceId is required"
                    });
                }

                if (parameters["arcLine"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "arcLine is required (array of two points [[x1,y1,z1], [x2,y2,z2]])"
                    });
                }

                // Parse parameters
                int viewIdInt = parameters["viewId"].ToObject<int>();
                ElementId viewId = new ElementId(viewIdInt);
                View view = doc.GetElement(viewId) as View;

                if (view == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {viewIdInt} not found"
                    });
                }

                int referenceIdInt = parameters["referenceId"].ToObject<int>();
                ElementId referenceId = new ElementId(referenceIdInt);
                Element referenceElement = doc.GetElement(referenceId);

                if (referenceElement == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Reference element with ID {referenceIdInt} not found"
                    });
                }

                // Parse arc line
                var lineArray = parameters["arcLine"].ToObject<double[][]>();
                XYZ point1 = new XYZ(lineArray[0][0], lineArray[0][1], lineArray[0][2]);
                XYZ point2 = new XYZ(lineArray[1][0], lineArray[1][1], lineArray[1][2]);
                Line arcLine = Line.CreateBound(point1, point2);

                using (var trans = new Transaction(doc, "Place Arc Length Dimension"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Create reference
                    Reference reference = new Reference(referenceElement);
                    ReferenceArray references = new ReferenceArray();
                    references.Append(reference);

                    // Create arc length dimension
                    Dimension dimension = doc.Create.NewDimension(view, arcLine, references);

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        dimensionId = (int)dimension.Id.Value,
                        viewId = viewIdInt,
                        referenceId = referenceIdInt
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        #endregion

        #region Callouts and Sections

        /// <summary>
        /// Creates a callout view
        /// </summary>
        [MCPMethod("createCallout", Category = "Annotation", Description = "Creates a callout view in a parent view")]
        public static string CreateCallout(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["parentViewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "parentViewId is required"
                    });
                }

                if (parameters["minPoint"] == null || parameters["maxPoint"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "minPoint and maxPoint are required (arrays of [x, y])"
                    });
                }

                // Parse parameters
                int parentViewIdInt = parameters["parentViewId"].ToObject<int>();
                ElementId parentViewId = new ElementId(parentViewIdInt);
                View parentView = doc.GetElement(parentViewId) as View;

                if (parentView == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Parent view with ID {parentViewIdInt} not found"
                    });
                }

                var minArray = parameters["minPoint"].ToObject<double[]>();
                var maxArray = parameters["maxPoint"].ToObject<double[]>();
                XYZ min = new XYZ(minArray[0], minArray[1], 0);
                XYZ max = new XYZ(maxArray[0], maxArray[1], 0);

                // Optional callout type ID
                ElementId calloutTypeId = null;
                if (parameters["calloutTypeId"] != null)
                {
                    int typeIdInt = parameters["calloutTypeId"].ToObject<int>();
                    calloutTypeId = new ElementId(typeIdInt);
                }

                using (var trans = new Transaction(doc, "Create Callout"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Create callout view (returns View, cast to ViewSection)
                    View calloutView = ViewSection.CreateCallout(doc, parentViewId, calloutTypeId, min, max);
                    ViewSection callout = calloutView as ViewSection;

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        calloutId = (int)callout.Id.Value,
                        calloutName = callout.Name,
                        parentViewId = parentViewIdInt
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Gets callouts in a view
        /// </summary>
        [MCPMethod("getCalloutsInView", Category = "Annotation", Description = "Gets all callout views in a parent view")]
        public static string GetCalloutsInView(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["viewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "viewId is required"
                    });
                }

                int viewIdInt = parameters["viewId"].ToObject<int>();
                ElementId viewId = new ElementId(viewIdInt);
                View view = doc.GetElement(viewId) as View;

                if (view == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {viewIdInt} not found"
                    });
                }

                // Get all views and find callouts referencing this view
                FilteredElementCollector collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSection));

                var callouts = new List<object>();

                foreach (ViewSection viewSection in collector)
                {
                    // Check if this is a callout view (callouts have ViewType == ViewType.Detail or similar)
                    // Note: In Revit 2026, use Parameter to get parent view
                    try
                    {
                        Parameter parentViewParam = viewSection.get_Parameter(BuiltInParameter.SECTION_PARENT_VIEW_NAME);
                        if (parentViewParam != null && !string.IsNullOrEmpty(parentViewParam.AsString()))
                        {
                            callouts.Add(new
                            {
                                calloutId = (int)viewSection.Id.Value,
                                calloutName = viewSection.Name,
                                parentViewName = parentViewParam.AsString(),
                                viewType = viewSection.ViewType.ToString()
                            });
                        }
                    }
                    catch { continue; }
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = true,
                    viewId = viewIdInt,
                    calloutsCount = callouts.Count,
                    callouts = callouts
                });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        #endregion

        #region Area and Room Tags (Additional)

        /// <summary>
        /// Places an area tag
        /// </summary>
        [MCPMethod("placeAreaTag", Category = "Annotation", Description = "Places an area tag in an area plan view")]
        public static string PlaceAreaTag(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["areaId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "areaId is required"
                    });
                }

                if (parameters["viewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "viewId is required"
                    });
                }

                if (parameters["location"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "location is required (array of [x, y, z])"
                    });
                }

                // Parse parameters
                int areaIdInt = parameters["areaId"].ToObject<int>();
                ElementId areaId = new ElementId(areaIdInt);
                Area area = doc.GetElement(areaId) as Area;

                if (area == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Area with ID {areaIdInt} not found"
                    });
                }

                int viewIdInt = parameters["viewId"].ToObject<int>();
                ElementId viewId = new ElementId(viewIdInt);
                View view = doc.GetElement(viewId) as View;

                if (view == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {viewIdInt} not found"
                    });
                }

                // NewAreaTag requires ViewPlan, not View
                ViewPlan viewPlan = view as ViewPlan;
                if (viewPlan == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {viewIdInt} is not a ViewPlan (area tags require area plans)"
                    });
                }

                var locationArray = parameters["location"].ToObject<double[]>();
                UV location = new UV(locationArray[0], locationArray[1]);

                using (var trans = new Transaction(doc, "Place Area Tag"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Create area tag
                    AreaTag areaTag = doc.Create.NewAreaTag(viewPlan, area, location);

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        areaTagId = (int)areaTag.Id.Value,
                        areaId = areaIdInt,
                        viewId = viewIdInt,
                        location = new { u = location.U, v = location.V }
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Gets area tags in a view
        /// </summary>
        [MCPMethod("getAreaTagsInView", Category = "Annotation", Description = "Gets all area tags in a view")]
        public static string GetAreaTagsInView(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["viewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "viewId is required"
                    });
                }

                int viewIdInt = parameters["viewId"].ToObject<int>();
                ElementId viewId = new ElementId(viewIdInt);
                View view = doc.GetElement(viewId) as View;

                if (view == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {viewIdInt} not found"
                    });
                }

                // Collect area tags in view
                FilteredElementCollector collector = new FilteredElementCollector(doc, viewId)
                    .OfClass(typeof(AreaTag));

                var areaTags = new List<object>();

                foreach (AreaTag areaTag in collector)
                {
                    try
                    {
                        var taggedArea = doc.GetElement(areaTag.Area.Id) as Area;

                        areaTags.Add(new
                        {
                            areaTagId = (int)areaTag.Id.Value,
                            areaId = taggedArea != null ? (int)taggedArea.Id.Value : -1,
                            hasLeader = areaTag.HasLeader,
                            location = areaTag.TagHeadPosition != null ? new
                            {
                                x = areaTag.TagHeadPosition.X,
                                y = areaTag.TagHeadPosition.Y,
                                z = areaTag.TagHeadPosition.Z
                            } : null
                        });
                    }
                    catch
                    {
                        continue;
                    }
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = true,
                    viewId = viewIdInt,
                    areaTagsCount = areaTags.Count,
                    areaTags = areaTags
                });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        #endregion

        #region Spot Elevations and Coordinates

        /// <summary>
        /// Places a spot elevation
        /// </summary>
        [MCPMethod("placeSpotElevation", Category = "Annotation", Description = "Places a spot elevation annotation")]
        public static string PlaceSpotElevation(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["viewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "viewId is required"
                    });
                }

                if (parameters["referenceId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "referenceId is required"
                    });
                }

                if (parameters["location"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "location is required (array of [x, y, z])"
                    });
                }

                // Parse parameters
                int viewIdInt = parameters["viewId"].ToObject<int>();
                ElementId viewId = new ElementId(viewIdInt);
                View view = doc.GetElement(viewId) as View;

                if (view == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {viewIdInt} not found"
                    });
                }

                int referenceIdInt = parameters["referenceId"].ToObject<int>();
                ElementId referenceId = new ElementId(referenceIdInt);
                Element referenceElement = doc.GetElement(referenceId);

                if (referenceElement == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Reference element with ID {referenceIdInt} not found"
                    });
                }

                var locationArray = parameters["location"].ToObject<double[]>();
                XYZ location = new XYZ(locationArray[0], locationArray[1], locationArray[2]);

                using (var trans = new Transaction(doc, "Place Spot Elevation"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Get reference from element
                    Reference reference = new Reference(referenceElement);

                    // Create spot elevation
                    SpotDimension spotElevation = doc.Create.NewSpotElevation(view, reference, location, XYZ.Zero, XYZ.Zero, location, true);

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        spotElevationId = (int)spotElevation.Id.Value,
                        viewId = viewIdInt,
                        referenceId = referenceIdInt,
                        location = new { x = location.X, y = location.Y, z = location.Z }
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Places a spot coordinate
        /// </summary>
        [MCPMethod("placeSpotCoordinate", Category = "Annotation", Description = "Places a spot coordinate annotation")]
        public static string PlaceSpotCoordinate(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["viewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "viewId is required"
                    });
                }

                if (parameters["referenceId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "referenceId is required"
                    });
                }

                if (parameters["location"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "location is required (array of [x, y, z])"
                    });
                }

                // Parse parameters
                int viewIdInt = parameters["viewId"].ToObject<int>();
                ElementId viewId = new ElementId(viewIdInt);
                View view = doc.GetElement(viewId) as View;

                if (view == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {viewIdInt} not found"
                    });
                }

                int referenceIdInt = parameters["referenceId"].ToObject<int>();
                ElementId referenceId = new ElementId(referenceIdInt);
                Element referenceElement = doc.GetElement(referenceId);

                if (referenceElement == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Reference element with ID {referenceIdInt} not found"
                    });
                }

                var locationArray = parameters["location"].ToObject<double[]>();
                XYZ location = new XYZ(locationArray[0], locationArray[1], locationArray[2]);

                using (var trans = new Transaction(doc, "Place Spot Coordinate"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Get reference from element
                    Reference reference = new Reference(referenceElement);

                    // Create spot coordinate
                    SpotDimension spotCoordinate = doc.Create.NewSpotCoordinate(view, reference, location, XYZ.Zero, XYZ.Zero, location, true);

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        spotCoordinateId = (int)spotCoordinate.Id.Value,
                        viewId = viewIdInt,
                        referenceId = referenceIdInt,
                        location = new { x = location.X, y = location.Y, z = location.Z }
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Places a spot slope
        /// </summary>
        [MCPMethod("placeSpotSlope", Category = "Annotation", Description = "Places a spot slope annotation")]
        public static string PlaceSpotSlope(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["viewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "viewId is required"
                    });
                }

                if (parameters["referenceId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "referenceId is required"
                    });
                }

                if (parameters["location"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "location is required (array of [x, y, z])"
                    });
                }

                // Parse parameters
                int viewIdInt = parameters["viewId"].ToObject<int>();
                ElementId viewId = new ElementId(viewIdInt);
                View view = doc.GetElement(viewId) as View;

                if (view == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {viewIdInt} not found"
                    });
                }

                int referenceIdInt = parameters["referenceId"].ToObject<int>();
                ElementId referenceId = new ElementId(referenceIdInt);
                Element referenceElement = doc.GetElement(referenceId);

                if (referenceElement == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Reference element with ID {referenceIdInt} not found"
                    });
                }

                var locationArray = parameters["location"].ToObject<double[]>();
                XYZ location = new XYZ(locationArray[0], locationArray[1], locationArray[2]);

                // Note: NewSpotSlope method does not exist in Revit 2026 API
                // Spot slope dimensions may need to be created using a different approach
                // or may not be supported via the API
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = "PlaceSpotSlope is not currently supported in Revit 2026 API - the Document.Create.NewSpotSlope method does not exist. Consider using PlaceSpotElevation or PlaceSpotCoordinate instead."
                });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        #endregion

        #region Reference Planes

        /// <summary>
        /// Creates a reference plane
        /// </summary>
        [MCPMethod("createReferencePlane", Category = "Annotation", Description = "Creates a reference plane in a view")]
        public static string CreateReferencePlane(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["bubbleEnd"] == null || parameters["freeEnd"] == null || parameters["cutVector"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "bubbleEnd, freeEnd, and cutVector are required"
                    });
                }

                var bubbleArray = parameters["bubbleEnd"].ToObject<double[]>();
                XYZ bubbleEnd = new XYZ(bubbleArray[0], bubbleArray[1], bubbleArray[2]);

                var freeArray = parameters["freeEnd"].ToObject<double[]>();
                XYZ freeEnd = new XYZ(freeArray[0], freeArray[1], freeArray[2]);

                var cutArray = parameters["cutVector"].ToObject<double[]>();
                XYZ cutVector = new XYZ(cutArray[0], cutArray[1], cutArray[2]);

                using (var trans = new Transaction(doc, "Create Reference Plane"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Create reference plane
                    ReferencePlane refPlane = doc.Create.NewReferencePlane(bubbleEnd, freeEnd, cutVector, doc.ActiveView);

                    // Optional: Set name if provided
                    if (parameters["name"] != null)
                    {
                        refPlane.Name = parameters["name"].ToString();
                    }

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        referencePlaneId = (int)refPlane.Id.Value,
                        name = refPlane.Name,
                        bubbleEnd = new { x = bubbleEnd.X, y = bubbleEnd.Y, z = bubbleEnd.Z },
                        freeEnd = new { x = freeEnd.X, y = freeEnd.Y, z = freeEnd.Z }
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Gets reference planes in a view
        /// </summary>
        [MCPMethod("getReferencePlanesInView", Category = "Annotation", Description = "Gets all reference planes in a view")]
        public static string GetReferencePlanesInView(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Collect all reference planes
                FilteredElementCollector collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(ReferencePlane));

                var referencePlanes = new List<object>();

                foreach (ReferencePlane refPlane in collector)
                {
                    referencePlanes.Add(new
                    {
                        referencePlaneId = (int)refPlane.Id.Value,
                        name = refPlane.Name,
                        bubbleEnd = refPlane.BubbleEnd != null ? new
                        {
                            x = refPlane.BubbleEnd.X,
                            y = refPlane.BubbleEnd.Y,
                            z = refPlane.BubbleEnd.Z
                        } : null,
                        freeEnd = refPlane.FreeEnd != null ? new
                        {
                            x = refPlane.FreeEnd.X,
                            y = refPlane.FreeEnd.Y,
                            z = refPlane.FreeEnd.Z
                        } : null
                    });
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = true,
                    count = referencePlanes.Count,
                    referencePlanes = referencePlanes
                });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        #endregion

        #region Matchlines

        /// <summary>
        /// Creates a matchline
        /// </summary>
        [MCPMethod("createMatchline", Category = "Annotation", Description = "Creates a matchline in a view")]
        public static string CreateMatchline(UIApplication uiApp, JObject parameters)
        {
            try
            {
                // Note: Matchlines in Revit are typically created through the UI
                // The API support for programmatic matchline creation is limited
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = "Matchline creation is not fully supported in Revit 2026 API. Matchlines are typically created through the UI in dependent views."
                });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Gets matchlines in a view
        /// </summary>
        [MCPMethod("getMatchlinesInView", Category = "Annotation", Description = "Gets all matchlines in a view")]
        public static string GetMatchlinesInView(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                if (parameters["viewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "viewId is required"
                    });
                }

                int viewIdInt = parameters["viewId"].ToObject<int>();
                ElementId viewId = new ElementId(viewIdInt);

                // Collect matchlines in view
                FilteredElementCollector collector = new FilteredElementCollector(doc, viewId)
                    .OfCategory(BuiltInCategory.OST_Matchline);

                var matchlines = new List<object>();

                foreach (Element matchline in collector)
                {
                    matchlines.Add(new
                    {
                        matchlineId = (int)matchline.Id.Value,
                        name = matchline.Name
                    });
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = true,
                    viewId = viewIdInt,
                    count = matchlines.Count,
                    matchlines = matchlines
                });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        #endregion

        #region Legend Components

        /// <summary>
        /// Places a legend component
        /// </summary>
        [MCPMethod("placeLegendComponent", Category = "Annotation", Description = "Places a legend component in a legend view")]
        public static string PlaceLegendComponent(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                if (parameters["legendViewId"] == null || parameters["familyId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "legendViewId and familyId are required"
                    });
                }

                int legendViewIdInt = parameters["legendViewId"].ToObject<int>();
                ElementId legendViewId = new ElementId(legendViewIdInt);
                View legendView = doc.GetElement(legendViewId) as View;

                if (legendView == null || legendView.ViewType != ViewType.Legend)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {legendViewIdInt} is not a legend view"
                    });
                }

                int familyIdInt = parameters["familyId"].ToObject<int>();
                ElementId familyId = new ElementId(familyIdInt);

                var x = parameters["x"]?.Value<double>() ?? 0;
                var y = parameters["y"]?.Value<double>() ?? 0;
                var position = new XYZ(x, y, 0);

                // Get the family symbol (type) to place
                var typeId = parameters["typeId"]?.Value<int>();
                FamilySymbol symbol = null;

                if (typeId.HasValue)
                {
                    symbol = doc.GetElement(new ElementId(typeId.Value)) as FamilySymbol;
                }
                else
                {
                    // Try to find a symbol from the given family
                    var family = doc.GetElement(familyId) as Family;
                    if (family != null)
                    {
                        var symbolIds = family.GetFamilySymbolIds();
                        if (symbolIds.Count > 0)
                        {
                            symbol = doc.GetElement(symbolIds.First()) as FamilySymbol;
                        }
                    }
                    else
                    {
                        // familyId might be a symbol ID directly
                        symbol = doc.GetElement(familyId) as FamilySymbol;
                    }
                }

                if (symbol == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "Could not resolve family symbol. Provide typeId or a valid familyId."
                    });
                }

                using (var trans = new Transaction(doc, "Place Legend Component"))
                {
                    trans.Start();

                    if (!symbol.IsActive) symbol.Activate();

                    // Place in legend view using NewFamilyInstance
                    var instance = doc.Create.NewFamilyInstance(
                        position, symbol, legendView);

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        elementId = (int)instance.Id.Value,
                        familyName = symbol.Family?.Name,
                        typeName = symbol.Name,
                        position = new { x, y }
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Gets legend components in a legend view
        /// </summary>
        [MCPMethod("getLegendComponents", Category = "Annotation", Description = "Gets all legend components in a legend view")]
        public static string GetLegendComponents(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                if (parameters["legendViewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "legendViewId is required"
                    });
                }

                int legendViewIdInt = parameters["legendViewId"].ToObject<int>();
                ElementId legendViewId = new ElementId(legendViewIdInt);

                // Collect legend components in view
                FilteredElementCollector collector = new FilteredElementCollector(doc, legendViewId)
                    .OfClass(typeof(Element))
                    .OfCategory(BuiltInCategory.OST_LegendComponents);

                var legendComponents = new List<object>();

                foreach (Element component in collector)
                {
                    legendComponents.Add(new
                    {
                        componentId = (int)component.Id.Value,
                        category = component.Category != null ? component.Category.Name : "Unknown"
                    });
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = true,
                    legendViewId = legendViewIdInt,
                    count = legendComponents.Count,
                    legendComponents = legendComponents
                });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Gets all annotations in a view
        /// </summary>
        [MCPMethod("getAllAnnotationsInView", Category = "Annotation", Description = "Gets all annotation elements in a view")]
        public static string GetAllAnnotationsInView(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                if (parameters["viewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "viewId is required"
                    });
                }

                int viewIdInt = parameters["viewId"].ToObject<int>();
                ElementId viewId = new ElementId(viewIdInt);
                View view = doc.GetElement(viewId) as View;

                if (view == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {viewIdInt} not found"
                    });
                }

                // Collect various annotation types
                var annotations = new List<object>();

                // Text notes
                foreach (TextNote textNote in new FilteredElementCollector(doc, viewId).OfClass(typeof(TextNote)))
                {
                    annotations.Add(new
                    {
                        id = (int)textNote.Id.Value,
                        type = "TextNote",
                        text = textNote.Text
                    });
                }

                // Dimensions
                foreach (Dimension dim in new FilteredElementCollector(doc, viewId).OfClass(typeof(Dimension)))
                {
                    annotations.Add(new
                    {
                        id = (int)dim.Id.Value,
                        type = "Dimension",
                        dimensionType = dim.DimensionType.Name
                    });
                }

                // Tags (Independent tags)
                foreach (IndependentTag tag in new FilteredElementCollector(doc, viewId).OfClass(typeof(IndependentTag)))
                {
                    annotations.Add(new
                    {
                        id = (int)tag.Id.Value,
                        type = "IndependentTag",
                        tagText = tag.TagText
                    });
                }

                // Spot dimensions
                foreach (SpotDimension spot in new FilteredElementCollector(doc, viewId).OfClass(typeof(SpotDimension)))
                {
                    annotations.Add(new
                    {
                        id = (int)spot.Id.Value,
                        type = "SpotDimension"
                    });
                }

                // Area tags
                foreach (AreaTag areaTag in new FilteredElementCollector(doc, viewId).OfClass(typeof(AreaTag)))
                {
                    annotations.Add(new
                    {
                        id = (int)areaTag.Id.Value,
                        type = "AreaTag"
                    });
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = true,
                    viewId = viewIdInt,
                    count = annotations.Count,
                    annotations = annotations
                });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Deletes an annotation element
        /// </summary>
        [MCPMethod("deleteAnnotation", Category = "Annotation", Description = "Deletes an annotation element from the document")]
        public static string DeleteAnnotation(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                if (parameters["annotationId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "annotationId is required"
                    });
                }

                int annotationIdInt = parameters["annotationId"].ToObject<int>();
                ElementId annotationId = new ElementId(annotationIdInt);

                Element annotation = doc.GetElement(annotationId);

                if (annotation == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Annotation with ID {annotationIdInt} not found"
                    });
                }

                using (var trans = new Transaction(doc, "Delete Annotation"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Delete the annotation
                    doc.Delete(annotationId);

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        deletedId = annotationIdInt,
                        message = "Annotation deleted successfully"
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        #endregion

        #region Leader Management

        /// <summary>
        /// Adds or enables a leader on an annotation symbol (generic annotation family instance)
        /// </summary>
        [MCPMethod("addAnnotationLeader", Category = "Annotation", Description = "Adds a leader to an existing annotation tag")]
        public static string AddAnnotationLeader(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                if (parameters["annotationId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "annotationId is required"
                    });
                }

                int annotationIdInt = parameters["annotationId"].ToObject<int>();
                ElementId annotationId = new ElementId(annotationIdInt);
                Element element = doc.GetElement(annotationId);

                if (element == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Annotation with ID {annotationIdInt} not found"
                    });
                }

                using (var trans = new Transaction(doc, "Add Annotation Leader"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    bool leaderAdded = false;
                    object leaderInfo = null;

                    // Check if it's an IndependentTag
                    if (element is IndependentTag tag)
                    {
                        // Enable leader on the tag
                        tag.HasLeader = true;
                        leaderAdded = tag.HasLeader;

                        // Get tag head position for info
                        XYZ tagHead = tag.TagHeadPosition;

                        leaderInfo = new
                        {
                            type = "IndependentTag",
                            hasLeader = tag.HasLeader,
                            tagHeadPosition = new { x = tagHead.X, y = tagHead.Y, z = tagHead.Z }
                        };
                    }
                    // Check if it's a FamilyInstance (generic annotation)
                    else if (element is FamilyInstance familyInstance)
                    {
                        // Generic annotations may have a "Leader" parameter
                        // Try to find and enable leader visibility
                        Parameter leaderParam = familyInstance.LookupParameter("Show Leader");
                        if (leaderParam == null)
                        {
                            leaderParam = familyInstance.LookupParameter("Leader");
                        }
                        if (leaderParam == null)
                        {
                            leaderParam = familyInstance.LookupParameter("Leader Visible");
                        }

                        if (leaderParam != null && !leaderParam.IsReadOnly)
                        {
                            if (leaderParam.StorageType == StorageType.Integer)
                            {
                                leaderParam.Set(1); // Enable
                                leaderAdded = true;
                            }
                        }

                        // For generic annotations, we can create a detail line as a leader
                        // if the family doesn't have built-in leader support
                        if (!leaderAdded)
                        {
                            // Get the annotation location
                            LocationPoint locPoint = familyInstance.Location as LocationPoint;
                            if (locPoint != null)
                            {
                                XYZ annotationLocation = locPoint.Point;

                                // If targetPoint is provided, create a leader line
                                if (parameters["targetPoint"] != null)
                                {
                                    var targetArray = parameters["targetPoint"].ToObject<double[]>();
                                    XYZ targetPoint = new XYZ(targetArray[0], targetArray[1], targetArray.Length > 2 ? targetArray[2] : 0);

                                    // Get the view
                                    ElementId ownerViewId = familyInstance.OwnerViewId;
                                    View view = doc.GetElement(ownerViewId) as View;

                                    if (view != null)
                                    {
                                        // Create detail line as leader
                                        Line leaderLine = Line.CreateBound(annotationLocation, targetPoint);
                                        DetailCurve detailLine = doc.Create.NewDetailCurve(view, leaderLine);

                                        leaderAdded = true;
                                        leaderInfo = new
                                        {
                                            type = "FamilyInstance",
                                            leaderLineId = (int)detailLine.Id.Value,
                                            startPoint = new { x = annotationLocation.X, y = annotationLocation.Y, z = annotationLocation.Z },
                                            endPoint = new { x = targetPoint.X, y = targetPoint.Y, z = targetPoint.Z }
                                        };
                                    }
                                }
                            }
                        }

                        if (leaderInfo == null)
                        {
                            leaderInfo = new
                            {
                                type = "FamilyInstance",
                                familyName = familyInstance.Symbol.FamilyName,
                                message = leaderAdded ? "Leader enabled via parameter" : "Family may not support leaders - provide targetPoint to create detail line"
                            };
                        }
                    }
                    else
                    {
                        trans.RollBack();
                        return Newtonsoft.Json.JsonConvert.SerializeObject(new
                        {
                            success = false,
                            error = $"Element type {element.GetType().Name} does not support leaders"
                        });
                    }

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        annotationId = annotationIdInt,
                        leaderAdded = leaderAdded,
                        leaderInfo = leaderInfo
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Sets the leader endpoint for an annotation (where the leader arrow points)
        /// </summary>
        [MCPMethod("setLeaderEndpoint", Category = "Annotation", Description = "Sets the endpoint of an annotation leader")]
        public static string SetLeaderEndpoint(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                if (parameters["annotationId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "annotationId is required"
                    });
                }

                if (parameters["endPoint"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "endPoint is required (array of [x, y, z])"
                    });
                }

                int annotationIdInt = parameters["annotationId"].ToObject<int>();
                ElementId annotationId = new ElementId(annotationIdInt);
                Element element = doc.GetElement(annotationId);

                if (element == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Annotation with ID {annotationIdInt} not found"
                    });
                }

                var endPointArray = parameters["endPoint"].ToObject<double[]>();
                XYZ endPoint = new XYZ(endPointArray[0], endPointArray[1], endPointArray.Length > 2 ? endPointArray[2] : 0);

                using (var trans = new Transaction(doc, "Set Leader Endpoint"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    bool success = false;
                    object result = null;

                    // Check if it's an IndependentTag
                    if (element is IndependentTag tag)
                    {
                        // For IndependentTag, we need to move the tag head position
                        // The leader end is determined by the tagged element location
                        if (!tag.HasLeader)
                        {
                            tag.HasLeader = true;
                        }

                        // Move tag head to create the desired leader geometry
                        // The endpoint becomes where the leader arrow points (the element)
                        // We position the tag head offset from that point
                        success = true;
                        result = new
                        {
                            type = "IndependentTag",
                            hasLeader = tag.HasLeader,
                            message = "IndependentTag leader endpoints are controlled by tag head position and tagged element"
                        };
                    }
                    // Check if it's a FamilyInstance
                    else if (element is FamilyInstance familyInstance)
                    {
                        // For generic annotations, we need to create/update a detail line
                        LocationPoint locPoint = familyInstance.Location as LocationPoint;
                        if (locPoint != null)
                        {
                            XYZ annotationLocation = locPoint.Point;
                            ElementId ownerViewId = familyInstance.OwnerViewId;
                            View view = doc.GetElement(ownerViewId) as View;

                            if (view != null)
                            {
                                // Check if there's an existing leader line linked to this annotation
                                // For now, create a new detail line
                                Line leaderLine = Line.CreateBound(annotationLocation, endPoint);
                                DetailCurve detailLine = doc.Create.NewDetailCurve(view, leaderLine);

                                success = true;
                                result = new
                                {
                                    type = "FamilyInstance",
                                    leaderLineId = (int)detailLine.Id.Value,
                                    startPoint = new { x = annotationLocation.X, y = annotationLocation.Y, z = annotationLocation.Z },
                                    endPoint = new { x = endPoint.X, y = endPoint.Y, z = endPoint.Z }
                                };
                            }
                        }
                    }
                    else
                    {
                        trans.RollBack();
                        return Newtonsoft.Json.JsonConvert.SerializeObject(new
                        {
                            success = false,
                            error = $"Element type {element.GetType().Name} does not support leader endpoints"
                        });
                    }

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = success,
                        annotationId = annotationIdInt,
                        result = result
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Gets leader information for an annotation
        /// </summary>
        [MCPMethod("getAnnotationLeaderInfo", Category = "Annotation", Description = "Gets leader information for an annotation tag")]
        public static string GetAnnotationLeaderInfo(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                if (parameters["annotationId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "annotationId is required"
                    });
                }

                int annotationIdInt = parameters["annotationId"].ToObject<int>();
                ElementId annotationId = new ElementId(annotationIdInt);
                Element element = doc.GetElement(annotationId);

                if (element == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Annotation with ID {annotationIdInt} not found"
                    });
                }

                object leaderInfo = null;

                // Check if it's an IndependentTag
                if (element is IndependentTag tag)
                {
                    XYZ tagHead = tag.TagHeadPosition;

                    leaderInfo = new
                    {
                        type = "IndependentTag",
                        hasLeader = tag.HasLeader,
                        tagHeadPosition = new { x = tagHead.X, y = tagHead.Y, z = tagHead.Z }
                    };
                }
                // Check if it's a FamilyInstance
                else if (element is FamilyInstance familyInstance)
                {
                    LocationPoint locPoint = familyInstance.Location as LocationPoint;
                    XYZ location = locPoint?.Point;

                    // Check for leader-related parameters
                    Parameter leaderParam = familyInstance.LookupParameter("Show Leader")
                        ?? familyInstance.LookupParameter("Leader")
                        ?? familyInstance.LookupParameter("Leader Visible");

                    bool hasLeaderParam = leaderParam != null;
                    bool leaderEnabled = hasLeaderParam && leaderParam.AsInteger() == 1;

                    leaderInfo = new
                    {
                        type = "FamilyInstance",
                        familyName = familyInstance.Symbol.FamilyName,
                        typeName = familyInstance.Symbol.Name,
                        location = location != null ? new { x = location.X, y = location.Y, z = location.Z } : null,
                        hasLeaderParameter = hasLeaderParam,
                        leaderEnabled = leaderEnabled,
                        message = "Generic annotations may need detail lines as leaders - use SetLeaderEndpoint to create"
                    };
                }
                else
                {
                    leaderInfo = new
                    {
                        type = element.GetType().Name,
                        message = "Element type may not support standard leader functionality"
                    };
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = true,
                    annotationId = annotationIdInt,
                    leaderInfo = leaderInfo
                });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Places an annotation symbol with a leader pointing to a target element
        /// Combined operation for efficient keynote placement with leaders
        /// </summary>
        [MCPMethod("placeAnnotationWithLeader", Category = "Annotation", Description = "Places an annotation tag with a leader")]
        public static string PlaceAnnotationWithLeader(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Validate required parameters
                if (parameters["symbolTypeId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "symbolTypeId is required"
                    });
                }

                if (parameters["viewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "viewId is required"
                    });
                }

                if (parameters["targetElementId"] == null && parameters["targetPoint"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "Either targetElementId or targetPoint is required"
                    });
                }

                // Parse parameters
                int symbolTypeIdInt = parameters["symbolTypeId"].ToObject<int>();
                ElementId symbolTypeId = new ElementId(symbolTypeIdInt);
                FamilySymbol symbolType = doc.GetElement(symbolTypeId) as FamilySymbol;

                if (symbolType == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Annotation symbol type with ID {symbolTypeIdInt} not found"
                    });
                }

                int viewIdInt = parameters["viewId"].ToObject<int>();
                ElementId viewId = new ElementId(viewIdInt);
                View view = doc.GetElement(viewId) as View;

                if (view == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {viewIdInt} not found"
                    });
                }

                // Determine target point
                XYZ targetPoint = null;
                int? targetElementId = null;

                if (parameters["targetElementId"] != null)
                {
                    int targetIdInt = parameters["targetElementId"].ToObject<int>();
                    targetElementId = targetIdInt;
                    Element targetElement = doc.GetElement(new ElementId(targetIdInt));

                    if (targetElement != null)
                    {
                        // Get element location/center
                        BoundingBoxXYZ bbox = targetElement.get_BoundingBox(view);
                        if (bbox != null)
                        {
                            targetPoint = new XYZ(
                                (bbox.Min.X + bbox.Max.X) / 2,
                                (bbox.Min.Y + bbox.Max.Y) / 2,
                                0
                            );
                        }
                        else
                        {
                            LocationPoint locPt = targetElement.Location as LocationPoint;
                            if (locPt != null)
                            {
                                targetPoint = new XYZ(locPt.Point.X, locPt.Point.Y, 0);
                            }
                        }
                    }
                }

                if (targetPoint == null && parameters["targetPoint"] != null)
                {
                    var targetArray = parameters["targetPoint"].ToObject<double[]>();
                    targetPoint = new XYZ(targetArray[0], targetArray[1], targetArray.Length > 2 ? targetArray[2] : 0);
                }

                if (targetPoint == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "Could not determine target point from element or coordinates"
                    });
                }

                // Calculate annotation location (offset from target)
                double offsetX = 3.0; // Default 3 feet offset
                double offsetY = 2.0;

                if (parameters["offset"] != null)
                {
                    var offsetArray = parameters["offset"].ToObject<double[]>();
                    offsetX = offsetArray[0];
                    offsetY = offsetArray.Length > 1 ? offsetArray[1] : offsetY;
                }

                // Determine offset direction based on leaderAngle parameter
                double leaderAngle = 45.0; // Default 45 degrees
                if (parameters["leaderAngle"] != null)
                {
                    leaderAngle = parameters["leaderAngle"].ToObject<double>();
                }

                // Calculate annotation position based on angle
                double angleRad = leaderAngle * Math.PI / 180.0;
                double offsetDistance = Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
                XYZ annotationLocation = new XYZ(
                    targetPoint.X + offsetDistance * Math.Cos(angleRad),
                    targetPoint.Y + offsetDistance * Math.Sin(angleRad),
                    0
                );

                // Override with explicit location if provided
                if (parameters["annotationLocation"] != null)
                {
                    var locArray = parameters["annotationLocation"].ToObject<double[]>();
                    annotationLocation = new XYZ(locArray[0], locArray[1], locArray.Length > 2 ? locArray[2] : 0);
                }

                using (var trans = new Transaction(doc, "Place Annotation with Leader"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Activate the symbol if not already active
                    if (!symbolType.IsActive)
                    {
                        symbolType.Activate();
                    }

                    // Create the annotation symbol instance
                    FamilyInstance symbol = doc.Create.NewFamilyInstance(annotationLocation, symbolType, view);

                    // Create leader line (detail curve)
                    Line leaderLine = Line.CreateBound(annotationLocation, targetPoint);
                    DetailCurve leaderCurve = doc.Create.NewDetailCurve(view, leaderLine);

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        symbolId = (int)symbol.Id.Value,
                        symbolType = symbolType.Name,
                        leaderId = (int)leaderCurve.Id.Value,
                        viewId = viewIdInt,
                        targetElementId = targetElementId,
                        annotationLocation = new { x = annotationLocation.X, y = annotationLocation.Y, z = annotationLocation.Z },
                        targetPoint = new { x = targetPoint.X, y = targetPoint.Y, z = targetPoint.Z },
                        leaderAngle = leaderAngle
                    });
                }
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Batch places multiple keynotes with leaders pointing to their respective elements
        /// Efficient method for placing all keynotes on a floor plan at once
        /// </summary>
        [MCPMethod("batchPlaceKeynotesWithLeaders", Category = "Annotation", Description = "Batch places keynote tags with leaders on multiple elements")]
        public static string BatchPlaceKeynotesWithLeaders(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                if (parameters["viewId"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "viewId is required"
                    });
                }

                if (parameters["keynotes"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "keynotes array is required"
                    });
                }

                int viewIdInt = parameters["viewId"].ToObject<int>();
                ElementId viewId = new ElementId(viewIdInt);
                View view = doc.GetElement(viewId) as View;

                if (view == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"View with ID {viewIdInt} not found"
                    });
                }

                var keynoteData = parameters["keynotes"].ToObject<JArray>();
                var results = new List<object>();
                int successCount = 0;
                int errorCount = 0;

                using (var trans = new Transaction(doc, "Batch Place Keynotes with Leaders"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    foreach (JObject keynote in keynoteData)
                    {
                        try
                        {
                            // Get symbol type
                            int symbolTypeIdInt = keynote["symbolTypeId"].ToObject<int>();
                            ElementId symbolTypeId = new ElementId(symbolTypeIdInt);
                            FamilySymbol symbolType = doc.GetElement(symbolTypeId) as FamilySymbol;

                            if (symbolType == null)
                            {
                                results.Add(new { symbolTypeId = symbolTypeIdInt, success = false, error = "Symbol type not found" });
                                errorCount++;
                                continue;
                            }

                            // Get target point
                            XYZ targetPoint = null;
                            if (keynote["targetPoint"] != null)
                            {
                                var targetArray = keynote["targetPoint"].ToObject<double[]>();
                                targetPoint = new XYZ(targetArray[0], targetArray[1], targetArray.Length > 2 ? targetArray[2] : 0);
                            }
                            else if (keynote["targetElementId"] != null)
                            {
                                int targetIdInt = keynote["targetElementId"].ToObject<int>();
                                Element targetElement = doc.GetElement(new ElementId(targetIdInt));
                                if (targetElement != null)
                                {
                                    BoundingBoxXYZ bbox = targetElement.get_BoundingBox(view);
                                    if (bbox != null)
                                    {
                                        targetPoint = new XYZ((bbox.Min.X + bbox.Max.X) / 2, (bbox.Min.Y + bbox.Max.Y) / 2, 0);
                                    }
                                }
                            }

                            if (targetPoint == null)
                            {
                                results.Add(new { symbolTypeId = symbolTypeIdInt, success = false, error = "Could not determine target point" });
                                errorCount++;
                                continue;
                            }

                            // Get annotation location (or calculate from offset)
                            XYZ annotationLocation;
                            if (keynote["annotationLocation"] != null)
                            {
                                var locArray = keynote["annotationLocation"].ToObject<double[]>();
                                annotationLocation = new XYZ(locArray[0], locArray[1], locArray.Length > 2 ? locArray[2] : 0);
                            }
                            else
                            {
                                // Default offset calculation
                                double leaderAngle = keynote["leaderAngle"]?.ToObject<double>() ?? 45.0;
                                double offsetDistance = keynote["offsetDistance"]?.ToObject<double>() ?? 4.0;
                                double angleRad = leaderAngle * Math.PI / 180.0;
                                annotationLocation = new XYZ(
                                    targetPoint.X + offsetDistance * Math.Cos(angleRad),
                                    targetPoint.Y + offsetDistance * Math.Sin(angleRad),
                                    0
                                );
                            }

                            // Activate and place symbol
                            if (!symbolType.IsActive)
                            {
                                symbolType.Activate();
                            }

                            FamilyInstance symbol = doc.Create.NewFamilyInstance(annotationLocation, symbolType, view);

                            // Create leader line
                            Line leaderLine = Line.CreateBound(annotationLocation, targetPoint);
                            DetailCurve leaderCurve = doc.Create.NewDetailCurve(view, leaderLine);

                            results.Add(new
                            {
                                symbolTypeId = symbolTypeIdInt,
                                symbolId = (int)symbol.Id.Value,
                                leaderId = (int)leaderCurve.Id.Value,
                                success = true
                            });
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            results.Add(new { success = false, error = ex.Message });
                            errorCount++;
                        }
                    }

                    trans.Commit();
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = true,
                    viewId = viewIdInt,
                    totalRequested = keynoteData.Count,
                    successCount = successCount,
                    errorCount = errorCount,
                    results = results
                });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        #endregion

        #region Legend Text and Keynote Schedule Methods

        /// <summary>
        /// Add text note(s) to a legend view at specified positions.
        /// Used to build legends with title, labels, and descriptions.
        /// </summary>
        [MCPMethod("addTextToLegend", Category = "Annotation", Description = "Adds a text note to a legend view")]
        public static string AddTextToLegend(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var legendViewId = parameters["legendViewId"]?.Value<int>();
                if (legendViewId == null)
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, error = "legendViewId is required" });

                var view = doc.GetElement(new ElementId(legendViewId.Value)) as View;
                if (view == null || view.ViewType != ViewType.Legend)
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, error = "Not a valid legend view" });

                var textEntries = parameters["entries"]?.ToObject<JArray>();
                if (textEntries == null || textEntries.Count == 0)
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, error = "entries array is required" });

                // Get or find text note type
                var textTypeId = parameters["textTypeId"]?.Value<int>();
                TextNoteType noteType = null;
                if (textTypeId.HasValue)
                {
                    noteType = doc.GetElement(new ElementId(textTypeId.Value)) as TextNoteType;
                }
                if (noteType == null)
                {
                    noteType = new FilteredElementCollector(doc)
                        .OfClass(typeof(TextNoteType))
                        .Cast<TextNoteType>()
                        .FirstOrDefault();
                }
                if (noteType == null)
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, error = "No text note type found" });

                var results = new List<object>();

                using (var trans = new Transaction(doc, "Add Text to Legend"))
                {
                    trans.Start();

                    foreach (JObject entry in textEntries)
                    {
                        var text = entry["text"]?.ToString();
                        if (string.IsNullOrEmpty(text)) continue;

                        var x = entry["x"]?.Value<double>() ?? 0;
                        var y = entry["y"]?.Value<double>() ?? 0;
                        var position = new XYZ(x, y, 0);

                        // Allow per-entry text type override
                        var entryTypeId = entry["textTypeId"]?.Value<int>();
                        var entryType = noteType;
                        if (entryTypeId.HasValue)
                        {
                            var customType = doc.GetElement(new ElementId(entryTypeId.Value)) as TextNoteType;
                            if (customType != null) entryType = customType;
                        }

                        var textNote = TextNote.Create(doc, view.Id, position, text, entryType.Id);
                        results.Add(new
                        {
                            id = (int)textNote.Id.Value,
                            text,
                            x, y
                        });
                    }

                    trans.Commit();
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = true,
                    legendViewId = legendViewId.Value,
                    count = results.Count,
                    textNotes = results
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Create a keynote schedule (keynote legend) that lists all keynotes used in the project.
        /// </summary>
        [MCPMethod("createKeynoteSchedule", Category = "Annotation", Description = "Creates a keynote legend schedule")]
        public static string CreateKeynoteSchedule(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var scheduleName = parameters["name"]?.ToString() ?? "Keynote Legend";

                // Check if schedule already exists
                var existing = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSchedule))
                    .Cast<ViewSchedule>()
                    .FirstOrDefault(s => s.Name == scheduleName);

                if (existing != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        alreadyExists = true,
                        scheduleId = (int)existing.Id.Value,
                        name = existing.Name,
                        message = "Schedule already exists"
                    });
                }

                using (var trans = new Transaction(doc, "Create Keynote Schedule"))
                {
                    trans.Start();

                    // Create a multi-category schedule filtering to keynotes
                    var schedule = ViewSchedule.CreateSchedule(doc,
                        new ElementId(BuiltInCategory.OST_KeynoteTags));

                    if (schedule == null)
                    {
                        trans.RollBack();
                        return Newtonsoft.Json.JsonConvert.SerializeObject(new
                        {
                            success = false,
                            error = "Failed to create keynote schedule"
                        });
                    }

                    schedule.Name = scheduleName;

                    // Add keynote fields
                    var definition = schedule.Definition;
                    var schedulableFields = definition.GetSchedulableFields();

                    foreach (var field in schedulableFields)
                    {
                        var fieldName = field.GetName(doc);
                        if (fieldName == "Keynote Text" || fieldName == "Key Value" ||
                            fieldName == "Keynote Number")
                        {
                            definition.AddField(field);
                        }
                    }

                    trans.Commit();

                    return Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        success = true,
                        scheduleId = (int)schedule.Id.Value,
                        name = schedule.Name,
                        fieldCount = definition.GetFieldCount()
                    });
                }
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        #endregion

        #region CD Generation Annotation Helpers

        /// <summary>
        /// Places a DETAIL CROSS REFERENCES text legend in each floor plan view,
        /// listing all detail numbers/sheets from the generated plan so users can navigate.
        /// </summary>
        [MCPMethod("addDetailCrossReferences", Category = "Annotation", Description = "Places a detail cross-reference legend in all floor plan views, listing every generated detail with its sheet reference")]
        public static string AddDetailCrossReferences(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc  = uiApp.ActiveUIDocument.Document;
                var plan = parameters["plan"] as JObject;
                var detailPlan = plan?["details"] as JArray ?? new JArray();

                if (detailPlan.Count == 0)
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = true, message = "No details in plan — nothing to cross-reference", notesPlaced = 0 });

                // Build the legend text
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("DETAIL CROSS REFERENCES");
                foreach (var d in detailPlan.Cast<JObject>())
                {
                    int    num   = d["detailNumber"]?.Value<int>() ?? 0;
                    string sheet = d["sheetReference"]?.ToString() ?? "?";
                    string title = d["title"]?.ToString() ?? d["detailType"]?.ToString() ?? "DETAIL";
                    sb.AppendLine($"{num}/{sheet}  {title.ToUpperInvariant()}");
                }
                string legendText = sb.ToString().TrimEnd();

                // Find a TextNoteType (prefer small)
                var textNoteType = new FilteredElementCollector(doc)
                    .OfClass(typeof(TextNoteType))
                    .Cast<TextNoteType>()
                    .OrderBy(t => Math.Abs(t.get_Parameter(BuiltInParameter.TEXT_SIZE)?.AsDouble() ?? 1.0 - 0.0078)) // ~3/32"
                    .FirstOrDefault();

                if (textNoteType == null)
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, error = "No TextNoteType found" });

                // Place legend in every non-template floor plan view
                var floorPlanViews = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .Where(v => !v.IsTemplate && v.ViewType == ViewType.FloorPlan)
                    .ToList();

                int notesPlaced = 0;
                foreach (var view in floorPlanViews)
                {
                    // Position at lower-left of the view's crop box
                    XYZ origin;
                    try
                    {
                        var crop = view.CropBox;
                        origin = new XYZ(crop.Min.X + 1.0, crop.Min.Y + 1.0, 0); // 1 ft margin
                    }
                    catch
                    {
                        origin = XYZ.Zero;
                    }

                    using (var trans = new Transaction(doc, $"BM: Cross-refs in {view.Name}"))
                    {
                        trans.Start();
                        var opts = new TextNoteOptions(textNoteType.Id)
                        {
                            HorizontalAlignment = HorizontalTextAlignment.Left,
                        };
                        TextNote.Create(doc, view.Id, origin, legendText, opts);
                        trans.Commit();
                        notesPlaced++;
                    }
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success     = true,
                    notesPlaced,
                    viewsTagged = floorPlanViews.Count,
                    detailCount = detailPlan.Count,
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Makes grid lines visible in all non-template floor plan and section views.
        /// </summary>
        [MCPMethod("setGridLinesVisible", Category = "Annotation", Description = "Ensures grid lines are visible in all floor plan and section views")]
        public static string SetGridLinesVisible(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var gridsCatId = new ElementId(BuiltInCategory.OST_Grids);
                int updated = 0;

                var views = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate && (v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.Section || v.ViewType == ViewType.Elevation))
                    .ToList();

                foreach (var view in views)
                {
                    try
                    {
                        if (view.GetCategoryHidden(gridsCatId))
                        {
                            using (var trans = new Transaction(doc, $"BM: Grid visibility {view.Name}"))
                            {
                                trans.Start();
                                view.SetCategoryHidden(gridsCatId, false);
                                trans.Commit();
                                updated++;
                            }
                        }
                    }
                    catch { /* view may not support this category */ }
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = true, viewsUpdated = updated, viewsChecked = views.Count });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Tightens crop regions of floor plan views to the building footprint (element bounding boxes + margin).
        /// </summary>
        [MCPMethod("setCropRegionsTight", Category = "Annotation", Description = "Sets floor plan view crop regions tight to the building footprint with a configurable margin")]
        public static string SetCropRegionsTight(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc    = uiApp.ActiveUIDocument.Document;
                double marginFt = parameters["marginFt"]?.ToObject<double>() ?? 3.0; // default 3 ft margin

                var floorPlanViews = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .Where(v => !v.IsTemplate && v.ViewType == ViewType.FloorPlan && v.CropBoxActive)
                    .ToList();

                int updated = 0;
                foreach (var view in floorPlanViews)
                {
                    // Collect all model elements and use get_BoundingBox(view) to filter
                    // to only elements in this view's scope.  Using FilteredElementCollector(doc, view.Id)
                    // would limit to elements already inside the *current* crop box, preventing expansion.
                    var elems = new FilteredElementCollector(doc)
                        .WhereElementIsNotElementType()
                        .ToList();

                    double minX = double.MaxValue, minY = double.MaxValue;
                    double maxX = double.MinValue, maxY = double.MinValue;
                    bool found = false;

                    foreach (var el in elems)
                    {
                        try
                        {
                            // get_BoundingBox(view) returns null for elements outside this view's level/scope
                            var bb = el.get_BoundingBox(view);
                            if (bb == null) continue;
                            minX = Math.Min(minX, bb.Min.X);
                            minY = Math.Min(minY, bb.Min.Y);
                            maxX = Math.Max(maxX, bb.Max.X);
                            maxY = Math.Max(maxY, bb.Max.Y);
                            found = true;
                        }
                        catch { }
                    }

                    if (!found) continue;

                    using (var trans = new Transaction(doc, $"BM: Crop {view.Name}"))
                    {
                        trans.Start();
                        var newCrop = new BoundingBoxXYZ
                        {
                            Min = new XYZ(minX - marginFt, minY - marginFt, view.CropBox.Min.Z),
                            Max = new XYZ(maxX + marginFt, maxY + marginFt, view.CropBox.Max.Z),
                        };
                        view.CropBox       = newCrop;
                        view.CropBoxActive = true;
                        trans.Commit();
                        updated++;
                    }
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = true, viewsUpdated = updated, marginFt });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Adds exterior dimension strings to floor plan views.
        /// Creates a horizontal string (above building, references N-S wall exterior faces)
        /// and a vertical string (right of building, references E-W wall exterior faces).
        /// Skips views that already have dimension elements unless skipIfHasDims=false.
        /// Must run BEFORE setCropRegionsTight so the crop expands to include the strings.
        /// </summary>
        [MCPMethod("addFloorPlanDimensions", Category = "Annotation", Description = "Adds exterior dimension strings to floor plan views: horizontal string above and vertical string to the right, referencing wall faces")]
        public static string AddFloorPlanDimensions(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                double offsetFt         = parameters["offsetFt"]?.ToObject<double>() ?? 2.0;
                bool   skipIfHasDims    = parameters["skipIfHasDims"]?.ToObject<bool>() ?? true;
                int?   specificViewId   = parameters["viewId"]?.ToObject<int?>();

                // ── Find best linear dimension type ───────────────────────────────
                var dimType = new FilteredElementCollector(doc)
                    .OfClass(typeof(DimensionType))
                    .Cast<DimensionType>()
                    .Where(dt => dt.StyleType == DimensionStyleType.Linear)
                    .OrderBy(dt =>
                    {
                        // Prefer CDC or BM prefixed types (firm standards)
                        var n = dt.Name;
                        if (n.StartsWith("CDC", StringComparison.OrdinalIgnoreCase)) return 0;
                        if (n.StartsWith("BM",  StringComparison.OrdinalIgnoreCase)) return 1;
                        return 2;
                    })
                    .FirstOrDefault();

                if (dimType == null)
                    return ResponseBuilder.Error("No linear dimension type found in model").Build();

                // ── Target views ──────────────────────────────────────────────────
                IEnumerable<ViewPlan> targetViews;
                if (specificViewId.HasValue)
                {
                    var sv = doc.GetElement(new ElementId(specificViewId.Value)) as ViewPlan;
                    targetViews = sv != null ? new[] { sv } : Enumerable.Empty<ViewPlan>();
                }
                else
                {
                    targetViews = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewPlan))
                        .Cast<ViewPlan>()
                        .Where(v => !v.IsTemplate && v.ViewType == ViewType.FloorPlan);
                }

                int viewsUpdated = 0;
                var log    = new List<object>();
                var errors = new List<string>();

                foreach (var view in targetViews)
                {
                    if (view == null) continue;

                    // Skip views that already have dimension elements
                    if (skipIfHasDims)
                    {
                        bool hasDims = new FilteredElementCollector(doc, view.Id)
                            .OfClass(typeof(Dimension))
                            .Any();
                        if (hasDims)
                        {
                            log.Add(new { view = view.Name, status = "skipped_has_dims" });
                            continue;
                        }
                    }

                    // Level Z for dimension line placement
                    var levelId = view.GenLevel?.Id ?? view.LevelId;
                    double levelZ = (doc.GetElement(levelId) as Level)?.Elevation ?? 0;

                    // Collect straight walls on this level (doc-scope so we don't miss
                    // walls outside the current crop box)
                    var walls = new FilteredElementCollector(doc)
                        .OfClass(typeof(Wall))
                        .Cast<Wall>()
                        .Where(w => w.LevelId == levelId
                                 && w.WallType?.Kind == WallKind.Basic
                                 && w.Location is LocationCurve lwlc
                                 && lwlc.Curve is Line)
                        .ToList();

                    if (walls.Count < 2)
                    {
                        log.Add(new { view = view.Name, status = "skipped_no_walls" });
                        continue;
                    }

                    // Bounding box of all wall endpoints
                    double minX = double.MaxValue, maxX = double.MinValue;
                    double minY = double.MaxValue, maxY = double.MinValue;
                    foreach (var w in walls)
                    {
                        var lc = (LocationCurve)w.Location;
                        var p0 = lc.Curve.GetEndPoint(0);
                        var p1 = lc.Curve.GetEndPoint(1);
                        minX = Math.Min(minX, Math.Min(p0.X, p1.X));
                        maxX = Math.Max(maxX, Math.Max(p0.X, p1.X));
                        minY = Math.Min(minY, Math.Min(p0.Y, p1.Y));
                        maxY = Math.Max(maxY, Math.Max(p0.Y, p1.Y));
                    }

                    // N-S walls (|dir.Y| > 0.7): faces are perpendicular to X
                    //   → can be referenced by a horizontal dimension line
                    var nsWalls = walls
                        .Where(w => Math.Abs(((Line)((LocationCurve)w.Location).Curve).Direction.Y) > 0.7)
                        .OrderBy(w => {
                            var lc = (LocationCurve)w.Location;
                            return (lc.Curve.GetEndPoint(0).X + lc.Curve.GetEndPoint(1).X) / 2.0;
                        })
                        .ToList();

                    // E-W walls (|dir.X| > 0.7): faces are perpendicular to Y
                    //   → can be referenced by a vertical dimension line
                    var ewWalls = walls
                        .Where(w => Math.Abs(((Line)((LocationCurve)w.Location).Curve).Direction.X) > 0.7)
                        .OrderBy(w => {
                            var lc = (LocationCurve)w.Location;
                            return (lc.Curve.GetEndPoint(0).Y + lc.Curve.GetEndPoint(1).Y) / 2.0;
                        })
                        .ToList();

                    int dimsCreated = 0;

                    using (var tx = new Transaction(doc, "BM: Add Floor Plan Dimensions"))
                    {
                        var fho = tx.GetFailureHandlingOptions();
                        fho.SetFailuresPreprocessor(new WarningSwallower());
                        tx.SetFailureHandlingOptions(fho);
                        tx.Start();

                        // ── Horizontal string: above building, refs N-S wall faces ──
                        if (nsWalls.Count >= 2)
                        {
                            var refs = new ReferenceArray();
                            foreach (var w in nsWalls)
                            {
                                try
                                {
                                    // Exterior face first, then interior — gives both faces of each wall
                                    foreach (var r in HostObjectUtils.GetSideFaces(w, ShellLayerType.Exterior)
                                                       .Concat(HostObjectUtils.GetSideFaces(w, ShellLayerType.Interior)))
                                        refs.Append(r);
                                }
                                catch { /* skip walls whose faces can't be extracted */ }
                            }
                            if (refs.Size >= 2)
                            {
                                try
                                {
                                    var dimLine = Line.CreateBound(
                                        new XYZ(minX - 1.0, maxY + offsetFt, levelZ),
                                        new XYZ(maxX + 1.0, maxY + offsetFt, levelZ));
                                    doc.Create.NewDimension(view, dimLine, refs, dimType);
                                    dimsCreated++;
                                }
                                catch (Exception ex) { errors.Add($"{view.Name} H: {ex.Message}"); }
                            }
                        }

                        // ── Vertical string: right of building, refs E-W wall faces ──
                        if (ewWalls.Count >= 2)
                        {
                            var refs = new ReferenceArray();
                            foreach (var w in ewWalls)
                            {
                                try
                                {
                                    foreach (var r in HostObjectUtils.GetSideFaces(w, ShellLayerType.Exterior)
                                                       .Concat(HostObjectUtils.GetSideFaces(w, ShellLayerType.Interior)))
                                        refs.Append(r);
                                }
                                catch { }
                            }
                            if (refs.Size >= 2)
                            {
                                try
                                {
                                    var dimLine = Line.CreateBound(
                                        new XYZ(maxX + offsetFt, minY - 1.0, levelZ),
                                        new XYZ(maxX + offsetFt, maxY + 1.0, levelZ));
                                    doc.Create.NewDimension(view, dimLine, refs, dimType);
                                    dimsCreated++;
                                }
                                catch (Exception ex) { errors.Add($"{view.Name} V: {ex.Message}"); }
                            }
                        }

                        tx.Commit();
                    }

                    if (dimsCreated > 0) viewsUpdated++;
                    log.Add(new { view = view.Name, dimsCreated, nsWalls = nsWalls.Count, ewWalls = ewWalls.Count, status = dimsCreated > 0 ? "ok" : "no_dims_created" });
                }

                return ResponseBuilder.Success()
                    .With("viewsUpdated",  viewsUpdated)
                    .With("dimTypeName",   dimType.Name)
                    .With("log",           log)
                    .With("errors",        errors)
                    .Build();
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        // ── Helpers for AddFloorPlanDimensions ────────────────────────────────────

        /// <summary>
        /// Writes project name, address, and number to the Revit ProjectInformation object
        /// so titleblocks pick them up automatically.
        /// </summary>
        [MCPMethod("setTitleblockProjectInfo", Category = "Annotation", Description = "Sets project name, number, address, and client name in Revit project information so all titleblocks reflect it")]
        public static string SetTitleblockProjectInfo(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var pi  = doc.ProjectInformation;

                using (var trans = new Transaction(doc, "BM: Project Info"))
                {
                    trans.Start();

                    void TrySet(BuiltInParameter bip, string value)
                    {
                        if (string.IsNullOrWhiteSpace(value)) return;
                        var p = pi.get_Parameter(bip);
                        if (p != null && !p.IsReadOnly) p.Set(value);
                    }

                    TrySet(BuiltInParameter.PROJECT_NAME,    parameters["projectName"]?.ToString());
                    TrySet(BuiltInParameter.PROJECT_NUMBER,  parameters["projectNumber"]?.ToString());
                    TrySet(BuiltInParameter.PROJECT_ADDRESS, parameters["projectAddress"]?.ToString());
                    TrySet(BuiltInParameter.CLIENT_NAME,     parameters["clientName"]?.ToString());
                    TrySet(BuiltInParameter.PROJECT_STATUS,  parameters["projectStatus"]?.ToString());
                    TrySet(BuiltInParameter.PROJECT_ISSUE_DATE, parameters["issueDate"]?.ToString());

                    trans.Commit();
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = true, message = "Project information updated" });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Places a Drawing List schedule on the G0.x cover sheet.
        /// Creates the Drawing List schedule if one does not already exist in the model.
        /// </summary>
        [MCPMethod("generateCoverSheet", Category = "Annotation", Description = "Places a Drawing List schedule on the G0.x cover sheet; creates the schedule if needed")]
        public static string GenerateCoverSheet(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // ── Find cover sheet (first G0.x sheet) ──────────────────────────────
                string targetSheetNum = parameters["sheetNumber"]?.ToString();
                ViewSheet coverSheet;
                if (!string.IsNullOrWhiteSpace(targetSheetNum))
                {
                    coverSheet = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSheet))
                        .Cast<ViewSheet>()
                        .FirstOrDefault(s => string.Equals(s.SheetNumber, targetSheetNum, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    coverSheet = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSheet))
                        .Cast<ViewSheet>()
                        .Where(s => s.SheetNumber.StartsWith("G0", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(s => s.SheetNumber, StringComparer.OrdinalIgnoreCase)
                        .FirstOrDefault();
                }

                if (coverSheet == null)
                    return ResponseBuilder.Error("No G0.x cover sheet found in model").Build();

                // ── Find or create Drawing List (Sheet List) schedule ────────────────
                // A sheet list is a ViewSchedule with CategoryId == OST_Sheets
                var sheetCatId = new ElementId(BuiltInCategory.OST_Sheets);
                var drawingList = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSchedule))
                    .Cast<ViewSchedule>()
                    .FirstOrDefault(s => !s.IsTemplate
                        && s.Definition.CategoryId == sheetCatId);

                bool scheduleCreated = false;
                if (drawingList == null)
                {
                    using (var tx = new Transaction(doc, "BM: Create Drawing List"))
                    {
                        tx.Start();
                        drawingList = ViewSchedule.CreateSchedule(doc, sheetCatId);
                        drawingList.Name = "DRAWING LIST *";
                        tx.Commit();
                        scheduleCreated = true;
                    }
                }

                // ── Check if already placed on this sheet ─────────────────────────────
                var existingSsi = new FilteredElementCollector(doc)
                    .OfClass(typeof(ScheduleSheetInstance))
                    .Cast<ScheduleSheetInstance>()
                    .FirstOrDefault(ssi => ssi.ScheduleId == drawingList.Id && ssi.OwnerViewId == coverSheet.Id);

                if (existingSsi != null)
                    return ResponseBuilder.Success()
                        .With("sheetNumber", coverSheet.SheetNumber)
                        .With("sheetName",   coverSheet.Name)
                        .With("drawingListId", (long)drawingList.Id.Value)
                        .With("scheduleCreated", scheduleCreated)
                        .With("status", "already_placed")
                        .Build();

                // ── Determine insertion point ─────────────────────────────────────────
                // Default: left-center of sheet, giving titleblock room on right
                double insertX = parameters["insertionX"]?.ToObject<double>() ?? 0.25; // ft from sheet origin
                double insertY = parameters["insertionY"]?.ToObject<double>() ?? 0.60;
                var insertPt = new XYZ(insertX, insertY, 0);

                using (var tx = new Transaction(doc, "BM: Place Drawing List on Cover Sheet"))
                {
                    tx.Start();
                    ScheduleSheetInstance.Create(doc, coverSheet.Id, drawingList.Id, insertPt);
                    tx.Commit();
                }

                return ResponseBuilder.Success()
                    .With("sheetNumber",     coverSheet.SheetNumber)
                    .With("sheetName",       coverSheet.Name)
                    .With("drawingListId",   (long)drawingList.Id.Value)
                    .With("scheduleCreated", scheduleCreated)
                    .With("status",          "placed")
                    .Build();
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        #endregion

        #region Deferred annotation items

        /// <summary>
        /// Makes level datum markers visible in all section and elevation views.
        /// Unhides the Levels category so FIRST FLOOR / SECOND FLOOR datums appear.
        /// </summary>
        [MCPMethod("setLevelMarkersVisible", Category = "Annotation", Description = "Unhides level datum markers in all section and elevation views so floor-to-floor heights are visible")]
        public static string SetLevelMarkersVisible(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var levelsCatId = new ElementId(BuiltInCategory.OST_Levels);
                int updated = 0;

                var views = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate &&
                                (v.ViewType == ViewType.Section || v.ViewType == ViewType.Elevation))
                    .ToList();

                foreach (var view in views)
                {
                    try
                    {
                        bool hidden = view.GetCategoryHidden(levelsCatId);
                        if (!hidden) continue; // already visible — skip
                        using (var trans = new Transaction(doc, $"BM: Level visibility {view.Name}"))
                        {
                            trans.Start();
                            view.SetCategoryHidden(levelsCatId, false);
                            trans.Commit();
                            updated++;
                        }
                    }
                    catch { }
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = true, viewsUpdated = updated, viewsChecked = views.Count });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Makes section cut indicators visible in all floor plan views so sections are navigable.
        /// Unhides OST_Sections category (the circled section marks) in floor plans.
        /// </summary>
        [MCPMethod("setSectionMarksVisible", Category = "Annotation", Description = "Unhides section cut indicators in floor plan views so section marks are visible and navigable")]
        public static string SetSectionMarksVisible(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var sectionsCatId = new ElementId(BuiltInCategory.OST_Sections);
                int updated = 0;

                var floorPlanViews = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .Where(v => !v.IsTemplate && v.ViewType == ViewType.FloorPlan)
                    .ToList();

                foreach (var view in floorPlanViews)
                {
                    try
                    {
                        if (!view.GetCategoryHidden(sectionsCatId)) continue;
                        using (var trans = new Transaction(doc, $"BM: Section marks {view.Name}"))
                        {
                            trans.Start();
                            view.SetCategoryHidden(sectionsCatId, false);
                            trans.Commit();
                            updated++;
                        }
                    }
                    catch { }
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = true, viewsUpdated = updated });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Places a north arrow in every floor plan view.
        /// Prefers an already-loaded "north arrow" family; falls back to a ↑ N TextNote.
        /// </summary>
        [MCPMethod("placeNorthArrow", Category = "Annotation", Description = "Places a north arrow annotation in each floor plan view, using a loaded family or a TextNote fallback")]
        public static string PlaceNorthArrow(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Find any loaded north-arrow family symbol
                FamilySymbol northSymbol = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(fs =>
                        fs.FamilyName.IndexOf("north", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        fs.Name.IndexOf("north", StringComparison.OrdinalIgnoreCase) >= 0);

                // True north rotation angle (project north → true north)
                double trueNorthAngle = 0;
                try
                {
                    var pos = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
                    trueNorthAngle = pos.Angle; // radians, project north → true north
                }
                catch { }

                var floorPlanViews = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .Where(v => !v.IsTemplate && v.ViewType == ViewType.FloorPlan)
                    .ToList();

                int placed = 0;
                var results = new List<object>();

                foreach (var view in floorPlanViews)
                {
                    // Position at upper-right of crop box
                    XYZ origin;
                    try
                    {
                        var crop = view.CropBox;
                        origin = new XYZ(crop.Max.X - 3.0, crop.Max.Y - 3.0, 0);
                    }
                    catch { origin = XYZ.Zero; }

                    bool usedFamily = false;
                    using (var trans = new Transaction(doc, $"BM: North arrow {view.Name}"))
                    {
                        trans.Start();
                        var fo = trans.GetFailureHandlingOptions();
                        fo.SetFailuresPreprocessor(new WarningSwallower());
                        trans.SetFailureHandlingOptions(fo);

                        if (northSymbol != null)
                        {
                            try
                            {
                                if (!northSymbol.IsActive) northSymbol.Activate();
                                var inst = doc.Create.NewFamilyInstance(origin, northSymbol, view);
                                // Apply true north rotation
                                if (Math.Abs(trueNorthAngle) > 0.001)
                                {
                                    var axis = Line.CreateBound(origin, origin + XYZ.BasisZ);
                                    ElementTransformUtils.RotateElement(doc, inst.Id, axis, trueNorthAngle);
                                }
                                usedFamily = true;
                            }
                            catch
                            {
                                // Family placement failed — fall through to TextNote
                            }
                        }

                        if (!usedFamily)
                        {
                            // Fallback: simple TextNote indicating north direction
                            var tnt = new FilteredElementCollector(doc)
                                .OfClass(typeof(TextNoteType))
                                .Cast<TextNoteType>()
                                .FirstOrDefault();
                            if (tnt != null)
                            {
                                var opts = new TextNoteOptions(tnt.Id)
                                {
                                    HorizontalAlignment = HorizontalTextAlignment.Center,
                                };
                                TextNote.Create(doc, view.Id, origin, "↑ N", opts);
                            }
                        }

                        trans.Commit();
                        placed++;
                        results.Add(new { viewName = view.Name, method = usedFamily ? "family" : "textNote" });
                    }
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    success = true,
                    placed,
                    familyUsed  = northSymbol?.FamilyName ?? "none (TextNote fallback)",
                    trueNorthAngleDeg = trueNorthAngle * 180 / Math.PI,
                    views = results,
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        #endregion

    }
}
