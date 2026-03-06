# Residential Wood-Frame Construction Details

> Comprehensive detail knowledge for single-family homes, duplexes, and small multi-family wood-frame construction.
> Covers IRC-compliant assemblies from foundation to ridge. Every layer, every fastener, every code reference.
> Generated: 2026-03-05

---

## TABLE OF CONTENTS

1. [Wood Frame Wall Sections](#1-wood-frame-wall-sections)
2. [Wood Frame Floor Details](#2-wood-frame-floor-details)
3. [Wood Frame Roof Details](#3-wood-frame-roof-details)
4. [Foundation Details for Wood Frame](#4-foundation-details-for-wood-frame)
5. [Window and Door in Wood Frame](#5-window-and-door-in-wood-frame)
6. [Wood Frame Connection Details](#6-wood-frame-connection-details)

---

## REVIT COMPONENT QUICK REFERENCE

Components from the 87-family BD Architect palette used throughout this document:

| Component | Primary Use in Wood Frame |
|-----------|--------------------------|
| **Dimension Lumber-Section** | Studs, plates, headers, joists, rafters, blocking |
| **Nominal Cut Lumber-Section** | Rough-sawn, exposed timber, ledger boards |
| **06-FRAMING-WOOD-Section-Stretch1** | Non-standard blocking, cripples, custom lengths |
| **Plywood-Section** | Wall sheathing, subfloor, roof deck |
| **06-SHEATHING-Plywood-Section1** | Alternate plywood with keynote mapping |
| **Gypsum Wallboard-Section** | Interior finish (1/2" standard) |
| **Gypsum Wallboard-Section1** | 5/8" Type X for fire-rated assemblies |
| **Rigid Insulation-Section** | Exterior CI, foundation insulation, thermal breaks |
| **07-INSULATION-RIGID** | Alternate rigid insulation with keynote |
| **Siding-Wood Bevel** | Lap siding (wood, fiber cement representation) |
| **Gypsum Plaster-Section** | Stucco finish coat |
| **Gypsum Sheathing-Section** | Exterior gypsum behind stucco/EIFS |
| **Asphalt Shingles Dynamic-2** | Roofing shingles |
| **Underlayment** | Roofing underlayment (felt/synthetic) |
| **Roof Ridge Vent** | Ridge ventilation |
| **Common Wood Nails-Side** | Fastener callouts in details |
| **Caulking-Section** | Sealant joints |
| **Joint Sealant and Backer Rod-Section** | Expansion joints, control joints |
| **Resilient Topset Base-Section** | Interior base trim |
| **Reinf Bar Section** | Rebar in foundation/concrete elements |
| **Anchor Bolt** | Foundation-to-sill connections |
| **Window Head - Wood Double Hung** | Window head detail component |
| **Window Sill - Wood Double Hung** | Window sill detail component |
| **Window Jamb - Wood Double Hung** | Window jamb detail component |
| **Window Head - Wood Casement** | Casement window head |
| **Window Sill - Wood Casement** | Casement window sill |
| **Window Jamb - Wood Casement** | Casement window jamb |

**Components NOT in BD Architect library (create or use detail lines/filled regions):**
- Batt insulation (use filled region with insulation hatch pattern)
- Housewrap/WRB membrane (use detail line, dashed)
- Flashing (use detail line or thin filled region)
- Sill seal/gasket (use detail line)
- Termite shield (use detail line with note)
- Hurricane straps/ties (use detail line with note)
- Wire lath (use detail line, zigzag)
- Vapor retarder (use detail line, dashed)
- Self-adhered membrane (use filled region, solid thin)

---

## 1. WOOD FRAME WALL SECTIONS

### 1.1 Standard 2x4 Wall Assembly

**Layer Stack (Interior to Exterior):**

| # | Layer | Material | Thickness | Notes |
|---|-------|----------|-----------|-------|
| 1 | Interior finish | 1/2" GWB | 0.5" | Single layer standard |
| 2 | Studs | 2x4 SPF @ 16" o.c. | 3.5" (actual) | IRC R602.3(5) |
| 3 | Cavity insulation | Fiberglass batt R-13 or R-15 | 3.5" | Fills full cavity |
| 4 | Sheathing | 7/16" OSB or 1/2" plywood | 7/16" - 1/2" | Structural sheathing |
| 5 | WRB | Housewrap (Tyvek, etc.) | membrane | IRC R703.2 |
| 6 | Exterior finish | Varies (see cladding variants) | varies | See below |

**Total wall thickness:** ~4.5" + cladding
**Whole-wall R-value:** ~R-11 to R-13 (depending on framing factor)

**Revit Components:**
- Gypsum Wallboard-Section (interior)
- Dimension Lumber-Section (2x4 studs, plates)
- Filled region with insulation hatch (cavity)
- Plywood-Section (sheathing)
- Detail line dashed (WRB)
- Siding-Wood Bevel or Gypsum Plaster-Section (exterior)

**Critical Dimensions:**
- Stud spacing: 16" o.c. (standard) per IRC R602.3(5)
- Bottom plate: single 2x4
- Top plate: double 2x4 (IRC R602.3.2)
- Stud height: 8'-0" typical (max 10' per IRC Table R602.3(5))

**Fastener Schedule (IRC Table R602.3(1)):**
- Studs to sole plate: 2-16d common (3.5" x 0.162") end-nailed, or 4-8d toenailed
- Top plate to stud: 2-16d common end-nailed
- Double top plate splice: 8-16d common, minimum 48" offset between joints
- Sheathing to framing: 8d common (2.5" x 0.131") @ 6" o.c. edges, 12" o.c. field

**Code Requirements:**
- IRC R602.3: Design and construction of exterior walls
- IRC R602.3(5): Stud size, height, and spacing table
- IRC R602.10: Wall bracing
- IRC R703.2: WRB required behind exterior cladding

**Common Mistakes:**
- Missing WRB behind cladding
- Compressing batt insulation (reduces R-value)
- Gaps at top/bottom of batt insulation
- Not staggering double top plate joints by minimum 48"

---

### 1.2 Standard 2x6 Wall Assembly

**Layer Stack (Interior to Exterior):**

| # | Layer | Material | Thickness | Notes |
|---|-------|----------|-----------|-------|
| 1 | Interior finish | 1/2" GWB | 0.5" | |
| 2 | Vapor retarder | Kraft-faced or poly (CZ 5+) | membrane | IRC R702.7 |
| 3 | Studs | 2x6 SPF @ 16" o.c. | 5.5" (actual) | |
| 4 | Cavity insulation | R-19 or R-21 fiberglass batt | 5.5" | Full cavity fill |
| 5 | Sheathing | 7/16" OSB or 1/2" plywood | 7/16" - 1/2" | |
| 6 | WRB | Housewrap | membrane | |
| 7 | Exterior finish | Varies | varies | |

**Total wall thickness:** ~6.5" + cladding
**Whole-wall R-value:** ~R-16 to R-18

**Key Differences from 2x4:**
- 57% deeper cavity = 50% more insulation
- Allows R-19/R-21 batt vs R-13/R-15
- Required in many climate zones for energy code compliance
- Better header insulation options (insulated headers)

**Code Requirements:**
- IRC Table N1102.1.2: Minimum R-20 wall or R-13 + R-5 ci (Climate Zones 4-5)
- IRC Table N1102.1.2: Minimum R-20+R-5 ci or R-13+R-10 ci (Climate Zones 6-8)

---

### 1.3 Advanced Framing / OVE (Optimum Value Engineering)

**Layer Stack (Interior to Exterior):**

| # | Layer | Material | Thickness | Notes |
|---|-------|----------|-----------|-------|
| 1 | Interior finish | 1/2" GWB | 0.5" | |
| 2 | Studs | 2x6 SPF @ 24" o.c. | 5.5" | Single top plate |
| 3 | Cavity insulation | R-21 fiberglass batt | 5.5" | Unfaced preferred |
| 4 | Sheathing | 7/16" OSB or 1/2" plywood | 7/16" - 1/2" | |
| 5 | WRB | Housewrap | membrane | |
| 6 | Exterior finish | Varies | varies | |

**OVE Key Techniques:**
- 2x6 studs @ 24" o.c. (vs 16" standard)
- Single top plate (with metal plate connectors at joints and above openings)
- Two-stud corners (with drywall clips or scrap backing)
- Single headers (insulated) -- no jack studs where possible
- Inline framing (studs, joists, rafters all stack on 24" module)
- Minimal cripple studs
- No double studs at openings (use hangers instead of jack studs)

**Benefits:**
- 5-10% less lumber (board-feet)
- 30% fewer pieces to cut and install
- Framing factor drops from ~25% to ~15%
- 60% deeper cavity than 2x4 = 60% more insulation
- Reduced thermal bridging = better whole-wall R-value

**Whole-wall R-value:** ~R-19 to R-21 (vs R-16-18 for conventional 2x6 @ 16")

**Code Requirements:**
- IRC R602.3(5): 2x6 @ 24" o.c. permitted for single-story and upper story of multi-story
- IRC R602.3.2: Single top plate permitted with approved connectors
- IRC R602.7: Headers in 24" o.c. framing (single member with insulation)

**Common Mistakes:**
- Not aligning floor joists, studs, and rafters on 24" module
- Forgetting metal plate connectors at single top plate joints
- Using three-stud corners instead of two-stud with clips
- Not providing adequate bracing (consult IRC R602.10)

---

### 1.4 Exterior Cladding Variants

#### 1.4.1 Vinyl Siding

**Additional Layers (outside WRB):**

| Layer | Material | Thickness |
|-------|----------|-----------|
| Vinyl siding | PVC panels | 0.040" - 0.046" |

**Notes:**
- No additional furring required (nails to sheathing through WRB)
- Hang loosely -- do not nail tight (allow thermal expansion)
- 1/4" gap at end joints
- Lock-and-nail installation -- bottom locks, top nails
- Starter strip at base, J-channel at openings and terminations
- No Revit detail component -- use detail line or thin filled region

**Revit Drawing:** Detail lines for siding profile; note "VINYL SIDING" with leader

---

#### 1.4.2 Wood Lap Siding / Fiber Cement (Hardie Board)

**Additional Layers (outside WRB):**

| Layer | Material | Thickness |
|-------|----------|-----------|
| Siding | Wood bevel or fiber cement lap | 7/16" - 5/8" |

**Revit Component:** Siding-Wood Bevel

**Installation:**
- Minimum 1-1/4" overlap between courses
- Blind nail at top (wood) or face nail 1" from bottom (fiber cement)
- 6d or 8d corrosion-resistant nails
- Minimum 6" clearance from grade
- Flash all butt joints with WRB tape or caulk

**Fiber Cement Specifics (James Hardie):**
- Minimum 7/16" OSB or 1/2" plywood substrate for nailing
- 6d corrosion-resistant siding nails, 2" long minimum
- Maximum 24" o.c. stud spacing for direct attachment
- Rainscreen furring (3/4" min) recommended over foam CI
- 180-day maximum weather exposure before cladding (if using ZIP)

---

#### 1.4.3 Stucco Over Wood Frame (Three-Coat System)

**Additional Layers (outside WRB):**

| # | Layer | Material | Thickness |
|---|-------|----------|-----------|
| 1 | WRB | 2 layers asphalt-saturated felt (Grade D) | 2 x membrane | IRC R703.2, R703.7.3 |
| 2 | Lath | Self-furring galv. metal lath (2.5 lb/sy min) | 1/4" standoff | |
| 3 | Scratch coat | Portland cement plaster | 3/8" | Score horizontally |
| 4 | Brown coat | Portland cement plaster | 3/8" | Straightedge flat/plumb |
| 5 | Finish coat | Finish plaster (integral color) | 1/8" | Various textures |

**Total stucco buildup:** ~7/8" (3/8" + 3/8" + 1/8")

**Revit Components:**
- Detail line (WRB layers -- 2 lines, dashed)
- Detail line zigzag (wire lath)
- Gypsum Plaster-Section (stucco body)

**Code Requirements:**
- IRC R703.7: Exterior plaster (stucco) requirements
- IRC R703.7.3: Two layers of WRB required behind lath
- Weep screed at base, min 4" above grade (IRC R703.7.2.1)
- Control joints at max 144 sq ft panels, max 18' in any direction

**Common Mistakes:**
- Single layer WRB (two layers required by code)
- Missing weep screed at base -- traps water
- No control joints -- causes cracking
- Lath not lapped minimum 1" at joints
- Not letting scratch coat cure 48 hours before brown coat

---

#### 1.4.4 Brick Veneer Over Wood Frame

**Additional Layers (outside WRB):**

| # | Layer | Material | Thickness |
|---|-------|----------|-----------|
| 1 | WRB | Housewrap or felt | membrane |
| 2 | Air space | Minimum 1" (2" preferred) | 1" - 2" | CRITICAL -- never fill |
| 3 | Brick ties | Corrugated metal, 22 ga min | -- | 1 per 3.5 sq ft |
| 4 | Brick veneer | Modular brick | 3-5/8" (nom. 4") |

**Total wall thickness:** ~10.5" - 12" (2x4) or ~12.5" - 14" (2x6)

**Critical Details:**
- Foundation must support brick independently (min 2/3 brick width bearing)
- Shelf angle NOT used in residential (brick bears on foundation ledge)
- Air space is drainage cavity -- never fill with mortar droppings
- Weep holes: min 3/16" diameter @ max 33" o.c. above flashing (IRC R703.7.6)
- Through-wall flashing at base, above openings, below sills
- Brick ties: corrugated metal, 22 ga, 1 per 3.5 sq ft max (IRC R703.8.4)

**Revit Components:**
- All standard wall components
- Filled region with brick hatch (45-degree cross-hatch)
- Detail lines for ties, flashing
- Note: No brick veneer detail item in BD Architect library

**Code Requirements:**
- IRC R703.8: Brick veneer requirements
- IRC R703.8.4: Anchorage -- ties @ 24" o.c. vert, 32" o.c. horiz (max 3.5 sq ft)
- IRC R703.8.4.1: Air space 1" minimum, 4-1/2" maximum

**Common Mistakes:**
- Mortar droppings blocking weep holes (use mesh mortar catchers)
- Missing flashing at window heads/sills
- Air space less than 1" (mortar squeeze-out)
- Ties too far apart or wrong type
- No bond break between brick and foundation

---

#### 1.4.5 Stone Veneer (Adhered and Anchored)

**Adhered (Thin) Stone Veneer -- Additional Layers:**

| # | Layer | Material | Thickness |
|---|-------|----------|-----------|
| 1 | WRB | 2 layers felt or equivalent | membrane |
| 2 | Lath | Galvanized metal lath | 1/4" standoff |
| 3 | Scratch coat | Type S morite/Portland plaster | 1/2" - 3/4" |
| 4 | Mortar bed | Type S mortar | 1/2" - 3/4" |
| 5 | Stone veneer | Manufactured or natural thin stone | 3/4" - 1-1/2" |

**Max weight:** 15 psf (adhered) per IRC R703.9
**Total veneer buildup:** ~2" - 3"

**Anchored Stone Veneer:**
- Same air space and tie requirements as brick veneer
- Requires foundation ledge support
- Max height typically limited by engineering

---

### 1.5 Sheathing Options

#### 1.5.1 OSB (Oriented Strand Board)
- Thickness: 7/16" (walls), 3/4" (subfloor), 7/16" - 5/8" (roof)
- Most common/economical wall sheathing
- Structural rating: meets IRC wall bracing requirements
- Moisture sensitive -- protect from prolonged wetting
- Nail: 8d common @ 6" edges, 12" field

#### 1.5.2 Plywood
- Thickness: 1/2" (walls), 3/4" (subfloor), 1/2" - 5/8" (roof)
- Better moisture tolerance than OSB
- Higher nail withdrawal resistance
- More expensive than OSB
- Nail: Same as OSB per IRC Table R602.3(1)

**Revit Component:** Plywood-Section (both OSB and plywood)

#### 1.5.3 ZIP System (Integrated WRB)
- 7/16" or 1/2" engineered wood panel with factory-applied WRB
- Seams sealed with ZIP tape (3-3/4" wide)
- Eliminates separate housewrap step
- 180-day weather exposure rating (ESR-3373)
- Structural 1 rated
- Flash window/door openings with ZIP stretch tape at corners/sills
- Higher material cost, lower labor cost

**Revit Drawing:** Use Plywood-Section; add note "ZIP SYSTEM SHEATHING W/ INTEGRATED WRB"

#### 1.5.4 Rigid Foam Sheathing
- XPS: R-5 per inch (1", 1.5", 2" common)
- Polyiso: R-6.5 per inch (but diminished in cold)
- EPS: R-4 per inch
- NOT structural -- must be combined with bracing (let-in 1x4 or metal T-bracing)
- Must use furring strips for cladding attachment if over 1" thick

**Revit Component:** Rigid Insulation-Section or 07-INSULATION-RIGID

---

### 1.6 WRB (Weather-Resistive Barrier) Options

| Type | Product Examples | Permeability | Notes |
|------|-----------------|--------------|-------|
| **Housewrap** | Tyvek, Typar | 50+ perms | Most common, stapled to sheathing |
| **Asphalt felt** | #15 felt (Grade D) | 5-10 perms (dry) | Required 2 layers behind stucco |
| **Fluid-applied** | Prosoco R-Guard, Henry | 10-15 perms | Roller/spray applied, seamless |
| **Integrated** | ZIP System | 12-16 perms | Built into sheathing panel |
| **Self-adhered** | Grace Vycor, Blueskin | 1-8 perms | Peel-and-stick, window/door openings |

**IRC R703.2:** Exterior walls shall have WRB that is water-resistive and allows passage of water vapor.

---

### 1.7 Insulation Options for Wood-Frame Walls

| Type | R-Value/inch | 2x4 (3.5") | 2x6 (5.5") | Notes |
|------|-------------|------------|------------|-------|
| **Fiberglass batt** | R-3.2-4.3 | R-13 / R-15 | R-19 / R-21 | Most common, cheapest |
| **Mineral wool batt** | R-3.7-4.2 | R-15 | R-23 | Better fire/sound, hydrophobic |
| **Blown cellulose** | R-3.5-3.8 | R-12-13 | R-19-21 | Dense-pack for walls, recycled content |
| **Open-cell SPF** | R-3.6-3.8 | R-13 | R-20 | Air barrier at 3.75"+, vapor open |
| **Closed-cell SPF** | R-6.5-7.0 | R-23-25 | R-36-39 | Air + vapor barrier at 1.5", structural |
| **Rigid XPS (ext. CI)** | R-5/in | Added to cavity R | Added to cavity R | Continuous insulation |
| **Rigid polyiso (ext. CI)** | R-6.5/in | Added to cavity R | Added to cavity R | Best R/inch for CI |

**Vapor Retarder Requirements (IRC R702.7):**
- Climate Zones 5-Marine 4: Class I or II vapor retarder (kraft-faced batt or poly)
- Climate Zones 1-4: Class III (latex paint on GWB qualifies)
- Closed-cell SPF at 1.5" thickness = Class II vapor retarder (no additional needed)
- Open-cell SPF: vapor retarder required in CZ 5+

**Revit Drawing Notes:**
- Batt insulation: Use filled region with "Insulation - Batt" hatch pattern
- Spray foam: Use filled region with stipple/dot pattern
- Rigid CI: Use Rigid Insulation-Section component

---

### 1.8 Interior Finish Variants

#### Single Layer GWB (Standard)
- 1/2" regular GWB
- Typical residential (non-rated)
- **Revit:** Gypsum Wallboard-Section

#### Double Layer GWB (Fire-Rated / Sound-Rated)
- 2x 5/8" Type X GWB
- Attached garage to house separation (IRC R302.6): 1/2" GWB minimum (one side)
- 1-hour fire rating: 2x 5/8" Type X on one side
- Sound: first layer screwed, second layer glued (Green Glue) for STC improvement
- **Revit:** 2x Gypsum Wallboard-Section1

#### Plaster (Traditional)
- 3-coat plaster over wood lath or blue board
- Not common in new construction
- Historical renovation details
- Total thickness: ~7/8" (3/8" scratch + 3/8" brown + 1/8" finish)

---

## 2. WOOD FRAME FLOOR DETAILS

### 2.1 Floor Joist Bearing on Foundation Wall

**Layer Stack (Top to Bottom):**

| # | Component | Material | Dimension | Notes |
|---|-----------|----------|-----------|-------|
| 1 | Subfloor | 3/4" T&G plywood/OSB | 23/32" actual | APA rated, glue + nail |
| 2 | Floor joists | 2x10 or 2x12 SPF @ 16" o.c. | 9.25" or 11.25" | Per IRC span tables |
| 3 | Rim/band joist | Same depth as floor joists | matches joists | Nailed through to joists |
| 4 | Sill plate | Pressure-treated 2x6 or 2x8 | 1.5" x 5.5/7.25" | PT lumber required |
| 5 | Sill seal | Foam gasket | 1/4" | Air seal at sill-foundation joint |
| 6 | Anchor bolts | 1/2" diameter J-bolt or wedge | 7" min embed | 6'-0" o.c. max, 12" from ends |
| 7 | Foundation wall | Concrete or CMU | 8" - 12" | |

**Minimum Bearing:**
- 1-1/2" on wood (IRC R502.6)
- 3" on concrete or masonry (IRC R502.6)

**Revit Components:**
- Plywood-Section (subfloor)
- Dimension Lumber-Section (joists, sill plate, rim joist)
- Detail line (sill seal gasket)
- Anchor Bolt
- Filled region concrete hatch (foundation wall)

**Fastener Schedule:**
- Subfloor to joists: 8d common or 10d ring-shank @ 6" edges, 12" field + construction adhesive
- Rim joist to floor joists: 3-16d common through rim into joist end
- Rim joist to sill plate: 8d toenail @ 6" o.c.
- Sill plate: 1/2" anchor bolts @ 6'-0" o.c. max (IRC R403.1.6)
- Sill plate to rim: 16d @ 16" o.c.

**Critical Notes:**
- Sill plate MUST be pressure-treated lumber (IRC R317.1)
- Anchor bolts within 12" of each plate end and splice (IRC R403.1.6)
- Anchor bolt in middle third of plate width
- Washer required on each anchor bolt
- Termite shield (metal flashing) between sill and foundation in termite zones

**Climate Zone Variations:**
- CZ 4+: Insulate rim joist area to same R-value as walls
- CZ 5+: Seal rim joist with rigid foam + spray foam (cut-and-cobble) or spray foam only

---

### 2.2 Floor Joist Bearing on Interior Bearing Wall

**Configuration:**
- Floor joists lap over bearing wall (min 3" overlap) or butt with splice plate
- Joists bear on double top plate of wall below
- Subfloor continuous over bearing wall
- Blocking at bearing point if joists are offset

**Revit Components:**
- Dimension Lumber-Section (joists, bearing wall studs, plates)
- Plywood-Section (subfloor)
- Common Wood Nails-Side (fastener callouts)

**Fastener Schedule:**
- Lapped joists: 3-16d common face-nailed
- Butted joists: metal splice plate (Simpson LTP4 or equivalent)
- Joists to top plate: 3-8d toenails each joist

---

### 2.3 Rim/Band Joist Detail (Critical Thermal Bridge)

**Why This Detail Matters:**
The rim joist is the #1 thermal bridge and air leak point in wood-frame construction. It connects the conditioned space to the outdoors with only 1.5" of wood (R-1.5). Every energy audit identifies this area.

**Layer Stack (Interior to Exterior) at Rim Joist:**

| # | Layer | Material | R-Value | Notes |
|---|-------|----------|---------|-------|
| 1 | Interior finish | GWB (if basement/crawl) | R-0.5 | Often missing in basements |
| 2 | Insulation | Cut rigid foam + caulk edges | R-5 to R-15 | Or 2" closed-cell SPF |
| 3 | Rim joist | 2x lumber or engineered | R-1.5 | Structural |
| 4 | Sheathing | OSB/plywood | R-0.5 | |
| 5 | WRB | Housewrap | -- | |
| 6 | Cladding | Per wall above | varies | |

**Best Practice Insulation Methods:**
1. **Cut-and-cobble:** Cut rigid XPS/polyiso to fit each joist bay, seal all edges with canned spray foam
2. **Spray foam:** 2" closed-cell SPF directly on rim joist interior face (R-14, air+vapor sealed)
3. **Combination:** 1" rigid foam + batt insulation over it

**DO NOT USE:** Unfaced fiberglass batt stuffed in rim joist cavity (allows condensation on cold rim joist surface)

**Code Requirements:**
- IRC Table N1102.4.1.1: Rim joists must be insulated and air-sealed
- Minimum R-13 (CZ 1-4) or R-20 (CZ 5-8) at rim joist
- Fire barrier required if using foam insulation in habitable space

---

### 2.4 Cantilever Floor Detail (Bay Window / Bump-Out)

**Maximum cantilever without engineering:** Typically 24" (2x10) or 24" (2x12) -- consult IRC Table R502.3.3

**Layer Stack (Top to Bottom at Cantilever):**

| # | Component | Material | Notes |
|---|-----------|----------|-------|
| 1 | Subfloor | 3/4" T&G plywood | Continuous |
| 2 | Floor joists | 2x10 or 2x12 | Extend past wall below |
| 3 | Cavity insulation | R-30 batt or spray foam | Fill entire cantilever cavity |
| 4 | Bottom air barrier | Rigid foam or plywood | CRITICAL -- seals bottom |
| 5 | Bottom sheathing | 1/2" plywood or OSB | Protection layer |

**Critical Air Sealing Points:**
- Seal subfloor to joists at perimeter with caulk
- Install rigid air barrier in each joist bay at the wall plane (where cantilever begins)
- Seal bottom of cantilever completely -- exposed to outdoor air
- Caulk all edges of bottom sheathing

**Common Mistakes:**
- Leaving bottom of cantilever open (wind washes insulation, pipes freeze)
- Not insulating back to the wall plane inside the floor
- Using fiberglass batt without bottom air barrier (worthless without containment)
- Forgetting to insulate the end of the cantilever (exposed 2x header)

**Revit Components:**
- Dimension Lumber-Section (joists extending past wall)
- Plywood-Section (subfloor, bottom sheathing)
- Rigid Insulation-Section (bottom air barrier)
- Filled region insulation hatch (cavity fill)

---

### 2.5 Subfloor Assembly

**Standard Residential Subfloor:**

| Component | Specification | Notes |
|-----------|---------------|-------|
| Subfloor panel | 23/32" (3/4" nominal) T&G plywood or OSB | APA Rated Sturd-I-Floor |
| Adhesive | Construction adhesive (AFG-01 compliant) | Reduces squeaks |
| Fasteners | 8d ring-shank or 10d common @ 6" edges, 12" field | Or #8 screws @ 6/12 |
| Panel orientation | Long dimension perpendicular to joists | Stagger end joints |

**Underlayment (over subfloor, under finish flooring):**

| Finish Floor | Underlayment | Thickness |
|-------------|--------------|-----------|
| Hardwood | None (direct to subfloor) or rosin paper | -- |
| Tile | 1/4" cement board (Hardiebacker) or Ditra | 1/4" |
| Vinyl/LVP | 1/4" luan plywood or self-leveling compound | 1/4" |
| Carpet | None (pad directly on subfloor) | -- |

---

### 2.6 Platform Framing -- Floor-to-Floor Connection

**Sequence (Bottom to Top):**

| # | Component | Notes |
|---|-----------|-------|
| 1 | Lower wall top plates | Double 2x (or single with OVE) |
| 2 | Rim joist | Same depth as floor joists, nailed to top plate |
| 3 | Floor joists | Bearing on lower wall top plate |
| 4 | Subfloor | Over joists, nailed + glued |
| 5 | Upper wall sole plate | On top of subfloor |
| 6 | Upper wall studs | Ideally stacked over lower studs |

**Key Characteristics:**
- Each story is framed independently
- Floor platform creates fire stop between stories (inherent)
- Most common modern framing method
- Allows shorter lumber lengths
- Shrinkage accumulates at each floor level (~1/4" per floor)

**Balloon Framing (Historical/Special Cases):**
- Studs run continuous from foundation to roof (2+ stories)
- Floor joists bear on ribbon/ledger board let into studs
- NO inherent fire stop between floors (must add fire blocking)
- Used today mainly for tall walls (2-story great rooms, stairwells)
- Fire blocking required at each floor level (IRC R602.8)

**Revit Drawing Note:** Show platform framing connection with clear break between stories. Annotate "PLATFORM FRAMING" in detail title.

---

### 2.7 TJI / Engineered Lumber Floor Details

**Components and Sizes:**

| Product | Depths Available | Max Span (floor, 40 psf LL) | Notes |
|---------|-----------------|------------------------------|-------|
| TJI 110 | 9-1/2", 11-7/8" | 15'-2" / 17'-6" (16" o.c.) | Economy grade |
| TJI 210 | 9-1/2", 11-7/8", 14" | 16'-8" / 19'-8" / 22'-6" | Standard residential |
| TJI 230 | 9-1/2", 11-7/8", 14", 16" | 17'-4" / 20'-6" / 23'-6" / 25'-2" | Premium residential |
| TJI 360 | 11-7/8", 14", 16" | 21'-4" / 24'-6" / 26'-4" | Long spans |

**Bearing Requirements:**
- Minimum bearing: 1-3/4" at ends
- Bearing blocking REQUIRED at all end bearing locations
- Use engineered lumber for blocking (NOT dimensional lumber -- differential shrinkage)
- Blocking materials: TJI joist sections, LVL, LSL, or rim board

**Web Stiffeners:**
- Required at concentrated loads
- Required at bearing points where top-flange loaded
- Cut plywood/OSB to fit snug between flanges, nail to web

**Hole Boring:**
- Only in web, never in flanges
- Centered vertically
- Follow manufacturer's hole chart (diameter and location limits)
- Knockout holes pre-cut in most TJI products

**Critical Differences from Dimensional Lumber:**
- Do NOT notch flanges (destroys structural capacity)
- Do NOT bear on bottom flange only
- Do NOT use dimensional lumber rim board (shrinkage mismatch)
- Requires blocking at all bearing points for lateral stability
- Must use approved joist hangers (not face-nailed)

**Revit Components:**
- Dimension Lumber-Section (use parametric for I-joist profile, or detail lines)
- Plywood-Section (web stiffeners, subfloor)
- Note: No TJI-specific component in BD library -- draw with detail lines showing top/bottom flange and plywood web

---

## 3. WOOD FRAME ROOF DETAILS

### 3.1 Eave Detail with Soffit (Vented)

**Components (Top to Bottom, Inside to Outside):**

| # | Component | Material | Notes |
|---|-----------|----------|-------|
| 1 | GWB ceiling | 1/2" GWB | Interior ceiling finish |
| 2 | Ceiling insulation | R-38 to R-60 batt or blown | Per climate zone |
| 3 | Ventilation baffle | Cardboard or foam baffle (Proper Vent) | Maintains 1" min airspace above insulation |
| 4 | Rafter or truss | 2x8, 2x10, 2x12, or truss | Per span tables |
| 5 | Roof sheathing | 7/16" OSB or 1/2" plywood | Extends to fascia |
| 6 | Underlayment | #15 felt or synthetic | Full coverage |
| 7 | Drip edge | Galvanized or aluminum L-flashing | Under felt at eave (IRC R905.2.8.5) |
| 8 | Ice & water shield | Self-adhered membrane | CZ 5+ only, min 24" past interior wall |
| 9 | Roofing | Asphalt shingles or metal | Starter strip at eave |
| 10 | Fascia board | 1x6 or 1x8 wood/PVC | Covers rafter tails |
| 11 | Soffit | 3/8" or 1/2" vented vinyl/aluminum/plywood | Continuous or individual vents |
| 12 | Frieze board | 1x wood trim | Transition between wall and soffit |

**Critical Dimensions:**
- Overhang: 12" - 24" typical (up to 36" with engineering)
- Soffit vent: Minimum 1/150 of attic area (reducible to 1/300 with balanced vent)
- Ventilation channel: Minimum 1" clear above insulation to sheathing
- Insulation: Must extend to outer edge of top plate (no gap at eave)

**Ventilation Airflow Path:**
Exterior air enters through vented soffit -> travels up 1" min channel between insulation baffle and roof sheathing -> exits through ridge vent

**Revit Components:**
- Gypsum Wallboard-Section (ceiling)
- Dimension Lumber-Section (rafters, blocking, lookouts)
- Plywood-Section (sheathing, soffit if plywood)
- Asphalt Shingles Dynamic-2 (roofing)
- Underlayment (felt)
- Detail lines (drip edge, ice shield, soffit vent, fascia)

**Fastener Schedule:**
- Roof sheathing: 8d common @ 6" edges, 12" field (IRC Table R602.3(1))
- High wind zones (>100 mph): 8d @ 4" edges at eaves, gables, ridge (6" elsewhere)
- Shingles: 4 nails per shingle min (6 in high wind)

**Code Requirements:**
- IRC R806.1: Ventilated attics required (unless unvented per R806.5)
- IRC R806.2: Minimum 1" airspace between insulation and sheathing
- IRC R905.2.8.5: Drip edge required at eaves
- IRC R905.2.7.1: Ice barrier in areas with history of ice dams (CZ 5+)

---

### 3.2 Ridge Detail (Vented Ridge)

**Components:**

| # | Component | Material | Notes |
|---|-----------|----------|-------|
| 1 | Ridge board or beam | 2x (1 size larger than rafters) or LVL | Structural ridge if no collar ties |
| 2 | Opposing rafters | 2x8, 2x10, or 2x12 | Nailed to ridge board + each other |
| 3 | Collar ties | 2x4 or 2x6 @ 48" o.c. | Upper 1/3 of rafter height |
| 4 | Roof sheathing | OSB or plywood | Stop 3/4" - 1-1/2" short of ridge center |
| 5 | Underlayment | Felt or synthetic | Drape over ridge slot |
| 6 | Ridge vent | Manufactured ridge vent with external baffle | CertainTeed, GAF Cobra, etc. |
| 7 | Shingles | Cap shingles (3-tab cut or hip/ridge caps) | Installed over ridge vent |

**Ridge Vent Slot:**
- Cut sheathing back 3/4" from each side of ridge (1-1/2" total slot width)
- Do NOT cut into ridge board
- Slot allows attic air to exhaust

**Ridge Vent Types:**
- Shingle-over (most common): Low profile, covered with cap shingles
- Rigid (GAF Cobra): External baffle for maximum airflow
- Filter style: Mesh to prevent insect/debris entry

**Balanced Ventilation Rule:**
- Exhaust (ridge) should NOT exceed intake (soffit)
- Ideal: 50/50 split between intake and exhaust
- NFA (Net Free Area) of ridge vent must be matched by soffit vents

**Revit Components:**
- Dimension Lumber-Section (ridge board, rafters, collar ties)
- Plywood-Section (sheathing -- draw gap at ridge)
- Roof Ridge Vent
- Asphalt Shingles Dynamic-2

**Code Requirements:**
- IRC R802.3: Ridge board required (at least 1x nominal, depth = cut end of rafter)
- IRC R802.4: Collar ties @ 48" o.c. max in upper 1/3
- IRC R806.1: Cross ventilation required unless exempt

---

### 3.3 Gable End Detail

**Framing Components:**

| # | Component | Notes |
|---|-----------|-------|
| 1 | Gable end wall studs | Extend from top plate to underside of rafters (varying heights) |
| 2 | End rafter (barge rafter) | Last rafter at gable |
| 3 | Lookouts (outriggers) | 2x4 blocking from second rafter to barge rafter (if overhang) |
| 4 | Rake board (fascia) | 1x4 to 1x8 along roof slope at gable |
| 5 | Soffit (if overhanging) | Plywood or vinyl under overhang |
| 6 | Gable vent | Louvered vent for attic ventilation |

**Overhang Methods:**
- **Ladder framing:** Lookouts (2x4) at 16-24" o.c. between barge rafter and first full rafter
- **Outrigger framing:** 2x4 or 2x6 extending from second rafter through first rafter to barge rafter (for overhangs >12")
- Overhangs >12" should use outrigger method for structural adequacy

**Gable End Bracing (High Wind):**
- IRC R602.10.6.2: Gable end walls require bracing in high-wind zones
- Continuous sheathing or let-in bracing
- Retrofit: Add horizontal blocking between gable studs

**Revit Components:**
- Dimension Lumber-Section (gable studs, lookouts, barge rafter)
- Plywood-Section (sheathing, soffit)
- Siding-Wood Bevel (gable wall siding)
- Detail lines (rake board, vent)

---

### 3.4 Rake Detail

**Components (from sheathing outward):**

| # | Component | Notes |
|---|-----------|-------|
| 1 | Roof sheathing | Extends to edge of overhang |
| 2 | Drip edge | Metal L-flashing along rake edge (over felt, unlike eave) |
| 3 | Underlayment | Laps over drip edge at rake |
| 4 | Shingles | Overhang drip edge 1/4" to 3/4" |
| 5 | Rake/barge board | 1x fascia along sloped edge |
| 6 | Frieze board | Trim at wall-to-soffit transition |

**Note on Drip Edge at Rake vs Eave:**
- At EAVE: Drip edge UNDER underlayment
- At RAKE: Drip edge OVER underlayment
- This prevents water from getting behind the drip edge

**Code:** IRC R905.2.8.5: Drip edge required at eaves and rakes

---

### 3.5 Hip and Valley Details

**Hip Rafter:**
- Typically 2x size larger than common rafters (2x12 if common are 2x10)
- Hip jack rafters bear on hip rafter at varying angles
- Hip rafter bears on corner of wall top plates and on ridge board
- Hip shingles: Cut from 3-tab or use manufactured hip/ridge caps

**Valley Rafter:**
- Same size increase as hip rafter
- Jack rafters frame into valley rafter
- Metal valley flashing (W-shaped, 24" wide min) or woven valley shingles
- Open valley: 6" exposed metal min, widening 1/8" per foot toward eave
- Closed/woven valley: Shingles woven alternating from each roof plane

**Critical Water Management:**
- Valley is highest-flow water concentration point on roof
- Self-adhered membrane (ice & water shield) full width of valley
- Metal flashing over membrane
- No shingle fasteners within 6" of valley centerline

---

### 3.6 Roof-to-Wall Connection

**Rafter/Truss to Top Plate:**
- Standard: 3-8d toenails (2 on one side, 1 on other) per IRC Table R602.3(1)
- High wind: Hurricane ties/straps (Simpson H2.5A, H10, etc.)
- Minimum uplift resistance: 175 lb per truss connection (IRC R802.11.1)
- Higher wind zones: See IRC Table R802.11 for required uplift values

**Hurricane Strap Types:**
| Connector | Uplift Capacity | Application |
|-----------|-----------------|-------------|
| H2.5A | 585 lb | Rafter-to-plate, face mount |
| H10 | 1,115 lb | Heavy uplift, hurricane zones |
| H1 | 525 lb | Light-duty rafter tie |
| MST | 1,880 lb | Continuous strap (multi-story) |
| HDU | 3,075-14,930 lb | Hold-down, foundation-to-frame |

**Continuous Load Path:**
For high-wind and seismic zones, the connection chain must be continuous:
1. Roof to wall (hurricane ties)
2. Wall to floor (straps across platform break)
3. Floor to wall below (straps)
4. Wall to foundation (anchor bolts + hold-downs)

**Revit Drawing:** Use detail lines for strap profiles with leader note identifying connector (e.g., "SIMPSON H2.5A HURRICANE TIE, TYP.")

---

### 3.7 Roofing Material Details

#### 3.7.1 Asphalt Shingles

**Layer Stack (Bottom to Top):**

| # | Layer | Material | Notes |
|---|-------|----------|-------|
| 1 | Roof sheathing | 7/16" OSB or 1/2" plywood | H-clips at unsupported edges if 24" rafter spacing |
| 2 | Drip edge | Metal L-flashing | At eaves (under felt) and rakes (over felt) |
| 3 | Ice & water shield | Self-adhered membrane | CZ 5+ at eaves, valleys, around penetrations |
| 4 | Underlayment | #15 felt or synthetic | Min single layer; double layer for slopes 2:12-4:12 |
| 5 | Starter strip | Modified shingle strip | Along eaves and rakes |
| 6 | Shingles | 3-tab or architectural | 4-6 nails per shingle depending on wind zone |
| 7 | Ridge/hip caps | Hip and ridge shingles | Over ridge vent |

**Minimum Roof Slope:** 2:12 with double underlayment and sealed; 4:12 standard

**Fastener Schedule:**
- Standard: 4 roofing nails per shingle (12 ga, 3/8" head, 3/4" penetration into sheathing)
- High wind (>110 mph): 6 nails per shingle
- Hand nailing or pneumatic with depth adjustment

**Revit Component:** Asphalt Shingles Dynamic-2, Underlayment

---

#### 3.7.2 Metal Roofing Over Wood Frame

**Standing Seam (Concealed Fastener):**

| # | Layer | Material | Notes |
|---|-------|----------|-------|
| 1 | Roof sheathing | 1/2" plywood min (NOT OSB for standing seam) | Solid deck required |
| 2 | Underlayment | Synthetic or #30 felt | High-temp rated |
| 3 | Clips | Manufacturer-specific fixed/floating | Allow thermal movement |
| 4 | Metal panels | 24 or 26 ga steel, 0.032 aluminum | 12" - 18" wide |

**Exposed Fastener (Screw-Down):**

| # | Layer | Material | Notes |
|---|-------|----------|-------|
| 1 | Purlins | 1x4 or 2x4 @ 24" o.c. over sheathing (or directly on rafters) | Provide nailing/attachment surface |
| 2 | Underlayment | Synthetic (if over solid deck) | Optional over purlins only |
| 3 | Metal panels | 29 ga or 26 ga corrugated/ribbed | Screwed at valley, not crown |

**Key Details:**
- Expansion/contraction: Panels move 1/8" per 10' per 50F change
- Standing seam clips allow sliding for thermal movement
- Ridge cap: Manufactured closure strip + ridge cap
- Eave: Metal drip edge or formed starter
- Valley: W-valley pan under panels
- Minimum slope: 3:12 for standing seam, 1:12 possible with sealant

---

### 3.8 Cathedral/Vaulted Ceiling Insulation

#### 3.8.1 Vented Cathedral Ceiling

**Layer Stack (Interior to Exterior):**

| # | Layer | Material | Notes |
|---|-------|----------|-------|
| 1 | GWB ceiling | 1/2" GWB | Screwed to rafter bottom face |
| 2 | Vapor retarder | Kraft-faced batt or separate poly (CZ 5+) | Interior side of insulation |
| 3 | Cavity insulation | Fiberglass batt or dense-pack cellulose | R-30 to R-49 |
| 4 | Ventilation baffle | Rigid foam or cardboard baffle | Minimum 1" airspace to sheathing |
| 5 | Ventilation channel | 1" minimum clear airspace | Soffit to ridge continuous |
| 6 | Roof sheathing | OSB or plywood | |
| 7 | Underlayment + roofing | Per roofing type | |

**Limitations:**
- Works only for simple gable/shed roofs (no hips, valleys, dormers, skylights interrupting airflow)
- Rafter depth must accommodate both insulation and ventilation channel
- 2x10 rafters: R-30 batt + 1" vent channel (tight)
- 2x12 rafters: R-38 batt + 1" vent channel

---

#### 3.8.2 Unvented Cathedral Ceiling (Hot Roof)

**Option A -- All Spray Foam:**

| # | Layer | Material | Notes |
|---|-------|----------|-------|
| 1 | GWB ceiling | 1/2" GWB | |
| 2 | Closed-cell SPF | Fill entire rafter bay | R-6.5/in, vapor barrier included |

**Option B -- Rigid Foam + Batt (IRC R806.5):**

| # | Layer | Material | Notes |
|---|-------|----------|-------|
| 1 | GWB ceiling | 1/2" GWB | |
| 2 | Vapor retarder | If air-permeable insulation used | CZ 5+ |
| 3 | Air-permeable insulation | Fiberglass or cellulose batt | Balance of cavity |
| 4 | Air-impermeable insulation | Closed-cell SPF or rigid foam | Against roof sheathing |
| 5 | Roof sheathing | OSB or plywood | |

**Minimum R-Value of Air-Impermeable Layer (IRC Table R806.5):**

| Climate Zone | Min R at Sheathing | Cavity Fill |
|-------------|-------------------|-------------|
| CZ 1 | R-5 | Balance with batt |
| CZ 2-3 | R-5 | Balance with batt |
| CZ 4, Marine 4 | R-10 | Balance with batt |
| CZ 5 | R-15 | Balance with batt |
| CZ 6 | R-20 | Balance with batt |
| CZ 7-8 | R-25 | Balance with batt |

**Code:** IRC R806.5 -- Unvented attic and unvented enclosed rafter assemblies

**Common Mistakes:**
- Using only air-permeable insulation in unvented assembly (condensation on sheathing)
- Insufficient rigid foam ratio (warm-side condensation control fails)
- Not air-sealing at eave, ridge, and gable transitions

---

### 3.9 Ice Dam Prevention Detail (Cold Climates)

**Root Cause:** Heat from conditioned space warms roof sheathing -> melts snow -> water runs to cold eave -> refreezes -> backs up under shingles

**Prevention Strategy (3 layers of defense):**

1. **Air seal attic floor** -- Stop warm air leaks into attic
   - Seal all penetrations (light fixtures, plumbing, wiring, HVAC)
   - Seal top plates of all walls below attic
   - Seal around chimney with metal flashing + fire-rated caulk

2. **Insulate to code** -- Reduce conductive heat transfer
   - Minimum R-38 (CZ 4-5), R-49 (CZ 6-7), R-60 (CZ 8)
   - Insulation must extend to outer edge of top plate at eave

3. **Ventilate** -- Keep roof deck cold
   - Soffit-to-ridge ventilation
   - Minimum 1" clear airspace from soffit to ridge
   - Baffles in every rafter bay at eave

**Ice & Water Shield Membrane:**
- Required in CZ 5+ (IRC R905.2.7.1)
- Self-adhered bituminous membrane at eaves
- Extend from eave to at least 24" past interior face of exterior wall
- Also install in valleys, around penetrations, skylights

**Revit Drawing Notes:**
- Show membrane as thick line at roof deck with note: "SELF-ADHERED MEMBRANE (ICE & WATER SHIELD), MIN 24\" PAST INTERIOR WALL PLANE"
- Show ventilation baffle in every rafter bay
- Dimension the 24" past wall plane

---

## 4. FOUNDATION DETAILS FOR WOOD FRAME

### 4.1 Slab-on-Grade to Wood Frame Wall

#### 4.1.1 Stem Wall (Most Common)

**Layer Stack (Top to Bottom):**

| # | Component | Material | Notes |
|---|-----------|----------|-------|
| 1 | Wall assembly | Per wall section above | 2x4 or 2x6 frame |
| 2 | Sole plate | PT 2x4 or 2x6 | Must be pressure-treated |
| 3 | Sill seal | Foam gasket or sill sealer | Air seal + capillary break |
| 4 | Anchor bolts | 1/2" J-bolts @ 6'-0" o.c. | 7" embed, 12" from ends |
| 5 | Stem wall | Concrete or CMU | Min 8" wide, extends above grade |
| 6 | Grade | -- | Min 6" stem wall above grade |
| 7 | Slab-on-grade | 4" concrete | Independent of stem wall (bond-break) |
| 8 | Vapor retarder | 6-mil poly (10-mil preferred) | Under slab, over gravel |
| 9 | Gravel base | 4" compacted gravel | Capillary break |
| 10 | Compacted subgrade | Native soil or engineered fill | |

**Critical Details:**
- Stem wall min 6" above exterior grade (IRC R404.1.6)
- 8" above grade in termite zones (IRC R318.1)
- Bond-break joint between slab and stem wall (allows independent movement)
- Slab reinforcement: 6x6 W1.4xW1.4 WWF or #3 rebar @ 18" o.c. each way
- Expansion joint filler at slab-to-stem wall interface
- Rigid insulation on exterior of stem wall (below-grade) in CZ 4+

**Revit Components:**
- Dimension Lumber-Section (sole plate, wall studs)
- Anchor Bolt
- Filled region concrete hatch (stem wall, slab)
- Rigid Insulation-Section (perimeter insulation)
- Detail line (sill seal, vapor retarder, grade line)

---

#### 4.1.2 Monolithic Slab (Thickened Edge)

**Layer Stack:**

| # | Component | Material | Notes |
|---|-----------|----------|-------|
| 1 | Wall assembly | Wood frame | |
| 2 | Sole plate | PT 2x | On top of thickened slab edge |
| 3 | Anchor bolts | 1/2" J-bolts | Cast into thickened edge |
| 4 | Thickened slab edge | 12" deep x 12" wide min | Integral footing + slab |
| 5 | Slab | 4" concrete | Monolithic with edge |
| 6 | Vapor retarder | 6-mil poly | Under entire slab |
| 7 | Gravel | 4" compacted | |

**Min depth of thickened edge:** 12" below grade (or below frost line -- see 4.4)
**Rebar:** 2-#4 continuous in thickened edge, 3" cover

**Advantages:** Single pour, economical
**Limitations:** Low stem wall height above grade (moisture risk), not for expansive soils

---

### 4.2 Crawl Space Foundation

#### 4.2.1 Stem Wall Crawl Space

**Components:**

| # | Component | Notes |
|---|-----------|-------|
| 1 | Floor system | Joists bearing on sill plate on top of stem wall |
| 2 | Sill plate | PT 2x on anchor bolts |
| 3 | Stem wall | Concrete or CMU, min 18" clear above crawl grade |
| 4 | Footing | Continuous concrete, below frost line |
| 5 | Crawl floor | Ground cover (6-mil poly min) |
| 6 | Ventilation | Vents or conditioned approach |

**Vented Crawl Space (Traditional):**
- Ventilation openings: 1 sq ft per 150 sq ft of crawl area (IRC R408.1)
- Reducible to 1/1500 with Class I vapor retarder on ground
- One vent within 3' of each corner
- Insulation in floor above: R-19 to R-30 (with vapor retarder facing up)
- Pipes and ducts in crawl space must be insulated

**Conditioned/Sealed Crawl Space (Preferred, IRC R408.3):**
- No vents -- crawl space is sealed and conditioned
- Ground cover: 6-mil poly lapped 6" at seams, sealed to walls
- Walls insulated: R-10 continuous (CZ 3-4) or R-15 (CZ 5+)
- Conditioned air supply: 1 cfm per 50 sq ft of crawl area
- Or exhaust fan: 1 cfm per 50 sq ft to outdoors
- No insulation in floor above

**Minimum Crawl Space Height:** 18" below joists, 12" below beams/ducts (IRC R408.6)

---

#### 4.2.2 Pier and Beam Foundation

**Components:**

| # | Component | Notes |
|---|-----------|-------|
| 1 | Floor system | Joists on beams |
| 2 | Beams | 2-2x10, 2-2x12, or steel | Span between piers |
| 3 | Posts | 6x6 PT wood or steel columns | On piers |
| 4 | Piers | Concrete pads or poured piers | Below frost line |
| 5 | Perimeter beam | Concrete or PT wood | Supports exterior walls |

**Post-to-Beam Connection:**
- Metal post cap (Simpson BC/ABA series)
- Post notched into beam is NOT code-compliant in most jurisdictions
- Metal connectors required for uplift resistance

**Post-to-Pier Connection:**
- Metal post base (Simpson CB/ABU series)
- 1/2" anchor bolt cast into pier
- Minimum 1" clearance from post bottom to grade (decay prevention)

---

### 4.3 Basement Wall to Wood Frame

**Layer Stack (Interior to Exterior):**

| # | Component | Material | Notes |
|---|-----------|----------|-------|
| 1 | Interior finish | 1/2" GWB on furring | Optional |
| 2 | Interior insulation | 2" rigid XPS/polyiso + batt | Or spray foam |
| 3 | Vapor retarder | Integral to rigid foam | Do NOT put poly on warm side of basement wall |
| 4 | Basement wall | 8" - 12" concrete or CMU | |
| 5 | Dampproofing | Bituminous coating | Min requirement |
| 6 | Waterproofing | Sheet or fluid-applied membrane | Better protection |
| 7 | Protection board | 1/2" rigid insulation or drain mat | Protects membrane |
| 8 | Drainage | Drain mat or gravel backfill | |
| 9 | Footing | 8" x 16" or 8" x 20" concrete | Below frost line |
| 10 | Footing drain | 4" perforated pipe in gravel | Daylight or sump pump |

**Transition at Top of Basement Wall (Critical Detail):**
- Floor joists bear on PT sill plate on top of basement wall
- Sill seal gasket between sill plate and concrete
- Anchor bolts: 1/2" @ 6'-0" o.c., 7" embedment
- Rim joist at perimeter insulated and air-sealed
- Exterior grade: Min 8" below top of foundation wall
- Backfill slope away from house: Min 6" fall in 10' (IRC R401.3)

**Insulation of Basement Wall (IRC N1102.2.8):**
- CZ 3: R-5 ci or R-13 cavity
- CZ 4-5: R-10 ci or R-13 cavity
- CZ 6-8: R-15 ci or R-19 cavity

---

### 4.4 Frost Wall / Frost-Protected Shallow Foundation (FPSF)

**Standard Frost Wall:**
- Foundation extends below frost line (varies by location)
- 12" (South) to 48"+ (Northern states/Canada)
- IRC R403.1.4: Minimum depth per local frost depth

**Frost-Protected Shallow Foundation (FPSF) -- IRC R403.3:**
- Slab on grade with horizontal rigid insulation extending outward
- Traps geothermal heat under slab, preventing frost heave
- Allows footing as shallow as 12" even in cold climates
- Requires specific insulation R-values and wing widths per IRC Table R403.3(1)

**FPSF Insulation Requirements (Sample):**

| Air Freezing Index | Vertical R-Value | Horizontal R-Value | Wing Width |
|-------------------|-----------------|--------------------|-----------|
| 1500 | R-4.5 | R-4 | 12" |
| 2500 | R-5.6 | R-5 | 24" |
| 3500 | R-7.2 | R-6.5 | 36" |
| 4000+ | R-8.5 | R-8.0 | 48" |

---

### 4.5 Sill Plate to Foundation Connection

**Standard Connection:**

| Component | Specification | Code Reference |
|-----------|---------------|---------------|
| Sill plate material | Pressure-treated 2x (SYP or DF) | IRC R317.1 |
| Anchor bolts | 1/2" diameter minimum | IRC R403.1.6 |
| Bolt spacing | 6'-0" maximum o.c. | IRC R403.1.6 |
| End distance | Max 12" from plate ends and splices | IRC R403.1.6 |
| Bolt embedment | 7" minimum into concrete | IRC R403.1.6 |
| Bolt location | Middle 1/3 of plate width | IRC R403.1.6 |
| Nut and washer | Required on every bolt | Standard practice |
| Sill seal | Foam gasket (1/4" compressible foam) | Air seal, capillary break |

**Alternative: Mudsill Anchors / Expansion Bolts:**
- 1/2" wedge anchors into cured concrete
- Acceptable alternative to cast-in J-bolts
- Same spacing and edge distance requirements
- Easier to install after concrete cures

**Termite Shield (Termite Zones):**
- Metal flashing between sill plate and foundation
- Extends 2" beyond each face of wall and bent down 45 degrees
- Forces termites into visible tubes for detection
- Required in IRC Termite Infestation Probability Map zones

**Moisture Protection:**
- Sill seal gasket provides capillary break
- PT lumber prevents decay from moisture wicking
- Top of foundation should be smooth and level
- No untreated wood within 8" of grade (6" in non-termite zones)

---

## 5. WINDOW AND DOOR IN WOOD FRAME

### 5.1 Window Head Detail

**2x4 Wall Configuration:**

| # | Component | Material | Notes |
|---|-----------|----------|-------|
| 1 | Interior GWB | 1/2" GWB | Returns to window frame |
| 2 | Header | Built-up 2x (see header table) | Per IRC R602.7 |
| 3 | Cripple studs | 2x4 above header to top plate | Fill space |
| 4 | King studs | 2x4 full height | Frame rough opening |
| 5 | Sheathing | OSB/plywood | Continuous over header |
| 6 | WRB + head flashing | Housewrap + flashing tape | Head flashing OVER nail fin |
| 7 | Drip cap / head flashing | Z-flashing (metal) | Directs water away from top |
| 8 | Exterior trim | 1x4 or brick mold | Optional |

**2x6 Wall Configuration:**
- Same as 2x4 but deeper header cavity
- Insulated header: sandwich rigid foam between 2x members
- Or use single LVL/PSL header with insulation on exterior side

**Revit Components:**
- Window Head - Wood Double Hung (or Window Head - Wood Casement)
- Dimension Lumber-Section (header, king studs, cripples)
- Plywood-Section (sheathing)
- Gypsum Wallboard-Section (interior return)
- Detail line (flashing, WRB)

---

### 5.2 Window Sill Detail (The #1 Leak Point)

**Layer Stack:**

| # | Component | Material | Notes |
|---|-----------|----------|-------|
| 1 | Interior sill | Wood stool + apron trim | Or drywall return |
| 2 | Sill plate (rough sill) | 2x4 or 2x6, level | Sloped 5-15 degrees outward preferred |
| 3 | Sill pan flashing | Self-adhered membrane | REQUIRED -- forms waterproof trough |
| 4 | Sheathing | OSB/plywood | Cut flush at rough opening |
| 5 | WRB integration | Housewrap tucked under sill flashing | Shingle lap principle |
| 6 | Exterior trim | 1x trim or integral brick mold | Caulked to cladding |
| 7 | Sealant | Backer rod + sealant | At exterior trim-to-cladding joint |

**Pan Flashing Installation (Critical Sequence):**
1. Cut WRB at sill in modified-I pattern, fold down
2. Apply sill pan flashing across rough sill, extending 9" beyond each side
3. Turn up pan flashing at jambs 2-3"
4. Apply jamb flashing over sill pan flashing edges (shingle lap)
5. Set window, fasten nail fin
6. Apply head flashing OVER head nail fin
7. Lap WRB back over head flashing

**The Cardinal Rule of Window Flashing:**
"Water flows down. Each upper layer overlaps the layer below it. NEVER reverse this lap."

**Code Requirements:**
- IRC R703.4: Flashing to be installed at top of windows to prevent water entry
- IRC R613.1: Window installation per manufacturer's instructions

**Common Mistakes:**
- No sill pan flashing (the single most common source of window leaks)
- Reverse-lapping: WRB behind sill flashing instead of under
- Caulking bottom of nail fin (traps water instead of letting it drain)
- Not extending pan flashing beyond jambs
- No back dam on sill pan (water runs behind)

**Revit Components:**
- Window Sill - Wood Double Hung (or Wood Casement)
- Dimension Lumber-Section (rough sill, cripples below)
- Detail lines (pan flashing, WRB layers)
- Caulking-Section (exterior sealant)

---

### 5.3 Window Jamb Detail

**2x4 Wall:**

| # | Component (Int. to Ext.) | Material | Notes |
|---|--------------------------|----------|-------|
| 1 | Interior GWB | 1/2" | Returns to window frame |
| 2 | Shimming space | Cedar shims | Behind window frame |
| 3 | King stud + jack stud | 2x4 + 2x4 | Frame rough opening |
| 4 | Cavity insulation | Batt or low-expand foam | Around window frame |
| 5 | Sheathing | OSB/plywood | |
| 6 | WRB + jamb flashing | Housewrap + peel-and-stick | |
| 7 | Window fin/frame | Manufacturer | |
| 8 | Exterior trim | 1x or brick mold | Caulked |

**2x6 Wall Difference:**
- Extension jamb needed (window typically 3.25" deep, wall is 5.5")
- 2x extension jamb or prefab jamb extension
- OR use outie window installation (flush with exterior)

**Rough Opening:**
- Width: Window unit width + 1/2" (1/4" shimming space each side)
- Height: Window unit height + 1/2" to 3/4"
- Square, plumb, and level critical

**Revit Component:** Window Jamb - Wood Double Hung (or Wood Casement)

---

### 5.4 Exterior Door Threshold / Sill Pan

**Components:**

| # | Component | Notes |
|---|-----------|-------|
| 1 | Interior finish floor | Hardwood, tile, etc. |
| 2 | Subfloor | 3/4" plywood |
| 3 | Floor joist / slab edge | Structure below |
| 4 | Door sill/threshold | Aluminum or wood, adjustable |
| 5 | Sill pan | Self-adhered membrane or metal pan | CRITICAL for water |
| 6 | Rough sill | PT 2x, sloped outward | Positive drainage |
| 7 | Exterior landing | Concrete, stone, or deck | Min 1/4" per foot slope away |

**Critical Waterproofing:**
- Door sill pans are as critical as window sill pans
- Slope rough sill outward (use beveled PT lumber or shim)
- Pan flashing up jambs 6" minimum
- Threshold weep holes must not be blocked
- Landing below door threshold: max 7-3/4" step down (IRC R311.3.1), minimum 1-1/2" below threshold for weather protection

---

### 5.5 Sliding Glass Door Sill Detail

**Additional Concerns:**
- Wider opening = more water exposure
- Track system must have integral weep system
- Sill pan extends full width of door + 6" each side
- Recessed track: ensure floor structure supports recessed framing
- ADA threshold: 1/2" max threshold height for accessible units (3/4" max for existing)

---

### 5.6 Garage Door Head/Jamb

**Header Requirements:**

| Opening Width | Minimum Header | Notes |
|--------------|---------------|-------|
| 8'-0" (single) | 2-2x10 or 3-1/2" x 9-1/4" LVL | Verify per IRC Table R602.7(1) |
| 9'-0" (single) | 2-2x12 or LVL | |
| 16'-0" (double) | LVL or steel beam | Exceeds prescriptive; requires engineering |
| 18'-0" (double) | Steel beam required | |

**Jamb Detail:**
- King studs + jack studs each side (or king + hanger for OVE)
- 2x6 min jamb width (provides 5.5" for track attachment)
- Reinforcement at track bracket locations (blocking between studs)
- Weatherstrip at jamb: vinyl or rubber compression seal
- Side clearance: min 3-3/4" from rough opening to nearest wall (for track)

**Head Detail:**
- Header height: Clear door height + 1-1/2" (track/operator clearance)
- Headroom clearance above door: min 10" for standard sectional, 12-15" for high-lift
- Blocking for operator motor mount (if ceiling-mount opener)
- Header 9" wider than door opening (4.5" bearing each side)

**Revit Components:**
- Dimension Lumber-Section (header, king studs, jack studs)
- Plywood-Section (header if built-up)
- Detail lines (weather seal, track)

---

### 5.7 Header Sizing Reference (IRC Table R602.7(1))

**Headers Supporting Roof + Ceiling Only (Single Story or Top Floor):**

| Span | 2x4 Wall Header | 2x6 Wall Header |
|------|-----------------|-----------------|
| 4'-0" | 2-2x4 | 2-2x4 |
| 5'-0" | 2-2x6 | 2-2x6 |
| 6'-0" | 2-2x6 | 2-2x6 |
| 8'-0" | 2-2x8 | 2-2x8 |
| 10'-0" | 2-2x10 | 2-2x10 |
| 12'-0" | 2-2x12 | 2-2x12 |

**Headers Supporting Roof + Ceiling + One Floor:**

| Span | 2x4 Wall Header | 2x6 Wall Header |
|------|-----------------|-----------------|
| 4'-0" | 2-2x6 | 2-2x6 |
| 5'-0" | 2-2x8 | 2-2x6 |
| 6'-0" | 2-2x8 | 2-2x8 |
| 8'-0" | 2-2x10 | 2-2x10 |
| 10'-0" | 2-2x12 | 2-2x12 |
| 12'-0" | Engineering required | Engineering required |

**Insulated Headers (OVE/Advanced Framing):**
- Single 2x header on interior face
- Rigid foam on exterior side to fill cavity
- Example: 2x6 wall, 2x6 header + 2" rigid foam = R-10 at header (vs R-0 with solid 2x built-up)
- Or: 2x4 header with 2" rigid foam + 1/2" plywood spacer

---

## 6. WOOD FRAME CONNECTION DETAILS

### 6.1 Corner Framing

#### 6.1.1 Three-Stud Corner (Standard)

**Configuration:**
```
        Wall B
          |
    S  S  S
    |  |  |
====S==S==S====  Wall A
    |  |  |
```
- 3 full studs: 2 ending Wall A, 1 turned 90 degrees for Wall B backing
- Provides nailing for both interior GWB surfaces
- Creates a solid but uninsulated corner

**Problem:** No room for insulation in corner = thermal bridge + cold spot

---

#### 6.1.2 California Corner (Preferred)

**Configuration (Plan View):**
```
        Wall B
          |
    S     S
    |     |
====S=====S====  Wall A
    |  S  |
       |
```
- 2 studs ending Wall A (one rotated 90 degrees flush with inside face)
- 1 stud at corner of Wall B
- The rotated stud provides GWB nailing AND leaves room for insulation behind it

**Advantages:**
- Insulation fills corner cavity
- Eliminates thermal bridge at corners
- Same structural capacity

---

#### 6.1.3 Two-Stud Corner with Drywall Clips (OVE)

**Configuration:**
```
        Wall B
          |
    S     S
    |     |
====S=====S====  Wall A
    |  C  |    C = drywall clip
```
- Only 2 studs at corner
- Drywall clip, 1x nailer strip, or scrap lumber for interior GWB backing
- Maximum insulation, minimum lumber
- IRC approved where corner doesn't transfer shear loads

**Clip Types:**
- Metal drywall clips (Simpson DC)
- 1x3 nailing strip screwed to stud
- Recycled plastic nailing strip

**Revit Drawing:** Show studs in section with clip symbol + note. Insulation hatch fills entire corner cavity.

---

### 6.2 T-Intersection (Partition to Exterior Wall)

#### 6.2.1 Standard (Blocking Method)

**Configuration (Plan View):**
```
    Exterior Wall
====S===B===S====
    |   |   |
        S      Interior Partition
        |
```
- Flat 2x4 blocking between exterior wall studs
- Provides GWB nailing at T-junction
- Allows insulation behind blocking

#### 6.2.2 Ladder Blocking (OVE Preferred)

**Configuration:**
```
    Exterior Wall
====S===B===S====
    |   B   |     B = horizontal 2x4 blocking (ladder rungs)
    |   B   |
    |   |   |
        S      Interior Partition
```
- Horizontal 2x4 blocks at 24" o.c. between exterior studs
- No extra full-height stud needed
- Maximum insulation behind junction
- Better air sealing possible

---

### 6.3 Header-to-King Stud Connection

**Standard Built-Up Header:**

| Component | Assembly |
|-----------|----------|
| King stud | Full-height stud at each side of opening |
| Jack stud (trimmer) | Shortened stud supporting header bottom |
| Header | 2x members with 1/2" plywood spacer (to match 3.5" stud depth in 2x4 wall) |
| Cripple studs | Above header to top plate |

**Fastener Schedule (IRC Table R602.3(1)):**
- Jack stud to king stud: 16d @ 12" o.c. face-nailed
- Header to king stud: 4-16d end-nailed (each end)
- Cripple studs to header: per stud-to-plate schedule

**OVE Alternative (No Jack Studs):**
- Header supported by metal hanger brackets (Simpson HH header hanger)
- Eliminates jack studs = one less piece of lumber per side
- King stud bears full load through hanger
- Header hanger must match load requirement

---

### 6.4 Top Plate to Rafter/Truss Connection

**Standard (Low Wind):**
- 3-8d toenails per rafter (2 one side, 1 other)
- Rafter notched with birdsmouth to bear on top plate
- Birdsmouth depth: max 1/3 rafter depth (IRC R802.6)

**High Wind / Seismic:**
- Hurricane ties required (see Section 3.6 for connector table)
- Minimum 175 lb uplift resistance per connection (IRC R802.11.1)
- Specific connector selected based on design wind speed and tributary area

**Truss-Specific:**
- Truss manufacturer specifies connection requirements
- Never cut, notch, or alter trusses in the field
- Truss-to-plate clips: Simpson H1, H2.5A, etc.

**Revit Drawing:** Show birdsmouth notch in rafter, detail the connector with leader note specifying model number and nail count.

---

### 6.5 Hold-Down and Strap Details

#### 6.5.1 Foundation to First Floor

| Connector | Application | Capacity |
|-----------|-------------|----------|
| HDU2 | Hold-down, shear wall end | 3,075 lb |
| HDU5 | Hold-down, heavy shear wall | 4,565 lb |
| HDU8 | Hold-down, high wind/seismic | 6,270 lb |
| STHD14 | Strap tie-down | 4,690 lb |
| PAHD | Purlin anchor / hold-down | 1,025 lb |

**Foundation anchor:** 5/8" or 3/4" anchor bolt, min 7" embedment
**Stud connection:** Through-bolted or multi-nail SDS screws

#### 6.5.2 Floor-to-Floor (Across Platform Break)

| Connector | Application | Capacity |
|-----------|-------------|----------|
| MST27 | Continuous strap, 1 story | 1,880 lb |
| MST37 | Continuous strap, multi-story | 2,830 lb |
| MST48 | Heavy strap, 2-story | 3,510 lb |
| CMST | Concealed strap | 1,585 lb |

**Installation:** Strap wraps from stud below, across rim joist, to stud above. Nailed per manufacturer schedule (typically 10d x 1.5" nails).

#### 6.5.3 Multi-Story Continuous Rod (ATS System)

For tall walls or high-load conditions:
- Continuous threaded rod from foundation to roof
- Steel bearing plates at each floor level with self-tightening (take-up) devices
- Compensates for wood shrinkage
- Simpson Strong-Tie ATS (Anchor Tiedown System)
- Used in 3-4 story wood frame construction

---

### 6.6 Beam Pocket Detail

**Wood Beam into Concrete/Masonry Wall:**

| Component | Specification |
|-----------|---------------|
| Pocket size | Beam width + 1" (sides) x beam depth + 1" (top) x 3" min bearing |
| Bearing plate | 1/4" steel plate or PT hardwood shim |
| Air gap | 1/2" minimum at sides and top of beam |
| End treatment | Beam end sealed or wrapped (moisture protection) |
| Fire protection | Pack mineral wool around beam if fire-rated wall |

**Wood Beam on Steel Column:**
- Metal post cap connector (Simpson BC/CC series)
- Beam sits in saddle, bolted through
- Column base plate anchored to footing

**Revit Components:**
- Dimension Lumber-Section or Nominal Cut Lumber-Section (beam)
- Filled region (concrete wall/pocket)
- Detail lines (bearing plate, air gaps)

---

### 6.7 Post-to-Beam Connections

| Connection Type | Connector | Notes |
|----------------|-----------|-------|
| Post under beam (compression) | Simpson BC post cap | Beam sits in saddle |
| Post on top of beam (compression) | Simpson ABU post base | Reversed orientation |
| Post beside beam (bolted) | Through-bolts + shear plate | 5/8" or 3/4" bolts |
| Post to concrete pier | Simpson CB column base | Anchor bolt in concrete |
| Notched post (AVOID) | Not recommended | Reduces cross-section, stress concentration |

**Minimum Bolt Edge Distance:** 7x bolt diameter from end of grain, 4x from edge
**Minimum Bolt Spacing:** 4x bolt diameter between bolts in a row

---

## DRAWING ANNOTATION STANDARDS

### Standard Notes for Wood Frame Details

**Wall Sections:**
- "2x6 STUDS @ 16\" O.C., TYP. U.N.O."
- "R-21 FIBERGLASS BATT INSULATION"
- "7/16\" OSB SHEATHING"
- "WEATHER-RESISTIVE BARRIER (WRB)"
- "1/2\" GYPSUM WALLBOARD"

**Floor Details:**
- "2x10 FLOOR JOISTS @ 16\" O.C."
- "23/32\" T&G PLYWOOD SUBFLOOR, GLUED AND NAILED"
- "P.T. 2x6 SILL PLATE W/ 1/2\" A.B. @ 6'-0\" O.C."
- "SILL SEAL GASKET"

**Roof Details:**
- "ASPHALT SHINGLES OVER #15 FELT"
- "RIDGE VENT, CONTINUOUS"
- "VENTILATION BAFFLE, MAINTAIN 1\" MIN AIRSPACE"
- "R-38 BLOWN INSULATION" (or per CZ requirement)

**Foundation Details:**
- "4\" CONC. SLAB ON 6-MIL V.B. ON 4\" COMPACTED GRAVEL"
- "CONT. #4 REBAR, 2 BARS TOP AND BOTTOM" (footings)
- "DAMPPROOFING, BELOW GRADE"
- "4\" PERF. DRAIN PIPE IN GRAVEL, FILTER FABRIC WRAPPED"

**Window/Door Details:**
- "SELF-ADHERED MEMBRANE SILL PAN FLASHING"
- "BACKER ROD AND SEALANT, TYP."
- "DRIP CAP / Z-FLASHING AT HEAD"

---

## CLIMATE ZONE QUICK REFERENCE

### Insulation Requirements by Climate Zone (IRC Table N1102.1.2)

| CZ | Ceiling | Wall | Floor | Basement | Crawl | Slab Edge |
|----|---------|------|-------|----------|-------|-----------|
| 1 | R-30 | R-13 | R-13 | R-0 | R-0 | R-0 |
| 2 | R-38 | R-13 | R-13 | R-0 | R-0 | R-0 |
| 3 | R-38 | R-20 or R-13+R-5ci | R-19 | R-5ci or R-13 | R-5ci or R-13 | R-0 |
| 4 | R-49 | R-20 or R-13+R-5ci | R-19 | R-10ci or R-13 | R-10ci or R-13 | R-10 |
| 5 | R-49 | R-20+R-5ci or R-13+R-10ci | R-30 | R-15ci or R-19 | R-15ci or R-19 | R-10 |
| 6 | R-49 | R-20+R-5ci or R-13+R-10ci | R-30 | R-15ci or R-19 | R-15ci or R-19 | R-10 |
| 7-8 | R-49 | R-20+R-5ci or R-13+R-10ci | R-38 | R-15ci or R-19 | R-15ci or R-19 | R-10 |

**ci = continuous insulation (no thermal bridging through framing)**

### Air Sealing Critical Points Checklist

1. Top plates of all walls (attic side)
2. Rim/band joists at all floor levels
3. Window and door rough openings (spray foam or backer rod + caulk)
4. Electrical boxes in exterior walls (foam pads behind cover plates)
5. Plumbing and wiring penetrations through plates
6. Recessed lights in insulated ceilings (use IC-rated, air-tight rated)
7. Attic access hatch (weatherstripped, insulated)
8. Fireplace/chimney chase (metal flashing + fire caulk)
9. Bathtub/shower drain penetrations
10. Duct boots in exterior walls or ceilings
11. Cantilever floor bays (rigid foam air barrier at wall plane)
12. Knee walls in bonus rooms (air barrier on attic side)

---

## IRC CODE REFERENCE INDEX

| Section | Topic |
|---------|-------|
| R301.2 | Climatic and geographic design criteria (wind, snow, seismic) |
| R302.6 | Dwelling/garage separation (1/2" GWB minimum) |
| R317.1 | Decay protection (PT lumber requirements) |
| R318.1 | Termite protection |
| R401-404 | Foundations |
| R403.1.6 | Anchor bolt requirements |
| R408 | Crawl space ventilation and conditioning |
| R502 | Floor construction |
| R502.6 | Bearing requirements |
| R602 | Wall construction |
| R602.3 | Design and construction (fastener schedule in Table R602.3(1)) |
| R602.3(5) | Stud size, height, and spacing |
| R602.7 | Headers |
| R602.8 | Fire blocking (required in balloon framing) |
| R602.10 | Wall bracing |
| R613 | Window and door installation |
| R702.7 | Vapor retarders |
| R703 | Exterior covering (WRB, cladding) |
| R703.4 | Flashing |
| R703.7 | Exterior plaster (stucco) |
| R703.8 | Brick veneer |
| R802 | Roof construction |
| R802.6 | Birdsmouth notch limits |
| R802.11 | Uplift connections |
| R806 | Roof ventilation |
| R806.5 | Unvented attic assemblies |
| R905 | Roofing materials |
| R905.2.7.1 | Ice barrier requirements |
| R905.2.8.5 | Drip edge |
| N1102 | Building thermal envelope |

---

*End of Residential Wood-Frame Construction Details*
*Total coverage: 6 categories, 40+ detail types, complete layer stacks, Revit component mapping, IRC code references*
