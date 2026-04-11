/**
 * NcsSheetPacker.cs
 * C# port of sheetPacker.js — bins classified views into concrete sheets
 * and produces the structured prompt block that Banana Chat sends to Claude.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RevitMCPBridge.NcsClassifier
{
    public static class NcsSheetPacker
    {
        // ARCH D printable area (sq in): 32 × 20 = 640
        private const double PrintableWidth  = 32.0;
        private const double PrintableHeight = 20.0;
        private const double PrintableArea   = PrintableWidth * PrintableHeight; // 640 sq in
        private const double FillTarget      = 0.75;

        // Conservative area estimates (sq in) for views without crop box data
        private static readonly Dictionary<string, double> FallbackAreaEstimates = new Dictionary<string, double>
        {
            { "A0", 250 }, { "A1", 180 }, { "A2", 120 }, { "A3", 100 },
            { "A4", 80  }, { "A5", 30  }, { "A6", 60  }, { "A7", 40  },
            { "G0", 200 }, { "G1", 80  }, { "S1", 180 }, { "S5", 30  }
        };

        // NCS discipline/type processing order (mirrors sheetGrammar.js order field)
        private static readonly (string Discipline, int[] Types, bool Required)[] NcsSlots =
        {
            ("G", new[]{0,1,2}, true),
            ("A", new[]{0,1,2,3,4,5,6,7}, true),
            ("S", new[]{0,1,5}, false),
            ("C", new[]{1,5}, false),
            ("L", new[]{1}, false),
            ("M", new[]{1,3,6}, false),
            ("P", new[]{1,3,6}, false),
            ("E", new[]{1,3,6}, false),
        };

        // Which slots are required (must appear even if empty)
        private static readonly HashSet<string> RequiredSlots = new HashSet<string>
        {
            "G0","G1","A0","A1","A6"
        };

        // Human-readable labels for gaps
        private static readonly Dictionary<string, string> SlotLabels = new Dictionary<string, string>
        {
            {"G0","Cover / Sheet Index / Project Data"},
            {"G1","General Notes / Energy Summary"},
            {"A0","Site Plan / Code Analysis / Life Safety"},
            {"A1","Floor Plans / RCPs / Roof Plan"},
            {"A2","Exterior Elevations"},
            {"A3","Building Sections / Wall Sections"},
            {"A4","Large-Scale Plans / Enlarged Views"},
            {"A5","Architectural Details"},
            {"A6","Architectural Schedules"},
            {"A7","Interior Elevations"},
            {"S0","Structural General Notes"},
            {"S1","Structural Plans"},
            {"S5","Structural Details"},
        };

        // ─── PUBLIC API ───────────────────────────────────────────────────

        public static NcsPackedPlan PackInventory(NcsClassifiedInventory inventory)
        {
            var sheets = new List<NcsPackedSheet>();
            var slotMap = inventory.SlotMap;

            foreach (var (discipline, typeNums, _) in NcsSlots)
            {
                foreach (var typeNum in typeNums)
                {
                    var slotKey = $"{discipline}{typeNum}";
                    var views   = slotMap.ContainsKey(slotKey) ? slotMap[slotKey] : new List<NcsClassifiedView>();

                    if (views.Count == 0)
                    {
                        if (RequiredSlots.Contains(slotKey))
                        {
                            var label = SlotLabels.ContainsKey(slotKey) ? SlotLabels[slotKey] : slotKey;
                            sheets.Add(new NcsPackedSheet
                            {
                                SheetId = $"{discipline}{typeNum}.1",
                                Discipline = discipline, SheetType = typeNum,
                                SequenceNum = 1, SlotKey = slotKey,
                                IsGap = true, GapLabel = label,
                                FillPercent = 0, NeedsMoreContent = true
                            });
                        }
                        continue;
                    }

                    // Floor plans get special per-level treatment
                    if (discipline == "A" && typeNum == 1)
                    {
                        sheets.AddRange(PackFloorPlans(views));
                        continue;
                    }

                    sheets.AddRange(PackSlotIntoSheets(discipline, typeNum, views));
                }
            }

            // Required gaps not yet represented
            var gaps = FindRequiredSheetGaps(slotMap);

            var nonGapSheets = sheets.Where(s => !s.IsGap).ToList();
            var underFill = nonGapSheets.Where(s => s.NeedsMoreContent && s.FillPercent > 0).ToList();
            var avgFill = nonGapSheets.Any(s => s.FillPercent > 0)
                ? (int)Math.Round(nonGapSheets.Where(s => s.FillPercent > 0).Average(s => s.FillPercent))
                : 0;

            return new NcsPackedPlan
            {
                Sheets = sheets,
                Gaps   = gaps,
                AmbiguousViews = inventory.Ambiguous,
                RenovationStatus = inventory.RenovationStatus,
                Stats = new PackedPlanStats
                {
                    TotalSheets       = nonGapSheets.Count,
                    GapSheets         = gaps.Count,
                    SheetsUnderFill   = underFill.Count,
                    AmbiguousViewCount= inventory.Ambiguous.Count,
                    AverageFill       = avgFill
                }
            };
        }

        /// <summary>
        /// Converts PackedPlan into the structured inventory block
        /// that goes directly into the Claude system prompt / user message.
        /// </summary>
        public static string BuildPromptInventoryBlock(NcsPackedPlan plan, NcsClassifiedInventory inventory)
        {
            var sb = new StringBuilder();

            sb.AppendLine("=== PRE-ASSIGNED SHEET LAYOUT (NCS/UDS Standard) ===");
            sb.AppendLine("The following assignments are DEFINITE — do not move these views:");
            sb.AppendLine();

            foreach (var sheet in plan.Sheets)
            {
                if (sheet.IsGap || sheet.Viewports.Count == 0) continue;
                var fillStr = sheet.NeedsMoreContent
                    ? $"⚠ {sheet.FillPercent}% fill — NEEDS MORE CONTENT"
                    : $"✓ {sheet.FillPercent}% fill";
                sb.AppendLine($"[{sheet.SheetId}] {fillStr}");
                foreach (var vp in sheet.Viewports)
                {
                    var cond = vp.RenovationCondition != null ? $" [{vp.RenovationCondition.ToUpper()}]" : "";
                    var lvl  = vp.Level != null ? $" (Level {vp.Level})" : "";
                    sb.AppendLine($"  - {vp.Name}{cond}{lvl}  (id: {vp.Id})");
                }
                sb.AppendLine();
            }

            if (plan.Gaps.Any())
            {
                sb.AppendLine("=== REQUIRED SHEETS WITH NO ASSIGNED VIEWS (GAPS) ===");
                sb.AppendLine("You MUST populate these sheets:");
                foreach (var gap in plan.Gaps)
                {
                    sb.AppendLine($"  {gap.SheetId}: {gap.Label}");
                    if (!string.IsNullOrEmpty(gap.GapNotes))
                        sb.AppendLine($"    → {gap.GapNotes}");
                }
                sb.AppendLine();
            }

            var underFill = plan.Sheets.Where(s => !s.IsGap && s.NeedsMoreContent && s.Viewports.Count > 0).ToList();
            if (underFill.Any())
            {
                sb.AppendLine("=== SHEETS UNDER 75% FILL — ADD VIEWS FROM AMBIGUOUS LIST ===");
                foreach (var s in underFill)
                    sb.AppendLine($"  {s.SheetId}: currently {s.FillPercent}% — needs ~{75 - s.FillPercent}% more");
                sb.AppendLine();
            }

            if (inventory.Ambiguous.Any())
            {
                sb.AppendLine("=== UNCLASSIFIED VIEWS — YOU MUST ASSIGN THESE ===");
                sb.AppendLine("Place each in the most appropriate sheet from the plan above:");
                foreach (var v in inventory.Ambiguous)
                    sb.AppendLine($"  - \"{v.Name}\"  viewType: {v.ViewType}  (id: {v.Id})  reason: {v.Reason}");
                sb.AppendLine();
            }

            if (inventory.RenovationStatus.IsRenovation && !inventory.RenovationStatus.IsValid)
            {
                sb.AppendLine("=== RENOVATION TRIO VIOLATIONS ===");
                sb.AppendLine("CRITICAL: Every floor level must have existing + demo + new plans:");
                foreach (var issue in inventory.RenovationStatus.Issues)
                    sb.AppendLine($"  Level {issue.Level}: has [{string.Join(", ", issue.Has)}], MISSING [{string.Join(", ", issue.Missing)}]");
                sb.AppendLine();
            }

            if (inventory.Blocked.Any())
            {
                sb.AppendLine($"=== BLOCKED VIEWS ({inventory.Blocked.Count} stripped) ===");
                sb.AppendLine("These views were removed and must NOT appear in the plan:");
                foreach (var v in inventory.Blocked)
                    sb.AppendLine($"  - \"{v.Name}\"  reason: {v.Reason}");
                sb.AppendLine();
            }

            if (inventory.PermitWarnings.Any())
            {
                sb.AppendLine("=== PERMIT-CRITICAL WARNINGS ===");
                sb.AppendLine("The following are common plan review rejection causes:");
                foreach (var w in inventory.PermitWarnings)
                    sb.AppendLine($"  ⚠ [{w.Id}] {w.Description}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        // ─── PACKING INTERNALS ────────────────────────────────────────────

        private static List<NcsPackedSheet> PackFloorPlans(List<NcsClassifiedView> views)
        {
            var byLevel = new Dictionary<string, List<NcsClassifiedView>>();
            foreach (var v in views)
            {
                var lvl = v.Level ?? "unknown";
                if (!byLevel.ContainsKey(lvl)) byLevel[lvl] = new List<NcsClassifiedView>();
                byLevel[lvl].Add(v);
            }

            var levelOrder = new[] { "B", "1", "2", "3", "4", "M", "R", "unknown" };
            var sorted = byLevel.Keys.OrderBy(k =>
            {
                var idx = Array.IndexOf(levelOrder, k);
                return idx < 0 ? 99 : idx;
            }).ToList();

            var sheets = new List<NcsPackedSheet>();
            int seq = 1;
            foreach (var lvl in sorted)
            {
                var lvlViews = byLevel[lvl].OrderBy(v => RenoOrder(v.RenovationCondition)).ThenBy(v => v.Name).ToList();
                var usedArea = lvlViews.Sum(v => GetViewArea(v, "A1"));
                sheets.Add(new NcsPackedSheet
                {
                    SheetId    = $"A1.{seq}",
                    Discipline = "A", SheetType = 1,
                    SequenceNum = seq, SlotKey = "A1",
                    Level = lvl,
                    Viewports = lvlViews,
                    UsedArea  = usedArea,
                    FillRatio = usedArea / PrintableArea,
                    FillPercent = (int)Math.Round(usedArea / PrintableArea * 100),
                    NeedsMoreContent = (usedArea / PrintableArea) < FillTarget,
                    RenovationConditions = lvlViews.Select(v => v.RenovationCondition)
                        .Where(c => c != null).Distinct().ToList()
                });
                seq++;
            }
            return sheets;
        }

        private static List<NcsPackedSheet> PackSlotIntoSheets(string discipline, int sheetType, List<NcsClassifiedView> views)
        {
            var slotKey = $"{discipline}{sheetType}";
            var sorted  = views.OrderBy(v => RenoOrder(v.RenovationCondition)).ThenBy(v => v.Name).ToList();

            var sheets = new List<NcsPackedSheet>();
            var current = new List<NcsClassifiedView>();
            double usedArea = 0;

            foreach (var v in sorted)
            {
                var area = GetViewArea(v, slotKey);
                if (current.Count > 0 && usedArea + area > PrintableArea * 1.05)
                {
                    sheets.Add(FinalizeSheet(discipline, sheetType, slotKey, sheets.Count + 1, current, usedArea));
                    current = new List<NcsClassifiedView>();
                    usedArea = 0;
                }
                current.Add(v);
                usedArea += area;
            }
            if (current.Count > 0)
                sheets.Add(FinalizeSheet(discipline, sheetType, slotKey, sheets.Count + 1, current, usedArea));

            return sheets;
        }

        private static NcsPackedSheet FinalizeSheet(string disc, int type, string slotKey,
            int seq, List<NcsClassifiedView> viewports, double usedArea)
        {
            return new NcsPackedSheet
            {
                SheetId    = $"{disc}{type}.{seq}",
                Discipline = disc, SheetType = type,
                SequenceNum = seq, SlotKey = slotKey,
                Viewports  = viewports,
                UsedArea   = usedArea,
                FillRatio  = usedArea / PrintableArea,
                FillPercent = (int)Math.Round(usedArea / PrintableArea * 100),
                NeedsMoreContent = (usedArea / PrintableArea) < FillTarget,
                IsOverfull = (usedArea / PrintableArea) > 1.0
            };
        }

        private static List<GapInfo> FindRequiredSheetGaps(Dictionary<string, List<NcsClassifiedView>> slotMap)
        {
            var gaps = new List<GapInfo>();
            foreach (var req in RequiredSlots)
            {
                if (!slotMap.ContainsKey(req) || slotMap[req].Count == 0)
                {
                    if (SlotLabels.ContainsKey(req))
                        gaps.Add(new GapInfo { SheetId=$"{req}.1", Label=SlotLabels[req] });
                }
            }
            return gaps;
        }

        private static double GetViewArea(NcsClassifiedView v, string slotKey)
        {
            if (v.CropWidthFt > 0 && v.CropHeightFt > 0 && v.Scale > 0)
            {
                // paper inches = (crop feet) * (12 in/ft / scale denom) — same as estimateViewportArea in JS
                double ratio = 12.0 / v.Scale;
                double pw = v.CropWidthFt  * ratio;
                double ph = v.CropHeightFt * ratio;
                return pw * ph;
            }
            var baseKey = slotKey?.Length >= 2 ? slotKey.Substring(0, 2) : slotKey;
            return FallbackAreaEstimates.ContainsKey(baseKey) ? FallbackAreaEstimates[baseKey] : 50;
        }

        private static int RenoOrder(string cond)
        {
            switch (cond)
            {
                case "existing": return 0;
                case "demo":     return 1;
                case "new":      return 2;
                default:         return 3;
            }
        }
    }
}
