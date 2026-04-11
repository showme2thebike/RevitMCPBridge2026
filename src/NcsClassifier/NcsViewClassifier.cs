/**
 * NcsViewClassifier.cs
 * C# port of viewClassifier.js — deterministic NCS slot assignment.
 * Mirrors the 13-step decision tree exactly so Banana Chat gets the same
 * pre-classification that the Railway daemon pipeline produces.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RevitMCPBridge.NcsClassifier
{
    public static class NcsViewClassifier
    {
        // ─── BLOCKLIST ────────────────────────────────────────────────────
        private static readonly string[] InternalViewBlocklist = {
            "bim monkey", "lineweight", "cdc component", "working",
            "coordination", "do not plot", "dnp", "_archive", "archiv",
            "clash", "navisworks", "temp ", "_temp", "test ", "_test",
            "placeholder", "30x40"
        };

        private static readonly Regex CopyViewPattern =
            new Regex(@"\bcopy\s*\d*\b|\bcopy\s*of\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // ─── RENOVATION CONDITION ─────────────────────────────────────────
        private static readonly Regex[] ExistingPatterns = {
            new Regex(@"\bexist(ing)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b_e\b",           RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\s+e$",            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"-e$",              RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bexg\b",          RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bex\b",           RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };
        private static readonly Regex[] DemoPatterns = {
            new Regex(@"\bdemo(lition)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b_d\b",            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\s+d$",             RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"-d$",               RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bdemol\b",         RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };
        private static readonly Regex[] NewPatterns = {
            new Regex(@"\bnew\b",      RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b_n\b",       RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\s+n$",        RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"-n$",          RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\bproposed\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };

        // ─── PUBLIC API ───────────────────────────────────────────────────

        public static NcsClassifiedView ClassifyView(NcsViewInfo view)
        {
            var name      = (view.Name     ?? "").Trim();
            var nameLower = name.ToLower();
            var vt        = (view.ViewType ?? "").ToLower();

            // ── 1. BLOCKED ────────────────────────────────────────────────
            if (CopyViewPattern.IsMatch(name))
                return Blocked(view, "copy-view");
            if (InternalViewBlocklist.Any(b => nameLower.Contains(b)))
                return Blocked(view, "internal-blocklist");
            if (view.IsInternal)
                return Blocked(view, "internal-flag");

            // ── 2. DERIVED FIELDS ─────────────────────────────────────────
            var level     = DetectLevel(name);
            var reno      = DetectRenovationCondition(name);

            // ── 2b. VIEW TEMPLATE (highest-confidence signal §10.3) ───────
            var tmpl = ClassifyFromViewTemplate(view.ViewTemplate, level, reno);
            if (tmpl != null && tmpl.SheetDiscipline != null)
            {
                var r = Clone(view, level, tmpl.RenovationCondition ?? reno);
                r.SheetDiscipline = tmpl.SheetDiscipline;
                r.SheetType       = tmpl.SheetType;
                r.PlanSubType     = tmpl.PlanSubType;
                r.Confidence      = tmpl.Confidence;
                r.Reason          = tmpl.Reason;
                return r;
            }
            var enrichedReno = tmpl?.RenovationCondition ?? reno;

            // ── 3. SCHEDULES ──────────────────────────────────────────────
            if (vt == "schedule" || vt == "panelschedule")
            {
                var slot = ClassifySchedule(name);
                var r = Clone(view, level, enrichedReno);
                r.SheetDiscipline = slot.Discipline;
                r.SheetType       = slot.SheetType;
                r.Confidence      = slot.Confidence;
                r.Reason          = slot.Reason;
                return r;
            }

            // ── 4. LEGENDS ────────────────────────────────────────────────
            if (vt == "legend")
            {
                var r = Clone(view, level, enrichedReno);
                r.SheetDiscipline = "G"; r.SheetType = 1; r.Confidence = "definite";
                r.Reason = RX(@"energy|insulation|r.value|thermal", name)
                    ? "energy legend → G1" : "legend → G1";
                return r;
            }

            // ── 5. CEILING PLANS / RCP ────────────────────────────────────
            if (vt == "ceilingplan" || vt == "ceiling plan")
            {
                var r = Clone(view, level, enrichedReno);
                r.SheetDiscipline = "A"; r.SheetType = 1; r.PlanSubType = "rcp";
                r.Confidence = "definite"; r.Reason = "CeilingPlan viewType → A1 (RCP)";
                return r;
            }

            // ── 6. FLOOR PLANS ────────────────────────────────────────────
            if (vt == "floorplan" || vt == "floor plan")
            {
                var planSubType = DetectPlanSubType(vt, name);

                if (view.Scale >= 240)
                    return With(Clone(view, level, enrichedReno), "A", 0, "sitePlan", "definite",
                        $"scale 1\"={view.Scale/12}' → A0 (site plan)");

                if (planSubType == "rcp")
                    return With(Clone(view, level, enrichedReno), "A", 1, "rcp", "definite", "RCP name match → A1 (RCP)");

                if (RX(@"site\s*plan|overall\s*site\s*plan", nameLower))
                    return With(Clone(view, level, enrichedReno), "A", 0, "sitePlan", "definite", "site plan → A0");

                if (RX(@"foundation\s*plan|footing\s*plan", nameLower))
                    return With(Clone(view, level, enrichedReno), "S", 1, "foundation", "definite", "foundation plan → S1");

                if (RX(@"floor\s*framing|roof\s*framing|framing\s*plan|structural\s*plan|rafter\s*plan", nameLower))
                {
                    var sub = RX("roof", nameLower) ? "roofFraming" : "floorFraming";
                    return With(Clone(view, level, enrichedReno), "S", 1, sub, "definite", $"{sub} → S1");
                }

                if (planSubType == "bracedWall")
                    return With(Clone(view, level, enrichedReno), "A", 1, "bracedWall", "definite", "braced wall plan → A1");

                if (planSubType == "egress")
                    return With(Clone(view, level, enrichedReno), "A", 0, "egress", "definite", "egress plan → A0");

                if (RX(@"enlarged|large.?scale|bathroom|bath\s|kitchen|entry\s|laundry|utility\s|closet|mudroom|pantry|powder\s*room", nameLower))
                    return With(Clone(view, level, enrichedReno), "A", 4, "enlargedPlan", "definite", "enlarged plan → A4");

                if (view.Scale > 0 && view.Scale <= 24 && view.Scale >= 12)
                    return With(Clone(view, level, enrichedReno), "A", 4, "enlargedPlan", "probable",
                        $"scale denom {view.Scale} (enlarged range) → A4");

                if (planSubType == "roofPlan")
                    return With(Clone(view, level, enrichedReno), "A", 1, "roofPlan", "definite", "roof plan → A1 (roof)");

                // Standard floor plan
                var fp = Clone(view, level, enrichedReno);
                fp.SheetDiscipline = "A"; fp.SheetType = 1; fp.PlanSubType = "floorPlan";
                fp.Confidence = level != null ? "definite" : "probable";
                fp.Reason = "floor plan → A1";
                return fp;
            }

            // ── 7. ELEVATIONS ─────────────────────────────────────────────
            if (vt == "elevation")
            {
                var isInterior = RX(@"interior\s*elev|kitchen\s*elev|bath(room)?\s*elev|fireplace\s*elev|built.in\s*elev|vanity\s*elev|cabinet\s*elev", nameLower);
                var isExterior = RX(@"\b(north|south|east|west|front|rear|back|side|left|right|exterior)\b.*elev|elev.*(north|south|east|west|front|rear|back|side)", nameLower);

                if (isInterior)
                    return With(Clone(view, level, enrichedReno), "A", 7, null, "definite", "interior elevation → A7");

                var ev = Clone(view, level, enrichedReno);
                ev.SheetDiscipline = "A"; ev.SheetType = 2;
                ev.Confidence = isExterior ? "definite" : "probable";
                ev.Reason = isExterior ? "exterior elevation → A2" : "elevation (unspecified direction) → A2";
                return ev;
            }

            // ── 8. SECTIONS ───────────────────────────────────────────────
            if (vt == "section")
            {
                var sv = Clone(view, level, enrichedReno);
                sv.SheetDiscipline = "A"; sv.SheetType = 3; sv.Confidence = "definite";
                sv.Reason = (view.Scale > 0 && view.Scale <= 16)
                    ? $"section at scale denom {view.Scale} → A3 (wall section)"
                    : "section → A3";
                return sv;
            }

            // ── 9. DETAILS ────────────────────────────────────────────────
            if (vt.Contains("detail") || vt.Contains("drafting"))
            {
                var detailSub = ClassifyDetailRouting(name);

                if (RX(@"structural\s*detail|hold.?down|shear\s*detail|moment\s*frame|beam\s*bearing|anchor\s*bolt|hurricane\s*tie|strap\s*detail|post\s*base", nameLower))
                {
                    var r = Clone(view, level, enrichedReno);
                    r.SheetDiscipline = "S"; r.SheetType = 5; r.DetailSubType = "structural";
                    r.Confidence = "probable"; r.Reason = "structural detail → S5";
                    return r;
                }

                if (view.Scale > 0 && view.Scale >= 48)
                {
                    var r = Clone(view, level, enrichedReno);
                    r.SheetDiscipline = "A"; r.SheetType = 4; r.DetailSubType = detailSub;
                    r.Confidence = "probable"; r.Reason = $"detail at 1/4\" scale (denom {view.Scale}) → probable A4";
                    return r;
                }

                {
                    var r = Clone(view, level, enrichedReno);
                    r.SheetDiscipline = "A"; r.SheetType = 5; r.DetailSubType = detailSub;
                    r.Confidence = "definite"; r.Reason = $"detail ({detailSub}) → A5";
                    return r;
                }
            }

            // ── 10. 3D VIEWS ──────────────────────────────────────────────
            if (vt.Contains("3d") || vt.Contains("camera") || vt.Contains("rendering") || vt.Contains("perspective"))
            {
                return With(Clone(view, level, enrichedReno), "A", 9, null, "probable", "3D view → A9");
            }

            // ── 11. MEP PLANS ─────────────────────────────────────────────
            if (vt.Contains("mechanical") || RX(@"\bhvac\b|ductwork|mech\s*plan", name))
                return With(Clone(view, level, enrichedReno), "M", 1, null, "probable", "mechanical plan → M1");
            if (vt.Contains("plumbing") || RX(@"\bplumb", name))
                return With(Clone(view, level, enrichedReno), "P", 1, null, "probable", "plumbing plan → P1");
            if (vt.Contains("electrical") || RX(@"\belec(trical)?\b|\bpower\s*plan\b|\blighting\s*plan\b", name))
                return With(Clone(view, level, enrichedReno), "E", 1, null, "probable", "electrical plan → E1");

            // ── 12. SCALE-ONLY FALLBACK ───────────────────────────────────
            if (view.Scale >= 240)
                return With(Clone(view, level, enrichedReno), "A", 0, "sitePlan", "probable",
                    $"scale 1\"={view.Scale/12}' → A0 (site plan)");

            // ── 13. UNCLASSIFIED → Claude ─────────────────────────────────
            {
                var r = Clone(view, level, enrichedReno);
                r.Confidence = "ambiguous";
                r.Reason = $"unrecognized viewType=\"{view.ViewType}\", name=\"{name}\"";
                return r;
            }
        }

        public static NcsClassifiedInventory ClassifyInventory(List<NcsViewInfo> views)
        {
            var all      = views.Select(ClassifyView).ToList();
            var blocked  = all.Where(v => v.Confidence == "blocked").ToList();
            var definite = all.Where(v => v.Confidence == "definite").ToList();
            var probable = all.Where(v => v.Confidence == "probable").ToList();
            var ambiguous= all.Where(v => v.Confidence == "ambiguous").ToList();

            var slotMap = new Dictionary<string, List<NcsClassifiedView>>();
            foreach (var v in definite.Concat(probable))
            {
                if (v.SheetDiscipline == null || !v.SheetType.HasValue) continue;
                var key = $"{v.SheetDiscipline}{v.SheetType}";
                if (!slotMap.ContainsKey(key)) slotMap[key] = new List<NcsClassifiedView>();
                slotMap[key].Add(v);
            }

            var revoStatus = CheckRenovationTrios(
                slotMap.ContainsKey("A1") ? slotMap["A1"] : new List<NcsClassifiedView>());
            var permitWarnings = RunPermitChecks(slotMap,
                slotMap.ContainsKey("A1") && slotMap["A1"].Any(v => v.Level == "2" || v.Level == "3" || v.Level == "4"));

            var total = all.Count;
            return new NcsClassifiedInventory
            {
                All = all, Blocked = blocked, Definite = definite,
                Probable = probable, Ambiguous = ambiguous,
                SlotMap = slotMap,
                RenovationStatus = revoStatus,
                PermitWarnings = permitWarnings,
                Stats = new ClassificationStats
                {
                    Total  = total,
                    Blocked = blocked.Count,
                    Definite = definite.Count,
                    Probable = probable.Count,
                    Ambiguous = ambiguous.Count,
                    ClassifiedPercent = (int)Math.Round(
                        (double)(definite.Count + probable.Count) /
                        Math.Max(total - blocked.Count, 1) * 100)
                }
            };
        }

        // ─── DETECTION HELPERS ────────────────────────────────────────────

        internal static string DetectLevel(string name)
        {
            var n = (name ?? "").ToLower();
            if (Regex.IsMatch(n, @"basement|cellar|sub-grade")) return "B";
            if (Regex.IsMatch(n, @"roof\s*plan|roof$"))         return "R";
            if (Regex.IsMatch(n, @"mezzanine|mezz"))            return "M";
            if (Regex.IsMatch(n, @"\b(level|floor|fl\.?)\s*1\b|first\s*floor|1st\s*floor")) return "1";
            if (Regex.IsMatch(n, @"\b(level|floor|fl\.?)\s*2\b|second\s*floor|2nd\s*floor")) return "2";
            if (Regex.IsMatch(n, @"\b(level|floor|fl\.?)\s*3\b|third\s*floor|3rd\s*floor"))  return "3";
            if (Regex.IsMatch(n, @"\b(level|floor|fl\.?)\s*4\b|fourth\s*floor|4th\s*floor")) return "4";
            return null;
        }

        internal static string DetectRenovationCondition(string name)
        {
            if (ExistingPatterns.Any(p => p.IsMatch(name))) return "existing";
            if (DemoPatterns.Any(p => p.IsMatch(name)))     return "demo";
            if (NewPatterns.Any(p => p.IsMatch(name)))      return "new";
            return null;
        }

        internal static string DetectPlanSubType(string viewType, string name)
        {
            var n  = (name ?? "").ToLower();
            var vt = (viewType ?? "").ToLower();
            if (vt == "ceilingplan" || vt == "ceiling plan") return "rcp";
            if (RX(@"reflected\s*ceiling|r\.c\.p\b|\brcp\b|ceiling\s*plan", n)) return "rcp";
            if (RX(@"roof\s*plan", n))                                           return "roofPlan";
            if (RX(@"braced?\s*wall|wall\s*brac|shear\s*wall\s*plan|lateral\s*plan", n)) return "bracedWall";
            if (RX(@"egress\s*plan|life\s*safety|exit\s*plan", n))              return "egress";
            return "floorPlan";
        }

        internal static string ClassifyDetailRouting(string name)
        {
            var n = (name ?? "").ToLower();
            if (RX(@"stair|staircase|guardrail|handrail|railing|baluster|newel|tread|riser|nosing|landing", n)) return "stair";
            if (RX(@"insulation|thermal|air\s*barrier|vapor\s*retarder|r.value|continuous\s*insulation|\bci\b|blower\s*door|energy\s*detail", n)) return "thermalEnvelope";
            if (RX(@"exterior|envelope|flashing|waterproof|eave|parapet|rake|siding|cladding|deck\s*detail|balcony|wrb|weather|sill\s*plate|foundation\s*detail|footing", n)) return "exterior";
            if (RX(@"interior|cabinet|millwork|trim|base\s*detail|casing|wainscot|built.in|casework|countertop", n)) return "interior";
            return "unspecified";
        }

        internal static ScheduleSlot ClassifySchedule(string name)
        {
            var n = (name ?? "").ToLower();

            if (RX(@"sheet\s*index|drawing\s*index", n))
                return new ScheduleSlot { Discipline="G", SheetType=0, Confidence="definite", Reason="sheet index → G0" };
            if (RX(@"energy|insulation\s*schedule|r.value\s*table|thermal\s*schedule", n))
                return new ScheduleSlot { Discipline="G", SheetType=1, Confidence="probable", Reason="energy schedule → G1" };
            if (RX(@"door\s*schedule|door\s*sched", n))
                return new ScheduleSlot { Discipline="A", SheetType=6, Confidence="definite", Reason="door schedule → A6" };
            if (RX(@"window\s*schedule|window\s*sched|fenestration\s*schedule", n))
                return new ScheduleSlot { Discipline="A", SheetType=6, Confidence="definite", Reason="window schedule → A6" };
            if (RX(@"lighting\s*(fixture\s*)?schedule|light\s*fixture\s*schedule|luminaire\s*schedule", n))
                return new ScheduleSlot { Discipline="A", SheetType=6, Confidence="definite", Reason="lighting fixture schedule → A6" };
            if (RX(@"smoke\s*(detector|alarm)|carbon\s*monoxide|co\s*detector|life\s*safety\s*schedule", n))
                return new ScheduleSlot { Discipline="A", SheetType=6, Confidence="definite", Reason="smoke/CO schedule → A6" };

            string[] archSchedulePatterns = {
                "finish schedule","room finish","interior finish","room schedule","area schedule",
                "space schedule","exterior finish schedule","material schedule","siding schedule",
                "hardware schedule","hardware set","lockset schedule","plumbing fixture",
                "appliance schedule","appliance list","casework schedule"
            };
            if (archSchedulePatterns.Any(k => n.Contains(k)))
                return new ScheduleSlot { Discipline="A", SheetType=6, Confidence="definite", Reason="arch schedule → A6" };

            if (RX(@"mechanical\s*equipment\s*schedule|hvac\s*schedule", n))
                return new ScheduleSlot { Discipline="M", SheetType=6, Confidence="probable", Reason="mech equipment schedule → M6" };
            if (RX(@"beam\s*schedule|column\s*schedule|footing\s*schedule|header\s*schedule|post\s*schedule|lintel\s*schedule", n))
                return new ScheduleSlot { Discipline="S", SheetType=0, Confidence="probable", Reason="structural schedule → S0" };
            if (RX(@"\bhvac\b|mechanical\s*schedule|duct\s*schedule", n))
                return new ScheduleSlot { Discipline="M", SheetType=6, Confidence="probable", Reason="mech schedule → M6" };
            if (RX(@"panel\s*schedule|electrical\s*schedule|circuit\s*schedule|load\s*schedule", n))
                return new ScheduleSlot { Discipline="E", SheetType=6, Confidence="probable", Reason="elec schedule → E6" };
            if (RX(@"plumbing\s*fixture|water\s*heater\s*schedule", n))
                return new ScheduleSlot { Discipline="P", SheetType=6, Confidence="probable", Reason="plumbing schedule → P6" };

            return new ScheduleSlot { Discipline="A", SheetType=6, Confidence="ambiguous", Reason="unknown schedule type" };
        }

        private static TemplateClassifyResult ClassifyFromViewTemplate(string templateName, string level, string reno)
        {
            if (string.IsNullOrEmpty(templateName)) return null;
            var t = templateName.ToUpper();

            string enrichedReno = reno;
            if (enrichedReno == null)
            {
                if (t.Contains("EXIST")) enrichedReno = "existing";
                else if (t.Contains("DEMO")) enrichedReno = "demo";
            }

            if (t.Contains("RCP"))
                return new TemplateClassifyResult { SheetDiscipline="A", SheetType=1, PlanSubType="rcp", RenovationCondition=enrichedReno, Confidence="definite", Reason=$"viewTemplate \"{templateName}\" → A1 (RCP)" };
            if (t.Contains("ELEV"))
                return new TemplateClassifyResult { SheetDiscipline="A", SheetType=2, RenovationCondition=enrichedReno, Confidence="definite", Reason=$"viewTemplate \"{templateName}\" → A2" };
            if (t.Contains("WSECT") || (t.Contains("WALL") && t.Contains("SECT")))
                return new TemplateClassifyResult { SheetDiscipline="A", SheetType=3, RenovationCondition=enrichedReno, Confidence="definite", Reason=$"viewTemplate \"{templateName}\" → A3 (wall section)" };
            if (t.Contains("SECT"))
                return new TemplateClassifyResult { SheetDiscipline="A", SheetType=3, RenovationCondition=enrichedReno, Confidence="definite", Reason=$"viewTemplate \"{templateName}\" → A3" };
            if (t.Contains("DETAIL"))
                return new TemplateClassifyResult { SheetDiscipline="A", SheetType=5, RenovationCondition=enrichedReno, Confidence="definite", Reason=$"viewTemplate \"{templateName}\" → A5" };
            if (t.Contains("ENLAR"))
                return new TemplateClassifyResult { SheetDiscipline="A", SheetType=4, PlanSubType="enlargedPlan", RenovationCondition=enrichedReno, Confidence="definite", Reason=$"viewTemplate \"{templateName}\" → A4" };
            if (t.Contains("PLAN") && !t.Contains("SITE") && !t.Contains("STRUCT") && !t.Contains("FOUND"))
                return new TemplateClassifyResult { SheetDiscipline="A", SheetType=1, PlanSubType="floorPlan", RenovationCondition=enrichedReno, Confidence="definite", Reason=$"viewTemplate \"{templateName}\" → A1" };

            // Template found but pattern not recognised — just return reno enrichment
            if (enrichedReno != reno)
                return new TemplateClassifyResult { RenovationCondition=enrichedReno };
            return null;
        }

        // ─── RENOVATION TRIO VALIDATION ───────────────────────────────────

        internal static RenovationStatus CheckRenovationTrios(List<NcsClassifiedView> floorPlanViews)
        {
            if (!floorPlanViews.Any())
                return new RenovationStatus { IsRenovation = false, IsValid = true };

            var hasAny = floorPlanViews.Any(v => v.RenovationCondition != null);
            if (!hasAny)
                return new RenovationStatus { IsRenovation = false, IsValid = true };

            var byLevel = new Dictionary<string, HashSet<string>>();
            foreach (var v in floorPlanViews)
            {
                var lvl = v.Level ?? "unknown";
                if (!byLevel.ContainsKey(lvl)) byLevel[lvl] = new HashSet<string>();
                if (v.RenovationCondition != null) byLevel[lvl].Add(v.RenovationCondition);
            }

            var issues = new List<RenovationLevelIssue>();
            foreach (var kv in byLevel)
            {
                var missing = new List<string>();
                foreach (var cond in new[] { "existing", "demo", "new" })
                    if (!kv.Value.Contains(cond)) missing.Add(cond);
                if (missing.Any())
                    issues.Add(new RenovationLevelIssue { Level=kv.Key, Has=kv.Value.ToList(), Missing=missing });
            }

            return new RenovationStatus { IsRenovation=true, IsValid=issues.Count==0, Issues=issues };
        }

        // ─── PERMIT CRITICAL CHECKS ───────────────────────────────────────

        internal static List<PermitWarning> RunPermitChecks(Dictionary<string, List<NcsClassifiedView>> slotMap, bool isMultiStory = false)
        {
            var warnings = new List<PermitWarning>();
            var slot = new Func<string, List<NcsClassifiedView>>(k =>
                slotMap.ContainsKey(k) ? slotMap[k] : new List<NcsClassifiedView>());

            if (!slot("A0").Any(v => RX("site", v.Name ?? "")))
                warnings.Add(new PermitWarning { Id="site_plan", Description="Site plan (A0.1)" });

            if (!slot("A0").Any())
                warnings.Add(new PermitWarning { Id="code_summary", Description="Code/zoning analysis on A0" });

            if (!slot("G1").Any(v => RX(@"energy|insulation|r.value|u.factor", v.Name ?? "")))
                warnings.Add(new PermitWarning { Id="energy_summary", Description="Energy code R-value/U-factor table (G1)" });

            if (!slot("A6").Any(v => RX("window", v.Name ?? "")))
                warnings.Add(new PermitWarning { Id="window_schedule_energy", Description="Window schedule with U-factor + SHGC columns (A6)" });

            if (!slot("A1").Any(v => v.PlanSubType == "rcp" || RX(@"rcp|reflected ceiling|ceiling plan", v.Name ?? "")))
                warnings.Add(new PermitWarning { Id="rcp_exists", Description="Reflected ceiling plan(s) present (A1)" });

            var allA1S1 = slot("A1").Concat(slot("S1")).ToList();
            if (!allA1S1.Any(v => v.PlanSubType == "bracedWall" || RX(@"brace|bracing|shear wall", v.Name ?? "")))
                warnings.Add(new PermitWarning { Id="braced_wall", Description="Braced wall plan per story (IRC R602.10)" });

            if (!slot("A3").Any(v => RX("wall section", v.Name ?? "")))
                warnings.Add(new PermitWarning { Id="wall_section", Description="Wall section with insulation layers (A3)" });

            if (isMultiStory && !slot("A5").Any(v => RX(@"stair|guardrail|handrail|railing", v.Name ?? "")))
                warnings.Add(new PermitWarning { Id="stair_detail", Description="Stair/guardrail detail for multi-story projects (A5)" });

            var a1a6 = slot("A1").Concat(slot("A6")).ToList();
            if (!a1a6.Any(v => RX(@"smoke|carbon monoxide|detector|alarm", v.Name ?? "")))
                warnings.Add(new PermitWarning { Id="smoke_co_shown", Description="Smoke/CO detector locations shown on RCP or plans" });

            return warnings;
        }

        // ─── FACTORY HELPERS ──────────────────────────────────────────────

        private static NcsClassifiedView Blocked(NcsViewInfo src, string reason) =>
            new NcsClassifiedView
            {
                Id=src.Id, Name=src.Name, ViewType=src.ViewType, Scale=src.Scale,
                ViewTemplate=src.ViewTemplate, CropWidthFt=src.CropWidthFt,
                CropHeightFt=src.CropHeightFt, IsInternal=src.IsInternal,
                Confidence="blocked", Reason=reason
            };

        private static NcsClassifiedView Clone(NcsViewInfo src, string level, string reno) =>
            new NcsClassifiedView
            {
                Id=src.Id, Name=src.Name, ViewType=src.ViewType, Scale=src.Scale,
                ViewTemplate=src.ViewTemplate, CropWidthFt=src.CropWidthFt,
                CropHeightFt=src.CropHeightFt, IsInternal=src.IsInternal,
                Level=level, RenovationCondition=reno
            };

        private static NcsClassifiedView With(NcsClassifiedView v, string disc, int type,
            string planSub, string confidence, string reason)
        {
            v.SheetDiscipline = disc; v.SheetType = type;
            v.PlanSubType = planSub; v.Confidence = confidence; v.Reason = reason;
            return v;
        }

        private static bool RX(string pattern, string input) =>
            Regex.IsMatch(input ?? "", pattern, RegexOptions.IgnoreCase);
    }
}
