# Window Details Catalog - BD Architect Detail Library

**Source:** `/mnt/d/BDArchitect-DetailLibrary-2025/removed_2026/06 - Window Details/`
**Total Files:** 84 .rvt detail files
**Preview Images:** 4 available in `/previews/06 - Window Details/`
**Generated:** 2026-03-05

---

## Table of Contents

1. [Single Hung Windows](#single-hung-windows)
2. [Casement Windows](#casement-windows)
3. [Sliding / Horizontal Rolling Windows](#sliding--horizontal-rolling-windows)
4. [Fixed Windows / Panels](#fixed-windows--panels)
5. [Sliding Glass Doors (SGD)](#sliding-glass-doors-sgd)
6. [Storefront Systems](#storefront-systems)
7. [Window Wall (WW 7000 Series)](#window-wall-ww-7000-series)
8. [CGI Impact Windows](#cgi-impact-windows)
9. [Specialty Glazing (Color Glass / No Glass Frames)](#specialty-glazing-color-glass--no-glass-frames)
10. [Skylights](#skylights)
11. [Elevator Doors / Openings](#elevator-doors--openings)
12. [Louver Openings](#louver-openings)
13. [Specialty Doors (Bi-Fold, Cascade, Nana)](#specialty-doors-bi-fold-cascade-nana)
14. [General / Multi-Condition Window Details](#general--multi-condition-window-details)
15. [Miscellaneous / Accessory Details](#miscellaneous--accessory-details)

---

## Summary by Condition

| Condition | Count | Files |
|-----------|-------|-------|
| Head | 14 | Window heads, storefront heads, SGD heads, concealed shade |
| Sill | 28 | Window sills, SGD sills, storefront sills, door sills, skylight sill |
| Jamb | 16 | Window jambs, SGD jambs, storefront jambs, elevator jambs, louver jambs |
| Mullion | 8 | Horizontal/vertical mullions, intermediate, transom, GWB-to-mullion |
| Combined | 8 | Head+jamb combos, head+sill combos, full detail sheets |
| Other | 10 | Cornice, termination, threshold, fixed panel, skylight, roof access |

## Summary by Wall System

| Wall System | Count | Notes |
|-------------|-------|-------|
| Concrete (cast-in-place slab/wall) | ~30 | Dominant system - high-rise multi-family |
| Stucco over frame | ~8 | Residential/low-rise commercial |
| CMU | ~3 | Ground floor / utility |
| GWB / Interior partition | ~5 | Interior storefront, GWB-to-mullion |
| Curtain wall / Storefront aluminum | ~20 | Storefront and window wall systems |
| Mixed / unspecified | ~18 | Generic or multiple wall conditions shown |

---

## Single Hung Windows

### SINGLE HUNG WINDOW
- **File**: SINGLE HUNG WINDOW.rvt
- **Type**: Section (composite - likely head/jamb/sill on one sheet)
- **Scale**: 1-1/2" = 1'-0" (typical)
- **Window System**: Single hung
- **Wall System**: Stucco over frame or concrete
- **Condition**: Full section (head, jamb, sill combined)
- **Components Used**: Window frame profile (detail item), glass pane, sealant bead, flashing
- **Line Styles**: Wide Lines (wall/slab profiles), Medium Lines (frame sections), Thin Lines (sealant, flashing)
- **Key Elements**: Operable lower sash, fixed upper lite, meeting rail, balance mechanism pocket, frame extrusion profile
- **Waterproofing**: Head flashing, sill pan flashing, sealant at perimeter
- **Building Science**: Thermal break at frame, weep holes at sill track
- **Related Details**: WINDOW SINGLE HUNG HEAD - B.O. SLAB, WINDOW SINGLE HUNG HEAD - HEADER, WINDOW_SINGLE HUNG SILL
- **Drawing Complexity**: Moderate

### WINDOW SINGLE HUNG HEAD - B.O. SLAB
- **File**: WINDOW SINGLE HUNG HEAD - B.O. SLAB.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Single hung
- **Wall System**: Concrete slab (bottom of slab condition)
- **Condition**: Head
- **Components Used**: Concrete slab profile, window head frame, sealant, shim space, interior GWB return
- **Line Styles**: Wide Lines (slab edge), Medium Lines (frame), Thin Lines (sealant, GWB)
- **Key Elements**: Window frame seated against underside of concrete slab, no lintel required, direct slab bearing
- **Waterproofing**: Sealant joint at head, no flashing (slab acts as head)
- **Building Science**: Thermal bridge at slab edge, insulation at frame-to-slab gap
- **Related Details**: WINDOW SINGLE HUNG HEAD - HEADER, WINDOW_SINGLE HUNG SILL
- **Drawing Complexity**: Simple

### WINDOW SINGLE HUNG HEAD - HEADER
- **File**: WINDOW SINGLE HUNG HEAD - HEADER.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Single hung
- **Wall System**: Wood or steel stud framed wall with header
- **Condition**: Head
- **Components Used**: Header (wood or steel), cripple studs, sheathing, weather barrier, frame, interior GWB
- **Line Styles**: Wide Lines (header), Medium Lines (frame, sheathing), Thin Lines (weather barrier, sealant)
- **Key Elements**: Lintel/header above opening, shim space, rough opening to frame relationship
- **Waterproofing**: Head flashing over window, weather barrier lapped over flashing
- **Building Science**: Insulation in header cavity, air barrier continuity at rough opening
- **Related Details**: WINDOW SINGLE HUNG HEAD - B.O. SLAB, WINDOW_SINGLE HUNG SILL
- **Drawing Complexity**: Moderate

### WINDOW_SINGLE HUNG SILL
- **File**: WINDOW_SINGLE HUNG SILL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Single hung
- **Wall System**: Concrete or framed
- **Condition**: Sill
- **Components Used**: Sill profile, sill pan flashing, weep holes, interior stool/apron or GWB return
- **Line Styles**: Wide Lines (wall/slab below), Medium Lines (frame, sill), Thin Lines (flashing, sealant)
- **Key Elements**: Sill slope to exterior, weep system, sill track for operable sash
- **Waterproofing**: Pan flashing with back dam, weep holes through sill, sealant at interior
- **Building Science**: Sill is primary leak point, slope critical (min 1/4" pitch)
- **Related Details**: WINDOW SINGLE HUNG HEAD - B.O. SLAB, WINDOW SINGLE HUNG HEAD - HEADER
- **Drawing Complexity**: Moderate

---

## Casement Windows

### WINDOW CASEMENT HEAD - B.O. SLAB
- **File**: WINDOW CASEMENT HEAD - B.O. SLAB.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Casement
- **Wall System**: Concrete slab (bottom of slab)
- **Condition**: Head
- **Components Used**: Concrete slab, casement frame head profile, sealant, GWB soffit return
- **Line Styles**: Wide Lines (slab), Medium Lines (frame), Thin Lines (sealant, finish)
- **Key Elements**: Casement hinge arm clearance at head, frame profile differs from single hung (deeper for hinge hardware)
- **Waterproofing**: Sealant at frame-to-slab, no flashing needed at B.O. slab
- **Building Science**: Thermal break in casement frame, slab edge insulation
- **Related Details**: WINDOW_DETAIL @ CONCRETE SILL-CASEMENT
- **Drawing Complexity**: Simple

### WINDOW_DETAIL @ CONCRETE SILL-CASEMENT
- **File**: WINDOW_DETAIL @ CONCRETE SILL-CASEMENT.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Casement
- **Wall System**: Concrete
- **Condition**: Sill
- **Components Used**: Concrete sill/wall, casement sill frame, sealant, weep, interior finish
- **Line Styles**: Wide Lines (concrete), Medium Lines (frame), Thin Lines (sealant)
- **Key Elements**: Casement sill profile (flush for outswing operation), weep drainage
- **Waterproofing**: Sill pan, weep holes, sealant at exterior and interior
- **Building Science**: Drainage plane continuity, no obstructions in weep path
- **Related Details**: WINDOW CASEMENT HEAD - B.O. SLAB
- **Drawing Complexity**: Moderate

---

## Sliding / Horizontal Rolling Windows

### SLIDING WINDOW HEAD DTL
- **File**: SLIDING WINDOW HEAD DTL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Sliding (horizontal)
- **Wall System**: Stucco over frame or concrete
- **Condition**: Head
- **Components Used**: Head frame with track, sealant, flashing, interior trim
- **Line Styles**: Wide Lines (wall), Medium Lines (frame/track), Thin Lines (sealant, flashing)
- **Key Elements**: Upper track for sliding panel, head frame extrusion, weatherstrip
- **Waterproofing**: Head flashing, sealant at perimeter
- **Building Science**: Track drainage, thermal break at frame
- **Related Details**: SLIDING WINDOW SILL DTL
- **Drawing Complexity**: Simple

### SLIDING WINDOW SILL DTL
- **File**: SLIDING WINDOW SILL DTL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Sliding (horizontal)
- **Wall System**: Stucco over frame or concrete
- **Condition**: Sill
- **Components Used**: Sill track (double or triple track), sill pan, weep holes, interior finish
- **Line Styles**: Wide Lines (wall below), Medium Lines (track/frame), Thin Lines (sealant, flashing)
- **Key Elements**: Sliding track profile (critical for operation and drainage), roller mechanism space
- **Waterproofing**: Sill track with integral weep, pan flashing below, slope to exterior
- **Building Science**: Track drainage is critical - water enters track during rain events
- **Related Details**: SLIDING WINDOW HEAD DTL
- **Drawing Complexity**: Moderate

### HORIZONTAL ROLLING WINDOW JAMB DETAIL
- **File**: HORIZONTAL ROLLING WINDOW JAMB DETAIL.rvt
- **Type**: Section (plan cut)
- **Scale**: 3" = 1'-0"
- **Window System**: Horizontal rolling
- **Wall System**: Concrete or stucco over frame
- **Condition**: Jamb
- **Components Used**: Jamb frame profile, sealant, backer rod, interior trim/GWB return
- **Line Styles**: Wide Lines (wall), Medium Lines (frame), Thin Lines (sealant, weatherstrip)
- **Key Elements**: Fixed panel jamb pocket, interlock at meeting stile, weatherstrip compression
- **Waterproofing**: Sealant and backer rod at exterior jamb, weatherstrip at interlock
- **Building Science**: Air infiltration at jamb interlock
- **Related Details**: HORIZONTAL ROLLING WINDOW SILL DETAIL, HORIZONTAL_ROLLING_WINDOW_DETAIL @ CONC HEADER
- **Drawing Complexity**: Moderate

### HORIZONTAL ROLLING WINDOW SILL DETAIL
- **File**: HORIZONTAL ROLLING WINDOW SILL DETAIL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Horizontal rolling
- **Wall System**: Concrete or stucco over frame
- **Condition**: Sill
- **Components Used**: Sill track, pan flashing, weep, interior finish
- **Line Styles**: Wide Lines (wall), Medium Lines (track), Thin Lines (flashing, sealant)
- **Key Elements**: Rolling track at sill, drainage path, roller pocket
- **Waterproofing**: Pan flashing, track weeps, slope to exterior
- **Building Science**: Track water management critical for hurricane zones
- **Related Details**: HORIZONTAL ROLLING WINDOW JAMB DETAIL
- **Drawing Complexity**: Moderate

### HORIZONTAL_ROLLING_WINDOW_DETAIL @ CONC HEADER
- **File**: HORIZONTAL_ROLLING_WINDOW_DETAIL @ CONC HEADER.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Horizontal rolling
- **Wall System**: Concrete header/beam
- **Condition**: Head
- **Components Used**: Concrete beam, head frame, sealant, shim, GWB return
- **Line Styles**: Wide Lines (concrete beam), Medium Lines (frame), Thin Lines (sealant)
- **Key Elements**: Frame seated against concrete header, no separate lintel
- **Waterproofing**: Sealant at frame-to-concrete
- **Building Science**: Thermal bridge at concrete header
- **Related Details**: HORIZONTAL ROLLING WINDOW JAMB DETAIL, HORIZONTAL ROLLING WINDOW SILL DETAIL
- **Drawing Complexity**: Simple

### WINDOW - HORIZTONAL ROLLING JAMB
- **File**: WINDOW - HORIZTONAL ROLLING JAMB.rvt
- **Type**: Section (plan cut)
- **Scale**: 3" = 1'-0"
- **Window System**: Horizontal rolling
- **Wall System**: Concrete or stucco
- **Condition**: Jamb
- **Components Used**: Jamb frame, sealant, backer rod, interior return
- **Line Styles**: Wide Lines (wall), Medium Lines (frame), Thin Lines (sealant)
- **Key Elements**: Jamb pocket for sliding panel, similar to HORIZONTAL ROLLING WINDOW JAMB DETAIL but possibly different wall condition
- **Waterproofing**: Perimeter sealant
- **Building Science**: Air seal at jamb
- **Related Details**: HORIZONTAL ROLLING WINDOW JAMB DETAIL
- **Drawing Complexity**: Simple

---

## Fixed Windows / Panels

### FIXED PANEL
- **File**: FIXED PANEL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Fixed
- **Wall System**: Concrete or frame
- **Condition**: Full section or jamb
- **Components Used**: Fixed frame extrusion, glazing tape/gasket, glass lite, sealant
- **Line Styles**: Wide Lines (wall), Medium Lines (frame), Thin Lines (glazing tape, sealant)
- **Key Elements**: Non-operable frame profile (simpler than operable), structural glazing tape or wet seal
- **Waterproofing**: Perimeter sealant, no weep needed (non-operable, sealed unit)
- **Building Science**: Best thermal performance of window types (no operable seals to fail)
- **Related Details**: FIXED WINDOW SILL DETAIL
- **Drawing Complexity**: Simple

### FIXED WINDOW SILL DETAIL
- **File**: FIXED WINDOW SILL DETAIL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Fixed
- **Wall System**: Concrete or frame
- **Condition**: Sill
- **Components Used**: Fixed sill frame, sealant, pan flashing, interior finish
- **Line Styles**: Wide Lines (wall below), Medium Lines (frame), Thin Lines (sealant, flashing)
- **Key Elements**: Simpler sill profile than operable windows (no track), sealed glazing pocket
- **Waterproofing**: Pan flashing, perimeter sealant
- **Building Science**: Fewer leak paths than operable windows
- **Related Details**: FIXED PANEL
- **Drawing Complexity**: Simple

---

## Sliding Glass Doors (SGD)

### SGD 2020 HEAD @ HEADER
- **File**: SGD 2020 HEAD @ HEADER.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: SGD (Series 2020 - standard residential sliding glass door)
- **Wall System**: Framed with header
- **Condition**: Head
- **Components Used**: Wood/steel header, SGD head frame, track housing, sealant, GWB
- **Line Styles**: Wide Lines (header/wall), Medium Lines (frame), Thin Lines (sealant, weatherstrip)
- **Key Elements**: Header supporting SGD, head track for sliding panel, weatherstrip
- **Waterproofing**: Head flashing, sealant
- **Building Science**: Header sizing for wide openings
- **Related Details**: SGD 2020 JAMB, SGD 2020 SILL @ BALCONY, SGD 2020 SILL @ SLAD EDGE, SGD 2020 SILL @ TERRACE
- **Drawing Complexity**: Moderate

### SGD 2020 JAMB
- **File**: SGD 2020 JAMB.rvt
- **Type**: Section (plan cut)
- **Scale**: 3" = 1'-0"
- **Window System**: SGD (Series 2020)
- **Wall System**: Concrete or framed
- **Condition**: Jamb
- **Components Used**: Jamb frame, interlock at meeting stile, sealant, backer rod, interior return
- **Line Styles**: Wide Lines (wall), Medium Lines (frame), Thin Lines (sealant, weatherstrip)
- **Key Elements**: Fixed panel pocket, sliding panel interlock, meeting stile weatherstrip
- **Waterproofing**: Perimeter sealant, weatherstrip at interlock
- **Building Science**: Air infiltration at meeting stile
- **Related Details**: SGD 2020 HEAD @ HEADER, SGD 2020 SILL variants
- **Drawing Complexity**: Moderate

### SGD 2020 SILL @ BALCONY
- **File**: SGD 2020 SILL @ BALCONY.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: SGD (Series 2020)
- **Wall System**: Concrete slab at balcony
- **Condition**: Sill at balcony transition
- **Components Used**: Concrete slab edge, SGD sill track, threshold, waterproofing membrane, tile/paver
- **Line Styles**: Wide Lines (slab), Medium Lines (track/threshold), Thin Lines (membrane, sealant)
- **Key Elements**: Interior-to-exterior floor level transition, threshold height for ADA, balcony slope away from door
- **Waterproofing**: Membrane under threshold, sill pan, balcony waterproofing
- **Building Science**: Water management at balcony transition is critical leak location
- **Related Details**: SGD 2020 SILL @ SLAD EDGE, SGD 2020 SILL @ TERRACE
- **Drawing Complexity**: Complex

### SGD 2020 SILL @ SLAD EDGE
- **File**: SGD 2020 SILL @ SLAD EDGE.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: SGD (Series 2020)
- **Wall System**: Concrete slab edge
- **Condition**: Sill at slab edge (no balcony)
- **Components Used**: Slab edge, SGD sill track, threshold, exterior finish below
- **Line Styles**: Wide Lines (slab), Medium Lines (track), Thin Lines (sealant, finish)
- **Key Elements**: SGD at building perimeter without balcony, slab edge exposed, wall below
- **Waterproofing**: Sill pan, slab edge waterproofing, drip edge below
- **Building Science**: Slab edge thermal bridge, no balcony drainage to manage
- **Related Details**: SGD 2020 SILL @ BALCONY, SGD 2020 SILL @ TERRACE
- **Drawing Complexity**: Moderate

### SGD 2020 SILL @ TERRACE
- **File**: SGD 2020 SILL @ TERRACE.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: SGD (Series 2020)
- **Wall System**: Concrete slab at terrace (ground or podium level)
- **Condition**: Sill at terrace/patio
- **Components Used**: Concrete slab, SGD sill, threshold, paver/topping on terrace, waterproofing
- **Line Styles**: Wide Lines (slab/topping), Medium Lines (track/threshold), Thin Lines (membrane, sealant)
- **Key Elements**: Terrace-to-interior transition, drainage plane, threshold height
- **Waterproofing**: Terrace waterproofing membrane, threshold pan, slope away from door
- **Building Science**: Terrace drainage, threshold ADA compliance
- **Related Details**: SGD 2020 SILL @ BALCONY, SGD 2020 SILL @ SLAD EDGE
- **Drawing Complexity**: Complex

### SGD 2400ST JAMB
- **File**: SGD 2400ST JAMB.rvt
- **Type**: Section (plan cut)
- **Scale**: 3" = 1'-0"
- **Window System**: SGD (Series 2400ST - heavy commercial/impact)
- **Wall System**: Concrete
- **Condition**: Jamb
- **Components Used**: Heavy jamb frame (larger profile than 2020), sealant, structural attachment, interior return
- **Line Styles**: Wide Lines (concrete wall), Medium Lines (heavy frame), Thin Lines (sealant)
- **Key Elements**: Heavier frame profile for impact rating, structural anchoring to concrete, larger weatherstrip
- **Waterproofing**: Sealant, weatherstrip
- **Building Science**: Impact-rated assembly for hurricane zones (Florida HVHZ)
- **Related Details**: SGD 2400ST SILL @ BALCONY, SGD 2400ST SILL @ TERRACE
- **Drawing Complexity**: Moderate

### SGD 2400ST SILL @ BALCONY
- **File**: SGD 2400ST SILL @ BALCONY.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: SGD (Series 2400ST)
- **Wall System**: Concrete slab at balcony
- **Condition**: Sill at balcony
- **Components Used**: Concrete slab, heavy sill track, threshold, waterproofing, balcony topping
- **Line Styles**: Wide Lines (slab), Medium Lines (heavy track), Thin Lines (membrane, sealant)
- **Key Elements**: Heavy-duty sill track, impact-rated threshold, balcony drainage
- **Waterproofing**: Full membrane under threshold, balcony slope
- **Building Science**: Hurricane-rated assembly, water management
- **Related Details**: SGD 2400ST JAMB, SGD 2400ST SILL @ TERRACE
- **Drawing Complexity**: Complex

### SGD 2400ST SILL @ TERRACE
- **File**: SGD 2400ST SILL @ TERRACE.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: SGD (Series 2400ST)
- **Wall System**: Concrete slab at terrace
- **Condition**: Sill at terrace
- **Components Used**: Concrete slab, heavy sill track, threshold, terrace topping, waterproofing
- **Line Styles**: Wide Lines (slab/topping), Medium Lines (heavy track), Thin Lines (membrane)
- **Key Elements**: Terrace transition, impact-rated threshold
- **Waterproofing**: Terrace membrane, threshold pan
- **Building Science**: Grade-level water management
- **Related Details**: SGD 2400ST JAMB, SGD 2400ST SILL @ BALCONY
- **Drawing Complexity**: Complex

### SLIDING GLASS DOOR
- **File**: SLIDING GLASS DOOR.rvt
- **Type**: Section (composite - multiple conditions)
- **Scale**: 1-1/2" = 1'-0"
- **Window System**: Sliding glass door (generic)
- **Wall System**: Varies
- **Condition**: Full assembly (head, jamb, sill)
- **Components Used**: Door frame profiles, track, threshold, sealant, flashing
- **Line Styles**: Wide Lines (wall), Medium Lines (frame), Thin Lines (sealant, hardware)
- **Key Elements**: Complete SGD assembly, meeting stile interlock, threshold
- **Waterproofing**: Head flashing, sill pan, perimeter sealant
- **Building Science**: Full SGD water/air management
- **Related Details**: All SGD details
- **Drawing Complexity**: Complex

### SLIDING GLASS DOOR JAMB FIXED @ MULL
- **File**: SLIDING GLASS DOOR JAMB FIXED @ MULL.rvt
- **Type**: Section (plan cut)
- **Scale**: 3" = 1'-0"
- **Window System**: Sliding glass door
- **Wall System**: Concrete or frame
- **Condition**: Jamb at mullion (fixed panel to fixed panel or fixed to wall)
- **Components Used**: Mullion connector, fixed panel frame, structural mullion, sealant
- **Line Styles**: Wide Lines (wall), Medium Lines (mullion/frame), Thin Lines (sealant, gasket)
- **Key Elements**: Mullion connection between SGD and adjacent fixed panel or wall
- **Waterproofing**: Mullion gaskets, sealant at frame-to-mullion
- **Building Science**: Mullion thermal break, structural capacity for wind loads
- **Related Details**: SLIDING GLASS DOOR
- **Drawing Complexity**: Moderate

---

## Storefront Systems

### STOREFRONT HEAD DETAIL
- **File**: STOREFRONT HEAD DETAIL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Storefront (aluminum)
- **Wall System**: Concrete or CMU
- **Condition**: Head
- **Components Used**: Storefront head receptor, anchor clip, sealant, interior finish
- **Line Styles**: Wide Lines (structure above), Medium Lines (storefront frame), Thin Lines (sealant, clip)
- **Key Elements**: Head receptor channel, clip attachment to structure, deflection gap for structural movement
- **Waterproofing**: Sealant at head, deflection gap sealed with backer rod and sealant
- **Building Science**: Deflection accommodation critical (floor-to-floor movement)
- **Related Details**: STOREFRONT SILL DETAIL, STOREFRONT DOOR HEAD_JAMB DTL
- **Drawing Complexity**: Moderate

### STOREFRONT SILL DETAIL
- **File**: STOREFRONT SILL DETAIL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Storefront (aluminum)
- **Wall System**: Concrete sill/curb
- **Condition**: Sill
- **Components Used**: Sill receptor, sill flashing, weep holes, anchor, interior finish
- **Line Styles**: Wide Lines (concrete sill), Medium Lines (storefront frame), Thin Lines (flashing, sealant)
- **Key Elements**: Sill receptor on curb, flashing under receptor, weep path to exterior
- **Waterproofing**: Through-sill flashing, weep holes in glazing pocket, sealant
- **Building Science**: Sill is primary water entry point for storefront
- **Related Details**: STOREFRONT HEAD DETAIL
- **Drawing Complexity**: Moderate

### STOREFRONT DOOR HEAD_JAMB DTL
- **File**: STOREFRONT DOOR HEAD_JAMB DTL.rvt
- **Type**: Section (combined head and jamb)
- **Scale**: 3" = 1'-0"
- **Window System**: Storefront door
- **Wall System**: Concrete or CMU (no stucco)
- **Condition**: Head and jamb (combined detail)
- **Components Used**: Door frame (heavier than glazing frame), closer reinforcement, pivot/butt hinge blocking, sealant
- **Line Styles**: Wide Lines (structure), Medium Lines (door frame), Thin Lines (sealant, hardware)
- **Key Elements**: Door frame is heavier section than glazing frame, closer pocket or surface mount, threshold transition
- **Waterproofing**: Sealant at perimeter
- **Building Science**: Door frame thermal performance lower than glazing (hardware penetrations)
- **Related Details**: STOREFRONT DOOR HEAD_JAMB (STUCCO) DTL, DOORS AT STOREFRONT_TYPICAL THRESHOLD
- **Drawing Complexity**: Moderate

### STOREFRONT DOOR HEAD_JAMB (STUCCO) DTL
- **File**: STOREFRONT DOOR HEAD_JAMB (STUCCO) DTL.rvt
- **Type**: Section (combined head and jamb)
- **Scale**: 3" = 1'-0"
- **Window System**: Storefront door
- **Wall System**: Stucco over frame
- **Condition**: Head and jamb at stucco wall
- **Components Used**: Stucco system (lath, scratch, brown, finish), J-mold/casing bead, door frame, sealant
- **Line Styles**: Wide Lines (framing), Medium Lines (door frame, stucco layers), Thin Lines (lath, sealant)
- **Key Elements**: Stucco termination at storefront frame, J-mold or casing bead, flexible sealant between stucco and aluminum
- **Waterproofing**: Weather barrier behind stucco, sealant at frame-to-stucco, weep screed
- **Building Science**: Dissimilar material junction requires flexible sealant (differential movement)
- **Related Details**: STOREFRONT DOOR HEAD_JAMB DTL
- **Drawing Complexity**: Complex

### STOREFRONT DETAIL @ HORIZONTAL MULLION
- **File**: STOREFRONT DETAIL @ HORIZONTAL MULLION.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Storefront
- **Wall System**: N/A (mullion-to-mullion)
- **Condition**: Horizontal mullion
- **Components Used**: Horizontal mullion extrusion, glazing gaskets (pressure plate or captured), setting blocks
- **Line Styles**: Wide Lines (mullion profile), Medium Lines (gaskets), Thin Lines (glass edge)
- **Key Elements**: Mullion extrusion profile, glazing pocket depth, pressure plate or snap cap
- **Waterproofing**: Glazing gaskets, weep at horizontal mullion
- **Building Science**: Horizontal mullions collect water - weep drainage essential
- **Related Details**: STORE FRONT_DETAIL @ HORIZONTAL MULLION, STORE FRONT_DETAIL @ VERTICAL MULLION
- **Drawing Complexity**: Moderate

### STORE FRONT_DETAIL @ HORIZONTAL MULLION
- **File**: STORE FRONT_DETAIL @ HORIZONTAL MULLION.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Storefront
- **Wall System**: N/A (mullion-to-mullion)
- **Condition**: Horizontal mullion (variant)
- **Components Used**: Horizontal mullion, glazing gaskets, setting blocks
- **Line Styles**: Wide Lines (mullion), Medium Lines (gaskets), Thin Lines (glass)
- **Key Elements**: May be different manufacturer or series than STOREFRONT DETAIL @ HORIZONTAL MULLION
- **Waterproofing**: Glazing gaskets, weep path
- **Building Science**: Horizontal mullion drainage
- **Related Details**: STOREFRONT DETAIL @ HORIZONTAL MULLION
- **Drawing Complexity**: Moderate

### STORE FRONT_DETAIL @ VERTICAL MULLION
- **File**: STORE FRONT_DETAIL @ VERTICAL MULLION.rvt
- **Type**: Section (plan cut)
- **Scale**: 3" = 1'-0"
- **Window System**: Storefront
- **Wall System**: N/A (mullion-to-mullion)
- **Condition**: Vertical mullion
- **Components Used**: Vertical mullion extrusion, glazing gaskets, snap cap or pressure plate
- **Line Styles**: Wide Lines (mullion profile), Medium Lines (gaskets), Thin Lines (glass)
- **Key Elements**: Vertical mullion structural member, glazing pocket, thermal break (if thermally broken system)
- **Waterproofing**: Gaskets seal glass to mullion
- **Building Science**: Vertical mullion carries wind load to head and sill
- **Related Details**: STORE FRONT_DETAIL @ HORIZONTAL MULLION
- **Drawing Complexity**: Moderate

### STORE FRONT_DETAIL @ VERTICAL TRANSOM MULLION
- **File**: STORE FRONT_DETAIL @ VERTICAL TRANSOM MULLION.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Storefront
- **Wall System**: N/A
- **Condition**: Vertical transom mullion (where transom meets vertical)
- **Components Used**: Transom connector, vertical mullion, T-connector hardware, gaskets
- **Line Styles**: Wide Lines (mullion profiles), Medium Lines (connector), Thin Lines (gaskets)
- **Key Elements**: T-intersection of horizontal transom and vertical mullion, hardware connector
- **Waterproofing**: Gaskets at intersection, weep from transom pocket
- **Building Science**: Structural load transfer at T-connection
- **Related Details**: STORE FRONT_DETAIL @ VERTICAL MULLION, STORE FRONT_DETAIL @ HORIZONTAL MULLION
- **Drawing Complexity**: Complex

### STORE FRONT_DETAIL @ VERTICAL
- **File**: STORE FRONT_DETAIL @ VERTICAL.rvt
- **Type**: Section (plan cut)
- **Scale**: 3" = 1'-0"
- **Window System**: Storefront
- **Wall System**: Varies
- **Condition**: Vertical mullion or jamb (generic)
- **Components Used**: Vertical frame member, glazing, gaskets
- **Line Styles**: Wide Lines (frame), Medium Lines (gaskets), Thin Lines (glass)
- **Key Elements**: Generic vertical storefront section
- **Waterproofing**: Gaskets
- **Building Science**: Standard storefront vertical member
- **Related Details**: STORE FRONT_DETAIL @ VERTICAL MULLION
- **Drawing Complexity**: Simple

### STORE FRONT_DETAIL @ CONCRETE SILL
- **File**: STORE FRONT_DETAIL @ CONCRETE SILL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Storefront
- **Wall System**: Concrete sill/curb
- **Condition**: Sill at concrete
- **Components Used**: Concrete curb, storefront sill receptor, sill flashing, anchor bolts, sealant
- **Line Styles**: Wide Lines (concrete), Medium Lines (storefront frame), Thin Lines (flashing, sealant)
- **Key Elements**: Receptor anchored to concrete curb, flashing under receptor, weep holes
- **Waterproofing**: Under-receptor flashing, weep holes, sealant at concrete-to-frame
- **Building Science**: Concrete curb provides positive drainage, prevents water ponding at sill
- **Related Details**: STOREFRONT SILL DETAIL
- **Drawing Complexity**: Moderate

### DOORS AT STOREFRONT_TYPICAL THRESHOLD
- **File**: DOORS AT STOREFRONT_TYPICAL THRESHOLD.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Storefront door
- **Wall System**: Concrete slab
- **Condition**: Threshold/sill
- **Components Used**: Threshold (mill finish or anodized aluminum), slab, pan flashing, sealant, floor finish transition
- **Line Styles**: Wide Lines (slab), Medium Lines (threshold), Thin Lines (sealant, finish)
- **Key Elements**: ADA-compliant threshold height (1/2" max), pan flashing under threshold, floor finish transition
- **Waterproofing**: Pan flashing, threshold drainage, sealant
- **Building Science**: ADA threshold height, water management
- **Related Details**: STOREFRONT DOOR HEAD_JAMB DTL
- **Drawing Complexity**: Moderate

### DOORS_STOREFRONT @ TERRACE
- **File**: DOORS_STOREFRONT @ TERRACE.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Storefront door
- **Wall System**: Concrete slab at terrace
- **Condition**: Sill/threshold at terrace transition
- **Components Used**: Concrete slab, threshold, terrace waterproofing, paver/topping, sealant
- **Line Styles**: Wide Lines (slab/topping), Medium Lines (threshold), Thin Lines (membrane, sealant)
- **Key Elements**: Interior-to-terrace level transition, threshold height, drainage away from door
- **Waterproofing**: Terrace membrane, threshold pan, slope
- **Building Science**: Critical junction - terrace water must not enter building
- **Related Details**: DOORS AT STOREFRONT_TYPICAL THRESHOLD
- **Drawing Complexity**: Complex

### TYP STOREFRONT DETAILS
- **File**: TYP STOREFRONT DETAILS.rvt
- **Type**: Section (composite sheet - multiple conditions)
- **Scale**: 1-1/2" = 1'-0" or 3" = 1'-0"
- **Window System**: Storefront (typical)
- **Wall System**: Varies
- **Condition**: Multiple (head, jamb, sill, mullion on one sheet)
- **Components Used**: Full storefront system components
- **Line Styles**: Standard detail line weights
- **Key Elements**: Complete storefront detail set on single sheet
- **Waterproofing**: All conditions shown
- **Building Science**: Comprehensive storefront assembly reference
- **Related Details**: All storefront details
- **Drawing Complexity**: Complex

### TYP STOREFRONT DETAILS @ EXTG WALL
- **File**: TYP STOREFRONT DETAILS @ EXTG WALL.rvt
- **Type**: Section (composite)
- **Scale**: 3" = 1'-0"
- **Window System**: Storefront
- **Wall System**: Existing wall (renovation condition)
- **Condition**: Multiple at existing wall
- **Components Used**: Existing wall (shown dashed or noted), new storefront system, sealant, anchors
- **Line Styles**: Wide Lines (existing wall), Medium Lines (new frame), Thin Lines (sealant), Dashed (existing)
- **Key Elements**: New storefront installed in existing wall opening, shim/anchor to existing structure
- **Waterproofing**: Sealant at new-to-existing junction
- **Building Science**: Existing wall integration, potential thermal bridging at anchors
- **Related Details**: TYP STOREFRONT DETAILS @ EXTG WALL - DOOR
- **Drawing Complexity**: Complex

### TYP STOREFRONT DETAILS @ EXTG WALL - DOOR
- **File**: TYP STOREFRONT DETAILS @ EXTG WALL - DOOR.rvt
- **Type**: Section (composite)
- **Scale**: 3" = 1'-0"
- **Window System**: Storefront door
- **Wall System**: Existing wall (renovation)
- **Condition**: Door at existing wall
- **Components Used**: Existing wall, new door frame, threshold, hardware blocking, sealant
- **Line Styles**: Wide/Dashed (existing), Medium (new frame), Thin (sealant)
- **Key Elements**: Door installation in existing opening, threshold at existing floor, ADA compliance
- **Waterproofing**: Sealant, threshold pan
- **Building Science**: Existing wall condition assessment needed
- **Related Details**: TYP STOREFRONT DETAILS @ EXTG WALL
- **Drawing Complexity**: Complex

### TYP STOREFRONT HEADER_JAMB & SILL DETAILS
- **File**: TYP STOREFRONT HEADER_JAMB & SILL DETAILS.rvt
- **Type**: Section (composite - all three conditions)
- **Scale**: 3" = 1'-0"
- **Window System**: Storefront
- **Wall System**: Varies
- **Condition**: Head, jamb, and sill (combined)
- **Components Used**: Complete storefront system
- **Line Styles**: Standard detail weights
- **Key Elements**: All three primary conditions on one detail sheet
- **Waterproofing**: All conditions addressed
- **Building Science**: Complete storefront water/air management
- **Related Details**: Individual storefront head/jamb/sill details
- **Drawing Complexity**: Complex

### INT. STOREFRONT HEADER DETAIL @ CONC. (Preview Available)
- **File**: No matching .rvt (preview only)
- **Preview**: `INT. STOREFRONT HEADER DETAIL @ CONC..png`
- **Type**: Section
- **Scale**: 3" = 1'-0" (based on dimension callouts visible)
- **Window System**: Interior storefront
- **Wall System**: Concrete structure above
- **Condition**: Head at concrete
- **Visual Analysis from Preview**: The drawing shows a concrete slab/beam above with the storefront head channel attached via clip angle. Clear dimension callouts show the storefront frame depth and deflection gap. Annotations on left side identify "CONC. FL. SLAB" and "GLAZED/UL ALUMINUM STOREFRONT SYSTEM." Simple detail with few layers - concrete above, aluminum frame below, clip attachment, and GWB soffit return on interior. No exterior weatherproofing shown (interior application).
- **Components Used**: Concrete slab, clip angle, storefront head channel, GWB soffit
- **Line Styles**: Wide Lines (concrete slab hatched), Medium Lines (storefront frame), Thin Lines (dimension lines, annotations)
- **Key Elements**: Deflection head detail, clip allows vertical movement, interior application means no waterproofing
- **Waterproofing**: None (interior storefront)
- **Building Science**: Deflection accommodation for structural slab movement, acoustic separation
- **Drawing Complexity**: Simple

### INT. STOREFRONT SILL DETAIL @ CONC. (Preview Available)
- **File**: No matching .rvt (preview only)
- **Preview**: `INT. STOREFRONT SILL DETAIL @ CONC..png`
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Interior storefront
- **Wall System**: Concrete floor slab
- **Condition**: Sill at concrete floor
- **Visual Analysis from Preview**: Shows concrete floor slab with storefront sill channel anchored on top. Annotations identify "CONC. FL. SLAB" on left, "GLAZED/UL ALUMINUM STOREFRONT" components. The sill frame sits directly on finished floor, anchored through slab. Interior finish (possibly carpet or tile) shown transitioning to storefront sill. Dimension lines indicate frame depth. GWB partition or return visible at side.
- **Components Used**: Concrete slab, storefront sill channel, anchor bolts, floor finish transition
- **Line Styles**: Wide Lines (concrete slab hatched), Medium Lines (storefront frame), Thin Lines (dimensions, leaders)
- **Key Elements**: Sill anchored to floor, floor finish transition, no drainage needed (interior)
- **Waterproofing**: None (interior storefront)
- **Building Science**: Sound transmission at sill, fire rating if required
- **Drawing Complexity**: Simple

---

## Window Wall (WW 7000 Series)

### WW 7000 HEAD @ DROPPED HEADER
- **File**: WW 7000 HEAD @ DROPPED HEADER.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Window wall (WW 7000 series - unitized or stick-built curtain wall)
- **Wall System**: Concrete dropped header/beam
- **Condition**: Head at dropped beam
- **Components Used**: Concrete beam, WW head receptor, deflection clip, sealant, interior finish
- **Line Styles**: Wide Lines (beam), Medium Lines (WW frame), Thin Lines (clip, sealant)
- **Key Elements**: Dropped header condition (beam below slab), deflection joint, WW head engagement
- **Waterproofing**: Sealant at head, internal drainage within WW system
- **Building Science**: Deflection gap critical for high-rise, thermal break in WW frame
- **Related Details**: WW 7000 JAMB, WW 7000 SILL variants, WW 7000 mullion details
- **Drawing Complexity**: Moderate

### WW 7000 JAMB
- **File**: WW 7000 JAMB.rvt
- **Type**: Section (plan cut)
- **Scale**: 3" = 1'-0"
- **Window System**: Window wall (WW 7000)
- **Wall System**: Concrete or transition to opaque wall
- **Condition**: Jamb
- **Components Used**: WW jamb extrusion, sealant, backer rod, interior GWB return, insulation
- **Line Styles**: Wide Lines (wall/structure), Medium Lines (WW frame), Thin Lines (sealant, insulation)
- **Key Elements**: WW-to-wall transition, thermal break, perimeter insulation at jamb
- **Waterproofing**: Perimeter sealant, internal WW drainage
- **Building Science**: Thermal bridge at WW-to-structure, air barrier continuity
- **Related Details**: WW 7000 HEAD @ DROPPED HEADER, WW 7000 SILL variants
- **Drawing Complexity**: Moderate

### WW 7000 DETAIL @ HORIZONTAL MULLION
- **File**: WW 7000 DETAIL @ HORIZONTAL MULLION.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Window wall (WW 7000)
- **Wall System**: N/A (mullion-to-mullion)
- **Condition**: Horizontal mullion
- **Components Used**: Horizontal mullion extrusion, pressure plate, snap cap, glazing gaskets, setting blocks
- **Line Styles**: Wide Lines (mullion), Medium Lines (pressure plate), Thin Lines (gaskets, glass edge)
- **Key Elements**: WW horizontal mullion profile, glazing pocket, pressure equalized drainage
- **Waterproofing**: Pressure equalized glazing pocket, internal weep drainage
- **Building Science**: PE (pressure equalized) rainscreen principle in glazing pocket
- **Related Details**: WW 7000 DETAIL @ VERTICAL MULLION
- **Drawing Complexity**: Moderate

### WW 7000 DETAIL @ VERTICAL MULLION
- **File**: WW 7000 DETAIL @ VERTICAL MULLION.rvt
- **Type**: Section (plan cut)
- **Scale**: 3" = 1'-0"
- **Window System**: Window wall (WW 7000)
- **Wall System**: N/A (mullion-to-mullion)
- **Condition**: Vertical mullion
- **Components Used**: Vertical mullion extrusion, pressure plate, snap cap, structural screw, gaskets
- **Line Styles**: Wide Lines (mullion), Medium Lines (pressure plate), Thin Lines (gaskets)
- **Key Elements**: Vertical structural mullion, wind load path, thermal break location
- **Waterproofing**: Gaskets, no weep at vertical (drains to horizontal)
- **Building Science**: Structural mullion for wind loads, thermal break
- **Related Details**: WW 7000 DETAIL @ HORIZONTAL MULLION
- **Drawing Complexity**: Moderate

### WW 7000 SILL
- **File**: WW 7000 SILL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Window wall (WW 7000)
- **Wall System**: Concrete slab
- **Condition**: Sill (typical)
- **Components Used**: Concrete slab edge, WW sill receptor, sill flashing, anchor, sealant
- **Line Styles**: Wide Lines (slab), Medium Lines (WW frame), Thin Lines (flashing, sealant)
- **Key Elements**: WW sill seated on slab, flashing under receptor, weep drainage
- **Waterproofing**: Sub-sill flashing, weep system
- **Building Science**: Sill water management, thermal break
- **Related Details**: WW 7000 SILL @ AMENITY TERRACE, WW 7000 SILL @ UNIT TERRACE
- **Drawing Complexity**: Moderate

### WW 7000 SILL @ AMENITY TERRACE
- **File**: WW 7000 SILL @ AMENITY TERRACE.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Window wall (WW 7000)
- **Wall System**: Concrete slab at amenity terrace
- **Condition**: Sill at amenity-level terrace
- **Components Used**: Concrete slab, WW sill, terrace waterproofing, paver on pedestal or topping slab, sealant
- **Line Styles**: Wide Lines (slab/topping), Medium Lines (WW frame), Thin Lines (membrane, sealant)
- **Key Elements**: Amenity deck transition (pool deck, common terrace), heavy-duty waterproofing, paver system
- **Waterproofing**: Terrace membrane tied to WW sill flashing, slope away from building
- **Building Science**: High traffic area, heavy waterproofing requirement
- **Related Details**: WW 7000 SILL, WW 7000 SILL @ UNIT TERRACE
- **Drawing Complexity**: Complex

### WW 7000 SILL @ UNIT TERRACE
- **File**: WW 7000 SILL @ UNIT TERRACE.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Window wall (WW 7000)
- **Wall System**: Concrete slab at unit terrace/balcony
- **Condition**: Sill at private unit terrace
- **Components Used**: Concrete slab, WW sill, balcony membrane, topping/tile, sealant
- **Line Styles**: Wide Lines (slab), Medium Lines (WW frame), Thin Lines (membrane, tile)
- **Key Elements**: Private terrace/balcony condition, typically smaller scale than amenity
- **Waterproofing**: Balcony membrane, sill pan, slope
- **Building Science**: Private balcony drainage, threshold transition
- **Related Details**: WW 7000 SILL, WW 7000 SILL @ AMENITY TERRACE
- **Drawing Complexity**: Complex

---

## CGI Impact Windows

### WINDOW_CGI_JAMB FIXED
- **File**: WINDOW_CGI_JAMB FIXED.rvt
- **Type**: Section (plan cut)
- **Scale**: 3" = 1'-0"
- **Window System**: CGI impact-rated fixed window
- **Wall System**: Concrete or CMU
- **Condition**: Jamb (fixed panel)
- **Components Used**: CGI frame extrusion, impact glass (laminated), sealant, anchor, interior return
- **Line Styles**: Wide Lines (wall), Medium Lines (CGI frame), Thin Lines (sealant, glass)
- **Key Elements**: Impact-rated frame profile (heavier than standard), laminated glass, structural attachment
- **Waterproofing**: Perimeter sealant, no weep needed for fixed
- **Building Science**: Impact resistance for HVHZ (Florida), laminated glass layup
- **Related Details**: WINDOW_CGI_JAMB FIXED @ MULL, WINDOW_CGI_JAMB_FIXEDSCREEN, WINDOW_CGI_SILL FIXED
- **Drawing Complexity**: Moderate

### WINDOW_CGI_JAMB FIXED @ MULL
- **File**: WINDOW_CGI_JAMB FIXED @ MULL.rvt
- **Type**: Section (plan cut)
- **Scale**: 3" = 1'-0"
- **Window System**: CGI impact-rated fixed window
- **Wall System**: N/A (mullion connection)
- **Condition**: Jamb at mullion (fixed-to-fixed connection)
- **Components Used**: CGI mullion connector, two fixed frames, structural mullion, gaskets
- **Line Styles**: Wide Lines (mullion), Medium Lines (frames), Thin Lines (gaskets)
- **Key Elements**: Mullion connection between adjacent CGI fixed panels, structural capacity for impact loads
- **Waterproofing**: Mullion gaskets
- **Building Science**: Mullion must resist missile impact and cyclic pressure
- **Related Details**: WINDOW_CGI_JAMB FIXED
- **Drawing Complexity**: Moderate

### WINDOW_CGI_JAMB_FIXEDSCREEN
- **File**: WINDOW_CGI_JAMB_FIXEDSCREEN.rvt
- **Type**: Section (plan cut)
- **Scale**: 3" = 1'-0"
- **Window System**: CGI impact-rated fixed window with screen
- **Wall System**: Concrete or CMU
- **Condition**: Jamb with integrated screen track
- **Components Used**: CGI frame, screen track/channel, screen mesh, sealant
- **Line Styles**: Wide Lines (wall), Medium Lines (frame), Thin Lines (screen track, mesh)
- **Key Elements**: Added screen track on interior or exterior face of fixed window frame
- **Waterproofing**: Same as fixed jamb, screen does not affect water management
- **Building Science**: Screen does not contribute to impact resistance
- **Related Details**: WINDOW_CGI_JAMB FIXED
- **Drawing Complexity**: Moderate

### WINDOW_CGI_SILL FIXED
- **File**: WINDOW_CGI_SILL FIXED.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: CGI impact-rated fixed window
- **Wall System**: Concrete or CMU
- **Condition**: Sill (fixed panel)
- **Components Used**: CGI sill frame, sill pan flashing, sealant, interior finish
- **Line Styles**: Wide Lines (wall below), Medium Lines (CGI frame), Thin Lines (flashing, sealant)
- **Key Elements**: Impact-rated sill frame, pan flashing, no track (fixed unit)
- **Waterproofing**: Pan flashing with back dam and end dams, perimeter sealant
- **Building Science**: Impact sill must maintain water resistance under cyclic pressure
- **Related Details**: WINDOW_CGI_JAMB FIXED, WINDOW_CGI_JAMB FIXED @ MULL
- **Drawing Complexity**: Moderate

---

## Specialty Glazing (Color Glass / No Glass Frames)

### COLOR GLASS FRAME DETAIL - JAMB
- **File**: COLOR GLASS FRAME DETAIL - JAMB.rvt
- **Type**: Section (plan cut)
- **Scale**: 3" = 1'-0"
- **Window System**: Colored/spandrel glass panel (non-vision)
- **Wall System**: Concrete or metal stud backup
- **Condition**: Jamb
- **Components Used**: Frame channel, colored/opaque glass (spandrel), insulation behind glass, sealant
- **Line Styles**: Wide Lines (wall), Medium Lines (frame), Thin Lines (insulation, sealant)
- **Key Elements**: Spandrel glass framing (hides structure behind), insulation behind opaque glass to prevent heat gain
- **Waterproofing**: Sealant at frame perimeter
- **Building Science**: Insulation behind spandrel glass critical to prevent heat buildup and condensation
- **Related Details**: COLOR GLASS FRAME DETAIL - SILL, NO GLASS FRAME DETAIL - JAMB
- **Drawing Complexity**: Moderate

### COLOR GLASS FRAME DETAIL - SILL
- **File**: COLOR GLASS FRAME DETAIL - SILL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Colored/spandrel glass panel
- **Wall System**: Concrete slab
- **Condition**: Sill
- **Components Used**: Sill frame, spandrel glass, insulation, sealant, anchor
- **Line Styles**: Wide Lines (slab), Medium Lines (frame), Thin Lines (insulation, sealant)
- **Key Elements**: Sill support for spandrel panel, insulation behind
- **Waterproofing**: Sill flashing, sealant
- **Building Science**: Shadow box effect - airspace + insulation behind spandrel
- **Related Details**: COLOR GLASS FRAME DETAIL - JAMB
- **Drawing Complexity**: Moderate

### NO GLASS FRAME DETAIL - JAMB
- **File**: NO GLASS FRAME DETAIL - JAMB.rvt
- **Type**: Section (plan cut)
- **Scale**: 3" = 1'-0"
- **Window System**: Blank/infill panel in glazing frame (no glass)
- **Wall System**: Concrete or stud backup
- **Condition**: Jamb
- **Components Used**: Frame channel, solid infill panel (metal, FRP, or composite), insulation, sealant
- **Line Styles**: Wide Lines (wall), Medium Lines (frame), Thin Lines (panel, sealant)
- **Key Elements**: Opaque infill panel in aluminum frame (matches glazing system appearance), insulation
- **Waterproofing**: Sealant at frame, panel gaskets
- **Building Science**: Higher thermal performance than glazed panels, conceals mechanical/structural
- **Related Details**: NO GLASS FRAME DETAIL - SILL
- **Drawing Complexity**: Simple

### NO GLASS FRAME DETAIL - SILL
- **File**: NO GLASS FRAME DETAIL - SILL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Blank/infill panel in glazing frame
- **Wall System**: Concrete slab
- **Condition**: Sill
- **Components Used**: Sill frame, infill panel, insulation, sealant
- **Line Styles**: Wide Lines (slab), Medium Lines (frame), Thin Lines (panel, sealant)
- **Key Elements**: Opaque panel sill condition
- **Waterproofing**: Sill flashing, sealant
- **Building Science**: Vapor barrier location behind panel
- **Related Details**: NO GLASS FRAME DETAIL - JAMB
- **Drawing Complexity**: Simple

---

## Skylights

### PYRAMID SKYLIGHT
- **File**: PYRAMID SKYLIGHT.rvt
- **Type**: Section / elevation (likely 3D or isometric elements)
- **Scale**: 1-1/2" = 1'-0" or 3/4" = 1'-0"
- **Window System**: Pyramid skylight (self-flashing or curb-mounted)
- **Wall System**: Roof structure
- **Condition**: Full assembly
- **Components Used**: Skylight frame, glazing, curb, roof membrane termination, flashing, condensation gutter
- **Line Styles**: Wide Lines (roof structure), Medium Lines (skylight frame), Thin Lines (flashing, membrane)
- **Key Elements**: Pyramid form (4 sloped glazing panels meeting at apex), curb, base flashing, counter-flashing
- **Waterproofing**: Curb flashing, membrane termination at curb, condensation gutter, weep to exterior
- **Building Science**: Condensation management (skylight interior gets cold), thermal break in frame
- **Related Details**: SLOPED SKYLIGHT, SKYLIGHT SILL
- **Drawing Complexity**: Complex

### SLOPED SKYLIGHT
- **File**: SLOPED SKYLIGHT.rvt
- **Type**: Section
- **Scale**: 1-1/2" = 1'-0" or 3/4" = 1'-0"
- **Window System**: Sloped/ridge skylight
- **Wall System**: Roof structure (sloped)
- **Condition**: Full section through slope
- **Components Used**: Roof framing, skylight curb, glazing, flashing, condensation gutter, interior frame
- **Line Styles**: Wide Lines (roof structure), Medium Lines (frame), Thin Lines (flashing, membrane)
- **Key Elements**: Sloped glazing installation in roof plane, curb height (min 4" above roof), head/sill at slope
- **Waterproofing**: Upper (head) and lower (sill) curb flashing, step flashing at sides, ice & water shield
- **Building Science**: Solar heat gain through horizontal glazing much higher than vertical, condensation risk
- **Related Details**: PYRAMID SKYLIGHT, SKYLIGHT SILL
- **Drawing Complexity**: Complex

### SKYLIGHT SILL
- **File**: SKYLIGHT SILL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Skylight
- **Wall System**: Roof curb
- **Condition**: Sill (lower curb)
- **Components Used**: Curb framing, sill flashing, roof membrane, skylight frame, condensation gutter
- **Line Styles**: Wide Lines (curb/roof), Medium Lines (frame), Thin Lines (flashing, membrane)
- **Key Elements**: Lower curb of skylight where water drains off glazing, flashing integration with roof
- **Waterproofing**: Sill flashing extends over roof membrane, no reverse lap
- **Building Science**: Most vulnerable skylight location - all water from glazing drains here
- **Related Details**: PYRAMID SKYLIGHT, SLOPED SKYLIGHT
- **Drawing Complexity**: Moderate

---

## Elevator Doors / Openings

### ELEVATOR HEAD AND SILL DETAIL
- **File**: ELEVATOR HEAD AND SILL DETAIL.rvt
- **Type**: Section (combined head and sill)
- **Scale**: 3" = 1'-0"
- **Window System**: Elevator door opening (not a window - categorized here per BD filing)
- **Wall System**: Concrete or CMU elevator shaft
- **Condition**: Head and sill combined
- **Components Used**: Elevator frame (hollow metal), sill angle, shaft wall, fire-rated assembly
- **Line Styles**: Wide Lines (shaft wall), Medium Lines (frame), Thin Lines (sealant, fire caulk)
- **Key Elements**: Elevator entrance frame, sill plate, fire-rated shaft wall, smoke seal
- **Waterproofing**: N/A (interior)
- **Building Science**: Fire rating (2-hour shaft), smoke seal, ADA threshold
- **Related Details**: ELEVATOR JAMB DETAIL, ELEVATOR SILL DETAIL variants
- **Drawing Complexity**: Moderate

### ELEVATOR JAMB DETAIL
- **File**: ELEVATOR JAMB DETAIL.rvt
- **Type**: Section (plan cut)
- **Scale**: 3" = 1'-0"
- **Window System**: Elevator door opening
- **Wall System**: Concrete or CMU shaft
- **Condition**: Jamb
- **Components Used**: Elevator entrance frame (hollow metal), shaft wall, fire caulk, GWB on corridor side
- **Line Styles**: Wide Lines (shaft wall), Medium Lines (frame), Thin Lines (fire caulk)
- **Key Elements**: Frame-to-shaft wall attachment, fire caulk at perimeter, corridor finish
- **Waterproofing**: N/A (interior)
- **Building Science**: Fire rating continuity, smoke seal
- **Related Details**: ELEVATOR HEAD AND SILL DETAIL
- **Drawing Complexity**: Simple

### ELEVATOR SILL DETAIL @ GROUND FLOOR
- **File**: ELEVATOR SILL DETAIL @ GROUND FLOOR.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Elevator door opening
- **Wall System**: Concrete shaft at ground floor
- **Condition**: Sill at ground floor (different from upper floors - may have pit waterproofing considerations)
- **Components Used**: Sill angle, concrete slab, elevator pit wall transition, floor finish
- **Line Styles**: Wide Lines (slab/wall), Medium Lines (sill angle), Thin Lines (finish)
- **Key Elements**: Ground floor sill may differ from upper floors due to pit proximity
- **Waterproofing**: Pit waterproofing if applicable
- **Building Science**: Fire rating, ADA sill
- **Related Details**: ELEVATOR SILL DETAIL AT GROUND FLOOR - CONCRETE WALL
- **Drawing Complexity**: Moderate

### ELEVATOR SILL DETAIL AT GROUND FLOOR - CONCRETE WALL
- **File**: ELEVATOR SILL DETAIL AT GROUND FLOOR - CONCRETE WALL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Elevator door opening
- **Wall System**: Concrete wall (cast-in-place shaft)
- **Condition**: Sill at concrete wall ground floor
- **Components Used**: Concrete wall, sill angle, floor slab, elevator pit wall, fire caulk
- **Line Styles**: Wide Lines (concrete), Medium Lines (sill angle), Thin Lines (fire caulk)
- **Key Elements**: Concrete shaft wall condition at ground (vs. CMU at upper floors in some buildings)
- **Waterproofing**: Pit waterproofing
- **Building Science**: Fire rating, structural sill support
- **Related Details**: ELEVATOR SILL DETAIL @ GROUND FLOOR
- **Drawing Complexity**: Moderate

### ELEVATOR_DOOR SILL
- **File**: ELEVATOR_DOOR SILL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Elevator door opening
- **Wall System**: Varies
- **Condition**: Sill (typical upper floor)
- **Components Used**: Sill angle, slab, fire caulk, floor finish
- **Line Styles**: Wide Lines (slab), Medium Lines (sill angle), Thin Lines (finish, fire caulk)
- **Key Elements**: Standard elevator sill at typical floor
- **Waterproofing**: N/A (interior)
- **Building Science**: Fire rating, ADA threshold
- **Related Details**: ELEVATOR HEAD AND SILL DETAIL
- **Drawing Complexity**: Simple

---

## Louver Openings

### LOUVER JAMB DETAIL
- **File**: LOUVER JAMB DETAIL.rvt
- **Type**: Section (plan cut)
- **Scale**: 3" = 1'-0"
- **Window System**: Louver (ventilation opening)
- **Wall System**: Concrete or CMU
- **Condition**: Jamb
- **Components Used**: Louver frame, louver blades, bird screen, sealant, interior finish
- **Line Styles**: Wide Lines (wall), Medium Lines (frame), Thin Lines (blades, screen)
- **Key Elements**: Louver frame in wall opening, blade angle for rain rejection, bird/insect screen
- **Waterproofing**: Drainable louver blades, rain rejection angle, sealant at frame perimeter
- **Building Science**: Free area calculation for airflow, rain penetration resistance
- **Related Details**: LOUVER SILL DETAIL, LOUVER_SILL
- **Drawing Complexity**: Moderate

### LOUVER SILL DETAIL
- **File**: LOUVER SILL DETAIL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Louver
- **Wall System**: Concrete or CMU
- **Condition**: Sill
- **Components Used**: Louver sill frame, drip edge, sill flashing, bird screen, sealant
- **Line Styles**: Wide Lines (wall), Medium Lines (frame), Thin Lines (flashing, screen)
- **Key Elements**: Sill drainage from louver, drip edge to exterior, collection trough
- **Waterproofing**: Sill flashing, drip edge, internal drainage trough
- **Building Science**: Water that passes louver blades must drain at sill, not enter building
- **Related Details**: LOUVER JAMB DETAIL
- **Drawing Complexity**: Moderate

### LOUVER_SILL
- **File**: LOUVER_SILL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Louver
- **Wall System**: Varies
- **Condition**: Sill (variant)
- **Components Used**: Louver sill, flashing, drainage
- **Line Styles**: Standard detail weights
- **Key Elements**: May be different louver manufacturer or wall condition than LOUVER SILL DETAIL
- **Waterproofing**: Sill drainage
- **Building Science**: Rain penetration at sill
- **Related Details**: LOUVER SILL DETAIL, LOUVER JAMB DETAIL
- **Drawing Complexity**: Simple

---

## Specialty Doors (Bi-Fold, Cascade, Nana)

### BI-FOLD DOORS SILL
- **File**: BI-FOLD DOORS SILL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Bi-fold door system
- **Wall System**: Concrete slab or framed
- **Condition**: Sill/threshold
- **Components Used**: Bi-fold track (recessed or surface), threshold, sill pan, pivot hardware pocket, sealant
- **Line Styles**: Wide Lines (slab), Medium Lines (track/threshold), Thin Lines (sealant, hardware)
- **Key Elements**: Bi-fold track allows panels to fold and stack, track is recessed flush with floor for ADA, pivot point hardware
- **Waterproofing**: Pan flashing under track, track drainage, perimeter sealant
- **Building Science**: Track drainage critical - water enters track when panels are opened
- **Related Details**: NANA DOOR SILL, CASCADE DOOR & SIDE LITE SILL
- **Drawing Complexity**: Complex

### CASCADE DOOR & SIDE LITE SILL
- **File**: CASCADE DOOR & SIDE LITE SILL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Cascade (multi-slide) door with fixed side lite
- **Wall System**: Concrete slab
- **Condition**: Sill/threshold
- **Components Used**: Multi-track sill (3+ tracks), side lite sill frame, threshold, pan flashing, sealant
- **Line Styles**: Wide Lines (slab), Medium Lines (multi-track), Thin Lines (flashing, sealant)
- **Key Elements**: Multiple parallel tracks for cascading panels, fixed side lite adjacent, track interlock
- **Waterproofing**: Multi-track drainage system, pan flashing, slope to exterior
- **Building Science**: Complex drainage with multiple tracks, each track must drain independently
- **Related Details**: BI-FOLD DOORS SILL, NANA DOOR SILL
- **Drawing Complexity**: Complex

### NANA DOOR SILL
- **File**: NANA DOOR SILL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: NanaWall (folding glass wall system)
- **Wall System**: Concrete slab
- **Condition**: Sill/threshold
- **Components Used**: NanaWall sill track, flush threshold option, drainage channel, pan flashing, sealant
- **Line Styles**: Wide Lines (slab), Medium Lines (track/threshold), Thin Lines (flashing, drainage)
- **Key Elements**: NanaWall proprietary track system, flush or ramped threshold options, full-width opening capability
- **Waterproofing**: Integrated drainage channel, pan flashing, sealant at perimeter
- **Building Science**: Full-width opening creates significant air/water management challenge when closed
- **Related Details**: BI-FOLD DOORS SILL, CASCADE DOOR & SIDE LITE SILL
- **Drawing Complexity**: Complex

---

## General / Multi-Condition Window Details

### WINDOW HEAD DETAIL
- **File**: WINDOW HEAD DETAIL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Generic window
- **Wall System**: Varies
- **Condition**: Head
- **Components Used**: Header/lintel, window head frame, flashing, sealant, interior trim
- **Line Styles**: Wide Lines (structure), Medium Lines (frame), Thin Lines (flashing, sealant)
- **Key Elements**: Generic window head condition, applicable to multiple window types
- **Waterproofing**: Head flashing, sealant
- **Building Science**: Lintel support, thermal break
- **Related Details**: WINDOW JAMB DETAIL, WINDOW SILL DTL
- **Drawing Complexity**: Simple

### WINDOW HEAD DTL (AT FLAT SLAB EDGE) 1
- **File**: WINDOW HEAD DTL (AT FLAT SLAB EDGE) 1.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Generic window
- **Wall System**: Concrete flat slab (slab edge condition)
- **Condition**: Head at slab edge
- **Components Used**: Concrete slab edge, window head frame, sealant, insulation, exterior finish
- **Line Styles**: Wide Lines (slab), Medium Lines (frame), Thin Lines (sealant, insulation)
- **Key Elements**: Window head at exposed slab edge (high-rise concrete construction), slab edge insulation
- **Waterproofing**: Sealant at frame-to-slab
- **Building Science**: Slab edge thermal bridge, insulation continuity
- **Related Details**: WINDOW HEAD DTL (AT FLAT SLAB EDGE) 3, WINDOW HEAD DTL (AT FLAT SLAB)
- **Drawing Complexity**: Moderate

### WINDOW HEAD DTL (AT FLAT SLAB EDGE) 3
- **File**: WINDOW HEAD DTL (AT FLAT SLAB EDGE) 3.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Generic window
- **Wall System**: Concrete flat slab edge (variant 3)
- **Condition**: Head at slab edge
- **Components Used**: Similar to variant 1 with different exterior finish or insulation approach
- **Line Styles**: Standard detail weights
- **Key Elements**: Alternate slab edge treatment (possibly different exterior cladding)
- **Waterproofing**: Sealant, possible drip edge
- **Building Science**: Thermal bridge mitigation alternate
- **Related Details**: WINDOW HEAD DTL (AT FLAT SLAB EDGE) 1
- **Drawing Complexity**: Moderate

### WINDOW HEAD DTL (AT FLAT SLAB)
- **File**: WINDOW HEAD DTL (AT FLAT SLAB).rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Generic window
- **Wall System**: Concrete flat slab (bottom of slab, not edge)
- **Condition**: Head at underside of slab
- **Components Used**: Concrete slab, window head frame, sealant, GWB soffit
- **Line Styles**: Wide Lines (slab), Medium Lines (frame), Thin Lines (sealant, GWB)
- **Key Elements**: Window head directly at slab soffit, no separate lintel
- **Waterproofing**: Sealant at frame-to-slab
- **Building Science**: Thermal bridge at slab, no deflection concern (rigid slab)
- **Related Details**: WINDOW HEAD DTL (AT FLAT SLAB EDGE) variants
- **Drawing Complexity**: Simple

### WINDOW JAMB DETAIL
- **File**: WINDOW JAMB DETAIL.rvt
- **Type**: Section (plan cut)
- **Scale**: 3" = 1'-0"
- **Window System**: Generic window
- **Wall System**: Varies
- **Condition**: Jamb
- **Components Used**: Jamb frame, sealant, backer rod, shims, interior trim/return
- **Line Styles**: Wide Lines (wall), Medium Lines (frame), Thin Lines (sealant, shims)
- **Key Elements**: Generic jamb condition, rough opening to frame relationship
- **Waterproofing**: Sealant and backer rod at exterior, interior air seal
- **Building Science**: Air barrier continuity at jamb
- **Related Details**: WINDOW HEAD DETAIL, WINDOW SILL DTL
- **Drawing Complexity**: Simple

### WINDOW SILL DTL
- **File**: WINDOW SILL DTL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Generic window
- **Wall System**: Stucco or frame
- **Condition**: Sill
- **Components Used**: Sill frame, pan flashing, weep, interior stool/return, sealant
- **Line Styles**: Wide Lines (wall below), Medium Lines (frame), Thin Lines (flashing, sealant)
- **Key Elements**: Generic sill with pan flashing, slope to exterior
- **Waterproofing**: Pan flashing, weep holes, sealant
- **Building Science**: Sill is #1 leak point - proper slope and flashing critical
- **Related Details**: WINDOW HEAD DETAIL, WINDOW JAMB DETAIL
- **Drawing Complexity**: Moderate

### WINDOW_DETAIL @ CONC HEADER
- **File**: WINDOW_DETAIL @ CONC HEADER.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Generic window
- **Wall System**: Concrete header/beam
- **Condition**: Head at concrete header
- **Components Used**: Concrete beam, window head frame, sealant, shim, GWB return
- **Line Styles**: Wide Lines (concrete), Medium Lines (frame), Thin Lines (sealant, shim)
- **Key Elements**: Window head bearing on concrete beam/header
- **Waterproofing**: Sealant at frame-to-concrete
- **Building Science**: Thermal bridge at concrete header
- **Related Details**: HORIZONTAL_ROLLING_WINDOW_DETAIL @ CONC HEADER
- **Drawing Complexity**: Simple

### WINDOW_DETAIL @ CONCRETE SILL
- **File**: WINDOW_DETAIL @ CONCRETE SILL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Generic window
- **Wall System**: Concrete sill/wall
- **Condition**: Sill at concrete
- **Components Used**: Concrete wall/sill, window sill frame, sealant, pan flashing, interior finish
- **Line Styles**: Wide Lines (concrete), Medium Lines (frame), Thin Lines (flashing, sealant)
- **Key Elements**: Window sill seated on concrete, slope built into concrete or shimmed
- **Waterproofing**: Pan flashing, sealant at frame-to-concrete
- **Building Science**: Concrete provides positive slope if properly formed
- **Related Details**: WINDOW_DETAIL @ CONC HEADER
- **Drawing Complexity**: Moderate

### WINDOW_DETAIL @ INTERMEDIATE MULLION
- **File**: WINDOW_DETAIL @ INTERMEDIATE MULLION.rvt
- **Type**: Section (plan cut or vertical section)
- **Scale**: 3" = 1'-0"
- **Window System**: Generic window system
- **Wall System**: N/A (mullion-to-mullion)
- **Condition**: Intermediate mullion (between window units)
- **Components Used**: Mullion extrusion, glazing gaskets, structural fasteners
- **Line Styles**: Wide Lines (mullion profile), Medium Lines (gaskets), Thin Lines (glass edges)
- **Key Elements**: Mullion connecting adjacent window units, structural and water management
- **Waterproofing**: Gaskets, internal drainage
- **Building Science**: Thermal break at mullion, structural wind load transfer
- **Related Details**: All mullion details
- **Drawing Complexity**: Moderate

### EXT. SLIDER WINDOW SILL DETAIL (Preview Available)
- **File**: No matching .rvt (preview only)
- **Preview**: `EXT. SLIDER WINDOW SILL DETAIL.png`
- **Type**: Section
- **Scale**: 3" = 1'-0" (based on annotation density)
- **Window System**: Exterior sliding window
- **Wall System**: Concrete or stucco over frame
- **Condition**: Sill
- **Visual Analysis from Preview**: Detailed section drawing showing a sliding window sill condition. Multiple annotation leaders point to specific components from the left side. The drawing shows the wall section below the sill with hatched materials (likely concrete or masonry), the sill track assembly with multiple horizontal lines indicating the track profiles, flashing below the track, and interior finish return. Dense annotation with text callouts identifying each material layer. The detail has significant depth showing wall construction below sill line. Break lines at top and bottom indicate this is a portion of a larger wall section.
- **Components Used**: Sill track (multi-rail), pan flashing, wall construction below, sealant, interior finish
- **Line Styles**: Wide Lines (wall construction hatched), Medium Lines (track profiles), Thin Lines (flashing, sealant, annotations)
- **Key Elements**: Multi-rail sliding track at sill, drainage path through track, pan flashing integration with wall below
- **Waterproofing**: Pan flashing, track drainage, weep holes
- **Building Science**: Track water management, slope to exterior
- **Drawing Complexity**: Complex

### EXTERIOR WINDOW SILL - TRULITE (Preview Available)
- **File**: No matching .rvt (preview only)
- **Preview**: `EXTERIOR WINDOW SILL - TRULITE.png`
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Trulite window system (commercial aluminum)
- **Wall System**: Concrete or CMU with exterior finish
- **Condition**: Sill
- **Visual Analysis from Preview**: Multi-layered section detail. Shows what appears to be two related conditions stacked vertically (possibly sill detail above and a wall section below, or two different sill conditions). Upper portion shows window sill frame with multiple component callouts. Hatched areas represent the wall construction (concrete or masonry). Clear annotations on both left and right sides identifying components. The lower portion shows additional wall/sill construction with break lines. More complex than the slider sill detail with additional layers of waterproofing or finish visible.
- **Components Used**: Trulite aluminum sill frame, sealant, flashing, wall construction, interior finish
- **Line Styles**: Wide Lines (wall construction hatched), Medium Lines (aluminum frame profiles), Thin Lines (sealant, flashing, annotations)
- **Key Elements**: Trulite proprietary frame profile, sill drainage, exterior finish transition
- **Waterproofing**: Sub-sill flashing, sealant, weep system
- **Building Science**: Commercial-grade sill water management
- **Drawing Complexity**: Complex

---

## Miscellaneous / Accessory Details

### CONCEALED SOLAR SHADE AT WINDOW HEADER
- **File**: CONCEALED SOLAR SHADE AT WINDOW HEADER.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Any window (accessory detail)
- **Wall System**: Concrete slab or framed soffit
- **Condition**: Head (with concealed shade pocket)
- **Components Used**: Shade pocket/housing, roller shade mechanism, window head frame, GWB soffit, blocking
- **Line Styles**: Wide Lines (structure), Medium Lines (shade housing), Thin Lines (shade fabric, blocking)
- **Key Elements**: Recessed pocket in soffit/ceiling for motorized shade, blocking for shade bracket, electrical provision
- **Waterproofing**: N/A (interior accessory)
- **Building Science**: Solar heat gain control, glare management
- **Related Details**: Window head details (any type)
- **Drawing Complexity**: Complex

### DETAIL ELEVATION & SECTION @ WINDOW CORNICE
- **File**: DETAIL ELEVATION & SECTION @ WINDOW CORNICE.rvt
- **Type**: Elevation and section (combined)
- **Scale**: 1-1/2" = 1'-0" or 3" = 1'-0"
- **Window System**: Any window (cornice trim accessory)
- **Wall System**: Stucco, precast, or EIFS
- **Condition**: Exterior cornice/trim above window
- **Components Used**: Cornice profile (precast, EIFS, or stucco), flashing above cornice, blocking, anchors
- **Line Styles**: Wide Lines (wall), Medium Lines (cornice profile), Thin Lines (flashing, anchors)
- **Key Elements**: Decorative cornice above window, flashing to prevent water behind cornice, anchorage
- **Waterproofing**: Flashing above cornice, weep behind cornice, sealant at cornice-to-wall
- **Building Science**: Cornice creates water trap if not properly flashed
- **Related Details**: Window head details
- **Drawing Complexity**: Complex

### GYPSUM BOARD HEADER DETAIL
- **File**: GYPSUM BOARD HEADER DETAIL.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Any (interior header condition)
- **Wall System**: Metal stud with GWB
- **Condition**: Head (interior partition header above opening)
- **Components Used**: Metal stud track, GWB layers, deflection track, sealant, corner bead
- **Line Styles**: Wide Lines (GWB layers), Medium Lines (metal stud), Thin Lines (sealant, tape)
- **Key Elements**: Non-structural GWB header above interior opening, deflection track at structure
- **Waterproofing**: N/A (interior)
- **Building Science**: Fire rating if rated partition, deflection at head
- **Related Details**: WALLS_FRAMING_GWB TO MULLION
- **Drawing Complexity**: Simple

### WALLS_FRAMING_GWB TO MULLION
- **File**: WALLS_FRAMING_GWB TO MULLION.rvt
- **Type**: Section (plan cut)
- **Scale**: 3" = 1'-0"
- **Window System**: Storefront or curtain wall
- **Wall System**: Metal stud with GWB
- **Condition**: Partition termination at glazing mullion
- **Components Used**: Metal stud track, GWB layers, deflection channel, sealant, mullion adapter
- **Line Styles**: Wide Lines (GWB), Medium Lines (stud/track), Thin Lines (sealant, adapter)
- **Key Elements**: GWB partition meeting aluminum mullion, transition from opaque to glazed, fire/smoke seal if rated
- **Waterproofing**: N/A (interior junction)
- **Building Science**: Acoustic separation at partition-to-mullion, fire rating continuity
- **Related Details**: GYPSUM BOARD HEADER DETAIL, storefront details
- **Drawing Complexity**: Moderate

### TERMINATION @ GLAZING
- **File**: TERMINATION @ GLAZING.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Curtain wall or storefront
- **Wall System**: Opaque wall transitioning to glazing
- **Condition**: Wall-to-glazing termination
- **Components Used**: Wall assembly, termination trim, sealant, glazing frame receptor, weather barrier termination
- **Line Styles**: Wide Lines (wall), Medium Lines (trim/frame), Thin Lines (sealant, barrier)
- **Key Elements**: Transition from opaque wall to glazing system, weather barrier tie-in, sealant joint
- **Waterproofing**: Weather barrier lap to glazing frame, sealant, flashing at transition
- **Building Science**: Critical junction - two different wall systems meeting, air/water barrier must be continuous
- **Related Details**: All storefront and window wall details
- **Drawing Complexity**: Moderate

### DOOR SILL @ UNIT D 1
- **File**: DOOR SILL @ UNIT D 1.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Unit door (residential unit entry or balcony door)
- **Wall System**: Concrete slab
- **Condition**: Sill/threshold
- **Components Used**: Threshold, slab, waterproofing (if exterior), floor finish transition, sealant
- **Line Styles**: Wide Lines (slab), Medium Lines (threshold), Thin Lines (finish, sealant)
- **Key Elements**: Unit D specific door sill condition (variant 1), may reference specific floor plan location
- **Waterproofing**: Threshold pan if exterior, sealant
- **Building Science**: ADA threshold, water management if exterior
- **Related Details**: DOOR SILL @ UNIT D 2
- **Drawing Complexity**: Moderate

### DOOR SILL @ UNIT D 2
- **File**: DOOR SILL @ UNIT D 2.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Unit door (variant 2)
- **Wall System**: Concrete slab
- **Condition**: Sill/threshold (alternate condition)
- **Components Used**: Threshold, slab, waterproofing, floor finish, sealant
- **Line Styles**: Standard detail weights
- **Key Elements**: Unit D door sill variant 2 (possibly different floor finish or threshold type)
- **Waterproofing**: Threshold pan if exterior
- **Building Science**: ADA threshold
- **Related Details**: DOOR SILL @ UNIT D 1
- **Drawing Complexity**: Moderate

### ROOF ACCESS DOOR SILL @ UTILITY ROOMS
- **File**: ROOF ACCESS DOOR SILL @ UTILITY ROOMS.rvt
- **Type**: Section
- **Scale**: 3" = 1'-0"
- **Window System**: Roof access door
- **Wall System**: CMU or concrete (utility/mechanical room)
- **Condition**: Sill/threshold at roof access
- **Components Used**: Raised threshold/curb, waterproofing membrane, door frame, sealant, floor slope to drain
- **Line Styles**: Wide Lines (wall/curb), Medium Lines (frame/threshold), Thin Lines (membrane, sealant)
- **Key Elements**: Raised curb/threshold for waterproofing (prevents roof water from entering building), fire-rated assembly
- **Waterproofing**: Raised curb with membrane, threshold above roof level, sealant
- **Building Science**: Roof-to-interior transition, fire rating, wind uplift at door
- **Related Details**: None specific
- **Drawing Complexity**: Moderate

---

## Grouped Summary

### By Window Type

| Window Type | Files | Key Conditions |
|-------------|-------|----------------|
| **Single Hung** | 4 | Head (B.O. slab, header), sill, full section |
| **Casement** | 2 | Head (B.O. slab), sill (concrete) |
| **Sliding/Horizontal Rolling** | 6 | Head (2), sill (2), jamb (2) |
| **Fixed** | 2 | Panel section, sill |
| **SGD 2020** | 5 | Head, jamb, sill (balcony/slab edge/terrace) |
| **SGD 2400ST** | 3 | Jamb, sill (balcony/terrace) |
| **SGD Generic** | 2 | Full section, jamb at mullion |
| **Storefront** | 16 | Head, sill, jamb, mullions (H/V/transom), doors, thresholds, existing wall |
| **Window Wall 7000** | 7 | Head, jamb, mullions (H/V), sill (standard/amenity/unit terrace) |
| **CGI Impact** | 4 | Jamb (fixed, at mullion, with screen), sill (fixed) |
| **Spandrel/Infill** | 4 | Color glass jamb/sill, no-glass jamb/sill |
| **Skylight** | 3 | Pyramid, sloped, sill |
| **Louver** | 3 | Jamb, sill (2 variants) |
| **Specialty Doors** | 3 | Bi-fold sill, cascade sill, NanaWall sill |
| **Elevator** | 5 | Head+sill, jamb, sill (ground floor variants), door sill |
| **Generic Window** | 12 | Head (4 variants), jamb, sill (3 variants), mullion, concrete conditions |
| **Accessories** | 5 | Solar shade, cornice, GWB header, GWB-to-mullion, glazing termination |
| **Unit Doors** | 3 | Door sill Unit D (2 variants), roof access door |

### By Wall Condition

| Wall Condition | Detail Count | Typical Details |
|----------------|-------------|-----------------|
| **Concrete (B.O. slab)** | ~12 | Single hung head, casement head, SGD heads, WW heads |
| **Concrete (slab edge)** | ~8 | Window head at flat slab edge variants, SGD sill at slab edge |
| **Concrete header/beam** | ~5 | Horizontal rolling head, generic window head at conc header |
| **Concrete sill/curb** | ~10 | Storefront sill, window sill at concrete, SGD sills |
| **Framed with header** | ~4 | Single hung head at header, SGD 2020 head |
| **Stucco over frame** | ~4 | Storefront door head/jamb stucco, sliding window details |
| **Existing wall** | ~2 | Typ storefront at existing wall |
| **Interior (GWB/partition)** | ~5 | GWB header, GWB to mullion, elevator details |
| **Roof structure** | ~3 | Skylight details |
| **Mullion-only (no wall)** | ~10 | All horizontal/vertical mullion details |
| **Balcony transition** | ~5 | SGD sills at balcony, WW sills at terrace |
| **Terrace/grade transition** | ~5 | SGD sills at terrace, storefront at terrace |

### By Detail Condition

| Condition | Count | Notes |
|-----------|-------|-------|
| **Head** | 14 | Most variety in wall/structure conditions |
| **Sill** | 28 | Largest group - sill is #1 leak point, many specific conditions |
| **Jamb** | 16 | Plan cut sections, including mullion connections |
| **Mullion (H)** | 4 | Storefront and WW horizontal mullion profiles |
| **Mullion (V)** | 4 | Storefront and WW vertical mullion profiles |
| **Mullion (special)** | 2 | Transom, intermediate |
| **Threshold** | 5 | Door thresholds at various floor conditions |
| **Combined** | 5 | Multi-condition sheets (head+jamb, head+sill, full sets) |
| **Full assembly** | 3 | Complete window/door sections |
| **Specialty** | 3 | Cornice, solar shade, termination |

---

## Drawing Pattern Analysis

### Common Conventions Observed

1. **Scale**: 3" = 1'-0" is the dominant scale for individual conditions; 1-1/2" = 1'-0" for composite/full-section sheets
2. **Orientation**: Head details show structure above, window below; sill details show window above, wall below; jamb details are plan-cut horizontal sections
3. **Break lines**: Used at top and bottom to indicate detail is extracted from larger assembly
4. **Hatching**: Concrete shown with standard dot/aggregate hatch; masonry with diagonal hatching; insulation with wavy lines; glass shown as single bold line
5. **Annotations**: Leaders from left side pointing to components, callout text identifying each material/component
6. **Line hierarchy**: Wide for primary structure (slab, wall), medium for frames and assemblies, thin for sealant, flashing, gaskets

### Construction Context

This library is predominantly from **South Florida high-rise multi-family residential** projects based on:
- Heavy use of concrete flat slab construction
- CGI impact window details (HVHZ requirement)
- SGD 2020 and 2400ST series (common Florida residential SGD products)
- WW 7000 window wall system (high-rise curtain wall)
- Balcony/terrace conditions at nearly every sill type
- Hurricane-rated assemblies throughout
- Stucco exterior finish details

### For AI Drawing Generation

When programmatically drawing these details in Revit:
1. Start with the **structure** (slab, wall, beam) using filled regions with appropriate hatch
2. Add the **frame profile** as detail components or filled regions
3. Add **flashing** as thin filled regions or detail lines
4. Add **sealant** as small filled circles or triangles at joints
5. Add **annotations** with leaders pointing to each component
6. Use **break lines** at detail boundaries
7. Maintain the **line weight hierarchy** (wide/medium/thin) for readability
8. Group related conditions (head/jamb/sill) on the same detail sheet when possible
