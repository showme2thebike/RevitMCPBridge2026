# Residential Interior Finish & Specialty Details

> Comprehensive reference for residential-specific construction details: kitchens, bathrooms, stairs, fireplaces, decks/porches, garages, and exterior trim. Fills the gap in the existing commercial-focused library.
> Dimensions, code references (IRC), material specifications, Revit detail components, and common mistakes.
> Generated: 2026-03-05

---

## TABLE OF CONTENTS

1. [Kitchen Details](#1-kitchen-details)
2. [Bathroom Details](#2-bathroom-details)
3. [Residential Stair Details](#3-residential-stair-details)
4. [Fireplace & Chimney Details](#4-fireplace--chimney-details)
5. [Deck & Porch Details](#5-deck--porch-details)
6. [Garage Details](#6-garage-details)
7. [Exterior Trim & Transition Details](#7-exterior-trim--transition-details)

---

## REVIT COMPONENT QUICK REFERENCE -- RESIDENTIAL FINISH DETAILS

Components from the 87-family BD Architect palette used throughout this document:

| Component | Primary Use in Residential Finish |
|-----------|-----------------------------------|
| **Dimension Lumber-Section** | Cabinet nailers, blocking, stair stringers, headers, deck framing |
| **Nominal Cut Lumber-Section** | Trim boards, ledger boards, countertop substrates, newel posts |
| **Plywood-Section** | Cabinet box, subfloor, countertop substrate, stair treads |
| **Gypsum Wallboard-Section** | Interior finish (1/2" standard), garage fire separation |
| **Gypsum Wallboard-Section1** | 5/8" Type X for garage separation at habitable rooms above |
| **Resilient Flooring-Section** | Tile representation, thin-set mortar layer |
| **Rigid Insulation-Section** | Fireplace clearance, thermal breaks |
| **Caulking-Section** | Sealant joints at tub flanges, countertop-to-wall, transitions |
| **Joint Sealant and Backer Rod-Section** | Expansion joints, control joints, tub-to-tile joint |
| **Resilient Topset Base-Section** | Interior base trim, toe kick trim |
| **Plastic-Laminate1** | Countertop surface, laminate faces |
| **Molding - Crown 1** | Crown molding, trim profiles |
| **Anchor Bolt** | Post bases, ledger connections |
| **Common Wood Nails-Side** | Fastener callouts |
| **Reinf Bar Section** | Footing reinforcement, fireplace masonry |
| **Floor Drain with Waterproofing-Section** | Shower pan drain |

**Components NOT in BD Architect library (create or use detail lines/filled regions):**
- Cement backer board (use filled region, concrete hatch, 1/2" thick)
- Waterproofing membrane (use detail line, heavy dashed, magenta)
- Thin-set mortar (use filled region, stipple hatch, 1/4" thick)
- Tile (use filled region with tile/stone hatch pattern)
- Stone/quartz countertop (use filled region, stone hatch)
- Fireplace firebrick (use filled region, brick hatch, annotate as firebrick)
- Flue liner (use detail lines, double-line clay tile)
- Deck joist hangers/hardware (use detail lines with manufacturer note)
- Post base hardware (use detail lines with Simpson callout)
- Screen mesh (use detail line, single thin dashed)
- Flashing (use detail line or thin filled region, 26 ga.)
- Vapor retarder (use detail line, dashed)
- Heating mat (use detail line, wavy/zigzag)
- Corbel/bracket (use detail lines or filled region)

---

## 1. KITCHEN DETAILS

### 1.1 Base Cabinet Section

**Assembly (Floor to Countertop Surface):**

```
    Countertop surface (stone/quartz/laminate)    1-1/4" to 1-1/2"
    ________________________________________________
   |  Plywood substrate (3/4")                     |
   |  ___________________________________________  |
   |  |                                         |  |
   |  |  Cabinet box (3/4" plywood sides)       |  |   34-1/2" cabinet
   |  |  Shelves at 10-12" spacing              |  |   height
   |  |  Drawer slides at top                   |  |
   |  |  Face frame or frameless                |  |
   |  |_________________________________________|  |
   |  |  Toe kick board (1/4" plywood)          |  |   4-1/2" high
   |  |_________________________________________|  |   3" deep recess
   |_______________________________________________|
        Finished Floor
        Subfloor (3/4" plywood)
        Floor joist / slab
```

**Layer Stack (Front to Back):**

| # | Layer | Material | Dimension | Notes |
|---|-------|----------|-----------|-------|
| 1 | Face frame | 3/4" x 1-1/2" hardwood | 3/4" thick | Frameless = no face frame |
| 2 | Cabinet side | 3/4" plywood or particleboard | 3/4" | Interior grade |
| 3 | Cabinet back | 1/4" plywood or hardboard | 1/4" | Stapled to back of box |
| 4 | Shelf | 3/4" plywood or melamine | 3/4" | Adjustable, 10-12" spacing |
| 5 | Bottom panel | 3/4" plywood | 3/4" | Raised 4-1/2" for toe kick |

**Critical Dimensions:**
- Total height: 36" AFF (34-1/2" cabinet + 1-1/2" countertop)
- Depth: 24" (box depth 21" + 3" face frame/door)
- Toe kick: 4-1/2" high x 3" deep recess
- Standard widths: 9", 12", 15", 18", 21", 24", 27", 30", 33", 36", 42", 48"
- Drawer heights: 4", 6", 8", 10" (front height)

**Revit Detail Components:**
- Plywood-Section (cabinet box sides, bottom, substrate)
- Plastic-Laminate1 (countertop surface for laminate)
- Dimension Lumber-Section (nailer strips, blocking)
- Resilient Topset Base-Section (toe kick trim)
- Caulking-Section (countertop-to-wall joint)

**Drawing Annotations:**
- "3/4" PLY. CABINET BOX"
- "1-1/2" STONE COUNTERTOP ON 3/4" PLY. SUBSTRATE"
- "4-1/2" TOE KICK, 3" RECESS"
- "SHIM AS REQ'D FOR LEVEL"
- "SECURE TO WALL W/ (2) #10 x 3" SCREWS THRU NAILER"
- Dimension: 36" total height from floor to countertop surface

**Common Mistakes:**
- Not showing nailer strip at wall (cabinets need secure wall attachment)
- Omitting toe kick dimension (critical for ADA accessibility variants)
- Forgetting countertop overhang (1" to 1-1/2" past face of door)
- Not noting cabinet screw-together at adjacent units

---

### 1.2 Upper Cabinet Section

**Assembly:**

```
        Ceiling (typ. 8'-0" or 9'-0")
        _________________________________
       |                                 |  Variable gap
       |  _____________________________  |  (crown mold or filler)
       |  |                           |  |
       |  | Upper cabinet box         |  |  30", 36", or 42" tall
       |  | (3/4" plywood sides)      |  |
       |  | 12" deep (typ.)           |  |
       |  | Adjustable shelves        |  |
       |  |___________________________|  |
       |_________________________________|
                                          <-- 18" clear above counter
        _________________________________
       | Countertop surface (36" AFF)    |
```

**Critical Dimensions:**
- Bottom of upper cabinet: 54" AFF (36" counter + 18" backsplash zone)
- Cabinet height: 30" (standard), 36" (tall), 42" (to ceiling)
- Depth: 12" (standard), 15" or 24" (above refrigerator)
- Top of 30" upper: 84" AFF (7'-0")
- Top of 36" upper: 90" AFF (7'-6")
- Top of 42" upper: 96" AFF (8'-0", meets standard ceiling)

**Code Requirements:**
- No specific IRC code for cabinet height, but NKBA recommends 15"-18" between counter and upper cabinet bottom
- If above cooktop/range: IRC M1503.1 requires min 24" clearance from unprotected combustible (30" typical with hood)

**Revit Detail Components:**
- Plywood-Section (cabinet box)
- Dimension Lumber-Section (wall nailer/blocking at 54" and 84")
- Molding - Crown 1 (crown molding at ceiling if specified)
- Common Wood Nails-Side (fastener callouts to wall studs)

**Drawing Annotations:**
- "UPPER CABINET - 12" DEEP x 30" TALL (TYP.)"
- "SECURE TO WALL STUDS W/ #10 x 3" SCREWS THRU NAILER STRIP"
- "1x4 NAILER BEHIND GWB AT 54" & 84" AFF"
- "18" CLEAR ABOVE COUNTERTOP (MIN.)"
- Dimension lines: 18" backsplash zone, 30" cabinet height

---

### 1.3 Kitchen Island Section

**Assembly (Section Cut Through Island):**

```
                    12-15" overhang
                    for seating
                   |<---------->|
    _______________________________________________
   |  Countertop (1-1/4" to 3 cm)                 |
   |  ____________________________________________ |
   |  | 3/4" ply substrate                       | |
   |  |  ______________________________________  | |
   |  | | Cabinet box (same as base cabinet)   | | |
   |  | | May contain:                         | | |
   |  | | - Sink cutout + plumbing chase       | | |
   |  | | - Dishwasher opening                 | | |
   |  | | - Electrical box for outlets         | | |
   |  | | - Pull-out trash/recycling           | | |
   |  | |_____________________________________| | |
   |  |________________________________________| |
   |_______________________________________________|
        Finished Floor
        ---- Plumbing/Electrical in slab or floor ----
```

**Critical Dimensions:**
- Standard island height: 36" (same as counters)
- Bar-height island: 42" (requires 12" knee clearance at 42" height)
- Seating overhang: 12"-15" minimum (12"+ requires support corbel/bracket)
- Corbel spacing: 24"-36" o.c. for overhangs > 12"
- Seat width: 24" per stool/chair
- Walkway clearance: 36" min (NKBA), 42"-48" preferred between island and counters

**Plumbing and Electrical:**
- Sink in island: requires drain, hot/cold supply through floor slab or joist bay
- Island electrical: min (1) 20A circuit dedicated, outlets per NEC 210.52(C)(2)
- NEC requires receptacle within 24" of any point along countertop; islands need at least one outlet
- GFCI protection required for all kitchen countertop receptacles (NEC 210.8)

**Revit Detail Components:**
- Plywood-Section (cabinet box, substrate)
- Plastic-Laminate1 or filled region (stone countertop)
- Dimension Lumber-Section (blocking for corbels, nailers)
- Caulking-Section (sink-to-countertop seal)

**Drawing Annotations:**
- "KITCHEN ISLAND - SEE PLAN FOR DIMS"
- "12" OVERHANG W/ STEEL CORBEL @ 30" O.C."
- "PROVIDE (1) 20A DEDICATED CIRCUIT"
- "GFCI PROTECTED OUTLET(S) PER NEC 210.52(C)"
- "PLUMBING ROUGH-IN BELOW SLAB - SEE PLUMBING"
- Section reference bubble to plumbing plan

**Common Mistakes:**
- Forgetting corbel support for overhangs greater than 12"
- Not showing electrical outlet locations (code requires them)
- Omitting plumbing chase / access panel for sink islands
- Not coordinating countertop overhang with seating plan

---

### 1.4 Countertop Edge Profiles

**Common Edge Types (shown in section at 3" = 1'-0" or full-size):**

```
EASED (STRAIGHT)      BULLNOSE           OGEE              WATERFALL
 ___________          ___________        ___________        ___________
|           |        |           |      |     ~     |      |           |
|           |        |          /       |    / \    |      |           |
|___________|        |_________/        |___/   \___|      |           |
                                                           |           |
                                                           |           |
                                                           |___________|
Simple 1/8" ease      Full round-over    S-curve profile    Slab continues
at top corners        on exposed edge    (classic/formal)   vertically to
                                                            floor
```

| Profile | Typical Thickness | Material Suitability | Cost Level | Notes |
|---------|-------------------|---------------------|------------|-------|
| Eased/Straight | 1-1/4" (3 cm) | All | $ | Default, most common |
| Bullnose (half) | 1-1/4" | Granite, quartz, marble | $$ | Rounded top edge only |
| Bullnose (full) | 1-1/4" | Granite, marble | $$ | Rounded top and bottom |
| Ogee | 1-1/4" | Granite, marble, quartz | $$$ | Classic/traditional |
| Bevel | 1-1/4" | All | $$ | 45-degree chamfer |
| Waterfall | 1-1/4" to 2" | Quartz, marble, porcelain | $$$$ | Requires miter joint at corner |
| Mitered (thick look) | 2 x 1-1/4" laminated | Quartz, porcelain | $$$ | Two slabs mitered to look 2-1/2" thick |

**Revit Detail Components:**
- Plastic-Laminate1 (laminate countertops)
- Filled region with stone hatch (natural stone/quartz)
- Plywood-Section (substrate below laminate)
- Caulking-Section (countertop-to-wall joint)

**Drawing Annotations:**
- "COUNTERTOP EDGE PROFILE: [TYPE] - SEE FINISH SCHEDULE"
- "1-1/4" (3 CM) QUARTZ COUNTERTOP"
- "3/4" PLY. SUBSTRATE (LAMINATE ONLY)"
- "SEALANT JOINT AT WALL, TYP."
- For waterfall: "MITER JOINT W/ EPOXY, REINFORCE W/ RODDING"

---

### 1.5 Backsplash Detail

**Assembly (Section at Countertop-to-Wall):**

```
    Upper cabinet bottom (54" AFF)
    _________________________________
   |  1/2" GWB                       |
   |  Tile backsplash (typ. 18")     |   Tile runs from countertop
   |  1/4" modified thin-set         |   to bottom of upper cabinet
   |  1/2" GWB (existing wall)       |
   |_________________________________|
   |  Caulk joint (silicone)         |  <-- NEVER grout at change of plane
   |_________________________________|
   |  Countertop surface             |
```

**Critical Notes:**
- Backsplash height: typically 18" (countertop to upper cabinet bottom)
- Full-height backsplash: countertop to ceiling (40"-60" depending on ceiling height)
- Tile is set on 1/4" modified thin-set directly over painted GWB (no cement board needed for vertical non-wet area)
- Joint at countertop-to-wall change of plane: ALWAYS 100% silicone caulk (color-matched), NEVER grout
- Joint at upper cabinet-to-tile: silicone caulk or leave 1/8" gap behind cabinet

**Revit Detail Components:**
- Gypsum Wallboard-Section (wall substrate)
- Resilient Flooring-Section (tile representation)
- Caulking-Section (countertop-to-tile joint)

**Drawing Annotations:**
- "TILE BACKSPLASH - SEE FINISH SCHEDULE"
- "SET IN 1/4" MODIFIED THIN-SET OVER PAINTED GWB"
- "SILICONE CAULK JOINT AT COUNTERTOP (COLOR MATCH)"
- "TILE TO UNDERSIDE OF UPPER CABINET"

---

### 1.6 Range Hood / Vent Hood Detail

**Assembly (Section Through Hood and Duct):**

```
    Roof or exterior wall
    _______________________
   |  Wall/roof cap         |  <-- Backdraft damper
   |  _____________________  |
   |  | Rigid metal duct   | |  <-- 6" or 8" round (per mfr.)
   |  | (galv. or alum.)   | |
   |  |                    | |
   |  | Through cabinet    | |  <-- Upper cabinet modified
   |  | or wall chase      | |
   |  |____________________| |
   |_________________________|
   |  Hood canopy            |  <-- Min. 24" above electric range
   |  (stainless/painted)    |      Min. 30" above gas range
   |  Fan/blower unit        |      (or per mfr. instructions)
   |_________________________|
   |  Range/cooktop          |
```

**Critical Dimensions:**
- Min. clearance to combustibles: 30" above gas cooktop, 24" above electric (verify mfr.)
- IRC M1503.1: exhaust required for residential cooking, min 100 CFM intermittent or 25 CFM continuous
- Duct size: 6" round (min for most hoods), 8" for > 600 CFM
- Duct material: rigid galvanized steel or aluminum (NEVER flexible/vinyl)
- All joints sealed with foil tape (not duct tape)
- Makeup air required for hoods > 400 CFM (IRC M1503.6)

**Revit Detail Components:**
- Dimension Lumber-Section (framing around duct chase)
- Gypsum Wallboard-Section (enclosure around duct)
- Detail lines (duct, hood outline)

**Drawing Annotations:**
- "RANGE HOOD - [CFM] - SEE SPEC"
- "6" (OR 8") RIGID METAL DUCT TO EXTERIOR"
- "SEAL ALL JOINTS W/ FOIL TAPE"
- "WALL/ROOF CAP W/ BACKDRAFT DAMPER"
- "30" MIN. ABOVE GAS COOKTOP (OR PER MFR.)"
- "MAKEUP AIR REQ'D IF > 400 CFM (IRC M1503.6)"

---

### 1.7 Under-Cabinet Lighting Detail

**Assembly (Section at Upper Cabinet Bottom):**

```
    Upper cabinet interior
    ______________________________
   |  Cabinet bottom (3/4" ply)   |
   |  ____________________________| <-- Light rail molding (1-1/2" x 3/4")
   |  | LED strip or puck light  ||     conceals fixture from view
   |  | (hardwired or plug-in)   ||
   |  |__________________________||
   |______________________________|
        |                        |
        | 18" backsplash zone    |
        |                        |
    ____________________________
   | Countertop                 |
```

**Key Notes:**
- LED tape light or LED puck lights mounted to underside of upper cabinet
- Light rail molding (1-1/2" x 3/4" profiled strip) conceals fixtures from seated eye level
- Hardwired to dedicated switch (preferred) or plug-in to undercabinet outlet
- NEC requires GFCI protection if plugged into countertop circuit
- Typical color temperature: 2700K-3000K (warm white, residential)

**Drawing Annotations:**
- "LED UNDER-CABINET LIGHT - SEE ELEC."
- "LIGHT RAIL MOLDING TO CONCEAL FIXTURE"
- "HARDWIRED TO SWITCH (OR PLUG-IN TO GFCI OUTLET)"

---

## 2. BATHROOM DETAILS

### 2.1 Tub/Shower Surround Section

**Assembly (Section Through Tub Wall):**

```
    Ceiling
    ___________________________________
   |  1/2" GWB (above wet zone)        |
   |___________________________________|  <-- 72" AFF typical (top of tile)
   |  Tile finish                       |
   |  1/4" modified thin-set mortar     |
   |  Waterproof membrane (Kerdi,       |
   |    RedGard, or sheet membrane)     |
   |  1/2" cement backer board          |  <-- HardieBacker, Durock, Wedi
   |  (screwed to studs @ 8" o.c.)     |
   |  2x4 studs @ 16" o.c.             |
   |___________________________________|
   |  1/8"-1/4" gap above tub flange   |  <-- Critical waterproofing detail
   |  Foam backer rod + silicone caulk  |
   |___________________________________|
   |  Tub flange (nailed to studs)      |
   |___________________________________|
   |  Bathtub basin                     |
   |___________________________________|
        Subfloor
```

**Layer Stack (Interior to Exterior):**

| # | Layer | Material | Thickness | Notes |
|---|-------|----------|-----------|-------|
| 1 | Tile | Ceramic/porcelain | 3/8" typ. | Set in modified thin-set |
| 2 | Thin-set mortar | Modified thin-set | 1/4" | ANSI A118.4 or A118.15 |
| 3 | Waterproof membrane | Kerdi, RedGard, Hydroban | varies | MUST be continuous, no gaps |
| 4 | Backer board | 1/2" cement board | 1/2" | NOT green board (MR drywall) |
| 5 | Studs | 2x4 SPF @ 16" o.c. | 3-1/2" | Blocking for accessories |
| 6 | Vapor retarder | 6-mil poly (optional) | membrane | Behind backer board, some codes |

**Critical Details at Tub Flange:**
- Backer board overlaps tub flange but does NOT touch tub rim
- Maintain 1/8" to 1/4" gap between backer board and tub
- Fill gap with foam backer rod + ASTM C920 silicone sealant
- Waterproof membrane extends over top of tub flange
- NEVER use grout at tub-to-tile joint (use silicone caulk)

**Code Requirements:**
- IRC R702.4: Cement, fiber-cement, or glass mat gypsum backers in wet areas
- Moisture-resistant adhesive (ANSI A118.4 modified thin-set)
- Waterproofing per IAPMO IS-14 or ANSI A118.10

**Revit Detail Components:**
- Dimension Lumber-Section (studs, blocking)
- Gypsum Wallboard-Section (above tile zone)
- Resilient Flooring-Section (tile representation)
- Caulking-Section (tub-to-tile silicone joint)
- Joint Sealant and Backer Rod-Section (backer rod at flange gap)
- Filled region (backer board, thin-set, waterproof membrane)

**Drawing Annotations:**
- "1/2" CEMENT BACKER BOARD ON 2x4 STUDS @ 16" O.C."
- "WATERPROOF MEMBRANE (SCHLUTER KERDI OR EQUAL)"
- "TILE IN MODIFIED THIN-SET - SEE FINISH SCHEDULE"
- "SILICONE CAULK AT TUB (NEVER GROUT)"
- "BACKER BOARD 1/4" ABOVE TUB FLANGE"
- "EXTEND WP MEMBRANE OVER TUB FLANGE"

**Common Mistakes:**
- Using greenboard (MR drywall) instead of cement backer board in wet areas
- Grouting the tub-to-tile joint instead of caulking
- Not waterproofing behind cement board (cement board is NOT waterproof)
- Backer board touching tub rim (wicks moisture)
- Not providing blocking for grab bars, towel bars, soap dishes

---

### 2.2 Shower Niche / Recessed Shelf Detail

**Assembly (Section Through Niche in Stud Wall):**

```
    Stud wall (2x4 @ 16" o.c.)
    _______________________________________
   |  Tile    |                    | Tile   |
   |  Thin-   |  NICHE INTERIOR   | Thin-  |
   |  set     |  Tile all 5 faces | set    |
   |  WP      |  + WP membrane    | WP     |
   |  Backer  |  + backer board   | Backer |
   |  Stud    |_________  ________| Stud   |
   |          | Header  ||  Header|        |
   |  ________|_________|_|_______|______  |
   |  |  Niche shelf (1/2" slope  |     |  |
   |  |  toward shower = 1/4"/ft) |     |  |
   |  |___________________________|_____|  |
   |  |  Sill framing             |     |  |
   |__________|___________________|________|
```

**Critical Dimensions:**
- Typical niche opening: 12" wide x 24"-28" tall (fits between 16" o.c. studs)
- Depth: 3-1/2" (depth of 2x4 stud cavity)
- Double-stud niche: 7" deep (full 2x4 wall thickness + back framing)
- Frame opening 3/4" to 1" larger than desired finished opening
- Shelf must slope toward shower at min 1/4" per foot for drainage

**Waterproofing -- CRITICAL:**
- Waterproof membrane must be continuous on ALL 5 interior faces (top, bottom, sides, back)
- Membrane must lap onto surrounding wall membrane min 2" on all sides
- Schluter KERDI-BAND or equivalent at all inside corners
- KERDI-KERECK pre-formed corners for clean transitions
- Liquid-applied membranes (RedGard, Hydroban, Aquadefense) require 2 coats
- Pre-formed niches (Schluter KERDI-BOARD-SN, GoBoard, Laticrete HydroBan) eliminate most failure points

**Code Requirements:**
- No specific IRC code for niches, but waterproofing standards apply
- TCNA Handbook Method B421 (shower receptor waterproofing)
- IAPMO IS-14 or ANSI A118.10 waterproofing compliance

**Revit Detail Components:**
- Dimension Lumber-Section (framing: header, sill, blocking)
- Resilient Flooring-Section (tile on all faces)
- Caulking-Section (sealant at tile transitions)
- Detail lines (waterproof membrane layer)

**Drawing Annotations:**
- "SHOWER NICHE - 12" W x 24" H x 3-1/2" D (FINISHED)"
- "SLOPE SHELF 1/4" PER FT TOWARD SHOWER"
- "WATERPROOF MEMBRANE CONTINUOUS ON ALL INTERIOR FACES"
- "LAP WP MEMBRANE MIN. 2" ONTO SURROUNDING WALL"
- "1/2" CEMENT BACKER BOARD ON ALL FACES"
- "TILE ALL (5) INTERIOR FACES"

**Common Mistakes:**
- Not sloping the shelf (water pools, grout fails, leaks)
- Incomplete waterproofing membrane (most common shower leak source)
- Placing niche on exterior wall (creates insulation/moisture problems)
- Not accounting for tile thickness when sizing rough opening
- Forgetting blocking for adjustable shelf supports

**Relationship to Other Details:**
- References tub/shower surround detail (Section 2.1) for surrounding wall assembly
- Coordinate with framing plan for stud layout and blocking

---

### 2.3 Vanity Section (Three Sink Types)

**Assembly Variants:**

```
UNDERMOUNT SINK          VESSEL SINK              DROP-IN SINK
____________________    ____________________    ____________________
| Countertop       |   | Countertop       |   | Countertop       |
|   ___________    |   |    __________     |   |  _____________   |
|  |           |   |   |   |          |    |   | |             |  |
|  |  Sink     |   |   |   |  Vessel  |    |   | |  Sink rim   |  |
|  |  (clips)  |   |   |   |  bowl    |    |   | |  overlaps   |  |
|  |___________|   |   |   |__________|    |   | |  countertop |  |
|__________________|   |_____|________|____|   | |_____________|  |
| Cabinet box      |   | Cabinet box       |   | Cabinet box      |
| (see 1.1)        |   | (see 1.1)        |   | (see 1.1)        |
```

**Vanity-Specific Dimensions:**
- Vanity height: 32" to 36" (modern trend is 36" = "comfort height")
- Vanity depth: 18"-21" (smaller than kitchen base cabinets)
- Vessel sink adds 4"-6" above countertop surface
- Undermount requires solid stone/quartz countertop (not laminate)

**Code Requirements:**
- IRC P2705.1: Fixture clearances (15" from center of fixture to wall, 30" center-to-center)
- Hot water max 120 deg F at fixture (IRC P2802.2)
- P-trap accessible for maintenance

**Revit Detail Components:**
- Plywood-Section (cabinet box, substrate)
- Plastic-Laminate1 (laminate countertop)
- Caulking-Section (sink-to-countertop seal)
- Resilient Topset Base-Section (vanity base trim)

**Drawing Annotations:**
- "BATH VANITY - [WIDTH] - SEE ELEV."
- "UNDERMOUNT SINK W/ CLIPS (BY FABRICATOR)"
- "COUNTERTOP: [MATERIAL] - SEE FINISH SCHEDULE"
- "P-TRAP ACCESSIBLE - SEE PLUMBING"
- "36" VANITY HEIGHT (COMFORT HEIGHT)"

---

### 2.4 Heated Floor Detail (Electric Mat Under Tile)

**Assembly (Section Through Floor):**

```
    Tile finish (ceramic/porcelain)        3/8"
    _________________________________________
   |  Modified thin-set mortar              | 1/4"
   |  Electric heating mat (embedded)       | 1/8"  (Nuheat, Ditra-Heat, WarmlyYours)
   |  Self-leveling compound (optional)     | 1/4" - 3/8"
   |  Modified thin-set mortar (bond coat)  | 1/4"
   |  Plywood subfloor (3/4")              | 3/4"
   |  Floor joist                           |
   |_________________________________________|
```

**Layer Stack:**

| # | Layer | Material | Thickness | Notes |
|---|-------|----------|-----------|-------|
| 1 | Tile | Ceramic/porcelain | 3/8" | Best conductor for radiant heat |
| 2 | Thin-set | Modified mortar | 1/4" | Embeds heating cables |
| 3 | Heating mat | Electric cable on mesh | 1/8" | Nuheat, Schluter DITRA-HEAT, WarmlyYours TempZone |
| 4 | Leveler (opt.) | Self-leveling compound | 1/4" | If cables need embedding before tile |
| 5 | Subfloor | 3/4" plywood | 3/4" | Clean, flat, structurally sound |

**Critical Requirements:**
- Thermostat with built-in GFCI required (NEC 424.44)
- Max surface temperature: 85 deg F (29 deg C)
- Sensor wire embedded in thin-set between heating cables
- Dedicated 20A circuit (120V for < 150 sq ft, 240V for larger areas)
- Do NOT cut heating cables (factory-terminated)
- Min. 3" from toilet wax ring, tub edges, cabinets
- Allow thin-set/mortar to fully cure before energizing (typically 28 days)

**Revit Detail Components:**
- Resilient Flooring-Section (tile layer)
- Plywood-Section (subfloor)
- Detail line, wavy/zigzag (heating mat representation)

**Drawing Annotations:**
- "ELECTRIC RADIANT FLOOR HEATING MAT"
- "EMBEDDED IN MODIFIED THIN-SET MORTAR"
- "THERMOSTAT W/ GFCI - SEE ELECTRICAL"
- "DEDICATED 20A CIRCUIT - [120V/240V]"
- "DO NOT ENERGIZE UNTIL MORTAR CURED (28 DAYS)"
- "MIN. 3" FROM FIXTURES, CABINETS, WALLS"

---

### 2.5 Recessed Medicine Cabinet Detail

**Assembly (Section Through Wall at Cabinet):**

```
    Top plate
    ___________________________
   |  Stud   |  Header  | Stud |   <-- 2x4 header across top of opening
   |         |  (2x4)   |      |
   |         |__________|      |
   |         |          |      |
   |  GWB    | Medicine |  GWB |   <-- Cabinet box recessed in stud cavity
   |         | Cabinet  |      |       Typ. 14" W x 24" H x 3-1/2" D
   |         | (recessed|      |
   |         |  box)    |      |
   |         |__________|      |
   |         |  Sill    |      |   <-- 2x4 sill/blocking at bottom
   |         |  (2x4)   |      |
   |_________|__________|______|
```

**Critical Dimensions:**
- Standard recessed cabinet: 14"-16" W x 24"-30" H x 3-1/2" D
- Top of cabinet (behind trim): 72" AFF typical
- Recess depth limited to stud cavity (3-1/2" for 2x4 wall)
- Frame opening 3/4" to 1" larger than cabinet body

**Framing Requirements:**
- Non-load-bearing wall: Cut stud, add horizontal 2x4 header and sill, screw to adjacent studs
- Load-bearing wall: install king studs full height, header with jack studs to transfer load
- If cabinet wider than stud bay (>14-1/2"): must cut stud and add proper header

**Do NOT Install On:**
- Exterior walls (breaks insulation envelope, moisture risk)
- Plumbing walls (conflicts with DWV pipes)
- Fire-rated walls (breaks rating unless properly detailed)

**Revit Detail Components:**
- Dimension Lumber-Section (header, sill, cripple studs)
- Gypsum Wallboard-Section (wall finish each side)
- Detail lines (cabinet body outline)

**Drawing Annotations:**
- "RECESSED MEDICINE CABINET - [SIZE]"
- "2x4 HEADER & SILL BLOCKING"
- "TOP OF CABINET @ 72" AFF"
- "VERIFY NON-LOAD-BEARING WALL"
- "NO EXTERIOR WALLS OR PLUMBING WALLS"

---

## 3. RESIDENTIAL STAIR DETAILS

### 3.1 Closed Stringer Stair Section

**Assembly:**

```
    Handrail (34"-38" above nosing)
    ___
   |   |   Balusters @ 4" max spacing
   |   |   |   |   |   |   |
   |___|___|___|___|___|___|___
   |  Tread (3/4" hardwood)     |   11" min (with nosing)
   |  _________________________|
   | |  1" nosing projection    |   3/4"-1-1/4" nosing
   | |__________________________|
   |  Riser (3/4" plywood/MDF)  |   7-3/4" max
   |  __________________________|
   |  2x12 stringer (cut or     |   Minimum 3-1/2" of wood
   |  housed/routed)            |   remaining at cut (IRC R311.7.5.1)
   |  _________________________|
   |  GWB under stair (if       |
   |  enclosed)                  |
   |_____________________________|
```

**IRC R311.7 Requirements (2021 IRC):**

| Dimension | IRC Requirement | Notes |
|-----------|----------------|-------|
| Riser height | 7-3/4" max | R311.7.5.1 |
| Tread depth | 10" min | R311.7.5.2 (nosing to nosing) |
| Tread depth (no nosing) | 11" min | When no nosing projection |
| Nosing projection | 3/4" min, 1-1/4" max | R311.7.5.3, radius max 9/16" |
| Stair width | 36" min clear | R311.7.1 (above handrail) |
| Headroom | 6'-8" min (80") | R311.7.2 |
| Riser variation | 3/8" max in flight | R311.7.5.1 |
| Tread variation | 3/8" max in flight | R311.7.5.2 |
| Handrail height | 34" min, 38" max | R311.7.8.1 |
| Guardrail height | 36" min (at open side) | R312.1.1 |
| Baluster spacing | 4" max (4" sphere test) | R312.1.3 |
| Open risers | 4" max opening | R311.7.5.1 (if open risers used) |
| Winders | 6" min tread at narrow end | R311.7.5.2.1 |

**Closed (Housed) Stringer:**
- Stringer routed with dado slots for treads and risers
- Treads and risers glued and wedged from back side
- Provides clean finish on exposed side (no visible cuts)
- Typical for formal/finished stairs

**Cut Stringer (Sawtooth):**
- 2x12 min (IRC R311.7.5.1: min 3-1/2" remaining stock at cuts)
- 3 stringers for stairs > 36" wide
- Treads and risers nailed/screwed to cut surfaces
- More common in utility/basement stairs

**Revit Detail Components:**
- Dimension Lumber-Section (stringers, treads, blocking)
- Plywood-Section (risers, tread substrate)
- Nominal Cut Lumber-Section (finished treads)
- Gypsum Wallboard-Section (soffit below if enclosed)
- Molding - Crown 1 (trim at stringer/wall junction)

**Drawing Annotations:**
- "7-3/4" MAX RISER / 10" MIN TREAD (IRC R311.7)"
- "3/4" NOSING PROJECTION (3/4"-1-1/4" PER IRC)"
- "2x12 CUT STRINGER - MIN 3-1/2" REMAINING"
- "HANDRAIL @ 34"-38" ABOVE NOSING LINE"
- "36" MIN CLEAR WIDTH"
- "6'-8" MIN HEADROOM"

**Common Mistakes:**
- Not dimensioning rise AND run (both are critical)
- Omitting headroom dimension at low point
- Not showing the 3-1/2" minimum stock remaining on cut stringer
- Forgetting riser variation tolerance (3/8" max within a flight)
- Finished floor material not accounted for in rise calculation

---

### 3.2 Open Stringer Stair Section

**Assembly:**

```
    Handrail (34"-38")
    ___
   |   |     Balusters through tread
   |   |     |   |   |
   |___|_____|___|___|________
   |  Tread (1-1/16" hardwood)  |   Return nosing on
   |  __________________________|   exposed end
   |  |  Riser (3/4" ply/MDF)  |
   |  |________________________|
   |   /                        |   Open stringer (exposed
   |  /  Cut stringer           |   sawtooth profile)
   | /   (stained/painted)      |
   |/___________________________|
```

**Key Differences from Closed Stringer:**
- Cut profile is visible/exposed (stained or painted finish)
- Treads extend past stringer face (return nosing at open end)
- Balusters typically mortised through tread from top (not through stringer)
- Bottom of stringer is clean diagonal line (no soffit needed)
- More material and finishing labor = higher cost

**Tread Return Nosing:**
- Return nosing wraps around exposed end of tread
- Mitered or routed to match front nosing profile
- Glued and pinned to tread end

**Revit Detail Components:**
- Dimension Lumber-Section (stringer, blocking)
- Nominal Cut Lumber-Section (finished treads with return nosing)
- Plywood-Section (risers)

**Drawing Annotations:**
- "OPEN CUT STRINGER - STAINED TO MATCH TREADS"
- "1-1/16" HARDWOOD TREAD W/ RETURN NOSING"
- "BALUSTERS MORTISED THROUGH TREAD - SEE ELEV."
- IRC dimensions (same as 3.1)

---

### 3.3 Stair at Landing Detail

**Assembly (Section at Mid-Landing):**

```
    Upper flight
    ____________________
   |  Stringer bears    |
   |  on landing        |
   |  framing           |
   |____________________|
   |                    |
   | LANDING            |   Min. 36" x 36" (IRC R311.7.6)
   | (framed as floor)  |   Framed with 2x10 or 2x12 joists
   |                    |   headed into stair walls
   |____________________|
   |  Lower flight      |
   |  stringer bears    |
   |  on landing        |
   |____________________|
```

**Critical Dimensions:**
- Landing length: min 36" measured in direction of travel (IRC R311.7.6)
- Landing width: not less than stair width (36" min)
- Height of doors at landing: floor of landing, not above or below
- Winder landing: min 6" tread at narrow end, 10" at 12" from narrow end

**Framing:**
- Landing framed as floor platform with joists (2x10 or 2x12)
- Joists bear on ledger or hanger into stair walls
- Subfloor (3/4" plywood) over joists
- Stringer from upper flight bears on top of landing framing
- Stringer from lower flight bears on landing framing from below or is supported by kicker plate

**Revit Detail Components:**
- Dimension Lumber-Section (joists, ledger, blocking, stringers)
- Plywood-Section (subfloor)
- Common Wood Nails-Side (joist hanger fasteners)

**Drawing Annotations:**
- "LANDING - 36" MIN. IN DIRECTION OF TRAVEL (IRC R311.7.6)"
- "2x10 LANDING JOISTS @ 16" O.C."
- "3/4" PLYWOOD SUBFLOOR"
- "STRINGER BEARS ON LANDING FRAMING"

---

### 3.4 Newel Post Mounting Detail

**Assembly (Section at Post Base):**

```
    Newel post (4x4 or 6x6)
    ________________________
   |                        |
   |  Lag bolt (or rail     |  <-- 3/8" x 6" lag screws (min 2)
   |  bolt kit) through     |      or dedicated newel bolt hardware
   |  post into framing     |
   |________________________|
   |  Floor finish          |
   |________________________|
   |  Subfloor (3/4" ply)   |
   |________________________|
   |  Blocking between      |  <-- Solid blocking between joists
   |  joists below          |      for anchor point
   |________________________|
   |  Floor joist            |
```

**Mounting Methods:**
1. **Through-bolt from below:** Threaded rod through subfloor into post base, nut with washer from below (strongest)
2. **Lag bolt:** Two 3/8" x 6" lag screws through post base into subfloor and blocking
3. **Newel bolt kit:** Proprietary hardware (e.g., Zipbolt UT) for easy tightening
4. **Newel plate:** Steel mounting plate screwed to floor, post bolts to plate

**Critical Notes:**
- Posts at top and bottom of stairs (starting and landing newels) must resist 200-lb lateral load (IRC R301.5)
- Solid blocking required between joists at post location
- For half-newel at wall: lag into studs and blocking

**Handrail-to-Newel Connection:**
- Rail bolt (draw bolt): 3/4" hole drilled into post, 1/4" pilot into rail end
- Center of rail bolt: 3/4" up from bottom of handrail, centered on rail width
- Pilot hole in rail: 1-1/2" from end, centered on bottom

**Revit Detail Components:**
- Dimension Lumber-Section (post, blocking)
- Plywood-Section (subfloor)
- Anchor Bolt (lag screw/bolt representation)
- Common Wood Nails-Side (supplemental fasteners)

**Drawing Annotations:**
- "4x4 (OR 6x6) NEWEL POST"
- "ANCHOR TO BLOCKING W/ (2) 3/8" x 6" LAG SCREWS (MIN.)"
- "SOLID BLOCKING BETWEEN JOISTS AT POST"
- "200 LB LATERAL LOAD CAPACITY (IRC R301.5)"

---

### 3.5 Handrail Bracket to Wall Detail

**Assembly:**

```
    Handrail (graspable profile)
    _______
   | 1-1/4"|  <-- 1-1/4" to 2" graspable diameter (IRC R311.7.8.3)
   |  to   |
   | 2" dia|
   |_______|
      |
      | Bracket arm (4"-5" projection)
      |
   ___|________
   |  Wall     |
   |  bracket  |   <-- Screwed to stud or blocking with
   |  rosette  |       #14 x 3" screws (min 2 per bracket)
   |___________|
   |  1/2" GWB |
   |  2x4 stud |
   |___________|
```

**Critical Dimensions:**
- Handrail height: 34" min, 38" max above nosing line (IRC R311.7.8.1)
- Graspable profile: 1-1/4" min, 2" max circular cross-section (IRC R311.7.8.3)
- Clearance between handrail and wall: 1-1/2" min (IRC R311.7.8.4)
- Bracket spacing: 4'-0" max o.c. (typical manufacturer spec)
- Bracket screws: min #14 x 3" into stud or blocking (not just drywall)

**Revit Detail Components:**
- Detail lines (handrail profile, bracket outline)
- Gypsum Wallboard-Section (wall finish)
- Dimension Lumber-Section (stud/blocking)
- Common Wood Nails-Side (screw callout)

**Drawing Annotations:**
- "WALL-MOUNTED HANDRAIL - [PROFILE]"
- "34"-38" ABOVE STAIR NOSING LINE (IRC R311.7.8)"
- "1-1/2" MIN CLEARANCE TO WALL (IRC R311.7.8.4)"
- "BRACKET TO STUD @ 4'-0" O.C. MAX"
- "1-1/4" TO 2" GRASPABLE DIA. (IRC R311.7.8.3)"

---

## 4. FIREPLACE & CHIMNEY DETAILS

### 4.1 Masonry Fireplace Section

**Assembly (Full Vertical Section):**

```
    Chimney cap (concrete or metal)
    ___________________________
   |  Spark arrestor           |  <-- 3/4" mesh, extends min 3" above cap
   |___________________________|
   |  Clay flue liner          |  <-- 5/8" min clay tile, 2" clearance to
   |  (within chimney          |      combustibles
   |   masonry walls)          |
   |                           |  <-- 4" min masonry around liner
   |___________________________|
   |  Smoke chamber            |  <-- Max 45 deg slope from vertical
   |  (corbeled or             |      (IRC R1001.8)
   |   parged smooth)          |      Height <= firebox opening width
   |___________________________|
   |  Throat / Damper          |  <-- Min 8" above lintel (IRC R1001.7)
   |  (cast iron or steel)     |      4" min throat depth
   |___________________________|
   |  Firebox                  |  <-- 20" min depth (IRC R1001.6)
   |  - Firebrick lining       |      Firebrick or equiv. lining
   |    (2" min refractory)    |      No combustibles within 6" of opening
   |  - Steel lintel at top    |
   |  - Back wall slopes       |
   |    forward above 14"      |
   |___________________________|
   |  Hearth extension         |  <-- 16" min in front (20" if > 6 sq ft opening)
   |  (4" concrete on          |      8" min each side (12" if > 6 sq ft)
   |   non-combustible         |      IRC R1001.9/R1001.10
   |   support)                |
   |___________________________|
   |  Ash dump (optional)      |
   |  Foundation               |  <-- 12" min concrete footing
   |___________________________|      below frost line, not on wood
```

**IRC Chapter 10 Key Requirements:**

| Component | IRC Section | Requirement |
|-----------|-------------|-------------|
| Firebox depth | R1001.6 | 20" min |
| Firebox lining | R1001.5 | 2" firebrick or equivalent |
| Throat/damper | R1001.7 | 8" min above top of fireplace opening, 4" min depth |
| Smoke chamber slope | R1001.8 | 45 deg max from vertical |
| Smoke chamber height | R1001.8 | Not greater than firebox opening width |
| Flue liner | R1003.11 | Clay tile (5/8" min), stainless steel, or cast-in-place |
| Flue area | R1003.15 | 1/10 of fireplace opening (round), 1/8 (rect.) |
| Chimney walls | R1003.2 | 4" min solid masonry around liner |
| Clearance to combustibles | R1003.18 | 2" min from chimney exterior |
| Hearth extension (front) | R1001.9 | 16" min (20" if opening > 6 sq ft) |
| Hearth extension (sides) | R1001.9 | 8" min (12" if opening > 6 sq ft) |
| Hearth thickness | R1001.9 | 4" min (noncombustible) |
| Chimney height | R1003.9 | 3' above roof penetration, 2' above anything within 10' |
| Chimney cricket | R1003.20 | Required when chimney width > 30" parallel to ridge |
| Foundation | R1001.2 | 12" min concrete, not on wood framing |
| Spark arrestor | R1003.9.1 | 3/8" to 3/4" mesh opening |

**Revit Detail Components:**
- Reinf Bar Section (rebar in foundation, lintel)
- Dimension Lumber-Section (framing around chimney with 2" clearance)
- Rigid Insulation-Section (fire stops at floor/ceiling penetrations)
- Detail lines (flue liner, firebox outline, damper)
- Filled region (firebrick, masonry, concrete hearth)

**Drawing Annotations:**
- "MASONRY FIREPLACE SECTION - SEE PLAN FOR DIMS"
- "2" FIREBRICK LINING (IRC R1001.5)"
- "20" MIN FIREBOX DEPTH (IRC R1001.6)"
- "DAMPER 8" MIN ABOVE OPENING (IRC R1001.7)"
- "SMOKE CHAMBER: 45 DEG MAX SLOPE (IRC R1001.8)"
- "CLAY FLUE LINER - 5/8" MIN (IRC R1003.11)"
- "2" CLEARANCE TO COMBUSTIBLES (IRC R1003.18)"
- "16" HEARTH EXTENSION (FRONT)"
- "12" CONC. FOUNDATION BELOW FROST LINE"
- "CHIMNEY: 3' ABOVE ROOF, 2' ABOVE ANYTHING WITHIN 10'"

**Common Mistakes:**
- Not showing 2" clearance to combustibles (framing, sheathing)
- Omitting fire-stop at each floor/ceiling penetration
- Forgetting hearth extension dimensions (varies by opening size)
- Not calling out flue size relative to firebox opening
- Missing spark arrestor at chimney top

---

### 4.2 Factory-Built (Zero-Clearance) Fireplace

**Assembly (Section):**

```
    Metal chimney pipe (Class A)
    ___________________________
   |  Double or triple-wall     |  <-- UL 103 listed chimney
   |  stainless/galv. pipe      |      2" clearance to combustibles
   |___________________________|      (or per listing)
   |  Ceiling/attic firestop    |
   |___________________________|
   |  Framing (2x4 or 2x6)     |  <-- Zero-clearance to framing
   |  enclosure                  |      per UL listing
   |___________________________|
   |  Factory-built firebox      |  <-- UL 127 listed
   |  (steel with refractory    |      Install per manufacturer
   |   lining panel)            |
   |___________________________|
   |  Noncombustible hearth pad  |  <-- Per manufacturer specs
   |___________________________|      (may be less than masonry req.)
        Combustible floor allowed
        (per listing)
```

**Key Differences from Masonry:**
- No concrete foundation required (self-supporting, lightweight)
- Zero clearance to combustibles at firebox sides/back (per UL listing)
- Factory-supplied chimney system (NOT field-built)
- Hearth extension may be smaller per manufacturer listing
- MUST be installed exactly per manufacturer instructions and listing

**Code Requirements:**
- IRC R1004: Factory-built fireplaces installed per listing and manufacturer instructions
- UL 127: Standard for factory-built fireplaces
- UL 103: Standard for factory-built chimneys

**Drawing Annotations:**
- "FACTORY-BUILT FIREPLACE - UL 127 LISTED"
- "INSTALL PER MANUFACTURER INSTRUCTIONS"
- "CLASS A CHIMNEY - UL 103 LISTED"
- "CLEARANCES PER MFR. LISTING"
- "FIRESTOP AT EACH FLOOR/CEILING PENETRATION"

---

### 4.3 Gas Fireplace Direct-Vent Detail

**Assembly (Section -- Horizontal Vent Through Wall):**

```
    Interior                 |  Wall  |  Exterior
                             |        |
    Gas fireplace unit       | Coaxial| Vent terminal
    (sealed combustion)      | vent   | (intake + exhaust)
    ________________________ | pipe   | _____________
   |  Firebox w/ ceramic     | ______ |             |
   |  logs or glass media    ||      || Outer pipe  |
   |                         || Inner|| = intake    |
   |  Burner assembly        || pipe || Inner pipe  |
   |                         ||=exh. || = exhaust   |
   |  No chimney/flue req'd  ||______||_____________|
   |_________________________|        |
   |  Gas supply line         |        |
   |  (CSST or black iron)    |        |
   |__________________________|        |
```

**Key Features:**
- Sealed combustion: draws outside air for combustion, exhausts outside
- Coaxial pipe: inner pipe = exhaust, outer pipe = intake
- Can vent horizontally (through wall) or vertically (through roof)
- No traditional chimney required
- High efficiency (70-90% AFUE)

**Clearance and Termination:**
- Vent terminal: min 12" above grade
- Min 4' below window/door/opening (most manufacturers)
- Min 3' from gas meter, electrical meter
- Per manufacturer listing for exact clearances
- IRC G2427 and manufacturer instructions govern vent termination

**Drawing Annotations:**
- "DIRECT-VENT GAS FIREPLACE"
- "COAXIAL VENT: INNER=EXHAUST, OUTER=INTAKE"
- "VENT TERMINAL PER MFR. - MIN 12" ABOVE GRADE"
- "GAS SUPPLY: 1/2" CSST OR BLACK IRON"
- "CLEARANCES PER MFR. LISTING"
- "SEALED COMBUSTION - NO ROOM AIR USED"

---

### 4.4 Chimney Through Roof Detail (Cricket & Flashing)

**Assembly (Section at Uphill Side of Chimney):**

```
              Chimney
              _______
             |       |
    _________|       |_________
   |  Counter-|       | Step   |
   |  flashing|       | flash. |  <-- Step flashing laps under shingles
   |  (into   |       |        |      Counter-flashing into mortar joint
   |  mortar  |       |        |
   |  joint)  |_______|        |
   |  ________________________ |
   | / Cricket (saddle)      / |  <-- IRC R1003.20: required when
   |/ Framed plywood         / |      chimney width > 30" parallel
   |________________________/  |      to ridge line
   |  Shingles over           |
   |  ice & water shield      |  <-- Self-adhered membrane under cricket
   |__________________________|
```

**IRC R1003.20 -- Chimney Cricket:**
- Required when chimney dimension parallel to ridge > 30"
- Cricket height per Table R1003.20 based on roof slope and chimney width
- Flash and counterflash same as chimney-roof intersection
- Cricket framed with plywood on 2x4 framing, covered with metal or shingles
- Ice and water shield membrane under cricket and extending 4" beyond

**Flashing System (4 Components):**
1. **Base flashing (front apron):** L-shaped metal bent to chimney and roof, extends 4" up chimney, 4" onto roof
2. **Step flashing (sides):** Individual L-shaped pieces woven with each shingle course, 4" on chimney, 4" on roof
3. **Counter-flashing:** Metal bent into mortar joint (reglet), laps over step flashing, sealed with masonry sealant
4. **Back pan / cricket flashing:** Sheet metal or membrane behind chimney, tied to step flashing

**Revit Detail Components:**
- Dimension Lumber-Section (cricket framing)
- Plywood-Section (cricket deck)
- Asphalt Shingles Dynamic-2 (roofing)
- Underlayment (ice & water shield)
- Detail lines (flashing, counter-flashing)
- Caulking-Section (sealant at counter-flashing reglet)

**Drawing Annotations:**
- "CHIMNEY CRICKET - IRC R1003.20"
- "STEP FLASHING - 4" ON CHIMNEY, 4" ON ROOF"
- "COUNTER-FLASHING INTO MORTAR JOINT - SEAL W/ MASONRY SEALANT"
- "ICE & WATER SHIELD UNDER CRICKET + 4" BEYOND"
- "CRICKET HEIGHT PER TABLE R1003.20"
- "CHIMNEY: 3' ABOVE ROOF, 2' ABOVE ANYTHING WITHIN 10' (IRC R1003.9)"

---

### 4.5 Hearth Detail and Mantel Support

**Hearth Extension Section:**

```
    Firebox opening
    _______________________________
   |  Noncombustible hearth        |  <-- 4" min concrete/masonry or
   |  extension                     |      per UL listing for prefab
   |  (stone, tile, brick           |
   |   on concrete substrate)      |
   |_______________________________|
   |  Plywood subfloor             |  <-- Subfloor under hearth only if
   |  (if cantilever/raised)       |      noncombustible layer = 4" min
   |_______________________________|
        16" min in front of opening
        (20" if opening > 6 sq ft)
        8" min each side
        (12" if opening > 6 sq ft)
```

**Mantel Clearance to Combustibles (IRC R1001.11):**

| Mantel Projection from Wall | Min. Clearance Above Fireplace Opening |
|-----------------------------|---------------------------------------|
| 1-1/2" or less | 6" |
| 1-1/2" to 3" | 9" |
| 3" to 6" | 12" |
| Over 6" | 12" |

- Mantel shelf support: steel angle bracket lag-bolted to framing/masonry
- Wood mantel: must maintain IRC clearances based on projection distance
- Noncombustible mantel (stone, concrete): no clearance restriction

**Drawing Annotations:**
- "HEARTH EXTENSION: [MATERIAL] ON 4" CONC. SUBSTRATE"
- "16" (OR 20") FRONT, 8" (OR 12") SIDES - IRC R1001.9"
- "MANTEL: [X]" PROJECTION, [Y]" CLEARANCE ABOVE OPENING (IRC R1001.11)"

---

## 5. DECK & PORCH DETAILS

### 5.1 Deck Ledger to House Connection

**THE MOST CRITICAL DECK DETAIL -- #1 cause of deck failures is ledger connection/flashing failure.**

**Assembly (Section at Ledger Board):**

```
    Siding removed at ledger zone
    ___________________________________
   |  Housewrap / WRB                  |  <-- WRB laps OVER top of Z-flashing
   |  (laps over flashing at top)      |
   |  _________________________________|
   |  Z-flashing (galv. or alum.)      |  <-- Extends up wall min 1" above ledger
   |  ________________________________ |      top, drip edge over front of ledger
   |  Self-adhered membrane            |  <-- Extends 1" above ledger, 2" below,
   |  (flashing tape)                  |      4" past ends
   |  ________________________________ |
   |  Ledger board (2x8 min PT)        |  <-- Bolted through rim joist
   |  1/2" dia. lag bolts or           |      NOT to studs
   |  through-bolts @ Table R507.6     |
   |  ________________________________ |
   |  1/2" spacer (washer stack or     |  <-- Creates drainage plane behind ledger
   |  manufactured spacer)             |      (prevents rot at rim joist)
   |  ________________________________ |
   |  Rim joist (house framing)        |
   |  ________________________________ |
   |  Sheathing (OSB/plywood)          |
   |___________________________________|
```

**Layer Stack (Exterior to Interior):**

| # | Layer | Material | Notes |
|---|-------|----------|-------|
| 1 | Z-flashing | 26 ga. galv. or aluminum | Over top of ledger, under siding above |
| 2 | Self-adhered membrane | Flashing tape (Grace Vycor, etc.) | On sheathing behind ledger, min 1" above, 2" below |
| 3 | Spacer | 1/2" washer stack or manufactured | Creates drainage gap behind ledger |
| 4 | Ledger board | 2x8 min, pressure-treated | Southern pine or approved species |
| 5 | Lag bolts | 1/2" dia. at IRC Table R507.6 spacing | Or through-bolts with washers |
| 6 | Rim joist | House framing | Ledger bolts into this, NOT into studs |

**IRC R507.6 Ledger Attachment:**
- Bolts: 1/2" dia. lag screws or through-bolts
- Spacing per IRC Table R507.6 (based on joist span and species)
- Typical: 1/2" lag @ 16" o.c. staggered top/bottom for shorter spans
- Edge distance: 2" min from top/bottom, 2" from ends
- Siding MUST be removed at ledger area
- Ledger MUST attach to rim/band joist, NOT studs

**Code Requirements:**
- IRC R507.6: Ledger board attachment
- IRC R507.2.1: Decay-resistant or pressure-treated lumber
- Flashing: IRC R507.6.1 requires flashing at ledger-to-wall connection

**Revit Detail Components:**
- Dimension Lumber-Section (ledger, rim joist, studs)
- Plywood-Section (wall sheathing)
- Anchor Bolt (lag bolts)
- Caulking-Section (sealant at flashing terminations)
- Detail lines (flashing, membrane)

**Drawing Annotations:**
- "DECK LEDGER - 2x[SIZE] PT LUMBER"
- "1/2" DIA. LAG BOLTS @ [SPACING] O.C. PER IRC TABLE R507.6"
- "SELF-ADHERED FLASHING MEMBRANE BEHIND LEDGER"
- "Z-FLASHING OVER LEDGER, UNDER SIDING"
- "1/2" SPACERS FOR DRAINAGE BEHIND LEDGER"
- "ATTACH TO RIM JOIST (NOT STUDS)"
- "REMOVE SIDING AT LEDGER ZONE"

**Common Mistakes:**
- Attaching ledger to studs instead of rim joist (insufficient capacity)
- No flashing or incomplete flashing (water intrusion, rot, failure)
- Not removing siding before attaching ledger
- Using nails instead of bolts (code violation)
- No spacers (water trapped behind ledger = rot)
- Flashing installed under WRB instead of over (water directed behind WRB)

---

### 5.2 Deck Post-to-Footing Connection

**Assembly (Section):**

```
    Deck beam (2-ply 2x10 or 6x6)
    ________________________________
   |  Post cap connector            |  <-- Simpson PC series or equiv.
   |  (Simpson PC44 or PC66)        |      Connects beam to post top
   |________________________________|
   |  Post (4x4 or 6x6 PT)         |
   |  (pressure-treated or          |  <-- Min 4x4 for heights < 8'
   |   cedar/redwood heartwood)     |      6x6 for heights > 8' or > 6' unbraced
   |________________________________|
   |  Post base connector           |  <-- Simpson ABA/ABU/PBS series
   |  (standoff type -- lifts       |      Elevates post 1" above concrete
   |   post off concrete)           |      to prevent moisture wicking
   |________________________________|
   |  J-bolt embedded in concrete   |  <-- 1/2" or 5/8" J-bolt, 7" embed min
   |________________________________|
   |  Concrete footing              |  <-- IRC R403.1: below frost line
   |  (tubular form or              |      Min 12" dia for 4x4 post
   |   poured pier)                 |      Min 18" dia for 6x6 post
   |________________________________|      6" min above grade
   |  Undisturbed soil              |
```

**Code Requirements:**
- IRC R507.8: Post-to-beam connection required (gravity + uplift)
- IRC R403.1: Footings below frost line depth (varies by jurisdiction)
- Post: min 4x4, pressure-treated or naturally durable (IRC R507.7)
- Post height: affects column design; > 8' may require 6x6 or engineering
- Standoff base: 1" min clearance above concrete (prevents rot)

**Hardware Callouts:**
- Post cap: Simpson PC44 (4x4), PC66 (6x6), or equivalent
- Post base: Simpson ABA44 (adjustable), ABU44, PBS44 (standoff), or equivalent
- J-bolt: 1/2" x 10" hot-dipped galvanized, 7" embed in concrete

**Revit Detail Components:**
- Dimension Lumber-Section (post)
- Nominal Cut Lumber-Section (beam)
- Anchor Bolt (J-bolt)
- Detail lines (Simpson hardware outline)
- Reinf Bar Section (optional rebar in footing)

**Drawing Annotations:**
- "4x4 (OR 6x6) PT POST ON SIMPSON [MODEL] BASE"
- "CONCRETE PIER FOOTING - [DIA.] x [DEPTH] BELOW FROST LINE"
- "1/2" J-BOLT W/ 7" MIN EMBED"
- "STANDOFF BASE - 1" MIN ABOVE CONCRETE"
- "POST CAP: SIMPSON [MODEL] - CONNECT BEAM TO POST"

---

### 5.3 Deck Beam-to-Post, Joist-to-Beam Connections

**Beam-to-Post:**

```
    2-ply 2x10 beam (built-up)
    ___________________________
   |  Through-bolts (2) 1/2"   |  <-- Stagger top and bottom
   |  or Simpson post cap      |
   |___________________________|
   |  6x6 post                 |
```

**Joist-to-Beam:**

```
    Joist (2x8, 2x10, 2x12)
    ___________________________
   |  Joist hanger              |  <-- Simpson LUS (face mount)
   |  (Simpson LUS series)      |      or top-flange hanger
   |___________________________|
   |  Beam                      |
   |___________________________|

    -- OR --

    Joist bearing on top of beam
    ___________________________
   |  Hurricane tie             |  <-- Simpson H1 or H2.5
   |  (Simpson H-series)        |
   |  Joist  |  Beam            |
   |_________|__________________|
```

**IRC R507.5 and R507.6:**
- Joists to beam: approved connectors or bearing with lateral restraint
- Beam to post: approved connectors (Simpson or engineered)
- All hardware: hot-dipped galvanized or stainless for PT lumber contact

**Drawing Annotations:**
- "JOIST TO BEAM: SIMPSON [MODEL] HANGER"
- "BEAM TO POST: SIMPSON [MODEL] POST CAP"
- "ALL HARDWARE HDG OR STAINLESS (PT LUMBER CONTACT)"

---

### 5.4 Deck Railing Post Attachment

**Assembly (Post Bolted to Rim Joist):**

```
    4x4 railing post
    ___________________
   |                   |
   |  (2) 1/2" dia.   |  <-- Through-bolts with washers
   |  through-bolts    |      2.5" min vertical spacing (for 2x8)
   |  with washers     |      5" max vertical spacing (for 2x10)
   |                   |
   |  Upper bolt:      |  <-- Connect to holdown/tension tie
   |  1800 lb tension  |      (e.g., Simpson DTT2Z)
   |  capacity req'd   |
   |___________________|
   |  Rim/end joist     |  <-- Min 2x8 end/rim joist
   |  (min 2x8)        |      Connected to adjacent joists
   |___________________|      to prevent rotation
```

**IRC R507.10 (2021 IRC) Requirements:**
- Guard posts: 4x4 minimum, NO notching allowed
- Through-bolts: min (2) 1/2" dia. with washers
- Upper bolt: tension device with 1800-lb capacity minimum
- Bolt edge distance: 2" min from edge of wood
- Vertical bolt spacing: 2.5" min, 5" max
- Post spacing: 6'-0" max o.c.
- Guard height: 36" min above deck surface (IRC R312.1.1)
- Baluster spacing: 4" max (4" sphere test) (IRC R312.1.3)

**Guardrail Load Requirements:**
- 200 lb concentrated at any point along top rail (IRC R301.5)
- 50 plf uniform along top rail
- 50 lb over 1 sq ft infill area (balusters)

**Revit Detail Components:**
- Dimension Lumber-Section (post, rim joist)
- Anchor Bolt (through-bolts)
- Detail lines (holdown/tension hardware)

**Drawing Annotations:**
- "4x4 GUARD POST - NO NOTCHING (IRC R507.10)"
- "(2) 1/2" THROUGH-BOLTS W/ WASHERS"
- "UPPER BOLT: 1800 LB TENSION DEVICE (SIMPSON DTT2Z OR EQUIV.)"
- "36" MIN GUARD HEIGHT (IRC R312.1.1)"
- "4" MAX BALUSTER SPACING (IRC R312.1.3)"
- "POST SPACING 6'-0" MAX O.C."

---

### 5.5 Porch Column Base Detail

**Assembly (Section at Column Base):**

```
    Column shaft (wood, fiberglass, or PVC)
    _______________________
   |  Column base molding   |  <-- Decorative base, conceals connection
   |  (cap/trim piece)      |
   |________________________|
   |  Plinth block           |  <-- Raises column off floor
   |  (3/4" PVC or PT wood) |      Prevents moisture wicking
   |________________________|
   |  Sealant joint          |  <-- Silicone caulk around base perimeter
   |________________________|
   |  Porch floor            |  <-- Sloped 1/4" per foot away from house
   |  (tongue & groove PT    |      for drainage
   |   or composite decking) |
   |________________________|
   |  Floor framing (2x8     |
   |   or 2x10 joists)       |
   |________________________|
```

**Key Notes:**
- Column typically sits on plinth block (not direct to floor)
- Plinth block: PVC, PT lumber, or composite (NOT raw wood)
- Sealant at base perimeter to prevent water entry
- Porch floor slopes 1/4" per foot away from house
- Load path: roof > beam > column > plinth > floor framing > foundation
- IRC R301.5: Column must support tributary roof/ceiling loads

**Drawing Annotations:**
- "[SIZE] PORCH COLUMN ON PLINTH BLOCK"
- "SEALANT JOINT AT BASE PERIMETER"
- "PORCH FLOOR: 1/4" PER FT SLOPE (AWAY FROM HOUSE)"
- "VERIFY COLUMN LOAD CAPACITY FOR TRIBUTARY AREA"

---

### 5.6 Screen Porch Detail

**Assembly (Section at Screen Frame):**

```
    Roof structure (shed or gable)
    ________________________________
   |  Beam / header                  |
   |________________________________|
   |  Screen frame top rail          |  <-- Aluminum or wood frame
   |  _____________________________  |
   |  |  Fiberglass screen mesh   |  |  <-- 18x16 or 20x20 mesh
   |  |  (stapled or spline)      |  |
   |  |___________________________|  |
   |  Screen frame bottom rail      |  <-- 30"-36" above floor = kick rail
   |________________________________|
   |  Knee wall (optional)          |  <-- 2x4 framed, clad with trim/siding
   |  30"-36" high                  |      Protects screen from damage
   |________________________________|
   |  Porch floor                    |
```

**Key Notes:**
- Screen mesh: fiberglass (standard), aluminum (more durable), or pet-resistant
- Frame systems: manufactured aluminum (strongest), site-built wood
- Knee wall (kick rail): 30"-36" tall solid wall below screens prevents damage
- Ceiling: beadboard, T&G, or vinyl soffit (ventilated or solid)
- Ceiling fan: requires fan-rated junction box in ceiling structure

**Drawing Annotations:**
- "SCREEN FRAME: ALUMINUM CHANNEL W/ SPLINE"
- "FIBERGLASS SCREEN MESH - 18x16"
- "30" KNEE WALL (FRAME + TRIM)"
- "CEILING: [MATERIAL] - SEE FINISH SCHEDULE"

---

## 6. GARAGE DETAILS

### 6.1 Garage Door Header

**Assembly (Section at Garage Door Opening):**

```
    Roof/floor structure above
    ___________________________________
   |  Cripple studs above header       |
   |___________________________________|
   |  Header: LVL, glulam, steel,     |  <-- Sized for span + load above
   |  or built-up lumber               |      16'-0" span = steel or eng. wood
   |  (sized per span table)           |
   |___________________________________|
   |  Jack studs (trimmers)           |  <-- Support header at each side
   |  King studs (full height)         |      Number per header load
   |___________________________________|
   |  Garage door opening              |
   |  (8'-0", 9'-0", 16'-0", 18'-0")  |  <-- Standard widths
   |___________________________________|
```

**Typical Header Sizing (Bearing Wall, Single Story Above):**

| Opening Width | Header Option |
|---------------|---------------|
| 8'-0" (single car) | (2) 2x12 with 1/2" plywood spacer |
| 9'-0" (single car) | (2) 2x12 or 3-1/2" x 9-1/4" LVL |
| 16'-0" (double car) | 3-1/2" x 11-7/8" LVL or 5-1/8" x 9" glulam or steel W-shape |
| 18'-0" (double car+) | Steel W8x or W10x (verify with engineer) |

**Note:** Headers > 12' typically require engineered lumber (LVL, PSL) or steel. Verify with structural engineer for specific loads.

**Revit Detail Components:**
- Dimension Lumber-Section (jack studs, king studs, cripples)
- Nominal Cut Lumber-Section (LVL or glulam header representation)
- Plywood-Section (plywood spacer in built-up header)

**Drawing Annotations:**
- "GARAGE DOOR HEADER: [TYPE AND SIZE]"
- "JACK STUDS AND KING STUDS EACH SIDE"
- "VERIFY HEADER SIZE W/ STRUCTURAL ENGINEER FOR SPANS > 12'"
- "SEE STRUCTURAL FOR POINT LOADS"

---

### 6.2 Garage Slab Detail

**Assembly (Section):**

```
    Finished garage floor (may be sealed/epoxy coated)
    ___________________________________________
   |  4" concrete slab (min 2500 psi)          |  <-- 4" min, 5" for heavy loads
   |  6x6 W1.4/W1.4 WWR or #3 @ 18" o.c.     |  <-- Reinforcement
   |  ________________________________________ |
   |  6-mil polyethylene vapor barrier          |  <-- Over compacted fill
   |  ________________________________________ |
   |  4" compacted gravel fill                  |  <-- Well-graded, compacted
   |  ________________________________________ |
   |  Undisturbed/compacted subgrade            |
   |___________________________________________|
```

**Critical Details:**
- Control joints: saw-cut 1/4 slab thickness, 10' max spacing (or 2-3x slab thickness in feet)
- Slope: 1/8" to 1/4" per foot toward garage door for drainage
- Isolation joint: at slab-to-wall and slab-to-foundation (1/2" expansion joint material)
- Thickened edge: 8"-12" thick at garage door opening (for apron support)

**Code Requirements:**
- IRC R506.1: Min 3-1/2" thick concrete slab
- IRC R506.2.1: Min 4" gravel/crushed stone base
- Vapor barrier per IRC R506.2.2 (6-mil poly min)
- Garage floor must be sloped toward vehicle entry or drain

**Drawing Annotations:**
- "4" CONC. SLAB ON 6-MIL POLY ON 4" COMP. GRAVEL"
- "6x6 W1.4/W1.4 WWR (OR #3 @ 18" E.W.)"
- "CONTROL JOINTS @ 10'-0" MAX O.C."
- "SLOPE 1/8" PER FT TOWARD GARAGE DOOR"
- "1/2" EXPANSION JOINT AT WALLS"

---

### 6.3 Garage-to-House Fire Separation

**Assembly (Section at Separation Wall):**

```
    House side               | Separation |  Garage side
    _________________________|    wall    |__________________________
   |  1/2" GWB (min)         |           |  1/2" GWB on garage side |
   |  or as required         | 2x4 studs |  (min per IRC R302.6)    |
   |  for house finish       | @ 16" o.c.|                          |
   |_________________________|___________|__________________________|

    IF HABITABLE ROOM ABOVE GARAGE:
    _________________________|    ceiling |__________________________
   |  Floor finish           |  assembly  |  5/8" Type X GWB         |
   |  Subfloor               |           |  (min per IRC R302.6)    |
   |  Floor joists           |           |  on garage ceiling       |
   |_________________________|___________|__________________________|
```

**IRC R302.6 Requirements:**

| Separation Condition | Minimum GWB | Notes |
|---------------------|-------------|-------|
| Wall between garage and dwelling | 1/2" GWB on garage side | Applied to garage side |
| Ceiling below habitable rooms | 5/8" Type X GWB on garage ceiling | Structure also protected |
| Structure supporting separation | 1/2" GWB or equivalent | Floor joists, etc. |

**Door Requirements (IRC R302.5.1):**
- NO door from garage directly into sleeping room
- Door between garage and dwelling:
  - Solid wood, min 1-3/8" thick, OR
  - Solid/honeycomb-core steel, min 1-3/8" thick, OR
  - 20-minute fire-rated door
- Door must be self-closing (IRC R302.5.1)
- Door sill: raised or sloped to prevent fuel spill entry

**Additional Requirements:**
- GWB joints: taped, finished, or sealed (gaps max 1/20")
- Ducts in garage: min 26-gauge steel, no openings into garage
- Penetrations: sealed with approved firestop material
- Attic access from garage: must maintain separation in attic space

**Revit Detail Components:**
- Gypsum Wallboard-Section (1/2" standard side)
- Gypsum Wallboard-Section1 (5/8" Type X at ceiling if habitable above)
- Dimension Lumber-Section (studs, joists)
- Caulking-Section (firestop at penetrations)
- Detail lines (door outline with self-closer annotation)

**Drawing Annotations:**
- "GARAGE/DWELLING SEPARATION - IRC R302.6"
- "1/2" GWB ON GARAGE SIDE OF WALL (MIN)"
- "5/8" TYPE X GWB ON GARAGE CEILING (IF HABITABLE ROOM ABOVE)"
- "DOOR: SOLID WOOD/STEEL 1-3/8" OR 20-MIN RATED"
- "SELF-CLOSING DEVICE ON DOOR"
- "TAPE AND FINISH ALL GWB JOINTS"
- "SEAL ALL PENETRATIONS W/ FIRESTOP"

**Common Mistakes:**
- Using regular 1/2" GWB at ceiling (must be 5/8" Type X when habitable rooms above)
- No self-closer on garage-to-house door
- Door opening directly into bedroom (code violation)
- Unsealed penetrations through separation wall (electrical, plumbing)
- Unfinished GWB joints (gaps allow fire/smoke passage)

---

## 7. EXTERIOR TRIM & TRANSITION DETAILS

### 7.1 Window and Door Trim Profiles

**Assembly (Section at Window Head):**

```
    Siding (laps over drip cap)
    ________________________________
   |  Z-flashing / drip cap         |  <-- Extends 1" above trim, drip over face
   |________________________________|
   |  Head casing (1x4 or 1x6)     |  <-- Flat casing, brick mold, or profiled
   |  (wood, PVC, or fiber cement)  |
   |________________________________|
   |  Window frame                   |
   |________________________________|
```

**Common Trim Profiles:**

| Profile | Dimension | Style | Notes |
|---------|-----------|-------|-------|
| Flat casing | 1x4 (3/4" x 3-1/2") | Modern/Craftsman | Most common residential |
| Flat casing wide | 1x6 (3/4" x 5-1/2") | Colonial/Craftsman | More substantial look |
| Brick mold | 2" x 1-1/4" profiled | Traditional | Standard with brick/stucco |
| Back band | 1-1/16" x 1-1/4" | Traditional | Added over flat casing for layered look |
| Crown/cap | 3/4" x 2" to 3" | Traditional | At head for drip cap function |

**Head Trim Detail:**
- Drip cap (Z-flashing) OVER top of head casing
- Siding laps OVER drip cap flashing
- 1/8" gap between trim and siding (do NOT caulk top horizontal joint)
- Caulk vertical joints of casing to window frame

**Sill Trim Detail:**
- Sill extends 1" past jamb casing each side
- Sill slopes 15 deg min outward for drainage
- Caulk joint between sill and window frame
- Do NOT caulk bottom of sill to wall (allow drainage)

**Materials:**
- Cellular PVC (AZEK, TimberTech): best for moisture resistance, no rot, paintable
- Finger-jointed primed wood: economical, paint required, rot-prone if paint fails
- Fiber cement: durable, requires painting, can crack if nailed too tight
- Clear cedar/redwood: traditional, expensive, weathers naturally or paint/stain

**Revit Detail Components:**
- Siding-Wood Bevel (siding)
- Nominal Cut Lumber-Section (trim boards)
- Caulking-Section (sealant joints)
- Detail lines (flashing, drip cap)
- Window Head/Sill/Jamb families (if wood window)

**Drawing Annotations:**
- "1x[WIDTH] [MATERIAL] TRIM CASING"
- "Z-FLASHING OVER HEAD CASING, UNDER SIDING"
- "CAULK VERTICAL JOINTS (NOT TOP HORIZONTAL)"
- "SILL: SLOPE 15 DEG MIN FOR DRAINAGE"
- "EXTEND SILL 1" PAST JAMB EACH SIDE"

---

### 7.2 Corner Boards

**Assembly (Plan Section at Building Corner):**

```
    ___________
   |  1x4     | 1x6    |
   |  corner  | corner  |
   |  board   | board   |
   |__________|_________|
   |  Siding  | Siding  |  <-- Siding butts against corner boards
   |  butts   | butts   |      (NOT mitered)
   |  here    | here    |
   |__________|_________|
   |  Housewrap          |
   |  Sheathing          |
   |_____________________|
```

**Key Notes:**
- Typically: 1x4 on one side, 1x6 on adjacent side (creates visual symmetry at corner)
- Alternative: two 1x4 boards (one butts to face of other)
- Material: PVC, finger-joint primed, cedar
- Install over housewrap, under siding
- Caulk siding-to-corner board joint, NOT corner board-to-corner board

**Drawing Annotations:**
- "1x4 & 1x6 CORNER BOARDS - [MATERIAL]"
- "INSTALL OVER WRB, UNDER SIDING"
- "BUTT SIDING TO CORNER BOARD - CAULK JOINT"

---

### 7.3 Frieze Board at Eave

**Assembly (Section at Eave):**

```
    Rafter tail / soffit
    ___________________________
   |  Fascia (1x6 or 1x8)      |
   |  Soffit (3/8" ply or vinyl)|
   |___________________________|
   |  Frieze board (1x8 or      |  <-- Trim board at wall-to-soffit junction
   |  1x10, flat or profiled)   |      Provides clean termination for siding
   |___________________________|
   |  Siding terminates below   |  <-- Top course of siding butts to frieze
   |  frieze board               |
   |___________________________|
```

**Key Notes:**
- Frieze board: transition between siding and soffit
- Ventilation: do NOT seal frieze board to soffit if using vented soffit
- Material: match siding material (PVC with PVC siding, etc.)

**Drawing Annotations:**
- "1x[WIDTH] FRIEZE BOARD"
- "TERMINATE SIDING AT BOTTOM OF FRIEZE"
- "DO NOT BLOCK SOFFIT VENTILATION"

---

### 7.4 Water Table / Belly Band

**Assembly (Section at Water Table):**

```
    Siding above
    ________________________________
   |  1/8" gap (no caulk at top)    |  <-- Allow drainage behind siding
   |________________________________|
   |  Z-flashing / drip cap         |  <-- Over top of water table
   |________________________________|
   |  Water table (1x8 to 1x12)     |  <-- Typically wider board
   |  (may have cap molding on top) |      Set at bottom of first floor
   |________________________________|      or at foundation-to-wall transition
   |  Siding below (or foundation)  |
   |________________________________|
```

**Purpose:**
- Protects lowest section of wall from splash-back (rain hitting ground)
- Historically called "splash board" or "mud board"
- Visual transition between foundation and siding
- Width: 8"-12" typical (1x8 to 1x12)
- Material: PVC (best for ground-level moisture), PT wood, fiber cement

**Drawing Annotations:**
- "WATER TABLE: 1x[WIDTH] [MATERIAL]"
- "Z-FLASHING OVER TOP, UNDER SIDING ABOVE"
- "1/8" GAP AT TOP - DO NOT CAULK"
- "SET AT FOUNDATION-TO-WALL TRANSITION"

---

### 7.5 Siding-to-Stone/Brick Transition

**Assembly (Section at Material Transition):**

```
    Siding (upper portion)
    ________________________________
   |  Housewrap / WRB               |  <-- WRB continuous behind both materials
   |  Sheathing                      |
   |________________________________|
   |  Z-flashing at transition       |  <-- Directs water over stone below
   |  (extends over stone ledge)     |      Laps under WRB above
   |________________________________|
   |  Stone/brick veneer             |  <-- 1" air gap behind veneer
   |  (thin veneer 3/4"-1-1/2"      |      Weep screed at bottom
   |   or full brick 3-5/8")        |
   |________________________________|
   |  Scratch coat + mortar          |  <-- Thin veneer: adhered to scratch coat
   |  (adhered veneer)               |      Full brick: mortar bed, brick ties
   |________________________________|
   |  Metal lath or brick ties       |
   |  WRB / Sheathing               |
   |________________________________|
```

**Critical Flashing Requirements:**
- WRB MUST be continuous behind both materials
- Z-flashing at horizontal transition: lap under WRB above, drip over stone below
- Weep screed at bottom of stone/brick (allows moisture to exit)
- 1" air gap behind full brick veneer (not required for thin adhered veneer < 3/4")
- Through-wall flashing at base of brick (above foundation shelf/angle)

**Adhered Veneer (Thin Stone):**
- Max weight: 15 psf (IRC R703.6.1)
- Substrate: metal lath over WRB and sheathing
- Scratch coat: 3/8"-1/2" Portland cement mortar
- Veneer stones set in mortar bed, grouted

**Anchored Veneer (Full Brick):**
- IRC R703.7: Brick ties to studs at 32" o.c. max vertically, 24" max horizontally
- 1" air space behind brick
- Through-wall flashing with weep holes at 24" o.c. (or open head joints)
- Lintel at openings (steel angle)

**Revit Detail Components:**
- Siding-Wood Bevel (siding portion)
- Gypsum Wallboard-Section (interior finish)
- Dimension Lumber-Section (studs)
- Plywood-Section (sheathing)
- Detail lines (flashing, WRB, brick ties, weep screed)
- Filled region with brick or stone hatch (veneer)

**Drawing Annotations:**
- "SIDING-TO-STONE TRANSITION"
- "Z-FLASHING: LAP UNDER WRB ABOVE, DRIP OVER STONE BELOW"
- "WRB CONTINUOUS BEHIND BOTH MATERIALS"
- "WEEP SCREED AT BOTTOM OF STONE VENEER"
- "THIN VENEER: MAX 15 PSF (IRC R703.6.1)"
- "FULL BRICK: 1" AIR SPACE, BRICK TIES @ 32" V x 24" H"

**Common Mistakes:**
- No flashing at horizontal transition (water infiltration guaranteed)
- WRB not continuous (gap between siding and stone zones)
- No weep screed at bottom of veneer (trapped moisture = efflorescence, freeze damage)
- Siding lapping over stone without flashing (directs water behind stone)
- Sealing bottom of stone veneer (traps moisture)

---

## CROSS-REFERENCE TABLE

| Detail | Related Details | Key Code Section |
|--------|----------------|-----------------|
| 1.1 Base Cabinet | 1.4 Countertop Edge, 1.5 Backsplash | NKBA guidelines |
| 1.3 Kitchen Island | 1.1 Base Cabinet, 1.4 Countertop Edge | NEC 210.52(C), NKBA |
| 1.6 Range Hood | 1.2 Upper Cabinet | IRC M1503 |
| 2.1 Tub Surround | 2.2 Shower Niche, 2.3 Vanity | IRC R702.4, TCNA |
| 2.2 Shower Niche | 2.1 Tub Surround | TCNA B421, ANSI A118.10 |
| 2.4 Heated Floor | 2.1 Tub Surround | NEC 424.44 |
| 3.1 Closed Stringer | 3.3 Landing, 3.4 Newel Post, 3.5 Handrail | IRC R311.7 |
| 3.2 Open Stringer | 3.4 Newel Post | IRC R311.7 |
| 3.4 Newel Post | 3.1/3.2 Stringers, 3.5 Handrail | IRC R301.5, R312 |
| 4.1 Masonry Fireplace | 4.4 Chimney/Cricket, 4.5 Hearth/Mantel | IRC Chapter 10 |
| 4.2 Zero-Clearance | 4.4 Chimney/Cricket | UL 127, UL 103 |
| 4.3 Direct-Vent Gas | None (self-contained) | IRC G2427 |
| 5.1 Deck Ledger | 5.3 Beam/Joist, 5.4 Railing Post | IRC R507.6 |
| 5.2 Post-to-Footing | 5.3 Beam/Joist | IRC R507.8, R403.1 |
| 5.4 Railing Post | 5.1 Deck Ledger | IRC R507.10, R312 |
| 6.3 Garage Separation | 6.1 Header, 6.2 Slab | IRC R302.5, R302.6 |
| 7.1 Window Trim | 7.2 Corner Boards, 7.4 Water Table | IRC R703 |
| 7.5 Siding-to-Stone | 7.1 Window Trim, 7.4 Water Table | IRC R703.6, R703.7 |

---

## APPENDIX: IRC QUICK CODE REFERENCE FOR RESIDENTIAL DETAILS

| Topic | IRC Section | Key Requirement |
|-------|-------------|-----------------|
| Stair dimensions | R311.7 | 7-3/4" max rise, 10" min run, 36" min width |
| Stair headroom | R311.7.2 | 6'-8" min |
| Handrail height | R311.7.8.1 | 34" min, 38" max |
| Handrail graspability | R311.7.8.3 | 1-1/4" to 2" circular |
| Guard height | R312.1.1 | 36" min (residential) |
| Baluster spacing | R312.1.3 | 4" max (sphere test) |
| Guard load | R301.5 | 200 lb concentrated |
| Garage separation wall | R302.6 | 1/2" GWB on garage side |
| Garage ceiling (hab. above) | R302.6 | 5/8" Type X GWB |
| Garage door to dwelling | R302.5.1 | Solid 1-3/8" or 20-min rated, self-closing |
| Fireplace firebox depth | R1001.6 | 20" min |
| Fireplace hearth (front) | R1001.9 | 16" min (20" if > 6 sq ft opening) |
| Mantel clearance | R1001.11 | Varies by projection (6"-12") |
| Chimney cricket | R1003.20 | Required when width > 30" parallel to ridge |
| Chimney height | R1003.9 | 3' above roof, 2' above anything within 10' |
| Clearance to combustibles | R1003.18 | 2" min |
| Deck ledger attachment | R507.6 | 1/2" bolts per Table R507.6 |
| Deck footing depth | R403.1 | Below frost line |
| Deck guard post | R507.10 | 4x4 min, no notching, 1800 lb tension device |
| Wet area backer | R702.4 | Cement, fiber-cement, or glass mat board |
| Concrete slab | R506.1 | Min 3-1/2" thick |
| Slab vapor barrier | R506.2.2 | 6-mil poly min |
| Kitchen exhaust | M1503 | 100 CFM intermittent or 25 CFM continuous |
| Makeup air | M1503.6 | Required for hoods > 400 CFM |
| Exterior veneer (adhered) | R703.6.1 | Max 15 psf |
| Exterior veneer (anchored) | R703.7 | Brick ties, 1" air space, weep holes |
