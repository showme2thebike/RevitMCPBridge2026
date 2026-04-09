# CAD Visual Rules — Revit Construction Documents
**BIM Monkey · Barrett Eastwood AIA · Reference v1.0**
*Authored by Eric Chrasta with Claude. Controls weight, pattern, tone, scale, and annotation — the layer that separates professional permit sets from model dumps.*

---

## 1  THE VISUAL HIERARCHY PRINCIPLE

Every element communicates its depth relative to the cut plane:

> **Things you cut through are heaviest. Things behind the cut are medium. Things in front are lightest. Annotation is always thinner than model geometry.**

Revit's Object Styles system encodes this through Cut vs. Projection line weight assignments per element category.

### 1.1  The Five Weight Levels

| Level | Name | Approx. mm | Used For | Revit Pen # |
|---|---|---|---|---|
| 1 | Profile | 0.50–0.70 | Drawing border, ground line, cut wall outline | 8–10 |
| 2 | Heavy / Cut | 0.35–0.50 | Cut walls, cut structural elements, section poche boundary | 6–7 |
| 3 | Medium | 0.25–0.35 | Doors, windows, stairs, casework (projection at plan scale) | 4–5 |
| 4 | Light | 0.13–0.18 | Furniture, equipment, overhead lines, room name tags | 2–3 |
| 5 | Hairline / Annotation | 0.09–0.13 | Dimension lines, extension lines, grid bubbles, hatch lines | 1 |

> Revit ships with 16 pen numbers. Pens 1 and 2 are reserved for fill/hatch patterns (pen 1 = filled regions, pen 2 = ceiling patterns). Start regular object assignments at pen 3.

### 1.2  Cut vs. Projection Settings

| Category | Cut Pen | Projection Pen |
|---|---|---|
| Walls | 7 (0.50mm) | 3 (0.18mm) |
| Floors | 7 | 2 (below cut — almost invisible) |
| Structural columns | 8 | 4 |
| Doors/Windows | 5 | 4 (swing/sill) |
| Furniture | 3 | 2 |

In a floor plan, walls should read 3–4× heavier than furniture next to them.

---

## 2  LINEWEIGHT BY VIEW TYPE

### 2.1  Floor Plans (1/8"–1/4" = 1'-0")

| Element | Cut Pen | Proj. Pen | Notes |
|---|---|---|---|
| Exterior walls | 7 | 3 | Heaviest line in the plan |
| Interior partition walls | 6 | 3 | |
| Structural columns | 8 | 4 | Poche filled |
| Doors (swing + frame) | 5 | 4 | Frame cut, swing projected |
| Windows (frame + sill) | 5 | 3 | Sill projected |
| Stairs (treads) | 4 | 3 | Dashed above cut = pen 2 |
| Casework / counters | 4 | 3 | |
| Furniture / equipment | 3 | 2 | Always lightest |
| Dimension strings | — | 1 | Annotation hairline |
| Room tags / text | — | 1 | Annotation hairline |
| Grid lines | — | 2 | Slightly heavier than dims |

### 2.2  Reflected Ceiling Plans (1/8"–1/4" = 1'-0")

| Element | Pen | Notes |
|---|---|---|
| Walls (cut plane) | 6–7 | Same as floor plan |
| Ceiling plane / soffits | 5 | Defines ceiling field |
| Ceiling grid (T-bar) | 3 | Must not dominate |
| Light fixture symbols | 3 | |
| HVAC diffusers / grilles | 3 | |
| Ceiling height annotations | 1 | Hairline |
| Tray / cove profiles | 4 | Slightly heavier — reads as 3D |

### 2.3  Elevations (1/8"–3/16" = 1'-0")

| Element | Pen | Notes |
|---|---|---|
| Grade line (ground) | 8 | **HEAVIEST** — profiles the building; most missed rule |
| Building silhouette | 7 | Outer profile |
| Window/door frames | 5 | |
| Siding / material joints | 3 | Repetitive — light |
| Roof planes (visible) | 4 | |
| Hidden / beyond | 2 dashed | Very light dashed |
| Annotations | 1 | Hairline |

