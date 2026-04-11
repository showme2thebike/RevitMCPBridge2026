using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPBridge.Helpers;
using Serilog;

namespace RevitMCPBridge.NcsClassifier
{
    public class NcsClassifierMethods
    {
        [MCPMethod("classifyAndPackViews",
            Category    = "SheetLayout",
            Description = "Runs the full NCS classification + sheet packing pipeline on the current document's views. Returns a pre-assigned sheet layout block (identical to the Railway daemon pipeline) that Claude uses to place sheets correctly. Call this BEFORE createSheet or placeViewOnSheet.")]
        public static string ClassifyAndPackViews(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument?.Document;
                if (doc == null)
                    return ResponseBuilder.Error("No active document").Build();

                // Optional: filter to specific view IDs
                HashSet<long> filterIds = null;
                if (parameters?["viewIds"] is JArray arr && arr.Count > 0)
                    filterIds = new HashSet<long>(arr.Select(t => (long)t));

                // Collect all placeable, non-template views
                var allViews = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate && IsPlaceable(v))
                    .ToList();

                if (filterIds != null)
                    allViews = allViews.Where(v => filterIds.Contains(v.Id.Value)).ToList();

                // Convert to NcsViewInfo
                var viewInfos = allViews.Select(v => ToViewInfo(v, doc)).ToList();

                // Run classification
                var classified = NcsViewClassifier.ClassifyInventory(viewInfos);

                // Run packing
                var packed = NcsSheetPacker.PackInventory(classified);

                // Build the prompt block
                var promptBlock = NcsSheetPacker.BuildPromptInventoryBlock(packed, classified);

                Log.Information(
                    "NCS classify+pack: {Total} views → {Definite} definite, {Probable} probable, " +
                    "{Ambiguous} ambiguous, {Blocked} blocked → {Sheets} sheets, {Gaps} gaps",
                    classified.Stats.Total, classified.Stats.Definite, classified.Stats.Probable,
                    classified.Stats.Ambiguous, classified.Stats.Blocked,
                    packed.Stats.TotalSheets, packed.Stats.GapSheets);

                return ResponseBuilder.Success()
                    .With("promptBlock",    promptBlock)
                    .With("totalViews",     classified.Stats.Total)
                    .With("classifiedPct",  classified.Stats.ClassifiedPercent)
                    .With("totalSheets",    packed.Stats.TotalSheets)
                    .With("gaps",           packed.Stats.GapSheets)
                    .With("ambiguous",      packed.Stats.AmbiguousViewCount)
                    .With("permitWarnings", classified.PermitWarnings.Count)
                    .With("isRenovation",   classified.RenovationStatus.IsRenovation)
                    .With("renovationValid",classified.RenovationStatus.IsValid)
                    .With("sheets", packed.Sheets
                        .Where(s => !s.IsGap)
                        .Select(s => new JObject
                        {
                            ["sheetId"]    = s.SheetId,
                            ["slotKey"]    = s.SlotKey,
                            ["level"]      = s.Level,
                            ["fillPct"]    = s.FillPercent,
                            ["needsMore"]  = s.NeedsMoreContent,
                            ["viewports"]  = new JArray(s.Viewports.Select(vp => new JObject
                            {
                                ["id"]        = vp.Id,
                                ["name"]      = vp.Name,
                                ["viewType"]  = vp.ViewType,
                                ["confidence"]= vp.Confidence,
                                ["planSub"]   = vp.PlanSubType,
                                ["detailSub"] = vp.DetailSubType,
                                ["level"]     = vp.Level,
                                ["reno"]      = vp.RenovationCondition
                            }))
                        }))
                    .Build();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "classifyAndPackViews failed");
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        // ─── REVIT → NcsViewInfo CONVERSION ──────────────────────────────

        public static NcsViewInfo ToViewInfo(View v, Document doc)
        {
            var info = new NcsViewInfo
            {
                Id         = v.Id.Value,
                Name       = v.Name,
                ViewType   = MapViewType(v.ViewType),
                Scale      = v.Scale > 0 ? v.Scale : 0,
                IsInternal = false
            };

            // View template name
            if (v.ViewTemplateId != ElementId.InvalidElementId)
            {
                try
                {
                    var tmpl = doc.GetElement(v.ViewTemplateId) as View;
                    if (tmpl != null) info.ViewTemplate = tmpl.Name;
                }
                catch { }
            }

            // Crop box dimensions (Revit internal units = feet)
            try
            {
                if (v.CropBoxActive && v.CropBox != null)
                {
                    var cb = v.CropBox;
                    info.CropWidthFt  = Math.Abs(cb.Max.X - cb.Min.X);
                    info.CropHeightFt = Math.Abs(cb.Max.Y - cb.Min.Y);
                }
            }
            catch { }

            return info;
        }

        private static string MapViewType(ViewType vt)
        {
            switch (vt)
            {
                case ViewType.FloorPlan:    return "FloorPlan";
                case ViewType.CeilingPlan:  return "CeilingPlan";
                case ViewType.Elevation:    return "Elevation";
                case ViewType.Section:      return "Section";
                case ViewType.Detail:       return "Detail";
                case ViewType.DraftingView: return "DraftingView";
                case ViewType.Schedule:     return "Schedule";
                case ViewType.PanelSchedule:return "PanelSchedule";
                case ViewType.Legend:       return "Legend";
                case ViewType.ThreeD:       return "ThreeD";
                default:                    return vt.ToString();
            }
        }

        private static bool IsPlaceable(View v)
        {
            // Exclude sheet views themselves and project browser views
            if (v.ViewType == ViewType.ProjectBrowser) return false;
            if (v.ViewType == ViewType.SystemBrowser)  return false;
            if (v is ViewSheet) return false;
            if (v.ViewType == ViewType.Undefined) return false;
            return v.CanBePrinted;
        }
    }
}
