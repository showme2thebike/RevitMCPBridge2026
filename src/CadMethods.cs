using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPBridge.Helpers;
using RevitMCPBridge.Validation;

namespace RevitMCPBridge
{
    /// <summary>
    /// AutoCAD DWG interoperability: export views/sheets to DWG for consultants,
    /// import DWG details into drafting views, link DWG backgrounds, and query
    /// existing CAD content. Practice guardrails live in the method descriptions
    /// and knowledge/cad-dwg-practices.md: LINK for model views, IMPORT only into
    /// drafting views, never explode imported CAD.
    /// </summary>
    public static class CadMethods
    {
        [MCPMethod("exportDwg", Category = "CAD", Description = "Export views or sheets to DWG (AutoCAD) files — the 'send backgrounds to the consultant' workflow. Params: viewIds (array, required), folder (optional — defaults to Documents/BIM Monkey/DWG Export), filePrefix (optional), version ('2018' default, or '2013'/'2010'/'2007' — consultants usually want 2018), setupName (optional — a saved DWG export setup so layer mapping follows the firm standard; omit for Revit defaults)")]
        public static string ExportDwg(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var viewIdsArr = parameters["viewIds"] as JArray;
                if (viewIdsArr == null || viewIdsArr.Count == 0)
                    return ResponseBuilder.Error("exportDwg", "viewIds (array of view/sheet element IDs) is required").Build();

                var viewIds = new List<ElementId>();
                foreach (var v in viewIdsArr)
                {
                    var id = new ElementId(v.ToObject<int>());
                    if (!(doc.GetElement(id) is View))
                        return ResponseBuilder.Error("exportDwg", $"Element {id.Value} is not a view or sheet").Build();
                    viewIds.Add(id);
                }

                var folder = parameters["folder"]?.ToString();
                if (string.IsNullOrWhiteSpace(folder))
                    folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BIM Monkey", "DWG Export");
                Directory.CreateDirectory(folder);

                // Export setup: use a saved setup so layers follow the firm
                // standard (AIA etc.); fall back to Revit defaults.
                DWGExportOptions options = null;
                var setupName = parameters["setupName"]?.ToString();
                var availableSetups = BaseExportOptions.GetPredefinedSetupNames(doc).ToList();
                if (!string.IsNullOrEmpty(setupName))
                {
                    if (!availableSetups.Contains(setupName))
                        return ResponseBuilder.Error("exportDwg", $"Export setup '{setupName}' not found. Available: {string.Join(", ", availableSetups)}").Build();
                    options = DWGExportOptions.GetPredefinedOptions(doc, setupName) as DWGExportOptions;
                }
                if (options == null) options = new DWGExportOptions();

                var version = parameters["version"]?.ToString() ?? "2018";
                switch (version)
                {
                    case "2018": options.FileVersion = ACADVersion.R2018; break;
                    case "2013": options.FileVersion = ACADVersion.R2013; break;
                    case "2010": options.FileVersion = ACADVersion.R2010; break;
                    case "2007": options.FileVersion = ACADVersion.R2007; break;
                    default: return ResponseBuilder.Error("exportDwg", "version must be one of: 2018, 2013, 2010, 2007").Build();
                }

                var prefix = parameters["filePrefix"]?.ToString() ?? "";
                var before = new HashSet<string>(Directory.GetFiles(folder, "*.dwg"));
                var ok = doc.Export(folder, prefix, viewIds, options);
                if (!ok)
                    return ResponseBuilder.Error("exportDwg", "Revit reported the export failed").Build();
                var files = Directory.GetFiles(folder, "*.dwg").Where(f => !before.Contains(f)).Select(Path.GetFileName).ToList();

                return ResponseBuilder.Success()
                    .With("folder", folder)
                    .With("files", files)
                    .With("viewCount", viewIds.Count)
                    .With("dwgVersion", version)
                    .With("setupUsed", string.IsNullOrEmpty(setupName) ? "(Revit defaults)" : setupName)
                    .With("availableSetups", availableSetups)
                    .Build();
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        [MCPMethod("importDwg", Category = "CAD", Description = "Import or link a DWG file. PRACTICE RULES: mode 'import' is for absorbing details into DRAFTING VIEWS only (e.g. a firm's AutoCAD detail library); mode 'link' is correct for model views (site plans, consultant backgrounds) — it stays connected to the source file. NEVER explode imported CAD afterward. Params: filePath (required), viewId (required — drafting view for import; any plan/view for link), mode ('import'|'link', default 'import'), units ('inch','foot','mm','cm','m','default'), placement ('origin'|'center'), colorMode ('bw' default | 'preserve' | 'invert')")]
        public static string ImportDwg(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var v = new ParameterValidator(parameters, "importDwg");
                v.Require("filePath");
                v.Require("viewId");
                v.ThrowIfInvalid();

                var filePath = parameters["filePath"].ToString();
                if (!File.Exists(filePath))
                    return ResponseBuilder.Error("importDwg", $"File not found: {filePath}").Build();
                if (!filePath.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase) && !filePath.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase))
                    return ResponseBuilder.Error("importDwg", "filePath must be a .dwg or .dxf file").Build();

