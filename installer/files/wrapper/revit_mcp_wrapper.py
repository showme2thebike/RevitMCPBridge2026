#!/usr/bin/env python3
"""
RevitMCPBridge - MCP Wrapper for Claude
Bridges Claude (stdio MCP) to Revit (Windows named pipes).

Usage:
  python revit_mcp_wrapper.py                    # defaults to RevitMCPBridge2026
  REVIT_PIPE_NAME=RevitMCPBridge2025 python revit_mcp_wrapper.py

This script is the MCP server that Claude connects to. It translates
Claude's MCP tool calls into named pipe messages that Revit understands.
"""
import subprocess
import json
import sys
import os
import time
import logging
import threading
import re
import urllib.request
import urllib.error
from mcp.server.fastmcp import FastMCP

# Redirect all logging to stderr so stdout stays clean for MCP JSON-RPC
logging.basicConfig(stream=sys.stderr, level=logging.WARNING)

# Configurable pipe name - matches the Revit version you're running
PIPE_NAME = os.environ.get("REVIT_PIPE_NAME", "RevitMCPBridge2026")
SERVER_NAME = f"revit-bridge-{PIPE_NAME.replace('RevitMCPBridge', '')}"
BIM_MONKEY_API_KEY = os.environ.get("BIM_MONKEY_API_KEY", "")
BIM_MONKEY_API_URL = "https://bimmonkey-production.up.railway.app"

mcp = FastMCP(SERVER_NAME)

SHEET_PLACEABLE_TYPES = {"FloorPlan", "CeilingPlan", "Elevation", "Section",
                          "Detail", "DraftingView", "Legend", "Schedule",
                          "EngineeringPlan", "AreaPlan"}


def _clean_str(s):
    """Strip control characters and chars that break Claude's JSON output.

    Double-quotes inside Revit names (e.g. 'Concrete 6"') get embedded in
    the generation prompt verbatim. When Claude copies them into its JSON
    response without escaping, JSON.parse fails. Replace with inch symbol.
    """
    if not isinstance(s, str):
        return s
    s = ''.join(c for c in s if ord(c) >= 32 or c == '\t').strip()
    s = s.replace('"', '\u2033')  # replace " with ″ (double prime / inch symbol)
    return s


def _get_revit_array(container, array_key):
    """Extract an array from a Revit response object or direct list."""
    if isinstance(container, list):
        return container
    if isinstance(container, dict):
        v = container.get(array_key)
        if isinstance(v, list):
            return v
    return []


def build_model_summary(raw_model):
    """
    Transform raw Revit method responses into a lean, structured model summary.

    Revit methods return nested objects like {"success": true, "views": [...]}.
    The backend expects flat top-level arrays: existingViews, existingSheets, levels, etc.
    This function reshapes AND prunes to only the fields the backend AI actually needs.

    A 500-view model goes from ~500KB to ~20KB after this transform.
    """
    # Views — filter to sheet-placeable types, keep only id/name/viewType
    raw_views = _get_revit_array(raw_model.get("views", {}), "views")
    existing_views = [
        {"id": v["id"], "name": _clean_str(v["name"]), "viewType": v["viewType"]}
        for v in raw_views
        if v.get("viewType") in SHEET_PLACEABLE_TYPES and v.get("id") and v.get("name")
    ]
    existing_drafting_views = [
        {"id": v["id"], "name": _clean_str(v["name"])}
        for v in raw_views
        if v.get("viewType") == "DraftingView" and v.get("id") and v.get("name")
    ]

    # Sheets — keep sheetNumber, sheetName, and placedViews (view IDs already on the sheet).
    # placedViews is used by the backend to mark which views are already placed so the
    # plan can set requiresDuplicate correctly instead of discovering conflicts at runtime.
    raw_sheets = _get_revit_array(raw_model.get("sheets", {}), "sheets")
    existing_sheets = [
        {
            "sheetNumber": _clean_str(s.get("sheetNumber")),
            "sheetName": _clean_str(s.get("sheetName") or s.get("name")),
            "placedViews": s.get("placedViews") or [],
        }
        for s in raw_sheets
        if s.get("sheetNumber")
    ]

    # Levels — keep id, name, elevation
    raw_levels = _get_revit_array(raw_model.get("levels", {}), "levels")
    levels = [
        {"id": l.get("id"), "name": _clean_str(l.get("name")), "elevation": round(float(l.get("elevation", 0) or 0), 2)}
        for l in raw_levels
        if l.get("name")
    ]

    # Rooms — keep id, name, number, area, levelName
    raw_rooms = _get_revit_array(raw_model.get("rooms", {}), "rooms")
    rooms = [
        {
            "id": r.get("id"),
            "name": _clean_str(r.get("name")),
            "number": _clean_str(r.get("number")),
            "area": round(float(r.get("area", 0) or 0), 1),
            "levelName": _clean_str(r.get("levelName") or r.get("level")),
        }
        for r in raw_rooms
        if r.get("name")
    ]

    # Wall types — keep id, name, width, function (drop all layer details)
    raw_wall_types = _get_revit_array(raw_model.get("wallTypes", {}), "wallTypes")
    wall_types = [
        {
            "id": wt.get("id"),
            "name": _clean_str(wt.get("name")),
            "width": round(float(wt.get("width", 0) or 0), 3),
            "function": _clean_str(wt.get("function")),
        }
        for wt in raw_wall_types
        if wt.get("name")
    ]

    # Project info — essentials only
    doc = raw_model.get("documentInfo", {})
    if not isinstance(doc, dict):
        doc = {}
    pi = doc.get("projectInfo") or {}
    if not isinstance(pi, dict):
        pi = {}
    project_info = {k: v for k, v in {
        "name": _clean_str(pi.get("name")),
        "address": _clean_str(pi.get("address")),
        "number": _clean_str(pi.get("number")),
    }.items() if v}

    project_name = (
        raw_model.get("projectName") or
        project_info.get("name") or
        _clean_str(doc.get("title")) or
        "Unnamed Project"
    )

    return {
        "projectName": project_name,
        "projectInfo": project_info,
        "buildingType": raw_model.get("buildingType", "residential"),
        "conditions": raw_model.get("conditions", []),
        "levels": levels,
        "rooms": rooms,
        "existingSheets": existing_sheets,
        "wallTypes": wall_types,
        "existingViews": existing_views,
        "existingDraftingViews": existing_drafting_views,
    }


# ── Persistent pipe bridge ────────────────────────────────────────────────
# Instead of spawning a new powershell.exe per call (~300-500ms each), we keep
# one PowerShell process alive that holds the Revit named pipe connection and
# forwards JSON request/response pairs over its stdin/stdout.
#
# The bridge script (pipe_bridge.ps1) lives alongside this file. It:
#   1. Connects to the Revit named pipe
#   2. Writes "READY" to stderr once connected
#   3. Loops: read JSON line from stdin → write to pipe → read response → write to stdout
#
# _bridge_lock serialises the request/response cycle and bridge lifecycle.
# All MCP tool calls are sequential (Claude doesn't parallelise), so one lock is fine.

_bridge_proc = None
_bridge_lock  = threading.Lock()
_BRIDGE_SCRIPT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "pipe_bridge.ps1")

# Auto-keepalive: send a proactive ping every N non-ping calls to prevent
# the named pipe from dropping silently under sustained generation load.
_call_counter   = 0
_KEEPALIVE_EVERY = 10


def _start_bridge():
    """Start pipe_bridge.ps1 and block until it signals READY (or fails)."""
    proc = subprocess.Popen(
        ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass",
         "-File", _BRIDGE_SCRIPT, "-PipeName", PIPE_NAME, "-ConnectTimeoutMs", "120000"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        bufsize=1,
    )

    ready  = threading.Event()
    err_msg = [None]

    def _watch_stderr():
        for line in proc.stderr:
            s = line.strip()
            if s == "READY":
                ready.set()
            elif s.startswith("ERROR:") or s == "DISCONNECTED":
                err_msg[0] = s
                ready.set()

    threading.Thread(target=_watch_stderr, daemon=True).start()

    if not ready.wait(timeout=35):
        proc.kill()
        raise TimeoutError(
            f"Bridge timed out connecting to Revit pipe '{PIPE_NAME}' (35s). "
            "Is Revit running with MCP Bridge loaded?"
        )

    if err_msg[0] or proc.poll() is not None:
        proc.kill()
        raise RuntimeError(f"Bridge failed to connect: {err_msg[0] or 'process exited'}")

    return proc


def _ensure_bridge():
    """Return the live bridge, starting one if needed. Caller must hold _bridge_lock."""
    global _bridge_proc
    if _bridge_proc is None or _bridge_proc.poll() is not None:
        _bridge_proc = _start_bridge()
    return _bridge_proc


# Per-method read timeouts (seconds).  Heavy write operations can hold the
# Revit main thread for 60-120 s; we need to wait that long before giving up
# and killing the bridge.  Without a timeout, readline() blocks forever and
# Claude's own MCP client fires first — leaving the bridge alive with a
# stale response in its buffer, desyncing every subsequent call.
_METHOD_TIMEOUTS = {
    "drawLayerStack":      180,
    "executePlan":          30,   # async dispatch — returns jobId in <5s; sync fallback uses 600s via _execute_plan_request directly
    "getExecutionStatus":   15,   # pure dict read, no Revit API
    "runFinishingPhase":   180,
    "createSheet":          90,
    "placeViewOnSheet":     90,
    "placeScheduleOnSheet": 90,
    "createSchedule":       90,
    "batchTagDoors":       120,
    "batchTagWindows":     120,
    "batchTagRooms":       120,
    "fitCropBoxToRooms":    90,
    "applyViewTemplates":        90,
    "exportBimMonkeySheetsToPNG": 600,  # 17+ sheets @ ~15s each = 4-5 min observed
}
_EXECUTE_PLAN_SYNC_TIMEOUT = 1800  # seconds for synchronous executePlan fallback
_DEFAULT_READ_TIMEOUT = 60  # seconds for lightweight read-only calls


