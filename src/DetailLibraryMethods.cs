using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using RevitMCPBridge2026;

namespace RevitMCPBridge
{
    /// <summary>
    /// MCP endpoints for working with pre-drawn detail views — both views already in the
    /// active project (getUnplacedDraftingViews) and views in an external library file
    /// (listDetailLibraryViews / copyDetailViewFromFile).
    ///
    /// Primary workflow (existing project views):
    ///   1. Daemon calls getUnplacedDraftingViews → gets list of Barrett's pre-drawn details
    ///   2. Generation prompt injects that list so Claude references existing view names
    ///   3. Phase 5 of executePlan places existing views instead of running drawLayerStack
    ///
    /// Secondary workflow (external library file):
    ///   1. Barrett maintains BM_DetailLibrary.rvt with named drafting views
    ///   2. listDetailLibraryViews() returns the available view names
    ///   3. Claude picks library view names in the generation plan (libraryViewName field)
    ///   4. copyDetailViewFromFile() copies the view into the active project at execution time
    /// </summary>
    public static class DetailLibraryMethods
    {
        [MCPMethod("getUnplacedDraftingViews",
            Category = "Detail",
            Description = "Returns all drafting views in the active project that are not placed on any sheet " +
                          "and contain at least one element. These are Barrett's pre-drawn standard details " +
                          "that should be placed before generating any new detail geometry. " +
                          "Call this at the start of every generation run and pass the result to the " +
                          "generation prompt so Claude references existing views by exact name.")]
        public static string GetUnplacedDraftingViews(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // All view IDs currently placed on sheets
                var placedViewIds = new HashSet<ElementId>(
                    new FilteredElementCollector(doc)
                        .OfClass(typeof(Viewport))
                        .Cast<Viewport>()
                        .Select(vp => vp.ViewId)
                );

                var views = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewDrafting))
                    .Cast<ViewDrafting>()
                    .Where(v => !v.IsTemplate && !placedViewIds.Contains(v.Id))
                    .Select(v =>
                    {
                        var elementCount = new FilteredElementCollector(doc, v.Id)
                            .WhereElementIsNotElementType()
                            .Where(e => e.Category != null &&
                                        e.Category.Id.Value != (int)BuiltInCategory.OST_Views)
                            .Count();
                        return new { v, elementCount };
                    })
                    .Where(x => x.elementCount > 0) // skip empty/placeholder views
                    .Select(x => new
                    {
                        id           = (long)x.v.Id.Value,
                        name         = x.v.Name,
                        scale        = x.v.Scale,
                        elementCount = x.elementCount,
                    })
                    .OrderBy(v => v.name)
                    .ToList();

                Log.Information("GetUnplacedDraftingViews: {Count} unplaced drafting views with content", views.Count);

                return JsonConvert.SerializeObject(new
                {
                    success   = true,
                    count     = views.Count,
                    views     = views,
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "GetUnplacedDraftingViews failed");
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
        }