                var view = doc.GetElement(new ElementId(parameters["viewId"].ToObject<int>())) as View;
                if (view == null)
                    return ResponseBuilder.Error("importDwg", "viewId does not resolve to a view").Build();

                var mode = (parameters["mode"]?.ToString() ?? "import").ToLowerInvariant();
                bool isDrafting = view.ViewType == ViewType.DraftingView;
                if (mode == "import" && !isDrafting)
                    return ResponseBuilder.Error("importDwg", $"Import target must be a DRAFTING view (got {view.ViewType}). Details are imported into drafting views; for model views use mode='link' — linked CAD stays connected to the source file and doesn't pollute the model.").Build();

                var options = new DWGImportOptions
                {
                    ThisViewOnly = mode == "import",
                    Placement = (parameters["placement"]?.ToString() ?? "origin") == "center" ? ImportPlacement.Centered : ImportPlacement.Origin,
                };
                switch ((parameters["units"]?.ToString() ?? "default").ToLowerInvariant())
                {
                    case "inch": options.Unit = ImportUnit.Inch; break;
                    case "foot": options.Unit = ImportUnit.Foot; break;
                    case "mm":   options.Unit = ImportUnit.Millimeter; break;
                    case "cm":   options.Unit = ImportUnit.Centimeter; break;
                    case "m":    options.Unit = ImportUnit.Meter; break;
                    default:     options.Unit = ImportUnit.Default; break;
                }
                switch ((parameters["colorMode"]?.ToString() ?? "bw").ToLowerInvariant())
                {
                    case "preserve": options.ColorMode = ImportColorMode.Preserved; break;
                    case "invert":   options.ColorMode = ImportColorMode.Inverted; break;
                    default:         options.ColorMode = ImportColorMode.BlackAndWhite; break;
                }

                ElementId newId = ElementId.InvalidElementId;
                using (var trans = new Transaction(doc, mode == "link" ? "Link DWG" : "Import DWG"))
                {
                    trans.Start();
                    bool ok = mode == "link"
                        ? doc.Link(filePath, options, view, out newId)
                        : doc.Import(filePath, options, view, out newId);
                    if (!ok || newId == ElementId.InvalidElementId)
                    {
                        trans.RollBack();
                        return ResponseBuilder.Error("importDwg", "Revit could not import/link the file — check the DWG version and contents").Build();
                    }
                    trans.Commit();
                }

                return ResponseBuilder.Success()
                    .With("elementId", newId.Value)
                    .With("mode", mode)
                    .With("viewId", view.Id.Value)
                    .With("viewName", view.Name)
                    .With("fileName", Path.GetFileName(filePath))
                    .With("note", mode == "import"
                        ? "Detail linework imported into the drafting view. Do NOT explode it — trace/annotate with native Revit elements as needed."
                        : "DWG linked — it stays connected to the source file and updates when reloaded.")
                    .Build();
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        [MCPMethod("getCadImports", Category = "CAD", Description = "List all CAD (DWG/DXF) imports and links already in the model: name, linked vs imported, owning view (view-specific vs model-wide), pinned state, and the source path for links. Use before importing to avoid duplicates and to audit CAD hygiene (imported model-wide CAD is a red flag).")]
        public static string GetCadImports(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var results = new List<object>();

                foreach (ImportInstance ii in new FilteredElementCollector(doc).OfClass(typeof(ImportInstance)))
                {
                    string ownerView = null;
                    if (ii.OwnerViewId != ElementId.InvalidElementId)
                        ownerView = (doc.GetElement(ii.OwnerViewId) as View)?.Name;

                    string sourcePath = null;
                    if (ii.IsLinked && doc.GetElement(ii.GetTypeId()) is CADLinkType linkType)
                    {
                        try
                        {
                            var extRef = linkType.GetExternalFileReference();
                            if (extRef != null)
                                sourcePath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extRef.GetAbsolutePath());
                        }
                        catch { }
                    }

                    results.Add(new
                    {
                        elementId = ii.Id.Value,
                        name = ii.Category?.Name ?? "(unnamed)",
                        isLinked = ii.IsLinked,
                        ownerView = ownerView ?? "(model-wide)",
                        viewSpecific = ii.ViewSpecific,
                        pinned = ii.Pinned,
                        sourcePath,
                    });
                }

                return ResponseBuilder.Success()
                    .With("count", results.Count)
                    .With("cadInstances", results)
                    .Build();
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }
    }
}