def _readline_with_timeout(stream, timeout_seconds):
    """Read one line from *stream* with a hard timeout.
    Returns the line string on success, or None on timeout."""
    result = [None]

    def _read():
        try:
            result[0] = stream.readline()
        except Exception:
            pass

    t = threading.Thread(target=_read, daemon=True)
    t.start()
    t.join(timeout_seconds)
    return result[0]  # None if thread still running (timed out)


def call_revit(method: str, params: dict = None) -> dict:
    """Send a JSON-RPC request to Revit via the persistent pipe bridge."""
    global _bridge_proc, _call_counter
    if params is None:
        params = {}

    # Auto-keepalive: every N non-ping calls send a proactive ping first.
    # This prevents the named pipe from dropping silently under sustained load
    # (e.g. rapid createSheet/placeViewOnSheet sequences during generation).
    if method != "ping":
        _call_counter += 1
        if _call_counter % _KEEPALIVE_EVERY == 0:
            try:
                call_revit("ping", {})
            except Exception:
                pass

    request_json = json.dumps({
        "jsonrpc": "2.0",
        "method": method,
        "params": params,
        "id": 1
    })

    read_timeout = _METHOD_TIMEOUTS.get(method, _DEFAULT_READ_TIMEOUT)

    # Retry with backoff on transient pipe/bridge failures.
    # Revit's pipe can go silent for 30-60s during model regeneration after
    # write operations; we reconnect and retry rather than surfacing the error.
    RETRY_DELAYS = [0, 5, 15, 30]
    last_error = "Unknown error"

    for attempt, delay in enumerate(RETRY_DELAYS):
        if delay > 0:
            time.sleep(delay)

        with _bridge_lock:
            try:
                bridge = _ensure_bridge()
                bridge.stdin.write(request_json + "\n")
                bridge.stdin.flush()
                line = _readline_with_timeout(bridge.stdout, read_timeout)

                if line is None:
                    # Timeout — kill bridge so the next attempt starts clean.
                    # Without this, the stale response sits in stdout and
                    # desyncs every subsequent call.
                    try:
                        bridge.kill()
                    except Exception:
                        pass
                    _bridge_proc = None
                    last_error = (
                        f"Revit did not respond to '{method}' within {read_timeout}s "
                        f"(attempt {attempt + 1}) — bridge reset"
                    )
                    continue

                if not line:
                    # Pipe closed by Revit (server stopped, crash, etc.) — reconnect next round
                    _bridge_proc = None
                    last_error = "Bridge disconnected (Revit pipe closed)"
                    continue

                response = json.loads(line.strip())
                err = response.get("error", "")

                # Retry on well-known transient Revit-side pipe errors
                if not response.get("success", True):
                    el = err.lower()
                    if ("semaphore" in el or "pipe is broken" in el
                            or ("connect" in el and "timed out" in el)):
                        _bridge_proc = None   # force fresh connection
                        last_error = err
                        continue

                return response

            except (BrokenPipeError, OSError):
                _bridge_proc = None
                last_error = f"Bridge I/O error (attempt {attempt + 1})"
            except json.JSONDecodeError:
                return {"success": False,
                        "error": f"Invalid response from Revit: {(line or '')[:200]}"}
            except (TimeoutError, RuntimeError) as e:
                return {"success": False, "error": str(e)}
            except FileNotFoundError:
                return {"success": False,
                        "error": "PowerShell not found. This wrapper requires Windows with PowerShell."}
            except Exception as e:
                return {"success": False, "error": str(e)}

    return {"success": False,
            "error": f"{last_error}. Make sure Revit is open and MCP Bridge is loaded. (Pipe: {PIPE_NAME})"}


@mcp.tool()
async def revit_ping() -> dict:
    """Test connection to Revit. Returns Revit version and project info if connected."""
    return call_revit("ping")


@mcp.tool()
async def revit_get_levels() -> dict:
    """Get all levels in the current Revit document."""
    return call_revit("getLevels")


@mcp.tool()
async def revit_get_active_view() -> dict:
    """Get the currently active view in Revit."""
    return call_revit("getActiveView")


@mcp.tool()
async def revit_get_document_info() -> dict:
    """Get information about the active Revit document (name, path, phase, etc.)."""
    return call_revit("getDocumentInfo")


@mcp.tool()
async def revit_get_wall_types() -> dict:
    """Get all available wall types in the document."""
    return call_revit("getWallTypes")


@mcp.tool()
async def revit_get_sheets() -> dict:
    """Get all sheets in the document."""
    return call_revit("getSheets")


@mcp.tool()
async def revit_create_wall(
    start_x: float,
    start_y: float,
    end_x: float,
    end_y: float,
    height: float = 10.0,
    wall_type_id: int = None,
    level_id: int = None
) -> dict:
    """Create a wall in Revit between two points."""
    params = {
        "startPoint": {"x": start_x, "y": start_y, "z": 0},
        "endPoint": {"x": end_x, "y": end_y, "z": 0},
        "height": height
    }
    if wall_type_id:
        params["wallTypeId"] = wall_type_id
    if level_id:
        params["levelId"] = level_id
    return call_revit("createWall", params)


@mcp.tool()
async def revit_execute(method_name: str, parameters: str = "{}") -> dict:
    """
    Execute ANY RevitMCPBridge method by name. This is the universal tool
    that gives you access to all 1,114 methods (2026) or 437 methods (2025).

    Args:
        method_name: The method to call (e.g., 'getRooms', 'placeDoor', 'createSheet',
                     'getElements', 'setParameter', 'createSchedule', etc.)
        parameters: JSON string of parameters to pass to the method.

    Examples:
        revit_execute("getRooms", "{}")
        revit_execute("placeDoor", '{"wallId": 12345, "location": {"x": 10, "y": 5}}')
        revit_execute("createSheet", '{"number": "A101", "name": "Floor Plan"}')

    Returns:
        Result dict from Revit with 'success' flag and data or error.
    """
    try:
        params = json.loads(parameters) if parameters else {}
    except json.JSONDecodeError:
        return {"success": False, "error": f"Invalid JSON parameters: {parameters[:200]}"}
    return call_revit(method_name, params)


@mcp.tool()
async def bim_monkey_generate(model_data: str) -> dict:
    """
    Submit Revit model data to the BIM Monkey backend and receive a complete
    CD plan trained on this firm's drawing standards.

    THIS MUST BE CALLED BEFORE CREATING ANY SHEETS OR VIEWS.
    Never improvise a CD plan — always use this tool to get the plan from the backend.

    Args:
        model_data: JSON string of the full Revit model snapshot. Build this by calling
                    revit_execute for: getDocumentInfo, getLevels, getRooms, getSheets,
                    getWallTypes, getViews — then combine into one object.
                    Or call bim_monkey_run() to do this automatically.

    Returns:
        {
          "success": true,
          "generationId": 42,
          "plan": {
            "sheets": [...],
            "detailPlan": [...],
            "schedulePlan": [...]
          },
          "warnings": [...]   # cross-reference errors only — act on these before executing
        }

    Redline pre-check: if the user has loaded a redline PDF, call bim_monkey_get_redlines()
    first and incorporate any changes. Redline corrections override the default plan.
    """
    if not BIM_MONKEY_API_KEY:
        return {"success": False, "error": "BIM_MONKEY_API_KEY not set in environment."}

    try:
        # Revit address/notes fields can contain literal CR/LF which produce invalid JSON.
        # Strip them before parsing — sanitizeModelData on the server runs after parse.
        if isinstance(model_data, str):
            model_data = ''.join(c if c not in ('\r', '\n') else ' ' for c in model_data)
        raw_model = json.loads(model_data) if isinstance(model_data, str) else model_data
    except json.JSONDecodeError as e:
        return {"success": False, "error": f"Invalid model_data JSON: {e}"}

    # Reshape raw Revit responses into lean, structured model summary.
    # This fixes the backend's existingViews/existingSheets/levels extraction
    # (which expects flat top-level arrays, not nested Revit response objects)
    # and prunes all fields the AI doesn't need — reducing a 500-view model
    # from ~500KB to ~20KB.
    model_obj = build_model_summary(raw_model)

    payload = json.dumps(model_obj).encode("utf-8")
    req = urllib.request.Request(
        f"{BIM_MONKEY_API_URL}/api/generate",
        data=payload,
        headers={
            "Content-Type": "application/json",
            "Authorization": f"Bearer {BIM_MONKEY_API_KEY}",
        },
        method="POST",
    )

    try:
        with urllib.request.urlopen(req, timeout=300) as resp:
            raw = b""
            while True:
                chunk = resp.read(8192)
                if not chunk:
                    break
                raw += chunk
            # Strip leading keep-alive newlines the server sends during generation
            data = json.loads(raw.decode("utf-8").strip())
            if not data.get("success"):
                return {"success": False, "error": data.get("error", "Backend returned failure")}
            # crossRefWarnings = actionable errors (orphan sheetReference, missing detail refs)
            # blankSheetWarnings = planning-time name-match failures; often resolve at execute time
            # Only surface crossRefWarnings as warnings[] — blankSheetWarnings are diagnostics only.
            cross_ref_warnings = data.get("crossRefWarnings") or []
            return {
                "success": True,
                "generationId": data.get("generationId"),
                "plan": data.get("plan"),
                "examplesUsed": data.get("examplesUsed", 0),
                "warnings": cross_ref_warnings,
            }
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="replace")[:500]
        if e.code == 401:
            key_preview = BIM_MONKEY_API_KEY[:12] + "..." if len(BIM_MONKEY_API_KEY) > 12 else BIM_MONKEY_API_KEY
            key_len = len(BIM_MONKEY_API_KEY)
            hint = " (key looks doubled — expected ~49 chars)" if key_len > 60 else ""
            return {"success": False, "error": (
                f"BIM Monkey API key rejected (401){hint}. "
                f"Key in use: '{key_preview}' ({key_len} chars). "
                f"Check BIM_MONKEY_API_KEY in Documents\\BIM Monkey\\.mcp.json — "
                f"it should be ~49 characters starting with bm_. "
                f"Reinstall the plugin to re-enter your key."
            )}
        return {"success": False, "error": f"HTTP {e.code}: {body}"}
    except urllib.error.URLError as e:
        return {"success": False, "error": f"Network error: {e.reason}"}
    except Exception as e:
        return {"success": False, "error": str(e)}


