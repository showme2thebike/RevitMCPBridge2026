# Detail Drawing Mastery: The Definitive Guide

> A comprehensive reference for producing architect-quality 2D construction details.
> Written so an AI can draw a professional detail without human guidance.

---

## Table of Contents

1. [Line Weight Standards](#1-line-weight-standards)
2. [Line Types and Their Meaning](#2-line-types-and-their-meaning)
3. [Scale Conventions](#3-scale-conventions)
4. [Material Representation (Hatching)](#4-material-representation-hatching)
5. [Material Symbols for Accessories](#5-material-symbols-for-accessories)
6. [Dimensioning Rules](#6-dimensioning-rules)
7. [Notes, Callouts, and Keynotes](#7-notes-callouts-and-keynotes)
8. [Detail Numbering and Cross-Referencing](#8-detail-numbering-and-cross-referencing)
9. [Detail Sheet Organization](#9-detail-sheet-organization)
10. [Visual Hierarchy and Drawing Order](#10-visual-hierarchy-and-drawing-order)
11. [Required Details for Permits](#11-required-details-for-permits)
12. [Common Mistakes and How to Avoid Them](#12-common-mistakes-and-how-to-avoid-them)
13. [Revit-Specific Detailing Strategy](#13-revit-specific-detailing-strategy)
14. [Professional Quality Checklist](#14-professional-quality-checklist)

---

## 1. Line Weight Standards

Line weight is the single most important graphic tool for communicating spatial depth and hierarchy in a 2D detail. A detail drawn with uniform line weight is unreadable.

### The Four-Weight Minimum System

Every detail drawing MUST use at least four distinct line weights:

| Weight | Name | mm | Purpose |
|--------|------|----|---------|
| **Wide** | Profile / Cut | 0.50-0.70 mm | Elements CUT by the section plane (walls, floors, structural members) |
| **Medium** | Object / Projection | 0.35 mm | Visible edges of objects BEYOND the cut plane |
| **Thin** | Secondary | 0.18-0.25 mm | Minor visible edges, material transitions, secondary elements |
| **Fine** | Annotation | 0.09-0.15 mm | Hatching, dimensions, leaders, text, break lines |

### Historical Pen Set (Rotring Standard)

Traditional architectural pen sets: 0.25 mm, 0.35 mm, 0.50 mm, 1.0 mm. These four sizes remain the conceptual basis for digital line weights.

### Weight Assignment by Element Type

**In Section/Detail Views:**

| Element | Weight | Rationale |
|---------|--------|-----------|
| Ground line (grade) | Widest (0.70 mm) | Anchors the entire drawing |
| Primary structure cut (walls, slabs, beams, columns) | Wide (0.50 mm) | Most important spatial information |
| Secondary cut elements (doors, windows, finishes) | Medium-Wide (0.35-0.40 mm) | Important but subordinate to structure |
| Visible edges beyond cut (furniture, fixtures, equipment) | Medium (0.25-0.30 mm) | Context elements |
| Material patterns and hatching | Fine (0.09-0.13 mm) | Must not compete with geometry |
| Dimension lines, leaders, text | Fine (0.13-0.18 mm) | Annotation layer |

**Key Rule:** CUT elements are ALWAYS heavier than elements seen in projection. This is the fundamental principle of section drawing.

### Digital Pen Table Settings (Recommended)

```
Profile lines:    0.53 mm  (color: varies by layer)
Heavy lines:      0.40 mm  (structure, primary outlines)
Medium lines:     0.30 mm  (object lines beyond cut)
Light lines:      0.15 mm  (secondary edges, minor elements)
Hatching:         0.09 mm  (material patterns only)
Dimensions:       0.13 mm  (annotation)
```

---

## 2. Line Types and Their Meaning

### Standard Architectural Line Types

| Line Type | Appearance | Use |
|-----------|------------|-----|
| **Continuous (solid)** | ____________ | Visible edges, outlines, cut lines |
| **Dashed (hidden)** | _ _ _ _ _ _ | Hidden edges behind surfaces, elements above cut plane in plan |
| **Center line** | ___._.__._ | Axes of symmetry, center of columns, center of openings |
| **Chain thick** | ===.=.===.= | Cutting planes (section cut indicators) |
| **Break line (short)** | ~~/\/\~~ | Short break in continuous element |
| **Break line (long)** | ——/\/\—— | Long break showing omitted length |
| **Phantom** | __..__..__ | Alternate positions, future work, adjacent construction |
| **Property/boundary** | _.._.._.._.. | Property lines, setback lines |

### Break Lines

- **Short break:** Thick, wavy freehand line for rectangular sections
- **Long break:** Full ruled lines with freehand zigzag in the middle
- **Cylindrical break:** S-curve showing cut section of cylindrical objects (pipes, columns)
- Break lines indicate that part of the element has been omitted to fit the detail on the sheet while preserving clarity

### Section Cut Indicators

The section cut line on a plan view consists of:
- A chain-thick line running through the plan showing where the cut occurs
- Arrows at each end showing the viewing direction
- A circle (bubble) at each end containing:
  - Top half: Section letter or number (e.g., "A" or "1")
  - Bottom half: Sheet number where the section drawing is found (e.g., "A5.01")

---

## 3. Scale Conventions

### Standard Scales by Detail Type

| Drawing Type | Typical Scale | Ratio | Use Case |
|-------------|---------------|-------|----------|
| **Site plan** | 1" = 20'-0" to 1" = 40'-0" | 1:240 to 1:480 | Overall site context |
| **Floor plan** | 1/4" = 1'-0" | 1:48 | Standard plan views |
| **Building section** | 1/4" = 1'-0" | 1:48 | Full building cuts |
| **Wall section** | 1/2" = 1'-0" to 3/4" = 1'-0" | 1:24 to 1:16 | Wall assemblies in context |
| **Enlarged plan** | 1/2" = 1'-0" | 1:24 | Bathroom, stair, kitchen plans |
| **Standard detail** | 1-1/2" = 1'-0" | 1:8 | Most construction details |
| **Large detail** | 3" = 1'-0" | 1:4 | Complex assemblies, connections |
| **Full size** | 12" = 1'-0" (1:1) | 1:1 | Custom profiles, moldings, hardware |

### Scale Selection Rules

1. **Show every layer of the assembly.** If layers are too thin to distinguish at the chosen scale, increase the scale.
2. **Standard details** (wall sections, roof edges, foundation footings): Use 1-1/2" = 1'-0" (1:8) as the default.
3. **Connection details** (flashing, sealant joints, trim profiles): Use 3" = 1'-0" (1:4) or full size.
4. **Multiple scales on one sheet are acceptable** and common. Always note the scale under each detail title.
5. A 3" = 1'-0" drawing is exactly TWICE as large as a 1-1/2" = 1'-0" drawing.
6. **NEVER mix metric and imperial scales** on the same project.

### Scale Relationships

```
1/8" = 1'-0"   →  1:96    (small-scale plans)
1/4" = 1'-0"   →  1:48    (standard plans)
3/8" = 1'-0"   →  1:32    (enlarged plans)
1/2" = 1'-0"   →  1:24    (wall sections)
3/4" = 1'-0"   →  1:16    (large wall sections)
1" = 1'-0"     →  1:12    (small details)
1-1/2" = 1'-0" →  1:8     (standard details)
3" = 1'-0"     →  1:4     (large details)
6" = 1'-0"     →  1:2     (half size)
12" = 1'-0"    →  1:1     (full size)
```

---

## 4. Material Representation (Hatching)

### Master Hatch Pattern Reference

Every material has a standardized graphic representation when cut in section. These patterns are defined in AIA Architectural Graphic Standards and BS 1192.

#### Structural Materials

| Material | Pattern Description | Angle | Notes |
|----------|-------------------|-------|-------|
| **Cast-in-place concrete** | Stippled dots (random small dots/speckles) | N/A | Dense random dot pattern; sometimes shown with triangular aggregate shapes |
| **Precast concrete** | Stippled dots with diagonal line | 45° line + dots | Diagonal line distinguishes from cast-in-place |
| **Concrete masonry (CMU)** | Diagonal lines with triangular shapes | 45° | Shows aggregate; cells drawn explicitly in detail views |
| **Brick (common)** | Diagonal lines (close spacing) | 45° | Tighter spacing than CMU |
| **Stone (cut)** | Diagonal lines (wide spacing) | 45° | Wider spacing than brick |
| **Stone (rubble)** | Irregular random shapes | N/A | Rough natural shapes |

#### Wood

| Material | Pattern Description | Notes |
|----------|-------------------|-------|
| **Solid wood (sawn lumber)** | Irregular curved lines representing grain | Growth rings visible in end grain; parallel grain lines in long grain |
| **Plywood / laminated wood** | Alternating parallel lines with grain pattern | Shows lamination layers; lines alternate direction |
| **Particle board / OSB** | Dense random small dots | Denser than concrete stipple |
| **Finish wood (hardwood)** | Tight grain lines | More refined than framing lumber |

#### Metals

| Material | Pattern Description | Angle | Notes |
|----------|-------------------|-------|-------|
| **Steel** | Evenly spaced diagonal lines | 45° | Standard section lining; closely spaced |
| **Aluminum** | Evenly spaced diagonal lines | 45° | Similar to steel but may use alternate angle |
| **Metal deck** | Drawn to actual profile shape | N/A | Corrugated profile with actual dimensions |
| **Steel plate** | Solid fill (very thin sections) or diagonal hatch | 45° | Thin sections may be shown solid black |

#### Insulation

| Material | Pattern Description | Notes |
|----------|-------------------|-------|
| **Batt / blanket insulation** | Wavy sinusoidal lines (cloud-like zigzag) | The classic "cotton candy" pattern; fills the cavity |
| **Rigid insulation (XPS/EPS/polyiso)** | Small triangular/crosshatch pattern at 90° | Geometric, regular pattern distinguishes from batt |
| **Loose fill insulation** | Random dots (similar to earth but lighter) | Less dense than concrete stipple |
| **Spray foam** | Irregular wavy pattern | Similar to batt but more irregular |

#### Finish Materials

| Material | Pattern Description | Notes |
|----------|-------------------|-------|
| **Gypsum board (drywall)** | Thin rectangle drawn to scale | Typically shown as a simple thin line at 1/2" or 5/8" thickness; may use light stipple |
| **Plaster** | Fine stipple with sand texture | Denser and more uniform than concrete stipple |
| **Ceramic tile** | Grid pattern showing tile layout | Square or rectangular grid with grout lines |

#### Earth and Aggregate

| Material | Pattern Description | Notes |
|----------|-------------------|-------|
| **Earth (undisturbed)** | Irregular short dashes with scattered dots | Random organic-looking pattern |
| **Gravel / crushed stone** | Irregular small circles/pebble shapes | Round or angular shapes depending on type |
| **Sand** | Fine dots (very dense stipple) | Finest of the dot patterns |
| **Compacted fill** | Earth pattern with parallel lines | Shows compaction effort |

#### Glass

| Material | Pattern Description | Notes |
|----------|-------------------|-------|
| **Glass** | Solid fill or single diagonal line through section | Thin sections shown as solid black line |

### Hatching Rules

1. **Spacing must be consistent** within each material region.
2. **Adjacent materials must use different patterns** — never the same hatch on both sides of a boundary.
3. **Hatch lines should be at 45° to the principal edges** of the cut element by default.
4. **Hatch line weight must be FINE** (0.09 mm) — hatching should never compete with geometry.
5. **Scale hatching appropriately.** Patterns that are too dense become solid black when printed; too sparse and they lose meaning.
6. **At minimum, always show hatches** for: batt insulation, plywood/OSB, solid wood, concrete, gravel, and earth.

---

## 5. Material Symbols for Accessories

### Waterproofing and Moisture Protection

| Element | Symbol/Representation | Drawing Convention |
|---------|----------------------|-------------------|
| **Waterproofing membrane** | Heavy solid line (thicker than normal) with label | Continuous bold line on surface receiving waterproofing; labeled "WPM" or by product name |
| **Vapor barrier/retarder** | Dashed or dash-dot line | Shown as a distinct line within the assembly; labeled "VB" or "VR" with perm rating |
| **Air barrier** | Continuous line (lighter than waterproofing) | Continuous line shown at the plane of airtightness; labeled "AB" |
| **Damp proofing** | Short diagonal hatching on surface | Applied to below-grade surfaces not subject to hydrostatic pressure |
| **Self-adhering membrane** | Thick line with "peel-and-stick" note | Label specifies mil thickness (e.g., "40 mil self-adhering membrane") |

### Flashing

| Element | Symbol/Representation | Drawing Convention |
|---------|----------------------|-------------------|
| **Through-wall flashing** | Bold bent line following geometry | Show full extent including end dams, drip edges; label material (copper, stainless, composite) |
| **Step flashing** | Series of overlapping rectangles | Show shingle-style overlap at wall-roof intersections |
| **Counter flashing** | Bold line with hook into reglet | Show reglet cut in masonry or concrete |
| **Drip edge** | Small bent profile at edge | Show actual profile shape; indicate fastening |
| **Kick-out flashing** | Curved diverter at base of roof-wall | Show full profile directing water to gutter |

### Sealants and Joints

| Element | Symbol/Representation | Drawing Convention |
|---------|----------------------|-------------------|
| **Sealant** | Small filled shape (typically trapezoid) at joint | Show with backer rod behind; label type and depth-to-width ratio |
| **Backer rod** | Small circle in joint | Circle diameter matches rod size; drawn behind sealant |
| **Bond breaker tape** | Thin line at back of joint | Prevents three-sided adhesion |
| **Expansion joint** | Gap with zigzag or compressible fill | Show gap width and filler material |
| **Control joint** | Thin line or tooled groove | Depth = 1/4 of slab/wall thickness minimum |

### Weep System

| Element | Symbol/Representation | Drawing Convention |
|---------|----------------------|-------------------|
| **Weep holes** | Small circles at base of masonry | Space at maximum 24" o.c.; shown at first course above flashing |
| **Weep tube** | Small tube/circle with leader | Label diameter and spacing |
| **Weep wick** | Zigzag line in mortar joint | Cotton or synthetic rope shown protruding from joint |

### Fasteners and Anchors

| Element | Symbol/Representation | Drawing Convention |
|---------|----------------------|-------------------|
| **Bolts** | Circle with cross (plan) or profile (section) | Show size, spacing, and embedment depth |
| **Screws** | Small triangle point (section) | Show head type (flat, pan, hex) |
| **Nails** | Thin line with small head | Show size (e.g., 10d, 16d) and spacing |
| **Anchors (masonry ties)** | Bent wire shape in cavity | Show type (corrugated, adjustable, seismic) |
| **Simpson ties/hangers** | Draw actual profile shape | Reference model number (e.g., "LUS26") |

---

## 6. Dimensioning Rules

### Placement Hierarchy

Dimensions are organized in tiers, working from the object outward:

```
OBJECT (the detail)
   ↕ 1/2" gap (minimum clearance from object to first dimension line)
   ├── Tier 1: Component dimensions (smallest, most specific)
   ↕ 3/8" gap between dimension tiers
   ├── Tier 2: Intermediate dimensions (wall-to-wall, opening locations)
   ↕ 3/8" gap
   └── Tier 3: Overall dimension (full extent of detail)
```

### Dimension Placement Rules

1. **First dimension line: 1/2" minimum** from the object being dimensioned.
2. **Subsequent tiers: 3/8" spacing** between parallel dimension lines.
3. **Outermost = overall dimension.** Work inward to smaller dimensions.
4. **Exterior dimensions go on the exterior** of the building in plan. Interior dimensions go on the interior.
5. **Never duplicate a dimension** that can be derived by adding sub-dimensions.
6. **Dimension to nominal sizes** for framing lumber (e.g., 2x6 is dimensioned as 5-1/2" actual).
7. **Dimension to face of finish** or face of structure — be explicit about which.
8. **String dimensions should add up.** If sub-dimensions don't total the overall, there is an error.

### What to Dimension in Details

| Always Dimension | Never Dimension |
|-----------------|-----------------|
| Overall height/width of assembly | Obvious standard sizes (don't dimension a door handle height) |
| Each material layer thickness | Hatching or patterns |
| Critical clearances and air spaces | Decorative elements (unless custom) |
| Structural member sizes | Items covered by specifications alone |
| Slopes (in inches per foot: e.g., "1/4" per 1'-0"") | Duplicate of information shown elsewhere |
| Embedment depths | |
| Edge distances for fasteners | |

### Dimension Text Standards

- **Minimum text height:** 3/32" (2.5 mm) on printed sheet
- **Font:** Sans-serif (Arial, Helvetica, or similar) per NCS recommendation
- **Fractions:** Stacked or diagonal (1/2, not .5)
- **Feet and inches:** Use dash separator: 5'-4 1/2" (not 5'4.5")
- **Text placed ABOVE the dimension line** (architectural convention) or centered in a break

### Slope Notation

- Roofs: Show as triangle with rise:run ratio (e.g., 4:12 or 1/4:12)
- Drainage: Show as inches per foot (e.g., "1/4" / 1'-0" MIN")
- Always show slope direction with an arrow

---

## 7. Notes, Callouts, and Keynotes

### Note Placement Rules

1. **Notes stay outside the detail** — minimum 1/2" clear from the drawing.
2. **1/8" gap** between note text and leader line.
3. **Left-justify all notes** in a consistent column when possible.
4. **Group related notes** — don't scatter them randomly around the detail.
5. **Notes read left-to-right, top-to-bottom** — follow the natural reading order.
6. **Never place notes inside dense hatching** or complex geometry.

### Leader Line Rules

1. Leaders are drawn as **straight lines at an angle** (never horizontal, never vertical).
2. Each leader has a **horizontal "elbow"** (landing) that aligns with the first or last letter of the note.
3. Leaders end with:
   - **Arrowhead** when pointing to an edge or line
   - **Dot (1.5 mm diameter)** when pointing to a surface or area
4. Leaders **must not cross each other** — rearrange notes to avoid crossings.
5. Leaders **should not be excessively long** — keep the note near the element.
6. Leader line weight: **Fine** (0.13 mm), same as dimensions.

### Keynotes vs. Text Notes

| Feature | Keynotes | Text Notes |
|---------|----------|------------|
| **Format** | Alphanumeric code with legend (e.g., "07.A") | Full text at the leader (e.g., "SELF-ADHERING MEMBRANE") |
| **Use when** | Many similar callouts; busy/dense details | Few callouts; simple details; clarity is paramount |
| **Advantage** | Reduces clutter; single reference point; consistent | Immediately readable; no legend lookup needed |
| **Standard** | NCS/AIA compliant; CSI-based numbering | Universally understood |
| **Legend location** | On same sheet or on a dedicated keynote sheet | N/A |

### Keynote Numbering (CSI-Based)

Keynotes are typically organized by CSI MasterFormat division:

```
03.A  = Cast-in-place concrete (Division 03)
04.A  = CMU block (Division 04)
05.A  = Structural steel (Division 05)
06.A  = Wood framing (Division 06)
07.A  = Waterproofing membrane (Division 07)
07.B  = Batt insulation (Division 07)
07.C  = Rigid insulation (Division 07)
07.D  = Metal flashing (Division 07)
07.E  = Sealant and backer rod (Division 07)
08.A  = Window assembly (Division 08)
09.A  = Gypsum board (Division 09)
09.B  = Ceramic tile (Division 09)
```

### Note Content Rules

1. **Be specific:** "1/2" GYP. BD. ON MTL. STUDS @ 16" O.C." not "DRYWALL ON STUDS"
2. **Use standard abbreviations** consistently (see abbreviation list below)
3. **Include material, size, and spacing** where applicable
4. **Reference specifications** for complex assemblies: "PER SPEC SECTION 07 62 00"
5. **Avoid vague notes** like "WATERPROOF AS REQUIRED" — specify the product, thickness, and method

### Standard Abbreviations

```
ABV     Above                  GYP     Gypsum
ADJ     Adjacent               HDR     Header
ALIGN   Alignment              HT      Height
APPROX  Approximate            INSUL   Insulation
BD      Board                  INT     Interior
BLK     Block/Blocking         JT      Joint
BLDG    Building               MAX     Maximum
BM      Beam                   MBR     Membrane
BOT     Bottom                 MECH    Mechanical
CLG     Ceiling                MIN     Minimum
CLR     Clear                  MTL     Metal
CMU     Concrete Masonry Unit  NOM     Nominal
COL     Column                 NTS     Not to Scale
CONC    Concrete               O.C.    On Center
CONT    Continuous             PLYWD   Plywood
DET     Detail                 PT      Point / Pressure Treated
DIA     Diameter               R       Radius
DIM     Dimension              REINF   Reinforcement / Reinforced
DN      Down                   REQ'D   Required
EA      Each                   SHT     Sheet
EL      Elevation              SIM     Similar
EQ      Equal                  SQ      Square
EXIST   Existing               STD     Standard
EXT     Exterior               STL     Steel
FDN     Foundation             STRUCT  Structural
FIN     Finish                 T&G     Tongue and Groove
FLR     Floor                  TYP     Typical
FTG     Footing                UNO     Unless Noted Otherwise
GA      Gauge                  VB      Vapor Barrier
GI      Galvanized Iron        W/      With
GL      Glass / Glazing        WD      Wood
GRD     Grade / Ground         WP      Waterproof / Waterproofing
```

---

## 8. Detail Numbering and Cross-Referencing

### Detail Marker Format

Every detail has a **detail bubble** (circle or oval) containing:

```
  ┌───────┐
  │   3   │  ← Detail number (sequential within the sheet)
  │───────│
  │ A5.01 │  ← Sheet number where the detail is drawn
  └───────┘
```

- **Top half:** Detail number or letter (1, 2, 3... or A, B, C...)
- **Bottom half:** Sheet number where the detail can be found

### Section Marker Format

```
        ↑  (arrow showing viewing direction)
  ┌───────┐
  │   A   │  ← Section letter
  │───────│
  │ A3.01 │  ← Sheet where section is drawn
  └───────┘
  (line through plan showing cut location)
```

### Elevation Marker Format

A circle with four triangular arrows pointing outward (or selectively):

```
       1/A2.01
         ↑
    ←  ──●──  →
    4/A2.02  2/A2.01
         ↓
       3/A2.02
```

Each arrow's number/sheet indicates which elevation is shown and where to find it.

### Sheet Numbering Convention (NCS/AIA)

```
Format: [Discipline Letter][Sheet Type Digit][Sequence Number]

Discipline Letters:
  G = General          A = Architectural
  S = Structural       M = Mechanical
  E = Electrical       P = Plumbing
  L = Landscape        C = Civil

Sheet Type Digits (Architectural):
  A0.xx = General (cover, code analysis, abbreviations, symbols)
  A1.xx = Plans (floor plans, roof plans)
  A2.xx = Elevations
  A3.xx = Sections
  A4.xx = Large-scale plans (enlarged plans)
  A5.xx = Details
  A6.xx = Schedules and diagrams
  A7.xx = User defined
  A8.xx = 3D views, renderings
  A9.xx = 3D views, renderings

Example: A5.01 = Architectural, Detail sheet, first sheet
```

### Cross-Reference Rules

1. **Every detail marker on a plan MUST correspond to an actual detail** on the referenced sheet.
2. **Every detail drawn MUST be referenced** from at least one plan, section, or elevation.
3. **Orphan details** (drawn but never referenced) confuse contractors.
4. **Orphan markers** (referenced but not drawn) generate RFIs and delays.
5. **Verify cross-references** as the final step before issuing documents.
6. Use "SIM" (similar) when one detail applies to multiple conditions — reference the same detail number.

---

## 9. Detail Sheet Organization

### Sheet Layout Principles

1. **Grid layout:** Details are arranged in a modular grid on the sheet, typically 2-3 columns and 2-4 rows.
2. **Read left-to-right, top-to-bottom:** Detail 1 is top-left, Detail 2 is next to it or below it.
3. **Group related details together:** All roof details on one sheet, all foundation details on another.
4. **Consistent margins:** 1/2" minimum from detail to title block border.
5. **Title each detail:** Name + Scale + Detail number directly below each drawing.

### Typical Detail Sheet Organization for a Project

```
Sheet A5.01 — Foundation Details
  - Typical footing detail
  - Slab-on-grade edge detail
  - Foundation wall waterproofing
  - Pier/column footing

Sheet A5.02 — Wall Section Details
  - Exterior wall assembly (typical)
  - Wall-to-foundation connection
  - Wall-to-roof connection
  - Window head/sill/jamb

Sheet A5.03 — Roof Details
  - Eave detail
  - Ridge detail
  - Roof penetration detail
  - Parapet cap / coping

Sheet A5.04 — Door and Window Details
  - Typical window head
  - Typical window sill
  - Typical window jamb
  - Door frame details

Sheet A5.05 — Interior Details
  - Partition types
  - Ceiling details
  - Casework sections
  - Stair details

Sheet A5.06 — Miscellaneous Details
  - Expansion joints
  - Control joints
  - Flashing details
  - Specialties
```

### Detail Title Block Format

```
───────────────────────────────
  TYPICAL EAVE DETAIL
  SCALE: 1-1/2" = 1'-0"        3/A5.03
───────────────────────────────
```

Each detail title includes:
- **Drawing title** (descriptive name in caps)
- **Scale**
- **Detail number/sheet reference** (matches the bubble on referring drawings)

---

## 10. Visual Hierarchy and Drawing Order

### What to Draw First (Layer Order)

When constructing a detail from scratch, follow this sequence:

1. **Structure first:** Draw structural elements (beams, columns, slabs, studs, joists) — these are the skeleton.
2. **Sheathing/substrate:** Add plywood, OSB, CMU, concrete — the surfaces that receive everything else.
3. **Membranes and barriers:** Add air barriers, vapor barriers, waterproofing — these define the control layers.
4. **Insulation:** Fill cavities with batt or rigid insulation patterns.
5. **Flashing and drainage:** Add flashing, weep systems, drip edges.
6. **Cladding/finish:** Add exterior finish (siding, brick veneer, stucco) and interior finish (gypsum, paint).
7. **Accessories:** Add sealant, backer rod, trim, fasteners.
8. **Dimensions:** Add dimension strings.
9. **Notes and leaders:** Add callouts and keynotes last.
10. **Hatching:** Apply material patterns last — they must not interfere with geometry or annotations.

### Creating Visual Hierarchy

The goal: a reader should understand the detail at a glance, with the most important information jumping out first.

**Techniques:**

| Technique | Effect |
|-----------|--------|
| Heavy line weight on cut elements | Immediately shows WHAT is being cut through |
| Light hatching | Identifies materials without visual noise |
| White space around dimensions | Prevents crowding |
| Consistent note alignment | Creates order and readability |
| Poché (solid fill) for heavy elements | Ground, concrete, structural walls read as massive |
| Minimal line work beyond cut | Keeps focus on the assembly being detailed |

### Poché Convention

**Poché** is the technique of filling cut structural elements (concrete, earth, heavy masonry) with a solid or near-solid tone. This creates immediate visual weight and helps the reader distinguish between:
- Structure (dark/heavy fill)
- Cavity/air space (white/empty)
- Lightweight materials (light hatching)

In Revit, poché is achieved through material cut patterns with dense hatching or solid fill.

---

## 11. Required Details for Permits

### Universally Required (All Jurisdictions)

These details are required for virtually every building permit in the United States:

| Detail | Why Required |
|--------|-------------|
| **Typical wall section** (full height, foundation to roof) | Shows complete assembly compliance with energy code, fire rating, structural |
| **Foundation detail** (footing and stem wall) | Verifies structural adequacy, frost depth, reinforcement |
| **Slab-on-grade edge** | Shows vapor barrier, insulation, thickening at edges |
| **Roof-to-wall connection** | Demonstrates load path continuity, especially in high-wind zones |
| **Window/door head, sill, jamb** | Shows flashing, support, air sealing |
| **Stair section** | Verifies riser/tread dimensions, handrail height, guardrail |
| **Guardrail/handrail detail** | Shows attachment, height (42" min guardrail, 34-38" handrail), baluster spacing (4" max) |
| **Fireplace/chimney detail** | Clearances to combustibles, flue size, hearth extension |

### Conditionally Required

| Detail | When Required |
|--------|--------------|
| **Roof penetration details** | When mechanical units, vents, or skylights penetrate the roof |
| **Expansion/control joint details** | Large buildings, long walls, changes in material |
| **Waterproofing details** | Below-grade construction, occupied spaces below grade |
| **Fire-rated assembly details** | Multi-family, mixed-use, commercial (UL assemblies) |
| **Accessible route details** | Public buildings, multi-family (ADA compliance) |
| **Hurricane strap/tie details** | High-wind zones (ASCE 7 wind speed > 110 mph) |
| **Seismic bracing details** | Seismic Design Categories D, E, F |
| **Energy code compliance details** | Continuous insulation, thermal bridging, air barrier continuity |
| **New-to-existing connection** | Any renovation or addition project |

### Required Scales for Permit Drawings

Per most jurisdictions:
- **Full building sections:** 1/4" = 1'-0" minimum
- **Wall sections and details:** 1/2" = 1'-0" minimum
- **Enlarged details:** 3/4" = 1'-0" or larger

---

## 12. Common Mistakes and How to Avoid Them

### The Fatal Five (Most Common Errors)

#### 1. Incomplete Details
**Problem:** Leaving out layers, connections, or conditions. Using vague notes like "WATERPROOF AS REQUIRED" instead of specifying the actual product, thickness, and application method.

**Fix:** Every detail must show EVERY layer of the assembly, from interior finish to exterior finish. If a membrane exists in the spec, it must appear in the detail. No exceptions.

#### 2. Coordination Failures
**Problem:** The architectural detail shows a 2x6 wall, but the structural drawing calls for 2x8. The mechanical drawing runs a duct through the space where the detail shows solid structure.

**Fix:**
- Cross-reference details against structural, mechanical, electrical, and plumbing drawings.
- Overlay drawings (digitally) to check for conflicts.
- Verify that dimensions in details match dimensions in plans.
- Hold coordination meetings before issuing CDs.

#### 3. Conflicting Information
**Problem:** The detail shows one material or dimension, but the specification or schedule says something different. The contractor doesn't know which to follow.

**Fix:**
- **Never duplicate information** between drawings and specifications. Draw it OR specify it — not both.
- If you must show it in both places, verify they match.
- Establish a **hierarchy of precedence** in the project manual (typically: specifications govern over drawings for material/quality; drawings govern over specifications for quantity/location).

#### 4. Missing or Incorrect Cross-References
**Problem:** A detail bubble on a plan points to "3/A5.02" but there is no Detail 3 on sheet A5.02.

**Fix:**
- Maintain a cross-reference matrix.
- Verify ALL detail/section/elevation references as the final QC step.
- In Revit, use linked detail callouts that auto-update.

#### 5. Wrong Scale or NTS Without Explanation
**Problem:** A detail labeled "1-1/2" = 1'-0"" was actually drawn at 3/4" = 1'-0"". Or a detail is labeled "NTS" (Not to Scale) but has dimensions that appear scalar.

**Fix:**
- Always verify scale before issuing.
- If NTS, add a prominent note AND ensure no one can accidentally measure the drawing.
- Avoid NTS whenever possible — it erodes trust in the entire document set.

### Additional Common Mistakes

| Mistake | Impact | Prevention |
|---------|--------|------------|
| Inconsistent line weights | Detail looks amateurish; hard to read | Use pen tables; establish standards at project start |
| Notes inside hatching | Unreadable | Always place notes outside the detail with leaders |
| Crowded dimensions | Confusing, overlapping text | Use adequate spacing (1/2" first tier, 3/8" subsequent) |
| Missing slope indicators | Water ponding, drainage failures | Always show slopes on horizontal surfaces |
| Ignoring gravity | Flashing detailed upside-down; water paths that defy physics | Trace every water drop path from top to bottom |
| Showing materials in wrong scale | Brick courses that don't match reality | Verify coursing dimensions (standard brick: 2-1/4" + 3/8" mortar = 2-5/8" per course) |
| Orphan details | Contractor can't find referenced drawing | QC all cross-references |
| Copy-paste from other projects without updating | Wrong wall types, wrong dimensions, wrong project name | Review every detail for project-specific accuracy |

---

## 13. Revit-Specific Detailing Strategy

### Tool Hierarchy (Best to Worst)

1. **Detail Components (families)** — BEST. Schedulable, taggable, consistent, parametric. Use for insulation, framing, fasteners, flashing, etc.
2. **Repeating Detail Components** — For regularly spaced elements (brick courses, metal deck, blocking).
3. **Filled Regions** — For material areas that need hatching (concrete fill, gravel, earth). Use sparingly.
4. **Masking Regions** — To cover model elements that don't cut cleanly. Use only when necessary.
5. **Detail Lines** — LAST RESORT. For one-off geometry that doesn't warrant a family. Not schedulable, not taggable.

### When to Use Each Tool

| Situation | Tool |
|-----------|------|
| Batt insulation in wall cavity | Detail Component (insulation family) |
| Rigid insulation board | Detail Component |
| Brick veneer coursing | Repeating Detail Component |
| Metal deck profile | Repeating Detail Component |
| Earth/grade below foundation | Filled Region with earth hatch |
| Concrete footing section | Filled Region with concrete hatch (if model doesn't cut properly) |
| Flashing at window head | Detail Component (bent metal family) |
| Sealant and backer rod | Detail Component |
| One-off trim profile | Detail Lines + Filled Region |
| Simpson hardware (joist hanger) | Detail Component (manufacturer family) |

### Filled Region vs. Detail Component Decision Tree

```
Can this element appear in multiple details?
  YES → Make it a Detail Component family
  NO  → Is it a simple area fill (like earth or concrete)?
    YES → Filled Region
    NO  → Is it a one-off linear element?
      YES → Detail Lines (last resort)
      NO  → Reconsider — it probably should be a Detail Component
```

### View Template Settings for Details

- **Detail Level:** Fine
- **Visual Style:** Hidden Line
- **Discipline:** Architectural
- **Scale:** 1-1/2" = 1'-0" (default) or as appropriate
- **Line weight overrides:** Per project standard pen table
- **Model categories to hide:** Rooms, Areas, Analytical elements, MEP placeholders
- **Annotation categories to show:** Dimensions, Text Notes, Detail Items, Keynotes, Matchlines

### Revit Detail Line Styles to Configure

```
01 - Wide         0.50 mm   (cut structure)
02 - Medium       0.35 mm   (cut secondary / object lines)
03 - Thin         0.25 mm   (visible edges beyond cut)
04 - Fine         0.13 mm   (dimensions, leaders)
05 - Hairline     0.09 mm   (hatching, very minor elements)
06 - Demolished   0.25 mm   (dashed, for demolished elements)
07 - Hidden       0.18 mm   (dashed, for hidden elements)
08 - Centerline   0.13 mm   (center-dash pattern)
```

---

## 14. Professional Quality Checklist

Run this checklist on every detail before issuing:

### Completeness

- [ ] Every layer of the assembly is shown (structure, substrate, barriers, insulation, finish)
- [ ] All connections to adjacent assemblies are detailed or referenced
- [ ] Flashing and waterproofing are shown at every transition and penetration
- [ ] Fastener types and spacing are indicated
- [ ] Slope/drainage is shown on all horizontal or near-horizontal surfaces
- [ ] Fire-rating assemblies are properly shown with UL reference

### Graphic Quality

- [ ] At least 4 distinct line weights are used
- [ ] Cut elements are heavier than projection elements
- [ ] Hatching is fine and does not compete with geometry
- [ ] Adjacent materials have different hatch patterns
- [ ] Ground line is the heaviest line in the drawing
- [ ] White space exists between the detail and annotations

### Dimensions

- [ ] All material thicknesses are dimensioned
- [ ] Critical clearances are dimensioned
- [ ] Dimension strings add up to the overall dimension
- [ ] Dimension text is minimum 3/32" when printed
- [ ] Slopes are noted with direction arrows
- [ ] First dimension line is 1/2" from the object

### Annotations

- [ ] All materials are identified by note or keynote
- [ ] Notes are outside the detail boundary
- [ ] Leader lines do not cross
- [ ] Leader lines have elbows (horizontal landings)
- [ ] Abbreviations are consistent with the abbreviation legend
- [ ] No vague notes ("AS REQUIRED", "VERIFY IN FIELD" without specifying what)

### Cross-References

- [ ] Detail number matches the reference bubble on the parent drawing
- [ ] Sheet number in the bubble matches the actual sheet
- [ ] Scale noted under the detail title matches the actual drawing scale
- [ ] All referenced specification sections exist in the project manual
- [ ] No orphan details (drawn but never referenced)
- [ ] No orphan markers (referenced but not drawn)

### Building Science

- [ ] Water path traced from top to bottom — no traps, no dead ends
- [ ] Vapor barrier on warm side of assembly (verify for climate zone)
- [ ] Air barrier is continuous and sealed at all transitions
- [ ] Thermal bridge is minimized or addressed with continuous insulation
- [ ] Drainage cavity is clear and connected to weep system
- [ ] Sealant joints have proper depth-to-width ratio (minimum 1:2, ideally 1:1)
- [ ] Backer rod diameter is 25-50% larger than joint width

---

## Appendix A: Material Layer Thicknesses (Common Values)

These are the actual dimensions to use when drawing layers to scale:

| Material | Common Thickness | Notes |
|----------|-----------------|-------|
| Gypsum board (drywall) | 1/2" or 5/8" | 5/8" Type X for fire rating |
| Plywood sheathing | 1/2" or 3/4" | 3/4" for subfloor; 1/2" or 7/16" for wall |
| OSB sheathing | 7/16" or 1/2" | Common wall sheathing |
| Batt insulation (2x4 wall) | 3-1/2" | Fills 2x4 cavity |
| Batt insulation (2x6 wall) | 5-1/2" | Fills 2x6 cavity |
| Rigid insulation (XPS/EPS) | 1", 1-1/2", 2", 3", 4" | Varies by R-value requirement |
| Polyiso rigid insulation | 1", 1-1/2", 2", 2-1/2", 3" | Higher R-value per inch |
| Air/drainage cavity | 1" minimum | 1" min for ventilated rainscreen |
| Brick veneer | 3-5/8" (nominal 4") | Standard modular brick |
| CMU block | 7-5/8" (nominal 8") | Standard 8" CMU |
| CMU block | 11-5/8" (nominal 12") | 12" CMU |
| Concrete slab on grade | 4" or 6" | 4" residential; 6" commercial/garage |
| Concrete foundation wall | 8", 10", or 12" | Varies by height and loading |
| Stucco (3-coat) | 7/8" | Over metal lath on sheathing |
| EIFS | 1" to 4" | EPS thickness varies |
| Vapor barrier (polyethylene) | 6 mil (0.006") | Drawn as a line, not to thickness scale |
| Self-adhering membrane | 40-60 mil | Drawn as a bold line |
| Metal flashing | 24-26 gauge | Drawn as a line at detail scale |
| Standing seam metal roof | 1" to 2" panel height | Show actual seam profile |
| Asphalt shingles | 1/4" to 3/8" nominal | Drawn as a thick line or thin wedge |
| Metal deck | 1-1/2" or 3" | Draw actual corrugation profile |
| Concrete topping on deck | 2-1/2" to 3-1/2" | Lightweight or normal weight |
| Ceramic tile + thinset | 3/8" to 1/2" total | Tile + adhesive |
| Mortar bed | 1-1/4" to 2" | For thick-bed tile installations |
| Terrazzo | 1/2" (thin-set) to 2-1/2" (sand cushion) | Varies by system |

## Appendix B: Standard Masonry Coursing

| Unit | Nominal Height | Actual Height | With Mortar Joint | Courses per Foot |
|------|---------------|---------------|-------------------|-----------------|
| Standard brick | 2-2/3" | 2-1/4" | 2-5/8" (2-1/4" + 3/8") | 4.57 → use 3 courses = 8" |
| Modular brick | 2-2/3" | 2-1/4" | 2-2/3" | 3 courses = 8" exactly |
| CMU (8" nominal) | 8" | 7-5/8" | 8" (7-5/8" + 3/8") | 1.5 courses per foot |
| CMU (4" nominal) | 8" height | 7-5/8" | 8" | Same as 8" CMU (face only) |

**Critical Rule:** 3 courses of modular brick = 8" = 1 CMU course height. This alignment is essential for masonry detailing.

## Appendix C: Sealant Joint Design

```
  ←─ W ─→
  ┌───────┐
  │SEALANT│  ← Depth = W/2 (minimum) to W (maximum)
  ├───────┤
  │BACKER │  ← Diameter = 1.25W to 1.5W
  │  ROD  │
  ├───────┤
  │ BOND  │  ← Prevents 3-sided adhesion
  │BREAKER│
  └───────┘

  Joint width (W): 1/4" minimum for sealant joints
  Depth-to-width ratio: 1:2 (min depth = W/2)
  Backer rod: 25-50% larger than joint width
  Sealant should adhere to TWO sides only (not back)
```

## Appendix D: Key Reference Standards

| Standard | Content |
|----------|---------|
| **AIA Architectural Graphic Standards (AGS)** | Master reference for all graphic conventions, symbols, material patterns |
| **US National CAD Standard (NCS) v6** | CAD layer names, line types, sheet organization, plotting conventions |
| **CSI MasterFormat** | Specification organization by division (01-49) |
| **CSI UniFormat** | Organization by building system (used for preliminary specs) |
| **AIA A201** | General Conditions — establishes document precedence |
| **ANSI Y14.1** | Drawing sheet sizes and formats |
| **ASME Y14.2** | Line conventions and lettering |
| **BS 1192** | British Standard for construction drawing practice (hatching patterns) |
| **IBC (International Building Code)** | Required details for code compliance |
| **IECC (International Energy Conservation Code)** | Insulation, air barrier, and thermal bridge requirements |
| **ASCE 7** | Loads — determines required structural connection details |
| **ADA/ABA Standards** | Accessibility requirements affecting detail dimensions |

---

*This guide synthesizes standards from AIA Architectural Graphic Standards, the US National CAD Standard (NCS), CSI MasterFormat conventions, and professional practice from sources including Life of an Architect, First in Architecture, Archtoolbox, Archisoup, and industry forums.*
