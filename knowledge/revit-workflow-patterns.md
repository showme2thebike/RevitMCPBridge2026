# Revit Workflow Patterns — Task Classification & Failure Prevention

## Task Type Classification

Identify the task type before starting. Each type has mandatory pre-execution steps.

| Type | Examples | Key risk |
|------|---------|---------|
| **data-entry** | Rename sheets, update parameters, fill schedules, set issuance dates | Overwriting existing data at wrong scope |
| **spatial-layout** | Place viewports, annotations, dimensions, casework, furniture | Wrong level, overlaps, stacked elements |
| **redline-execution** | Apply markup changes from PDF or verbal description | Modifying wrong element, missing spatial context |

## Clarify-First Rules by Task Type

### spatial-layout tasks — ask before executing:
- "Which floor/level are we working on?"
- "Should I check for existing elements in this area first?"

Skip only if user already stated: level name, room name, element type, AND coordinates or sheet.

### redline-execution tasks — ask before executing:
- "Which element exactly — ID, or can you describe its location more specifically?"
- "What should it change to?"

Skip only if the user provided the element ID directly.

### data-entry tasks — ask only if scope is ambiguous:
- "Should I update all instances or just the ones in the active view?"

## Pre-Placement Checklist

Before placing ANY element (viewport, annotation, dimension string, detail component, casework):

1. Call `getElementsInBoundingBox` with the target area bounding box — check for conflicts
2. If conflicts exist → report them, ask how to resolve, do NOT place over existing elements
3. For casework, furniture, millwork (no dedicated get method) → use `executeRevitScript` with the appropriate built-in category

### executeRevitScript — query casework in active view
```csharp
var items = new FilteredElementCollector(doc, doc.ActiveView.Id)
    .OfCategory(BuiltInCategory.OST_Casework)
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>()
    .Select(fi => new {
        id = fi.Id.IntegerValue,
        name = fi.Name,
        family = fi.Symbol.FamilyName
    }).ToList();
return Newtonsoft.Json.JsonConvert.SerializeObject(items);
```

Built-in categories with no dedicated MCP get method:
- `OST_Casework` — upper/lower cabinets, built-ins
- `OST_Furniture` — freestanding furniture
- `OST_SpecialityEquipment` — appliances

## Common Failure Patterns (Baines_V8 reference)

| Symptom | Root cause | Correct behavior |
|---------|-----------|-----------------|
| Viewport at wrong scale | Placed before getRecommendedScale | Always call getRecommendedScale first |
| Stacked viewports on sheet | No pre-placement conflict check | Call getElementsInBoundingBox before placeViewOnSheet |
| Dimension string on wrong wall | Ambiguous description without level | Ask "which floor?" before dimensioning |
| Casework missing from inventory | getElements doesn't cover OST_Casework | Use executeRevitScript with OST_Casework |
| Redline applied to wrong instance | Multiple similar elements, no ID confirmed | Confirm element ID before any model write |
| Label offset wrong after placement | Used auto:true on setViewportLabelOffset | Read existing bbox first; set explicit numeric offset |

## Casework Family Placement via executeRevitScript

Family must already be loaded before placement. Check first:
```csharp
var sym = new FilteredElementCollector(doc)
    .OfClass(typeof(FamilySymbol))
    .Cast<FamilySymbol>()
    .FirstOrDefault(s => s.FamilyName == "TARGET_FAMILY_NAME");
if (sym == null) return "Family not loaded — user must load it in Revit first";
if (!sym.IsActive) { sym.Activate(); doc.Regenerate(); }
// then place with doc.Create.NewFamilyInstance(location, sym, level, StructuralType.NonStructural)
```

Cloud families cannot be loaded via MCP. If not found, report to user and stop.

## Corrections Block Usage

The `===CORRECTIONS===` block in context contains past approved/edited/denied actions for this firm. At the start of any spatial or redline task:
1. Scan for entries matching the current element type, sheet, or operation
2. State applicable ones before executing: "Past correction for [topic]: [lesson] — applying now"
3. If a correction contradicts your current plan, follow the correction