def _execute_plan_request(plan_dict: dict) -> dict:
    """
    Send a single executePlan request via the persistent pipe bridge using async dispatch.

    Flow:
      1. Send executePlan with "async":true  →  Revit returns {jobId} immediately (<5 s)
      2. Poll getExecutionStatus every 5 s until status is Complete or Failed (max 30 min)
      3. Return the execution result

    If the C# plugin does not support async (old DLL), the dispatch falls back to
    synchronous execution on the Revit side; we detect this by checking whether the
    response contains a "jobId" field and handle both cases.
    """
    _POLL_INTERVAL = 5    # seconds between status polls
    _MAX_WAIT      = 1800 # 30-minute ceiling

    # ── Step 1: dispatch ───────────────────────────────────────────────────────
    dispatch = call_revit("executePlan", {"plan": plan_dict, "async": True})

    if not dispatch.get("success", False):
        return dispatch  # pipe/bridge error

    job_id = dispatch.get("jobId")
    if not job_id:
        # Old plugin (sync path): dispatch returned the full result directly
        return dispatch

    # ── Step 2: poll ───────────────────────────────────────────────────────────
    elapsed = 0
    while elapsed < _MAX_WAIT:
        time.sleep(_POLL_INTERVAL)
        elapsed += _POLL_INTERVAL

        status_resp = call_revit("getExecutionStatus", {"jobId": job_id})
        if not status_resp.get("success", False):
            # Transient bridge error — keep polling; call_revit already retried internally
            continue

        status = status_resp.get("status", "")

        if status == "Complete":
            result = status_resp.get("result") or {}
            if isinstance(result, dict):
                result.setdefault("success", True)
            return result

        if status == "Failed":
            return {"success": False, "error": status_resp.get("error", "Async executePlan failed")}

        # Queued / Running — keep waiting

    return {
        "success": False,
        "error": (
            f"executePlan async job {job_id} timed out after {_MAX_WAIT}s. "
            "Revit may still be processing — check model state before retrying. "
            "Call getExecutionStatus manually if needed."
        )
    }


def _normalize_sheet_num(num: str) -> str:
    """Normalize sheet number for fuzzy comparison: strip leading zeros after dot.
    E.g. 'A4.01' -> 'A4.1', 'G0.01a' -> 'G0.1a', 'A1.1' -> 'A1.1' (unchanged).
    """
    import re
    if not num:
        return num
    return re.sub(r'\.0+(\d)', r'.\1', num)


def _validate_plan(plan: dict) -> dict:
    """
    Validate a CD plan before execution.
    Returns { "blockers": [...], "warnings": [...] }
    Blockers must be fixed before executing. Warnings are advisory.
    Also auto-corrects sheetReference values whose only difference from a real
    sheet number is leading zeros (e.g. 'A4.01' when the sheet is 'A4.1').
    """
    blockers = []
    warnings = []

    sheets      = plan.get("sheets",      [])
    detail_plan = plan.get("detailPlan",  [])
    sched_plan  = plan.get("schedulePlan", [])

    if not sheets and not detail_plan and not sched_plan:
        blockers.append("Plan is empty — sheets, detailPlan, and schedulePlan are all empty")
        return {"blockers": blockers, "warnings": warnings}

    # Duplicate sheet numbers
    sheet_numbers = [s.get("sheetNumber") for s in sheets if s.get("sheetNumber")]
    sheet_set = set(sheet_numbers)
    # Normalized -> canonical map for fuzzy matching
    norm_to_canonical = {_normalize_sheet_num(n): n for n in sheet_numbers}
    # Name -> sheet number map for name-based fallback
    sheet_name_to_num = {s.get("sheetName", "").lower(): s.get("sheetNumber") for s in sheets if s.get("sheetNumber")}
    dupes = sorted({n for n in sheet_numbers if sheet_numbers.count(n) > 1})
    if dupes:
        blockers.append(f"Duplicate sheet numbers in plan: {', '.join(dupes)}")

    # Orphan sheetReference in detailPlan — auto-correct leading-zero mismatches
    for d in detail_plan:
        ref = d.get("sheetReference") or d.get("sheetNumber")
        if not ref or ref in sheet_set:
            continue
        # Try normalized match (e.g. A4.01 -> A4.1)
        canonical = norm_to_canonical.get(_normalize_sheet_num(ref))
        if canonical:
            # Auto-correct in place
            if "sheetReference" in d:
                d["sheetReference"] = canonical
            elif "sheetNumber" in d:
                d["sheetNumber"] = canonical
            warnings.append(
                f"detailPlan item {d.get('detailNumber', '?')}: "
                f"auto-corrected sheetReference '{ref}' → '{canonical}'"
            )
        else:
            # Name-based fallback: find a sheet whose name contains "detail"
            fallback = next((num for name, num in sheet_name_to_num.items() if "detail" in name), None)
            if fallback:
                if "sheetReference" in d:
                    d["sheetReference"] = fallback
                elif "sheetNumber" in d:
                    d["sheetNumber"] = fallback
                warnings.append(
                    f"detailPlan item {d.get('detailNumber', '?')}: "
                    f"auto-corrected sheetReference '{ref}' → '{fallback}' (name-based match)"
                )
            else:
                warnings.append(
                    f"detailPlan item {d.get('detailNumber', '?')}: "
                    f"sheetReference '{ref}' not found in sheets[] — detail will be skipped"
                )

    # schedulePlan references non-existent sheets (warning only)
    for s in sched_plan:
        ref = s.get("sheetNumber")
        if not ref or ref in sheet_set:
            continue
        canonical = norm_to_canonical.get(_normalize_sheet_num(ref))
        if canonical:
            s["sheetNumber"] = canonical
            warnings.append(f"schedulePlan '{s.get('name', '?')}': auto-corrected sheetNumber '{ref}' → '{canonical}'")
        else:
            # Name-based fallback: find a sheet whose name contains "schedule"
            fallback = next((num for name, num in sheet_name_to_num.items() if "schedule" in name), None)
            if fallback:
                s["sheetNumber"] = fallback
                warnings.append(f"schedulePlan '{s.get('name', '?')}': auto-corrected sheetNumber '{ref}' → '{fallback}' (name-based match)")
            else:
                warnings.append(f"schedulePlan '{s.get('name', '?')}': sheetNumber '{ref}' not in sheets[]")

    # Sheets with no views (non-cover, non-schedule types)
    for s in sheets:
        vtype = s.get("viewType", "")
        if not s.get("views") and vtype not in ("cover", "schedule", "keynote"):
            warnings.append(f"Sheet {s.get('sheetNumber', '?')} has no views assigned")

    return {"blockers": blockers, "warnings": warnings}


@mcp.tool()
async def bim_monkey_validate_plan(plan: dict) -> dict:
    """
    Validate a CD plan before execution.

    Run this on the plan returned by bim_monkey_generate to catch structural issues
    before any Revit writes. bim_monkey_run() calls this automatically.

    Returns:
        {
          "blockers": [...],   # must fix before executing (orphan refs, duplicate sheets)
          "warnings": [...]    # advisory (empty sheets, schedule sheet not in plan)
        }
    """
    return _validate_plan(plan)


def _collect_model_data() -> dict:
    """Collect all required Revit model data in one place."""
    return {
        "documentInfo": call_revit("getDocumentInfo"),
        "levels":        call_revit("getLevels"),
        "rooms":         call_revit("getRooms"),
        "sheets":        call_revit("getSheets"),
        "wallTypes":     call_revit("getWallTypes"),
        "views":         call_revit("getViews"),
    }


def _fetch_generation(generation_id: int) -> dict:
    """Fetch an existing generation record from the backend."""
    req = urllib.request.Request(
        f"{BIM_MONKEY_API_URL}/api/generation/{generation_id}",
        headers={"Authorization": f"Bearer {BIM_MONKEY_API_KEY}"},
    )
    with urllib.request.urlopen(req, timeout=15) as resp:
        data = json.loads(resp.read().decode("utf-8"))
        return data.get("generation", {})


