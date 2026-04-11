# BimMonkey Backend Best Practices
## Knowledge Reference for Banana Chat AI Agent

---

## 1. Architecture Overview

```
Raw Views (Revit API)
   |
   v
viewClassifier / NcsViewClassifier.cs
  (NCS slot assignment — deterministic 13-step decision tree)
   |
   v
sheetPacker / NcsSheetPacker.cs
  (bin-pack views onto sheets with fill geometry)
   |
   v
BuildPromptInventoryBlock()
  (structured text block injected into Banana Chat context)
   |
   v
Claude (Banana Chat)
  (handles ambiguous views; uses promptBlock for definite assignments)
   |
   v
planValidator gate
  (6 checks with severity — blocks invalid sheet sets before MCP)
   |
   v
MCP Commands
  (createSheet, placeViewOnSheet, placeScheduleOnSheet, etc.)
```

The pipeline is linear and sequential. **Never skip stages or reorder them.**

---

## 2. sheetGrammar

### Sheet ID Format

```
[Discipline][SheetType].[Sequence]
```

| Example | Meaning |
|---------|---------|
| `G0.1`  | General, Cover Sheet, first |
| `A1.2`  | Architectural, Plans, second sheet (Level 2 floor plan) |
| `A5.3`  | Architectural, Details, third sheet |
| `S5.3`  | Structural, Details, third sheet |
| `M6.1`  | Mechanical, Schedules, first sheet |

**Valid format regex:** `^[A-Z][0-9]\.[0-9]+$`

### Discipline Codes

| Code | Discipline |
|------|------------|
| G    | General / Cover |
| A    | Architectural |
| S    | Structural |
| C    | Civil |
| L    | Landscape |
| M    | Mechanical |
| P    | Plumbing |
| E    | Electrical |

### Active NCS Slot Types by Discipline

| Discipline | Active Types |
|------------|-------------|
| G          | 0, 1, 2 |
| A          | 0, 1, 2, 3, 4, 5, 6, 7 |
| S          | 0, 1, 5 |
| C          | 1, 5 |
| L          | 1 |
| M          | 1, 3, 6 |
| P          | 1, 3, 6 |
| E          | 1, 3, 6 |

### Sheet Type Numbers (0-9)

| Number | Type |
|--------|------|
| 0      | General / Cover / Site / Code Analysis |
| 1      | Plans (floor, RCP, structural) |
| 2      | Elevations |
| 3      | Sections / Wall Sections |
| 4      | Enlarged Plans / Large-Scale Views |
| 5      | Details |
| 6      | Schedules / Diagrams |
| 7      | Interior Elevations |
| 8      | Reports / Specifications |
| 9      | 3D / Rendering / Camera Views |

### Required Sheets (must exist in every set)

| Slot | Sheet | Description |
|------|-------|-------------|
| G0   | G0.1  | Cover sheet / project info / sheet index |
| G1   | G1.1  | General notes / energy summary |
| A0   | A0.1  | Site plan / code analysis / life safety |
| A1   | A1.1+ | Floor plan(s) — one per level |
| A6   | A6.1  | Schedules (door, window, finish) |

### planSubType Values (A1 floor plan sub-classification)

| Value | Revit ViewType | Description |
|-------|---------------|-------------|
| `floorPlan` | FloorPlan | Standard floor plan |
| `rcp` | FloorPlan or CeilingPlan | Reflected ceiling plan |
| `roofPlan` | FloorPlan | Roof plan |
| `sitePlan` | FloorPlan | Site / survey plan |
| `egress` | FloorPlan | Egress / life safety plan |
| `enlargedPlan` | FloorPlan, Detail, or DraftingView | Large-scale partial plan (A4 slot) |
| `bracedWall` | FloorPlan | Braced wall / shear wall plan |
| `foundation` | FloorPlan | Foundation plan (routes to S1) |
| `floorFraming` | FloorPlan | Floor framing plan (routes to S1) |
| `roofFraming` | FloorPlan | Roof framing plan (routes to S1) |

### detailSubType Values (A5 detail sub-classification)

