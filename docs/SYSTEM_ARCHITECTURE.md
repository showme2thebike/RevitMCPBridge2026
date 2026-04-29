# BIM Ops Studio - System Architecture

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                                    USER INTERFACE                                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                 │
│  │  Wispr Flow  │  │   VS Code    │  │   Terminal   │  │    Voice     │                 │
│  │   (Voice)    │  │    (IDE)     │  │   (Claude)   │  │   Output     │                 │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘  └──────▲───────┘                 │
│         │                 │                 │                 │                          │
│         └─────────────────┼─────────────────┘                 │                          │
│                           ▼                                   │                          │
│  ┌────────────────────────────────────────────────────────────┴──────────────────────┐  │
│  │                           CLAUDE CODE (WSL Ubuntu)                                 │  │
│  │                                                                                    │  │
│  │   ┌─────────────────────────────────────────────────────────────────────────┐     │  │
│  │   │                    INTELLIGENT ORCHESTRATION LAYER                       │     │  │
│  │   │  • Sub-agent dispatch (code-reviewer, test-writer, tech-lead, etc.)     │     │  │
│  │   │  • Parallel task execution                                               │     │  │
│  │   │  • Context management (~200k tokens)                                     │     │  │
│  │   │  • TodoWrite tracking                                                    │     │  │
│  │   └─────────────────────────────────────────────────────────────────────────┘     │  │
│  │                                      │                                            │  │
│  │   ┌──────────────────────────────────┼──────────────────────────────────────┐     │  │
│  │   │                         MCP CLIENT LAYER                                 │     │  │
│  │   └──────────────────────────────────┼──────────────────────────────────────┘     │  │
│  └──────────────────────────────────────┼────────────────────────────────────────────┘  │
└─────────────────────────────────────────┼───────────────────────────────────────────────┘
                                          │
          ┌───────────────────────────────┼───────────────────────────────┐
          │                               │                               │
          ▼                               ▼                               ▼
┌─────────────────────┐     ┌─────────────────────────┐     ┌─────────────────────┐
│   MCP SERVERS       │     │    WINDOWS BRIDGE       │     │   DATA LAYER        │
│   (stdio/TCP)       │     │    (Named Pipes)        │     │   (SQLite/JSON)     │
└─────────┬───────────┘     └───────────┬─────────────┘     └─────────┬───────────┘
          │                             │                             │
          │                             │                             │
┌─────────┴─────────────────────────────┴─────────────────────────────┴─────────────────┐
│                                                                                        │
│  ┌────────────────────────────────────────────────────────────────────────────────┐   │
│  │                              MCP SERVER FLEET                                   │   │
│  │                                                                                 │   │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐                 │   │
│  │  │ claude-memory   │  │   voice-mcp     │  │  sqlite-server  │                 │   │
│  │  │                 │  │                 │  │                 │                 │   │
│  │  │ • memory_store  │  │ • speak         │  │ • read_query    │                 │   │
│  │  │ • memory_recall │  │ • speak_summary │  │ • write_query   │                 │   │
│  │  │ • semantic_srch │  │ • list_voices   │  │ • create_table  │                 │   │
│  │  │ • find_patterns │  │ • stop_speaking │  │ • list_tables   │                 │   │
│  │  │ • corrections   │  │                 │  │                 │                 │   │
│  │  │       ▼         │  │       ▼         │  │       ▼         │                 │   │
│  │  │   memory.db     │  │  Edge TTS API   │  │  *.db files     │                 │   │
│  │  │   (549 mem)     │  │  (Windows)      │  │                 │                 │   │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────┘                 │   │
│  │                                                                                 │   │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐                 │   │
│  │  │  bluebeam-mcp   │  │ windows-browser │  │   aider-mcp     │                 │   │
│  │  │                 │  │                 │  │   (x3)          │                 │   │
│  │  │ • get_status    │  │ • browser_open  │  │                 │                 │   │
│  │  │ • open_document │  │ • screenshot    │  │ • ollama        │                 │   │
│  │  │ • take_screenshт│  │ • click/type    │  │ • llama4        │                 │   │
│  │  │ • go_to_page    │  │ • get_monitors  │  │ • quasar        │                 │   │
│  │  │       ▼         │  │       ▼         │  │       ▼         │                 │   │
│  │  │  Bluebeam Revu  │  │ Chrome/Edge     │  │  Multi-file     │                 │   │
│  │  │  (COM API)      │  │ (pyautogui)     │  │  code edits     │                 │   │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────┘                 │   │
│  │                                                                                 │   │
│  └────────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                        │
│  ┌────────────────────────────────────────────────────────────────────────────────┐   │
│  │                           SYSTEM BRIDGE DAEMON                                  │   │
│  │                        (D:\_CLAUDE-TOOLS\system-bridge)                         │   │
│  │                                                                                 │   │
│  │  ┌──────────────────────────────────────────────────────────────────────────┐  │   │
│  │  │  live_state.json (updated every 10 seconds)                              │  │   │
│  │  │  • Open applications + window titles                                      │  │   │
│  │  │  • Monitor configuration (3 screens)                                      │  │   │
│  │  │  • CPU/Memory usage                                                       │  │   │
│  │  │  • Clipboard content                                                      │  │   │
│  │  │  • Recent files                                                           │  │   │
│  │  │  • App open/close events                                                  │  │   │
│  │  │  • Revit MCP connection status                                            │  │   │
│  │  └──────────────────────────────────────────────────────────────────────────┘  │   │
│  │                                                                                 │   │
│  └────────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                        │
└────────────────────────────────────────────────────────────────────────────────────────┘