@mcp.tool()
async def bim_monkey_run(
    chunk_size: int = 5,
    generation_id: int = None,
) -> dict:
    """
    Full CD generation in one call — the primary entry point for Start Generation.

    Collects Revit model data, generates the CD plan from the backend, validates the plan,
    executes it in Revit (all sheets, views, details, schedules), runs the finishing phase,
    and marks the run complete. Claude only needs to call this one tool and report the result.

    Args:
        chunk_size:    Sheets per execution pass (default 5). Use 3 for elevation-heavy projects.
        generation_id: Resume a prior interrupted run by its generationId. Skips the generate
                       step and re-executes from the existing plan, skipping already-created sheets.

    Returns:
        {
          "success": true,
          "generationId": 42,
          "sheetsCreated": 14, "viewsPlaced": 22, "detailsCreated": 6, "schedulesPlaced": 3,
          "errorCount": 0, "errors": [],
          "abortedAtChunk": null,            # set if execution stopped early
          "finishing": { ... },              # runFinishingPhase result
          "planWarnings": [],                # cross-reference issues from generate
          "validationWarnings": []           # local plan validation warnings
        }
    """
    import json as _json

    # ── Step 1: get the plan ──────────────────────────────────────────────────
    plan = None
    gen_id = generation_id
    plan_warnings = []

    if generation_id:
        # Resume: fetch existing plan from backend, diff against current Revit state
        try:
            gen_record = _fetch_generation(generation_id)
            plan = gen_record.get("cd_plan")
            if not plan:
                return {"success": False, "error": f"Generation {generation_id} has no saved plan"}
        except Exception as e:
            return {"success": False, "error": f"Could not fetch generation {generation_id}: {e}"}

        # Strip already-created sheets from the plan so we don't re-run them
        try:
            existing = call_revit("getSheets")
            existing_nums = {s.get("sheetNumber") for s in (existing.get("sheets") or [])}
            original_count = len(plan.get("sheets", []))
            plan = dict(plan)
            plan["sheets"] = [s for s in plan.get("sheets", []) if s.get("sheetNumber") not in existing_nums]
            skipped = original_count - len(plan["sheets"])
            if skipped:
                plan_warnings.append(f"Resume: skipped {skipped} sheets already in Revit")
        except Exception:
            pass  # proceed with full plan if getSheets fails

    else:
        # Fresh run: collect model data and generate
        raw = _collect_model_data()
        model_json = _json.dumps(build_model_summary(raw))
        gen_result = await bim_monkey_generate(model_data=model_json)
        if not gen_result.get("success"):
            return gen_result
        plan = gen_result["plan"]
        gen_id = gen_result["generationId"]
        plan_warnings = gen_result.get("warnings", [])

    # ── Step 2: validate plan before touching Revit ───────────────────────────
    validation = _validate_plan(plan)
    if validation["blockers"]:
        return {
            "success": False,
            "generationId": gen_id,
            "blockers": validation["blockers"],
            "warnings": validation["warnings"],
            "planWarnings": plan_warnings,
        }

    # ── Step 3: execute ───────────────────────────────────────────────────────
    exec_result = await bim_monkey_execute_plan(
        plan=plan, chunk_size=chunk_size, generation_id=gen_id
    )

    # ── Step 4: finishing phase ───────────────────────────────────────────────
    finish_result = {}
    if exec_result.get("sheetsCreated", 0) > 0 or exec_result.get("success"):
        finish_result = call_revit("runFinishingPhase", {})

    # ── Step 5: mark executed ─────────────────────────────────────────────────
    if gen_id:
        try:
            await bim_monkey_mark_executed(generation_id=gen_id)
        except Exception:
            pass  # non-blocking

    return {
        "success": exec_result.get("success", False),
        "generationId": gen_id,
        "sheetsCreated":   exec_result.get("sheetsCreated",   0),
        "viewsPlaced":     exec_result.get("viewsPlaced",     0),
        "detailsCreated":  exec_result.get("detailsCreated",  0),
        "schedulesPlaced": exec_result.get("schedulesPlaced", 0),
        "errorCount":      exec_result.get("errorCount",      0),
        "errors":          exec_result.get("errors",          []),
        "abortedAtChunk":  exec_result.get("abortedAtChunk"),
        "finishing":       finish_result,
        "planWarnings":    plan_warnings,
        "validationWarnings": validation["warnings"],
    }


def _post_chunk_progress(generation_id: int, chunk_index: int, phase: str, sheets_created: list):
    """Non-blocking: record completed chunk to backend for resume support."""
    if not BIM_MONKEY_API_KEY or not generation_id:
        return
    try:
        payload = json.dumps({
            "chunkIndex":    chunk_index,
            "phase":         phase,
            "sheetsCreated": sheets_created,
            "completedAt":   __import__("datetime").datetime.utcnow().isoformat() + "Z",
        }).encode("utf-8")
        req = urllib.request.Request(
            f"{BIM_MONKEY_API_URL}/api/generation/{generation_id}/chunk-progress",
            data=payload,
            headers={"Content-Type": "application/json",
                     "Authorization": f"Bearer {BIM_MONKEY_API_KEY}"},
            method="POST",
        )
        urllib.request.urlopen(req, timeout=5)
    except Exception:
        pass  # never fail execution over a checkpoint error


@mcp.tool()
async def bim_monkey_execute_plan(plan: dict, chunk_size: int = 5, generation_id: int = None) -> dict:
    """
    Execute a full CD plan in Revit in one batch pass — all three phases, no round trips.

    This is the Quick Mode executor. Pass the plan object returned by bim_monkey_generate.
    All sheet creation, view placement, detail drawing, and schedule creation happens in
    one or more C# calls. Use this instead of calling createSheet / placeViewOnSheet /
    drawLayerStack / createSchedule individually.

    Args:
        plan:          The plan dict returned by bim_monkey_generate (the "plan" field).
                       Must include sheets[], detailPlan[], and schedulePlan[].
        chunk_size:    Max sheets per execution pass (default 5). For projects with more
                       sheets than chunk_size, Phase 1 runs in multiple sequential passes.
                       Details and schedules always run as separate passes after all sheets.
                       Set to 0 to disable chunking (single pass, legacy behavior).
        generation_id: The generationId from bim_monkey_generate. When provided, execution
                       results (sheetsCreated, errors, etc.) are automatically posted to the
                       BIM Monkey backend for tracking in the admin dashboard.

    Returns when not chunked (chunk_size=0 or <= total sheets):
        { "success": true, "sheetsCreated": 14, "viewsPlaced": 22, ... }

    Returns when chunked:
        {
          "success": true,
          "sheetsCreated": 45, "viewsPlaced": 90, "detailsCreated": 6, "schedulesPlaced": 3,
          "errorCount": 2,
          "chunks": [
            { "phase": "sheets", "chunk": 1, "of": 4, "sheets": ["A1.1",...], "sheetsCreated": 12 },
            ...
            { "phase": "details", "detailsCreated": 6 },
            { "phase": "schedules", "schedulesPlaced": 3 }
          ]
        }

    QUICK MODE — call sequence:
      1. bim_monkey_generate(model_data) → get plan + generationId
      2. bim_monkey_execute_plan(plan, generation_id=generationId) → execute + auto-report results
      3. bim_monkey_mark_executed(generationId) → mark run complete in dashboard
    """
    import json as _json

    sheets       = plan.get("sheets",      [])
    detail_plan  = plan.get("detailPlan",  [])
    sched_plan   = plan.get("schedulePlan", [])

    def _report_execution(result):
        """Post execution results to backend if generation_id was provided."""
        if not generation_id or not BIM_MONKEY_API_KEY:
            return
        try:
            payload = json.dumps({
                "sheetsCreated":      result.get("sheetsCreated", 0),
                "sheetsExisted":      result.get("sheetsExisted", 0),
                "viewsPlaced":        result.get("viewsPlaced", 0),
                "viewsDuplicated":    result.get("viewsDuplicated", 0),
                "detailsCreated":     result.get("detailsCreated", 0),
                "schedulesPlaced":    result.get("schedulesPlaced", 0),
                "errorCount":         result.get("errorCount", 0),
                "errors":             result.get("errors", []),
                "needsManualAction":  result.get("needsManualAction", []),
            }).encode("utf-8")
            req = urllib.request.Request(
                f"{BIM_MONKEY_API_URL}/api/generation/{generation_id}/execution-result",
                data=payload,
                headers={"Content-Type": "application/json", "Authorization": f"Bearer {BIM_MONKEY_API_KEY}"},
                method="POST",
            )
            with urllib.request.urlopen(req, timeout=15):
                pass
        except Exception:
            pass  # non-blocking — never fail the execution call over a reporting error

    # Single-pass mode: chunk_size=0, or plan fits in one chunk
    if chunk_size <= 0 or len(sheets) <= chunk_size:
        result = _execute_plan_request(plan)
        _report_execution(result)
        return result

    # ── Chunked mode ───────────────────────────────────────────────────────────
    totals = {
        "success":         True,
        "sheetsCreated":   0,
        "sheetsExisted":   0,
        "viewsPlaced":     0,
        "viewsAlreadyPlaced": 0,
        "viewsDuplicated": 0,
        "detailsCreated":  0,
        "schedulesPlaced": 0,
        "errorCount":      0,
        "errors":          [],
        "needsManualAction": [],
        "chunks":          [],
    }

    def _merge(totals, result, phase_info):
        for key in ("sheetsCreated", "sheetsExisted", "viewsPlaced", "viewsAlreadyPlaced",
                    "viewsDuplicated", "detailsCreated", "schedulesPlaced", "errorCount"):
            totals[key] += result.get(key, 0)
        totals["errors"].extend(result.get("errors", []))
        totals["needsManualAction"].extend(result.get("needsManualAction", []))
        totals["chunks"].append({**phase_info, **{
            k: result.get(k) for k in ("sheetsCreated", "sheetsExisted", "viewsPlaced",
                                        "detailsCreated", "schedulesPlaced", "errorCount", "error")
            if result.get(k) is not None
        }})
        if not result.get("success") and result.get("error"):
            totals["success"] = False

    # Phase 1: sheets in chunks (details and schedules empty — run separately after)
    sheet_chunks = [sheets[i:i+chunk_size] for i in range(0, len(sheets), chunk_size)]
    n = len(sheet_chunks)
    for idx, chunk in enumerate(sheet_chunks):
        chunk_plan = {"sheets": chunk, "detailPlan": [], "schedulePlan": []}
        result = _execute_plan_request(chunk_plan)
        phase_info = {
            "phase":  "sheets",
            "chunk":  idx + 1,
            "of":     n,
            "sheets": [s.get("sheetNumber") for s in chunk],
        }
        _merge(totals, result, phase_info)
        # Checkpoint successful chunk to backend so runs can be resumed
        if result.get("success") and generation_id:
            _post_chunk_progress(generation_id, idx + 1, "sheets",
                                 [s.get("sheetNumber") for s in chunk])
        if not result.get("success") and result.get("error"):
            # Hard failure (timeout, pipe error) — check actual Revit state before aborting.
            # The pipe may have timed out after C# finished writing all sheets but before
            # the response was read.  Query getSheets and compare against the chunk to see
            # how many were actually created.
            expected_in_chunk = {s.get("sheetNumber") for s in chunk}
            try:
                sheets_resp = call_revit("getSheets")
                existing_nums = {s.get("sheetNumber") for s in (sheets_resp.get("sheets") or [])}
                recovered = expected_in_chunk & existing_nums
                if recovered:
                    # Sheets were created despite the timeout — patch totals and continue.
                    totals["sheetsCreated"] += len(recovered)
                    totals["success"] = True
                    # Remove the false-failure entry added by _merge
                    if totals["chunks"] and totals["chunks"][-1].get("error"):
                        totals["chunks"][-1]["error"] = None
                        totals["chunks"][-1]["sheetsCreated"] = len(recovered)
                    totals["chunks"][-1]["recoveredAfterTimeout"] = True
                    # Don't set abortedAtChunk — let remaining chunks proceed
                    continue
            except Exception:
                pass
            # Could not recover — abort remaining sheet chunks
            totals["abortedAtChunk"] = idx + 1
            break

    sheets_aborted = "abortedAtChunk" in totals

    # Phase 2: details (separate pass — all details in one call)
    # Skip if sheets phase aborted — target sheets likely don't exist yet.
    if detail_plan and not sheets_aborted:
        result = _execute_plan_request({"sheets": [], "detailPlan": detail_plan, "schedulePlan": []})
        _merge(totals, result, {"phase": "details"})

    # Phase 3: schedules (separate pass)
    # Skip if sheets phase aborted — target sheets likely don't exist yet.
    if sched_plan and not sheets_aborted:
        result = _execute_plan_request({"sheets": [], "detailPlan": [], "schedulePlan": sched_plan})
        _merge(totals, result, {"phase": "schedules"})

    _report_execution(totals)
    return totals


