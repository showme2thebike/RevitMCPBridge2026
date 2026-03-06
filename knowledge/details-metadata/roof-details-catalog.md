# Roof Details Catalog - BD Architect Detail Library

**Category:** 01 - Roof Details
**Source:** `/mnt/d/BDArchitect-DetailLibrary-2025/removed_2026/01 - Roof Details/`
**Total Files:** 57 .rvt detail files
**Extracted JSON Data:** 18 details from project models (in `knowledge/details/01-roof/`)
**Preview PNGs:** None available (directory empty)
**Last Updated:** 2026-03-05

---

## Summary Statistics

| Metric | Count |
|--------|-------|
| Total .rvt files | 57 |
| Parapet details | 7 |
| Scupper/drainage details | 9 |
| Penetration/flashing details | 10 |
| Equipment/curb details | 6 |
| Eave/edge details | 4 |
| Waterproofing details | 7 |
| Roof tile details (pitched) | 6 (from JSON extractions) |
| Wall sections with roof | 4 (from JSON extractions) |
| Miscellaneous/specialty | 10 |

---

## Extracted Detail Metadata (from JSON)

These details have full element-level data extracted from Revit project models. Each entry includes actual Revit components, line styles, and annotation text used.

---

### ROOF - EDGE DETAIL TYP.1
- **File**: ROOF_-_EDGE_DETAIL_TYP_1.json (extracted from project)
- **Type**: Section
- **Scale**: 1/2" = 1'-0"
- **Roof System**: TPO/FTR single-ply membrane
- **Condition**: Edge/drip edge termination
- **Components Used**: Break Line5 (2 instances)
- **Line Styles**: INSULATION, MEMBRANE, METAL, SUBSTRATE, BLOCKING, FASTENERS, ANCHORS, Line-1, Line-2, Line-3, Text
- **Key Elements**: FTR/FTR-FB membrane, fiberclad metal edge, butyl sealing tape, galvanized metal cleat, wood blocking, insulation, galvanized annular ring shank nails
- **Waterproofing**: Membrane flashing strip hot-air welded, continuous membrane termination at edge metal
- **Building Science**: Wind uplift resistance at perimeter zone, thermal continuity through insulation, mechanical fastening schedule per spec
- **Related Details**: ROOF - GUTTER DETAIL TYP.1 (companion gutter version)
- **Drawing Complexity**: Complex (381 detail lines, 17 text notes, 5 filled regions)
- **Element Counts**: 381 lines, 2 components, 17 texts, 5 fills
- **Notes**: Uses semantic line styles (INSULATION, MEMBRANE, METAL, etc.) rather than generic pen weights -- highest quality detail drafting convention in this library

---

### ROOF - GUTTER DETAIL TYP.1
- **File**: ROOF_-_GUTTER_DETAIL_TYP_1.json (extracted from project)
- **Type**: Section
- **Scale**: 1/2" = 1'-0"
- **Roof System**: TPO/FTR single-ply membrane
- **Condition**: Eave with gutter integration
- **Components Used**: Break Line5 (2 instances)
- **Line Styles**: INSULATION, MEMBRANE, METAL, SUBSTRATE, BLOCKING, FASTENERS, ANCHORS, Line-1, Line-2, Line-3, Text
- **Key Elements**: Anodized aluminum gutter, fiberclad metal, 2" aluminum tape over joints, 4" flashing strip (heat welded), membrane, insulation, wood blocking
- **Waterproofing**: Continuous membrane termination into gutter, flashing strip heat-welded, joint treatment at edge metal
- **Building Science**: Drainage path through gutter, thermal bridge at edge, wind uplift at perimeter
- **Related Details**: ROOF - EDGE DETAIL TYP.1 (non-gutter version)
- **Drawing Complexity**: Complex (631 detail lines, 18 text notes, 4 filled regions)
- **Element Counts**: 631 lines, 2 components, 18 texts, 4 fills
- **Notes**: Same semantic line style system as edge detail. Most line-intensive detail in the set.

---

