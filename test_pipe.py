"""
Read-only diagnostic script for the RevitMCPBridge named pipe.
Only calls read operations — safe to run any time without modifying the model.

Usage:
    python test_pipe.py
"""
import json
import sys

PIPE_NAME = r"\\.\pipe\RevitMCPBridge2026"


def send(f, method, params=None):
    req = json.dumps({"method": method, "params": params or {}})
    f.write((req + "\n").encode())
    f.flush()
    line = b""
    while True:
        ch = f.read(1)
        if ch == b"\n" or ch == b"":
            break
        line += ch
    return json.loads(line.decode())


def hr(label):
    print(f"\n{'='*60}")
    print(f"  {label}")
    print('='*60)


def pp(data):
    print(json.dumps(data, indent=2))


def main():
    print(f"Connecting to {PIPE_NAME} ...")
    try:
        f = open(PIPE_NAME, "r+b", buffering=0)
    except Exception as e:
        print(f"FAILED to open pipe: {e}")
        print("Make sure Revit is open and Server is Started (BIM Monkey > Start Server).")
        sys.exit(1)
    print("Connected.\n")

    # ── 1. Ping ──────────────────────────────────────────────────────────────
    hr("1. PING")
    r = send(f, "ping")
    pp(r)

    # ── 2. Get sheets ─────────────────────────────────────────────────────────
    hr("2. GET SHEETS")
    r = send(f, "getSheets")
    sheets = r.get("result", {}).get("sheets") or []
    total = r.get("result", {}).get("totalSheets", 0)
    print(f"Total sheets: {total}")
    for s in sheets[:5]:
        print(f"  {s.get('sheetNumber'):8} | {s.get('sheetName')} (views: {s.get('viewCount', 0)})")
    if total > 5:
        print(f"  ... and {total - 5} more")

    # ── 3. Get unplaced views ─────────────────────────────────────────────────
    hr("3. GET UNPLACED VIEWS")
    r = send(f, "getUnplacedViews")
    result = r.get("result", {})
    print(f"Total views:    {result.get('totalViews', 0)}")
    print(f"Placed views:   {result.get('placedViews', 0)}")
    print(f"Unplaced views: {result.get('unplacedViews', 0)}")

    # ── 4. Get project info ───────────────────────────────────────────────────
    hr("4. GET PROJECT INFO")
    r = send(f, "getProjectInfo")
    pp(r)

    f.close()
    print("\nDone. Pipe is healthy.")


if __name__ == "__main__":
    main()