@mcp.tool()
async def bim_monkey_run_finishing_phase(
    drawn_by: str = None,
    checked_by: str = None,
    issue_date: str = None,
    project_number: str = None,
    margin_ft: float = 6.0,
    sheet_numbers: list = None,
) -> dict:
    """
    Run all four finishing steps in a single C# call after executePlan completes.

    Replaces the four separate bim_monkey_populate_titleblocks / bim_monkey_apply_view_templates /
    bim_monkey_set_crop_boxes / bim_monkey_audit_sheets calls.  Everything runs in-process inside
    Revit with zero pipe round-trips per sheet — much faster and no timeout risk.

    Steps performed (in order, all in one call):
      1. Titleblocks — writes DrawnBy, CheckedBy, IssueDate, ProjectNumber to every generated sheet.
         Falls back to Revit Project Information if values are not supplied.
      2. View templates — applies the best-matching saved template to every placed view by type
         (floor plan, RCP, elevation, section, detail).
      3. Crop boxes — fits floor plan view crop boxes to room extents + marginFt on each level.
      4. Sheet audit — checks scale and empty-view issues and returns a summary.

    Args:
        drawn_by:       Drawn-by initials. Defaults to Revit Project Information → Author.
        checked_by:     Checked-by initials. Defaults to drawn_by.
        issue_date:     Issue date string e.g. "2026-03-29". Defaults to today.
        project_number: Project number. Defaults to Revit Project Information → Project Number.
        margin_ft:      Margin outside room extents for crop boxes (default 6 ft).
        sheet_numbers:  Optional list of sheet numbers to target. Defaults to all " *" sheets.

    Returns:
        {
          "success": true,
          "sheetsProcessed": 14,
          "titleblocks":   { "updated": 14, "skipped": 0, "emptyFields": [], "valuesApplied": {...} },
          "viewTemplates": { "applied": 18, "skipped": 4, "skippedReasons": [...] },
          "cropBoxes":     { "levelsProcessed": 3, "viewsUpdated": 3, "skipped": [] },
          "audit":         { "sheetsChecked": 14, "issues": [], "ok": true }
        }
    """
    params = {}
    if drawn_by:       params["drawnBy"]       = drawn_by
    if checked_by:     params["checkedBy"]     = checked_by
    if issue_date:     params["issueDate"]      = issue_date
    if project_number: params["projectNumber"] = project_number
    if margin_ft != 6.0: params["marginFt"]    = margin_ft
    if sheet_numbers:  params["sheetNumbers"]  = sheet_numbers

    return call_revit("runFinishingPhase", params)


@mcp.tool()
async def bim_monkey_report_issues(generation_id: int, issues: list) -> dict:
    """
    Report issues encountered during a Revit execution run to the BIM Monkey dashboard.
    Call this AFTER completing all Revit operations, before bim_monkey_mark_executed.

    Each issue should be a dict with:
      - type: short category (e.g. "placement_error", "method_fallback", "duplicate_view", "schedule_skipped")
      - message: one-sentence description of what happened
      - item: optional — which sheet/view/detail was affected
      - recommendation: optional — what should change in the next run

    Args:
        generation_id: The generationId returned by bim_monkey_generate.
        issues: List of issue dicts encountered during execution.

    Returns:
        {"success": true} on success.
    """
    if not BIM_MONKEY_API_KEY:
        return {"success": False, "error": "BIM_MONKEY_API_KEY not set in environment."}

    payload = json.dumps({"issues": issues}).encode("utf-8")
    req = urllib.request.Request(
        f"{BIM_MONKEY_API_URL}/api/generation/{generation_id}/system-report",
        data=payload,
        headers={
            "Content-Type": "application/json",
            "Authorization": f"Bearer {BIM_MONKEY_API_KEY}",
        },
        method="POST",
    )

    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            return {"success": data.get("success", True)}
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="replace")[:300]
        return {"success": False, "error": f"HTTP {e.code}: {body}"}
    except Exception as e:
        return {"success": False, "error": str(e)}


@mcp.tool()
async def bim_monkey_mark_executed(generation_id: int) -> dict:
    """
    Mark a generation as executed in Revit. Call this after all sheets, views,
    and details from the CD plan have been created in Revit.

    Args:
        generation_id: The generationId returned by bim_monkey_generate.

    Returns:
        {"success": true} on success.
    """
    if not BIM_MONKEY_API_KEY:
        return {"success": False, "error": "BIM_MONKEY_API_KEY not set in environment."}

    req = urllib.request.Request(
        f"{BIM_MONKEY_API_URL}/api/generation/{generation_id}/mark-executed",
        data=b"{}",
        headers={
            "Content-Type": "application/json",
            "Authorization": f"Bearer {BIM_MONKEY_API_KEY}",
        },
        method="PATCH",
    )

    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            return {"success": True, "revit_executed_at": data.get("revit_executed_at")}
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="replace")[:300]
        return {"success": False, "error": f"HTTP {e.code}: {body}"}
    except Exception as e:
        return {"success": False, "error": str(e)}


# ── Redline Review tools (Sprint N) ──────────────────────────────────────────

def _redline_base_folder() -> str:
    return os.path.join(os.path.expandvars('%USERPROFILE%'), 'Documents', 'BIM Monkey', 'Redline Review')


def _latest_session_folder() -> str:
    """Return the most recent timestamped session subfolder, or None if none exist."""
    base = _redline_base_folder()
    if not os.path.isdir(base):
        return None
    subfolders = sorted(
        [d for d in os.listdir(base) if os.path.isdir(os.path.join(base, d))],
        reverse=True
    )
    return os.path.join(base, subfolders[0]) if subfolders else None


