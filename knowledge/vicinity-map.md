# Vicinity Map — Generation & Placement

## What it is
A black-on-white architectural drafting-style street map centered on the project
site, showing surrounding streets at roughly a 900-meter radius. Matches firm
standard: double-line roads, rotated uppercase street labels, bold asterisk site
marker, minimal circle+crosshair north arrow.

---

## How to generate

Run the generator script from a terminal (Claude Code or PowerShell):

```bash
python "C:\Users\<user>\Documents\BIM Monkey\wrapper\generate_vicinity_map.py" \
    "9714 14th Ave NW, Seattle WA" \
    "C:\Users\<user>\Documents\BIM Monkey\vicinity_map.png"
```

Or with a custom radius (default is 900 m — about a half-mile):
```bash
python "...\generate_vicinity_map.py" "ADDRESS" "OUTPUT.png" --dist 1200
```

**Address format**: full street address including city and state. The geocoder
(Nominatim / OSM) handles most US addresses correctly. If geocoding fails,
try adding zip code or being more specific.

**Output**: PNG at 150 DPI, ~1425 × 1620 px (portrait), pure black on white.

---

## Banana Chat workflow

When the user asks to create a vicinity map:

1. **Get the address** — from project notes, CLAUDE.md, or ask the user
2. **Set output path** — `C:\Users\<user>\Documents\BIM Monkey\vicinity_map.png`
3. **Run the script** — takes 10–30 seconds (downloads OSM data on first run)
4. **Import into Revit** — use `importImage` MCP method or place as a
   raster image in a drafting view

### Example Banana Chat invocation
```
Run this command and wait for it to finish:
python "C:\Users\echra\Documents\BIM Monkey\wrapper\generate_vicinity_map.py" "9714 14th Ave NW, Seattle WA 98117" "C:\Users\echra\Documents\BIM Monkey\vicinity_map.png"
```

---

## Style parameters (matching firm standard)

| Element | Specification |
|---------|---------------|
| Background | Pure white |
| Road rendering | Two-pass: wide white fill → narrow black outline (parallel line effect) |
| Primary/arterial | White 12px, black 2.0px |
| Secondary | White 10px, black 1.8px |
| Residential | White 6px, black 1.2px |
| Footways/paths | Single black line, 0.5–0.6px |
| Street labels | Arial 6.5pt, uppercase, rotated to road bearing, one per named street |
| Site marker | Bold `*` asterisk + `SITE` text, 7.5pt, to the right |
| North arrow | Circle + crosshair + filled north triangle, top-right corner |
| Output size | 9.5" × 10.8" at 150 DPI |

---

## Road type hierarchy (draw order, bottom → top)

Footways → Service → Residential → Tertiary → Secondary → Primary → Trunk → Motorway

Major roads draw on top of minor roads; all white passes run before all black passes
so casing never bleeds through the interior fill.

---

## Dependencies

Auto-installed on first run if missing:
- `osmnx` — OSM data fetching and network graph
- `geopandas` — spatial dataframes
- `matplotlib` — rendering
- `shapely` — geometry operations

OSM data is cached locally after the first download — subsequent runs for the
same area are fast.

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Geocoding failed | Add city + state + zip; try alternate address format |
| No streets render | Increase `--dist`; check address is correct |
| Street label missing | OSM name tag absent — normal for some minor streets |
| Script not found | Check installer ran; file at `Documents\BIM Monkey\wrapper\` |
| osmnx install fails | Run `pip install osmnx` manually in an admin terminal |

---

## Comparison with firm standard example

The style is calibrated to match the example map (Seattle area, ~900m radius):
- NW grid with numbered avenues (9th–15th Ave NW) and numbered streets (90th–100th)
- Diagonal street (Holman Rd NW) rendered with label rotated to match bearing
- Site marker at project address with asterisk + SITE text
- Irregular streets in park/natural areas rendered as thin single lines
- No building footprints, no color, no fills — pure line drawing

*Reference: generate_vicinity_map.py in Documents\BIM Monkey\wrapper\*
*OSM data © OpenStreetMap contributors — accurate for most US addresses*
