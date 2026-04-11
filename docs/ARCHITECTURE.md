# RevitMCPBridge2026 Architecture

## System Overview

Two independent paths exist for AI-driven Revit automation:

```
PATH A: Claude Code + MCP wrapper (external terminal)
─────────────────────────────────────────────────────

┌─────────────────────────────────────────────────────────────────────┐
│                        Claude Code / AI Client                       │
│                    (Natural Language Interface)                      │
└─────────────────────────────────────┬───────────────────────────────┘
                                      │ MCP stdio (JSON-RPC)
                                      ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      revit_mcp_wrapper.py                            │
│              (Python MCP server — bridges stdio ↔ pipe)             │
│  • Launched automatically by Claude Code via .mcp.json              │
│  • All logging goes to STDERR — stdout must stay clean for MCP      │
│  • Source lives in woodstudioai/scripts/ (not RevitMCPBridge2026)   │
│  • Installed to Documents\BIM Monkey\wrapper\ on end-user machines  │
└─────────────────────────────────────┬───────────────────────────────┘
                                      │ Windows Named Pipe
                                      ▼
┌─────────────────────────────────────────────────────────────────────┐
│                         Named Pipe Server                            │
│                    \\.\pipe\RevitMCPBridge2026                       │
│  • Request parsing and routing                                       │
│  • Method dispatch (705 methods)                                     │
│  • Response serialization                                            │
└─────────────────────────────────────┬───────────────────────────────┘
                                      │ Revit Idling Event
                                      ▼
┌─────────────────────────────────────────────────────────────────────┐
│                          Revit 2026 API                              │
└─────────────────────────────────────────────────────────────────────┘


PATH B: Banana Chat (in-process Revit ribbon panel)
────────────────────────────────────────────────────

┌─────────────────────────────────────────────────────────────────────┐
│                     Banana Chat (Revit Ribbon)                       │
│                       AgentChatPanel.cs                              │
│  • Direct Anthropic API (claude-sonnet-4-6) — no wrapper, no Railway│
│  • CoreKnowledgeFiles pre-loaded: cad-visual-rules.md,              │
│    bimmonkey-backend-best-practices.md + 6 others                   │
│  • CAD visual rules §1,4,7,8 injected into every system prompt      │
└──────────┬──────────────────────────────┬───────────────────────────┘
           │ Anthropic API (HTTPS)         │ Named Pipe (MCP methods)
           ▼                              ▼
┌──────────────────────┐   ┌─────────────────────────────────────────┐
│   Claude API         │   │  NCS Classification Pipeline (in-process)│
│   (claude-sonnet)    │   │  NcsViewClassifier.cs — 13-step tree     │
│                      │   │  NcsSheetPacker.cs — bin-packing         │
│   Handles:           │   │  classifyAndPackViews MCP method         │
│   • Ambiguous views  │   │  → runs BEFORE any sheet creation        │
│   • User chat        │   └────────────────┬────────────────────────┘
│   • Plan decisions   │                    │ Revit Idling Event
└──────────────────────┘                    ▼
                             ┌──────────────────────────────────────┐
                             │         Revit 2026 API               │
                             └──────────────────────────────────────┘
```

## Core Components

### 1. MCPServer.cs
Main entry point for MCP communication.

**Responsibilities:**
- Named pipe server lifecycle
- Request/response handling
- Method routing to appropriate handler
- JSON serialization

**Key Methods:**
- `StartServer()` - Initialize named pipe
- `ProcessRequest()` - Route to method handler
- `ExecuteMethod()` - Invoke specific method
- `ListAvailableMethods()` - Return method catalog

### 2. Method Files (*Methods.cs)

Each category has a dedicated method file:

| File | Methods | Purpose |
|------|---------|---------|
| WallMethods.cs | 11 | Wall creation, modification |
| RoomMethods.cs | 10 | Room operations |
| ViewMethods.cs | 12 | View management |
| SheetMethods.cs | 11 | Sheet/viewport |
| ScheduleMethods.cs | 34 | Schedule operations |
| NcsClassifierMethods.cs | 1 | NCS classification + sheet packing (see below) |
| ... | ... | ... |

**Standard Method Pattern:**
```csharp
public static string MethodName(UIApplication uiApp, JObject parameters)
{
    try {
        var doc = uiApp.ActiveUIDocument.Document;

        // Parameter validation
        // Transaction for modifications
        // Return JSON result
    }
    catch (Exception ex) {
        return ErrorResponse(ex);
    }
}
```

