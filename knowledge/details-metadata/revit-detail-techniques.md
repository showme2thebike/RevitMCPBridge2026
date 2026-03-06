# Revit Detail Techniques -- From Theory to API Execution

> The bridge between "what a detail should look like" and "how to create it in Revit via MCP."
> Generated: 2026-03-05

---

## TABLE OF CONTENTS

1. [View Types: Drafting vs Detail](#1-view-types-drafting-vs-detail)
2. [Drawing Elements Hierarchy](#2-drawing-elements-hierarchy)
3. [Detail Components](#3-detail-components)
4. [Repeating Detail Components](#4-repeating-detail-components)
5. [Filled Regions & Hatch Patterns](#5-filled-regions--hatch-patterns)
6. [Insulation Batting Pattern](#6-insulation-batting-pattern)
7. [Line Styles & Weights](#7-line-styles--weights)
8. [Annotation in Details](#8-annotation-in-details)
9. [Detail Organization & Libraries](#9-detail-organization--libraries)
10. [API Method Reference](#10-api-method-reference)
11. [End-to-End Workflow Examples](#11-end-to-end-workflow-examples)
12. [Coordinate System Reference](#12-coordinate-system-reference)

---

## 1. VIEW TYPES: DRAFTING VS DETAIL

### When to Use Each

| View Type | Source | Shows Model? | Has Crop Region? | Use For |
|-----------|--------|-------------|------------------|---------|
| **Drafting View** | Created from View menu | No | No | Standard/generic details not tied to model geometry. Carpet transitions, generic flashing, typical sections. |
| **Detail View (Callout)** | Created as callout from plan/section/elevation | Yes | Yes | Project-specific details where model geometry provides the base (wall section at specific location). |
| **Detail View (Section)** | Section cut through model | Yes | Yes | Building sections, wall sections at specific conditions. |

### Key Differences

**Drafting Views:**
- Pure 2D canvas with no model geometry visible
- Not tied to any location in the building
- Can be referenced by callouts using "Reference Other View" option
- Content is view-specific (detail lines, detail components, filled regions, text)
- Cannot be cropped (size controlled by content and scale)
- Ideal for reusable standard details

**Detail Callouts:**
- Start with model geometry (walls, floors, roofs shown as cut/projected)
- Tied to a specific model location -- moves if model changes
- Can add 2D embellishments on top of model geometry
- Use when the model provides 80%+ of the geometry and you just annotate/embellish
- Crop region controls visible area

### Creating via API

```json
// Drafting view
{"method": "createView", "params": {"type": "Drafting", "viewName": "TYPICAL WALL SECTION", "scale": 16}}

// Detail callout (from a section or plan)
{"method": "createCallout", "params": {"parentViewId": 12345, "min": {"x": 10, "y": 0, "z": 5}, "max": {"x": 15, "y": 0, "z": 12}}}
```

### Reference Other View (Linking Callouts to Drafting Views)

A callout marker can reference a drafting view instead of creating a new model view. This maintains the visual link (callout bubble on plans) while the actual detail content lives in a reusable drafting view. In the Revit UI, check "Reference Other View" in the options bar before drawing the callout. Via API, this is set using the `ReferenceOtherView` property on the callout.

---

## 2. DRAWING ELEMENTS HIERARCHY

### Priority Order (Best Practice)

Use these element types in this priority order. Higher = preferred.

| Priority | Element | When to Use | Why |
|----------|---------|-------------|-----|
| **1** | Detail Components | Any standard construction element (studs, GWB, rebar, bolts, siding) | Intelligent, taggable, consistent, reusable. Can be keynoted. |
| **2** | Repeating Detail Components | Repetitive patterns (stud spacing, rebar, masonry courses, roof tiles) | Auto-spaces components along a path. |
| **3** | Filled Regions | Material representation in section (concrete, earth, insulation, gravel) | Shows material graphic convention. Can be transparent or opaque. |
| **4** | Insulation Tool | Batt insulation (wavy line pattern) | Purpose-built for the batting pattern. |
| **5** | Detail Lines | Outlines, edges, custom geometry not covered by components | Fast but not intelligent -- cannot be tagged or scheduled. |
| **6** | Text Notes | Labels, callouts, notes | Use keynotes instead when possible. |

### Why Detail Components Over Lines

- **Taggable**: Detail components can have keynotes attached. Loose lines cannot.
- **Consistent**: Same family = same appearance everywhere.
- **Editable**: Change the family once, all instances update.
- **Schedulable**: Can appear in component schedules.
- **Liability**: Reduces incorrect information. A detail component has embedded data; a line is just a line.

### The Cardinal Rule

> "If the detail is to be 2D yet still remain intelligent, use detail component families and tags -- not lines or text. This ensures consistency in appearance, information, and spelling, while limiting liability." -- Autodesk University

---

## 3. DETAIL COMPONENTS

### What They Are

Detail components are 2D Revit families (`.rfa` files) of category `OST_DetailComponents`. They represent materials in section or elevation within detail/drafting views. They are view-specific elements.

### Standard Revit Detail Components (Built-in)

These ship with Revit in the default library:

| Family Name | Typical Types | Use For |
|-------------|---------------|---------|
| Dimension Lumber-Section | 2x4, 2x6, 2x8, 2x10, 2x12 | Wood framing in section |
| Nominal Cut Lumber-Section | 4x4, 4x6, 6x6 | Posts, beams in section |
| Gypsum Wallboard-Section | 1/2", 5/8" | GWB layers |
| Plywood-Section | 1/2", 5/8", 3/4" | Sheathing, subfloor |
| C Studs-Section | 3-5/8", 6" | Metal stud framing |
| Runner Channels-Section | 3-5/8", 6" | Metal stud track |
| Rigid Insulation-Section | 1", 1-1/2", 2" | Rigid board insulation |
| Reinf Bar Section | #3 through #8 | Rebar dots in concrete section |
| Concrete-Section | Various | Concrete fill |
| Anchor Bolt | 1/2", 5/8" | Anchor bolts in section |
| Break Line | Standard | Break lines for shortened views |
| Siding-Wood Bevel | Various | Lap siding in elevation |

### BD Architect Library Components (87 Families)

See `detail-components-reference.md` for the complete 87-family reference from the BD Architect library. Key categories:

- **Framing**: Dimension Lumber-Section, Nominal Cut Lumber-Section, C Studs-Section, Runner Channels-Section, Interior Metal Channels-Section
- **Sheathing**: Gypsum Wallboard-Section, Gypsum Sheathing-Section, Plywood-Section
- **Masonry**: 04-CMU-2 Core-Section, CMU-2 Core-Side, Bond Beams-Single-Section
- **Fasteners**: Anchor Bolt, Lag Bolt Side, Self-Tapping Screw, Concrete Screw
- **Finish**: Resilient Topset Base-Section, Tile Thin Set-Section, Acoustical Ceiling Tile-Section
- **Roofing**: Asphalt Shingle-Section, Standing Seam Metal Panel-Section, Single-Ply Membrane
- **Waterproofing**: Flashing-Section, Sealant-Section, Drip Edge-Section

### Placing Detail Components via API

```json
// By family + type name
{
  "method": "placeDetailComponentByName",
  "params": {
    "viewId": 12345,
    "familyName": "Dimension Lumber-Section",
    "typeName": "2x6",
    "location": {"x": 1.0, "y": 2.0, "z": 0}
  }
}

// By type ID (faster, no name lookup)
{
  "method": "placeDetailComponent",
  "params": {
    "viewId": 12345,
    "typeId": 67890,
    "location": {"x": 1.0, "y": 2.0, "z": 0}
  }
}

// With rotation (degrees)
{
  "method": "placeDetailComponentAdvanced",
  "params": {
    "viewId": 12345,
    "typeId": 67890,
    "location": {"x": 1.0, "y": 2.0, "z": 0},
    "rotation": 90
  }
}

// In batchDrawDetail
{
  "method": "batchDrawDetail",
  "params": {
    "viewId": 12345,
    "operations": [
      {"op": "detailComponent", "familyName": "Dimension Lumber-Section", "typeName": "2x6", "location": [1.0, 2.0], "rotation": 0},
      {"op": "detailComponent", "familyName": "Reinf Bar Section", "typeName": "#5", "location": [0.25, -0.75]}
    ]
  }
}
```

### Querying Available Components

```json
// Get all detail component types loaded in document
{"method": "getDetailComponentTypes", "params": {}}

// Get all detail component families (grouped by family)
{"method": "getDetailComponentFamilies", "params": {}}

// Search local disk for .rfa files
{"method": "searchLocalDetailFamilies", "params": {"searchPath": "C:\\ProgramData\\Autodesk\\RVT 2026\\Libraries"}}

// Load a family from disk
{"method": "loadDetailComponentFamily", "params": {"familyPath": "C:\\path\\to\\family.rfa"}}
```

---

## 4. REPEATING DETAIL COMPONENTS

### What They Are

A repeating detail is a pattern of detail components automatically spaced along a line path. Typical uses:
- Wood studs at 16" O.C.
- Metal studs at 16" or 24" O.C.
- Brick courses in elevation
- Masonry units in section
- Roof shingles
- Rebar spacing

### How They Work

1. You sketch a path (2-point line)
2. Revit fills the path with repeated instances of a detail component
3. Type properties control: which component, spacing, layout method

### Type Properties

| Property | Options | Description |
|----------|---------|-------------|
| Detail | Family:Type | The component to repeat |
| Layout | Fixed Distance, Fixed Number, Fill Available Space, Maximum Spacing | How spacing is calculated |
| Inside | Check/Uncheck | Whether components are inside or straddle the path |
| Spacing | Distance value | Distance between component origins |

### Placing via API

```json
{
  "method": "placeRepeatingDetailComponent",
  "params": {
    "viewId": 12345,
    "typeId": 67890,
    "startPoint": {"x": 0, "y": 0, "z": 0},
    "endPoint": {"x": 0, "y": 8.0, "z": 0}
  }
}
```

### Common Repeating Detail Setups

| Use Case | Component | Spacing | Notes |
|----------|-----------|---------|-------|
| Wood studs @ 16" O.C. | Dimension Lumber-Section : 2x4 | 1.333' (16") | Layout: Fixed Distance |
| Wood studs @ 24" O.C. | Dimension Lumber-Section : 2x6 | 2.0' (24") | Layout: Fixed Distance |
| Metal studs @ 16" O.C. | C Studs-Section : 3-5/8" | 1.333' (16") | Layout: Fixed Distance |
| Rebar @ 12" O.C. | Reinf Bar Section : #4 | 1.0' (12") | Vertical rebar in CMU |
| Brick courses | Brick (detail item) | ~0.222' (2-2/3") | Course height including mortar |

---

## 5. FILLED REGIONS & HATCH PATTERNS

### What They Are

Filled regions are closed 2D shapes with a fill pattern applied. They represent material cross-sections in detail views using standard graphic conventions.

### Standard Material Patterns (Drafting Patterns)

These are the patterns available in a typical Revit template for representing materials in section:

| Pattern Name | Visual | Represents | Notes |
|-------------|--------|------------|-------|
| Concrete | Random dots/triangles | Cast-in-place concrete | AIA/CSI standard |
| Earth | Stippled dots | Earth/soil | Used at grade line |
| Sand | Fine dots | Sand/gravel base | Lighter than earth |
| Gravel | Larger random dots | Gravel fill | Under slabs |
| Wood | Parallel lines | Wood in section | Grain direction matters |
| Plywood | Cross-hatch | Plywood/OSB sheathing | 45-degree cross |
| Steel | Dense diagonal lines | Steel in section | 45-degree single direction |
| Aluminum | Alternating diag lines | Aluminum in section | |
| Insulation | Wavy lines (or use tool) | Batt insulation | Batting pattern preferred |
| Rigid Insulation | Diagonal cross-hatch | Rigid board insulation | Evenly spaced X pattern |
| Gypsum Board | Fine stipple ("Sand") | GWB in section | Light, uniform dots |
| Gypsum Plaster | Larger stipple | Plaster finish | |
| CMU | Block pattern | Concrete masonry | Shows cells and webs |
| Brick | Stacked rectangles | Brick in section | With mortar joints |
| Masonry | Irregular stone | Stone masonry | Random coursing |
| Glass | None (just outline) | Glass in section | Usually just two lines |
| Diagonal Crosshatch | X pattern | General fill | Multi-purpose |

### Creating Filled Regions via API

```json
// Direct API call
{
  "method": "createFilledRegion",
  "params": {
    "viewId": 12345,
    "filledRegionTypeId": 67890,
    "boundaryLoops": [[
      {"x": 0, "y": 0, "z": 0},
      {"x": 2, "y": 0, "z": 0},
      {"x": 2, "y": 1, "z": 0},
      {"x": 0, "y": 1, "z": 0}
    ]]
  }
}

// In batchDrawDetail (by type name)
{
  "method": "batchDrawDetail",
  "params": {
    "viewId": 12345,
    "operations": [
      {"op": "filledRegion", "typeName": "Concrete", "points": [[0,0],[2,0],[2,-1],[0,-1]]},
      {"op": "filledRegion", "typeName": "Earth", "points": [[0,-1],[2,-1],[2,-2],[0,-2]]}
    ]
  }
}
```

### Querying Available Filled Region Types

```json
{"method": "getFilledRegionTypes", "params": {}}
```

Returns array of `{id, name, fillPatternName, isSolid, isMasking, foregroundColor}`.

### Layer Order (Draw Order)

Filled regions can overlap. The draw order determines visibility:
- **Last drawn = on top** (in the same view)
- Use `overrideElementGraphics` to adjust if needed
- **Transparent regions** can overlay opaque ones (insulation over framing)

### Practical Layer Stacking

For a typical wall section, draw filled regions in this order (bottom to top):

1. **Concrete/masonry** (opaque, heavy outline)
2. **Wood framing** (opaque, medium outline)
3. **Sheathing** (opaque, thin outline)
4. **Insulation** (transparent, overlapping framing cavity)
5. **GWB/finish** (opaque, thin outline)
6. **Earth** (opaque, at grade)

### Transparent vs Opaque

- **Opaque** (default): Region covers everything behind it. Used for solid materials (concrete, wood, GWB).
- **Transparent**: Region shows pattern but allows elements behind to show through. Used for insulation overlays, air barriers.
- Set via filled region type properties: Background = Transparent.

### Masking Regions

A masking region is a filled region with no pattern -- it simply hides model geometry behind it. Useful for cleaning up model details where you want to redraw a portion in 2D.

```json
{
  "method": "createMaskingRegion",
  "params": {
    "viewId": 12345,
    "boundaryLoops": [[
      {"x": 0, "y": 0, "z": 0},
      {"x": 5, "y": 0, "z": 0},
      {"x": 5, "y": 3, "z": 0},
      {"x": 0, "y": 3, "z": 0}
    ]]
  }
}
```

---

## 6. INSULATION BATTING PATTERN

### The Wavy Line

The insulation batting pattern (the distinctive S-curve/wavy line representing batt insulation) is created in Revit through three methods:

### Method A: Insulation Tool (Preferred in Revit UI)

- Annotate tab > Detail panel > Insulation
- Draw a path; Revit fills with batting curves
- Adjustable width and bulge ratio in Properties
- Creates a special `InsulationLiningBase` element

### Method B: `placeInsulationPattern` MCP Method

The bridge has a dedicated method that draws the batting pattern algorithmically between two vertical lines:

```json
{
  "method": "placeInsulationPattern",
  "params": {
    "viewId": 12345,
    "leftX": 1.0,
    "rightX": 1.458,
    "bottomY": 0,
    "topY": 8.0,
    "bulgeCount": 12
  }
}
```

This draws alternating sine-wave arcs between the left and right boundaries, creating the standard batting appearance.

### Method C: `addInsulation` MCP Method

Adds insulation to an existing detail line:

```json
{
  "method": "addInsulation",
  "params": {
    "viewId": 12345,
    "lineId": 67890,
    "width": 0.458
  }
}
```

### Method D: In `drawAssemblyDetail`

When `drawAssemblyDetail` encounters a material classified as "insulation" (batt, fiberglass, mineral wool), it automatically calls `DrawBattInsulation()`, which generates the wavy line pattern between the layer boundaries.

### Method E: Detail Component

Use a parametric batt insulation detail component family with adjustable width parameter. Place as any other detail component.

---

## 7. LINE STYLES & WEIGHTS

### Standard Line Styles in Revit

| Line Style | Pen Weight | Use In Details | API Name |
|------------|-----------|----------------|----------|
| **Wide Lines** | 5 (~0.50mm) | Cut lines of primary elements (exterior wall faces, structural members) | `"Wide Lines"` |
| **Medium Lines** | 3 (~0.35mm) | Cut lines of secondary elements (interior partitions, framing members) | `"Medium Lines"` |
| **Thin Lines** | 1 (~0.18mm) | Projection lines, annotations, leaders, dimensions, detail pattern lines | `"Thin Lines"` |
| **<Hidden>** | 1 (~0.18mm) | Hidden/concealed elements, dashed pattern | `"Hidden"` or `"<Hidden>"` |
| **<Centerline>** | 1 (~0.18mm) | Center lines of symmetrical elements, center-dash-center pattern | `"<Centerline>"` |
| **<Overhead>** | 1 (~0.18mm) | Elements above the cut plane | `"<Overhead>"` |
| **<Beyond>** | 1 (~0.18mm) | Visible elements beyond the cut plane | `"<Beyond>"` |
| **<Demolished>** | 1 (~0.18mm) | Demolished elements in phasing | `"<Demolished>"` |

### Line Weight Assignments for Details

| What Is Being Drawn | Line Style | Why |
|---------------------|-----------|-----|
| Exterior face of wall | Wide Lines | Primary cut element, heaviest weight |
| Interior face of wall | Medium Lines | Secondary cut |
| Studs / framing in section | Medium Lines | Structural cut element |
| GWB / finish layers | Thin Lines | Thin material, light weight |
| Sheathing | Thin Lines | Thin material |
| Insulation batting | Thin Lines | Pattern, not a cut edge |
| Concrete outline | Wide Lines | Heavy structural material |
| Rebar circles | Medium Lines | Important but not primary |
| Flashing | Thin Lines | Thin metal, detail |
| Grade line | Wide Lines | Important reference |
| Dimension lines | Thin Lines | Annotation |
| Leader lines | Thin Lines | Annotation |
| Break lines | Thin Lines | Symbolic |
| Hidden elements | Hidden (dashed) | Behind other elements |
| Membranes/barriers | Hidden or Dash | Zero-thickness layer |

### Querying and Creating Line Styles

```json
// Get all available line styles
{"method": "getLineStyles", "params": {}}

// Create a custom line style
{
  "method": "createLineStyle",
  "params": {
    "name": "Property Line",
    "weight": 5,
    "color": {"r": 0, "g": 0, "b": 0},
    "pattern": "Long Dash"
  }
}

// Modify existing line style
{
  "method": "modifyLineStyle",
  "params": {
    "lineStyleId": 12345,
    "weight": 3,
    "color": {"r": 255, "g": 0, "b": 0}
  }
}
```

### How Line Weights Scale with View Scale

Revit maps pen weights (1-16) to actual plotted thicknesses. The Object Styles and Line Weights dialogs define this mapping. Key insight:

- **Model line weights** (pen numbers) map to different thicknesses at different scales
- **Annotation line weights** remain constant regardless of scale
- At 1/4" = 1'-0" (scale 48), pen weight 1 = ~0.003" plotted; pen weight 5 = ~0.016"
- At 3" = 1'-0" (scale 4), all weights appear thicker because the view is zoomed in

### Line Patterns

| Pattern Name | Appearance | Use |
|-------------|-----------|-----|
| Solid | _____________ | Default, most elements |
| Dash | - - - - - - | Hidden lines |
| Center | ___ . ___ . | Centerlines |
| Dash Dot | ___ . ___ . | Property lines |
| Long Dash | ______ ______ | Beyond elements |
| Hidden | - - - - | Standard hidden |
| Dot | . . . . . . | Special cases |

---

## 8. ANNOTATION IN DETAILS

### Keynoting Strategy

Three types of keynotes, each with different behavior:

| Type | Attached To | Updates When | Best For |
|------|-------------|-------------|----------|
| **Element Keynote** | Specific element instance | Element type changes | Model elements visible in detail callouts |
| **Material Keynote** | Material of an element | Material assignment changes | Material-based labeling |
| **User Keynote** | Any location (click to place) | Never (manual) | Detail components, custom callouts, 2D elements |

### Critical Rule: Keynotes Require Components

**Keynotes cannot be attached to detail lines, filled regions, or detail groups.** They can only be attached to:
- Detail component instances
- Model elements
- Materials

This is the #1 reason to use detail components instead of loose lines -- keynote-ability.

### Placing Keynotes via API

```json
// Place a keynote on a detail component
{
  "method": "placeKeynote",
  "params": {
    "viewId": 12345,
    "elementId": 67890,
    "keynoteType": "Element",
    "location": {"x": 5.0, "y": 3.0, "z": 0},
    "hasLeader": true
  }
}

// Batch place keynotes with leaders
{
  "method": "batchPlaceKeynotesWithLeaders",
  "params": {
    "viewId": 12345,
    "keynotes": [
      {"elementId": 100, "location": {"x": 5.0, "y": 8.0, "z": 0}},
      {"elementId": 101, "location": {"x": 5.0, "y": 7.0, "z": 0}},
      {"elementId": 102, "location": {"x": 5.0, "y": 6.0, "z": 0}}
    ]
  }
}

// Load a keynote file
{"method": "loadKeynoteFile", "params": {"filePath": "C:\\path\\to\\keynotes.txt"}}

// Get all keynote entries
{"method": "getKeynoteEntries", "params": {}}
```

### Keynote File Format

Standard Revit keynote text file (tab-delimited):
```
01.00.00	DIVISION 01 - GENERAL
01.01.00	Site Work
01.01.01	Excavation and grading
08.00.00	DIVISION 08 - OPENINGS
08.11.00	Metal Doors and Frames
08.11.01	Hollow metal door frame, 16 ga.
```

### Keynote Legends

A keynote legend is a schedule-like table that lists all keynotes used on a sheet. Place it on the sheet for contractor reference.

```json
{"method": "createKeynoteSchedule", "params": {"viewName": "Keynote Legend"}}
```

### Text Notes in Details

```json
// Via batchDrawDetail
{
  "method": "batchDrawDetail",
  "params": {
    "viewId": 12345,
    "operations": [
      {"op": "text", "location": [5.0, 8.0], "text": "5/8\" TYPE X GWB", "typeName": "3/32\" Arial"}
    ]
  }
}
```

### Text Size Standards

| Text Purpose | Height | Revit Type Name Pattern |
|-------------|--------|------------------------|
| Regular notes | 3/32" | "3/32\" Arial" |
| Note titles/headers | 3/16" | "3/16\" Arial" |
| Subtitles | 1/8" | "1/8\" Arial" |
| Dimensions | 3/32" | (set in dimension type) |
| Drawing titles | 1/4" | "1/4\" Arial" |

### Dimension Placement

```json
// Dimension between detail lines
{
  "method": "dimensionDetailLines",
  "params": {
    "viewId": 12345,
    "lineIds": [100, 101, 102],
    "dimensionLineY": -0.5
  }
}
```

### Leader Best Practices

- Leaders at 30, 45, or 60 degree angles
- Never cross leaders over each other
- Group keynotes vertically on one side of the detail
- Leader endpoint touches the element being called out
- Arrowhead style: filled or open triangle (firm standard)

```json
// Place annotation with leader
{
  "method": "placeAnnotationWithLeader",
  "params": {
    "viewId": 12345,
    "elementId": 67890,
    "tagLocation": {"x": 5.0, "y": 3.0, "z": 0},
    "leaderEnd": {"x": 2.0, "y": 2.0, "z": 0}
  }
}
```

### Standard Abbreviations for Detail Notes

| Abbrev | Meaning | Abbrev | Meaning |
|--------|---------|--------|---------|
| UNO | Unless Noted Otherwise | O.C. | On Center |
| TYP. | Typical | SIM. | Similar |
| GWB | Gypsum Wall Board | CONT | Continuous |
| VIF | Verify In Field | NIC | Not In Contract |
| T.O. | Top Of | B.O. | Bottom Of |
| AFF | Above Finished Floor | CLR | Clear |
| MIN | Minimum | MAX | Maximum |
| NTS | Not To Scale | EQ | Equal |
| EA | Each | MTL | Metal |
| STL | Steel | CONC | Concrete |
| WD | Wood | FTG | Footing |
| PT | Pressure Treated | FND | Foundation |

---

## 9. DETAIL ORGANIZATION & LIBRARIES

### Firm Library Structure

Most firms organize details by CSI MasterFormat division:

```
Detail Library/
  03 - Concrete/
    03-01 Spread Footing.rvt
    03-02 Grade Beam.rvt
    03-03 Slab on Grade Edge.rvt
  04 - Masonry/
    04-01 CMU Wall Section.rvt
    04-02 CMU Control Joint.rvt
  05 - Metals/
    05-01 Steel Column Base Plate.rvt
  06 - Wood & Plastics/
    06-01 Wood Frame Wall Section.rvt
    06-02 Floor Framing at Bearing Wall.rvt
  07 - Thermal & Moisture/
    07-01 Parapet Detail.rvt
    07-02 Roof Edge.rvt
    07-03 Through-Wall Flashing.rvt
  08 - Openings/
    08-01 Window Head.rvt
    08-02 Window Sill.rvt
    08-03 Window Jamb.rvt
```

### Storage Approaches

| Approach | Pros | Cons |
|----------|------|------|
| **Separate .rvt files per detail** | Easy to browse, version independently | Many files to manage |
| **One .rvt per division** | Fewer files, related details together | Larger files, harder to find specific detail |
| **In project template (.rte)** | Available immediately in new projects | Template gets large, may include unused details |
| **JSON templates (MCP approach)** | AI-reproducible, version-controlled, parametric | Requires MCP bridge to render |

### MCP Detail Library System

The bridge includes a JSON-based detail library at `knowledge/details/`:

```json
// Browse the library
{"method": "getDetailLibrary", "params": {}}

// Get details in a category
{"method": "getDetailsInCategory", "params": {"category": "07-roof"}}

// Search by keyword
{"method": "searchDetailLibrary", "params": {"keyword": "parapet"}}

// Import a detail from library into the current document
{"method": "importDetailToDocument", "params": {"category": "07-roof", "detailName": "parapet-standard"}}
```

### Detail Numbering on Sheets

Standard format: `Detail# / Sheet#` (e.g., `3/A5.01`)

- Details on sheets are numbered sequentially per sheet: 1, 2, 3...
- Sheet number appears below the detail number in the reference bubble
- Cross-reference from plans/sections uses the same format

### Reusable Detail Workflow

1. Create detail in a drafting view (not a callout)
2. Name it descriptively with division prefix: `"07 - PARAPET DETAIL - METAL COPING"`
3. Add to firm's detail library (.rvt file or JSON template)
4. In new projects: import from library or copy from template
5. Reference using "Reference Other View" callouts from plans/sections

### Converting Between View Types

```json
// Convert a detail view (with model geometry) to a drafting view (pure 2D)
{
  "method": "convertDetailToDraftingView",
  "params": {"sourceViewId": 12345}
}

// Batch convert multiple detail views
{
  "method": "batchConvertDetailsToDraftingViews",
  "params": {"viewIds": [12345, 12346, 12347]}
}

// Intelligent trace (traces model geometry into 2D lines)
{
  "method": "intelligentTraceDetailToDraftingView",
  "params": {"sourceViewId": 12345}
}
```

---

## 10. API METHOD REFERENCE

### Complete Detail Methods (65 MCP Methods)

#### Drawing & Creation

| Method | Description | Key Parameters |
|--------|-------------|----------------|
| `createDetailLine` | Single detail line | viewId, startPoint, endPoint, lineStyleId |
| `createDetailArc` | Detail arc | viewId, center, radius, startAngle, endAngle |
| `createDetailPolyline` | Connected line segments | viewId, points[], closed |
| `createDetailLineInDraftingView` | Detail line in drafting view | viewId, x1, y1, x2, y2, lineStyle |
| `createFilledRegion` | Filled region from boundary | viewId, filledRegionTypeId, boundaryLoops |
| `createMaskingRegion` | White-out masking region | viewId, boundaryLoops |
| `placeDetailComponent` | By type ID | viewId, typeId, location |
| `placeDetailComponentByName` | By family+type name | viewId, familyName, typeName, location |
| `placeDetailComponentAdvanced` | With rotation/mirror | viewId, typeId, location, rotation |
| `placeRepeatingDetailComponent` | Repeating pattern | viewId, typeId, startPoint, endPoint |
| `addInsulation` | Insulation on line | viewId, lineId, width |
| `placeInsulationPattern` | Batting between bounds | viewId, leftX, rightX, bottomY, topY |
| `createBreakLine` | Break line symbol | viewId, location, rotation |
| `placeBreakLineAuto` | Auto-placed break line | viewId |
| `placeMarkerSymbol` | Annotation marker | viewId, symbolName, location |

#### Batch & Assembly Operations

| Method | Description | Key Parameters |
|--------|-------------|----------------|
| `batchDrawDetail` | Multiple ops in one transaction | viewId, operations[] (line/arc/filledRegion/text/detailComponent) |
| `drawLayerStack` | Auto-draw assembly layers | viewId, layers[], height, direction, addLabels |
| `drawAssemblyDetail` | Intelligent assembly section | viewId, typeId, height, studSpacing, addDimensions |
| `getTypeAssembly` | Read compound structure | typeId |
| `importSvgToDetail` | SVG to Revit elements | viewId, svg, targetWidthFeet |

#### Query & Inspection

| Method | Description |
|--------|-------------|
| `getDetailLineInfo` | Info about a detail line |
| `getDetailLinesInView` | All detail lines in view |
| `getFilledRegionInfo` | Info about a filled region |
| `getFilledRegionsInView` | All filled regions in view |
| `getFilledRegionTypes` | All filled region types |
| `getDetailComponentInfo` | Info about a component instance |
| `getDetailComponentsInView` | All components in view |
| `getDetailComponentTypes` | All component types |
| `getDetailComponentFamilies` | All component families |
| `getInsulationInfo` | Info about insulation element |
| `getLineStyles` | All line styles |
| `getBreakLineTypes` | All break line types |
| `getDetailGroupTypes` | All detail group types |
| `getDraftingViewBounds` | View bounding box |
| `getElementGraphicsOverrides` | Graphics overrides for element |

#### Modification & Management

| Method | Description |
|--------|-------------|
| `modifyDetailLine` | Change line endpoints/style |
| `modifyFilledRegionBoundary` | Change region boundary |
| `modifyLineStyle` | Change line style properties |
| `createLineStyle` | Create new line style |
| `overrideElementGraphics` | Override element display |
| `clearElementGraphicsOverrides` | Clear overrides |
| `deleteDetailElement` | Delete a detail element |
| `copyDetailElements` | Copy between views |
| `removeInsulation` | Remove insulation |

#### Groups & Library

| Method | Description |
|--------|-------------|
| `createDetailGroup` | Group elements |
| `placeDetailGroup` | Place group instance |
| `createDetailComponentLibrary` | Capture view as library |
| `extractAndReplaceFilledRegions` | Extract/replace regions |
| `getDetailLibrary` | Browse JSON library |
| `getDetailsInCategory` | Category contents |
| `searchDetailLibrary` | Search by keyword |
| `importDetailToDocument` | Import from library |
| `loadDetailComponentFamily` | Load .rfa from disk |
| `searchLocalDetailFamilies` | Find .rfa on disk |
| `activateDetailComponentType` | Activate type for placement |
| `loadAutodeskFamilyAutomated` | Cloud family loading |

#### View Conversion

| Method | Description |
|--------|-------------|
| `convertDetailToDraftingView` | Detail to drafting |
| `batchConvertDetailsToDraftingViews` | Batch convert |
| `traceDetailToDraftingView` | Trace model geometry to 2D |
| `intelligentTraceDetailToDraftingView` | Smart trace with material detection |

#### Annotation Methods (from AnnotationMethods.cs)

| Method | Description |
|--------|-------------|
| `placeKeynote` | Place keynote tag |
| `loadKeynoteFile` | Load keynote database |
| `getKeynoteEntries` | List all keynotes |
| `getKeynotesInView` | Keynotes in view |
| `batchPlaceKeynotesWithLeaders` | Batch keynote placement |
| `createKeynoteSchedule` | Keynote legend |
| `placeAnnotationSymbol` | Annotation symbol |
| `createCallout` | Callout view |
| `placeAnnotationWithLeader` | Tag with leader |
| `setLeaderEndpoint` | Adjust leader endpoint |
| `addAnnotationLeader` | Add leader to tag |

---

## 11. END-TO-END WORKFLOW EXAMPLES

### Example A: Standard Wood Frame Wall Section (from scratch)

```
Step 1: Create drafting view
  createView(type="Drafting", viewName="TYPICAL WOOD FRAME WALL SECTION", scale=16)

Step 2: Query model for actual wall assembly (if exists)
  getTypeAssembly(typeId=<wall_type_id>)

Step 3: Draw automatically from assembly
  drawAssemblyDetail(viewId=<new_view>, typeId=<wall_type_id>, height=2.5, addDimensions=true, addLabels=true)

  This automatically:
  - Draws GWB layers with "Sand" fill
  - Draws wood studs at 16" O.C. with medium lines
  - Draws batt insulation with wavy pattern
  - Draws sheathing with plywood fill
  - Draws siding
  - Adds break lines at top and bottom
  - Adds dimension string
  - Adds material labels with leaders

Step 4: Review
  captureViewportToBase64(viewId=<new_view>)

Step 5: Fix/embellish
  batchDrawDetail(viewId=<new_view>, operations=[
    // Add whatever drawAssemblyDetail missed
  ])
```

### Example B: Manual Detail with batchDrawDetail

```json
{
  "method": "batchDrawDetail",
  "params": {
    "viewId": 12345,
    "operations": [
      // Concrete footing (filled region)
      {"op": "filledRegion", "typeName": "Concrete", "points": [[0,-1],[3,-1],[3,0],[0,0]]},

      // Slab on grade
      {"op": "filledRegion", "typeName": "Concrete", "points": [[0,0],[6,0],[6,0.333],[0,0.333]]},

      // Gravel base
      {"op": "filledRegion", "typeName": "Sand", "points": [[0,-0.333],[6,-0.333],[6,0],[0,0]]},

      // Earth fill
      {"op": "filledRegion", "typeName": "Earth", "points": [[-1,-2],[7,-2],[7,-0.333],[-1,-0.333]]},

      // Grade line (wide)
      {"op": "line", "start": [-1, 0], "end": [7, 0], "lineStyle": "Wide Lines"},

      // Rebar in footing
      {"op": "detailComponent", "familyName": "Reinf Bar Section", "typeName": "#5", "location": [0.5, -0.5]},
      {"op": "detailComponent", "familyName": "Reinf Bar Section", "typeName": "#5", "location": [2.5, -0.5]},

      // Anchor bolt
      {"op": "detailComponent", "familyName": "Anchor Bolt", "typeName": "1/2\"", "location": [1.5, -0.25]},

      // Labels
      {"op": "text", "location": [7.5, -0.5], "text": "CONT. SPREAD FOOTING\n24\" x 12\" MIN."},
      {"op": "text", "location": [7.5, 0.17], "text": "4\" CONC. SLAB ON GRADE\nOVER 4\" COMPACTED GRAVEL"}
    ]
  }
}
```

### Example C: drawLayerStack for Quick Assembly Section

```json
{
  "method": "drawLayerStack",
  "params": {
    "viewId": 12345,
    "originX": 0,
    "originY": 0,
    "height": 8.0,
    "direction": "left-to-right",
    "addLabels": true,
    "addDimensions": true,
    "layers": [
      {"name": "5/8\" GWB", "thickness": 0.052, "hatch": "Sand", "lineWeight": "Thin Lines"},
      {"name": "Vapor Barrier", "thickness": 0, "linePattern": "Dash"},
      {"name": "2x6 Stud @ 16\" O.C.", "thickness": 0.458, "hatch": "Wood - Section", "lineWeight": "Medium Lines"},
      {"name": "R-21 Batt Insulation", "thickness": 0.458, "hatch": "Insulation", "overlay": true},
      {"name": "7/16\" OSB Sheathing", "thickness": 0.036, "hatch": "Plywood", "lineWeight": "Thin Lines"},
      {"name": "Weather Barrier", "thickness": 0, "linePattern": "Dash"},
      {"name": "1\" Air Gap", "thickness": 0.083, "lineWeight": "Thin Lines"},
      {"name": "Brick Veneer", "thickness": 0.292, "hatch": "Brick", "lineWeight": "Wide Lines"}
    ]
  }
}
```

### Example D: Detail Replication (Extract + Reproduce)

See `detail-replication-method.md` for the complete proven workflow:
1. `analyzeDetailView` -- get element counts and crop bounds
2. `getDetailLinesInViewVA` -- extract all line coordinates
3. `getTextNotePositions` -- extract text and leaders
4. `getDetailComponentsInViewVA` -- extract component locations
5. `getFilledRegionsInView` -- extract regions
6. Normalize coordinates (subtract crop region min)
7. Replicate in new view with offset coordinates

---

## 12. COORDINATE SYSTEM REFERENCE

### Drafting View Coordinates

| Property | Value |
|----------|-------|
| Origin | (0, 0, 0) -- or wherever you start drawing |
| X axis | Horizontal (right = positive) |
| Y axis | Vertical (up = positive) |
| Z axis | Always 0 (2D view) |
| Units | **Feet** (Revit internal units) |

### Section/Detail View Coordinates

| Property | Value |
|----------|-------|
| Origin | Project origin |
| X axis | Horizontal in section plane |
| Y axis | **Always 0** (perpendicular to view -- collapsed in 2D) |
| Z axis | **Vertical** (up = positive) |

**Critical**: In section views, the vertical coordinate is Z, not Y. In drafting views, it is Y. The `createDetailLine` method accepts points with x/y/z, so:
- Drafting view: use `{"x": 1.0, "y": 2.0, "z": 0}`
- Section view: use `{"x": 1.0, "y": 0, "z": 2.0}`

### Common Dimensions in Feet

| Dimension | Inches | Feet | Notes |
|-----------|--------|------|-------|
| 2x4 actual | 3.5" | 0.2917' | |
| 2x6 actual | 5.5" | 0.4583' | |
| 2x8 actual | 7.25" | 0.6042' | |
| 2x10 actual | 9.25" | 0.7708' | |
| 2x12 actual | 11.25" | 0.9375' | |
| 1/2" GWB | 0.5" | 0.0417' | |
| 5/8" GWB | 0.625" | 0.0521' | |
| 7/16" OSB | 0.4375" | 0.0365' | |
| 3/4" plywood | 0.75" | 0.0625' | |
| 8" CMU | 7.625" | 0.6354' | Nominal 8", actual 7-5/8" |
| 4" slab | 4" | 0.3333' | |
| 6" slab | 6" | 0.5' | |
| 3-5/8" metal stud | 3.625" | 0.3021' | |
| 6" metal stud | 6" | 0.5' | |
| Standard brick | 3.625" wide x 2.25" tall | 0.302' x 0.1875' | With 3/8" mortar: 2.667" course |
| 16" O.C. spacing | 16" | 1.3333' | |
| 24" O.C. spacing | 24" | 2.0' | |

### Scale Reference

| Scale | Ratio | Common Use |
|-------|-------|-----------|
| 1/8" = 1'-0" | 96 | Building plans |
| 1/4" = 1'-0" | 48 | Floor plans, elevations |
| 3/8" = 1'-0" | 32 | Enlarged plans |
| 1/2" = 1'-0" | 24 | Building sections |
| 3/4" = 1'-0" | 16 | Wall sections |
| 1" = 1'-0" | 12 | Cabinet details |
| 1-1/2" = 1'-0" | 8 | Window/door details |
| 3" = 1'-0" | 4 | Small details, flashings |
| 6" = 1'-0" | 2 | Very small details |
| Full Size | 1 | Hardware, profiles |

---

## SOURCES

- [Autodesk University: Creating Intelligent Details in Revit](https://medium.com/autodesk-university/creating-intelligent-details-in-revit-7a3cd1c9403d)
- [Detailing in Revit: Everything You Need to Know (LazyBim)](https://lazybim.com/detailing-in-revit/)
- [Detailing & Documenting in Revit (Parametric Monkey)](https://parametricmonkey.com/2017/01/14/detailing-in-revit/)
- [Detailing with Revit 101 (IMAGINiT)](https://resources.imaginit.com/support-blog/detailing-with-revit-101-2)
- [Detail Revit Drawings: Quality and Beauty (Project by n.)](https://projectbyn.com/revit-drawings/)
- [Revit Drafting: Complete Guide to 2D Detailing (BIMProus)](https://www.bimprous.com/revit-drafting-guide-to-2d-detailing/)
- [Creating Detail Callouts for Drafting Views (Noble Desktop)](https://www.nobledesktop.com/learn/revit/creating-detail-callouts-for-drafting-views)
- [Drafting View vs Detail View (Autodesk Forums)](https://forums.autodesk.com/t5/revit-architecture-forum/what-really-is-the-difference-between-a-drafting-view-and-a/td-p/8496853)
- [Revit Keynotes Best Practices (IntegratedBIM)](https://integratedbim.com/revit-keynotes-best-practices/)
- [Detail Components and Keynotes (ARKANCE)](https://www.arkance.us/blog/detail-components-detailing-with-keynotes)
- [Standardize Documentation with Detail Components (Novedge)](https://novedge.com/blogs/design-news/revit-tip-standardize-revit-documentation-with-detail-components)
- [Revit Line Weights (Engipedia)](https://www.engipedia.com/revit-line-weights/)
- [Custom Line Styles in Revit (BIM Associates)](https://www.bimassociates.com/blog/custom-line-styles-revit/)
- [Understanding Line Weight Basics (AUGI)](https://www.augi.com/articles/detail/understanding-line-weight-basics)
- [Line Weights in Revit (BIM Pure)](https://www.bimpure.com/blog/13-tips-to-understand-line-weights-in-revit)
- [Revit Fill Patterns (BIM Chapters)](https://bimchapters.blogspot.com/2017/06/revit-fill-patterns.html)
- [FilledRegion.Create (Revit API Docs)](https://www.revitapidocs.com/2016/ca304caa-7f95-4638-67d9-a138be609b9f.htm)
- [FilledRegion API (TwentyTwo)](https://twentytwo.space/2021/01/30/revit-api-filledregion/)
- [Managing a Detail Library (Revit Forum)](https://www.revitforum.org/forum/revit-architecture-forum-rac/architecture-and-general-revit-questions/25156-managing-an-office-revit-detail-library)
- [How to Create Standard Detail Libraries (Structure Drafting)](https://structuredrafting.com/how-to-create-standard-detail-libraries-in-revit/)
- [Organizing Details in Revit (AEC Tech Talk)](https://aectechtalk.wordpress.com/2021/05/22/organizing-details-in-revit/)
- [Insulation Tool for Batt Detailing (Noble Desktop)](https://www.nobledesktop.com/learn/revit/insulation-tool-review-for-bat-insulation-detailing-in-revit)
- [CurvedInsulation Add-in (GitHub)](https://github.com/mikaeldeity/CurvedInsulation)
- [Insert a Repeating Detail (Autodesk Help)](https://knowledge.autodesk.com/support/revit-products/learn-explore/caas/CloudHelp/cloudhelp/2015/ENU/Revit-DocumentPresent/files/GUID-2F20E583-F92A-4AB7-99F1-BE50BF2B8FF1-htm.html)
- [About Drafting Views (Autodesk Help)](https://help.autodesk.com/cloudhelp/2025/ENU/Revit-DocumentPresent/files/GUID-7B76A2EA-F1F4-484B-AA96-2E2E53B56E49.htm)