### ROOF DETAIL-1
- **File**: ROOF_DETAIL-1.json (extracted from project)
- **Type**: Section
- **Scale**: 1/8" = 1'-0" (3/4" = 1'-0" actual)
- **Roof System**: Pitched (shingle/underlayment)
- **Condition**: Typical roof-to-wall section
- **Components Used**: Gypsum Plaster-Section (3/4"), Gypsum Sheathing-Section (1/2"), Nominal Cut Lumber-Section (1x3, 1x6), Plywood-Section (3/4"), Underlayment (Single Layer), Break Line
- **Line Styles**: Line-2, Line-3
- **Key Elements**: Plywood sheathing, underlayment, lumber framing, gypsum board, gypsum sheathing
- **Waterproofing**: Single-layer underlayment
- **Building Science**: Standard wood-frame residential roof assembly
- **Related Details**: Wall sections (MONO-FT, SPREAD-FT variants)
- **Drawing Complexity**: Simple (4 detail lines, 11 components, 0 texts)
- **Element Counts**: 4 lines, 11 components, 0 texts, 1 fill
- **Notes**: Component-heavy detail with minimal linework -- relies on Revit detail components for material representation

---

### ROOFTOP TERRACE PAVER DETAIL
- **File**: ROOFTOP_TERRACE_PAVER_DETAIL.json (extracted from project)
- **Type**: Section
- **Scale**: 1/8" = 1'-0"
- **Roof System**: TPO membrane with paver overburden
- **Condition**: Rooftop terrace/plaza deck
- **Components Used**: 00-BREAK LINE (1 instance)
- **Line Styles**: (none -- relies on filled regions)
- **Key Elements**: 20mm porcelain pavers (R11 slip rating), adjustable pedestals (Bison or eq.), TPO membrane, min 1/4"/ft slope
- **Waterproofing**: TPO membrane below pedestal system, 1/8" max paver gap, perimeter containment
- **Building Science**: Slope for drainage under pavers, pedestal height adjustment (1/2" to 24"+), slip-rated paver surface
- **Related Details**: FP-19 (TPO), FP-20 (Pavers) referenced in notes
- **Drawing Complexity**: Moderate (0 lines, 2 components, 14 texts, 13 filled regions)
- **Element Counts**: 0 lines, 2 components, 14 texts, 13 fills
- **Notes**: Text-heavy with numbered notes. Uses filled regions extensively for material representation.

---

### ROOF EAVE DETAIL
- **File**: ROOF_EAVE_DETAIL.json (extracted from project)
- **Type**: Section
- **Scale**: 1/8" = 1'-0"
- **Roof System**: Concrete roof tile (pitched)
- **Condition**: Eave at concrete curb
- **Components Used**: 00-BREAK LINE, 06-FRAMING-WOOD-Section-Stretch1 (2X), 06-SHEATHING-Plywood-Section1 (3/4")
- **Line Styles**: 1, 1_DASH B (x0.5), 4, Pen 01, Pen 03
- **Key Elements**: Concrete roof tile, engineered wood trusses @ 24" O.C., 3/4" plywood sheathing, truss anchors, concrete curb, raised stucco banding
- **Waterproofing**: Overlap per manufacturer specs and approved NOA
- **Building Science**: Hurricane zone detail (truss anchors, NOA compliance), 4:12 pitch indicated
- **Related Details**: ROOF TILE EAVE DETAIL, ROOF TILE EAVE DETAIL w/ gutter
- **Drawing Complexity**: Moderate (15 lines, 5 components, 14 texts, 15 fills)
- **Element Counts**: 15 lines, 5 components, 14 texts, 15 fills
- **Notes**: Florida-specific with NOA (Notice of Acceptance) reference. Pen-numbered line styles.

---

### ROOF TILE EAVE DETAIL
- **File**: ROOF_TILE_EAVE_DETAIL.json (extracted from project)
- **Type**: Section
- **Scale**: 1/8" = 1'-0"
- **Roof System**: Concrete roof tile (pitched)
- **Condition**: Tile eave without gutter
- **Components Used**: 00-BREAK LINE, 06-FRAMING-WOOD-Section-Stretch1 (2X), 06-SHEATHING-Plywood-Section1 (3/4")
- **Line Styles**: 1, 3, Pen 01, Pen 03
- **Key Elements**: Concrete roof tile, engineered wood trusses @ 24" O.C., 3/4" plywood sheathing, smooth stucco finish, truss anchors
- **Waterproofing**: Underlayment below tile
- **Building Science**: Structural beams per structural drawings, hurricane-rated truss anchors
- **Related Details**: ROOF TILE EAVE DETAIL w/ gutter, ROOF EAVE DETAIL
- **Drawing Complexity**: Moderate (15 lines, 6 components, 15 texts, 14 fills)
- **Element Counts**: 15 lines, 6 components, 15 texts, 14 fills

---

### ROOF TILE EAVE DETAIL w/ gutter
- **File**: ROOF_TILE_EAVE_DETAIL_w__gutter.json (extracted from project)
- **Type**: Section
- **Scale**: 1/8" = 1'-0"
- **Roof System**: Concrete roof tile (pitched) at CMU wall
- **Condition**: Eave with gutter at masonry wall
- **Components Used**: 00-BREAK LINE, 04-CMU-2 Core-Section (8"x8"x16"), 06-FRAMING-WOOD-Section-Stretch1 (2X), 06-SHEATHING-Plywood-Section1 (3/4"), 07-INSULATION-RIGID (1-1/2"), 09-FRAMING_CHANNEL (1.5"x1.375"), Bond Beams-Single-Section1 (8"x8"x16")
- **Line Styles**: 1, 1_DASH B (x0.5), 3, Pen 03
- **Key Elements**: Concrete roof tile, CMU wall, bond beam, rigid insulation, hat channel furring, plywood sheathing, trusses, stucco finish, gutter
- **Waterproofing**: Standard tile underlayment
- **Building Science**: CMU/masonry construction, thermal break with rigid insulation, bond beam at top of wall
- **Related Details**: ROOF TILE EAVE DETAIL (without gutter), typical wall type details referenced
- **Drawing Complexity**: Complex (15 lines, 10 components, 19 texts, 15 fills)
- **Element Counts**: 15 lines, 10 components, 19 texts, 15 fills
- **Notes**: Most component-rich tile eave variant. Shows full wall-to-roof connection at CMU.

---

### ROOF TILE MANSARDE CAP
- **File**: ROOF_TILE_MANSARDE_CAP.json (extracted from project)
- **Type**: Section
- **Scale**: 1/8" = 1'-0"
- **Roof System**: Concrete tile over TPO membrane transition
- **Condition**: Mansard cap / flat-to-pitched transition
- **Components Used**: 00-BREAK LINE (2 instances)
- **Line Styles**: Pen 03
- **Key Elements**: Concrete tile cap in adhesive foam, one-piece metal flashing on continuous metal cleat, TPO membrane with bonding adhesive, peel & stick membrane underlayment, plywood sheathing, pre-engineered wood trusses
- **Waterproofing**: TPO membrane at flat portion, peel & stick underlayment at slope, metal flashing transition between systems
- **Building Science**: Critical transition between flat (TPO) and pitched (tile) roofing, counter-flashing detail, drainage continuity
- **Related Details**: ROOF TILE RIDGE / HIP, ROOF TILE SIDEWALL FLASHING
- **Drawing Complexity**: Moderate (24 lines, 2 components, 8 texts, 8 fills)
- **Element Counts**: 24 lines, 2 components, 8 texts, 8 fills
- **Notes**: Important hybrid roof condition. Manufacturer specs required for tile cap installation.

---

### ROOF TILE RIDGE / HIP
- **File**: ROOF_TILE_RIDGE___HIP.json (extracted from project)
- **Type**: Section
- **Scale**: 1/8" = 1'-0"
- **Roof System**: Concrete roof tile (pitched)
- **Condition**: Ridge or hip cap
- **Components Used**: 00-BREAK LINE (2 instances)
- **Line Styles**: 01-Line-Fine, 02-Line-Very Thin, Waterproofing Membrane
- **Key Elements**: Mortar set ridge/hip cap, concrete tile roof, plywood sheathing, engineered wood trusses, peel & stick membrane underlayment
- **Waterproofing**: Peel & stick membrane underlayment continuous through ridge
- **Building Science**: Mortar bedding for cap tiles, ventilation considerations at ridge
- **Related Details**: ROOF TILE VALLEY, ROOF TILE MANSARDE CAP
- **Drawing Complexity**: Simple (10 lines, 2 components, 5 texts, 6 fills)
- **Element Counts**: 10 lines, 2 components, 5 texts, 6 fills
- **Notes**: Uses "Waterproofing Membrane" as a dedicated line style

---

### ROOF TILE SIDEWALL FLASHING
- **File**: ROOF_TILE_SIDEWALL_FLASHING.json (extracted from project)
- **Type**: Section
- **Scale**: 1/8" = 1'-0"
- **Roof System**: Concrete roof tile (pitched) at wall
- **Condition**: Sidewall flashing (roof-to-wall transition)
- **Components Used**: 00-BREAK LINE (3 instances)
- **Line Styles**: 01-Line-Fine, 02-Line-Very Thin, 03-Line-Thin, Pen 01, Pen 03, Waterproofing Membrane
- **Key Elements**: Stucco finish, continuous sealant, continuous metal counter flashing with cleat, concrete roof tile, continuous metal flashing (step flashing), plywood sheathing, peel & stick membrane underlayment
- **Waterproofing**: Continuous metal counter flashing, step flashing beneath tiles, peel & stick underlayment, sealant at wall interface
- **Building Science**: Water management at wall-to-roof intersection, counter flashing behind stucco, sealed lap joints
- **Related Details**: ROOF TILE VALLEY, ROOF TILE MANSARDE CAP, ROOF TILE RIDGE / HIP
- **Drawing Complexity**: Moderate (23 lines, 3 components, 7 texts, 8 fills)
- **Element Counts**: 23 lines, 3 components, 7 texts, 8 fills

---

### ROOF TILE VALLEY
- **File**: ROOF_TILE_VALLEY.json (extracted from project)
- **Type**: Section
- **Scale**: 1/8" = 1'-0"
- **Roof System**: Concrete roof tile (pitched)
- **Condition**: Valley flashing
- **Components Used**: None (pure linework)
- **Line Styles**: 01-Line-Fine, 02-Line-Very Thin, 03-Line-Thin, Waterproofing Membrane
- **Key Elements**: Concrete tile roof (both slopes), plywood sheathing, peel & stick membrane underlayment, continuous cleat fastened to substrate, 1" min valley clip, center rib (1" high)
- **Waterproofing**: Peel & stick underlayment in valley, metal valley flashing with center rib, cleat attachment system
- **Building Science**: Valley drainage, center rib prevents cross-flow, clip system allows thermal movement
- **Related Details**: ROOF TILE RIDGE / HIP, ROOF TILE SIDEWALL FLASHING
- **Drawing Complexity**: Complex (108 detail lines, 0 components, 6 texts, 6 fills)
- **Element Counts**: 108 lines, 0 components, 6 texts, 6 fills
- **Notes**: Entirely linework-based (no detail components). Most line-dense tile detail.

---

### WALL SECTION FLAT ROOF (MONO-FT)
- **File**: WALL_SECTION_FLAT_ROOF__MONO-FT_.json (extracted from project)
- **Type**: Wall section (full height)
- **Scale**: 1/16" = 1'-0"
- **Roof System**: Flat (built-up/membrane) on wood joists
- **Condition**: Full wall section -- monolithic footing
- **Components Used**: Break Line, Gypsum Plaster-Section (3/4"), Gypsum Sheathing-Section (1/2"), Nominal Cut Lumber-Section (1x3, 2x12), Plywood-Section (3/4", 5/8"), Reinf Bar Section (#5)
- **Line Styles**: (none -- component-based)
- **Key Elements**: 1/4" slope/ft, 2x10 wood joists @ 16" O.C., R-19 batt insulation, 1/2" gypsum board on 1x3 furring @ 24" O.C., 4" concrete slab with WWM, 6 mil polyethylene vapor barrier, double wire mesh at perimeter (3' min)
- **Waterproofing**: Roof membrane (not detailed at this scale), vapor barrier at slab
- **Building Science**: Thermal: R-19 batt in roof, vapor barrier at grade. Structural: monolithic footing, reinforced slab
- **Related Details**: WALL SECTION FLAT ROOF (SPREAD-FT), WALL SECTION GABLE ROOF variants
- **Drawing Complexity**: Complex (0 lines, 29 components, 23 texts, 6 fills)
- **Element Counts**: 0 lines, 29 components, 23 texts, 6 fills
- **Notes**: Component-heavy assembly drawing. Shows foundation to roof in one detail.

---

### WALL SECTION FLAT ROOF (SPREAD-FT)
- **File**: WALL_SECTION_FLAT_ROOF__SPREAD-FT_.json (extracted from project)
- **Type**: Wall section (full height)
- **Scale**: 1/16" = 1'-0"
- **Roof System**: Flat (built-up/membrane) on wood joists
- **Condition**: Full wall section -- spread footing
- **Components Used**: Break Line, Gypsum Plaster-Section (3/4"), Gypsum Sheathing-Section (1/2"), Nominal Cut Lumber-Section (1x3, 2x12), Plywood-Section (3/4", 5/8"), Reinf Bar Section (#5)
- **Line Styles**: (none -- component-based)
- **Key Elements**: Same roof assembly as MONO-FT variant. Key difference: spread footing at foundation, 1x4 P.T. wood firestop, R-4.1 insulation between furring strips
- **Waterproofing**: Roof membrane, vapor barrier at slab
- **Building Science**: Firestop at wall/floor intersection, additional insulation at furring
- **Related Details**: WALL SECTION FLAT ROOF (MONO-FT)
- **Drawing Complexity**: Complex (0 lines, 29 components, 25 texts, 7 fills)
- **Element Counts**: 0 lines, 29 components, 25 texts, 7 fills

---

### WALL SECTION GABLE ROOF (MONO-FT)
- **File**: WALL_SECTION_GABLE_ROOF__MONO-FT_.json (extracted from project)
- **Type**: Wall section (full height)
- **Scale**: 1/16" = 1'-0"
- **Roof System**: Pitched gable (shingle/tile on trusses)
- **Condition**: Full wall section -- monolithic footing
- **Components Used**: Break Line, Gypsum Plaster-Section (3/4"), Gypsum Sheathing-Section (1/2"), Nominal Cut Lumber-Section (1x3, 2x8), Plywood-Section (3/4", 5/8"), Reinf Bar Section (#5), Underlayment (Single Layer)
- **Line Styles**: (none -- component-based)
- **Key Elements**: Pre-engineered wood trusses @ 24" O.C., R-19 batt insulation, plywood sheathing, underlayment, 4" concrete slab with WWM, vapor barrier
- **Waterproofing**: Single-layer underlayment, vapor barrier at slab
- **Building Science**: Pitched drainage, attic ventilation implied, standard residential construction
- **Related Details**: WALL SECTION GABLE ROOF (SPREAD-FT), WALL SECTION FLAT ROOF variants
- **Drawing Complexity**: Complex (0 lines, 30 components, 25 texts, 8 fills)
- **Element Counts**: 0 lines, 30 components, 25 texts, 8 fills

---

### WALL SECTION GABLE ROOF (SPREAD-FT)
- **File**: WALL_SECTION_GABLE_ROOF__SPREAD-FT_.json (extracted from project)
- **Type**: Wall section (full height)
- **Scale**: 1/16" = 1'-0"
- **Roof System**: Pitched gable (shingle/tile on trusses) at CMU wall
- **Condition**: Full wall section -- spread footing with CMU
- **Components Used**: Bond Beam - 001 (8"x8"x16"), Break Line, Gypsum Plaster-Section (3/4"), Gypsum Sheathing-Section (1/2"), Nominal Cut Lumber-Section (1x3, 2x8), Plywood-Section (3/4", 5/8"), Reinf Bar Section (#5), Underlayment (Single Layer)
- **Line Styles**: (none -- component-based)
- **Key Elements**: CMU wall with bond beam, pre-engineered trusses, 1x4 P.T. wood firestop, R-4.1 insulation at furring, 4" concrete slab, machine-compacted termite-treated fill
- **Waterproofing**: Underlayment, vapor barrier at slab
- **Building Science**: CMU construction (Florida typical), termite treatment, firestop at wall/floor
- **Related Details**: WALL SECTION GABLE ROOF (MONO-FT), WALL SECTION FLAT ROOF (SPREAD-FT)
- **Drawing Complexity**: Complex (0 lines, 32 components, 25 texts, 10 fills)
- **Element Counts**: 0 lines, 32 components, 25 texts, 10 fills
- **Notes**: Most component-dense wall section. CMU + bond beam detail components.

---

### PROPOSED ROOF PLAN - Section 1
- **File**: PROPOSED_ROOF_PLAN_-_Section_1.json (extracted from project)
- **Type**: Plan
- **Scale**: 1/4" = 1'-0"
- **Roof System**: General (plan view)
- **Condition**: Roof plan
- **Components Used**: None
- **Line Styles**: None
- **Drawing Complexity**: Empty (plan reference only -- content may be model-based)
- **Element Counts**: 0 lines, 0 components, 0 texts, 0 fills

---

### PROPOSED ROOF PLAN - Section 2
- **File**: PROPOSED_ROOF_PLAN_-_Section_2.json (extracted from project)
- **Type**: Plan
- **Scale**: 1/4" = 1'-0"
- **Roof System**: General (plan view)
- **Condition**: Roof plan
- **Drawing Complexity**: Empty (plan reference only)

---

### ROOF FRAMING DETAILS Copy 1
- **File**: ROOF_FRAMING_DETAILS_Copy_1.json (extracted from project)
- **Type**: Framing diagram
- **Scale**: 1/4" = 1'-0"
- **Roof System**: General framing
- **Condition**: Structural framing layout
- **Drawing Complexity**: Empty (content may be in referenced views)

---

## BD Architect Library Files (57 .rvt files)

The following files exist as standalone Revit detail files. No preview images or JSON extractions are available for these. Metadata is inferred from file naming conventions and architectural context.

---

### PARAPET DETAILS (7 files)

#### PARAPET STUCCO CAP DETAIL
- **File**: PARAPET STUCCO CAP DETAIL.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical for cap details)
- **Roof System**: Built-up / modified bitumen / TPO (flat roof)
- **Condition**: Parapet cap with stucco finish
- **Components Used**: (inferred) Metal cap flashing, stucco finish, counterflashing, membrane, blocking
- **Line Styles**: (unknown -- needs extraction)
- **Key Elements**: Stucco-clad parapet cap, metal coping, membrane termination
- **Waterproofing**: Membrane base flashing up parapet face, counterflashing at cap, sealant at transitions
- **Building Science**: Thermal bridge at parapet, moisture management at cap joint, stucco crack control
- **Related Details**: PARAPET WALL DETAIL @ BUILT-UP ROOF, TYP. PARAPET WALL DETAIL
- **Drawing Complexity**: Moderate (estimated)

#### PARAPET WALL DETAIL @ BUILT-UP ROOF
- **File**: PARAPET WALL DETAIL @ BUILT-UP ROOF.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Built-up roof (BUR)
- **Condition**: Parapet wall at BUR
- **Key Elements**: BUR membrane termination, base flashing, counterflashing, metal coping, parapet wall construction, blocking
- **Waterproofing**: BUR plies turned up parapet, base flashing, counterflashing reglet or surface-mounted
- **Building Science**: Wind uplift at parapet, drainage away from parapet, thermal continuity
- **Related Details**: PARAPET WALL DETAIL W_STUCCO @ BUILT-UP ROOF
- **Drawing Complexity**: Moderate

#### PARAPET WALL DETAIL W_STUCCO @ BUILT-UP ROOF
- **File**: PARAPET WALL DETAIL W_STUCCO @ BUILT-UP ROOF.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Built-up roof (BUR)
- **Condition**: Parapet wall with stucco exterior at BUR
- **Key Elements**: Same as above with stucco cladding on parapet exterior face
- **Waterproofing**: BUR base flashing, counterflashing behind stucco, weep screed
- **Building Science**: Stucco moisture management, control joints, weep path
- **Related Details**: PARAPET WALL DETAIL @ BUILT-UP ROOF
- **Drawing Complexity**: Complex (stucco adds layers)

#### SKLAR TYP. PARAPET DETAIL
- **File**: SKLAR TYP. PARAPET DETAIL.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof (general)
- **Condition**: Typical parapet (project-specific -- "Sklar" project)
- **Key Elements**: Standard parapet assembly, coping, membrane, blocking
- **Related Details**: TYP. PARAPET WALL DETAIL
- **Drawing Complexity**: Moderate

#### TYP. PARAPET FLASHING DETAL
- **File**: TYP. PARAPET FLASHING DETAL.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof (general)
- **Condition**: Typical parapet flashing (base + counter)
- **Key Elements**: Base flashing, counterflashing, membrane termination bar, sealant, coping
- **Waterproofing**: Complete parapet flashing system -- base, counter, cap
- **Building Science**: Flashing lap sequence, sealant joints, movement accommodation
- **Related Details**: TYP. PARAPET WALL DETAIL, TYP. PARAPET WALL DETAIL W-INSULATION
- **Drawing Complexity**: Moderate
- **Notes**: Filename has typo ("DETAL" instead of "DETAIL")

#### TYP. PARAPET WALL DETAIL
- **File**: TYP. PARAPET WALL DETAIL.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof (general)
- **Condition**: Typical parapet wall full assembly
- **Key Elements**: Parapet wall construction, coping, membrane, base flashing, counterflashing, blocking, nailer
- **Waterproofing**: Full membrane system at parapet
- **Building Science**: Standard parapet detailing
- **Related Details**: TYP. PARAPET WALL DETAIL W-INSULATION
- **Drawing Complexity**: Moderate

#### TYP. PARAPET WALL DETAIL W-INSULATION
- **File**: TYP. PARAPET WALL DETAIL W-INSULATION.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof (general)
- **Condition**: Parapet wall with continuous insulation
- **Key Elements**: All parapet components plus continuous insulation on parapet face, thermal barrier
- **Waterproofing**: Membrane, base flashing, counterflashing with insulation integration
- **Building Science**: Thermal continuity through parapet (eliminates thermal bridge), R-value maintenance from roof through parapet
- **Related Details**: TYP. PARAPET WALL DETAIL
- **Drawing Complexity**: Complex (additional insulation layers)

---

### SCUPPER AND DRAINAGE DETAILS (9 files)

#### DRAINAGE SCUPPER
- **File**: DRAINAGE SCUPPER.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof (general)
- **Condition**: Primary drainage scupper through parapet
- **Key Elements**: Scupper opening in parapet, conductor head, downspout connection, flashing, membrane
- **Waterproofing**: Membrane lining through scupper, sealed flanges, overflow capacity
- **Building Science**: Primary drainage, min 4" scupper height, sizing per roof area
- **Related Details**: OVERFLOW DRAINAGE SCUPPER, DRAINAGE SCUPPER @ BUILT-UP ROOF
- **Drawing Complexity**: Moderate

#### DRAINAGE SCUPPER @ BUILT-UP ROOF
- **File**: DRAINAGE SCUPPER @ BUILT-UP ROOF.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Built-up roof (BUR)
- **Condition**: Scupper integrated with BUR system
- **Key Elements**: BUR membrane continuity through scupper, metal scupper box, membrane integration
- **Waterproofing**: BUR plies continuous into scupper, soldered or welded metal box
- **Related Details**: DRAINAGE SCUPPER @ BUILT-UP ROOF_WITH STUCCO
- **Drawing Complexity**: Complex

#### DRAINAGE SCUPPER @ BUILT-UP ROOF_WITH STUCCO
- **File**: DRAINAGE SCUPPER @ BUILT-UP ROOF_WITH STUCCO.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Built-up roof (BUR)
- **Condition**: Scupper at stucco parapet with BUR
- **Key Elements**: Scupper through stucco-clad parapet, stucco termination at scupper opening
- **Waterproofing**: BUR + scupper box + stucco moisture management
- **Related Details**: DRAINAGE SCUPPER @ BUILT-UP ROOF
- **Drawing Complexity**: Complex

#### OVERFLOW DRAINAGE SCUPPER
- **File**: OVERFLOW DRAINAGE SCUPPER.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof (general)
- **Condition**: Overflow/emergency scupper (2" above primary drain)
- **Key Elements**: Overflow scupper opening, min 2" above primary, visible discharge
- **Building Science**: Code-required secondary drainage, visible overflow alerts maintenance
- **Related Details**: OVERFLOW SCUPPER, ROOF_OVERFLOW SCUPPER, ROOF_1-PLY OVERFLOW SCUPPER
- **Drawing Complexity**: Simple

#### OVERFLOW SCUPPER
- **File**: OVERFLOW SCUPPER.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof (general)
- **Condition**: Overflow scupper (alternate version)
- **Related Details**: OVERFLOW DRAINAGE SCUPPER
- **Drawing Complexity**: Simple

#### ROOF_1-PLY OVERFLOW SCUPPER
- **File**: ROOF_1-PLY OVERFLOW SCUPPER.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Single-ply membrane (TPO/PVC/EPDM)
- **Condition**: Overflow scupper for single-ply roof
- **Key Elements**: Single-ply membrane termination at scupper, welded membrane flashing
- **Waterproofing**: Membrane boot or welded flashing at scupper penetration
- **Related Details**: ROOF_OVERFLOW SCUPPER
- **Drawing Complexity**: Moderate

#### ROOF_OVERFLOW SCUPPER
- **File**: ROOF_OVERFLOW SCUPPER.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof (general)
- **Condition**: Overflow scupper (roof-prefixed variant)
- **Related Details**: OVERFLOW DRAINAGE SCUPPER, ROOF_1-PLY OVERFLOW SCUPPER
- **Drawing Complexity**: Simple

#### ROOF _ DRAIN DETAIL
- **File**: ROOF _ DRAIN DETAIL.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof (general)
- **Condition**: Interior roof drain
- **Key Elements**: Roof drain body, clamping ring, membrane flashing, insulation taper to drain, gravel stop or strainer
- **Waterproofing**: Membrane clamped between drain flange and clamping ring, tapered insulation to drain
- **Building Science**: Low point drainage, sump at drain, overflow provisions
- **Related Details**: ROOF DRAIN DETAIL, ROOF_MOD BIT ROOF DRAIN
- **Drawing Complexity**: Moderate

#### ROOF DRAIN DETAIL
- **File**: ROOF DRAIN DETAIL.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof (general)
- **Condition**: Interior roof drain (alternate version)
- **Related Details**: ROOF _ DRAIN DETAIL, ROOF_MOD BIT ROOF DRAIN
- **Drawing Complexity**: Moderate

---

### PENETRATION AND FLASHING DETAILS (10 files)

#### ENCLOSURE FOR MULTIPLE PIPING THRU DECK
- **File**: ENCLOSURE FOR MULTIPLE PIPING THRU DECK.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof (general)
- **Condition**: Multiple pipe penetrations with enclosure
- **Key Elements**: Metal enclosure/pitch pan, sealant fill, membrane flashing, support framing
- **Waterproofing**: Enclosure flashed to membrane, sealant fill around pipes, counterflashing
- **Building Science**: Grouped penetrations reduce individual flashings, maintenance access
- **Related Details**: ROOF_ENCLOSURE FOR PIPING, TYP ROOF PENETRATION DETAIL
- **Drawing Complexity**: Complex

#### ROOF_ENCLOSURE FOR PIPING
- **File**: ROOF_ENCLOSURE FOR PIPING.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof (general)
- **Condition**: Single/small pipe group enclosure
- **Related Details**: ENCLOSURE FOR MULTIPLE PIPING THRU DECK
- **Drawing Complexity**: Moderate

#### TYP ROOF PENETRATION DETAIL
- **File**: TYP ROOF PENETRATION DETAIL.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof (general)
- **Condition**: Typical single penetration (pipe, conduit)
- **Key Elements**: Pipe boot or pitch pan, membrane flashing collar, sealant, clamp
- **Waterproofing**: Prefab pipe boot or field-fabricated flashing, membrane lapped over flashing
- **Building Science**: Thermal bridge at penetration, fire-stop if rated assembly
- **Related Details**: ROOF - VENT BOOT FLASHING, ROOF VENT BOOT FLASHING, ROOF_PLUMBING VENT
- **Drawing Complexity**: Simple

#### ROOF - VENT BOOT FLASHING
- **File**: ROOF - VENT BOOT FLASHING.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat or pitched
- **Condition**: Vent pipe boot flashing
- **Key Elements**: Rubber or metal boot, base flange, membrane/shingle integration, clamp
- **Waterproofing**: Boot sealed to pipe, base flange under upper course, over lower course
- **Related Details**: ROOF VENT BOOT FLASHING, ROOF_PLUMBING VENT
- **Drawing Complexity**: Simple

#### ROOF VENT BOOT FLASHING
- **File**: ROOF VENT BOOT FLASHING.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat or pitched
- **Condition**: Vent pipe boot flashing (alternate version)
- **Related Details**: ROOF - VENT BOOT FLASHING
- **Drawing Complexity**: Simple

#### ROOF_PLUMBING VENT
- **File**: ROOF_PLUMBING VENT.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof
- **Condition**: Plumbing vent penetration
- **Key Elements**: Plumbing vent pipe, boot flashing, membrane integration
- **Related Details**: ROOF VENT BOOT FLASHING
- **Drawing Complexity**: Simple

#### ROOF_SURFACE MOUNTED COUNTER FLASH 3D
- **File**: ROOF_SURFACE MOUNTED COUNTER FLASH 3D.rvt
- **Type**: 3D / Isometric
- **Scale**: NTS or 1" = 1'-0"
- **Roof System**: Flat roof (general)
- **Condition**: Surface-mounted counterflashing detail (3D view)
- **Key Elements**: Counterflashing reglet, termination bar, sealant, base flashing below
- **Waterproofing**: Surface-mounted bar with sealant (vs. reglet-set)
- **Building Science**: Easier retrofit than reglet, requires ongoing sealant maintenance
- **Related Details**: TYP. PARAPET FLASHING DETAL
- **Drawing Complexity**: Moderate
- **Notes**: One of few 3D/isometric details in the library. Useful for understanding flashing geometry.

#### ROOF THRU FLASHING @ EQUIPMENT STAND
- **File**: ROOF THRU FLASHING @ EQUIPMENT STAND.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof
- **Condition**: Through-wall flashing at equipment support
- **Key Elements**: Equipment stand, through-flashing, membrane, curb
- **Waterproofing**: Continuous through-flashing at curb base, membrane turned up curb
- **Related Details**: ROOF_THRU FLASHING AT EQUIPMENT STAND
- **Drawing Complexity**: Moderate

#### ROOF_THRU FLASHING AT EQUIPMENT STAND
- **File**: ROOF_THRU FLASHING AT EQUIPMENT STAND.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof
- **Condition**: Through-flashing at equipment stand (alternate version)
- **Related Details**: ROOF THRU FLASHING @ EQUIPMENT STAND
- **Drawing Complexity**: Moderate

#### SCUPPER DETAILS
- **File**: SCUPPER DETAILS.rvt
- **Type**: Section + Plan
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof (general)
- **Condition**: Scupper assembly (may include plan and section)
- **Key Elements**: Scupper box, conductor head, downspout, membrane integration
- **Related Details**: DRAINAGE SCUPPER, OVERFLOW SCUPPER
- **Drawing Complexity**: Complex (likely multi-view)

---

### EQUIPMENT AND CURB DETAILS (6 files)

#### ROOF EQUIPMENT SUPPORT STAND
- **File**: ROOF EQUIPMENT SUPPORT STAND.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof
- **Condition**: Equipment support/dunnage on roof
- **Key Elements**: Steel dunnage frame, roof curb, membrane flashing, vibration isolation pads, blocking
- **Waterproofing**: Membrane turned up curb, counterflashing at equipment base, through-flashing
- **Building Science**: Load distribution to structure, vibration isolation, drainage around curb
- **Related Details**: ROOF_EQUIPMENT SUPPORT STAND, ROOF_CHEM CURB
- **Drawing Complexity**: Moderate

#### ROOF_EQUIPMENT SUPPORT STAND
- **File**: ROOF_EQUIPMENT SUPPORT STAND.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof
- **Condition**: Equipment support stand (alternate version)
- **Related Details**: ROOF EQUIPMENT SUPPORT STAND
- **Drawing Complexity**: Moderate

#### ROOF_CHEM CURB
- **File**: ROOF_CHEM CURB.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof
- **Condition**: Chemical/containment curb on roof
- **Key Elements**: Raised curb for chemical containment, membrane flashing, counterflashing, sealant
- **Waterproofing**: Secondary containment at chemical equipment, membrane continuity
- **Building Science**: Chemical spill containment, environmental protection
- **Related Details**: ROOF EQUIPMENT SUPPORT STAND
- **Drawing Complexity**: Moderate

#### ROOF_PREFAB METAL CURB OLD POLYISO
- **File**: ROOF_PREFAB METAL CURB OLD POLYISO.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof with polyiso insulation
- **Condition**: Prefabricated metal curb at existing polyiso insulation
- **Key Elements**: Prefab metal curb, existing polyiso insulation, membrane termination, blocking
- **Waterproofing**: Membrane turned up prefab curb, factory-welded corners
- **Building Science**: Retrofit/reroofing condition with existing insulation
- **Related Details**: ROOF_EQUIPMENT SUPPORT STAND, ROOF_CHEM CURB
- **Drawing Complexity**: Moderate
- **Notes**: "OLD POLYISO" suggests this is a re-roofing/overlay detail

#### ROOF_MECH DUCT ENCLOSURE
- **File**: ROOF_MECH DUCT ENCLOSURE.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof
- **Condition**: Mechanical duct penetration enclosure
- **Key Elements**: Duct enclosure/chase through roof, flashing, counterflashing, insulation
- **Waterproofing**: Membrane flashing around enclosure, counterflashing, sealant
- **Building Science**: Thermal insulation of duct, fire-stop at roof assembly
- **Related Details**: ENCLOSURE FOR MULTIPLE PIPING THRU DECK
- **Drawing Complexity**: Complex

#### VIBRATION ISOLATION PAD FLASHING W_ FLUID APPLIED MEMBRANE & TPO ROOFING
- **File**: VIBRATION ISOLATION PAD FLASHING W_ FLUID APPLIED MEMBRANE & TPO ROOFING.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: TPO single-ply membrane
- **Condition**: Vibration isolation pad at equipment on TPO roof
- **Key Elements**: Vibration isolation pad, fluid-applied membrane transition, TPO membrane, equipment support
- **Waterproofing**: TPO membrane to fluid-applied transition at pad, compatible chemistry required
- **Building Science**: Vibration isolation for HVAC equipment, membrane compatibility at transition
- **Related Details**: ROOF EQUIPMENT SUPPORT STAND, ROOF_CHEM CURB
- **Drawing Complexity**: Complex
- **Notes**: Hybrid membrane system -- TPO field with fluid-applied at equipment interface

---

### EAVE AND EDGE DETAILS (4 files)

#### DETAIL OF SLOPED EAVE @ FLAT ROOF
- **File**: DETAIL OF SLOPED EAVE @ FLAT ROOF.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof with sloped fascia
- **Condition**: Sloped eave condition at flat roof edge
- **Key Elements**: Tapered edge, fascia board, drip edge, membrane termination, gutter attachment
- **Waterproofing**: Membrane over tapered insulation to edge, drip edge flashing
- **Building Science**: Positive drainage to edge, wind uplift at perimeter zone
- **Related Details**: ROOF - EDGE DETAIL TYP.1 (extracted)
- **Drawing Complexity**: Moderate

#### HIGH ROOF SECTION
- **File**: HIGH ROOF SECTION.rvt
- **Type**: Section
- **Scale**: 1/4" = 1'-0" or 1/8" = 1'-0"
- **Roof System**: General (multi-story building)
- **Condition**: High roof section showing full assembly
- **Key Elements**: Full roof assembly, structural framing, insulation, membrane, parapet or edge
- **Building Science**: Full building envelope section at high roof
- **Related Details**: Wall section details
- **Drawing Complexity**: Complex

#### ROOF EVE @ TOWER
- **File**: ROOF EVE @ TOWER.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Pitched or flat
- **Condition**: Eave condition at architectural tower element
- **Key Elements**: Tower wall-to-roof transition, eave framing, flashing, soffit
- **Building Science**: Complex geometry at tower intersection
- **Related Details**: DETAIL OF SLOPED EAVE @ FLAT ROOF
- **Drawing Complexity**: Complex

#### ROOF SPECIAL CONDITION
- **File**: ROOF SPECIAL CONDITION.rvt
- **Type**: Section
- **Scale**: Varies
- **Roof System**: General
- **Condition**: Non-standard/special roof condition
- **Drawing Complexity**: Unknown (project-specific)

---

### WATERPROOFING AND SPECIALTY DETAILS (7 files)

#### ELEVATOR WATERPROOFING DETAL
- **File**: ELEVATOR WATERPROOFING DETAL.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: N/A (elevator pit)
- **Condition**: Elevator pit waterproofing
- **Key Elements**: Elevator pit walls, waterproof membrane, drainage, sump pit
- **Waterproofing**: Full waterproofing of elevator pit -- critical for below-grade conditions
- **Building Science**: Hydrostatic pressure resistance, sump pump, drainage mat
- **Related Details**: ELEVATOR_WATERPROOFING DETAIL
- **Drawing Complexity**: Complex
- **Notes**: Filename has typo ("DETAL")

#### ELEVATOR_WATERPROOFING DETAIL
- **File**: ELEVATOR_WATERPROOFING DETAIL.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: N/A (elevator pit)
- **Condition**: Elevator pit waterproofing (corrected/alternate version)
- **Related Details**: ELEVATOR WATERPROOFING DETAL
- **Drawing Complexity**: Complex

#### PIT SECTION AT SUMP PUMP W_CRYSTALLINE WATERPROOFING
- **File**: PIT SECTION AT SUMP PUMP W_CRYSTALLINE WATERPROOFING.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: N/A (below-grade pit)
- **Condition**: Sump pump pit with crystalline waterproofing
- **Key Elements**: Sump pit, crystalline waterproofing coating, concrete, sump pump
- **Waterproofing**: Crystalline/capillary waterproofing (Xypex or similar) -- integral concrete treatment
- **Building Science**: Self-healing concrete waterproofing, no membrane required
- **Related Details**: PIT SECTION W_CRYSTALLINE WATERPROOFING
- **Drawing Complexity**: Moderate

#### PIT SECTION W_CRYSTALLINE WATERPROOFING
- **File**: PIT SECTION W_CRYSTALLINE WATERPROOFING.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: N/A (below-grade pit)
- **Condition**: General pit section with crystalline waterproofing
- **Related Details**: PIT SECTION AT SUMP PUMP W_CRYSTALLINE WATERPROOFING
- **Drawing Complexity**: Moderate

#### PLANTER DETAIL
- **File**: PLANTER DETAIL.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof / plaza deck
- **Condition**: Rooftop planter waterproofing
- **Key Elements**: Planter walls, waterproof membrane, drainage layer, filter fabric, growing media, overflow drain
- **Waterproofing**: Full waterproof membrane in planter, drainage mat, overflow protection
- **Building Science**: Root barrier, drainage, overburden weight on structure
- **Related Details**: PLANTER CAP AND WATERPROOFING
- **Drawing Complexity**: Moderate

#### PLANTER CAP AND WATERPROOFING
- **File**: PLANTER CAP AND WATERPROOFING.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof / plaza deck
- **Condition**: Planter cap/coping with waterproofing
- **Key Elements**: Planter cap flashing, waterproof membrane termination, sealant, coping
- **Waterproofing**: Membrane termination at planter cap, counterflashing, sealant
- **Related Details**: PLANTER DETAIL
- **Drawing Complexity**: Moderate

#### SHOWER_GUESTROOM SHOWER WATERPROOFING LAYERING DIAGRAM - 90 DEGREE CORNER
- **File**: SHOWER_GUESTROOM SHOWER WATERPROOFING LAYERING DIAGRAM - 90 DEGREE CORNER.rvt
- **Type**: Diagram / Isometric
- **Scale**: NTS
- **Roof System**: N/A (interior wet area)
- **Condition**: Shower waterproofing at inside corner
- **Key Elements**: Waterproof membrane layers, corner treatment, reinforcement fabric, overlap sequence
- **Waterproofing**: Liquid or sheet membrane layering sequence at 90-degree inside corner
- **Building Science**: Critical leak point -- corner membrane continuity, proper lap sequence
- **Related Details**: Elevator waterproofing details (similar membrane concept)
- **Drawing Complexity**: Moderate
- **Notes**: Hospitality/hotel detail (guestroom shower). Included in roof category likely due to waterproofing classification.

---

### MISCELLANEOUS ROOF DETAILS (10 files)

#### EXPANSION JOINT - SECTION AT ROOF
- **File**: EXPANSION JOINT - SECTION AT ROOF.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof
- **Condition**: Expansion/control joint at roof
- **Key Elements**: Expansion joint cover, membrane termination both sides, curb, sealant, bellows/flexible membrane
- **Waterproofing**: Flexible membrane or bellows spanning joint, sealed to membrane both sides
- **Building Science**: Thermal movement accommodation, structural separation joint, weather seal
- **Related Details**: Parapet details (joint often extends through parapet)
- **Drawing Complexity**: Complex
- **Notes**: Critical structural/envelope detail. Joint must accommodate building movement.

#### ROOF - WALKPAD SYSTEM
- **File**: ROOF - WALKPAD SYSTEM.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof (membrane)
- **Condition**: Walkpad/traffic pad on roof membrane
- **Key Elements**: Walkpad (concrete or rubber), adhesive or mechanical attachment, membrane protection
- **Building Science**: Protects membrane from foot traffic, maintenance paths
- **Related Details**: ROOF-WALKPAD SYSTEM
- **Drawing Complexity**: Simple

#### ROOF-WALKPAD SYSTEM
- **File**: ROOF-WALKPAD SYSTEM.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof (membrane)
- **Condition**: Walkpad system (alternate version)
- **Related Details**: ROOF - WALKPAD SYSTEM
- **Drawing Complexity**: Simple

#### ROOF ACCESS HATCH DETAIL
- **File**: ROOF ACCESS HATCH DETAIL.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof
- **Condition**: Roof access hatch curb and flashing
- **Key Elements**: Hatch frame, curb, membrane flashing, counterflashing, insulation, ladder/stairs
- **Waterproofing**: Membrane up curb, counterflashing at frame, gasket at hatch lid
- **Building Science**: Thermal insulated hatch, fall protection considerations
- **Related Details**: Equipment curb details
- **Drawing Complexity**: Moderate

#### ROOF ANCHOR DETAIL
- **File**: ROOF ANCHOR DETAIL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0" (typical for small details)
- **Roof System**: Flat roof
- **Condition**: Fall protection/safety anchor point
- **Key Elements**: Anchor post, base plate, structural connection, membrane flashing, counterflashing
- **Waterproofing**: Membrane flashing around anchor base, sealant, counterflashing boot
- **Building Science**: Structural capacity for fall arrest loads, OSHA compliance
- **Related Details**: ROOF LIGHTNING PROTECTION
- **Drawing Complexity**: Moderate

#### ROOF CRICKET DIAGRAM
- **File**: ROOF CRICKET DIAGRAM.rvt
- **Type**: Plan/Diagram
- **Scale**: NTS or 1/4" = 1'-0"
- **Roof System**: Flat roof
- **Condition**: Cricket/saddle at roof penetration or equipment
- **Key Elements**: Tapered insulation cricket, drainage direction arrows, dimensional layout
- **Building Science**: Positive drainage diversion around obstructions, prevents ponding
- **Related Details**: ROOF_CRICKET DIAGRAM
- **Drawing Complexity**: Simple (diagram)

#### ROOF_CRICKET DIAGRAM
- **File**: ROOF_CRICKET DIAGRAM.rvt
- **Type**: Plan/Diagram
- **Scale**: NTS or 1/4" = 1'-0"
- **Roof System**: Flat roof
- **Condition**: Cricket/saddle diagram (alternate version)
- **Related Details**: ROOF CRICKET DIAGRAM
- **Drawing Complexity**: Simple (diagram)

#### ROOF LIGHTNING PROTECTION
- **File**: ROOF LIGHTNING PROTECTION.rvt
- **Type**: Section / Detail
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof
- **Condition**: Lightning protection conductor and air terminal at roof
- **Key Elements**: Air terminal (lightning rod), conductor cable, base mount, membrane flashing, clamp
- **Waterproofing**: Membrane flashing at conductor penetration or surface mount
- **Building Science**: UL 96A / NFPA 780 compliance, grounding path, bonding
- **Related Details**: ROOF_LIGHTNING PROTECTION, ROOF ANCHOR DETAIL
- **Drawing Complexity**: Moderate

#### ROOF_LIGHTNING PROTECTION
- **File**: ROOF_LIGHTNING PROTECTION.rvt
- **Type**: Section / Detail
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof
- **Condition**: Lightning protection (alternate version)
- **Related Details**: ROOF LIGHTNING PROTECTION
- **Drawing Complexity**: Moderate

#### ROOF_MOD BIT ROOF DRAIN
- **File**: ROOF_MOD BIT ROOF DRAIN.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Modified bitumen
- **Condition**: Roof drain at modified bitumen membrane
- **Key Elements**: Drain body, clamping ring, mod-bit membrane, tapered insulation, strainer dome
- **Waterproofing**: Mod-bit membrane stripped into drain flange, clamping ring compression seal
- **Building Science**: Tapered insulation to drain sump, positive drainage
- **Related Details**: ROOF DRAIN DETAIL, ROOF _ DRAIN DETAIL
- **Drawing Complexity**: Moderate

---

### SPECIALTY ITEMS (4 files)

#### TRASH CHUTE ROOF CAP
- **File**: TRASH CHUTE ROOF CAP.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof
- **Condition**: Trash chute termination at roof
- **Key Elements**: Chute cap/vent, counterflashing, membrane, rain protection
- **Waterproofing**: Membrane flashing at chute penetration, cap for weather protection
- **Building Science**: Ventilation of chute, fire damper, weather cap
- **Related Details**: TRASH CHUTE DISCHARGE
- **Drawing Complexity**: Moderate

#### TRASH CHUTE DISCHARGE
- **File**: TRASH CHUTE DISCHARGE.rvt
- **Type**: Section
- **Scale**: 1/4" = 1'-0" (typical)
- **Roof System**: N/A (building section)
- **Condition**: Trash chute bottom/discharge
- **Key Elements**: Discharge door, compactor connection, fire sprinkler, enclosure
- **Related Details**: TRASH CHUTE ROOF CAP
- **Drawing Complexity**: Moderate

#### ROOF DECK DETAIL @ CMU
- **File**: ROOF DECK DETAIL @ CMU.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof on CMU bearing wall
- **Condition**: Roof deck connection at CMU wall
- **Key Elements**: Metal deck, CMU wall, bond beam, anchor bolts, ledger, membrane
- **Waterproofing**: Membrane termination at wall transition
- **Building Science**: Bearing connection, lateral tie, bond beam reinforcement
- **Related Details**: ROOF DECK DETAIL @ CMU W STUCCO
- **Drawing Complexity**: Moderate

#### ROOF DECK DETAIL @ CMU W STUCCO
- **File**: ROOF DECK DETAIL @ CMU W STUCCO.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Roof System**: Flat roof on CMU bearing wall with stucco
- **Condition**: Roof deck at stucco-clad CMU
- **Key Elements**: Same as CMU version plus stucco finish, control joints, weep screed
- **Waterproofing**: Membrane + stucco moisture management
- **Related Details**: ROOF DECK DETAIL @ CMU
- **Drawing Complexity**: Complex

---

## Group Summaries

### By Roof Type

| Roof Type | File Count | Key Details |
|-----------|-----------|-------------|
| **Built-Up Roof (BUR)** | 3 | Parapet @ BUR, Drainage scupper @ BUR, Parapet w/stucco @ BUR |
| **Modified Bitumen** | 1 | ROOF_MOD BIT ROOF DRAIN |
| **TPO/Single-Ply** | 4 | Edge detail, Gutter detail, 1-ply overflow scupper, Vibration isolation w/ TPO |
| **Concrete Tile (Pitched)** | 6 | Eave (3 variants), Mansard cap, Ridge/hip, Sidewall flashing, Valley |
| **Shingle/Underlayment** | 1 | ROOF DETAIL-1 |
| **General/System-Agnostic** | 42+ | Most parapet, penetration, equipment, waterproofing details |

### By Condition Type

| Condition | File Count | Notes |
|-----------|-----------|-------|
| **Parapet** | 7 | Standard, w/stucco, w/insulation, @ BUR, flashing |
| **Eave/Edge** | 7 | Sloped eave, tile eave (3), edge detail, gutter, tower eave |
| **Drain** | 3 | Roof drain (2), mod-bit drain |
| **Scupper (Primary)** | 4 | Standard, @ BUR, @ BUR w/stucco, scupper details |
| **Scupper (Overflow)** | 4 | Overflow drainage, overflow (2), 1-ply overflow |
| **Penetration/Flashing** | 6 | Vent boot (2), plumbing vent, roof penetration, piping enclosure (2) |
| **Equipment/Curb** | 6 | Equipment stand (2), chem curb, prefab curb, mech duct, vibration pad |
| **Ridge/Hip/Valley** | 2 | Ridge/hip, valley |
| **Cricket** | 2 | Cricket diagram (2 variants) |
| **Wall Section** | 4 | Flat roof mono/spread, gable roof mono/spread |
| **Waterproofing** | 7 | Elevator (2), pit (2), planter (2), shower corner |
| **Walkpad** | 2 | Walkpad system (2 variants) |
| **Lightning** | 2 | Lightning protection (2 variants) |
| **Access/Safety** | 2 | Roof hatch, roof anchor |
| **Expansion Joint** | 1 | Expansion joint at roof |
| **Terrace/Plaza** | 1 | Rooftop terrace paver detail |
| **Deck Connection** | 2 | Roof deck @ CMU, @ CMU w/stucco |
| **Specialty** | 3 | Trash chute cap/discharge, special condition |

### By Special Conditions

| Special Condition | Files | Description |
|-------------------|-------|-------------|
| **Waterproofing (below-grade)** | 4 | Elevator pit, sump pit (crystalline system) |
| **Waterproofing (wet area)** | 1 | Shower corner membrane layering |
| **Waterproofing (planter/terrace)** | 3 | Planter, planter cap, rooftop terrace pavers |
| **Expansion Joint** | 1 | Structural separation at roof level |
| **Lightning Protection** | 2 | Air terminal and conductor routing |
| **Re-roofing/Retrofit** | 1 | Prefab curb at existing polyiso |
| **3D/Isometric Views** | 1 | Surface-mounted counterflashing 3D |
| **Hybrid Membrane** | 1 | TPO + fluid-applied at vibration pad |

---

## Revit Component Library (from extracted details)

### Detail Component Families Used

| Family | Types Used | Detail Context |
|--------|-----------|---------------|
| Break Line / Break Line5 | Break Line | All details (assembly termination) |
| 00-BREAK LINE | 008 - 1 1/2" = 1'-0" | Tile details, eave details |
| 04-CMU-2 Core-Section | 8" x 8" x 16" | CMU wall at eave |
| 06-FRAMING-WOOD-Section-Stretch1 | 2X | Wood framing in eaves |
| 06-SHEATHING-Plywood-Section1 | 3/4" | Roof/wall sheathing |
| 07-INSULATION-RIGID | 1 1/2" | Rigid insulation at CMU |
| 09-FRAMING_CHANNEL | 01.5" x 1.375" | Hat channel furring |
| Bond Beam - 001 / Bond Beams-Single-Section1 | 8" x 8" x 16" | CMU bond beam |
| Gypsum Plaster-Section | 3/4" | Interior finish |
| Gypsum Sheathing-Section | 1/2" | Exterior sheathing |
| Nominal Cut Lumber-Section | 1x3, 1x6, 2x8, 2x12 | Framing, blocking, furring |
| Plywood-Section | 3/4", 5/8" | Sheathing, subfloor |
| Reinf Bar Section | #5 | Foundation reinforcement |
| Underlayment | Single Layer | Roof underlayment |

### Line Style Conventions

Two distinct drafting conventions are present in this library:

**Convention A: Semantic Line Styles (highest quality)**
Used in: ROOF - EDGE DETAIL, ROOF - GUTTER DETAIL
- `INSULATION` -- wavy insulation representation
- `MEMBRANE` -- roof membrane lines
- `METAL` -- metal flashing/components
- `SUBSTRATE` -- structural substrate
- `BLOCKING` -- wood blocking
- `FASTENERS` -- nails, screws
- `ANCHORS` -- anchor bolts, clips
- `Line-1` / `Line-2` / `Line-3` -- weight hierarchy (heavy/medium/light)

**Convention B: Pen-Weight Line Styles**
Used in: ROOF EAVE DETAIL, ROOF TILE details
- `Pen 01` -- heaviest weight (profile lines)
- `Pen 03` -- medium weight (internal lines)
- `1` / `3` / `4` -- numeric pen weights
- `01-Line-Fine` / `02-Line-Very Thin` / `03-Line-Thin` -- descriptive weights
- `1_DASH B (x0.5)` -- hidden/dashed lines
- `Waterproofing Membrane` -- dedicated membrane style

**Recommendation for programmatic replication:** Convention A (semantic) is preferred for AI-driven detail generation because line style names carry material meaning, making automated element identification and modification possible.

---

## Duplicate/Variant Analysis

Several details exist in multiple versions. When selecting for replication:

| Detail | Variants | Recommended |
|--------|----------|-------------|
| Overflow Scupper | 4 files | ROOF_1-PLY OVERFLOW SCUPPER (membrane-specific) |
| Roof Drain | 3 files | ROOF_MOD BIT ROOF DRAIN (system-specific) |
| Equipment Stand | 2 files | Either (likely same content) |
| Lightning Protection | 2 files | Either (likely same content) |
| Cricket Diagram | 2 files | Either (likely same content) |
| Walkpad | 2 files | Either (likely same content) |
| Vent Boot | 2 files + plumbing vent | Pick based on roof type |
| Elevator WP | 2 files | ELEVATOR_WATERPROOFING DETAIL (corrected name) |
| Thru Flashing | 2 files | Either (likely same content) |
| Parapet Wall | 3 files | TYP. PARAPET WALL DETAIL W-INSULATION (most complete) |

---

## Notes for Programmatic Replication

1. **Scale mapping**: Details cluster around three scales:
   - 1/2" = 1'-0" for close-up flashings and edge conditions
   - 1/8" = 1'-0" for typical section details (tile, eave, wall sections)
   - 1/16" = 1'-0" for full wall sections
   - 1-1/2" = 1'-0" estimated for library .rvt files (standard BD Architect scale)

2. **Component vs. linework**: Two approaches coexist:
   - **Component-heavy** (wall sections, ROOF DETAIL-1): Use Revit detail components for materials
   - **Linework-heavy** (edge detail, gutter, valley): Draw with detail lines + filled regions
   - Most tile details use a hybrid approach

3. **Text annotation patterns**:
   - Material callouts use uppercase text
   - Dimension references (spacing, thickness) inline with callout
   - Slope indicators shown as rise/run triangles (e.g., "4 / 12")
   - "SEE STRUCTURAL" cross-references to other disciplines
   - NOA references for Florida hurricane zone compliance

4. **Filled regions**: Used extensively for material hatching (concrete, insulation, earth, gravel). Pattern names not captured in extraction -- would need Revit API query.

5. **Missing data**: The 57 .rvt files in the BD Architect library have NOT been opened/extracted yet. Preview PNGs are not generated for this category. Full metadata would require opening each file in Revit and running the detail extraction MCP method.