┌────────────────────────────────────────────────────────────────────────────────────────┐
│                              WINDOWS NATIVE APPLICATIONS                                │
│                                                                                         │
│  ┌────────────────────────────────────────────────────────────────────────────────┐    │
│  │                         REVIT 2026 + MCP BRIDGE                                 │    │
│  │                                                                                 │    │
│  │   ┌─────────────────────────────────────────────────────────────────────────┐  │    │
│  │   │                    RevitMCPBridge2026.dll                                │  │    │
│  │   │                                                                          │  │    │
│  │   │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌─────────────┐  │  │    │
│  │   │  │ MCPServer.cs │  │MethodFiles  │  │Intelligence │  │ AgentFrame- │  │  │    │
│  │   │  │              │  │ (35 files)   │  │   Layer     │  │   work      │  │  │    │
│  │   │  │ Named Pipe:  │  │              │  │             │  │             │  │  │    │
│  │   │  │ RevitMCP-    │  │• WallMethods │  │• Layout-    │  │• In-Revit   │  │  │    │
│  │   │  │ Bridge2026   │  │• ViewMethods │  │  Intelligence│  │  Haiku AI  │  │  │    │
│  │   │  │              │  │• SheetMethods│  │• Proactive- │  │• Tool Defs  │  │  │    │
│  │   │  │ 705 methods  │  │• RoomMethods │  │  Monitor    │  │• Safe Cmd   │  │  │    │
│  │   │  │ registered   │  │• BatchMethods│  │• Correction-│  │  Processor  │  │  │    │
│  │   │  │              │  │• DetailMthds │  │  Learner    │  │             │  │  │    │
│  │   │  │              │  │• MEPMethods  │  │• Preference │  │             │  │  │    │
│  │   │  │              │  │• 28 more...  │  │  Memory     │  │             │  │  │    │
│  │   │  └──────────────┘  └──────────────┘  └──────────────┘  └─────────────┘  │  │    │
│  │   │                                                                          │  │    │
│  │   │  ┌──────────────────────────────────────────────────────────────────┐   │  │    │
│  │   │  │                    KNOWLEDGE BASE (121 files)                     │   │  │    │
│  │   │  │  • Building Types (17)    • Codes/Standards (15)                  │   │  │    │
│  │   │  │  • MEP Systems (10)       • Project Delivery (10)                 │   │  │    │
│  │   │  │  • Structural (12)        • Documentation (7)                     │   │  │    │
│  │   │  │  • Workflows (4)          • User Preferences                      │   │  │    │
│  │   │  └──────────────────────────────────────────────────────────────────┘   │  │    │
│  │   │                                                                          │  │    │
│  │   └──────────────────────────────────────────────────────────────────────────┘  │    │
│  │                                         │                                       │    │
│  │                                         ▼                                       │    │
│  │                              ┌──────────────────┐                               │    │
│  │                              │   REVIT API      │                               │    │
│  │                              │   (.NET 4.8)     │                               │    │
│  │                              │                  │                               │    │
│  │                              │   Document       │                               │    │
│  │                              │   UIDocument     │                               │    │
│  │                              │   Application    │                               │    │
│  │                              └──────────────────┘                               │    │
│  │                                                                                 │    │
│  └─────────────────────────────────────────────────────────────────────────────────┘    │
│                                                                                         │
│  ┌─────────────────────┐  ┌─────────────────────┐  ┌─────────────────────┐             │
│  │   Bluebeam Revu     │  │    Chrome/Edge      │  │   File System       │             │
│  │                     │  │                     │  │                     │             │
│  │   PDF markup &      │  │   Web automation    │  │   D:\ drive         │             │
│  │   annotations       │  │   via MCP           │  │   (projects, tools) │             │
│  └─────────────────────┘  └─────────────────────┘  └─────────────────────┘             │
│                                                                                         │
└─────────────────────────────────────────────────────────────────────────────────────────┘


┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                              ML / VISION PIPELINE                                        │
│                                                                                          │
│  ┌────────────────────────────────────────────────────────────────────────────────┐     │
│  │                              FloorPlanML                                        │     │
│  │                         (D:\FloorPlanML)                                        │     │
│  │                                                                                 │     │
│  │  PDF/Image ──► ┌─────────────┐    ┌─────────────┐    ┌─────────────┐           │     │
│  │                │ YOLOv8      │    │ PyMuPDF     │    │ OpenCV      │           │     │
│  │                │ (ML detect) │    │ (Vector)    │    │ (Perimeter) │           │     │
│  │                │ 10.5% prec  │    │ extraction  │    │ tracing     │           │     │
│  │                └──────┬──────┘    └──────┬──────┘    └──────┬──────┘           │     │
│  │                       │                  │                  │                   │     │
│  │                       └──────────────────┼──────────────────┘                   │     │
│  │                                          ▼                                      │     │
│  │                              ┌─────────────────┐                                │     │
│  │                              │ Unified         │                                │     │
│  │                              │ Orchestrator    │                                │     │
│  │                              │                 │                                │     │
│  │                              │ Hybrid mode:    │                                │     │
│  │                              │ Vector + ML     │                                │     │
│  │                              └────────┬────────┘                                │     │
│  │                                       │                                         │     │
│  │                                       ▼                                         │     │
│  │                              ┌─────────────────┐                                │     │
│  │                              │ Wall Coords     │──────► RevitMCPBridge2026     │     │
│  │                              │ (JSON)          │        createWallsFromPolyline │     │
│  │                              └─────────────────┘                                │     │
│  │                                                                                 │     │
│  └─────────────────────────────────────────────────────────────────────────────────┘     │
│                                                                                          │
│  ┌────────────────────────────────────────────────────────────────────────────────┐     │
│  │                         CAD Extraction Pipeline                                 │     │
│  │                                                                                 │     │
│  │  DXF/DWG ──► Layer Filter ──► Centerline Extract ──► Wall Coords ──► Revit    │     │
│  │              (A-WALL, etc)    (~21ft accuracy)        (JSON)                    │     │
│  │                                                                                 │     │
│  └─────────────────────────────────────────────────────────────────────────────────┘     │
│                                                                                          │
└──────────────────────────────────────────────────────────────────────────────────────────┘


