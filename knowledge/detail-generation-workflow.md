# Detail Generation Workflow

## Overview
AI-driven 2D detail creation in Revit drafting views using the MCP bridge.

## MCP Methods (Detail Generation)

| Method | Purpose | Phase |
|--------|---------|-------|
| `getTypeAssembly` | Read wall/floor/roof compound structure layers | Input |
| `drawLayerStack` | Auto-draw assembly layers (lines + hatches + labels) | Drawing |
| `batchDrawDetail` | Execute multiple draw ops in one transaction | Drawing |
| `importSvgToDetail` | Convert SVG markup to Revit detail elements | Drawing |
| `captureViewportToBase64` | Capture view image for AI self-review | Review |
| `exportViewImage` | Export view to disk for review | Review |

## Workflow Steps

### Step 1: Understand What to Draw
- Read the appropriate template from `knowledge/details/templates/`
- If a specific wall/floor/roof type exists in the model, call `getTypeAssembly(typeId)` to get actual layers
- Merge template knowledge (annotations, code requirements, connections) with actual model data

### Step 2: Create the Drafting View
- Use existing `createView` with type "Drafting" or work in an existing drafting view
- Set appropriate scale (1.5" = 1'-0" for wall sections, 3" = 1'-0" for window details)

### Step 3: Draw the Geometry

**For assembly sections (wall, floor, roof):**
```json
{
  "method": "drawLayerStack",
  "params": {
    "viewId": "<drafting_view_id>",
    "originX": 0, "originY": 0,
    "height": 8.0,
    "direction": "left-to-right",
    "addLabels": true,
    "layers": [
      {"name": "5/8\" GWB", "thickness": 0.052, "hatch": "Gypsum Board", "lineWeight": "Thin Lines"},
      {"name": "Vapor Barrier", "thickness": 0, "linePattern": "Dash"},
      {"name": "2x6 Stud", "thickness": 0.458, "hatch": "Wood - Section", "lineWeight": "Medium Lines"},
      {"name": "R-21 Insulation", "thickness": 0.458, "hatch": "Insulation", "overlay": true}
    ]
  }
}
```

**For custom geometry (foundations, connections, flashings):**
```json
{
  "method": "batchDrawDetail",
  "params": {
    "viewId": "<drafting_view_id>",
    "operations": [
      {"op": "filledRegion", "points": [[0,0],[2,0],[2,-1],[0,-1]], "typeName": "Concrete"},
      {"op": "line", "start": [-1, 0], "end": [3, 0], "lineStyle": "Wide Lines"},
      {"op": "text", "location": [3.5, -0.5], "text": "CONT. SPREAD FOOTING"},
      {"op": "detailComponent", "familyName": "Reinf Bar Section", "typeName": "#5", "location": [0.25, -0.75]}
    ]
  }
}
```

**For SVG-based details:**
```json
{
  "method": "importSvgToDetail",
  "params": {
    "viewId": "<drafting_view_id>",
    "originX": 0, "originY": 0,
    "targetWidthFeet": 2.0,
    "svg": "<svg viewBox='0 0 100 200'>...</svg>"
  }
}
```

### Step 4: Add Annotations
Use `batchDrawDetail` with text operations for callouts, and dimension operations.

### Step 5: Self-Review (Feedback Loop)
```json
{"method": "captureViewportToBase64", "params": {"viewId": "<drafting_view_id>"}}
```
- Examine the captured image
- Check: Are all layers visible? Text readable? Dimensions correct?
- Fix issues with additional `batchDrawDetail` calls
- Re-capture to verify

## Coordinate System

In drafting views:
- Origin at (0, 0)
- X increases right
- Y increases up
- Units in FEET (Revit internal)
- No Z coordinate (always 0)

### Common Conversions
| Dimension | Inches | Feet |
|-----------|--------|------|
| 2x4 actual | 3.5" | 0.2917' |
| 2x6 actual | 5.5" | 0.4583' |
| 2x8 actual | 7.25" | 0.6042' |
| 2x10 actual | 9.25" | 0.7708' |
| 2x12 actual | 11.25" | 0.9375' |
| 1/2" GWB | 0.5" | 0.0417' |
| 5/8" GWB | 0.625" | 0.0521' |
| 7/16" OSB | 0.4375" | 0.0365' |
| 8" CMU | 7.625" | 0.6354' |
| 4" slab | 4" | 0.3333' |

## Template Files
See `knowledge/details/templates/_index.json` for the complete template library.

## SVG Tips for Detail Generation

### Custom Attributes
- `data-revit-fill="Concrete"` - Map to specific Revit filled region type
- `data-revit-linestyle="Wide Lines"` - Map to specific Revit line style

### Stroke Width Mapping
- `stroke-width < 1` → Thin Lines
- `stroke-width 1-2` → Medium Lines
- `stroke-width > 2` → Wide Lines

### Coordinate Space
- SVG viewBox defines the coordinate space
- `targetWidthFeet` or `targetHeightFeet` auto-scales
- Y-axis is flipped (SVG Y-down → Revit Y-up) by default
