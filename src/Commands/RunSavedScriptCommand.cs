using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPBridge.AgentFramework;

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
                TaskDialog.Show("Saved Scripts", "Sign in to BIM Monkey to access your saved scripts.\n\nVisit app.bimmonkey.ai and install the plugin with your account credentials.");
                return Result.Cancelled;
            }

            // Fetch script list
            JArray scripts;
            try
            {
                var resp    = RevitMCPBridge2026.SavedScriptsMethods.FetchScriptList(apiKey);
                scripts = resp["scripts"] as JArray ?? new JArray();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Saved Scripts", $"Could not load scripts:\n{ex.Message}");
                return Result.Failed;
            }

            if (scripts.Count == 0)
            {
                TaskDialog.Show("Saved Scripts",
                    "No saved scripts yet.\n\nUse Banana Chat to generate a Revit script, then click \"Save as Script\" on the response. " +
                    "It will appear here and runs with zero tokens.");
                return Result.Cancelled;
            }

            // Show WPF picker
            var picker = new SavedScriptPickerWindow(scripts);
            var ok     = picker.ShowDialog();
            if (ok != true || picker.SelectedScriptId == null)
                return Result.Cancelled;

            // Fetch full script (with code)
            JObject script;
            try
            {
                script = RevitMCPBridge2026.SavedScriptsMethods.FetchScript(apiKey, picker.SelectedScriptId);
                if (script == null) { TaskDialog.Show("Saved Scripts", "Script not found."); return Result.Failed; }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Saved Scripts", $"Could not load script:\n{ex.Message}");
                return Result.Failed;
            }

            // Execute via Roslyn — zero tokens
            var execParams = new JObject
            {
                ["code"]   = script["code"]?.ToString() ?? "",
                ["usings"] = script["usings"] ?? new JArray()
            };
            var resultJson = RevitMCPBridge2026.ScriptMethods.ExecuteRevitScript(uiApp, execParams);
            var result     = JObject.Parse(resultJson);

            // Log run async
            RevitMCPBridge2026.SavedScriptsMethods.LogRunFireAndForget(apiKey, picker.SelectedScriptId);

            // Show result
            var success = result["success"]?.Value<bool>() ?? false;
            var title   = $"Script: {picker.SelectedScriptName}";
            if (success)
            {
                var output = result["result"]?.ToString();
                TaskDialog.Show(title, string.IsNullOrEmpty(output) ? "Completed successfully." : $"Completed.\n\n{output}");
            }
            else
            {
                var err  = result["error"]?.ToString() ?? "Unknown error";
                var diag = result["diagnostics"] is JArray d && d.Count > 0 ? "\n\n" + string.Join("\n", d) : "";
                TaskDialog.Show(title, $"Error: {err}{diag}");
            }

            return Result.Succeeded;
        }
    }

    // ── WPF picker dialog ────────────────────────────────────────────────────────
    internal class SavedScriptPickerWindow : Window
    {
        public string SelectedScriptId   { get; private set; }
        public string SelectedScriptName { get; private set; }

        private readonly ListBox _list;

        public SavedScriptPickerWindow(JArray scripts)
        {
            Title  = "Run Saved Script";
            Width  = 480;
            Height = 380;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = System.Windows.Media.Brushes.White;
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

            var root = new System.Windows.Controls.Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var header = new TextBlock
            {
                Text       = "Select a script to run",
                FontSize   = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.Black,
                Margin     = new Thickness(16, 14, 16, 8),
            };
            System.Windows.Controls.Grid.SetRow(header, 0);
            root.Children.Add(header);

            // List
            _list = new ListBox { Margin = new Thickness(12, 0, 12, 0), BorderThickness = new Thickness(1) };
            _list.MouseDoubleClick += (s, e) => TryConfirm();
            foreach (JObject item in scripts)
            {
                var name = item["name"]?.ToString() ?? "(unnamed)";
                var desc = item["description"]?.ToString();
                var runs = item["run_count"]?.Value<int>() ?? 0;

                var panel = new StackPanel { Margin = new Thickness(4, 4, 4, 4) };
                panel.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, FontSize = 12 });
                if (!string.IsNullOrEmpty(desc))
                    panel.Children.Add(new TextBlock { Text = desc, FontSize = 11, Foreground = System.Windows.Media.Brushes.Gray });
                panel.Children.Add(new TextBlock { Text = $"{runs} run{(runs != 1 ? "s" : "")}", FontSize = 10, Foreground = System.Windows.Media.Brushes.LightGray });

                var li = new ListBoxItem { Content = panel, Tag = item };
                _list.Items.Add(li);
            }
            if (_list.Items.Count > 0) _list.SelectedIndex = 0;
            System.Windows.Controls.Grid.SetRow(_list, 1);
            root.Children.Add(_list);

            // Buttons
            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(12, 10, 12, 14),
            };
            var runBtn = new Button { Content = "Run", Width = 80, Height = 28, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            runBtn.Click += (s, e) => TryConfirm();
            var cancelBtn = new Button { Content = "Cancel", Width = 72, Height = 28, IsCancel = true };
            cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
            btnRow.Children.Add(runBtn);
            btnRow.Children.Add(cancelBtn);
            System.Windows.Controls.Grid.SetRow(btnRow, 2);
            root.Children.Add(btnRow);

            Content = root;
        }

        private void TryConfirm()
        {
            if (_list.SelectedItem is ListBoxItem li && li.Tag is JObject item)
            {
                SelectedScriptId   = item["id"]?.ToString();
                SelectedScriptName = item["name"]?.ToString() ?? "Script";
                DialogResult = true;
                Close();
            }
        }
    }
}