| Value | Name keywords | Preferred sequence start |
|-------|--------------|--------------------------|
| `stair` | stair, staircase, guardrail, handrail, railing, baluster, newel, tread, riser, nosing, landing | A5.5+ |
| `thermalEnvelope` | insulation, thermal, air barrier, vapor retarder, r-value, continuous insulation, ci, blower door, energy detail | A5.3+ |
| `exterior` | exterior, envelope, flashing, waterproof, eave, parapet, rake, siding, cladding, deck detail, balcony, wrb, weather, sill plate, foundation detail, footing | A5.1+ |
| `interior` | interior, cabinet, millwork, trim, base detail, casing, wainscot, built-in, casework, countertop | A5.7+ |
| `unspecified` | (none of above) | fill remaining A5 slots |

---

## 3. viewClassifier — 13-Step Decision Tree

Every raw Revit view is evaluated through 13 steps in order. **First match wins.**

### Confidence Levels

| Confidence | Meaning | Action |
|------------|---------|--------|
| `blocked`  | Strip entirely — never place on sheet | Do not route to Claude |
| `definite` | Assign without asking Claude | Trust and use directly |
| `probable` | Assign, flag for review | Use; warn Barrett if suspicious |
| `ambiguous` | Claude must decide | Present in promptBlock ambiguous list |

### Step 1: Block Filter

First check CopyViewPattern (word-boundary match):

```
COPY_VIEW_PATTERN = /\bcopy\s*\d*\b|\bcopy\s*of\b/i
```

Examples matched: "Copy 1", "Copy of Floor Plan", "Copy", "copy 12"
Examples NOT matched: "Photocopy" (no word boundary), "Canopy" (no word boundary)

Then check InternalViewBlocklist (case-insensitive substring match):

```
"bim monkey"    "lineweight"    "cdc component"   "working"
"coordination"  "do not plot"   "dnp"             "_archive"
"archiv"        "clash"         "navisworks"       "temp "
"_temp"         "test "         "_test"            "placeholder"
"30x40"
```

Note: "temp " and "test " have a trailing space to avoid false-positives on words like "temperature" or "testimony". "_archive" and "archiv" both match "archival" etc.

Finally check IsInternal flag (set by Revit API if view is marked internal).

All three checks → `confidence = "blocked"`, added to blocklist.

### Step 2: Derived Fields

Before classification, extract:
- **Level** via `DetectLevel(name)` — see Level Detection section
- **RenovationCondition** via `DetectRenovationCondition(name)` — see Renovation Detection section

### Step 2b: View Template Signal (Highest Confidence)

If the view has an assigned View Template name, use it as the primary signal. Template matching is case-insensitive on the template name (converted to uppercase):

| Template name contains | Slot | Notes |
|------------------------|------|-------|
| `RCP`                  | A1 (planSubType: rcp) | Checked before PLAN |
| `ELEV`                 | A2 | |
| `WSECT` or (WALL + SECT) | A3 (wall section) | Checked before SECT |
| `SECT`                 | A3 | |
| `DETAIL`               | A5 | |
| `ENLAR`                | A4 (planSubType: enlargedPlan) | |
| `PLAN` (not SITE, STRUCT, FOUND) | A1 (planSubType: floorPlan) | |
| `EXIST`                | enriches renovationCondition = "existing" | May not produce slot assignment alone |
| `DEMO`                 | enriches renovationCondition = "demo" | May not produce slot assignment alone |

Template match → `confidence = "definite"`. Skip Steps 3–11.

### Step 3: Schedules

ViewType = `Schedule` or `PanelSchedule` → route via `ClassifySchedule()`.

**Schedule priority routing (in order — first match wins):**

| Name matches | Slot | Confidence | Note |
|-------------|------|------------|------|
| "sheet index", "drawing index" | G0 | definite | |
| "energy", "insulation schedule", "r-value table", "thermal schedule" | G1 | probable | |
| "door schedule", "door sched" | A6 | definite | |
| "window schedule", "window sched", "fenestration schedule" | A6 | definite | |
| "lighting fixture schedule", "light fixture schedule", "luminaire schedule" | A6 | definite | **Lighting before electrical** — lighting fixture schedules go on arch sheets, not E6 |
| "smoke detector", "carbon monoxide", "co detector", "life safety schedule" | A6 | definite | |
| finish schedule, room finish, interior finish, room schedule, area schedule, space schedule, exterior finish schedule, material schedule, siding schedule, hardware schedule, hardware set, lockset schedule, plumbing fixture, appliance schedule, appliance list, casework schedule | A6 | definite | Substring match |
| "mechanical equipment schedule", "hvac schedule" | M6 | probable | |
| "beam schedule", "column schedule", "footing schedule", "header schedule", "post schedule", "lintel schedule" | S0 | probable | |
| "hvac", "mechanical schedule", "duct schedule" | M6 | probable | |
| "panel schedule", "electrical schedule", "circuit schedule", "load schedule" | E6 | probable | |
| "plumbing fixture", "water heater schedule" | P6 | probable | |
| (no match) | A6 | ambiguous | Unknown schedule type |

