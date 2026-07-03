using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPBridge.AgentFramework;
using WpfColor   = System.Windows.Media.Color;
using WpfBrush   = System.Windows.Media.SolidColorBrush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfGrid    = System.Windows.Controls.Grid;
using WpfSV      = System.Windows.Controls.ScrollViewer;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RunSavedScriptCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp  = commandData.Application;
            var apiKey = SessionTokenManager.ApiKey;

            if (string.IsNullOrEmpty(apiKey))
            {
                TaskDialog.Show("Scripts", "Sign in to BIM Monkey to access your saved scripts.\n\nVisit app.bimmonkey.ai and install the plugin with your account credentials.");
                return Result.Cancelled;
            }

            JArray scripts;
            try
            {
                var resp = RevitMCPBridge2026.SavedScriptsMethods.FetchScriptList(apiKey);
                scripts  = resp["scripts"] as JArray ?? new JArray();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Scripts", $"Could not load scripts:\n{ex.Message}");
                return Result.Failed;
            }

            if (scripts.Count == 0)
            {
                TaskDialog.Show("Scripts",
                    "No saved scripts yet.\n\nUse Banana Chat to generate a Revit script, then click \"Save as Script\" " +
                    "on the response. It will appear here and runs with zero tokens.");
                return Result.Cancelled;
            }

            var picker = new SavedScriptPickerWindow(scripts);
            if (picker.ShowDialog() != true || picker.SelectedScriptId == null)
                return Result.Cancelled;

            JObject script;
            try
            {
                script = RevitMCPBridge2026.SavedScriptsMethods.FetchScript(apiKey, picker.SelectedScriptId);
                if (script == null) { TaskDialog.Show("Scripts", "Script not found."); return Result.Failed; }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Scripts", $"Could not load script:\n{ex.Message}");
                return Result.Failed;
            }

            var execParams = new JObject
            {
                ["code"]   = script["code"]?.ToString() ?? "",
                ["usings"] = script["usings"] ?? new JArray()
            };
            var resultJson = RevitMCPBridge2026.ScriptMethods.ExecuteRevitScript(uiApp, execParams);
            var result     = JObject.Parse(resultJson);

            RevitMCPBridge2026.SavedScriptsMethods.LogRunFireAndForget(apiKey, picker.SelectedScriptId);

            var success = result["success"]?.Value<bool>() ?? false;
            string outputText;
            if (success)
            {
                var raw = result["result"]?.ToString();
                outputText = string.IsNullOrEmpty(raw) ? "Completed successfully." : raw;
            }
            else
            {
                var err  = result["error"]?.ToString() ?? "Unknown error";
                var diag = result["diagnostics"] is JArray d && d.Count > 0
                    ? "\n\n" + string.Join("\n", d) : "";
                outputText = $"Error: {err}{diag}";
            }

            LastScriptResult.Set(picker.SelectedScriptName, picker.SelectedScriptDescription, outputText, success);

            var resultWindow = new ScriptResultWindow(picker.SelectedScriptName, outputText, success);
            resultWindow.ShowDialog();

            return Result.Succeeded;
        }
    }

    // ── Shared color palette (matches Skills window) ──────────────────────────────
    internal static class ScriptColors
    {
        public static readonly WpfBrush Bg      = B(0xFA, 0xFA, 0xFA);
        public static readonly WpfBrush Sidebar = B(0xF3, 0xF3, 0xF3);
        public static readonly WpfBrush Border  = B(0xDA, 0xDA, 0xDA);
        public static readonly WpfBrush Text    = B(0x16, 0x16, 0x16);
        public static readonly WpfBrush Muted   = B(0x6E, 0x6E, 0x6E);
        public static readonly WpfBrush Accent  = B(0x1C, 0x1C, 0x1C);
        public static readonly WpfBrush White   = B(0xFF, 0xFF, 0xFF);
        public static readonly WpfBrush Green   = B(0x16, 0x8E, 0x3C);
        public static readonly WpfBrush Red     = B(0xB9, 0x14, 0x14);
        public static readonly WpfBrush LightGray = B(0xBB, 0xBB, 0xBB);

        private static WpfBrush B(byte r, byte g, byte b)
            => new WpfBrush(WpfColor.FromRgb(r, g, b));
    }

    // ── Script picker (two-panel, matches Skills aesthetic) ───────────────────────
    internal class SavedScriptPickerWindow : System.Windows.Window
    {
        public string SelectedScriptId          { get; private set; }
        public string SelectedScriptName        { get; private set; }
        public string SelectedScriptDescription { get; private set; }

        private readonly System.Windows.Controls.ListBox  _list;
        private readonly System.Windows.Controls.TextBlock _detailName;
        private readonly System.Windows.Controls.TextBlock _detailDesc;
        private readonly System.Windows.Controls.TextBlock _detailMeta;
        private readonly System.Windows.Controls.Button    _runBtn;

        public SavedScriptPickerWindow(JArray scripts)
        {
            Title  = "Scripts — BIM Monkey";
            Width  = 620;
            Height = 420;
            MinWidth  = 480;
            MinHeight = 300;
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            Background = ScriptColors.Bg;
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

            // Root: left | separator | right ; content | separator | buttons
            var root = new WpfGrid();
            root.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(230) });
            root.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1) });
            root.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1) });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

            // Left sidebar
            var sidebar = new WpfGrid { Background = ScriptColors.Sidebar };
            sidebar.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            sidebar.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            WpfGrid.SetColumn(sidebar, 0);
            WpfGrid.SetRow(sidebar, 0);
            root.Children.Add(sidebar);

            var sidebarHeader = new System.Windows.Controls.TextBlock
            {
                Text       = "SCRIPTS",
                FontSize   = 8,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = ScriptColors.Muted,
                Margin     = new System.Windows.Thickness(14, 12, 14, 8),
            };
            WpfGrid.SetRow(sidebarHeader, 0);
            sidebar.Children.Add(sidebarHeader);

            _list = new System.Windows.Controls.ListBox
            {
                Background      = WpfBrushes.Transparent,
                BorderThickness = new System.Windows.Thickness(0),
                Margin          = new System.Windows.Thickness(0),
                Padding         = new System.Windows.Thickness(0),
            };
            WpfSV.SetHorizontalScrollBarVisibility(_list, System.Windows.Controls.ScrollBarVisibility.Disabled);
            _list.SelectionChanged += OnSelectionChanged;
            _list.MouseDoubleClick += (s, e) => TryConfirm();

            foreach (JObject item in scripts)
            {
                var name = item["name"]?.ToString() ?? "(unnamed)";
                var desc = item["description"]?.ToString() ?? "";
                var runs = item["run_count"]?.Value<int>() ?? 0;

                var panel = new System.Windows.Controls.StackPanel
                    { Margin = new System.Windows.Thickness(14, 9, 10, 9) };
                panel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text         = name,
                    FontSize     = 11.5,
                    FontWeight   = System.Windows.FontWeights.SemiBold,
                    Foreground   = ScriptColors.Text,
                    TextTrimming = System.Windows.TextTrimming.CharacterEllipsis,
                });
                if (!string.IsNullOrEmpty(desc))
                    panel.Children.Add(new System.Windows.Controls.TextBlock
                    {
                        Text         = desc,
                        FontSize     = 10,
                        Foreground   = ScriptColors.Muted,
                        TextTrimming = System.Windows.TextTrimming.CharacterEllipsis,
                        Margin       = new System.Windows.Thickness(0, 2, 0, 0),
                    });
                panel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text       = $"{runs} run{(runs != 1 ? "s" : "")}",
                    FontSize   = 9.5,
                    Foreground = ScriptColors.LightGray,
                    Margin     = new System.Windows.Thickness(0, 2, 0, 0),
                });

                var li = new System.Windows.Controls.ListBoxItem
                {
                    Content         = panel,
                    Tag             = item,
                    Padding         = new System.Windows.Thickness(0),
                    BorderThickness = new System.Windows.Thickness(0, 0, 0, 1),
                    BorderBrush     = ScriptColors.Border,
                    Background      = WpfBrushes.Transparent,
                };
                _list.Items.Add(li);
            }

            WpfGrid.SetRow(_list, 1);
            sidebar.Children.Add(_list);

            // Vertical separator
            var vSep = new System.Windows.Controls.Border { Background = ScriptColors.Border };
            WpfGrid.SetColumn(vSep, 1);
            WpfGrid.SetRow(vSep, 0);
            root.Children.Add(vSep);

            // Right detail panel
            var detail = new System.Windows.Controls.StackPanel
                { Margin = new System.Windows.Thickness(20, 18, 20, 16) };
            WpfGrid.SetColumn(detail, 2);
            WpfGrid.SetRow(detail, 0);
            root.Children.Add(detail);

            _detailName = new System.Windows.Controls.TextBlock
            {
                FontSize     = 14,
                FontWeight   = System.Windows.FontWeights.SemiBold,
                Foreground   = ScriptColors.Text,
                TextWrapping = System.Windows.TextWrapping.Wrap,
                Margin       = new System.Windows.Thickness(0, 0, 0, 6),
                Text         = "Select a script",
            };
            detail.Children.Add(_detailName);

            _detailDesc = new System.Windows.Controls.TextBlock
            {
                FontSize     = 11.5,
                Foreground   = ScriptColors.Muted,
                TextWrapping = System.Windows.TextWrapping.Wrap,
                Margin       = new System.Windows.Thickness(0, 0, 0, 10),
            };
            detail.Children.Add(_detailDesc);

            _detailMeta = new System.Windows.Controls.TextBlock
            {
                FontSize   = 10.5,
                Foreground = ScriptColors.Muted,
            };
            detail.Children.Add(_detailMeta);

            // Horizontal separator
            var hSep = new System.Windows.Controls.Border
                { Background = ScriptColors.Border, Height = 1 };
            WpfGrid.SetColumn(hSep, 0);
            WpfGrid.SetColumnSpan(hSep, 3);
            WpfGrid.SetRow(hSep, 1);
            root.Children.Add(hSep);

            // Button bar
            var btnBarGrid = new WpfGrid { Margin = new System.Windows.Thickness(14, 10, 14, 12) };
            btnBarGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            btnBarGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = System.Windows.GridLength.Auto });
            btnBarGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = System.Windows.GridLength.Auto });
            WpfGrid.SetColumn(btnBarGrid, 0);
            WpfGrid.SetColumnSpan(btnBarGrid, 3);
            WpfGrid.SetRow(btnBarGrid, 2);
            root.Children.Add(btnBarGrid);

            var countLabel = new System.Windows.Controls.TextBlock
            {
                Text              = $"{scripts.Count} script{(scripts.Count != 1 ? "s" : "")}",
                FontSize          = 10.5,
                Foreground        = ScriptColors.Muted,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
            };
            WpfGrid.SetColumn(countLabel, 0);
            btnBarGrid.Children.Add(countLabel);

            var cancelBtn = MakeButton("Cancel", false);
            cancelBtn.IsCancel = true;
            cancelBtn.Margin   = new System.Windows.Thickness(0, 0, 8, 0);
            cancelBtn.Click   += (s, e) => { DialogResult = false; Close(); };
            WpfGrid.SetColumn(cancelBtn, 1);
            btnBarGrid.Children.Add(cancelBtn);

            _runBtn = MakeButton("Run", true);
            _runBtn.IsDefault  = true;
            _runBtn.IsEnabled  = false;
            _runBtn.Click     += (s, e) => TryConfirm();
            WpfGrid.SetColumn(_runBtn, 2);
            btnBarGrid.Children.Add(_runBtn);

            Content = root;
            if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        }

        private void OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_list.SelectedItem is System.Windows.Controls.ListBoxItem li && li.Tag is JObject item)
            {
                _detailName.Text = item["name"]?.ToString() ?? "";
                _detailDesc.Text = item["description"]?.ToString() ?? "";
                var runs    = item["run_count"]?.Value<int>() ?? 0;
                var lastRun = item["last_run_at"]?.ToString();
                _detailMeta.Text = lastRun != null
                    ? $"{runs} run{(runs != 1 ? "s" : "")}   ·   Last run {FormatDate(lastRun)}"
                    : $"{runs} run{(runs != 1 ? "s" : "")}";
                _runBtn.IsEnabled = true;
            }
        }

        private static string FormatDate(string iso)
        {
            return DateTime.TryParse(iso, out var d) ? d.ToString("MMM d, yyyy") : iso;
        }

        private void TryConfirm()
        {
            if (_list.SelectedItem is System.Windows.Controls.ListBoxItem li && li.Tag is JObject item)
            {
                SelectedScriptId          = item["id"]?.ToString();
                SelectedScriptName        = item["name"]?.ToString() ?? "Script";
                SelectedScriptDescription = item["description"]?.ToString() ?? "";
                DialogResult = true;
                Close();
            }
        }

        private static System.Windows.Controls.Button MakeButton(string label, bool primary)
        {
            return new System.Windows.Controls.Button
            {
                Content         = label,
                Width           = 80,
                Height          = 28,
                FontSize        = 12,
                FontFamily      = new System.Windows.Media.FontFamily("Segoe UI"),
                Cursor          = System.Windows.Input.Cursors.Hand,
                Background      = primary ? ScriptColors.Accent : ScriptColors.Bg,
                Foreground      = primary ? ScriptColors.White  : ScriptColors.Text,
                BorderBrush     = primary ? ScriptColors.Accent : ScriptColors.Border,
                BorderThickness = new System.Windows.Thickness(1),
            };
        }
    }

    // ── Script result window (selectable, copyable output) ────────────────────────
    internal class ScriptResultWindow : System.Windows.Window
    {
        public ScriptResultWindow(string scriptName, string output, bool success)
        {
            Title  = $"Script Result — {scriptName}";
            Width  = 560;
            Height = 420;
            MinWidth  = 380;
            MinHeight = 260;
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            Background = ScriptColors.Bg;
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

            var root = new WpfGrid();
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

            // Status bar
            var statusBar = new System.Windows.Controls.Border
            {
                Background = success ? ScriptColors.Green : ScriptColors.Red,
                Padding    = new System.Windows.Thickness(14, 8, 14, 8),
                Child      = new System.Windows.Controls.TextBlock
                {
                    Text       = success ? $"✓  {scriptName} completed" : $"✕  {scriptName} failed",
                    Foreground = ScriptColors.White,
                    FontSize   = 12,
                    FontWeight = System.Windows.FontWeights.SemiBold,
                },
            };
            WpfGrid.SetRow(statusBar, 0);
            root.Children.Add(statusBar);

            // Selectable output (read-only TextBox — allows selection, Ctrl+A, Ctrl+C)
            var outputBox = new System.Windows.Controls.TextBox
            {
                Text            = output,
                IsReadOnly      = true,
                FontFamily      = new System.Windows.Media.FontFamily("Consolas, Courier New"),
                FontSize        = 11.5,
                Foreground      = ScriptColors.Text,
                Background      = ScriptColors.Bg,
                BorderThickness = new System.Windows.Thickness(0),
                Padding         = new System.Windows.Thickness(14, 12, 14, 12),
                TextWrapping    = System.Windows.TextWrapping.Wrap,
                AcceptsReturn   = false,
                Cursor          = System.Windows.Input.Cursors.IBeam,
            };
            WpfSV.SetVerticalScrollBarVisibility(outputBox,   System.Windows.Controls.ScrollBarVisibility.Auto);
            WpfSV.SetHorizontalScrollBarVisibility(outputBox, System.Windows.Controls.ScrollBarVisibility.Disabled);
            WpfGrid.SetRow(outputBox, 1);
            root.Children.Add(outputBox);

            // Button bar
            var btnBorder = new System.Windows.Controls.Border
            {
                BorderBrush     = ScriptColors.Border,
                BorderThickness = new System.Windows.Thickness(0, 1, 0, 0),
                Padding         = new System.Windows.Thickness(14, 10, 14, 12),
            };
            var btnRow = new System.Windows.Controls.StackPanel
            {
                Orientation         = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            };

            var copyBtn = MakeResultButton("Copy All", false);
            copyBtn.Margin = new System.Windows.Thickness(0, 0, 8, 0);
            copyBtn.Click += (s, e) =>
            {
                System.Windows.Clipboard.SetText(output);
                ((System.Windows.Controls.Button)s).Content = "Copied ✓";
            };
            btnRow.Children.Add(copyBtn);

            var closeBtn = MakeResultButton("Close", true);
            closeBtn.IsDefault = true;
            closeBtn.Click    += (s, e) => Close();
            btnRow.Children.Add(closeBtn);

            btnBorder.Child = btnRow;
            WpfGrid.SetRow(btnBorder, 2);
            root.Children.Add(btnBorder);

            Content = root;
            Loaded += (s, e) => outputBox.Focus();
        }

        private static System.Windows.Controls.Button MakeResultButton(string label, bool primary)
        {
            return new System.Windows.Controls.Button
            {
                Content         = label,
                Width           = primary ? 72 : 84,
                Height          = 28,
                FontSize        = 12,
                Background      = primary ? ScriptColors.Accent : ScriptColors.Bg,
                Foreground      = primary ? ScriptColors.White  : ScriptColors.Text,
                BorderBrush     = primary ? ScriptColors.Accent : ScriptColors.Border,
                BorderThickness = new System.Windows.Thickness(1),
                Cursor          = System.Windows.Input.Cursors.Hand,
            };
        }
    }
}