> The grade/ground line is **always** the heaviest line in an elevation — heavier than the wall above it.

### 2.4  Sections (1/8"–1/4" = 1'-0")

- Cut elements at cut plane: pen 7–8 with solid poche fill
- Projected elements (cabinetry, stairs visible beyond cut): pen 3–4
- Void / negative space: pen 2 light or white
- Earth below grade: solid dark fill (K100) or heavy hatch pen 7
- Adjacent cut elements that touch (wall-to-floor) must share the same Cut pen — prevents visible seam

### 2.5  Details (3/4"–3" = 1'-0")

| Element | Pen | Notes |
|---|---|---|
| Outline of entire assembly | 7–8 | Profile line |
| Structural framing | 6 | |
| Sheathing / subfloor planes | 5 | |
| Insulation batt symbol | 3 | Light — repeating |
| Air barrier / membrane | 4 | |
| Finish layers (GWB, tile) | 4 | |
| Fastener / connector symbols | 3 | |
| Dimension strings | 1 | Hairline |
| Material label leaders | 1 | Hairline |
| Keynote hexagons | 2 | Slightly heavier than leaders |

---

## 3  FILL PATTERNS AND POCHE

### 3.1  Drafting vs. Model Patterns

| Type | Behaviour | Use For |
|---|---|---|
| Drafting pattern | Fixed angle/size regardless of scale | Cut section material symbols (concrete hatch, insulation, wood grain) |
| Model pattern | Scales with model — true size | Surface materials in projection: brick coursing, tile grid, metal panels |

> **Never use Model patterns in cut views** — always use Drafting patterns for cut material symbols.

### 3.2  Standard Cut Patterns by Material

| Material | Pattern | Pen | Notes |
|---|---|---|---|
| Concrete / CMU | Diagonal crosshatch 45°/135° | 1 | Dense — thin pen prevents muddy print |
| Wood (lumber) | Diagonal grain 45° | 1 | Single diagonal only |
| Wood (sheet goods) | X crosshatch fine | 1 | Plywood, OSB |
| Batt insulation | Zigzag / wave symbol | 2 | Must read clearly |
| Rigid insulation | Diagonal grid with dots | 1 | Or solid light grey |
| Steel (structural) | Solid black (poche) | Solid fill | Always solid |
| Masonry brick | Running bond hatch | 1 | Detail scale only |
| Gypsum wallboard | Diagonal fine 45° | 1 | Very fine — GWB is thin |
| Earth / soil | Diagonal + dot random | 2 | Below grade in foundations |
| Void beyond | No fill / white | — | Rooms beyond section cut |

### 3.3  Poche Rules

- **Structural mass cut by section plane = solid black** (pen 16 or Revit solid fill)
- Concrete walls, CMU, solid masonry, concrete slabs: solid poche
- Wood stud walls: poche only plates and blocking, not the void cavity
- Steel members: always solid poche regardless of scale
- Earth: solid dark fill or heavy diagonal hatch to distinguish from structure

### 3.4  Halftone

| Scenario | What to Halftone |
|---|---|
| RCP — floor plan underlay | Floor plan elements |
| Renovation — existing work | Existing phase elements |
| MEP plans | Architectural elements (linked model) |
| Structural plans | Arch finishes / partitions |
| Enlarged plan vs. context | Elements outside crop boundary |

> Halftone values compound when stacked (phase filter + linked model + discipline underlay). Test-plot before issuing.

---

## 4  SCALE CONVENTIONS

### 4.1  Standard Scales by View Type

