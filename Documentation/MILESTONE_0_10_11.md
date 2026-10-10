# ROTH Unity 0.10.11: RAW9 trace analysis

This release adds a **read-only**, standard-library-only analysis tool to help compare RAW opcode 9 motion with the original DOS game once an independently captured DOS reference is available.

## Files
- `Tools/analyze_raw9_trace.py`: tolerant parsing of `RAW9_TRACE|` Unity log lines, JSON summaries per sector, diagnostics for out-of-range motion, player obstruction and failed rollback.
- `Tools/test_raw9_trace.py`: 8 automated Python unit tests with synthetic traces; does **not** require retail game files.

## Usage

1. Open the project in Unity 6000.6.0f1 and enable `TraceRaw9` on the dynamically instantiated `RothHorizontalSectorMover` component after the first trigger.
2. Trigger sector movement and save a copy of the Unity Player/Editor log.
3. Run `python Tools/analyze_raw9_trace.py path/to/Player.log --json raw9_report.json`
4. Run `python Tools/test_raw9_trace.py` and `python Tools/test_raw9_regression.py`

The trace parser expects exactly the 19 fields published in the 0.10.10 milestone. It ignores unrelated log lines but rejects malformed tagged records with exit code 2. The per-sector movement distance counts **committed integer RAW position deltas**, not sub-integer interpolation or video-measured displacement.

## Provenance and limitations

The published `slidick/roth-editor` catalog labels opcode 9 as `Move Sector`, with six args, texture follow bits 1-4, repeat bit 6, X-axis bit 7, and auto-revert arg 5. This **independent catalog** supports field naming, **not** the actual DOS movement routine or speed/tick semantics. The current Unity logic for start-end displacement, speed, auto return and repeat remains hypothetical until validated against DOS observations.

There is **no DOS executable** and no retail RAW/DAS archive in this workspace. No DOS execution trace was compared here. There is no Unity editor/builder available here either. This is a tooling milestone, not a claim of game fidelity or a Windows build. No copyrighted game assets are included.
