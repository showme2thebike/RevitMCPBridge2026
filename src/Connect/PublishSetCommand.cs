using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPBridge2026.AgentFramework;

namespace RevitMCPBridge2026.Connect
{
    /// <summary>
    /// Publish set from the ribbon (Connect's lead feature; the full plugin does the same from Banana Chat's publishSet tool).
    /// Dialog → preview/confirm → DocumentMethods.ExportSheetsToPDF (one PDF per sheet; Revit 2026 cannot combine) →
    /// POST /api/plugin/publish-set (api/publishSet.js). The server publishes when analysis finishes and the Inbox reports it.
    /// Runs on Revit's thread (an IExternalCommand), so the export is a direct call and the upload blocks with a wait cursor.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class PublishSetCommand : IExternalCommand
    {
        public const string PublishUrl = "https://bimmonkey-production.up.railway.app/api/plugin/publish-set";

        public static string ProjectNameFor(Document doc)
        {
            if (doc == null) return null;
            var t = doc.Title ?? "";
            if (t.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase)) t = t.Substring(0, t.Length - 4);
            return t.Trim();
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RibbonTelemetry.Click("PublishSet");
            var key = RevitMCPBridge.AgentFramework.SessionTokenManager.ReadBimMonkeyApiKey();
            try
            {
                if (string.IsNullOrEmpty(key)) { TaskDialog.Show("BIM Monkey Connect", "No BIM Monkey API key found. Run the installer again or add it to Documents\\BIM Monkey\\CLAUDE.md."); return Result.Succeeded; }
                var uiApp = commandData.Application;
                var doc = uiApp.ActiveUIDocument?.Document;
                if (doc == null) { TaskDialog.Show("BIM Monkey Connect", "Open a project first."); return Result.Succeeded; }

                var sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                    .Where(s => !s.IsPlaceholder).OrderBy(s => s.SheetNumber).ToList();
                if (sheets.Count == 0) { TaskDialog.Show("BIM Monkey Connect", "This project has no sheets to publish."); return Result.Succeeded; }

                var dlg = new PublishSetDialog(ProjectNameFor(doc), sheets);
                TelemetryService.Track(key, "publish_set", toolName: "PublishSetButton", metadata: new { stage = "dialog", sheets = sheets.Count, edition = "connect" });
                if (dlg.ShowDialog() != true) return Result.Succeeded;

                // Validate through the same parser Banana Chat's tool uses
                var input = new JObject
                {
                    ["projectName"] = dlg.ProjectName, ["label"] = dlg.Label, ["notes"] = new JArray(dlg.Notes),
                    ["collaborators"] = new JArray(dlg.Collaborators.Select(c => new JObject { ["name"] = c.name, ["email"] = c.email })),
                    ["notify"] = dlg.Notify, ["addToLibrary"] = dlg.AddToLibrary,
                };
                var req = PublishSet.Parse(input, out var parseError);
                if (req == null) { TaskDialog.Show("BIM Monkey Connect", parseError); return Result.Succeeded; }
                var chosen = dlg.SelectedSheets;
                if (chosen.Count == 0) { TaskDialog.Show("BIM Monkey Connect", "Pick at least one sheet."); return Result.Succeeded; }

                // Preview + confirm (the same contract as the chat tool: nothing leaves the machine before a yes)
                var matched = chosen.Select(s => new SheetMatch { Number = s.SheetNumber, Name = s.Name, Id = s.Id.Value }).ToList();
                var token = PublishSet.ConfirmToken(req.ProjectName, matched.Select(m => m.Number), req.Collaborators.Select(c => c.Email), req.AddToLibrary);
                var preview = PublishSet.PreviewText(req, matched, new List<string>(), token);
                var cut = preview.IndexOf("Show this preview", StringComparison.Ordinal); if (cut > 0) preview = preview.Substring(0, cut).TrimEnd();
                var confirm = new TaskDialog("BIM Monkey Connect — Publish set")
                {
                    MainInstruction = $"Publish {matched.Count} sheet{(matched.Count == 1 ? "" : "s")} of \"{req.ProjectName}\"?",
                    MainContent = preview.Replace("PUBLISH PREVIEW — nothing has been exported or sent yet.\r\n", "").Replace("PUBLISH PREVIEW — nothing has been exported or sent yet.\n", ""),
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No, DefaultButton = TaskDialogResult.No,
                };
                TelemetryService.Track(key, "publish_set", toolName: "PublishSetButton", metadata: new { stage = "preview", sheets = matched.Count, collaborators = req.Collaborators.Count, add_to_library = req.AddToLibrary, edition = "connect" });
                if (confirm.Show() != TaskDialogResult.Yes) return Result.Succeeded;

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var tempDir = Path.Combine(Path.GetTempPath(), "BimMonkeyPublish", Guid.NewGuid().ToString("N"));
                try
                {
                    Directory.CreateDirectory(tempDir);
                    var exportJson = RevitMCPBridge.DocumentMethods.ExportSheetsToPDF(uiApp, new JObject
                    {
                        ["sheetIds"] = new JArray(matched.Select(m => m.Id)), ["outputFolder"] = tempDir,
                        ["fileName"] = "{SheetNumber} - {SheetName}", ["combineIntoSingle"] = false,
                    });
                    var export = JObject.Parse(exportJson);
                    if (export["success"]?.ToObject<bool>() != true)
                    {
                        TelemetryService.Track(key, "publish_set", toolName: "PublishSetButton", success: false, durationMs: sw.ElapsedMilliseconds, metadata: new { stage = "failed", at = "export", edition = "connect" });
                        TaskDialog.Show("BIM Monkey Connect", "PDF export failed: " + (export["error"]?.ToString() ?? "unknown") + "\n\nNothing was published.");
                        return Result.Succeeded;
                    }
                    var files = PublishSet.OrderExportedFiles((export["exportedFiles"] as JArray)?.OfType<JObject>(), matched)
                        .Select(f => f["fullPath"]?.ToString()).Where(p => !string.IsNullOrEmpty(p) && File.Exists(p)).ToList();
                    if (files.Count == 0) { TaskDialog.Show("BIM Monkey Connect", "Revit exported no PDF files. Nothing was published."); return Result.Succeeded; }

                    using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");
                    RevitMCPBridge.AgentFramework.PluginIdentity.Stamp(client.DefaultRequestHeaders);
                    var form = new MultipartFormDataContent();
                    foreach (var path in files)
                        form.Add(new ByteArrayContent(File.ReadAllBytes(path)) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf") } }, "files", Path.GetFileName(path));
                    form.Add(new StringContent(req.ProjectName), "projectName");
                    if (req.Label != null) form.Add(new StringContent(req.Label), "label");
                    form.Add(new StringContent(JsonConvert.SerializeObject(req.Notes)), "notes");
                    form.Add(new StringContent(JsonConvert.SerializeObject(req.Collaborators.Select(c => new { name = c.Name, email = c.Email }))), "collaborators");
                    form.Add(new StringContent(JsonConvert.SerializeObject(matched.Select(m => m.Number))), "sheetNumbers");
                    form.Add(new StringContent(req.Notify ? "true" : "false"), "notify");
                    form.Add(new StringContent(req.AddToLibrary ? "true" : "false"), "addToLibrary");

                    HttpResponseMessage resp;
                    var oldCursor = System.Windows.Forms.Cursor.Current;
                    try { System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor; resp = RevitMCPBridge.AgentFramework.ApiRetry.PostAsync(client, PublishUrl, form).GetAwaiter().GetResult(); }
                    finally { System.Windows.Forms.Cursor.Current = oldCursor; }
                    var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    JObject server = null; try { server = JObject.Parse(body); } catch { }
                    if (!resp.IsSuccessStatusCode)
                    {
                        TelemetryService.Track(key, "publish_set", toolName: "PublishSetButton", success: false, durationMs: sw.ElapsedMilliseconds, metadata: new { stage = "failed", at = "upload", status = (int)resp.StatusCode, edition = "connect" });
                        TaskDialog.Show("BIM Monkey Connect", $"BIM Monkey did not accept the set ({(int)resp.StatusCode}): {server?["error"]?.ToString() ?? body}\n\nNothing was published.");
                        return Result.Succeeded;
                    }
                    TelemetryService.Track(key, "publish_set", toolName: "PublishSetButton", success: true, durationMs: sw.ElapsedMilliseconds,
                        metadata: new { stage = "uploaded", sheets = matched.Count, files = files.Count, collaborators = req.Collaborators.Count, notify = req.Notify, add_to_library = req.AddToLibrary, job_id = server?["jobId"]?.ToString(), planned_issue = server?["plannedIssueNumber"]?.ToString(), edition = "connect" });
                    TaskDialog.Show("BIM Monkey Connect", PublishSet.ResultText(server, files.Count, new List<string>()));
                    return Result.Succeeded;
                }
                finally { try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { } }
            }
            catch (Exception ex)
            {
                TelemetryService.Track(key, "publish_set", toolName: "PublishSetButton", success: false, metadata: new { stage = "failed", at = "exception", error = ex.Message.Length > 160 ? ex.Message.Substring(0, 160) : ex.Message, edition = "connect" });
                message = ex.Message; return Result.Failed;
            }
        }
    }