| View Type | Standard Scale(s) | Notes |
|---|---|---|
| Site plan | 1"=20'–40' or 1/16"=1'-0" | Scale to fit lot — north up |
| Floor plans (small) | 1/4"=1'-0" (1:48) | Preferred for most residential |
| Floor plans (large) | 3/16" or 1/8"=1'-0" | > ~4,000 SF on ARCH D |
| RCP | Same as matching floor plan | Must match exactly |
| Roof plan | 1/8"–1/4"=1'-0" | Matches floor plan scale |
| Exterior elevations | 1/8"–3/16"=1'-0" | All four at same scale |
| Building sections | 1/8"–1/4"=1'-0" | Same as plans preferred |
| Wall sections | 3/4"–1"=1'-0" | Show every layer |
| Enlarged plans | 1/2"–1"=1'-0" | Kitchen, baths, stairs |
| Details (typical exterior) | 1"–1.5"=1'-0" | Min to show flashing/fastening |
| Details (complex) | 1.5"–3"=1'-0" | Stair nosing, window jamb |
| Structural details | 1"–3"=1'-0" | Connection details |
| Schedules | N/A | Fit to sheet, min 1/8" text |

Scale denominator reference: 48=1/4", 64=3/16", 96=1/8", 192=1/16", 16=3/4", 12=1", 8=1.5", 4=3"

### 4.2  Detail Level vs. Scale

| Detail Level | Typical Scale | Shows | Hides |
|---|---|---|---|
| Coarse | 1/16"–1/8" | Overall form, poche | Door swings, sills, framing |
| Medium | 3/16"–1/4" | Door swings, sills, casework | Framing members, hardware |
| Fine | 1/2" and larger | All geometry incl. framing, hardware | — |

> Plans at 1/4"=1'-0" using Coarse detail level show only walls — no door swings, no casework. Most common cause of empty-looking Revit plans.

---

## 5  LINE PATTERN STANDARDS

| Pattern | NCS Name | Meaning / Use |
|---|---|---|
| Solid | Continuous | Visible edges, cut lines, dimension strings — default |
| Long dash | Hidden | Elements beyond cut plane (beams above, overhead cabinets) |
| Short dash | Dashed | Concealed or below-grade, future work |
| Dash-dot | Center | Centerlines: column grid, wall centerlines, plumbing |
| Long-short-long | Phantom | Property lines, match lines, beyond-scope work |
| Dotted | Dotted | Insulation symbol, adjacent construction not in scope |
| Dash-2dot | Divide | Easements, zoning setbacks |

### Overhead Elements in Floor Plans (Critical)

