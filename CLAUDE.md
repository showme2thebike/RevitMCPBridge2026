# BIM Monkey — RevitMCPBridge2026

## Knowledge File Encryption
All `.md` files in `knowledge/` are AES-256 encrypted at build time.
- Key: `CONTENT_ENCRYPTION_KEY` (64 hex chars) — set in Railway env vars
- Script: `bimmonkey-ai-git/scripts/encrypt_knowledge.js`
- `build_plugin_zip.ps1` runs this automatically — set `$env:CONTENT_ENCRYPTION_KEY` before building
- To add a new knowledge file: add plaintext `.md`, then run the build script (encrypt runs automatically)
- The plugin decrypts in-memory using the key returned by `/api/auth/session`
- Never commit plaintext knowledge files — always check BM01 magic header (`42 4D 01 00`) before committing

## Build & Deploy
```bash
# 0. Set encryption key (get from Railway → bimmonkey-production → Variables)
$env:CONTENT_ENCRYPTION_KEY = "cb158b53d99cec1c26919c05159c1b65ed893a16855df5c61d1bd645aeeb1655"

# 1. Build plugin
dotnet publish -c Release --no-self-contained

# 2. *** COPY DLL to installer staging AND dev Revit location ***
# Installer staging (Inno Setup reads from here, NOT bin/Release/publish/)
cp "C:/Users/echra/.bimmonkey/RevitMCPBridge2026/bin/Release/publish/RevitMCPBridge2026.dll" \
   "C:/Users/echra/.bimmonkey/RevitMCPBridge2026/installer/files/2026/RevitMCPBridge2026.dll"
# Dev Revit location — deploy to BOTH; Revit may load from either depending on install type
cp "C:/Users/echra/.bimmonkey/RevitMCPBridge2026/bin/Release/publish/RevitMCPBridge2026.dll" \
   "C:/Users/echra/AppData/Roaming/Autodesk/Revit/Addins/2026/RevitMCPBridge2026.dll"
cp "C:/Users/echra/.bimmonkey/RevitMCPBridge2026/bin/Release/publish/RevitMCPBridge2026.dll" \
   "C:/ProgramData/Autodesk/Revit/Addins/2026/RevitMCPBridge2026.dll"
# Knowledge files — must live alongside the DLL for Banana Chat to find them
cp -r "C:/Users/echra/.bimmonkey/RevitMCPBridge2026/knowledge" \
   "C:/Users/echra/AppData/Roaming/Autodesk/Revit/Addins/2026/knowledge"
# *** WRAPPER SCRIPTS — copy to OneDrive path (NOT C:/Users/echra/Documents — those are different on this machine) ***
cp "C:/Users/echra/.bimmonkey/bimmonkey-ai-git/scripts/generate_vicinity_map.py" \
   "C:/Users/echra/OneDrive/Documents/BIM Monkey/wrapper/generate_vicinity_map.py"

# 3. Build installer (Inno Setup)
powershell.exe -Command "& 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' 'C:\Users\echra\.bimmonkey\bimmonkey-ai-git\scripts\BimMonkeySetup.iss'"
powershell.exe -Command "Compress-Archive -Path 'dist/BimMonkeySetup.exe' -DestinationPath 'dist/BimMonkeySetup.zip' -Force"

# 4. *** COPY ZIP to frontend/public/ — this is what bimmonkey.ai/install downloads ***
cp "C:/Users/echra/.bimmonkey/bimmonkey-ai-git/dist/BimMonkeySetup.zip" \
   "C:/Users/echra/.bimmonkey/bimmonkey-ai-git/frontend/public/BimMonkeySetup.zip"

# 5. *** COMMIT SOURCE to RevitMCPBridge2026 FIRST ***
cd "C:/Users/echra/.bimmonkey/RevitMCPBridge2026"
git add src/ installer/files/2026/RevitMCPBridge2026.dll Properties/AssemblyInfo.cs
git commit -m "..." && git push

# 6. *** THEN commit installer to bimmonkey-ai-git ***
# Also update AppVersion in scripts/BimMonkeySetup.iss to match AssemblyInformationalVersion
cd "C:/Users/echra/.bimmonkey/bimmonkey-ai-git"
cp "C:/Users/echra/.bimmonkey/bimmonkey-ai-git/dist/BimMonkeySetup.zip" \
   "C:/Users/echra/.bimmonkey/bimmonkey-ai-git/landing/BimMonkeySetup.zip"
git add scripts/BimMonkeySetup.iss dist/BimMonkeySetup.exe dist/BimMonkeySetup.zip frontend/public/BimMonkeySetup.zip landing/BimMonkeySetup.zip
git commit -m "..." && git push
```

> **WARNING:** Both repos must be committed on every release.
> RevitMCPBridge2026 = plugin source code (C# files, DLL)
> bimmonkey-ai-git = installer binaries + AppVersion string
> Skipping RevitMCPBridge2026 → source history diverges from what's running.
> Skipping bimmonkey-ai-git → bimmonkey.ai/install serves the old installer.

> **WARNING:** Steps 2 and 4 are both required on every build.
> Skipping step 2 → installer packages the old DLL (plugin changes don't appear).
> Skipping step 4 → bimmonkey.ai/install serves the old zip.
> GitHub Releases are NOT used for the install download.

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

## Named Pipe Keepalive
The pipe can drop silently after ~15-20 sequential writes during heavy generation runs.
Call `bim_monkey_ping()` every 10-15 MCP operations (e.g. inside `createSheet` / `placeViewOnSheet` loops) to keep the connection alive. The `ping` method returns immediately with no Revit API side effects — it is safe to call at any frequency.

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