@mcp.tool()
async def bim_monkey_analyze_redlines(
    pdf_path: str = None,
    dpi: int = 225,
    max_pages: int = None,
    timeout_per_page: int = 20,
) -> dict:
    """
    Convert a redline PDF into PNG page images for visual analysis.

    Args:
        pdf_path:         Optional. Full path to the PDF file. If omitted, uses the most
                          recent redline_*.pdf in the Redline Review folder.
        dpi:              Render resolution. Default 225 — good balance of speed and
                          readability for handwritten notes and small callout text.
                          Use 300 only if text is still unreadable at 225.
        max_pages:        Maximum pages to convert. Omit to convert all pages.
                          For PDFs with >10 pages, use max_pages=6 for a quick first pass,
                          report preliminary findings, then call again for remaining pages.
        timeout_per_page: Ignored — conversion now runs inline via PyMuPDF (no subprocess).
                          Kept for backwards compatibility.

    After this returns, READ each file in pages[].path using the Read tool.
    Look for ALL annotation types: red ink, orange callouts, circled elements,
    revision clouds, and typed annotation boxes — do not assume only red = markup.
    For each annotation record: page number, element/area marked, and requested change.

    IMPORTANT: If annotationObjects.count == 0 the markups are baked-in (scanned or
    flattened). Text extraction via the Read tool will NOT find visual redlines.
    Use visual analysis of the PNG images only.

    Returns:
        {
          "success": true,
          "pageCount": 22,
          "pagesConverted": 22,
          "dpi": 225,
          "pages": [{"page": 1, "path": "C:\\...\\page-001.png", "width_px": 2175, "height_px": 1680}],
          "annotationObjects": {"count": 5, "types": ["Highlight", "Ink"]}
        }
    """
    # Resolve session folder and PDF path
    if pdf_path and os.path.exists(pdf_path):
        resolved_pdf = pdf_path
        session_folder = os.path.dirname(pdf_path)
    else:
        session_folder = _latest_session_folder()
        if not session_folder:
            return {
                "success": False,
                "error": (
                    "No redline session found. Either pass pdf_path directly or click "
                    "'Load' in the Redline Review ribbon panel first."
                )
            }
        resolved_pdf = os.path.join(session_folder, 'redline.pdf')
        if not os.path.exists(resolved_pdf):
            return {"success": False, "error": f"No redline.pdf in session folder: {session_folder}"}

    # Primary path: PyMuPDF (fitz) inline — fast, no subprocess overhead, no timeout risk.
    # Subprocess path was consistently timing out and falling through to this anyway.
    try:
        import fitz  # PyMuPDF
        os.makedirs(session_folder, exist_ok=True)
        doc = fitz.open(resolved_pdf)
        total_pages = len(doc)
        limit = min(total_pages, max_pages) if max_pages else total_pages
        scale = dpi / 72
        mat = fitz.Matrix(scale, scale)

        pages = []
        all_annot_types = []

        for i in range(limit):
            page = doc[i]

            # Collect annotation objects (live markup data — present in non-flattened PDFs)
            for annot in page.annots():
                annot_type = annot.type[1] if annot.type else "Unknown"
                all_annot_types.append(annot_type)

            # Render page to PNG
            pix = page.get_pixmap(matrix=mat)
            png_path = os.path.join(session_folder, f"page-{i + 1:03d}.png")
            pix.save(png_path)
            pages.append({
                "page": i + 1,
                "path": png_path,
                "width_px": pix.width,
                "height_px": pix.height,
            })

        doc.close()

        # Summarize annotation objects
        annot_type_counts = {}
        for t in all_annot_types:
            annot_type_counts[t] = annot_type_counts.get(t, 0) + 1
        annotation_objects = {
            "count": len(all_annot_types),
            "types": list(annot_type_counts.keys()),
            "counts": annot_type_counts,
        }

        result = {
            "success": True,
            "pdfPath": resolved_pdf,
            "pageCount": total_pages,
            "pagesConverted": len(pages),
            "dpi": dpi,
            "pages": pages,
            "annotationObjects": annotation_objects,
        }

        if annotation_objects["count"] == 0:
            result["warning"] = (
                "annotationObjects.count == 0 — markups are baked-in (scanned or flattened PDF). "
                "Visual analysis of PNG images is required; text extraction will not find redlines."
            )

        if max_pages and total_pages > max_pages:
            result["remainingPages"] = total_pages - max_pages
            result["note"] = (
                f"Quick pass: converted {max_pages} of {total_pages} pages. "
                f"Call again without max_pages (or max_pages={total_pages}) to convert all pages."
            )

        return result

    except ImportError:
        pass  # fitz not available — fall through to subprocess
    except Exception as fitz_err:
        return {"success": False, "error": f"PyMuPDF conversion failed: {fitz_err}"}

    # Fallback: subprocess (only reached if fitz is not installed)
    script_dir = os.path.dirname(os.path.abspath(__file__))
    script_path = os.path.join(script_dir, 'analyze_redlines.py')
    if not os.path.exists(script_path):
        return {
            "success": False,
            "error": (
                "PyMuPDF (fitz) is not installed and analyze_redlines.py not found. "
                "Run: pip install pymupdf"
            )
        }
    pages_to_convert = min(30, max_pages) if max_pages else 30
    total_timeout = max(60, pages_to_convert * timeout_per_page)
    try:
        import subprocess as _sp
        cmd = [sys.executable, script_path, '--pdf', resolved_pdf, '--folder', session_folder,
               '--dpi', str(dpi)]
        if max_pages:
            cmd += ['--max-pages', str(max_pages)]
        proc = _sp.run(cmd, capture_output=True, text=True, timeout=total_timeout)
        output = proc.stdout.strip()
        if not output:
            return {"success": False, "error": f"Script produced no output. stderr: {proc.stderr.strip()[:300]}"}
        return json.loads(output)
    except Exception as e:
        return {"success": False, "error": f"Subprocess fallback failed: {e}"}


@mcp.tool()
async def bim_monkey_save_redline_analysis(changes: str) -> dict:
    """
    Save your structured redline analysis to disk so generation can use it.

    Args:
        changes: JSON string — list of change objects:
            [{
              "page": 1,
              "element": "Door 101",
              "change": "Change to 3'-0\" wide",
              "type": "dimension",
              "confidence": "high",
              "method": "image_analysis"
            }, ...]
            type values: dimension | specification | note | tag | deletion | addition | other
            confidence values: high | medium | low
            method values: image_analysis | text_extraction | inferred
              - image_analysis: identified visually from PNG page images
              - text_extraction: read from PDF text layer (no images available)
              - inferred: deduced from context when neither image nor text was conclusive

    Returns:
        {"success": true, "path": "...", "changeCount": N}
    """
    folder = _latest_session_folder() or _redline_base_folder()
    os.makedirs(folder, exist_ok=True)

    try:
        parsed = json.loads(changes) if isinstance(changes, str) else changes
        out_path = os.path.join(folder, 'redline_analysis.json')
        with open(out_path, 'w') as f:
            json.dump({"changes": parsed, "analyzedAt": __import__('datetime').datetime.now().isoformat()}, f, indent=2)
        return {"success": True, "path": out_path, "changeCount": len(parsed)}
    except Exception as e:
        return {"success": False, "error": str(e)}


@mcp.tool()
async def bim_monkey_get_redlines() -> dict:
    """
    Read saved redline analysis. Call this at the start of generation to check
    if the architect has loaded redline markup that should override the default plan.

    Returns:
        {"success": true, "hasRedlines": true, "changeCount": 5, "changes": [...]}
        OR {"success": true, "hasRedlines": false} if no redlines loaded.
    """
    folder = _latest_session_folder() or _redline_base_folder()
    analysis_path = os.path.join(folder, 'redline_analysis.json')

    if not os.path.exists(analysis_path):
        return {"success": True, "hasRedlines": False}

    try:
        with open(analysis_path, 'r') as f:
            data = json.load(f)
        changes = data.get('changes', [])
        return {
            "success": True,
            "hasRedlines": len(changes) > 0,
            "changeCount": len(changes),
            "analyzedAt": data.get('analyzedAt'),
            "changes": changes,
        }
    except Exception as e:
        return {"success": False, "error": str(e)}


# ── Detail Component tools (Sprint Q) ────────────────────────────────────────

@mcp.tool()
async def bim_monkey_list_detail_components() -> dict:
    """
    List detail component families and types loaded in the current Revit document.

    Use this at the start of Phase 2 to determine whether to use placeDetailComponent
    (BIM-correct, taggable) or fall back to drawLayerStack (lines only).

    If families like "Detail Component - 2x6 Stud", "Detail Component - GWB", etc. are
    present, prefer placeDetailComponent / placeDetailComponentByName over drawLayerStack.

    Returns families[] and types[] available for placement via placeDetailComponent.
    """
    families = call_revit("getDetailComponentFamilies")
    types = call_revit("getDetailComponentTypes", {})
    return {
        "success": True,
        "families": families.get("families", []),
        "types": types.get("types", []),
        "note": (
            "Prefer placeDetailComponent for wall layer stacks if matching types exist. "
            "Detail components are BIM-correct and taggable — they represent real building materials "
            "rather than drafted lines. Fall back to drawLayerStack only when no matching families are loaded."
        ),
    }


# ── CD Workflow Automation (Session 61) ──────────────────────────────────────