### Step 4: Legends

ViewType = `Legend` → G1 (general notes/legend), `confidence = "definite"`
Special case: if name contains "energy", "insulation", "r-value", or "thermal" → reason annotated as "energy legend → G1"

### Step 5: Ceiling Plans / RCP

ViewType = `CeilingPlan` → A1 (planSubType: rcp), `confidence = "definite"`

### Step 6: Floor Plans

ViewType = `FloorPlan` — evaluated in this order:

1. Scale >= 240 (1"=20' or smaller) → A0 (planSubType: sitePlan), definite
2. planSubType = "rcp" (name matches RCP pattern) → A1 (rcp), definite
3. Name matches `site plan|overall site plan` → A0 (sitePlan), definite
4. Name matches `foundation plan|footing plan` → S1 (foundation), definite
5. Name matches `floor framing|roof framing|framing plan|structural plan|rafter plan` → S1 (floorFraming or roofFraming), definite
6. planSubType = "bracedWall" → A1 (bracedWall), definite
7. planSubType = "egress" → A0 (egress), definite
8. Name matches enlarged/bathroom/kitchen/etc. → A4 (enlargedPlan), definite
9. Scale denominator in range 12–24 (1/2"–1") → A4 (enlargedPlan), probable
10. planSubType = "roofPlan" → A1 (roofPlan), definite
11. Default floor plan → A1 (floorPlan); confidence = "definite" if level detected, else "probable"

**Enlarged plan name patterns:** `enlarged|large-scale|bathroom|bath |kitchen|entry |laundry|utility |closet|mudroom|pantry|powder room`

### Step 7: Elevations

ViewType = `Elevation`:
- Interior: name matches `interior elev|kitchen elev|bath(room)? elev|fireplace elev|built-in elev|vanity elev|cabinet elev` → A7, definite
- Exterior: name matches compass direction or "exterior" + "elev" → A2, definite
- Default: A2, probable (direction ambiguous)

### Step 8: Sections

ViewType = `Section` → A3, definite
- Scale denominator <= 16 (3/4" or larger): reason = "wall section"
- Default: "section"

### Step 9: Details

ViewType = `Detail` or `DraftingView`:
1. Name matches structural detail patterns (`structural detail|hold-down|shear detail|moment frame|beam bearing|anchor bolt|hurricane tie|strap detail|post base`) → S5, probable
2. Scale >= 48 (1/4" scale or smaller) → A4, probable ("detail at 1/4" scale")
3. Default → A5, definite; apply detailSubType routing

### Step 10: 3D Views

ViewType contains "3d", "camera", "rendering", or "perspective" → A9, probable

### Step 11: MEP Plans

- ViewType contains "mechanical" or name matches `\bhvac\b|ductwork|mech plan` → M1, probable
- ViewType contains "plumbing" or name matches `\bplumb` → P1, probable
- ViewType contains "electrical" or name matches `\belec(trical)?\b|\bpower plan\b|\blighting plan\b` → E1, probable

### Step 12: Scale-Only Fallback

Scale >= 240 → A0 (sitePlan), probable (catches any view type not handled above)

### Step 13: Unclassified → Claude

No match in steps 1-12 → `confidence = "ambiguous"`, reason includes viewType and name

---

## 4. Detection Helper Patterns

### Level Detection (DetectLevel)

Returns one of: `"B"`, `"1"`, `"2"`, `"3"`, `"4"`, `"M"`, `"R"`, or `null`.

| Returns | Matches (case-insensitive) |
|---------|---------------------------|
| `"B"`   | `basement`, `cellar`, `sub-grade` |
| `"R"`   | `roof plan`, `roof` at end of string |
| `"M"`   | `mezzanine`, `mezz` |
| `"1"`   | `level 1`, `floor 1`, `fl 1`, `fl.1`, `first floor`, `1st floor` |
| `"2"`   | `level 2`, `floor 2`, `fl 2`, `fl.2`, `second floor`, `2nd floor` |
| `"3"`   | `level 3`, `floor 3`, `fl 3`, `fl.3`, `third floor`, `3rd floor` |
| `"4"`   | `level 4`, `floor 4`, `fl 4`, `fl.4`, `fourth floor`, `4th floor` |

### Renovation Condition Detection (DetectRenovationCondition)

Returns `"existing"`, `"demo"`, `"new"`, or `null`. Checked in that order; first match wins.

**Existing patterns:**
```
\bexist(ing)?\b    \b_e\b    \s+e$    -e$    \bexg\b    \bex\b
```
Examples: "Level 1 Existing", "First Floor - E", "Floor Plan EX", "Level 1 EXG"

**Demo patterns:**
```
\bdemo(lition)?\b    \b_d\b    \s+d$    -d$    \bdemol\b
```
Examples: "Demo Plan", "Level 1 Demolition", "Floor Plan - D", "L1 DEMOL"

**New patterns:**
```
\bnew\b    \b_n\b    \s+n$    -n$    \bproposed\b
```
Examples: "New Work Plan", "Level 1 New", "Floor Plan - N", "Proposed Work"

### Plan SubType Detection (DetectPlanSubType)

| Returns | Condition |
|---------|-----------|
| `"rcp"` | ViewType = CeilingPlan, or name matches `reflected ceiling\|r\.c\.p\b\|\brcp\b\|ceiling plan` |
| `"roofPlan"` | Name matches `roof plan` |
| `"bracedWall"` | Name matches `braced? wall\|wall brac\|shear wall plan\|lateral plan` |
| `"egress"` | Name matches `egress plan\|life safety\|exit plan` |
| `"floorPlan"` | Default |

---

## 5. classifyInventory() Return Object (NcsClassifiedInventory)

```csharp
{
  All:             List<NcsClassifiedView>,  // every view processed
  Blocked:         List<NcsClassifiedView>,  // confidence = "blocked"
  Definite:        List<NcsClassifiedView>,  // confidence = "definite"
  Probable:        List<NcsClassifiedView>,  // confidence = "probable"
  Ambiguous:       List<NcsClassifiedView>,  // confidence = "ambiguous"
  SlotMap:         Dictionary<string, List<NcsClassifiedView>>,
                   // key = "A1", "G0", "S5" etc.
                   // contains Definite + Probable only
  RenovationStatus: {
    IsRenovation: bool,
    IsValid:      bool,   // all present levels have full trio
    Issues:       [ { Level, Has: [], Missing: [] } ]
  },
  PermitWarnings:  List<{ Id, Description }>,
  Stats: {
    Total, Blocked, Definite, Probable, Ambiguous,
    ClassifiedPercent   // (Definite + Probable) / (Total - Blocked) × 100
  }
}
```

---

## 6. sheetPacker

### Fill Target and Sheet Size

- Printable area: **ARCH D — 32 × 20 inches = 640 sq in**
- Fill target: **75%** of printable area (480 sq in)
- Overflow threshold: **105%** — if adding a view would exceed 672 sq in, start new sheet
- Views under 75% fill receive `NeedsMoreContent = true` flag

### Viewport Area Calculation

When a view has crop box data and scale:
```
ratio = 12.0 / scaleDenominator          (converts feet to paper inches)
paperWidthIn  = cropWidthFt  × ratio
paperHeightIn = cropHeightFt × ratio
area = paperWidthIn × paperHeightIn      (square inches)
```

When crop data is unavailable (fallback estimates):

| Slot | Estimated sq in |
|------|----------------|
| A0   | 250 |
| A1   | 180 |
| A2   | 120 |
| A3   | 100 |
| A4   | 80  |
| A5   | 30  |
| A6   | 60  |
| A7   | 40  |
| G0   | 200 |
| G1   | 80  |
| S1   | 180 |
| S5   | 30  |

### NCS Processing Order (PackInventory)

Slots are processed in this sequence:
```
G0 → G1 → G2 → A0 → A1 → A2 → A3 → A4 → A5 → A6 → A7
S0 → S1 → S5 → C1 → C5 → L1 → M1 → M3 → M6 → P1 → P3 → P6 → E1 → E3 → E6
```

### Floor Plan Special Treatment (A1)

Floor plans get **one sheet per level**. Level sort order:
```
B → 1 → 2 → 3 → 4 → M → R → unknown
```

Within each level, views are sorted:
1. By renovation condition: `existing (0) → demo (1) → new (2) → none (3)`
2. Then alphabetically by name

Sheet IDs: A1.1, A1.2, A1.3... (sequential regardless of level)

### Gap Sheets

If a required slot has no views, a gap sheet is inserted with `IsGap = true`:

| Required slot | Gap label |
|--------------|-----------|
| G0 | Cover / Sheet Index / Project Data |
| G1 | General Notes / Energy Summary |
| A0 | Site Plan / Code Analysis / Life Safety |
| A1 | Floor Plans / RCPs / Roof Plan |
| A6 | Architectural Schedules |

### PackedPlan Output Fields (NcsPackedPlan)

```csharp
{
  Sheets: [
    {
      SheetId:     "A1.1",        // e.g. A1.1
      Discipline:  "A",
      SheetType:   1,
      SequenceNum: 1,
      SlotKey:     "A1",
      Level:       "1",           // null for non-floor-plan slots
      Viewports:   [ NcsClassifiedView, ... ],
      UsedArea:    320.5,         // sq in
      FillPercent: 50,            // int (0-100+)
      NeedsMoreContent: true,     // fill < 75%
      IsOverfull:  false,         // fill > 100%
      IsGap:       false,
      GapLabel:    null,
    }
  ],
  Gaps: [
    { SheetId: "G0.1", Label: "Cover / Sheet Index / Project Data", GapNotes: null }
  ],
  AmbiguousViews: [ NcsClassifiedView, ... ],
  RenovationStatus: { ... },
  Stats: {
    TotalSheets,        // non-gap sheets
    GapSheets,
    SheetsUnderFill,
    AmbiguousViewCount,
    AverageFill         // int percent
  }
}
```

### BuildPromptInventoryBlock — 6-Section Output

The function produces a structured text block with these sections (only non-empty sections appear):

1. **PRE-ASSIGNED SHEET LAYOUT** — definite/probable sheets with viewport list and fill %
2. **REQUIRED SHEETS WITH NO ASSIGNED VIEWS (GAPS)** — gap sheets Claude must populate
3. **SHEETS UNDER 75% FILL** — sheets needing more views from ambiguous list
4. **UNCLASSIFIED VIEWS — YOU MUST ASSIGN THESE** — ambiguous views with reason
5. **RENOVATION TRIO VIOLATIONS** — levels missing existing/demo/new
6. **BLOCKED VIEWS** — views stripped from the set (must not be placed)
7. **PERMIT-CRITICAL WARNINGS** — 9 automated permit check results

---

## 7. planValidator

### Signature

```javascript
validatePlan(plan, { isRenovation, blockedViewIds, throwOnError })
```

- `plan`: the PackedPlan object
- `isRenovation`: boolean — enables renovation trio check
- `blockedViewIds`: Set of view IDs that must not appear on sheets
- `throwOnError`: if true, throws on first error instead of collecting all

### 6 Validation Checks

| Check | Severity | Rule |
|-------|----------|------|
| Sheet ID format | **error** | Every sheet ID must match `^[A-Z][0-9]\.[0-9]+$` |
| No blocked views | **error** | No view in `blockedViewIds` may appear on any sheet |
| No blank sheets | **error** | Every non-gap sheet must have at least one viewport |
| Required sheets present | **error** | G0, G1, A0, A1, A6 must all exist |
| Renovation trios complete | **error** | If `isRenovation`, every level must have existing + demo + new |
| Wrong content on sheet | **warn** | View's classified slot doesn't match the sheet it's placed on |

### Valid vs. Rejected Sheet IDs

| Status | Examples |
|--------|---------|
| Valid | `A1.1`, `G0.1`, `S5.3`, `M6.1`, `A10.1` (wait — A10 fails: type must be single digit) |
| Rejected | `A1`, `a1.1` (lowercase), `A1.`, `1A.1`, `A01.1`, `AA.1`, `A1.1.2` |

Correct regex: `^[A-Z][0-9]\.[0-9]+$` — single uppercase letter, single digit, dot, one or more digits.

### Recommended Integration Pattern

```
1. Run classifyAndPackViews (C# MCP method)
2. Review promptBlock — use definite assignments directly
3. Resolve ambiguous views (Claude decides)
4. Build final plan object
5. Run planValidator gate
   → If errors: fix and re-validate before proceeding
   → If warnings only: proceed, flag warnings to Barrett
6. Issue MCP commands in NCS order
```

---

## 8. Permit-Critical Checks (9 automated)

Run by `RunPermitChecks()` after classification. Failures are warnings, not hard errors. All 9 must be resolved before permit submission.

| Check ID | Description | What triggers it |
|----------|-------------|-----------------|
| `site_plan` | Site plan (A0.1) must exist | A0 slot empty or no view with "site" in name |
| `code_summary` | Code/zoning analysis on A0 | A0 slot empty entirely |
| `energy_summary` | Energy code R-value/U-factor table (G1) | G1 has no view with energy/insulation/r-value/u-factor in name |
| `window_schedule_energy` | Window schedule with U-factor + SHGC columns (A6) | A6 has no view with "window" in name |
| `rcp_exists` | Reflected ceiling plan(s) present (A1) | No A1 view with planSubType=rcp or RCP in name |
| `braced_wall` | Braced wall plan per story IRC R602.10 | No A1 or S1 view with bracedWall subtype or brace/shear wall in name |
| `wall_section` | Wall section with insulation layers (A3) | A3 slot has no view with "wall section" in name |
| `stair_detail` | Stair/guardrail detail for multi-story projects (A5) | Multi-story AND no A5 view with stair/guardrail/handrail in name |
| `smoke_co_shown` | Smoke/CO detector locations on RCP or plans | No A1 or A6 view with smoke/carbon monoxide/detector/alarm in name |

---

## 9. classifyAndPackViews MCP Method

Call this **first** in every sheet placement session. It runs the full C# pipeline in-process.

```
Method: classifyAndPackViews
Category: SheetLayout
Parameters: { viewIds?: number[] }   // optional — filter to specific view IDs
```

**Response fields:**

| Field | Type | Description |
|-------|------|-------------|
| `promptBlock` | string | Full 6-section inventory block — inject directly into context |
| `totalViews` | int | Total views processed |
| `classifiedPct` | int | Percent of non-blocked views classified definite or probable |
| `totalSheets` | int | Non-gap sheets in the plan |
| `gaps` | int | Gap sheets (required slots with no views) |
| `ambiguous` | int | Views that need Claude's decision |
| `permitWarnings` | int | Permit-critical warning count |
| `isRenovation` | bool | True if renovation views detected |
| `renovationValid` | bool | True if all renovation trios are complete |
| `sheets` | array | Sheet objects with viewports (see below) |

**Sheet object in response:**
```json
{
  "sheetId":   "A1.1",
  "slotKey":   "A1",
  "level":     "1",
  "fillPct":   68,
  "needsMore": true,
  "viewports": [
    { "id": 12345, "name": "Level 1 Floor Plan", "viewType": "FloorPlan",
      "confidence": "definite", "planSub": "floorPlan", "detailSub": null,
      "level": "1", "reno": null }
  ]
}
```

---

## 10. Key Rules for Banana Chat

### Execution Order

1. **Call `classifyAndPackViews` FIRST** — always, before any sheet creation or viewport placement
2. **`promptBlock` is authoritative for definite assignments** — never override definite classifications
3. **Create sheets in NCS order:** G0 → G1 → A0 → A1 → A2 → A3 → A4 → A5 → A6 → A7 → S → M → P → E
4. **Use `sheetId` from promptBlock** — never generate your own sheet numbers
5. **Populate gap sheets** — create placeholders for all required slots even if empty
6. **Renovation trios must be complete per level** — verify all three (existing, demo, new) before proceeding
7. **Never place blocked views** — if Barrett requests a blocked view, warn and ask for confirmation

### Detail SubType Sequence Assignment

- `exterior` → start at A5.1 and increment
- `thermalEnvelope` → start at A5.3 and increment
- `stair` → start at A5.5 and increment
- `interior` → start at A5.7 and increment
- `unspecified` → fill remaining A5 sequence numbers

### Common Mistakes to Avoid

- Creating sheets before calling `classifyAndPackViews`
- Using arbitrary sheet numbers not from promptBlock
- Calling Claude for views that already have definite assignments
- Skipping the planValidator gate
- Placing blocked views
- Treating interior elevations as A2 (they go to A7)
- Routing lighting fixture schedules to E6 (they go to A6)
- Missing the floor-plan-level packing rule — each level must be a separate sheet

### Sheet Asterisk Rule

Always append ` *` to every sheet name created or edited. Do not wait to be asked.

### Stacked Viewport Check

After any placement, audit for stacked/centered viewports and fix label offsets per-view using `getDraftingViewBounds`.
