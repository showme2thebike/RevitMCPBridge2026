# Vicinity Map — Generation & Placement

## What it is
A black-on-white architectural drafting-style street map centered on the project
site, showing surrounding streets at roughly a 900-meter radius. Matches firm
standard: double-line roads, rotated uppercase street labels, bold asterisk site
marker, minimal circle+crosshair north arrow.

---

## Banana Chat workflow (use runScript — do NOT tell the user to run commands manually)

When the user asks to create a vicinity map:

1. **Get the address** — from project notes or ask the user. Include city and state.
2. **Call runScript** with the parameters below — do not ask the user to open a terminal.
3. **Check the result** — stdout will say "Saved → ..." when done. The PNG lands in the user's `Documents\BIM Monkey\` folder.
4. **Import into Revit** — use `importImage` with the output path returned in `outputDir`.

### MCP call to generate the map

```json
{
  "method": "runScript",
  "parameters": {
    "scriptName": "generate_vicinity_map.py",
    "args": "\"FULL ADDRESS HERE\" \"vicinity_map.png\"",
    "timeoutSeconds": 120
  }
}
```

Replace `FULL ADDRESS HERE` with the project address (e.g. `9714 14th Ave NW, Seattle WA 98117`).

The output file is always written to `Documents\BIM Monkey\vicinity_map.png` (the `outputDir` field in the response confirms the exact path for the current user).

### Optional: custom radius

```json
"args": "\"ADDRESS\" \"vicinity_map.png\" --dist 1200"
```

Default radius is 900 m. Use 1200 for a wider area.

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

## Script location (installer-managed — do not hardcode paths)

The installer places `generate_vicinity_map.py` at:

```
%USERPROFILE%\Documents\BIM Monkey\wrapper\generate_vicinity_map.py
```

The `runScript` method resolves this path automatically for whichever user is running Revit.
Output goes to `%USERPROFILE%\Documents\BIM Monkey\vicinity_map.png`.

Never hardcode `C:\Users\<anyone>\...` in commands. Always use `runScript` so the bridge
resolves paths from the current user's environment.

---

## Dependencies

Auto-installed by the script on first run if missing:
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
| `success: false` + script not found | Reinstall BIM Monkey; the wrapper folder must exist at `Documents\BIM Monkey\wrapper\` |
| Geocoding failed | Add city + state + zip; try alternate address format |
| No streets render | Increase `--dist`; check address is correct |
| Street label missing | OSM name tag absent — normal for some minor streets |
| osmnx install fails | User runs `pip install osmnx` manually in an admin terminal |
| `python` not found | Python is not on the system PATH; user installs Python 3.x |

---

## Comparison with firm standard example

The style is calibrated to match the example map (Seattle area, ~900m radius):
- NW grid with numbered avenues (9th–15th Ave NW) and numbered streets (90th–100th)
- Diagonal street (Holman Rd NW) rendered with label rotated to match bearing
- Site marker at project address with asterisk + SITE text
- Irregular streets in park/natural areas rendered as thin single lines
- No building footprints, no color, no fills — pure line drawing

*OSM data © OpenStreetMap contributors — accurate for most US addresses*