    /// <summary>Code-only WPF dialog (no XAML, same as the rest of the plugin's windows).</summary>
    internal class PublishSetDialog : Window
    {
        private readonly System.Windows.Controls.TextBox _project = new System.Windows.Controls.TextBox();
        private readonly System.Windows.Controls.TextBox _label = new System.Windows.Controls.TextBox();
        private readonly System.Windows.Controls.TextBox _notes = new System.Windows.Controls.TextBox { AcceptsReturn = true, Height = 54, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        private readonly System.Windows.Controls.TextBox _people = new System.Windows.Controls.TextBox { AcceptsReturn = true, Height = 54, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        private readonly CheckBox _notify = new CheckBox { Content = "Email each collaborator their link", IsChecked = true };
        private readonly CheckBox _library = new CheckBox { Content = "This is the final set: add it to my library (replaces the project's library pages and informs firm standards)", IsChecked = false };
        private readonly List<CheckBox> _sheetBoxes = new List<CheckBox>();
        private readonly List<ViewSheet> _sheets;

        public string ProjectName => _project.Text.Trim();
        public string Label => _label.Text.Trim();
        public bool Notify => _notify.IsChecked == true;
        public bool AddToLibrary => _library.IsChecked == true;
        public List<string> Notes => _notes.Text.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        public List<ViewSheet> SelectedSheets => _sheetBoxes.Where(b => b.IsChecked == true).Select(b => (ViewSheet)b.Tag).ToList();

        /// <summary>One person per line: "Name &lt;email&gt;", "Name, email", or just the email.</summary>
        public List<(string name, string email)> Collaborators
        {
            get
            {
                var list = new List<(string, string)>();
                foreach (var raw in _people.Text.Split('\n'))
                {
                    var line = raw.Trim(); if (line.Length == 0) continue;
                    var m = System.Text.RegularExpressions.Regex.Match(line, @"^(.*?)[\s,<]*([^\s<>,]+@[^\s<>,]+)>?\s*$");
                    if (m.Success) list.Add((m.Groups[1].Value.Trim(' ', ',', '<'), m.Groups[2].Value.Trim()));
                    else list.Add(("", line));
                }
                return list;
            }
        }

        public PublishSetDialog(string projectName, List<ViewSheet> sheets)
        {
            _sheets = sheets;
            Title = "BIM Monkey Connect — Publish set";
            Width = 560; Height = 640; WindowStartupLocation = WindowStartupLocation.CenterScreen; ResizeMode = ResizeMode.CanResize;
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"); FontSize = 12;
            _project.Text = projectName ?? "";

            var root = new System.Windows.Controls.Grid { Margin = new Thickness(14) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var top = new StackPanel();
            top.Children.Add(Labelled("Project (as it appears in the web app Library)", _project));
            top.Children.Add(Labelled("Issue label (optional): Permit set, Bid set, Revision 2…", _label));
            top.Children.Add(Labelled("Notes shown to collaborators with this issue (one per line, optional)", _notes));
            top.Children.Add(Labelled("Collaborators (one per line: Name <email>, or just the email; optional)", _people));
            top.Children.Add(_notify); _notify.Margin = new Thickness(0, 2, 0, 2);
            top.Children.Add(_library); _library.Margin = new Thickness(0, 2, 0, 8);
            System.Windows.Controls.Grid.SetRow(top, 0); root.Children.Add(top);

            var sheetHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var all = new Button { Content = "All", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
            var none = new Button { Content = "None", Padding = new Thickness(8, 2, 8, 2) };
            all.Click += (s, e) => _sheetBoxes.ForEach(b => b.IsChecked = true);
            none.Click += (s, e) => _sheetBoxes.ForEach(b => b.IsChecked = false);
            DockPanel.SetDock(none, Dock.Right); DockPanel.SetDock(all, Dock.Right);
            sheetHeader.Children.Add(none); sheetHeader.Children.Add(all);
            sheetHeader.Children.Add(new TextBlock { Text = $"Sheets to publish ({sheets.Count})", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            var sheetStack = new StackPanel();
            foreach (var s in sheets)
            {
                var cb = new CheckBox { Content = $"{s.SheetNumber}  {s.Name}", IsChecked = true, Tag = s, Margin = new Thickness(0, 1, 0, 1) };
                _sheetBoxes.Add(cb); sheetStack.Children.Add(cb);
            }
            var sheetBox = new DockPanel();
            DockPanel.SetDock(sheetHeader, Dock.Top); sheetBox.Children.Add(sheetHeader);
            sheetBox.Children.Add(new ScrollViewer { Content = sheetStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderThickness = new Thickness(1), BorderBrush = System.Windows.Media.Brushes.LightGray, Padding = new Thickness(6) });
            System.Windows.Controls.Grid.SetRow(sheetBox, 1); root.Children.Add(sheetBox);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var cancel = new Button { Content = "Cancel", Width = 90, Margin = new Thickness(0, 0, 8, 0), IsCancel = true };
            var ok = new Button { Content = "Preview…", Width = 110, IsDefault = true };
            ok.Click += (s, e) =>
            {
                if (ProjectName.Length == 0) { MessageBox.Show(this, "Project name is required.", "BIM Monkey Connect"); return; }
                if (SelectedSheets.Count == 0) { MessageBox.Show(this, "Pick at least one sheet.", "BIM Monkey Connect"); return; }
                DialogResult = true;
            };
            buttons.Children.Add(cancel); buttons.Children.Add(ok);
            System.Windows.Controls.Grid.SetRow(buttons, 2); root.Children.Add(buttons);
            Content = root;
        }

        private static UIElement Labelled(string label, UIElement input)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            sp.Children.Add(new TextBlock { Text = label, Foreground = System.Windows.Media.Brushes.DimGray, Margin = new Thickness(0, 0, 0, 2) });
            sp.Children.Add(input);
            return sp;
        }
    }
}
