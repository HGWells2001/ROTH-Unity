#!/usr/bin/env python3
"""Validate and summarize Unity RAW9_TRACE lines; no retail assets required.

Usage: python Tools/analyze_raw9_trace.py unity-player.log
       python Tools/analyze_raw9_trace.py unity-player.log --json report.json

The trace is diagnostic, not evidence of DOS-faithfulness.
"""
import argparse
import collections
import json
import math
from pathlib import Path
import sys

COLUMNS = (
    "frame", "time", "sector", "axis", "event",
    "before_x", "before_y", "after_x", "after_y",
    "start", "end", "position", "destination", "speed",
    "player_x", "player_y", "player_z", "flags", "revert_ticks",
)
EVENTS = {
    "trigger", "step", "bounds-rejected", "rider-blocked",
    "rollback-failed", "arrived", "returned", "repeat",
    "return", "return-obstructed",
}
INTEGER = {"frame", "sector", "before_x", "before_y", "after_x",
           "after_y", "start", "end", "flags", "revert_ticks"}
FLOAT = set(COLUMNS) - INTEGER - {"axis", "event"}

def parse_line(line, line_number):
    if "RAW9_TRACE|" not in line:
        return None
    pieces = line.split("RAW9_TRACE|", 1)[1].strip().split("|")
    if len(pieces) != len(COLUMNS):
        raise ValueError(f"line {line_number}: expected {len(COLUMNS)} columns, got {len(pieces)}")
    obj = dict(zip(COLUMNS, pieces))
    try:
        for key in INTEGER:
            obj[key] = int(obj[key])
        for key in FLOAT:
            obj[key] = float(obj[key])
            if not math.isfinite(obj[key]):
                raise ValueError(f"{key} is not finite")
    except ValueError as exc:
        raise ValueError(f"line {line_number}: {exc}") from exc
    if obj["axis"] not in ("X", "Y"):
        raise ValueError(f'line {line_number}: invalid axis {obj["axis"]!r}')
    if obj["event"] not in EVENTS:
        raise ValueError(f'line {line_number}: unknown event {obj["event"]!r}')
    if not (0 <= obj["sector"] <= 65535 and 0 <= obj["flags"] <= 65535
            and 0 <= obj["revert_ticks"] <= 65535):
        raise ValueError(f"line {line_number}: invalid unsigned 16-bit command value")
    obj["source_line"] = line_number
    return obj

def analyze(text):
    records = []
    errors = []
    for number, line in enumerate(text.splitlines(), 1):
        try:
            entry = parse_line(line, number)
            if entry is not None:
                records.append(entry)
        except ValueError as exc:
            errors.append(str(exc))
    summaries = {}
    for sector, group_iter in __import__("itertools").groupby(
        sorted(records, key=lambda r: (r["sector"], r["source_line"])),
        key=lambda r: r["sector"]
    ):
        group = list(group_iter)
        counts = collections.Counter(r["event"] for r in group)
        step = [r for r in group if r["event"] == "step"]
        total_raw = sum(abs(
            (r["after_x"] - r["before_x"]) if r["axis"] == "X"
            else (r["after_y"] - r["before_y"])
        ) for r in step)
        timestamps = [r["time"] for r in group]
        warnings = []
        if counts["rollback-failed"]:
            warnings.append("sector rollback failed")
        if counts["bounds-rejected"]:
            warnings.append("RAW 16-bit boundary rejected motion")
        if counts["rider-blocked"]:
            warnings.append("player movement obstructed")
        if counts["trigger"] > counts["arrived"] + counts["returned"]:
            warnings.append("some triggered motions have no logged completion")
        if any(a > b for a, b in zip(timestamps, timestamps[1:])):
            warnings.append("time moved backwards; possible app session reset")
        summaries[str(sector)] = {
            "events": dict(counts), "total_committed_raw_distance": total_raw,
            "first_time": min(timestamps), "last_time": max(timestamps),
            "warnings": warnings,
        }
    return {"trace_rows": len(records), "malformed_rows": errors, "sectors": summaries}

def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("log", type=Path)
    ap.add_argument("--json", type=Path, dest="json_output")
    args = ap.parse_args(argv)
    report = analyze(args.log.read_text(encoding="utf-8-sig", errors="replace"))
    output = json.dumps(report, indent=2, sort_keys=True)
    print(output)
    if args.json_output:
        args.json_output.write_text(output + "\n", encoding="utf-8")
    return 2 if report["malformed_rows"] else 0

if __name__ == "__main__":
    sys.exit(main())
