# Detail Drawing Playbook — Revit MCP API

**Purpose:** Step-by-step guide for drawing professional construction details via the MCP bridge.
**Based on:** BD Architect detail library analysis (40+ details), Revit API testing, Weber's feedback.

---

## Golden Rules

1. **Leaders from text** — ALWAYS use `createTextNoteWithLeader`. NEVER draw separate lines as leaders.
2. **Fill all gaps** — Every material layer gets a filled region or detail component. No floating geometry.
3. **Notes follow assembly order** — Top-to-bottom reading order matches top-to-bottom layer order.
4. **ALL CAPS text** — Every callout, every note.
5. **Specific callouts** — "5/8" TYPE 'X' GYP. BD." not "DRYWALL". Include size, type, spacing.
6. **Leaders angled 30-60°** — Never horizontal. The `createTextNoteWithLeader` method handles the elbow automatically.
7. **Leaders never cross** — Arrange note positions to fan leaders out without intersection.
8. **Line weight hierarchy** — Cut lines (Wide), projection lines (Medium), annotation/leaders (Thin).

---

## API Methods Reference

### Text with Leaders (PRIMARY — use this for all annotations)
```json
{
  "method": "createTextNoteWithLeader",
  "params": {
    "viewId": <int>,
    "text": "CALLOUT TEXT",
    "textX": <float>,    // Text position X (feet)
    "textY": <float>,    // Text position Y (feet)
    "textZ": 0.0,
    "leaderX": <float>,  // Leader endpoint X (touches the element)
    "leaderY": <float>,  // Leader endpoint Y (touches the element)
    "leaderZ": 0.0,
    "expandCrop": true
  }
}
```

### Add Leader to Existing Text
```json
{
  "method": "addLeaderToTextNote",
  "params": {
    "textNoteId": <int>,
    "leaderX": <float>,
    "leaderY": <float>,
    "leaderZ": 0.0
  }
}
```

### Adjust Leader Position
```json
{
  "method": "setTextNoteLeaderEndpoint",
  "params": {
    "textNoteId": <int>,
    "leaderIndex": 0,
    "leaderX": <float>,
    "leaderY": <float>,
    "leaderZ": 0.0
  }
}
```

### Wall Assembly (drawLayerStack)
```json
{
  "method": "drawLayerStack",
  "params": {
    "viewId": <int>,
    "layers": [
      {"name": "5/8\" GWB", "thickness": 0.0521, "material": "Gypsum board"},
      {"name": "2x6 STUD", "thickness": 0.4583, "material": "Wood 1"},
      {"name": "7/16\" OSB", "thickness": 0.0365, "material": "Plywood"}
    ],
    "leftBound": 0.0,
    "bottomBound": -1.5,
    "topBound": 0.5,
    "orientation": "horizontal"
  }
}
```

### Filled Regions (for gaps, flashing, membranes)
```json
{
  "method": "batchDrawDetail",
  "params": {
    "viewId": <int>,
    "operations": [
      {
        "op": "filledRegion",
        "points": [[x1,y1],[x2,y2],[x3,y3],[x4,y4]],
        "fillType": "Concrete"
      }
    ]
  }
}
```

Available fill types: `Concrete`, `Gypsum board`, `Wood 1`, `Plywood`, `Sand`, `RIGID INSULATION`, `Solid Black`, `Earth`, `Diagonal Up`, `Steel`

### Detail Components (point-based — confirmed working)
- Dimension Lumber
- Anchor Bolt
- Break Line
- Caulking
- C Studs
- Window Sill / Window Sash

### Lines
```json
{
  "method": "batchDrawDetail",
  "params": {
    "viewId": <int>,
    "operations": [
      {"op": "line", "start": [x1,y1], "end": [x2,y2], "lineStyle": "<Wide Lines>"}
    ]
  }
}
```

Line styles: `<Wide Lines>` (cut), `<Medium Lines>` (projection), `<Thin Lines>` (annotation), `<Hidden Lines>` (dashed)

---

## Annotation Placement Strategy

### Right-Side Notes (exterior elements, looking at section)
- Align all text at same X position (e.g., X = 1.5)
- Leader endpoints touch the exact layer in the assembly
- Space notes vertically ~0.15 ft apart
- Order: top layer first, bottom layer last

### Left-Side Notes (interior elements)
- Align all text at same X position (e.g., X = -1.2)
- Same fanning/spacing rules

### Construction Notes
- Place below the detail
- Numbered list format
- Include: slopes, membrane types, sealant locations, clearances, installation sequences

### Dimensions
- Use sparingly — embed dimensions in callout text instead
- Show overall assembly thickness as a separate dimension
- Show critical clearances (rough opening gaps, etc.)
- **Tiered placement:** 1st dim line 1/2" from object, subsequent at 3/8" intervals
- Dimension tick marks (not arrowheads) at extension line intersections
- Slopes shown with direction arrows (e.g., "2% MAX. SLOPE")

---

## Fill Region Strategy — No Gaps

