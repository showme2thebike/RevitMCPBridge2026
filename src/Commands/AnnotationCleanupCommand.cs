using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using WpfColor = System.Windows.Media.Color;
using WpfGrid  = System.Windows.Controls.Grid;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AnnotationCleanupCommand : IExternalCommand
    {
        private static readonly string LogPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".bimops", "fix_overlaps_debug.txt");

        private static void Log(string msg)
        {
            try { System.IO.File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff}  {msg}\r\n"); } catch { }
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Log("=== Execute START ===");
            try
            {
                var uiDoc = commandData.Application.ActiveUIDocument;
                Log($"uiDoc={(uiDoc == null ? "NULL" : "ok")}");
                if (uiDoc == null)
                {
                    TaskDialog.Show("Annotation Cleanup", "No active document.");
                    return Result.Cancelled;
                }

                var doc  = uiDoc.Document;
                var view = uiDoc.ActiveView;
                Log($"view={(view == null ? "NULL" : $"{view.ViewType} '{view.Name}'")}");

                if (view == null || view.ViewType == ViewType.Schedule || view.ViewType == ViewType.DrawingSheet)
                {
                    TaskDialog.Show("Annotation Cleanup",
                        "Open a floor plan, section, elevation, or detail view first.");
                    return Result.Cancelled;
                }

                double scale = view.Scale;
                Log($"scale={scale}");

                var textNotes = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(TextNote))
                    .Cast<TextNote>()
                    .ToList();
                Log($"textNotes={textNotes.Count}");

                var tags = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(IndependentTag))
                    .Cast<IndependentTag>()
                    .ToList();
                Log($"tags={tags.Count}");

                var annotations = new List<AnnBounds>();
                foreach (var tn  in textNotes) TryAdd(annotations, BuildTextNoteBounds(tn,  view, scale));
                foreach (var tag in tags)      TryAdd(annotations, BuildTagBounds(tag,       view, scale));

                int totalElements = textNotes.Count + tags.Count;
                int skipped       = totalElements - annotations.Count;
                Log($"annotations={annotations.Count}  skipped={skipped}");

                if (annotations.Count < 2)
                {
                    string skipNote = skipped > 0
                        ? $"\n\n{skipped} element(s) skipped — Revit did not return measurable bounds (common for some tag families). Run the command while the view is fully open and regenerated."
                        : "";
                    Log($"Showing 'too few annotations' dialog");
                    TaskDialog.Show("Annotation Cleanup",
                        $"Only {annotations.Count} measurable annotation(s) found in '{view.Name}' — nothing to compare.{skipNote}");
                    return Result.Succeeded;
                }

                var overlaps = FindOverlaps(annotations);
                Log($"overlaps={overlaps.Count}");

                if (overlaps.Count == 0)
                {
                    Log("Showing 'no overlaps' dialog");
                    TaskDialog.Show("Annotation Cleanup",
                        $"No overlaps found among {annotations.Count} annotations in '{view.Name}'.");
                    return Result.Succeeded;
                }

                var results = new List<NudgeResult>();
                using (var trans = new Transaction(doc, "BIM Monkey — Fix Annotation Overlaps"))
                {
                    trans.Start();
                    foreach (var pair in overlaps)
                        results.Add(TryNudge(doc, pair));
                    trans.Commit();
                }
                Log($"Nudged {results.Count} pairs. Showing results window.");

                new OverlapResultsWindow(view.Name, annotations.Count, overlaps.Count, skipped, results).ShowDialog();
                Log("Results window closed.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                message = ex.Message;
                return Result.Failed;
            }
        }

        // ── Bounds builders ──────────────────────────────────────────────────────

        private AnnBounds BuildTextNoteBounds(TextNote tn, View view, double scale)
        {
            try
            {
                var bb = tn.get_BoundingBox(view);
                if (bb == null || bb.Min.IsAlmostEqualTo(bb.Max)) return null; // skip — no reliable bounds
                double hw = (bb.Max.X - bb.Min.X) / 2;
                double hh = (bb.Max.Y - bb.Min.Y) / 2;
                if (hw < 0.001 || hh < 0.001) return null; // degenerate box
                return new AnnBounds(tn.Id, $"Text: \"{Clip(tn.Text, 28)}\"",
                    (bb.Min.X + bb.Max.X) / 2, (bb.Min.Y + bb.Max.Y) / 2,
                    hw, hh, !tn.Pinned);
            }
            catch { return null; }
        }

        private AnnBounds BuildTagBounds(IndependentTag tag, View view, double scale)
        {
            try
            {
                var bb = tag.get_BoundingBox(view);
                if (bb == null || bb.Min.IsAlmostEqualTo(bb.Max)) return null; // skip — no reliable bounds
                double hw = (bb.Max.X - bb.Min.X) / 2;
                double hh = (bb.Max.Y - bb.Min.Y) / 2;
                if (hw < 0.001 || hh < 0.001) return null; // degenerate box
                return new AnnBounds(tag.Id, $"Tag: {tag.TagText ?? "(no text)"}",
                    (bb.Min.X + bb.Max.X) / 2, (bb.Min.Y + bb.Max.Y) / 2,
                    hw, hh, !tag.Pinned);
            }
            catch { return null; }
        }

        // ── Overlap detection ────────────────────────────────────────────────────

        // Two elements must overlap by at least this fraction of the smaller element's
        // dimension in BOTH axes to count as a real overlap. Filters out floating-point
        // adjacency and tiny corner touches that aren't visually meaningful.
        private const double OverlapThreshold = 0.10;

        private List<(AnnBounds A, AnnBounds B)> FindOverlaps(List<AnnBounds> ann)
        {
            var result = new List<(AnnBounds, AnnBounds)>();
            for (int i = 0; i < ann.Count; i++)
            {
                for (int j = i + 1; j < ann.Count; j++)
                {
                    var a = ann[i]; var b = ann[j];
                    double overlapX = Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX);
                    double overlapY = Math.Min(a.MaxY, b.MaxY) - Math.Max(a.MinY, b.MinY);
                    if (overlapX <= 0 || overlapY <= 0) continue; // no intersection

                    // Require meaningful overlap — at least 10% of the smaller element in each axis
                    double minW = Math.Min(a.HalfW, b.HalfW) * 2;
                    double minH = Math.Min(a.HalfH, b.HalfH) * 2;
                    if (overlapX >= minW * OverlapThreshold && overlapY >= minH * OverlapThreshold)
                        result.Add((a, b));
                }
            }
            return result;
        }

        // ── Nudge ────────────────────────────────────────────────────────────────

        private NudgeResult TryNudge(Document doc, (AnnBounds A, AnnBounds B) pair)
        {
            var (a, b) = pair;
            // Move the higher-ID element (newer placement) away from the lower-ID (older)
            var mover  = a.Id.Value > b.Id.Value ? a : b;
            var anchor = a.Id.Value > b.Id.Value ? b : a;

            if (!mover.CanMove)
                return NudgeResult.Fail(mover.Label, anchor.Label, "Pinned — cannot move");

            try
            {
                var el = doc.GetElement(mover.Id);
                if (el == null)
                    return NudgeResult.Fail(mover.Label, anchor.Label, "Element not found");

                double dx   = mover.Cx - anchor.Cx;
                double dy   = mover.Cy - anchor.Cy;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                const double buffer = 0.15;  // 0.15 ft clearance after nudge
                XYZ nudge;

                if (dist < 0.001)
                {
                    // Exactly stacked — move up by combined half-heights
                    nudge = new XYZ(0, mover.HalfH + anchor.HalfH + buffer, 0);
                }
                else
                {
                    double nx = dx / dist, ny = dy / dist;
                    double overlapX = Math.Min(mover.MaxX, anchor.MaxX) - Math.Max(mover.MinX, anchor.MinX);
                    double overlapY = Math.Min(mover.MaxY, anchor.MaxY) - Math.Max(mover.MinY, anchor.MinY);
                    double moveAmt  = Math.Max(overlapX * Math.Abs(nx), overlapY * Math.Abs(ny)) + buffer;
                    nudge = new XYZ(nx * moveAmt, ny * moveAmt, 0);
                }

                ElementTransformUtils.MoveElement(doc, mover.Id, nudge);
                // Update center so later pair checks reflect the new position
                mover.Cx += nudge.X;
                mover.Cy += nudge.Y;

                return NudgeResult.Ok(mover.Label, anchor.Label,
                    $"moved {nudge.X:F2}', {nudge.Y:F2}'");
            }
            catch (Exception ex)
            {
                return NudgeResult.Fail(mover.Label, anchor.Label, ex.Message);
            }
        }

        private void TryAdd(List<AnnBounds> list, AnnBounds item) { if (item != null) list.Add(item); }

        private string Clip(string s, int max) =>
            string.IsNullOrEmpty(s) ? "" : s.Length > max ? s.Substring(0, max) + "…" : s;

        // ── Internal data types ────────────────────────────────────────────────

        internal class AnnBounds
        {
            public ElementId Id;
            public string Label;
            public bool CanMove;
            public double Cx, Cy, HalfW, HalfH;

            public double MinX => Cx - HalfW;
            public double MaxX => Cx + HalfW;
            public double MinY => Cy - HalfH;
            public double MaxY => Cy + HalfH;

            public AnnBounds(ElementId id, string label, double cx, double cy,
                double hw, double hh, bool canMove)
            {
                Id = id; Label = label; Cx = cx; Cy = cy;
                HalfW = hw; HalfH = hh; CanMove = canMove;
            }
        }

        internal class NudgeResult
        {
            public bool   Success;
            public string Mover, Anchor, Detail;

            public static NudgeResult Ok(string mover, string anchor, string detail) =>
                new NudgeResult { Success = true,  Mover = mover, Anchor = anchor, Detail = detail };
            public static NudgeResult Fail(string mover, string anchor, string reason) =>
                new NudgeResult { Success = false, Mover = mover, Anchor = anchor, Detail = reason };
        }
    }

    // ── Results Window ────────────────────────────────────────────────────────

    internal class OverlapResultsWindow : Window
    {
        public OverlapResultsWindow(
            string viewName, int totalAnn, int totalOverlaps, int skipped,
            List<AnnotationCleanupCommand.NudgeResult> results)
        {
            Title  = "BIM Monkey — Annotation Cleanup";
            Width  = 500;
            Height = 480;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode  = ResizeMode.CanResize;
            Background  = new SolidColorBrush(WpfColor.FromRgb(30, 30, 30));
            Foreground  = Brushes.White;
            FontFamily  = new System.Windows.Media.FontFamily("Segoe UI");

            int fixedCount  = results.Count(r => r.Success);
            int failedCount = results.Count - fixedCount;

            var root = new WpfGrid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var header = new StackPanel { Margin = new Thickness(16, 14, 16, 10) };
            header.Children.Add(new TextBlock
            {
                Text = $"View: {viewName}",
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(WpfColor.FromRgb(210, 210, 210))
            });
            header.Children.Add(new TextBlock
            {
                Text = $"{totalAnn} annotations scanned · {totalOverlaps} overlapping pairs",
                FontSize = 11.5, Foreground = new SolidColorBrush(WpfColor.FromRgb(140, 140, 140)),
                Margin = new Thickness(0, 2, 0, 8)
            });
            var chips = new StackPanel { Orientation = Orientation.Horizontal };
            chips.Children.Add(Chip($"✓ Fixed: {fixedCount}", WpfColor.FromRgb(28, 72, 38), WpfColor.FromRgb(80, 200, 100)));
            if (failedCount > 0)
                chips.Children.Add(Chip($"✗ Could not fix: {failedCount}", WpfColor.FromRgb(80, 32, 26), WpfColor.FromRgb(220, 90, 70)));
            header.Children.Add(chips);
            WpfGrid.SetRow(header, 0);
            root.Children.Add(header);

            // Scrollable results list
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(10, 0, 10, 0)
            };
            var list = new StackPanel();

            if (fixedCount > 0)
            {
                list.Children.Add(SectionLabel("Fixed"));
                foreach (var r in results.Where(r => r.Success))
                    list.Children.Add(Row(r, true));
            }
            if (failedCount > 0)
            {
                list.Children.Add(SectionLabel("Could Not Fix"));
                foreach (var r in results.Where(r => !r.Success))
                    list.Children.Add(Row(r, false));
                list.Children.Add(new TextBlock
                {
                    Text = "Review pinned or constrained elements manually, then run again.",
                    FontSize = 10.5, FontStyle = FontStyles.Italic,
                    Foreground = new SolidColorBrush(WpfColor.FromRgb(130, 130, 130)),
                    Margin = new Thickness(10, 4, 10, 6), TextWrapping = TextWrapping.Wrap
                });
            }

            scroll.Content = list;
            WpfGrid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            // Footer
            var footer = new Border
            {
                BorderBrush = new SolidColorBrush(WpfColor.FromRgb(55, 55, 55)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(14, 10, 14, 10)
            };
            var footerGrid = new WpfGrid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var undoNote = new TextBlock
            {
                Text = "All moves are one undo step — Ctrl+Z reverts everything.",
                FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(WpfColor.FromRgb(130, 130, 130))
            };
            WpfGrid.SetColumn(undoNote, 0);
            footerGrid.Children.Add(undoNote);

            var closeBtn = new Button
            {
                Content = "Close", Width = 72, Height = 28, FontSize = 11.5,
                Background = new SolidColorBrush(WpfColor.FromRgb(55, 55, 55)),
                Foreground = Brushes.White, BorderThickness = new Thickness(0)
            };
            closeBtn.Click += (s, e) => Close();
            WpfGrid.SetColumn(closeBtn, 1);
            footerGrid.Children.Add(closeBtn);

            footer.Child = footerGrid;
            WpfGrid.SetRow(footer, 2);
            root.Children.Add(footer);

            Content = root;
        }

        private Border Chip(string text, WpfColor bg, WpfColor fg) => new Border
        {
            Background = new SolidColorBrush(bg), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 6, 0),
            Child = new TextBlock
            {
                Text = text, Foreground = new SolidColorBrush(fg),
                FontSize = 11, FontWeight = FontWeights.SemiBold
            }
        };

        private TextBlock SectionLabel(string title) => new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(115, 115, 115)),
            Margin = new Thickness(10, 10, 0, 4)
        };

        private Border Row(AnnotationCleanupCommand.NudgeResult r, bool success)
        {
            var row = new Border
            {
                Background = new SolidColorBrush(success
                    ? WpfColor.FromRgb(28, 44, 32) : WpfColor.FromRgb(48, 28, 26)),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(8, 2, 8, 2),
                Padding = new Thickness(10, 7, 10, 7)
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = r.Mover, FontSize = 11.5, Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap
            });
            string prefix = success ? $"↔ {r.Anchor}  ·  " : "Reason: ";
            stack.Children.Add(new TextBlock
            {
                Text = prefix + r.Detail, FontSize = 10.5, TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(WpfColor.FromRgb(155, 155, 155)),
                Margin = new Thickness(0, 2, 0, 0)
            });
            row.Child = stack;
            return row;
        }
    }
}
