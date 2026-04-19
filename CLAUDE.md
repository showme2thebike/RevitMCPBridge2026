# BIM Monkey — RevitMCPBridge2026

## Build & Deploy
```bash
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

# 3. Build installer (Inno Setup)
powershell.exe -Command "& 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' 'C:\Users\echra\.bimmonkey\bimmonkey-ai-git\scripts\BimMonkeySetup.iss'"
powershell.exe -Command "Compress-Archive -Path 'dist/BimMonkeySetup.exe' -DestinationPath 'dist/BimMonkeySetup.zip' -Force"

# 4. *** COPY ZIP to frontend/public/ — this is what bimmonkey.ai/install downloads ***
cp "C:/Users/echra/.bimmonkey/bimmonkey-ai-git/dist/BimMonkeySetup.zip" \
   "C:/Users/echra/.bimmonkey/bimmonkey-ai-git/frontend/public/BimMonkeySetup.zip"

# 5. *** UPDATE VERSION STRING in both install pages — must match AssemblyInfo.cs InformationalVersion ***
# Format: v0.2.YYYYMMDD  (e.g. v0.2.20260417)
# File 1: frontend/src/pages/Install.jsx  — search for "Windows · Revit 2024"
# File 2: landing/install.html            — search for "Windows · Revit 2024"
# Also update Properties/AssemblyInfo.cs  — AssemblyInformationalVersion line

# 6. Commit and push — Netlify auto-deploys and serves the new zip
cd "C:/Users/echra/.bimmonkey/bimmonkey-ai-git"
git add dist/BimMonkeySetup.exe dist/BimMonkeySetup.zip frontend/public/BimMonkeySetup.zip \
        frontend/src/pages/Install.jsx landing/install.html
git commit -m "..." && git push
```

> **WARNING:** Steps 2, 4, and 5 are all required on every build.
> Skipping step 2 → installer packages the old DLL (plugin changes don't appear).
> Skipping step 4 → bimmonkey.ai/install serves the old zip.
> Skipping step 5 → install pages show the wrong version number to users.
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

## Telemetry Architecture (AgentCore.cs + TelemetryService.cs)
Events posted to `/api/telemetry` via Bearer = BIM Monkey API key:

| Event | When | Key metadata |
|---|---|---|
| `session_start` | First user message per AgentCore instance | — |
| `chat_message` | Every user message | `chars`, `words` (no content) |
| `tool_call` | Every MCP call completes | `toolName`, `durationMs`, `success` |
| `quality_failure` | `ResultVerifier.Verified=false` | `toolName`, `reason` |
| `api_error` | Anthropic API error | `error` |
| `session_outcome` | Session ends (any path) | `outcome`, `stage`, `last_tool`, `input_tokens`, `output_tokens`, `durationMs` |

**session_outcome outcomes:** `completed` · `interrupted` · `error`
**session_outcome stages:** `thinking` (Claude API in flight) · `executing` (Revit tool in flight) · `responding` (text delivered, idle)

**Interrupted tracking — two layers:**
1. `AgentChatPanel.Closing` → sets `_isClosing = true`, calls `_agent.NotifyInterrupted()` → fire-and-forget `TelemetryService.Send` (non-blocking; Revit process stays alive so thread pool completes it), then `SaveSession`/`DisconnectMCP` on `Task.Run` so UI thread never blocks
2. `AppDomain.CurrentDomain.ProcessExit` → `TelemetryService.SendSync` (blocking, 3s timeout — process is dying so thread pool can't be trusted)

`_sessionOutcomeSent` flag prevents double-firing when both paths trigger.

**Banana Chat close — no UI thread blocking:**
All agent event handlers use `Dispatcher.BeginInvoke` (async) not `Dispatcher.Invoke` (sync). Each handler checks `_isClosing` before and after the dispatch. This prevents the deadlock where RunAsync fires an event while the UI thread is blocked in the Closing handler.

**callMCPMethod unwrap:** Claude routes most Revit calls through `callMCPMethod` wrapper. Before verification and telemetry, unwrap: real method = `block.Input["method"]`, real params = `block.Input["parameters"]`.

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
