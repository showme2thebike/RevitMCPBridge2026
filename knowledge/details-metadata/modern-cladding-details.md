# Modern Cladding Systems & Rain Screen Assemblies

> Complete reference for drawing architect-quality 2D construction details in Revit via API.
> Covers rain screen, metal panel, fiber cement, brick veneer, stone veneer, stucco/EIFS, and mixed cladding transitions.

---

## Table of Contents

1. [Rain Screen Cladding Systems](#1-rain-screen-cladding-systems)
2. [Metal Panel Cladding](#2-metal-panel-cladding)
3. [Fiber Cement (Hardie) Details](#3-fiber-cement-hardie-details)
4. [Brick Veneer over Wood Frame](#4-brick-veneer-over-wood-frame)
5. [Stone / Manufactured Stone Veneer](#5-stone--manufactured-stone-veneer)
6. [Stucco / EIFS Details](#6-stucco--eifs-details)
7. [Modern Mixed Cladding Transitions](#7-modern-mixed-cladding-transitions)

---

## 1. Rain Screen Cladding Systems

### 1.1 Rain Screen Principle

The rain screen is a pressure-equalized, drained, and ventilated wall system. It separates the water-shedding function (outer cladding) from the air/water/thermal barrier (inner wall). Water that penetrates the outer cladding drains down the cavity and exits at the base through weep openings.

**Three forces that drive water into walls (all neutralized by rain screen):**
- Gravity — drained by cavity
- Kinetic energy (wind-driven rain) — deflected by cladding
- Capillary action — broken by air gap (minimum 3/8")
- Air pressure differential — equalized by vented cavity

### 1.2 Wall Assembly (Outside to Inside)

| Layer | Material | Thickness | Notes |
|-------|----------|-----------|-------|
| Cladding | Wood, metal, fiber cement, stone, etc. | Varies by material | Outer rain deflection layer |
| Ventilation gap | Air cavity | 3/4" typical (3/8" min, 1" preferred) | Must be 80% open in cross-section |
| Furring/support | Wood straps, metal hat channel, or clips | 3/4" to 1-1/2" | Vertical orientation for drainage |
| Continuous insulation (CI) | Mineral wool or rigid foam | 1" to 4" (climate dependent) | Outboard of sheathing |
| Water-resistive barrier (WRB) | House wrap or fluid-applied | — | Must lap shingle-style |
| Sheathing | Plywood or OSB | 7/16" to 5/8" | Structural substrate |
| Framing | Wood stud or steel stud | 2x4 or 2x6 | Load-bearing structure |
| Cavity insulation | Batt or blown | Fills stud cavity | R-13 to R-21 (wood frame) |
| Interior vapor control | Poly or smart membrane | — | Climate zone dependent |
| Interior finish | Gypsum board | 1/2" or 5/8" | — |

### 1.3 Furring/Support Systems

**Wood furring strips:**
- 1x3 or 1x4 pressure-treated or cedar
- Vertical orientation, 16" o.c. (align with studs)
- Fastened through CI into studs with structural screws
- Simple, cost-effective; limited to low-rise

**Metal hat channel:**
- 7/8" or 1-5/8" depth aluminum or galvanized steel
- Vertical orientation, 16" or 24" o.c.
- Non-combustible (required Type I-IV construction)
- Screw-attached through CI into structure

**Clip systems (e.g., Cascadia Clip, Knight Wall MFI):**
- Fiberglass or stainless steel clips
- Accommodate up to 8" of continuous insulation
- Minimal thermal bridging (3% contact area)
- Clip attaches to stud; rail attaches to clip; cladding attaches to rail
- Automatically creates consistent cavity depth

### 1.4 Top and Bottom Ventilation

**Base (intake):**
- Perforated J-channel or vented starter strip
- Insect screen (corrosion-resistant mesh, 1/16" openings)
- Minimum 4" above grade
- Weep openings every 16"-24" if not continuous

**Top (exhaust):**
- Vented soffit or perforated trim at top of wall
- Gap behind fascia or parapet cap
- Insect screen required

**At windows/doors:**
- Maintain continuous cavity around openings
- Sill pan flashing drains to cavity
- Head flashing directs water outward over cladding

### 1.5 Flashing Integration at Openings

```
RAIN SCREEN AT WINDOW HEAD:
                    _______________
     Cladding -->  |               |
     Air gap  -->  |   ___________/  <-- Head flashing (drip edge)
     CI       -->  |  |
     WRB      -->  |  |  Window frame
     Sheathing --> |  |____________
                   |               |
```

- Head flashing: Bent metal, extends minimum 1" beyond cladding face
- Jamb: Flexible flashing membrane, lapped under head flashing
- Sill: Pan flashing with end dams, sloped 5-degree minimum to exterior
- Back dam at sill: Minimum 2" up behind window frame

### 1.6 Code Requirements

| Code | Section | Requirement |
|------|---------|-------------|
| IBC 2021 | 1402.2 | Weather protection required for exterior walls |
| IBC 2021 | 2510.6 | 3/16" drainage cavity or 90% drainage efficiency (Zones A, C) |
| IBC 2021 | 1403.2 | WRB required behind exterior veneer |
| IRC 2021 | R703.1.1 | Water-resistive barrier required |
| ASTM E2273 | — | Standard test for drainage efficiency |
| NFPA 285 | — | Fire test for exterior wall assemblies (required for combustible CI in Type I-IV) |

### 1.7 Climate Zone Considerations

| Climate Zone | CI Minimum | Vapor Control | Notes |
|--------------|-----------|---------------|-------|
| 1-3 (Hot) | R-5 to R-7.5 | None or Class III | Ventilated cavity helps dry inward |
| 4 (Mixed) | R-7.5 to R-10 | Class III | Smart vapor retarder recommended |
| 5-6 (Cold) | R-10 to R-15 | Class II or smart | Keep sheathing above dew point |
| 7-8 (Very Cold) | R-15 to R-25 | Class I or smart | Thick CI critical; cavity ventilation aids drying |

### 1.8 Common Failures

1. **Blocked cavity** — Mortar droppings, insulation sag, or debris blocks drainage. Keep cavity 80% open.
2. **Missing insect screen** — Wasps, insects nest in cavity. Always screen top and bottom.
3. **Inadequate flashing** — Water bypasses cavity at windows. Sill pan with end dams is non-negotiable.
4. **Fastener thermal bridging** — Long screws through CI create thermal bridges. Use clip systems for thick CI.
5. **No bottom ventilation** — Cavity becomes a moisture trap. Must have intake and exhaust.

### 1.9 Revit Detail Components

- `Detail Component: Furring Strip (wood)` — 1x3 or 1x4 profile
- `Detail Component: Hat Channel` — 7/8" or 1-5/8" metal hat
- `Detail Component: Clip System` — Fiberglass clip with rail
- `Detail Component: Insect Screen` — Hatched mesh at openings
- `Detail Component: Continuous Insulation` — Mineral wool or foam board
- `Detail Component: WRB` — Heavy dashed line behind CI
- `Filled Region: Air Cavity` — White/clear, 3/4" wide
- `Detail Component: Flashing` — Bent metal at head, sill, base

### 1.10 Drawing Annotations

- Dimension air gap (3/4" typ.)
- Note CI type and R-value
- Note WRB product
- Call out fastener type and spacing (into studs)
- Note "Vent opening w/ insect screen" at top and bottom
- Note flashing material and gauge
- Reference ASTM E2273 drainage test if required

---

## 2. Metal Panel Cladding

### 2.1 Panel Types Overview

| Type | Profile | Fastener | Gauge | Width | Application |
|------|---------|----------|-------|-------|-------------|
| Standing seam | 1" to 2" vertical ribs | Concealed clips | 22-24 ga steel, .032 aluminum | 12"-18" | Premium walls, curved surfaces |
| Flush seam | Flat face, interlocking edges | Concealed | 22-24 ga | 12"-16" | Modern/minimal aesthetic |
| R-Panel / PBR | 1-1/4" trapezoidal ribs | Exposed (self-drilling screws) | 22-26 ga | 36" | Agricultural, industrial, budget commercial |
| Corrugated | 7/8" sinusoidal waves | Exposed | 22-26 ga | 26"-36" | Industrial, rustic, modern residential |
| ACM/MCM (composite) | Flat, route-and-return pans | Concealed clips | 4mm composite (0.5mm skins) | Custom | High-end commercial, signage |

### 2.2 Standing Seam Wall Panel Assembly (Outside to Inside)

| Layer | Material | Thickness | Notes |
|-------|----------|-----------|-------|
| Metal panel | Steel or aluminum, factory-finished | 22-24 ga (0.030"-0.024") | PVDF or SMP coating |
| Clip/attachment | Floating clips on hat channel or Z-girt | — | Allows thermal movement |
| Air cavity | Ventilated space | 1" to 2" | Behind panel, over CI |
| Continuous insulation | Mineral wool or polyiso | 1" to 4" | Non-combustible preferred |
| WRB / air barrier | Fluid-applied or self-adhered membrane | — | Continuous, sealed at penetrations |
| Sheathing | Exterior gypsum or plywood | 1/2" to 5/8" | Non-combustible for Type I-IV |
| Metal stud framing | 3-5/8" to 6" steel studs | — | 16" or 24" o.c. |
| Cavity insulation | Batt insulation | Fills cavity | R-13 to R-19 |
| Interior gypsum | Type X if fire-rated | 5/8" | — |

### 2.3 Clip and Attachment Details

**One-piece fixed clip:**
- Fastened to subgirt/hat channel
- Panel snaps onto clip
- Panel slides along clip axis for thermal movement
- Friction-fit only — no mechanical lock against movement

**Two-piece sliding clip:**
- Base fastened to structure
- Top piece engages panel seam
- Slide mechanism accommodates 1/2" to 1" thermal movement
- Used for long panel runs (> 20')

**Thermal break at clips:**
- Thermal spacer block (nylon, fiberglass, or HDPE) between clip and structure
- Reduces thermal bridging through metal fasteners
- Critical for energy code compliance with thick CI

### 2.4 Exposed Fastener Panel (R-Panel / Corrugated)

**Assembly (budget wall):**

| Layer | Material | Thickness |
|-------|----------|-----------|
| Metal panel | 26-24 ga steel | 0.018"-0.024" |
| Self-drilling screws | #12 or #14 w/ EPDM washer | — |
| Girt/purlin | Z-girt or C-channel | 1-1/2" to 3-1/2" |
| Insulation | Faced fiberglass blanket | 2" to 6" |
| Liner panel (optional) | 26 ga steel | — |

**Fastener spacing:**
- Side-lap screws: 12" o.c. (walls)
- Panel-to-girt screws: Every rib or every other rib at each girt
- Girt spacing: 24" to 48" o.c. (per engineering)

**Base trim:** Factory-bent base channel, fastened with pancake screws 12" o.c., seals bottom of panel to foundation.

**Cap trim:** Formed closure strip at top, fastened under soffit or parapet cap.

### 2.5 ACM/MCM Composite Panel Systems

**Route-and-return fabrication:**
- 4mm composite sheet (two 0.5mm aluminum skins, PE or FR core)
- Router cuts V-groove on back face
- Panel folds into a pan (tray) with 1" return legs
- Pan attaches to aluminum extrusion frame with clips

**Wet seal system:**
- Silicone sealant between panels (medium modulus)
- Backer rod behind sealant
- Joint width: 1/2" typical
- Single-line barrier wall — sealant is the weather seal

**Dry seal system:**
- Interlocking aluminum extrusions between panels
- Gaskets within extrusions
- No exposed sealant
- Pressure-equalized rain screen principle

**NFPA 285 requirement:** ACM with PE core fails; FR (fire-retardant) core or mineral core required for buildings over 40' or Type I-IV construction.

### 2.6 Metal Panel at Window/Door

**Head:**
- Bent metal head flashing, continuous across opening
- Drip edge extends 1" beyond panel face
- Sealant between flashing and window frame
- Panel terminates at J-trim or receiver channel above flashing

**Jamb:**
- J-trim or brake-formed channel receives panel edge
- Sealant between trim and window frame
- Backer rod + sealant joint (3/8" to 1/2")

**Sill:**
- Sloped sill flashing (5-degree minimum)
- End dams at jamb transitions
- Panel starts below sill with J-trim
- Weep holes or gap at bottom of sill trim

### 2.7 Corner Details

**Outside corner:**
- Brake-formed corner trim (one piece, folded)
- OR two J-trims meeting at corner with sealant
- Minimum 2" leg on each side
- Sealant joint at panel-to-trim interface

**Inside corner:**
- Brake-formed inside corner trim
- Panel terminates at trim with 1/4" gap + sealant
- Allows for building movement

### 2.8 Critical Dimensions

| Dimension | Value | Notes |
|-----------|-------|-------|
| Panel gauge (concealed fastener) | 22-24 ga steel, .032 aluminum | Minimum for structural clip engagement |
| Panel gauge (exposed fastener) | 24-26 ga steel | Budget applications |
| Seam height | 1" to 2" | Standing seam walls |
| Joint width (ACM wet seal) | 1/2" | With backer rod |
| Thermal movement | 1/8" per 10' per 100 deg F (steel) | Aluminum moves 2x as much |
| Maximum panel length (no expansion joint) | 20' (steel), 12' (aluminum) | Longer requires sliding clips |
| Screw spacing (exposed) | 12" o.c. at laps, each rib at girts | — |

### 2.9 Common Failures

1. **Oil-canning** — Flat panels show waviness. Use striations, stiffening ribs, or thicker gauge.
2. **Thermal buckling** — Fixed both ends of long panel. Use floating clips at one end.
3. **Galvanic corrosion** — Dissimilar metals in contact. Isolate with nylon washers or thermal breaks.
4. **PE core ACM in fire** — Catastrophic. Specify FR core or mineral core for all buildings.
5. **Sealant failure at wet-seal joints** — Wrong sealant or no backer rod. Use silicone with backer rod, size joint for movement.

### 2.10 Revit Detail Components

- `Detail Component: Standing Seam Panel` — Profile with seam ribs
- `Detail Component: Metal Panel Clip` — One-piece or two-piece
- `Detail Component: Hat Channel / Z-Girt` — Substructure profile
- `Detail Component: ACM Panel Pan` — Route-and-return tray section
- `Detail Component: Backer Rod + Sealant` — Joint fill
- `Detail Component: Thermal Break Spacer` — At clip locations
- `Filled Region: Insulation (Continuous)` — Hatch behind cavity
- `Detail Component: Self-Drilling Screw` — With EPDM washer

---

## 3. Fiber Cement (Hardie) Details

### 3.1 Product Types

| Product | Profile | Thickness | Width | Exposure |
|---------|---------|-----------|-------|----------|
| HardiePlank Lap Siding | Horizontal lap, smooth or textured | 5/16" (0.312") | 6.25" to 12" nominal | Varies by width |
| HardiePanel | 4'x8' flat sheet | 5/16" | 48" | N/A — full sheet |
| HardieShingle | Staggered edge shingle | 1/4" | 12" or 15.25" | 7" typical |
| HardieTrim | Flat trim board | 3/4" or 1" | 3.5" to 11.25" | N/A — trim |
| Board & Batten | HardiePanel + HardieTrim battens | 5/16" panel + 3/4" batten | — | — |

### 3.2 Horizontal Lap Siding Assembly (Outside to Inside)

| Layer | Material | Thickness | Notes |
|-------|----------|-----------|-------|
| Fiber cement lap siding | HardiePlank | 5/16" | Factory-primed, field-painted |
| Starter strip | 1-1/4" ripped HardiePanel | 5/16" x 1-1/4" | Creates angle for first course |
| Rain screen gap (optional) | Furring strips or drain mat | 3/8" to 3/4" | Recommended in all climate zones |
| WRB | House wrap or fluid-applied | — | Required, lapped shingle-style |
| Sheathing | Plywood or OSB | 7/16" to 1/2" | Structural nail base |
| Framing | 2x4 or 2x6 wood studs | 3.5" to 5.5" | 16" o.c. |
| Cavity insulation | Fiberglass batt or blown cellulose | Fills cavity | R-13 to R-21 |
| Vapor retarder | Per climate zone | — | — |
| Interior gypsum | Drywall | 1/2" | — |

### 3.3 Lap Siding Installation Specifications

**Overlap:** Minimum 1-1/4" lap (all methods)

**Blind nailing (preferred):**
- Nail placed 3/4" to 1" from top edge of plank
- Nail line printed on HZ5 products
- Nail penetrates into stud minimum 1" (through sheathing)
- Each plank fastened at every stud (16" o.c. maximum)

**Face nailing:**
- Nail placed 3/4" to 1" from bottom edge
- Head snug to surface — never countersunk or overdriven
- Required for planks wider than 9.25" (two nails per stud: one blind, one face)

**Fastener requirements:**
- 6d siding nails (0.118" shank x 0.267" head x 2" long)
- OR roofing nails (0.089" shank x 0.221" head x 2" long) for blind nailing
- Hot-dipped galvanized or stainless steel only
- Minimum 3/8" from plank ends

**Butt joints:**
- 1/8" gap between plank ends at butt joints
- Caulk with paintable sealant after installation
- Stagger joints between courses (minimum 2 stud bays apart)
- Metal flashing or Hardie backer strip behind every butt joint

### 3.4 HardiePanel (Vertical) with Battens

**Assembly:**
- 4'x8' panels installed vertically
- 1/8" gap at panel joints (not caulked — batten covers)
- HardieTrim battens (3/4" x 1-1/2" to 3-1/2") fastened over joints
- Battens nailed through panel into studs

**Board and batten spacing:** Battens typically 12" to 16" o.c. (every other stud if 8" o.c. panels)

### 3.5 Window and Door Trim Details

**Head:**
- HardieTrim head board (3/4" x 3-1/2" minimum)
- Z-flashing above trim, under WRB
- 1/4" gap between trim and siding above (not caulked — allows drainage behind)
- Siding overlaps Z-flashing by minimum 1"

**Jamb:**
- HardieTrim jamb boards (3/4" x 3-1/2" to 5-1/2")
- 1/8" gap between siding and trim, caulked
- Flashing membrane behind trim, integrated with WRB

**Sill:**
- HardieTrim sill board, sloped 5 degrees to exterior
- Sill flashing pan beneath, with end dams
- 1/4" gap between siding and sill trim bottom — NOT caulked (weep)

### 3.6 Base / Starter Course Detail

```
FIBER CEMENT BASE DETAIL:

     HardiePlank siding
     |  1-1/4" overlap
     |  |
     ===+========  <-- Second course
     |            |
     ===+========  <-- First course
     |--| 1-1/4" starter strip
     |
     6" min. above grade
     |
     ~~~~~~~~  Grade
```

- Starter strip: 1-1/4" wide ripped HardiePanel, nailed at base
- First plank overhangs starter by 1/4"
- Minimum 6" clearance from grade to bottom of siding
- Minimum 1" clearance from horizontal surfaces (decks, roofs)
- Minimum 2" clearance from roofing material

### 3.7 Fiber Cement over Rain Screen vs Direct-Applied

| Criteria | Direct-Applied | Over Rain Screen |
|----------|---------------|-----------------|
| WRB | Required | Required |
| Air gap | None (WRB against sheathing) | 3/8" to 3/4" |
| Drying potential | Single-direction (outward) | Bidirectional (cavity ventilation) |
| Fastener length | Standard (2") | Longer (through furring + sheathing) |
| Code | Acceptable all zones | Required in marine (Zone C) per IBC 2510.6 |
| Best practice | Acceptable in dry climates | Recommended everywhere, required in wet |
| Cost premium | Baseline | +$0.50-1.50/SF for furring system |

### 3.8 Code Requirements

| Code | Section | Requirement |
|------|---------|-------------|
| IRC R703.10 | Fiber cement siding | Must comply with ASTM C1186 Type A (non-structural) |
| IRC R703.1.1 | WRB | Required behind all siding |
| IBC 1404 | Installation | Per manufacturer's instructions (ICC-ES ESR-2290) |
| Hardie spec | — | Minimum 6" above grade, 1" above horizontal surfaces |
| Hardie spec | — | Maximum 1-1/4" overlap for blind nailing |
| ASTM C1186 | — | Fiber cement flat sheet specification |

### 3.9 Common Failures

1. **Overdriven nails** — Crushes fiber cement, causes cracking. Nails must be snug, not countersunk.
2. **No flashing behind butt joints** — Water penetrates at every joint. Always flash.
3. **Caulked bottom of Z-flashing** — Traps water. 1/4" gap above Z-flashing, never caulked.
4. **Ground contact** — Wicks moisture, swells, delaminates. Maintain 6" clearance.
5. **Cut edges not sealed** — Hardie requires touch-up primer on all cut edges within 24 hours.
6. **Insufficient overlap** — Less than 1-1/4" allows wind-driven rain entry.

### 3.10 Revit Detail Components

- `Detail Component: Fiber Cement Lap Siding` — 5/16" thick with lap profile
- `Detail Component: Starter Strip` — 5/16" x 1-1/4"
- `Detail Component: Fiber Cement Panel` — 5/16" flat sheet
- `Detail Component: Fiber Cement Trim` — 3/4" x variable width
- `Detail Component: Z-Flashing` — Bent metal, 26 ga galvanized
- `Detail Component: Siding Nail` — Face or blind position
- `Filled Region: Fiber Cement` — Dense hatch, similar to concrete but thinner
- `Annotation: Lap Dimension` — 1-1/4" callout

---

## 4. Brick Veneer over Wood Frame

### 4.1 Full Wall Section (Outside to Inside)

| Layer | Material | Thickness | Notes |
|-------|----------|-----------|-------|
| Brick veneer | Modular (3-5/8") or standard (3-3/4") | 3-5/8" to 3-3/4" | Self-supporting, non-structural |
| Air space | Clear cavity | 1" minimum, 2" preferred | Tolerance: -1/4" to +3/8" |
| Continuous insulation (optional) | Rigid foam or mineral wool | 1" to 2" | Fastened to sheathing face |
| WRB | House wrap or fluid-applied | — | Over sheathing |
| Sheathing | Plywood or OSB | 7/16" to 1/2" | — |
| Wood framing | 2x4 or 2x6 studs | 3.5" to 5.5" | 16" o.c. |
| Cavity insulation | Fiberglass batt | Fills cavity | R-13 to R-21 |
| Vapor retarder | Per climate zone | — | — |
| Interior gypsum | Drywall | 1/2" | — |

**Total wall thickness:** Approximately 12" to 15" (varies with stud size, CI, air space)

### 4.2 Foundation / Base Detail

```
BRICK VENEER BASE DETAIL:

     Brick veneer
     |    1" min. air space
     |    |  WRB
     |    |  | Sheathing
     |    |  | | Stud
     ====    =====
     ====    =====
     ====    =====  <-- Through-wall flashing (extends past brick face)
     ====    =====
     ~~~~    ~~~~~  <-- Weep holes @ 24" o.c. (open head joint or tubes)
     ====    =====
     |  |    |   |
     |  |    |   |
     FOUNDATION (brick ledge)
     |              |
     ~~~~~~~~~~~~~~~~
          FOOTING
```

- Through-wall flashing at base: Self-adhered membrane or metal, lapped into WRB
- Flashing extends through brick face with drip edge
- Weep holes: 3/16" diameter minimum, 24" o.c. maximum (16" o.c. preferred)
- Open head joint weeps: Omit mortar in head joint at flashing level
- Tube weeps: Cotton rope or plastic tubes in head joints
- Foundation brick ledge: Width = brick + air space + CI (typically 5-5/8" to 7")

### 4.3 Wall Ties

**Corrugated metal ties (residential):**
- 22 ga galvanized corrugated strip
- 7/8" wide x 6-5/8" long minimum
- Maximum area per tie: 2.67 SF
- Spacing: 32" o.c. horizontal, 24" o.c. vertical (every 6th course at 16" stud spacing)

**Adjustable wire ties (commercial / preferred):**
- Stainless steel or hot-dip galvanized
- Two-piece: eye-and-pintle or adjustable slot
- Same area/spacing requirements
- Better accommodates differential movement

**Additional ties required:**
- Within 12" of all openings
- Maximum 3'-0" o.c. around perimeter of openings > 16"
- At inside and outside corners

### 4.4 Shelf Angle Detail (Multi-Story)

**When required:** Brick veneer on wood frame is typically limited to 30' in height (IRC). Multi-story requires intermittent support.

**Shelf angle assembly:**
- Steel angle: L 3-1/2" x 3-1/2" x 1/4" minimum (sized by engineer)
- Bolted to structure (concrete floor edge, steel beam, or wood ledger with through-bolts)
- Flashing above and below angle
- Weep holes immediately above angle
- Soft joint below angle: 3/8" to 1/2" sealant joint (backer rod + sealant)
- This soft joint accommodates vertical differential movement

**Soft joint calculation:** Joint width = anticipated movement / sealant movement capability. Typical: 3/8" joint with 50% compressible sealant accommodates 3/16" movement.

### 4.5 Window Head (Steel Lintel)

**Lintel sizing:**
- L 3-1/2" x 3-1/2" x 1/4" for openings up to 6'-0"
- L 4" x 3-1/2" x 1/4" for openings 6'-0" to 8'-0"
- Minimum bearing: 4" each side (8" preferred for spans over 6'-0")
- Loose steel angle lintel (painted or galvanized)

**Flashing at head:**
- Through-wall flashing over lintel, turned up as back dam
- End dams at each end (flashing turns up vertically)
- Flashing extends past face of brick with drip edge
- Weep holes at 24" o.c. above flashing

### 4.6 Window Sill (Rowlock Course)

**Rowlock sill:**
- Brick laid on edge (rowlock) sloping outward
- Minimum 15-degree slope (1/4" per inch)
- Through-wall flashing beneath rowlock, turned up behind window frame
- End dams at jambs
- Sealant joint between window frame and rowlock top
- Weep holes at ends of sill

### 4.7 Window Jamb

- Brick returns to window frame
- 1/2" sealant joint between brick and frame (backer rod + sealant)
- Wall ties at 24" o.c. vertically adjacent to opening
- WRB wraps into opening, lapped under sill pan

### 4.8 Expansion Joints

**Vertical expansion joints:**
- Spacing: 25' o.c. maximum (no openings), 20' o.c. (with openings)
- Width: 3/8" to 1/2"
- Filled with: Backer rod + sealant (not mortar)
- Location: At corners, at changes in wall height, at columns, aligned with window/door edges

**Horizontal expansion joints:**
- At every shelf angle (soft joint beneath)
- At roof/floor line transitions
- Width: 3/8" minimum

**Sealant:** Use compatible sealant (polyurethane or silicone) with backer rod. Sealant depth = 1/2 joint width. Never fill with mortar.

### 4.9 Soldier Course / Decorative Coursing

- Soldier course: Brick stood vertically (typically over windows as decorative lintel)
- Requires steel lintel behind for structural support (soldier brick is not structural)
- Maintain flashing above lintel regardless of decorative coursing
- Rowlock: Brick on edge, used at sills and sometimes as belt course
- Header course: Brick turned perpendicular (decorative in veneer — does not tie to backing)

### 4.10 Code Requirements

| Code | Section | Requirement |
|------|---------|-------------|
| IRC R703.8 | Anchored masonry veneer | Maximum 30' height for wood frame backing |
| TMS 402/602 | — | Structural design of masonry |
| IBC 1405.6 | Anchored veneer | Wall tie requirements, air space limits |
| BIA TN 28B | — | Brick veneer over wood frame (design guide) |
| BIA TN 44B | — | Wall ties for brick masonry |
| BIA TN 18A | — | Accommodating expansion of brickwork |
| BIA TN 7 | — | Water resistance of brick masonry |

### 4.11 Common Failures

1. **Missing weep holes** — Most common failure. Water fills cavity, soaks sheathing, causes rot. ALWAYS detail weeps.
2. **Mortar bridges in cavity** — Mortar droppings bridge air space, wick water to sheathing. Use mortar net or cavity drainage board.
3. **Inadequate flashing** — Water bypasses flashings at windows and base. End dams are critical.
4. **No expansion joints** — Brick grows over time (moisture expansion). Cracks at corners and windows. Space joints per BIA.
5. **Corroded wall ties** — Galvanized coating fails in aggressive environments. Use stainless steel in coastal areas.
6. **Shelf angle not shimmed** — Uneven support. Steel shims between angle and structure are acceptable; mortar shims are not.

### 4.12 Revit Detail Components

- `Detail Component: Brick Veneer` — Modular brick coursing profile
- `Detail Component: Mortar Joint` — 3/8" joints
- `Detail Component: Wall Tie` — Corrugated or adjustable wire
- `Detail Component: Steel Angle Lintel` — L-shape with dimensions
- `Detail Component: Shelf Angle` — L-angle at floor line
- `Detail Component: Through-Wall Flashing` — Heavy line with drip edge
- `Detail Component: Weep Hole` — Circle or open head joint
- `Detail Component: Rowlock Sill` — Brick on edge, sloped
- `Detail Component: Soldier Course` — Brick vertical
- `Detail Component: Backer Rod + Sealant` — At expansion joints
- `Filled Region: Air Space` — Clear/white, 1" to 2"
- `Filled Region: Brick` — Diagonal brick hatch

---

## 5. Stone / Manufactured Stone Veneer

### 5.1 Adhered Stone Veneer (Manufactured / Cultured Stone)

#### 5.1.1 Wall Assembly (Outside to Inside)

| Layer | Material | Thickness | Notes |
|-------|----------|-----------|-------|
| Manufactured stone units | Cement/aggregate cast stone | 3/4" to 2" | Irregular thickness |
| Setting bed mortar | Type S or N mortar | 1/2" average | Full coverage on back of stone |
| Scratch coat | Portland cement mortar | 1/2" minimum | Must embed lath, scored horizontally |
| Metal lath | Expanded metal or woven wire | 2.5 lb/SY minimum | Self-furred or furred 1/4" from substrate |
| WRB (two layers) | #15 felt or equivalent | Two layers required | First layer horizontal, second layer over lath |
| Sheathing | Plywood or OSB | 7/16" to 1/2" | — |
| Framing | Wood stud | 2x4 or 2x6 | 16" o.c. |
| Cavity insulation | Batt | Fills cavity | — |
| Interior gypsum | Drywall | 1/2" | — |

**Total cladding thickness:** Approximately 1-3/4" to 3" (mortar + stone)

#### 5.1.2 Critical Installation Requirements

**Lath embedding:** Metal lath must be embedded in the scratch coat with minimum 1/4" mortar between lath and substrate for 50% of the surface area.

**Scratch coat:** Minimum 1/2" thick (differs from stucco's 3/8" scratch coat requirement per ASTM C926). Score horizontally with notched trowel to create mechanical key.

**Setting mortar:** Full (100%) butter coverage on back of each stone unit. Nominal 1/2" thickness. Press and wiggle stone into scratch coat.

**Two layers of WRB:** Required for all adhered stone veneer installations. This is more stringent than other cladding types.

#### 5.1.3 Grade Clearance

- Minimum 4" above finished grade to bottom of stone
- Weep screed at base, attached to sheathing
- Weep screed must extend through WRB to drain any moisture

#### 5.1.4 Stone-to-Siding Transition

```
STONE-TO-SIDING TRANSITION:

     Siding (fiber cement, wood, etc.)
     |
     Z-flashing (1/4" gap above, not caulked)
     |
     ==================  <-- Transition line
     |
     Stone veneer (set in mortar)
     |
     Scratch coat over lath
     |
     Two-layer WRB (continuous behind both materials)
```

- Z-flashing at horizontal transition (stone below, siding above is most common)
- WRB must be continuous behind both cladding materials
- Upper siding overlaps Z-flashing
- 1/4" gap between siding and Z-flashing (drainage, not caulked)
- Stone terminates below Z-flashing with sealant at top

### 5.2 Anchored Stone Veneer (Natural Stone)

#### 5.2.1 Assembly

| Layer | Material | Thickness | Notes |
|-------|----------|-----------|-------|
| Natural stone | Granite, limestone, marble, slate | 1-1/4" to 4" | Full-depth or calibrated |
| Stainless steel anchors | Disc/dowel or strap anchors | — | 4 anchors per stone minimum |
| Air space | Clear cavity | 1" minimum | Drainage and pressure equalization |
| Continuous insulation | Rigid insulation | Per climate zone | — |
| WRB / air barrier | Fluid-applied or membrane | — | Continuous |
| Sheathing / backup wall | CMU, concrete, or steel stud | — | Structural backup |

**Anchor types:**
- Disc anchors: Stainless steel disc with dowel pin into stone edge
- Strap anchors: Bent stainless strap, one leg embedded in mortar, one bolted to structure
- Dowel anchors: Pin in drilled hole, secured with epoxy
- Kerf anchors: Blade fits into kerf (saw cut) in stone edge

**This system is a true rain screen:** Air space, drainage, pressure equalization.

### 5.3 Code Requirements

| Code | Section | Requirement |
|------|---------|-------------|
| IBC 1405.10 | Adhered masonry veneer | Maximum weight 15 PSF, maximum thickness per unit |
| IBC 1405.9 | Anchored stone veneer | Structural anchors, air space requirements |
| IRC R703.9 | Adhered masonry veneer | Installation per ASTM C1780 |
| ASTM C1780 | — | Installation standard for adhered stone veneer |
| ASTM C1670 | — | Specification for manufactured stone units |
| ASTM C1528 | — | Bond strength of adhered stone (50 PSI minimum) |

### 5.4 Common Failures

1. **Insufficient mortar coverage** — Only buttering perimeter of stone. Must be 100% coverage.
2. **Single layer WRB** — Code requires two layers for adhered stone. Moisture damage behind.
3. **Stone below grade** — Wicks ground moisture, efflorescence, freeze-thaw damage. Maintain 4" clearance.
4. **No weep screed** — Water trapped behind stone. Always terminate with weep screed at base.
5. **Inadequate scratch coat** — Too thin or not scored. Stone debonds. Minimum 1/2" with horizontal scoring.
6. **Missing transition flashing** — At stone-to-siding change. Z-flashing is mandatory.

### 5.5 Revit Detail Components

- `Detail Component: Manufactured Stone Unit` — Irregular profile, 1" to 2" thick
- `Detail Component: Natural Stone Panel` — Uniform thickness, calibrated
- `Detail Component: Metal Lath` — Expanded metal, zigzag profile
- `Detail Component: Scratch Coat` — 1/2" minimum, scored lines
- `Detail Component: Stone Anchor` — Disc, strap, or kerf type
- `Detail Component: Weep Screed` — Metal with drip and drainage holes
- `Filled Region: Mortar Bed` — Stippled hatch, 1/2" typical
- `Filled Region: Stone` — Solid or speckled hatch (varies by stone type)

---

## 6. Stucco / EIFS Details

### 6.1 Traditional Three-Coat Stucco

#### 6.1.1 Wall Assembly (Outside to Inside)

| Layer | Material | Thickness | Notes |
|-------|----------|-----------|-------|
| Finish coat | Colored portland cement plaster | 1/8" | Troweled to texture |
| Brown coat | Portland cement/sand plaster | 1/4" to 3/8" | Rodded straight |
| Scratch coat | Portland cement/sand plaster | 3/8" to 1/2" | Scored horizontally |
| Metal lath | 17-gauge expanded metal, self-furred | — | Crimped 1/4" off sheathing |
| WRB | Two layers #15 felt or Grade D paper | — | Over sheathing |
| Sheathing | Plywood or OSB | 7/16" to 1/2" | Structural substrate |
| Framing | Wood stud | 2x4 or 2x6 | 16" o.c. |
| Cavity insulation | Batt | Fills cavity | — |
| Vapor retarder | Per climate zone | — | — |
| Interior gypsum | Drywall | 1/2" | — |

**Total stucco thickness: 7/8" (minimum)**

**Weight: 10-12 PSF** (important for seismic and wind calculations)

#### 6.1.2 Coat Specifications

**Scratch coat (3/8" to 1/2"):**
- Mix: 1 part Portland cement, 2.25 to 4 parts sand
- Apply with force to embed lath
- Score horizontally with metal rake while wet
- Cure: Moist cure 48 hours minimum

**Brown coat (1/4" to 3/8"):**
- Apply after scratch coat has cured (minimum 48 hours, 7 days preferred)
- Rod with straightedge for flat, plumb surface
- Float to uniform texture
- Cure: Moist cure minimum 7 days

**Finish coat (1/8"):**
- Apply after brown coat fully cured (minimum 7 days)
- Contains integral color pigment
- Texture options: dash, sand float, smooth trowel, skip trowel, lace
- Do not apply in direct sun or high wind

#### 6.1.3 Wire Lath Specifications

- 17-gauge self-furred expanded metal lath (2.5 lb/SY minimum)
- OR 17-gauge woven wire (stucco netting), 1" x 1" or 1-1/2" x 1-1/2" mesh
- Self-furred lath crimped 1/4" off sheathing (allows scratch coat to flow behind)
- Fastened with roofing nails or staples at 6" o.c. max to each stud
- Lap minimum 1/2" at sides, 1" at ends
- Wrap corners: Lath wraps minimum 16" around corners (inside and outside)

### 6.2 One-Coat Stucco

| Layer | Material | Thickness | Notes |
|-------|----------|-----------|-------|
| One-coat base | Fiber-reinforced portland cement | 3/8" to 1/2" | Applied in one pass |
| Finish coat | Acrylic or cement-based | 1/16" to 1/8" | Color/texture coat |
| Metal lath | Self-furred | — | Same as three-coat |
| WRB | Two layers required | — | Same as three-coat |
| Sheathing + framing | Same as three-coat | — | — |

**Total thickness: 1/2" to 5/8"**
- Faster installation (one base coat vs two)
- Fiber reinforcement reduces cracking
- Common in California and Southwest residential

### 6.3 EIFS (Exterior Insulation and Finish System)

#### 6.3.1 Drainable EIFS Assembly (Outside to Inside) — Current Standard

| Layer | Material | Thickness | Notes |
|-------|----------|-----------|-------|
| Finish coat | Acrylic-based textured finish | 1/16" | Factory color |
| Base coat | Polymer-modified cement | 1/16" | Applied over mesh |
| Reinforcing mesh | Alkali-resistant fiberglass | — | Embedded in base coat |
| EPS insulation | Expanded polystyrene foam | 1" to 4" | Shaped/sanded to profile |
| Drainage plane | Grooved foam, mesh, or mat | 1/8" to 3/16" | Between insulation and WRB |
| Adhesive (or mech. fasteners) | EIFS adhesive or plastic fasteners | — | Attaches insulation to substrate |
| WRB | Fluid-applied or membrane | — | Over sheathing |
| Sheathing | Exterior gypsum or plywood | 1/2" to 5/8" | — |
| Framing | Wood or steel stud | — | — |
| Cavity insulation | Batt | Fills cavity | — |
| Interior gypsum | Drywall | 1/2" to 5/8" | — |

**Total EIFS cladding thickness: 1-1/4" to 4-1/4"** (depending on insulation)

**CRITICAL: All EIFS installations since ~2000 must be drainable.** Barrier EIFS (no drainage) is prohibited in new construction. The drainage plane allows any moisture that penetrates the finish to drain down to weep openings at the base.

#### 6.3.2 EIFS Key Details

**Base termination:**
- Starter track at base (perforated metal or PVC)
- Minimum 8" above grade (more than stucco's 4-6")
- Weep openings at starter track bottom
- Insulation does not contact ground

**At windows:**
- EIFS returns into window frame (sealant joint 1/4" to 3/8")
- Backwrap mesh: Mesh wraps around insulation edge at openings (minimum 2-1/2" onto substrate)
- Sill pan flashing beneath window, draining to EIFS exterior face
- No through-wall penetrations without sealant

### 6.4 Stucco Control Joints

**Spacing rules:**
- Maximum panel area: 144 SF
- Maximum dimension in any direction: 18'
- Maximum length-to-width ratio: 2.5:1
- At all re-entrant corners (inside corners of L-shaped walls)
- At all construction joints in the substrate
- Aligned with window/door edges where possible

**Control joint construction:**
- Single-piece prefabricated galvanized or PVC accessory
- V-groove or rectangular profile
- Installed on face of lath (not behind)
- Does not interrupt WRB

**Expansion joint construction:**
- Two-piece (back-to-back casing beads) OR manufactured slip-type
- Fastened to substrate below lath and WRB
- Accommodates structural movement (not just shrinkage)
- Used at: floor lines, building separations, material changes in substrate

### 6.5 Stucco Weep Screed (Foundation Termination)

```
STUCCO WEEP SCREED DETAIL:

     Stucco (7/8")
     |  Lath
     |  |  WRB
     |  |  | Sheathing
     ====  ======
     ====  ======
     ====  ======
     [==========]  <-- Weep screed (3-1/2" nailing flange)
      \               Drip edge extends 1/2" below sheathing
       \              Weep holes in ground flange
        |
     4" min. above grade
        |
     ~~~~~~~~  Grade
```

**Requirements:**
- 26-gauge galvanized steel minimum (IBC requirement)
- 3-1/2" nailing flange (IBC requirement for three-coat stucco)
- Weep holes in ground flange for drainage
- Minimum 4" above grade (6" preferred)
- Lath overlaps nailing flange minimum 1"
- WRB overlaps weep screed top flange

### 6.6 Stucco at Windows (Casing Bead / J-Mold)

**Casing bead:** Metal trim that terminates stucco at window/door frames
- 3/4" or 7/8" depth (matches stucco thickness)
- Perforated flange embedded in stucco
- Exposed edge creates clean termination line
- 1/4" sealant joint between casing bead and window frame

**J-Mold (J-Trim):** Alternative termination
- J-channel receives stucco edge
- Often includes weep holes (J-Weep)
- Used at sills for drainage

### 6.7 Stucco-to-Different-Material Transition

**Stucco-to-siding (horizontal transition):**
- Z-flashing at transition line
- Upper material (siding) overlaps Z-flashing top leg
- Lower material (stucco) terminates at Z-flashing bottom leg
- Casing bead terminates stucco at Z-flashing
- 1/4" gap between siding and Z-flashing (not caulked)
- WRB continuous behind both materials

**Stucco-to-stone (horizontal transition):**
- Control joint or casing bead at transition
- Both materials share continuous WRB and lath system
- Scratch coat is common to both (stone sets into scratch coat)
- Sealant joint at interface

### 6.8 Code Requirements

| Code | Section | Requirement |
|------|---------|-------------|
| IBC 2510 | Stucco (portland cement plaster) | Installation per ASTM C926, lath per ASTM C1063 |
| IBC 2510.6 | Drainage | 3/16" drainage cavity or 90% efficiency (Zones A, C) |
| IBC 1407 | EIFS | Drainable EIFS required; NFPA 285 fire test |
| IRC R703.7 | Stucco | Two-layer WRB, weep screed 4" above grade |
| ASTM C926 | — | Application of portland cement plaster |
| ASTM C1063 | — | Installation of lathing and furring |
| ASTM E2568 | — | EIFS — standard spec |
| NFPA 285 | — | Fire test for exterior wall assemblies (applies to EIFS with foam) |

### 6.9 Climate Zone Considerations

| Climate | Three-Coat Stucco | EIFS |
|---------|-------------------|------|
| Hot-dry (zones 1-3B) | Excellent performance, minimal moisture risk | Good; drainage still required |
| Hot-humid (zones 1-3A) | Requires drainage gap per IBC 2510.6 | Drainable EIFS mandatory |
| Mixed (zone 4) | Drainage recommended | Drainable mandatory |
| Cold (zones 5-8) | Freeze-thaw risk; drainage critical | Continuous insulation benefit; vapor analysis needed |
| Marine (zone 4C) | Drainage gap required per IBC | Drainable mandatory |

### 6.10 Common Failures

1. **Barrier EIFS (non-drainable)** — Trapped moisture causes catastrophic rot. All modern EIFS must be drainable.
2. **Missing control joints** — Shrinkage cracks across large panels. Never exceed 144 SF without a control joint.
3. **Weep screed too low** — Ground splash wets stucco base. Maintain 4" minimum (6" preferred).
4. **Insufficient cure time** — Brown coat applied too soon over scratch coat. Wait minimum 48 hours (7 days preferred).
5. **Single WRB layer** — Code requires two layers behind stucco/EIFS on wood frame.
6. **EIFS without NFPA 285 test** — Building code violation for buildings over 40'. Verify assembly has passing test report.
7. **Control joints misaligned with openings** — Cracks propagate from window corners. Align joints with opening edges.

### 6.11 Revit Detail Components

- `Detail Component: Stucco Three-Coat` — 7/8" total profile with coat lines
- `Detail Component: Stucco One-Coat` — 1/2" profile
- `Detail Component: EIFS Assembly` — EPS + mesh + base coat + finish
- `Detail Component: Metal Lath` — Expanded or woven wire zigzag
- `Detail Component: Weep Screed` — Foundation termination profile
- `Detail Component: Casing Bead` — 3/4" or 7/8" depth
- `Detail Component: J-Mold` — J-channel profile
- `Detail Component: Control Joint` — V-groove or rectangular
- `Detail Component: Expansion Joint` — Back-to-back casing beads
- `Filled Region: Stucco` — Light stipple hatch
- `Filled Region: EPS Insulation` — Triangle/diamond hatch
- `Filled Region: Drainage Plane` — Dashed line behind insulation

---

## 7. Modern Mixed Cladding Transitions

### 7.1 Z-Flashing at Horizontal Material Changes

The most common transition detail. Used wherever an upper cladding material sits above a lower cladding material with a horizontal line of demarcation.

```
Z-FLASHING TRANSITION (SECTION):

     Upper cladding (siding, panel, etc.)
     |
     1/4" gap (NOT caulked — drainage)
     |
     ========================  <-- Z-flashing top leg (under WRB)
     |                      |
     |  Z-flashing vertical |  <-- Visible drip edge
     |                      |
     ========================  <-- Z-flashing bottom leg (over lower material)
     |
     Lower cladding (stone, stucco, brick, etc.)
```

**Z-flashing specifications:**
- Material: 26 ga galvanized steel, aluminum (.019"), or stainless steel
- Top leg: Minimum 2", tucked under WRB of upper wall section
- Bottom leg: Minimum 2", extending over face of lower cladding
- Drip edge: 1/4" to 3/8" kick-out at bottom
- Top gap: 1/4" between upper siding and Z-flashing (NOT caulked — allows drainage)
- Bottom overlap: Lower cladding terminates 1/4" below Z-flashing bottom leg

### 7.2 Siding-to-Stone Transition

**Most common residential transition.** Typically stone veneer below (wainscot), siding above.

**Assembly requirements:**
- Continuous WRB behind both materials (lapped shingle-style)
- Z-flashing at transition line, integrated with WRB
- Stone terminates at weep screed or casing bead below Z-flashing
- Two layers WRB required behind stone section (more stringent than siding)
- Siding starter strip above Z-flashing (same as base-of-wall starter)
- Maintain 1/4" drainage gap between siding and Z-flashing

**Common mistake:** Caulking the gap above the Z-flashing. This traps water behind the siding. The gap must remain open for drainage.

### 7.3 Metal Panel-to-Wood Transition

**Assembly:**
- Continuous WRB/air barrier behind both materials
- Transition trim (brake-formed receiver channel)
- Metal panel terminates into receiver channel with sealant
- Wood cladding begins above/below with its own attachment system
- Maintain separate drainage paths for each cladding zone

**Thermal considerations:** Metal panel subgirt system and wood furring may be at different depths. Transition trim bridges the depth difference.

### 7.4 Stucco-to-Siding Transition

**Horizontal transition (stucco below):**
- Casing bead terminates stucco at transition line
- Z-flashing over casing bead
- Siding begins above with starter strip
- WRB continuous, lapped over Z-flashing top leg

**Vertical transition (different materials side by side):**
- Casing bead on stucco side
- J-trim or corner trim on siding side
- 1/2" sealant joint between (backer rod + sealant)
- WRB continuous behind both, with extra flashing strip at joint line

### 7.5 Material Change at Floor Line

Common in multi-story mixed-use: different cladding on each floor (e.g., brick below, metal panel above).

**Key principles:**
- Floor line provides natural structural break
- Horizontal transition with through-wall flashing at floor edge
- Each cladding system independently supported (own shelf angle, clips, or attachment)
- Sealant joint at interface allows differential movement
- Fire containment: Perimeter fire safing at each floor edge per IBC 714.4

### 7.6 Material Change at Corners

**Outside corner (two different materials meeting):**
- Each material terminates at corner trim on its respective side
- Corner trim: Brake-formed or extruded, sized to cover both material depths
- Sealant joint where trim meets each cladding
- WRB wraps corner continuously (minimum 6")

**Inside corner (two different materials meeting):**
- Each material terminates with its own J-trim or casing bead
- Sealant joint at corner line
- Simpler than outside corner (less water exposure)

### 7.7 Drip Edge and Kickout Flashing

**At every horizontal transition and at roof-to-wall intersections:**
- Kickout flashing: Diverts water from roof into gutter, preventing concentrated flow onto lower wall
- 45-degree bend minimum
- Must be present at every roof-to-wall termination where gutter begins

**Drip edge at transitions:**
- All Z-flashings must have a drip edge (1/4" bend outward at bottom)
- Prevents water from traveling back underneath via surface tension

### 7.8 Code Requirements for Transitions

| Code | Section | Requirement |
|------|---------|-------------|
| IBC 1402.2 | — | Weather protection at all exterior wall assemblies |
| IBC 1403.2 | — | WRB continuous, including at transitions |
| IBC 714.4 | — | Perimeter fire containment at floor lines |
| IRC R703.4 | Flashing | Required at all transitions between dissimilar materials |
| AAMA 711 | — | Voluntary standard for sill pan flashing at transitions |

### 7.9 Common Failures

1. **No Z-flashing at material change** — Single most common defect. Water runs behind lower material.
2. **Caulked drainage gap** — Gap above Z-flashing must remain open. Caulk traps water.
3. **WRB discontinuity at transition** — WRB must be continuous behind both materials. Overlap all layers shingle-style.
4. **Mismatched depths** — Different claddings at different depths create ledges that collect water. Transition trim must bridge cleanly.
5. **Missing kickout flashing** — Concentrated roof water destroys cladding at roof-wall transitions.
6. **No fire safing at floor line** — Code violation in Type I-IV construction. Mineral wool safing required at each floor.

### 7.10 Revit Detail Components

- `Detail Component: Z-Flashing` — Profile with top leg, vertical, bottom leg, drip
- `Detail Component: Kickout Flashing` — 45-degree diverter
- `Detail Component: Transition Trim` — Brake-formed channel bridging two depths
- `Detail Component: Casing Bead` — Stucco termination at transitions
- `Detail Component: Sealant Joint` — Backer rod + sealant profile
- `Detail Component: Fire Safing` — Mineral wool at floor edge
- `Detail Component: Corner Trim` — Inside and outside brake-formed
- `Filled Region: Each Cladding Material` — Distinct hatch per material type

---

## Appendix A: Quick Reference — Minimum Dimensions

| Dimension | Value | Where |
|-----------|-------|-------|
| Rain screen air gap | 3/4" typical (3/8" min) | Behind all rain screen cladding |
| Brick veneer air space | 1" min, 2" preferred | Between brick and sheathing |
| Stucco total thickness (3-coat) | 7/8" | Scratch + brown + finish |
| Stucco total thickness (1-coat) | 1/2" to 5/8" | One base + finish |
| EIFS insulation | 1" minimum | EPS or mineral wool |
| Fiber cement lap overlap | 1-1/4" | Each course over previous |
| Fiber cement above grade | 6" minimum | Bottom of siding to dirt |
| Stone veneer above grade | 4" minimum | Bottom of stone to dirt |
| Stucco weep screed above grade | 4" min (6" preferred) | Bottom of weep screed to grade |
| EIFS above grade | 8" minimum | Base of system to grade |
| Brick weep hole spacing | 24" o.c. max (16" preferred) | Above all flashings |
| Brick wall tie spacing | 32" horiz. x 24" vert. max | 2.67 SF max per tie |
| Brick expansion joint spacing | 25' max (20' with openings) | Vertical joints |
| Stucco control joint max area | 144 SF | Per panel |
| Stucco control joint max length | 18' in any direction | 2.5:1 aspect ratio max |
| Sealant joint depth | 1/2 of joint width | With backer rod |
| Z-flashing leg length | 2" minimum each side | Top and bottom legs |
| Window sill slope | 5 degrees (1/4" per inch) | All sill flashings |

## Appendix B: Quick Reference — Weight of Cladding

| Material | Weight (PSF) | Notes |
|----------|-------------|-------|
| Fiber cement lap siding (5/16") | 2.5 | Lightweight |
| Vinyl siding | 0.5 | Lightest common cladding |
| Wood lap siding (3/4") | 2.0-3.0 | Varies by species |
| Metal panel (24 ga steel) | 1.5-2.0 | Lightweight |
| ACM panel (4mm) | 2.2 | Per SF of panel |
| Three-coat stucco (7/8") | 10-12 | Heavy — seismic concern |
| One-coat stucco (1/2") | 6-8 | Lighter than three-coat |
| EIFS (2" EPS) | 1.0-2.0 | Very lightweight |
| Brick veneer (3-5/8") | 40-45 | Heaviest — requires foundation support |
| Manufactured stone veneer | 10-15 | Weight limit 15 PSF (IBC) |
| Natural stone veneer (1-1/4") | 15-25 | Requires structural anchors |

## Appendix C: Quick Reference — Fire Testing Requirements

| System | NFPA 285 Required? | Combustible? | IBC Reference |
|--------|-------------------|-------------|---------------|
| Rain screen with combustible CI | Yes (Type I-IV) | Yes | IBC 1402.5 |
| Metal panel with mineral wool CI | No (non-combustible) | No | IBC 1403 |
| EIFS with EPS | Yes | Yes | IBC 1407 |
| ACM with PE core | Fails NFPA 285 | Yes — PROHIBITED | IBC 1407 |
| ACM with FR/mineral core | Must pass | Tested assembly | IBC 1407 |
| Three-coat stucco on wood frame | No | N/A (stucco is non-combustible) | IBC 2510 |
| Brick veneer | No | Non-combustible | IBC 1405 |
| Fiber cement siding | No (non-combustible) | No | IBC 1404 |

## Appendix D: Material Hatches for Revit Details

| Material | Hatch Pattern | Revit Pattern Name | Notes |
|----------|--------------|-------------------|-------|
| Brick (section) | Diagonal crosshatch | `Brick` or `Masonry - Brick` | 45-degree lines |
| Stone (section) | Random rubble or ashlar | `Stone` or `Masonry - Stone` | Irregular for rubble |
| Stucco (section) | Fine stipple or dots | `Plaster` or custom stipple | Light density |
| Concrete (section) | Triangle aggregate | `Concrete` | Standard |
| Metal panel (section) | Solid fill (thin) | Solid black line | 0.024" to 0.032" |
| Insulation - rigid | Triangle/diamond | `Insulation - Rigid` | — |
| Insulation - batt | Wavy lines | `Insulation - Batt` | — |
| Air space | Blank/white | No fill | Dimension it clearly |
| Wood (section) | End grain circles | `Wood - End Grain` | For studs in section |
| Gypsum board | Solid thin line | Thin solid region | 1/2" or 5/8" |
| EPS foam | Small triangles | `Insulation - Rigid` | Or custom EPS |
| Fiber cement | Dense dots | Custom or `Concrete` variant | Thinner than concrete |
| Mortar | Stipple | `Plaster` | Same as stucco |
| Sealant | Solid fill (dark) | Solid dark region | Small profile |
| Membrane/WRB | Heavy dashed line | `Membrane` or heavy line | Bold for visibility |
| Flashing | Solid heavy line | Heavy solid line | Thickest line weight |