### 3. NCS Classification Pipeline (in-process)

Added in April 2026. Banana Chat now runs the same deterministic NCS/UDS sheet classification that the Railway daemon uses — entirely in-process via C#, with no network call required.

```
classifyAndPackViews (MCP method)
  │
  ├── NcsViewClassifier.ClassifyInventory()
  │     13-step decision tree — assigns every view to an NCS slot
  │     Confidence levels: blocked → definite → probable → ambiguous
  │     Inputs: view name, viewType, scale, crop box, view template name
  │
  ├── NcsSheetPacker.PackInventory()
  │     Bin-packs classified views onto ARCH D sheets (640 sq in)
  │     Floor plans: one sheet per level (A1.1, A1.2 …)
  │     Fill target: 75% — flags under-filled sheets for content
  │
  └── NcsSheetPacker.BuildPromptInventoryBlock()
        Produces the structured 6-section text block injected into
        Banana Chat context before Claude sees the sheet placement task
```

**New files:**

| File | Purpose |
|------|---------|
| `src/NcsClassifier/NcsModels.cs` | Data types: NcsViewInfo, NcsClassifiedView, NcsPackedSheet, NcsPackedPlan, etc. |
| `src/NcsClassifier/NcsViewClassifier.cs` | 13-step decision tree; renovation/level detection; permit checks |
| `src/NcsClassifier/NcsSheetPacker.cs` | Bin-packing; floor plan per-level splitting; gap sheet detection; prompt block builder |
| `src/NcsClassifier/NcsClassifierMethods.cs` | MCP-exposed `classifyAndPackViews` method |

**Two generation paths — Railway daemon vs. Banana Chat:**