Elements above the cut plane (4'-0" AFF) shown as dashed:
- Kitchen upper cabinets — long-dash, medium pen
- Skylight opening — long-dash, medium pen + label
- Structural beams/headers — long-dash, heavy pen
- Stair flight above — long-dash, light pen + diagonal arrow + 'UP' label
- Ceiling soffits — long-dash, light pen (coordinate with RCP)

> **Most common error:** upper cabinets shown as solid lines. They must be dashed.

---

## 6  ANNOTATION STANDARDS

### 6.1  Text Sizes on Paper (plotted inches)

| Text Type | Plotted Height | Used For |
|---|---|---|
| Sheet title | 3/16"–1/4" | Title block sheet name |
| View title | 3/16" | View name below each drawing |
| Scale notation | 3/32"–1/8" | Below view title |
| Keynote text | 3/32" | Body notes (standard) |
| Dimension text | 3/32" | Dimension numbers |
| Room labels | 3/32"–1/8" | Room name in plan |
| Material callouts | 3/32" | Leaders + material names |
| Detail reference tags | 5/64"–3/32" | Inside section/detail markers |
| Title block info | 1/8"–3/16" | Project name, address, date |

> **3/32" is the minimum legible text size** on a plotted CD. Never go below — illegible on half-size check sets.

### 6.2  Reference Marker Anatomy (NCS)

| Marker | Symbol | Contents |
|---|---|---|
| Section | Circle + half arrow | Top: section number / Bottom: sheet number (A3.1) |
| Elevation | Circle + directional arrow | Top: elevation letter / Bottom: sheet number |
| Detail callout | Circle or hexagon | Top: detail number / Bottom: sheet where detail lives |
| Keynote | Hexagon (NCS mandated) | Number keyed to sheet keynote list |
| Revision delta | Triangle or circle | Revision number — cloud + delta triangle |
| Match line | Phantom line + label box | Sheet continuation reference |

### 6.3  Dimension String Hierarchy

String from largest to smallest:

| Level | What it Dims | Example |
|---|---|---|
| 1st (outermost) | Overall building | 42'-6" |
| 2nd | Major grid / structural bays | 12'-0" \| 14'-0" \| 16'-6" |
| 3rd | Openings and partitions | Door 3'-0" \| partition 6'-3" \| window 4'-0" |
| 4th (innermost) | Fine offsets, reveals, sills | Sill height 2'-6", reveal 1/4" |

Rules:
- Dimension to face of framing (structural) or face of finish — note which on plans
- Never dimension to wall centerline unless it is a structural/grid line
- Minimum 3/8" gap between dimension line and building line
- Extension lines stop 1/16" from the object
- All text reads horizontally or 90° CCW for vertical strings — never diagonal

### 6.4  Keynote System

- Each sheet carries its own keynote list in the right margin
- Symbol: **hexagon** (NCS — mandatory)
- Format: sequential per sheet (1, 2, 3…)
- Text: short, ≤8 words, reference spec section
- Sheet-based numbering (restart at 1 each sheet) preferred over global CSI keynotes for residential practice

---

## 7  VIEW TEMPLATES

All visual rules are enforced through View Templates in Revit.

### 7.1  What a View Template Controls

| Parameter | Controls |
|---|---|
| View Scale | Locks scale — prevents non-standard scales |
| Detail Level | Coarse/Medium/Fine — must match scale |
| Visibility/Graphics | Category visibility; cut/projection overrides; halftone |
| Display Style | Hidden Line (CD standard) vs. Wireframe vs. Shaded |
| Phase Filter | Which phases shown/hidden/halftoned |
| Discipline | Arch / Structural / Mechanical — affects visibility |
| View Range | Cut plane height (default 4'-0" AFF) and projection depth |
| Underlay | Grey context plan for RCPs or upper-floor plans |
| Crop/Annotation Crop | Crop region and annotation boundary |

### 7.2  Minimum View Template Set for Residential CDs

| Template Name | Scale | Notes |
|---|---|---|
| A-PLAN-1/4 | 1/4"=1' | Architectural floor plan |
| A-PLAN-1/8 | 1/8"=1' | Large buildings |
| A-RCP-1/4 | 1/4"=1' | Furniture hidden, ceiling categories on |
| A-ELEV-3/16 | 3/16"=1' | Exterior elevation |
| A-SECT-1/4 | 1/4"=1' | Building section — poche on structural |
| A-WSECT-3/4 | 3/4"=1' | Wall section — Fine detail, all layers |
| A-ENLAR-1/2 | 1/2"=1' | Enlarged plan |
| A-DETAIL-1 | 1"=1' | Architectural detail |
| A-DETAIL-3 | 3"=1' | Complex detail |
| A-EXIST | — | Existing conditions — halftone phase filter |
| A-DEMO | — | Demo plan — existing halftone + new |
| S-PLAN-1/4 | 1/4"=1' | Structural plan — arch halftoned |

### 7.3  View Range for Floor Plans

| Parameter | Standard Value | Notes |
|---|---|---|
| Top of View Range | 8'-0" AFF (or plate height) | Ceiling above this is cut |
| Cut Plane | 4'-0" AFF | Standard horizontal cut |
| Bottom of View Range | 0'-0" / floor level | |
| View Depth | -1'-0" (below floor) | Shows foundation/slab edge |

> Split-level homes require custom View Range per level — 4'-0" default cuts the wrong floor.

---

## 8  RENOVATION / PHASE GRAPHICS

### 8.1  Phase Filter Standard

| Plan Type | Existing Elements | Demo Elements | New Elements |
|---|---|---|---|
| Existing Plan | Full weight, black | Shown normal | Not shown |
| Demo Plan | 50% halftone grey | Bold + hatched or red | Not shown |
| New Work Plan | 50% halftone grey | Removed / not shown | Full weight, black |

Key rule: on New Work plan, existing work recedes to grey; new work punches forward at full weight. A contractor must tell at a glance what is new.

Demo items: hatch fill or red (colour print) or bold X-pattern. Mark with DEMO keynote.

---

## 9  TITLE BLOCK

### 9.1  Required Zones

| Zone | Location | Contents |
|---|---|---|
| Firm information | Upper right column | Name, address, phone, license #, logo |
| Project information | Middle right column | Project name, address, owner name |
| Issue block | Right column | Issue dates, revision dates, submission type |
| Seal block | Right column | Architect's seal + signature space |
| Sheet identification | Lower right corner | Sheet number (large, bold), sheet title |
| Scale / north arrow | Lower margin | Drawing scale(s), north arrow for plans |
| Revision column | Right margin | Revision number, date, description (cloud+delta) |

### 9.2  Sheet Number Format (NCS)

`[Discipline][SheetType].[Sequence]`

- G0.1 = General, Cover Sheet
- A1.2 = Architectural, Plans, Sheet 2 (Level 2 Floor Plan)
- A5.3 = Architectural, Details, Sheet 3
- A6.1 = Architectural, Schedules, Sheet 1
- S1.1 = Structural, Plans, Sheet 1

Sheet number minimum plotted height: **1/4"**, lower-right corner.

---

## 10  VIEW VALIDATION IMPLICATIONS FOR BIMMONKEY

### 10.1  Properties Available from Revit API

| Property | What It Tells Us | Validation Rule |
|---|---|---|
| viewType | FloorPlan / CeilingPlan / Section / Elevation / Detail / Schedule | Primary routing signal — in viewClassifier.js |
| scale | View scale denominator (48 = 1/4"=1') | Flag wrong scale for sheet type |
| detailLevel | Coarse / Medium / Fine | Plans at 1/4" must be Medium or Fine |
| cropWidthFt / cropHeightFt | Crop box in feet | Fill calculation; flag absurdly large views on detail sheets |
| viewTemplate | Assigned View Template name | **Highest-confidence signal** — encode Barrett's intent |
| discipline | Architectural / Structural / Mechanical | MEP views must not land on A-series sheets |
| phase | Phase name | Confirm renovation trio has correct phases per level |
| phaseFilter | Phase Filter name | Verify correct filter per condition |

### 10.2  Scale Validation Rules

| Sheet Slot | Scale Denominator | Acceptable? | Action |
|---|---|---|---|
| A1 (floor plan) | 48 or 64 (1/4"–3/16") | Definite | Route to A1 |
| A1 (floor plan) | 96 (1/8") | Probable | Route to A1, flag if building is small |
| A4 (enlarged) | 24 or 12 (1/2"–1") | Definite | Route to A4 |
| A5 (detail) | 12–4 (1"–3") | Definite | Route to A5 |
| A5 (detail) | 48 (1/4") | Suspicious | Flag as probable A4 or misclassified |
| A2 (elevation) | 96 or 64 | Definite | Standard elevation scale |
| A0 (site plan) | 240+ (1"=20' or more) | Definite | Route to A0 |
| A3 (wall section) | 16 or 12 (3/4"–1") | Definite | Route to A3 |

### 10.3  View Template Name Signals (Highest Confidence)

| Template Name Contains | Signal |
|---|---|
| 'PLAN' + '1/4' | A1 slot, scale 1/4" |
| 'RCP' | A1 slot, planSubType=rcp |
| 'ELEV' | A2 slot |
| 'SECT' or 'WALL SECT' | A3 slot |
| 'DETAIL' | A5 slot |
| 'EXIST' | renovationCondition = existing |
| 'DEMO' | renovationCondition = demo |

> `viewTemplate` is one of the **highest-confidence** classification signals — it encodes the human decision Barrett already made when setting up views. Prioritise it over name-based heuristics.