┌──────────────────────────────────────────────────────────────────────────────────────────┐
│                                 SUPPORTING TOOLS                                          │
│                              (D:\_CLAUDE-TOOLS - 47 tools)                                │
│                                                                                           │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐      │
│  │ orchestration/  │  │ pipelines/      │  │ bim-validator/  │  │ visual-review/  │      │
│  │                 │  │                 │  │                 │  │                 │      │
│  │ methods-index   │  │ cd-set-assembly │  │ Post-operation  │  │ Spatial logic   │      │
│  │ knowledge-trigg │  │ markup-to-model │  │ BIM checks      │  │ verification    │      │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘  └─────────────────┘      │
│                                                                                           │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐      │
│  │ ai-render/      │  │ perimeter-      │  │ Claude_Skills/  │  │ revit-startup-  │      │
│  │                 │  │ tracer/         │  │                 │  │ helper/         │      │
│  │ Flux ControlNet │  │ Wall geometry   │  │ Domain          │  │ Dialog          │      │
│  │ + SDXL          │  │ extraction      │  │ expertise (17)  │  │ dismisser       │      │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘  └─────────────────┘      │
│                                                                                           │
└───────────────────────────────────────────────────────────────────────────────────────────┘


═══════════════════════════════════════════════════════════════════════════════════════════
                                    DATA FLOW SUMMARY
═══════════════════════════════════════════════════════════════════════════════════════════

  USER (Voice/Text)
       │
       ▼
  CLAUDE CODE (WSL) ◄────────────────────────────────────────┐
       │                                                      │
       ├───► claude-memory (persistent context) ─────────────►│
       │                                                      │
       ├───► system-bridge (live Windows state) ─────────────►│
       │                                                      │
       ├───► voice-mcp (audio feedback) ◄────────────────────┤
       │                                                      │
       └───► RevitMCPBridge2026 ───► Revit API ───► .rvt ────┘
                    │
                    ├── 705 methods
                    ├── 121 knowledge files
                    └── Intelligence layer

═══════════════════════════════════════════════════════════════════════════════════════════
```

## Connection Types

| Connection | Protocol | Direction |
|------------|----------|-----------|
| Claude Code ↔ MCP Servers | stdio (JSON-RPC) | Bidirectional |
| Claude Code ↔ RevitMCPBridge | Named Pipe | Bidirectional |
| System Bridge → live_state.json | File write | One-way (10s interval) |
| Voice MCP → Windows | Edge TTS API | One-way |
| Bluebeam MCP → Bluebeam | COM Automation | Bidirectional |
| FloorPlanML → Revit | Via RevitMCPBridge | One-way |

## Key Integration Points

### 1. RevitMCPBridge (Primary)
- **Pipe:** `\\.\pipe\RevitMCPBridge2026`
- **Protocol:** JSON messages `{"method": "...", "params": {...}}`
- **Methods:** 705 registered
- **Knowledge:** 121 markdown files

### 2. Memory System (Persistence)
- **Database:** `D:\_CLAUDE-TOOLS\claude-memory-server\memory.db`
- **Memories:** 549 stored
- **Features:** Full-text search, semantic search, corrections, patterns

### 3. System Bridge (Awareness)
- **State File:** `D:\_CLAUDE-TOOLS\system-bridge\live_state.json`
- **Update Interval:** 10 seconds
- **Tracks:** Apps, monitors, clipboard, recent files, Revit status

### 4. FloorPlanML (Vision)
- **Location:** `D:\FloorPlanML`
- **Pipelines:** YOLO (10.5%), PyMuPDF (vector), OpenCV (perimeter)
- **Output:** Wall coordinates → Revit via MCP

## Startup Sequence

```
1. Windows boots
   └── System Bridge daemon starts (Startup folder)
       └── Writes live_state.json every 10s

2. User opens Revit 2026
   └── RevitMCPBridge2026.dll loads (addin)
       └── Named pipe server starts
       └── 705 methods registered

3. User starts Claude Code
   └── Reads live_state.json (knows Revit is open)
   └── Loads memory_smart_context
   └── Ready for commands