@mcp.tool()
async def bim_monkey_populate_titleblocks(
    drawn_by: str = None,
    checked_by: str = None,
    issue_date: str = None,
    project_number: str = None,
    sheet_numbers: list = None,
) -> dict:
    """
    Populate titleblock parameters across sheets from the model's project info.

    Reads projectInfo from the model (project name, number, client, address) and
    writes drawn-by, checked-by, issue date, and project number to every sheet's
    titleblock parameters.  Pass explicit overrides to use values that differ from
    what is stored in the Revit file.

    Args:
        drawn_by:       Drawn-by initials/name. Defaults to projectInfo.authorName.
        checked_by:     Checked-by initials/name. Defaults to drawn_by.
        issue_date:     Issue date string, e.g. "2026-03-29". Defaults to today.
        project_number: Project number string. Defaults to projectInfo.projectNumber.
        sheet_numbers:  Optional list of sheet numbers to target. If omitted, targets
                        all sheets whose name ends with " *" (BIM Monkey generated).

    Returns:
        { "success": true, "updated": 14, "skipped": 0, "errors": [] }
    """
    import datetime

    # Pull project info from the model
    info = call_revit("getProjectInfo")
    proj_num  = project_number or info.get("projectNumber") or info.get("number") or ""
    author    = drawn_by      or info.get("authorName")    or info.get("author")  or ""
    checker   = checked_by    or author
    date_str  = issue_date    or datetime.date.today().isoformat()

    # Warn if key fields are empty — user must fill them in Revit Project Information
    empty_fields = []
    if not proj_num: empty_fields.append("projectNumber (set in Revit → Manage → Project Information)")
    if not author:   empty_fields.append("authorName / drawn by (set in Revit → Manage → Project Information)")

    # Get all sheets
    sheets_resp = call_revit("getSheets")
    all_sheets  = sheets_resp.get("sheets", [])

    # Filter to target sheets — getSheets returns "sheetName" not "name"
    if sheet_numbers:
        targets = [s for s in all_sheets if s.get("sheetNumber") in sheet_numbers]
    else:
        # Default: only BIM Monkey generated sheets (name ends with " *")
        targets = [s for s in all_sheets if str(s.get("sheetName", s.get("name", ""))).endswith(" *")]
        if not targets:
            targets = all_sheets  # fall back to all if none are marked

    updated = 0
    skipped = 0
    errors  = []

    PARAM_SETS = [
        # (paramName variants to try, value)
        (["DrawnBy", "Drawn By", "drawn_by", "DrawBy"],       author),
        (["CheckedBy", "Checked By", "checked_by", "CheckBy"], checker),
        (["SheetIssueDate", "Issue Date", "IssueDate",
          "Sheet Issue Date", "issue_date"],                    date_str),
        (["ProjectNumber", "Project Number", "project_number"], proj_num),
    ]

    for sheet in targets:
        sheet_id = sheet.get("id") or sheet.get("sheetId")
        if not sheet_id:
            skipped += 1
            continue
        sheet_updated = False
        for variants, value in PARAM_SETS:
            if not value:
                continue
            for param_name in variants:
                result = call_revit("setSheetParameter", {
                    "sheetId":   sheet_id,
                    "paramName": param_name,
                    "value":     value,
                })
                if result.get("success"):
                    sheet_updated = True
                    break  # found working param name for this field
        if sheet_updated:
            updated += 1
        else:
            skipped += 1

    return {
        "success": True,
        "updated":       updated,
        "skipped":       skipped,
        "errors":        errors,
        "emptyFields":   empty_fields,  # fields not populated — user must set in Revit Project Information
        "valuesApplied": {
            "drawnBy":       author,
            "checkedBy":     checker,
            "issueDate":     date_str,
            "projectNumber": proj_num,
        },
    }


@mcp.tool()
async def bim_monkey_audit_sheets(
    sheet_numbers: list = None,
    bim_monkey_only: bool = True,
) -> dict:
    """
    Pre-export health check: audit all placed views for scale violations and empty viewports.

    Checks every viewport on every target sheet against expected scale ranges by view type,
    and flags any viewport with zero elements in view.  Run this before Barrett reviews
    to catch issues that would require manual cleanup.

    Expected scales (Revit scale denominator):
      floor_plan / rcp:  96 (1/8") or 48 (1/4") or 32 (3/8")
      elevation:         96 (1/8") or 48 (1/4") or 32 (3/8")
      section:           96 or 48 or 32 or 16 (3/4")
      detail:            4 (3"=1') or 12 (1"=1') or 24 (1/2"=1') or 48 (1/4"=1')
      schedule / cover:  any (no constraint)

    Args:
        sheet_numbers:   Optional list of sheet numbers to check. If omitted,
                         checks all BIM Monkey sheets (name ends with " *") when
                         bim_monkey_only=True, or all sheets otherwise.
        bim_monkey_only: If True (default), only audit sheets marked with " *".

    Returns:
        {
          "success": true,
          "sheetsChecked": 14,
          "issues": [
            { "sheet": "A1.1", "view": "Level 1 Floor Plan",
              "type": "wrong_scale", "scale": "1:200", "expected": "1:96 or 1:48" },
            { "sheet": "A4.1", "view": "Detail 3",
              "type": "empty_view", "elementCount": 0 }
          ],
          "warnings": [],
          "ok": true
        }
    """
    EXPECTED_SCALES = {
        "FloorPlan":     [96, 48, 32, 24],
        "CeilingPlan":   [96, 48, 32, 24],
        "Elevation":     [96, 48, 32, 16],
        "Section":       [96, 48, 32, 16, 12],
        "Detail":        [4, 12, 24, 48],
        "DraftingView":  [4, 12, 24, 48],
    }

    sheets_resp = call_revit("getSheets")
    all_sheets  = sheets_resp.get("sheets", [])

    if sheet_numbers:
        targets = [s for s in all_sheets if s.get("sheetNumber") in sheet_numbers]
    elif bim_monkey_only:
        # getSheets returns "sheetName" not "name"
        targets = [s for s in all_sheets if str(s.get("sheetName", s.get("name", ""))).endswith(" *")]
        if not targets:
            targets = all_sheets
    else:
        targets = all_sheets

    issues   = []
    warnings = []

    for sheet in targets:
        sheet_num = sheet.get("sheetNumber", "?")
        sheet_id  = sheet.get("sheetId") or sheet.get("id")

        # getSheets does not include viewport details — fetch them separately
        vp_resp   = call_revit("getViewportsOnSheet", {"sheetId": sheet_id})
        viewports = vp_resp.get("viewports", [])

        for vp in viewports:
            view_id   = vp.get("viewId") or vp.get("id")
            view_name = vp.get("viewName") or vp.get("name", "?")
            view_type = vp.get("viewType") or vp.get("type", "")
            scale     = vp.get("scale") or vp.get("viewScale")

            # Scale check
            if view_type in EXPECTED_SCALES and scale:
                try:
                    scale_int = int(scale)
                    if scale_int not in EXPECTED_SCALES[view_type]:
                        issues.append({
                            "sheet":    sheet_num,
                            "view":     view_name,
                            "type":     "wrong_scale",
                            "scale":    f"1:{scale_int}",
                            "expected": "1:" + " or 1:".join(str(s) for s in EXPECTED_SCALES[view_type]),
                        })
                except (ValueError, TypeError):
                    pass

            # Empty view check
            if view_id:
                elems = call_revit("getElementsInView", {"viewId": view_id})
                count = elems.get("count", 0) or len(elems.get("elements", []))
                if count == 0:
                    issues.append({
                        "sheet":        sheet_num,
                        "view":         view_name,
                        "type":         "empty_view",
                        "elementCount": 0,
                    })

    return {
        "success":       True,
        "sheetsChecked": len(targets),
        "issues":        issues,
        "warnings":      warnings,
        "ok":            len(issues) == 0,
    }


@mcp.tool()
async def bim_monkey_apply_view_templates(
    sheet_numbers: list = None,
    floor_plan_template: str = None,
    elevation_template: str = None,
    section_template: str = None,
    detail_template: str = None,
) -> dict:
    """
    Apply view templates to all placed views on BIM Monkey sheets.

    First checks what templates are available in the model, then applies the best
    match to each view by type.  If explicit template names are not passed, uses
    the first template whose name contains the view type keyword.

    Args:
        sheet_numbers:        Limit to specific sheet numbers.  Defaults to all " *" sheets.
        floor_plan_template:  Exact template name for floor plan / RCP views.
        elevation_template:   Exact template name for elevation views.
        section_template:     Exact template name for section views.
        detail_template:      Exact template name for detail / drafting views.

    Returns:
        {
          "success": true,
          "templatesFound": ["Floor Plan CD", "Elevation CD", ...],
          "applied": 18,
          "skipped": 4,
          "skippedReasons": ["No matching template for DraftingView"]
        }
    """
    # Discover available templates
    tmpl_resp  = call_revit("getViewTemplates")
    templates  = tmpl_resp.get("templates", []) or tmpl_resp.get("viewTemplates", [])
    # getViewTemplates may return objects with "name", "templateName", or "viewTemplateName"
    tmpl_names = []
    for t in templates:
        n = t.get("name") or t.get("templateName") or t.get("viewTemplateName") or ""
        if n:
            tmpl_names.append(n)

    def best_match(keyword, override):
        if override:
            return override
        kw = keyword.lower()
        for name in tmpl_names:
            if kw in name.lower():
                return name
        return None

    type_map = {
        "FloorPlan":   best_match("floor plan", floor_plan_template),
        "CeilingPlan": best_match("rcp",         floor_plan_template) or best_match("ceiling", floor_plan_template),
        "Elevation":   best_match("elevation",   elevation_template),
        "Section":     best_match("section",     section_template),
        "Detail":      best_match("detail",      detail_template),
        "DraftingView":best_match("detail",      detail_template) or best_match("drafting", detail_template),
    }

    sheets_resp = call_revit("getSheets")
    all_sheets  = sheets_resp.get("sheets", [])

    if sheet_numbers:
        targets = [s for s in all_sheets if s.get("sheetNumber") in sheet_numbers]
    else:
        targets = [s for s in all_sheets if str(s.get("sheetName", s.get("name", ""))).endswith(" *")]
        if not targets:
            targets = all_sheets

    applied  = 0
    skipped  = 0
    reasons  = set()

    for sheet in targets:
        # getSheets does not include viewport details — fetch per sheet
        sheet_id  = sheet.get("sheetId") or sheet.get("id")
        vp_resp   = call_revit("getViewportsOnSheet", {"sheetId": sheet_id})
        viewports = vp_resp.get("viewports", [])
        for vp in viewports:
            view_id   = vp.get("viewId") or vp.get("id")
            view_type = vp.get("viewType") or vp.get("type", "")
            tmpl_name = type_map.get(view_type)
            if not tmpl_name or not view_id:
                skipped += 1
                if not tmpl_name:
                    reasons.add(f"No matching template for {view_type}")
                continue
            result = call_revit("applyViewTemplate", {
                "viewId":       view_id,
                "templateName": tmpl_name,
            })
            if result.get("success"):
                applied += 1
            else:
                skipped += 1
                reasons.add(result.get("error", f"applyViewTemplate failed for {view_type}"))

    return {
        "success":        True,
        "templatesFound": tmpl_names,
        "typeMap":        {k: v for k, v in type_map.items() if v},
        "applied":        applied,
        "skipped":        skipped,
        "skippedReasons": list(reasons),
    }