        [MCPMethod("listDetailLibraryViews",
            Category = "Detail",
            Description = "List all drafting view names in a master detail library .rvt file. " +
                          "Call this before generation to know which library views are available. " +
                          "Pass the result to the generation prompt so Claude can reference real library views " +
                          "instead of generating geometry from scratch.")]
        public static string ListDetailLibraryViews(UIApplication uiApp, JObject parameters)
        {
            var sourceFilePath = parameters["sourceFilePath"]?.ToString();

            if (string.IsNullOrEmpty(sourceFilePath))
                return JsonConvert.SerializeObject(new { success = false, error = "sourceFilePath is required" });

            if (!File.Exists(sourceFilePath))
                return JsonConvert.SerializeObject(new { success = false, error = $"File not found: {sourceFilePath}" });

            Document sourceDoc = null;
            try
            {
                sourceDoc = uiApp.Application.OpenDocumentFile(sourceFilePath);
                if (sourceDoc == null)
                    return JsonConvert.SerializeObject(new { success = false, error = "Could not open file" });

                var views = new FilteredElementCollector(sourceDoc)
                    .OfClass(typeof(ViewDrafting))
                    .Cast<ViewDrafting>()
                    .Where(v => !v.IsTemplate)
                    .Select(v =>
                    {
                        var elementCount = new FilteredElementCollector(sourceDoc, v.Id)
                            .WhereElementIsNotElementType()
                            .Where(e => e.Category != null &&
                                        e.Category.Id.Value != (int)BuiltInCategory.OST_Views)
                            .Count();
                        return new
                        {
                            name         = v.Name,
                            scale        = v.Scale,
                            elementCount = elementCount,
                        };
                    })
                    .Where(v => v.elementCount > 0) // skip empty/placeholder views
                    .OrderBy(v => v.name)
                    .ToList();

                sourceDoc.Close(false);

                Log.Information("ListDetailLibraryViews: {Count} views in {File}", views.Count, Path.GetFileName(sourceFilePath));

                return JsonConvert.SerializeObject(new
                {
                    success  = true,
                    file     = Path.GetFileName(sourceFilePath),
                    viewCount = views.Count,
                    views    = views,
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ListDetailLibraryViews failed");
                try { sourceDoc?.Close(false); } catch { }
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
        }

        [MCPMethod("copyDetailViewFromFile",
            Category = "Detail",
            Description = "Copy a named drafting view from a master detail library .rvt file into the " +
                          "active project. Use listDetailLibraryViews first to get valid view names. " +
                          "Returns the viewId of the copied view so it can be placed on a sheet. " +
                          "Falls back gracefully — if the view is not found, returns success:false with " +
                          "availableViews so the caller can fall back to drawLayerStack.")]
        public static string CopyDetailViewFromFile(UIApplication uiApp, JObject parameters)
        {
            var activeDoc      = uiApp.ActiveUIDocument.Document;
            var sourceFilePath = parameters["sourceFilePath"]?.ToString();
            var viewName       = parameters["viewName"]?.ToString();
            var targetName     = parameters["targetName"]?.ToString(); // optional rename in active doc

            if (string.IsNullOrEmpty(sourceFilePath))
                return JsonConvert.SerializeObject(new { success = false, error = "sourceFilePath is required" });
            if (string.IsNullOrEmpty(viewName))
                return JsonConvert.SerializeObject(new { success = false, error = "viewName is required" });
            if (!File.Exists(sourceFilePath))
                return JsonConvert.SerializeObject(new { success = false, error = $"Library file not found: {sourceFilePath}" });

            Document sourceDoc = null;
            try
            {
                sourceDoc = uiApp.Application.OpenDocumentFile(sourceFilePath);
                if (sourceDoc == null)
                    return JsonConvert.SerializeObject(new { success = false, error = "Could not open library file" });

                // Find the requested drafting view by name (case-insensitive)
                var sourceView = new FilteredElementCollector(sourceDoc)
                    .OfClass(typeof(ViewDrafting))
                    .Cast<ViewDrafting>()
                    .Where(v => !v.IsTemplate)
                    .FirstOrDefault(v => string.Equals(v.Name, viewName, StringComparison.OrdinalIgnoreCase));

                if (sourceView == null)
                {
                    // Return available view names so caller can log or fall back
                    var available = new FilteredElementCollector(sourceDoc)
                        .OfClass(typeof(ViewDrafting))
                        .Cast<ViewDrafting>()
                        .Where(v => !v.IsTemplate)
                        .Select(v => v.Name)
                        .OrderBy(n => n)
                        .ToList();

                    sourceDoc.Close(false);
                    return JsonConvert.SerializeObject(new
                    {
                        success        = false,
                        error          = $"View '{viewName}' not found in library",
                        availableViews = available,
                    });
                }

                // Copy the view element — Revit automatically copies the view contents
                var copyOptions = new CopyPasteOptions();
                copyOptions.SetDuplicateTypeNamesHandler(new ImportDuplicateHandler());

                ICollection<ElementId> copiedIds;
                using (var tx = new Transaction(activeDoc, $"BIM Monkey — Copy Library Detail: {viewName}"))
                {
                    tx.Start();
                    var fho = tx.GetFailureHandlingOptions();
                    fho.SetFailuresPreprocessor(new CopyWarningSwallower());
                    tx.SetFailureHandlingOptions(fho);

                    copiedIds = ElementTransformUtils.CopyElements(
                        sourceDoc,
                        new List<ElementId> { sourceView.Id },
                        activeDoc,
                        Transform.Identity,
                        copyOptions);

                    tx.Commit();
                }

                sourceDoc.Close(false);

                // Find the newly copied ViewDrafting
                var copiedView = copiedIds
                    .Select(id => activeDoc.GetElement(id))
                    .OfType<ViewDrafting>()
                    .FirstOrDefault();

                if (copiedView == null)
                    return JsonConvert.SerializeObject(new { success = false, error = "View copy succeeded but copied view not found in active document" });

                // Rename to targetName (with * suffix) if requested
                var finalName = !string.IsNullOrEmpty(targetName) ? targetName : viewName;
                var markedName = finalName.EndsWith(" *") ? finalName : finalName + " *";

                using (var tx = new Transaction(activeDoc, "BIM Monkey — Name Copied Detail"))
                {
                    tx.Start();
                    try { copiedView.Name = markedName; } catch { /* name collision — keep original */ }
                    tx.Commit();
                }

                Log.Information("CopyDetailViewFromFile: copied '{View}' → '{Name}' (id {Id})",
                    viewName, copiedView.Name, copiedView.Id.Value);

                return JsonConvert.SerializeObject(new
                {
                    success      = true,
                    viewId       = (long)copiedView.Id.Value,
                    viewName     = copiedView.Name,
                    sourceView   = viewName,
                    sourceFile   = Path.GetFileName(sourceFilePath),
                    elementsCopied = copiedIds.Count,
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CopyDetailViewFromFile failed");
                try { sourceDoc?.Close(false); } catch { }
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
        }

        // ── Suppress non-fatal warnings during cross-document copy ─────────────
        private class CopyWarningSwallower : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
            {
                foreach (var f in failuresAccessor.GetFailureMessages())
                    if (f.GetSeverity() == FailureSeverity.Warning)
                        failuresAccessor.DeleteWarning(f);
                return FailureProcessingResult.Continue;
            }
        }
    }
}