| Path | Who calls it | Classification source | Network |
|------|-------------|----------------------|---------|
| Daemon (bimmonkey_run.py) | Railway backend | viewClassifier.js → sheetPacker.js (Node.js) | Yes — Railway |
| Banana Chat (AgentChatPanel) | Eric/Barrett direct chat | NcsViewClassifier.cs → NcsSheetPacker.cs (C# in-process) | No |

Both paths implement the same 13-step decision tree and produce the same NCS slot assignments. The C# port was created so Banana Chat gets equivalent deterministic pre-classification without needing Railway to be reachable.

### 4. Banana Chat (AgentChatPanel)

In-process AI assistant panel in the Revit ribbon. Uses the Anthropic API directly (not MCP wrapper / not Railway).

**System prompt enhancements (April 2026):**
- **CAD visual rules quick-ref** — sections 1, 4, 7, 8 of `cad-visual-rules.md` are extracted at startup and injected into every session's system prompt. Full reference available via `getKnowledgeFile`.
- **CoreKnowledgeFiles** — the following files are always pre-loaded (not on-demand) into every Banana Chat session:
  - `cad-visual-rules.md` — lineweight, scale, annotation, view template standards
  - `bimmonkey-backend-best-practices.md` — NCS pipeline rules, exact regex patterns, planValidator signature

**Sheet placement workflow (enforced in system prompt):**
```
Step 0: Call classifyAndPackViews FIRST — always, before createSheet or placeViewOnSheet
Step 1: Review promptBlock — definite assignments are authoritative
Step 2: Resolve ambiguous views
Step 3: Create sheets in NCS order (G0→G1→A0→A1→…→E)
Step 4: Place viewports; always append ` *` to sheet names
Step 5: Audit for stacked viewports; fix label offsets
```

### 5. Intelligence Layer

Five levels of autonomy:

```
┌────────────────────────────────────────────────────────────────┐
│ Level 5: Full Autonomy                                          │
│ ┌─────────────────────────────────────────────────────────────┐│
│ │ AutonomousExecutor.cs                                       ││
│ │ • GoalPlanner - Task decomposition                          ││
│ │ • SelfHealer - Error recovery                               ││
│ │ • GuardrailSystem - Bounded execution                       ││
│ │ • QualityAssessor - Result verification                     ││
│ └─────────────────────────────────────────────────────────────┘│
├────────────────────────────────────────────────────────────────┤
│ Level 4: Proactive Intelligence                                 │
│ ┌─────────────────────────────────────────────────────────────┐│
│ │ ProactiveMonitor.cs                                         ││
│ │ • Gap detection                                              ││
│ │ • Suggestion engine                                          ││
│ │ • Model snapshots                                            ││
│ └─────────────────────────────────────────────────────────────┘│
├────────────────────────────────────────────────────────────────┤
│ Level 3: Learning & Memory                                      │
│ ┌─────────────────────────────────────────────────────────────┐│
│ │ CorrectionLearner.cs                                        ││
│ │ • Error pattern learning                                     ││
│ │ • Correction storage                                         ││
│ │ • Knowledge export                                           ││
│ └─────────────────────────────────────────────────────────────┘│
├────────────────────────────────────────────────────────────────┤
│ Level 2: Context Awareness                                      │
│ ┌─────────────────────────────────────────────────────────────┐│
│ │ ChangeTracker.cs                                            ││
│ │ • Element relationship tracking                              ││
│ │ • Session context                                            ││
│ └─────────────────────────────────────────────────────────────┘│
├────────────────────────────────────────────────────────────────┤
│ Level 1: Basic Bridge                                           │
│ ┌─────────────────────────────────────────────────────────────┐│
│ │ Direct MCP → Revit API translation                          ││
│ │ 705 methods                                                  ││
│ └─────────────────────────────────────────────────────────────┘│
└────────────────────────────────────────────────────────────────┘
```

## Data Flow

### Request Processing

```
1. Client sends JSON request
   {"method": "createWall", "params": {...}}

2. Named pipe receives message

3. MCPServer.ProcessRequest parses JSON

4. Method router finds handler
   methodMap["createWall"] -> WallMethods.CreateWall

5. Handler executes in Revit context
   - Validates parameters
   - Creates Transaction
   - Calls Revit API
   - Commits Transaction

6. Response serialized to JSON
   {"success": true, "elementId": 123456}

7. Response sent back through pipe
```

### Transaction Management

All modifications require transactions:

```csharp
using (var trans = new Transaction(doc, "Create Wall"))
{
    trans.Start();

    // Revit API calls
    Wall.Create(doc, curve, wallTypeId, levelId, height, 0, false, false);

    trans.Commit();
}
```

## File Structure

```
RevitMCPBridge2026/
├── src/                        # C# source
│   ├── MCPServer.cs           # Named pipe server
│   ├── RevitMCPBridge.cs      # Add-in entry point
│   ├── *Methods.cs            # API method implementations (705 methods total)
│   ├── NcsClassifier/         # NCS classification pipeline (April 2026)
│   │   ├── NcsModels.cs       # Data types for the pipeline
│   │   ├── NcsViewClassifier.cs  # 13-step decision tree
│   │   ├── NcsSheetPacker.cs  # Bin-packing + prompt block builder
│   │   └── NcsClassifierMethods.cs  # classifyAndPackViews MCP method
│   ├── AgentFramework/        # Banana Chat AI assistant panel
│   │   └── AgentChatPanel.cs  # In-process Claude API client
│   ├── AutonomousExecutor.cs  # Level 5 autonomy
│   ├── ProactiveMonitor.cs    # Level 4 intelligence
│   ├── CorrectionLearner.cs   # Level 3 learning
│   └── ChangeTracker.cs       # Level 2 context
├── knowledge/                  # Domain knowledge (113 files, ~1.5 MB)
│   ├── _index.md              # Knowledge index
│   ├── cad-visual-rules.md    # ⭐ CRITICAL: lineweight, scale, annotation, view templates
│   ├── bimmonkey-backend-best-practices.md  # ⭐ CRITICAL: NCS pipeline rules + regex patterns
│   ├── room-standards.md      # Room sizing
│   ├── code-compliance.md     # Building codes
│   └── ...
├── Properties/                 # Assembly info
├── bin/                        # Build output
├── docs/                       # Documentation
├── tests/                      # Test suites
├── scripts/                    # Build/deploy scripts
└── installer/                  # Inno Setup installer project
    └── files/2026/            # DLL staging for installer build
```

## Integration Points

### Revit Integration

- **Add-in Manifest**: RevitMCPBridge2026.addin
- **Entry Point**: ExternalApplication.OnStartup
- **Event Hook**: Application.Idling (for async operations)
- **API Version**: Revit 2026 (.NET Framework 4.8)

### External Integrations

- **Stable Diffusion**: python/diffusion_service.py for AI rendering
- **Claude Memory MCP**: Persistent learning storage
- **Floor Plan Vision MCP**: PDF extraction

## Security Considerations

1. **Named Pipe Access**: Local machine only
2. **Transaction Safety**: All operations are transactional
3. **Input Validation**: Parameters validated before execution
4. **Guardrails**: Level 5 autonomy has configurable limits

## Performance

- **Async Operations**: Non-blocking pipe communication
- **Batch Support**: Bulk operations reduce overhead
- **Caching**: Type lookups cached per session
- **Logging**: Serilog for diagnostics

## Extension Points

### Adding New Methods

1. Create method in appropriate *Methods.cs file
2. Register in MCPServer.cs method router
3. Add to ListAvailableMethods catalog
4. Update documentation

### Adding New Intelligence

1. Create new intelligence class
2. Wire into IntelligenceMethods.cs
3. Register endpoints in MCPServer.cs
4. Add MCP method catalog entry

---

## Build & Deploy Architecture

### Repo Split — Critical to Understand

| Repo | Owner | Contains |
|------|-------|----------|
| `WeberG619/RevitMCPBridge2026` | Dev only | C# plugin source, DLL |
| `showme2thebike/woodstudioai` | Product repo | Installer, wrapper, React frontend, API backend |

**The wrapper (`revit_mcp_wrapper.py`) lives in `woodstudioai/scripts/`** — NOT here.
It is installer glue, not plugin code. It was moved there so the entire
deployable product lives in one repo the product account controls.

### Frontend / Website Architecture

The `woodstudioai` repo contains three web surfaces:

| Folder | Purpose | Deployed |
|--------|---------|---------|
| `frontend/` | React SPA (Vite) — all public-facing pages | Yes — Netlify builds from here |
| `landing/` | Legacy static HTML files | **NOT deployed** — disconnected |
| `api/` | Express.js backend | Railway (`bimmonkey-production.up.railway.app`) |

**Critical:** `landing/*.html` files are **not deployed** to bimmonkey.ai.
Netlify builds from `frontend/` only (`netlify.toml` `base = "frontend"`).
All marketing pages (`/how-it-works`, `/install`, `/tos`, etc.) are React
components in `frontend/src/pages/`. Edit those files — not the `landing/` HTML.

The `landing/` folder should be treated as archived/reference only. Any content
changes must be made in the corresponding React page component:

| URL | Component |
|-----|-----------|
| `/` | `frontend/src/pages/Landing.jsx` |
| `/how-it-works` | `frontend/src/pages/HowItWorks.jsx` |
| `/install` | `frontend/src/pages/Install.jsx` |
| `/tos` | `frontend/src/pages/Tos.jsx` |

**Netlify deploy config** (`netlify.toml`):
- Build base: `frontend/`
- Build command: `npm install && npm run build`
- Publish dir: `dist/`
- Catch-all redirect: `/* → /index.html` (SPA routing)

**API auth header:** The backend uses `Authorization: Bearer <key>`, not `X-API-Key`.
Correct endpoint to validate a key: `GET /api/auth/verify` with `Authorization: Bearer bm_...`.

### Dev Build Flow (developer machine only)

```
1. Edit C# source in RevitMCPBridge2026/
2. Edit installer/wrapper in woodstudioai/scripts/ if needed
3. Run the build script — ONE command handles everything:

   cd C:\Users\echra\CascadeProjects\wood-studio-ai-git
   .\scripts\build_plugin_zip.ps1

   → dotnet publish → bin/Release/publish/RevitMCPBridge2026.dll  (correct output path)
   → ISCC           → dist/BimMonkeySetup.exe

4. gh release create → upload BimMonkeySetup.exe to GitHub
5. git push (woodstudioai)
```

⚠️  NEVER run MSBuild manually then ISCC separately.
MSBuild outputs to bin/Release/ but the installer reads from bin/Release/publish/.
dotnet publish (used by the script) outputs to the right place.
Running them out of order silently packages the old DLL.

### End User Install Flow

```
1. Download BimMonkeySetup.exe
2. Run it — wizard installs:
   %APPDATA%\Autodesk\Revit\Addins\2026\RevitMCPBridge2026.dll
   %APPDATA%\Autodesk\Revit\Addins\2026\RevitMCPBridge2026.addin
   Documents\BIM Monkey\wrapper\revit_mcp_wrapper.py
   Documents\BIM Monkey\.mcp.json          ← auto-written with API key + python path
   Documents\BIM Monkey\CLAUDE.md          ← auto-written with API key
   Documents\BIM Monkey\README.md
3. Open Revit → BIM Monkey tab → Start Server
4. Open terminal in Documents\BIM Monkey → type "claude"
5. Claude Code reads .mcp.json, auto-starts the wrapper, tools appear
```

### The MCP Connection Chain (end-user runtime)

```
claude (Claude Code)
  └─ reads .mcp.json
  └─ spawns: python wrapper\revit_mcp_wrapper.py
               │  (stdio JSON-RPC)
               └─ connects to \\.\pipe\RevitMCPBridge2026
                               │  (Windows named pipe)
                               └─ Revit add-in (RevitMCPBridge2026.dll)
                                               │
                                               └─ Revit 2026 API
```

## Generation Protocol (How Claude Creates CD Sets)

Claude **must never improvise a CD plan**. Every generation follows this exact sequence:

```
1. Read model:  revit_execute("getDocumentInfo"), getLevels, getRooms,
                getSheets, getViews, getWallTypes — combine into one JSON object

2. Get plan:    bim_monkey_generate(modelJson)
                → build_model_summary() reshapes + prunes raw Revit responses
                  into the compact format the backend expects
                → POSTs lean model summary (~20KB) to /api/generate on Railway
                → Backend does RAG lookup against firm's approved_examples
                → Backend injects previous run notes as direct instructions
                → Returns { generationId, plan }
                → STOP and report if this fails — do not proceed

3. Execute:     revit_execute() for each sheet, view, schedule, detail in plan
                - Schedule views: use placeScheduleOnSheet (not placeViewOnSheet)
                - Views marked requiresDuplicate:true: call duplicateView (newName param) first
                - Log all failures, continue — do not abort entire run

4. Report:      bim_monkey_report_issues(generationId, issues)
                → Posts all issues encountered to dashboard System Report

5. Mark done:   bim_monkey_mark_executed(generationId)
                → Logs run in dashboard so notes/feedback loop works
```

This is enforced in `CLAUDE.md` (the project instructions file Claude reads on startup)
and in the tool docstrings in `revit_mcp_wrapper.py`.

### Backend Generation Pipeline (`/api/generate`)

```
POST /api/generate
  │
  │  ← receives lean model summary (~20KB) built by build_model_summary()
  │    Keys: projectName, projectInfo, buildingType, levels[], rooms[],
  │          existingSheets[], wallTypes[], existingViews[], existingDraftingViews[]
  │    (NOT raw Revit response objects — those are never sent to the backend)
  │
  ├── fetchApprovedExamples(buildingType, firmId)
  │     → queries approved_examples table (firm's private library only)
  │     → RAG: retrieved examples injected as reference context
  │
  ├── fetchPreviousRunNotes(firmId, projectName)
  │     → queries last 5 generations with notes for same project
  │     → injected as "FEEDBACK FROM PREVIOUS RUNS (apply to this generation):"
  │     → these are INSTRUCTIONS to the AI, not just background context
  │
  ├── fetchCrossReferenceRules() + fetchLayoutTemplates()
  │
  ├── existingViews → structured EXISTING VIEWS block (id|viewType|name per line)
  │     used for view-to-sheet mapping in the sheets prompt
  │
  ├── existingSheets → sheet collision detection + numbering style detection
  │
  └── Two-phase Claude API call (cached system prompt):
        Phase 1 → sheets JSON (viewPlacements include isSchedule:true and requiresDuplicate:true flags)
        Phase 2 → detail plan JSON
        → returns { success, generationId, plan }
```

### Dashboard System Report (`/api/generation/:id/system-report`)

```
POST /api/generation/:id/system-report
  ← called by bim_monkey_report_issues() MCP tool after execution
  ← body: { issues: [{type, message, item?, recommendation?}] }
  → stores in generations.system_notes JSONB column
  → shown in run detail view as "System Report" section (yellow, above sheets)
```

### Community Pool — Deprecated

Community pool was removed. All `approved_examples` rows are now firm-private
(`in_community_pool = false` hardcoded). The Settings toggle, Team stats section,
and all community pool language have been removed from the frontend and ToS.

### Wrapper Bug Fixes Applied (March 2026 — Sprint N)

The following CD-generation bugs were fixed based on post-run analysis of the BAINES REMODEL output:

| Area | Problem | Fix |
|------|---------|-----|
| `bim_monkey_generate` payload | Raw Revit response objects sent to backend — `existingViews`, `existingSheets`, `levels` extracted as empty arrays; backend viewsBlock and sheet collision detection silently broken | Replaced ad-hoc slimming with `build_model_summary()` which reshapes nested responses into flat arrays the backend expects |
| Payload size | 157 views × full metadata = ~200KB; 500-view models would hit ~500KB and timeout | `build_model_summary()` prunes to `{id,name,viewType}` per view — 500-view model now ~20KB |
| Schedule placement | Backend CD plan contained no signal; executor Claude called `placeViewOnSheet` for Schedule views (always fails in Revit) | sheetsPrompt now instructs AI to mark Schedule viewPlacements with `"isSchedule": true` |
| View duplication | No signal when same view needed on multiple sheets; executor called `placeViewOnSheet` twice → Revit error | sheetsPrompt now marks second+ occurrences with `"requiresDuplicate": true` |
| `duplicateView` param | Executor passed `name` parameter; plugin reads `newName` — view was renamed silently | CLAUDE.md execution rules updated: `newName` not `name` |
| Auto system notes | No post-run issue reporting; errors only visible in Claude's local transcript | New `bim_monkey_report_issues` MCP tool + `POST /api/generation/:id/system-report` endpoint + dashboard System Report section |

### Plugin Bug Fixes Applied (March 2026 — v0.1.202603220758)

The following field-session bugs were fixed based on a BAINES REMODEL CD run:

| Method | Problem | Fix |
|--------|---------|-----|
| `batchExecute` | Called `ExecuteMethodDirect` which requires `_currentUiApp` to be set externally — always returned "No UIApplication context" | Changed to call `ExecuteMethod(uiApp, ...)` directly using the `uiApp` already in scope |
| `createFilledRegionFromPoints` | Hard-failed if `typeId` not supplied; no fallback | Added `typeName` lookup by name (case-insensitive); returns list of available types on failure |
| `getTextNotes` | No filtering — dumped entire document (300k+ chars, exceeded token limit) | Added `viewId` filter, `fields` selector, `maxResults`/`offset` pagination; returns `totalCount` + `hasMore` |
| `drawLayerStack` | Only supported `left-to-right` / `right-to-left`; no vertical | Added `top-to-bottom` / `bottom-to-top` directions; layers stack along Y axis, `height` param becomes horizontal span width |
| `placeViewOnSheetSmart` | Always defaulted to sheet center when no `targetZone` — caused viewport stacking | Added 9-zone auto-detection (TL/TC/TR/ML/MC/MR/BL/BC/BR); finds first unoccupied zone |
| `createTextNoteWithLeader` | Ignored `position:{x,y}` / `leaderEnd:{x,y}` object params | Added nested object support as alternative to flat `textX`/`textY`/`leaderX`/`leaderY` |
| `moveTextNote` | `.Coord` setter is unreliable in Revit — move was a no-op | Replaced with `ElementTransformUtils.MoveElement` using delta; reports actual post-move position |
| `moveViewport` | Only accepted `newLocation:{x,y}` — flat `x`/`y` and `location:{x,y}` silently ignored | Added fallback to parse `location` token or flat `x`/`y` params |

**Server startup change (same session):** Server no longer auto-starts on add-in load.
The Start Server ribbon button is now clickable immediately on Revit open. This prevents
the button appearing greyed-out and avoids false "server already running" state when the
named pipe isn't actually ready.

### Critical Lessons Learned

**Logging must go to stderr.**
FastMCP logs INFO lines to stdout by default. Claude Code reads stdout
expecting clean JSON-RPC. Any non-JSON line on stdout silently breaks
the handshake — tools never appear, no error shown. Fix:
```python
logging.basicConfig(stream=sys.stderr, level=logging.WARNING)
```

**`.mcp.json` must be written by the installer.**
Claude Code only auto-starts MCP servers declared in `.mcp.json` in the
project directory. Without it, users get no tools and no error. The
installer writes it post-install with the user's API key and the correct
absolute path to the wrapper.

**Dev machine ≠ end-user machine in one way only.**
Developer runs: build DLL → build installer → run installer.
End user runs: run installer. From the installer step onward, identical.