@mcp.tool()
async def bim_monkey_set_crop_boxes(
    margin_ft: float = 6.0,
    level_names: list = None,
) -> dict:
    """
    Auto-fit crop boxes on floor plan views to the rooms on each level.

    Gets all rooms, groups them by level, computes the bounding box of all room
    locations on that level, and sets the crop box of the corresponding floor
    plan view with a consistent margin.  Eliminates manual crop dragging after
    generation.

    Args:
        margin_ft:    Margin to add outside the room extents on all sides.
                      Default 6 ft — shows building perimeter walls cleanly.
        level_names:  Optional list of level names to process. Defaults to all levels.

    Returns:
        {
          "success": true,
          "levelsProcessed": 3,
          "viewsUpdated": 3,
          "skipped": ["Roof — no rooms found"]
        }
    """
    # Get rooms grouped by level
    rooms_resp = call_revit("getRooms")
    rooms      = rooms_resp.get("rooms", [])

    # Get floor plan views
    views_resp = call_revit("getViews", {"viewType": "FloorPlan"})
    fp_views   = views_resp.get("views", [])

    # Build level → rooms map
    by_level = {}
    for room in rooms:
        level = room.get("levelName") or room.get("level", "")
        if not level:
            continue
        if level not in by_level:
            by_level[level] = []
        by_level[level].append(room)

    def _norm_level(name: str) -> str:
        """Normalize level name for fuzzy matching: strip trailing (E)/(N)/(NEW)/(EXIST)/etc."""
        s = re.sub(r'\s*\([^)]*\)\s*$', '', name.strip())
        return s.strip().upper()

    # Build level → ALL floor plan views map — keyed by both raw and normalized names.
    # Duplicate views ("Level 1 Copy 1 *") share the same levelName as the original —
    # crop ALL of them so whichever copy is on the sheet gets the right crop box.
    views_for_level = {}       # raw level name → [views]
    views_for_norm  = {}       # normalized level name → [views]
    for v in fp_views:
        lvl = v.get("levelName") or v.get("associatedLevel") or v.get("level", "")
        if lvl:
            views_for_level.setdefault(lvl, []).append(v)
            views_for_norm.setdefault(_norm_level(lvl), []).append(v)

    updated = 0
    skipped = []

    levels_to_process = level_names or list(by_level.keys())

    for level in levels_to_process:
        rooms_on_level = by_level.get(level, [])
        if not rooms_on_level:
            skipped.append(f"{level} — no rooms found")
            continue

        # Try exact match first, then normalized (strips direction suffixes like " (E)")
        level_views = views_for_level.get(level) or views_for_norm.get(_norm_level(level), [])
        if not level_views:
            skipped.append(f"{level} — no floor plan view found")
            continue

        # Compute bounding box from room location points
        xs = []
        ys = []
        for room in rooms_on_level:
            loc = room.get("location") or room.get("locationPoint") or {}
            x   = loc.get("x") or room.get("x")
            y   = loc.get("y") or room.get("y")
            if x is not None:
                xs.append(float(x))
            if y is not None:
                ys.append(float(y))

        if not xs or not ys:
            skipped.append(f"{level} — room locations unavailable")
            continue

        min_x = min(xs) - margin_ft
        max_x = max(xs) + margin_ft
        min_y = min(ys) - margin_ft
        max_y = max(ys) + margin_ft

        # Crop all floor plan views for this level (original + any BIM Monkey copies)
        for fp_view in level_views:
            view_id = fp_view.get("id") or fp_view.get("viewId")
            result  = call_revit("setCropBox", {
                "viewId": view_id,
                "minX":   min_x,
                "minY":   min_y,
                "maxX":   max_x,
                "maxY":   max_y,
            })
            if result.get("success"):
                updated += 1
            else:
                skipped.append(f"{level} ({fp_view.get('name', view_id)}) — setCropBox failed: {result.get('error', 'unknown')}")

    return {
        "success":         True,
        "levelsProcessed": len(levels_to_process),
        "viewsUpdated":    updated,
        "skipped":         skipped,
    }


@mcp.tool()
async def bim_monkey_batch_tag_views(
    view_ids: list,
    tag_types: list = None,
    tag_location: str = "lower-left",
    skip_already_tagged: bool = True,
) -> dict:
    """
    Tag rooms, doors, and/or windows across multiple views in a single MCP call.

    Replaces the N×3 pattern of calling batchTagRooms + batchTagDoors + batchTagWindows
    per view. For a 10-view project with all 3 tag types, reduces 30 calls to 1.

    Args:
        view_ids:            List of view element IDs (ints) to tag.
        tag_types:           List of tag categories to apply. Default: ["rooms", "doors", "windows"].
                             Pass ["rooms"] to skip doors and windows (e.g. for roof plans).
        tag_location:        Tag placement: "lower-left" (default) or "center".
        skip_already_tagged: If True (default), skip elements that already have a tag.

    Returns:
        {
          "success": true,
          "viewsProcessed": 10,
          "totalTagged": 87,
          "totalSkipped": 12,
          "totalFailed": 0,
          "tagTypes": ["rooms","doors","windows"],
          "tagLocation": "lower-left",
          "executionTimeMs": 420,
          "correlationId": "a3f8b21c",
          "views": [{ "viewId": 123, "viewName": "LEVEL 1 FLOOR PLAN *", "tagged": 9, "skipped": 1, "failed": 0 }, ...]
        }
    """
    return call_revit("batchTagViews", {
        "viewIds":           view_ids,
        "tagTypes":          tag_types or ["rooms", "doors", "windows"],
        "tagLocation":       tag_location,
        "skipAlreadyTagged": skip_already_tagged,
    })


@mcp.tool()
async def bim_monkey_ping() -> dict:
    """
    Lightweight keepalive — call every 10-15 sequential MCP operations during
    heavy generation runs (createSheet, placeViewOnSheet loops) to prevent
    the named pipe from dropping under sustained load.

    Returns immediately with no Revit API side effects.

    Returns:
        { "success": true, "pong": true, "timestamp": "..." }
    """
    return call_revit("ping", {})


@mcp.tool()
async def bim_monkey_remember(note: str) -> dict:
    """
    Save a correction or preference to firm memory so it applies to every future session.

    Call this whenever the user says something like:
      - "remember: always put stair sections on A5.1"
      - "never use reflected ceiling plans for single family"
      - "bathroom elevations always go on A3.2"
      - "remember that" / "save that" / "keep that for next time"

    The note is saved to the BIM Monkey backend against the firm's account —
    it persists across machines, reinstalls, and sessions.

    Args:
        note: The correction or preference to remember, in plain language.

    Returns:
        {"success": true, "note": "...", "noteCount": N}
    """
    if not BIM_MONKEY_API_KEY:
        return {"success": False, "error": "BIM_MONKEY_API_KEY not set"}
    try:
        payload = json.dumps({"note": note.strip()}).encode("utf-8")
        req = urllib.request.Request(
            f"{BIM_MONKEY_API_URL}/api/firms/memory",
            data=payload,
            headers={"Content-Type": "application/json", "x-api-key": BIM_MONKEY_API_KEY},
            method="POST",
        )
        with urllib.request.urlopen(req, timeout=10) as r:
            data = json.loads(r.read().decode("utf-8"))
        return {"success": True, "note": data.get("note"), "noteCount": data.get("noteCount", 0)}
    except Exception as e:
        return {"success": False, "error": str(e)}


@mcp.tool()
async def bim_monkey_get_memory() -> dict:
    """
    Read all firm memory — preferences and corrections saved by bim_monkey_remember().

    Call this at the start of every session before doing any work. Apply everything
    returned throughout the session without being asked.

    Returns:
        {"success": true, "memory": "...", "noteCount": N}
        memory is plain text — read and apply it throughout the session.
        Returns {"success": true, "memory": null, "noteCount": 0} if nothing saved yet.
    """
    if not BIM_MONKEY_API_KEY:
        return {"success": False, "error": "BIM_MONKEY_API_KEY not set"}
    try:
        req = urllib.request.Request(
            f"{BIM_MONKEY_API_URL}/api/firms/memory",
            headers={"x-api-key": BIM_MONKEY_API_KEY},
            method="GET",
        )
        with urllib.request.urlopen(req, timeout=10) as r:
            data = json.loads(r.read().decode("utf-8"))
        return {"success": True, "memory": data.get("memory"), "noteCount": data.get("noteCount", 0)}
    except Exception as e:
        return {"success": False, "error": str(e)}


if __name__ == "__main__":
    mcp.run()