### What ALWAYS gets a filled region:
| Element | Fill Type | Notes |
|---------|-----------|-------|
| Batt insulation | `Wood 1` (wavy) or dedicated insulation hatch | Fills entire stud cavity |
| Concrete/CMU | `Concrete` (stipple) | Full block/slab profile |
| Gypsum board | `Gypsum board` or thin outline | Thin layer, may not need fill |
| Wood framing | `Wood 1` (grain pattern) | For solid wood members |
| Plywood/OSB | `Plywood` (cross-hatch) | Sheathing layers |
| Rigid insulation | `RIGID INSULATION` | Board insulation |
| Earth/grade | `Earth` or `Sand` | Below grade line |
| Metal flashing | `Solid Black` | Thin, solid fill |
| Sealant | `Solid Black` (small) | Small bead profile |
| Membrane/WRB | Bold line only | Single line, no fill needed |

### What stays WHITE (intentionally):
- Air spaces and cavities
- Metal stud profiles (drawn as outline only)

---

## Standard Callout Text Formats

### Wall Layers (specific format)
- `5/8" TYPE 'X' GYP. BD.`
- `3-5/8" MTL. STUDS @ 16" O.C.`
- `2x6 WOOD STUDS @ 16" O.C.`
- `R-21 BATT INSULATION`
- `7/16" OSB SHEATHING`
- `SELF-ADHERED WRB / AIR BARRIER`
- `1" CONT. RIGID INSULATION (R-5)`

### Window/Door Elements
- `WINDOW SASH (WOOD DBL. HUNG)`
- `SLOPED WOOD SILL (1/4"/FT MIN.)`
- `SELF-ADHERED MEMBRANE SILL PAN`
- `CONT. BEAD OF SEALANT (TYP.)`
- `H.M. DOOR FRAME (16 GA.)`
- `WOOD STOOL AND APRON`

### Roof/Flashing
- `MODIFIED BITUMEN ROOF MEMBRANE`
- `TAPERED RIGID INSULATION (1/4"/FT)`
- `GALV. METAL EDGE FLASHING`
- `CONT. CLEAT (24 GA. GALV.)`

### Structural
- `#4 REBAR @ 12" O.C. EA. WAY`
- `4" CONC. SLAB ON GRADE W/ 6x6 W.W.M.`
- `VAPOR BARRIER OVER 4" COMP. GRAVEL`

---

## Drawing Priority Order

Use elements in this order of preference:
1. **Detail Components** (best) — intelligent, taggable, keynote-able
2. **Repeating Detail Components** — studs at O.C., brick courses
3. **Filled Regions** — material hatching for areas
4. **Detail Lines** (last resort) — one-off geometry only

Keynotes can ONLY attach to detail components and model elements, NOT to detail lines or filled regions.

## Drawing Build Sequence

Draw layers in this order (structure first, annotations last):
1. Structure (studs, joists, slabs, beams)
2. Sheathing/substrate (plywood, OSB, CMU)
3. Membranes and barriers (air barrier, vapor barrier, WRB)
4. Insulation (batt or rigid)
5. Flashing and drainage
6. Cladding/finish (exterior and interior)
7. Accessories (sealant, backer rod, trim, fasteners)
8. Break lines
9. Dimensions
10. Notes and leaders
11. Hatching (LAST — never interferes with geometry)

## Reference Detail Sheets (for visual comparison)

| Path | Content |
|------|---------|
| `/mnt/d/2-118_BHN_Detail_PDFs/` | BHN healthcare detail sheet PNGs (partitions, doors, misc) |
| `/mnt/d/BDArchitect-DetailLibrary-2025/previews/` | 100+ detail preview images by category |
| `/mnt/d/003 - RESOURCES/05 - STANDARDS/Wood Details/` | AWC wood construction standard PDFs |

---

## Workflow Sequence

### Step 1: Create Drafting View
```json
{"method": "createDraftingView", "params": {"name": "DETAIL NAME", "scale": 4}}
```
Scale: 4 = 3"=1'-0" (window/door details), 8 = 1-1/2"=1'-0" (wall sections), 12 = 1"=1'-0" (building sections)

### Step 2: Draw Assembly (drawLayerStack)
Build the wall/floor/roof assembly with proper layers, hatching, and bounds.

### Step 3: Add Detail Components
Place real Revit detail components for specific elements (lumber, caulking, sills, etc.)

### Step 4: Fill Gaps
Add filled regions for any remaining materials not covered by drawLayerStack or components.

### Step 5: Add Break Lines
Place break line components at top and bottom if the assembly continues beyond the detail.

### Step 6: Annotate with Leaders
Use `createTextNoteWithLeader` for EVERY callout. Text on right side for exterior, left for interior.
- Position leader endpoints precisely on the layer being called out
- Fan leaders at angles — avoid crossing
- Follow assembly stacking order

### Step 7: Add Dimensions
Overall assembly thickness. Critical clearances only.

### Step 8: Add Construction Notes
Numbered list below the detail with installation requirements.

### Step 9: Export and Review
```json
{"method": "exportViewImage", "params": {"viewId": <int>, "imageSize": 2000}}
```

### Step 10: Iterate
Compare against professional examples. Fix alignment, add missing info, adjust spacing.

---

## Quality Checklist

- [ ] Every material layer has a filled region or detail component
- [ ] No gaps or floating geometry
- [ ] All text uses leaders (not separate lines)
- [ ] Leaders angled 30-60°, no crossings
- [ ] Notes are ALL CAPS with specific material info
- [ ] Notes follow assembly stacking order
- [ ] Line weight hierarchy: cut > projection > annotation
- [ ] Construction notes below detail
- [ ] Overall assembly dimension shown
- [ ] Break lines where assembly continues
- [ ] Interior/exterior sides clearly distinguished
