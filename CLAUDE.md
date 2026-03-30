# BIM Monkey — RevitMCPBridge2026

## Build & Deploy
```bash
# 1. Build
dotnet publish -c Release --no-self-contained

# 2. Build installer (Inno Setup)
powershell.exe -Command "& 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' 'C:\Users\echra\.bimmonkey\bimmonkey-ai-git\scripts\BimMonkeySetup.iss'"
# Output: C:\Users\echra\.bimmonkey\bimmonkey-ai-git\dist\BimMonkeySetup.exe

# 3. Zip
powershell.exe -Command "Compress-Archive -Path 'dist/BimMonkeySetup.exe' -DestinationPath 'dist/BimMonkeySetup.zip' -Force"

# 4. Commit dist/ to git
git add dist/BimMonkeySetup.exe dist/BimMonkeySetup.zip && git commit -m "..." && git push

# 5. *** CREATE GITHUB RELEASE — THIS IS WHAT app.bimmonkey.ai/install DOWNLOADS ***
# The install page points to: github.com/showme2thebike/bimmonkeyai/releases/latest/download/BimMonkeySetup.zip
# Committing dist/ to the repo does NOT update the download. You MUST create a release.
gh release create "v0.1.$(date +%Y%m%d%H%M)" --title "..." --notes "..." dist/BimMonkeySetup.zip
```

> **WARNING:** Steps 1–4 alone are NOT enough. Without step 5, app.bimmonkey.ai/install
> still serves the old installer. Always create the GitHub release after every rebuild.

## BIM Monkey API
```
RAILWAY_API_URL=https://bimmonkey-production.up.railway.app
BIM_MONKEY_API_KEY=bm_...   # firm-specific key in CLAUDE.md in Documents\BimMonkey\
```

## Key Facts
- 705 MCP endpoints via named pipe `\\.\pipe\RevitMCPBridge2026`
- C# .NET Framework 4.8, Revit 2026 add-in
- All document modifications must be in Transaction blocks
- Restart Revit after deploying new DLL

## Revit API Notes
- `ScheduleSheetInstance.Create()` for schedules — NOT `Viewport.Create()`
- Empty views cannot be placed on sheets — they must have content first
- `ElementId` always: `new ElementId(int)`
- `ScheduleFieldId` not `int` for field references (Revit 2026)
- Cloud families require manual UI interaction — PostCommand can't pass parameters
- MCP times out if Revit has a modal dialog open

## Method Pattern
```csharp
[MCPMethod("methodName", Category = "Category", Description = "...")]
public static string MethodName(UIApplication uiApp, JObject parameters)
{
    try
    {
        var doc = uiApp.ActiveUIDocument.Document;
        // validate params, then:
        using (var trans = new Transaction(doc, "Operation"))
        {
            trans.Start();
            // ... Revit API calls ...
            trans.Commit();
            return ResponseBuilder.Success().With("key", value).Build();
        }
    }
    catch (Exception ex)
    {
        return ResponseBuilder.FromException(ex).Build();
    }
}
```

## Sheet Methods: When to Use What
- `placeViewOnSheet` — floor plans, elevations, sections, drafting views
- `placeScheduleOnSheet` — Door/Window/Wall schedules (uses ScheduleSheetInstance.Create)
- `createDraftingView` then `placeViewOnSheet` — detail views

## Knowledge Files (on-demand — read only when needed)
Available in `knowledge/` directory. Read specific files as tasks require:
- `revit-api-lessons.md` — API gotchas and patterns
- `cd-standards.md` — sheet organization, numbering
- `annotation-standards.md` — text styles, leaders
- `error-recovery.md` — MCP errors, timeouts
