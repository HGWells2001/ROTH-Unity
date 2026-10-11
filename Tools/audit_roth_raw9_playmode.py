#!/usr/bin/env python3
"""Correlate Unity RAW9_TRACE, RAW9_DOS_STATE and ROTH_PIT_TRACE.

Read-only diagnostic for the opt-in DOS experiment. The report flags
inconsistent steps and unavailable trace data, not proof of DOS fidelity.

Usage: python Tools/audit_roth_raw9_playmode.py Player.log --json raw9_audit.json
"""
import argparse
import json
import math
from pathlib import Path
import sys

from analyze_raw9_trace import parse_line as parse_motion

PIT_FIELDS = ("frame", "unity_time", "counter16", "elapsed_ticks", "pacing_ticks", "fraction")
STATE_FIELDS = ("frame", "command_index", "sector", "event", "command_state",
                "animation_flags", "wait_ticks", "active")
STATE_EVENTS = ("trigger", "retrigger", "wait-finished", "wait-armed", "reverse", "arrived")


def parse_tagged(line, number, tag, fields, numeric):
    if tag not in line:
        return None
    values = line.split(tag, 1)[1].strip().split("|")
    if len(values) != len(fields):
        raise ValueError(f"line {number}: {tag[:-1]} needs {len(fields)} fields, got {len(values)}")
    row = dict(zip(fields, values))
    try:
        for k in numeric:
            row[k] = int(row[k]) if k != "fraction" and k != "unity_time" else float(row[k])
    except ValueError as error:
        raise ValueError(f"line {number}: invalid {tag[:-1]} number: {error}") from error
    row["source_line"] = number
    return row


def parse_pit(line, number):
    row = parse_tagged(line, number, "ROTH_PIT_TRACE|", PIT_FIELDS, PIT_FIELDS)
    if row is None:
        return None
    if (row["frame"] < 0 or row["counter16"] not in range(65536)
            or not 0 <= row["pacing_ticks"] <= 45 or row["elapsed_ticks"] < 0
            or row["pacing_ticks"] > row["elapsed_ticks"]
            or not math.isfinite(row["unity_time"])
            or row["unity_time"] < 0
            or not math.isfinite(row["fraction"])
            or not 0 <= row["fraction"] < 1):
        raise ValueError(f"line {number}: impossible PIT counter, tick or fraction value")
    return row


def parse_state(line, number):
    row = parse_tagged(line, number, "RAW9_DOS_STATE|", STATE_FIELDS,
                       ("frame", "command_index", "sector", "command_state",
                        "animation_flags", "wait_ticks", "active"))
    if row is None:
        return None
    if (row["frame"] < 0 or not 0 <= row["command_state"] <= 255
            or not 0 <= row["animation_flags"] <= 255
            or not 0 <= row["wait_ticks"] <= 65535 or row["active"] not in (0, 1)
            or not 0 <= row["sector"] <= 65535 or row["command_index"] < 0
            or row["event"] not in STATE_EVENTS):
        raise ValueError(f"line {number}: invalid DOS command-state record")
    return row


def inspect(log):
    """Check trace-internal invariants; do NOT call these DOSBox accuracy tests."""
    motion, pit, state, parse_errors = [], [], [], []
    for lineno, line in enumerate(log.splitlines(), 1):
        try:
            m = parse_motion(line, lineno)
            if m is not None:
                motion.append(m)
            p = parse_pit(line, lineno)
            if p is not None:
                pit.append(p)
            s = parse_state(line, lineno)
            if s is not None:
                state.append(s)
        except ValueError as error:
            parse_errors.append(str(error))

    warnings = []
    pit_by_frame = {}
    for row in pit:
        frame = row["frame"]
        if frame in pit_by_frame:
            warnings.append(f"duplicate PIT trace for frame {frame}")
        else:
            pit_by_frame[frame] = row

    timeline = sorted(motion + state, key=lambda x: x["source_line"])
    sector_state = {}
    by_sector = {}
    for row in timeline:
        sector = row["sector"]
        info = by_sector.setdefault(str(sector), {
            "steps": 0, "movement_units": 0, "state_records": 0,
            "examined_steps": 0, "warnings": [],
        })
        if "animation_flags" in row:
            if row["event"] == "trigger" or row["event"] == "arrived":
                if row["event"] == "arrived":
                    # No further frames should inherit a terminal state.
                    sector_state.pop(sector, None)
                else:
                    sector_state[sector] = row
            else:
                sector_state[sector] = row
            info["state_records"] += 1
            continue
        if row["event"] != "step":
            continue
        info["steps"] += 1
        dx, dy = row["after_x"] - row["before_x"], row["after_y"] - row["before_y"]
        if (row["axis"] == "X" and dy) or (row["axis"] == "Y" and dx):
            info["warnings"].append(f"line {row['source_line']}: non-moving axis changed")
        delta = dx if row["axis"] == "X" else dy
        info["movement_units"] += abs(delta)
        active = sector_state.get(sector)
        if active is None:
            # This may be legacy motion: do not pretend it uses DOS ticks.
            continue
        info["examined_steps"] += 1
        if active["animation_flags"] & 0x40:
            info["warnings"].append(f"line {row['source_line']}: vertex movement during DOS wait")
        reverse = bool(active["animation_flags"] & 0x80)
        if (reverse and delta > 0) or (not reverse and delta < 0):
            info["warnings"].append(f"line {row['source_line']}: movement opposes DOS direction")
        p = pit_by_frame.get(row["frame"])
        if p is None:
            info["warnings"].append(f"line {row['source_line']}: missing PIT trace for frame {row['frame']}")
            continue
        speed = (row["flags"] >> 8) & 255
        allowance = p["pacing_ticks"] * speed
        if abs(delta) > allowance:
            info["warnings"].append(
                f"line {row['source_line']}: moved {abs(delta)} RAW units but PIT/speed allowance is {allowance}")
    for section in by_sector.values():
        warnings.extend(section["warnings"])
    return {
        "motion_rows": len(motion), "pit_rows": len(pit),
        "state_rows": len(state), "parse_errors": parse_errors,
        "warnings": warnings, "sectors": by_sector,
        "notes": ["This is a consistency audit of Unity traces, not a DOSBox comparison.",
                  "Enable TraceRaw9 and RothDosTickClock.TraceTicks for complete diagnostics.",
                  "Legacy or non-DOS movements without state traces are not subject to tick checks."],
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("log", type=Path)
    parser.add_argument("--json", type=Path)
    args = parser.parse_args(argv)
    try:
        report = inspect(args.log.read_text(encoding="utf-8-sig", errors="replace"))
        out = json.dumps(report, indent=2, sort_keys=True)
        print(out)
        if args.json:
            args.json.write_text(out + "\n", encoding="utf-8")
        return 2 if report["parse_errors"] else 0
    except OSError as error:
        print(f"Cannot read log: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