```

---

## Banana Chat — AgentCore Architecture (v0.4+)

Banana Chat is the in-Revit AI chat panel (`AgentChatPanel.cs`). It drives a full agentic loop
via `AgentCore.cs` using raw HTTP to the Anthropic Messages API (no SDK).

### API Call Structure

Every call to Claude is built in `CallClaudeAsync` as:

```
POST https://api.anthropic.com/v1/messages
{
  model, max_tokens,
  cache_control: { type: "ephemeral" },          ← automatic caching on message history
  system: [{ type: "text", text: "...", cache_control: { type: "ephemeral" } }],
  tools: [ ...all tools..., { ..., cache_control: { type: "ephemeral" } } ],  ← last tool only
  messages: [ ...conversation history... ],
  stream: true
}
```

### Prompt Caching Strategy (v0.4.20260428)

Three explicit cache breakpoints using 5-minute TTL:

| Breakpoint | What it covers | Change frequency |
|---|---|---|
| Last tool definition | All tool definitions | Static — never changes within a session |
| System block | persistentIntelBlock (firm memory, rules) | Per session — same across all turns |
| Automatic (top-level) | Growing message history | Per turn — advances each call |

**Why this matters for agentic loops:** Barrett's sessions average ~158 API calls (each tool
execution is a new API call). Without caching, tools + system are priced at $3/MTok × 158 times.
With caching, they are written once at $3.75/MTok then read at $0.30/MTok — ~90% savings on
the static prefix for sessions with many tool calls.

**Scaling benefit:** Going from 54 → 100 tools with caching costs ~$0.47/session extra vs
~$4.50/session extra without caching. Caching makes tool count a session-fixed cost, not a
per-call multiplier.

### Token Tracking

`UsageInfo` captures four fields from every streaming response (`message_start` SSE event):

| Field | API key | Meaning |
|---|---|---|
| `InputTokens` | `input_tokens` | Uncached tokens after last breakpoint |
| `OutputTokens` | `output_tokens` | Generated tokens |
| `CacheReadInputTokens` | `cache_read_input_tokens` | Tokens served from cache (0.1x price) |
| `CacheCreationInputTokens` | `cache_creation_input_tokens` | Tokens written to cache (1.25x price) |

All four are accumulated at session level and sent in `session_outcome` telemetry.

**Status bar display:** `↑ 12K  ↓ 956  ⚡ 200K cached` — the `⚡` counter appears once
caching is active (second API call onward in a session).

**Cost formula:**
```
cost = (inputTokens / 1M × basePrice)
     + (cacheCreation / 1M × basePrice × 1.25)
     + (cacheRead / 1M × basePrice × 0.10)
     + (outputTokens / 1M × outputPrice)
```

### Federated Learning — Corrections System

Three paths for Barrett to submit a correction:

1. **Conversational** — Barrett says something negative after a write op, watches Claude fix it,
   then says "done". Banana Chat injects a `CORRECTION DIFF: trigger=<op>` message; Claude
   synthesizes a rule and calls `memoryStoreCorrection`, which POSTs to `/api/corrections`.

2. **Wrench button (🔧)** — Appears after any write operation. Starts the correction watcher
   explicitly without requiring negative language.

3. **Thumbs-down (👎)** — Dialog asks what went wrong. Rule is POSTed directly to
   `/api/corrections`, bypassing the diff flow.

**Backend storage:** `banana_corrections` table on Railway PostgreSQL:
- `firm_id`, `trigger_operation`, `natural_language_rule`, `before_state`, `after_state`
- `confirmed`, `promoted_to_layer1`, `promoted_at`

**Admin review** at `/admin` → Corrections tab:
- Rolling list of all corrections (respects excludeFirms filter for Eric/Barrett)
- Patterns section: corrections from 2+ firms on the same `trigger_operation` → eligible
  for promotion to Layer 1 (system prompt)
- "Promote to Layer 1" button marks the pattern; Eric manually adds the rule to
  `persistentIntelBlock` in `AgentChatPanel.cs`

### Railway Backend Endpoints (BIM Monkey)

| Endpoint | Auth | Purpose |
|---|---|---|
| `POST /api/corrections` | Bearer API key | Plugin stores a captured correction |
| `GET /api/admin/corrections` | x-admin-key | Rolling list + cross-firm patterns |
| `PATCH /api/admin/corrections/:id/promote` | x-admin-key | Mark pattern as promoted |
| `POST /api/telemetry` | Bearer API key | Plugin session events (session_start, tool_call, session_outcome) |
| `GET /api/admin/plugin-telemetry` | x-admin-key | Aggregated plugin usage stats |
