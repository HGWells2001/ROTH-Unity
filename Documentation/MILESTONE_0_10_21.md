# ROTH Unity 0.10.21: opt-in PIT-paced selected RAW9 movement

**Scope:** experimental Unity pacing extension on top of 0.10.20. No retail files shipped. This is NOT DOS-equivalent movement or a Unity-tested build.

## Ground truth

The user-owned GOG ROTH.EXE (SHA-256 `e2d54427cd0692798e2df457b1ea8d3bca8cf1383ec2d4ea782165fe0ba56a05`) programs PIT channel 0 with running divisor `0x4216`, nominally 70.52737 ticks/sec at 1,193,182 Hz. Original IRQ0 increments a 16-bit counter. A gameplay loop caps its sampled frame delta at 45. See `MILESTONE_0_10_17.md` for the x86 and LE-relocation evidence. This is a nominal hardware calculation, not a measured DOSBox frame rate.

## New runtime functionality

- `RothDosTickClock.cs`: independent component, early Unity execution order, fractional phase accumulator, 16-bit wrapping counter, integer ticks, configurable safety cap 1–45, optional scaled/unscaled Unity time and `ROTH_PIT_TRACE` diagnostics. Its `Advance(seconds)` method permits deterministic manual tests. No game files are read.
- `RothHorizontalSectorMover.cs`: **UseNominalDosTickPacing** inspector toggle, OFF by default. Requires **UseSelectedRaw9Vertices**, also OFF by default. Per motion it snapshots whether pacing is active. When tick-paced, step allowance is the unsigned high-byte command speed multiplied by the current frame's capped nominal DOS ticks. The old `Time.deltaTime` path is unchanged when the toggle is OFF.
- The clock component is attached automatically if tick pacing is requested and no clock exists. A missing or disabled clock during an existing motion causes cancellation, not a silent fallback to the old speed.
- The existing `RAW9_TRACE` format is preserved. Enable `TraceTicks` on the clock for `ROTH_PIT_TRACE|frame|unityTime|counter16|elapsedTicks|pacingTicks|fraction`.

## Run / evaluate

1. Open the latest project in Unity 6000.6.0f1, with a backup of the project and retail RAW data. Perform the 0.10.20 topology audit and choose a safe four-face sector.
2. Baseline: both `UseSelectedRaw9Vertices` and `UseNominalDosTickPacing` OFF.
3. Geometry comparison: only `UseSelectedRaw9Vertices` ON.
4. Experimental timebase: both switches ON. Trigger the same opcode9 case and inspect gameplay plus the Unity Console. Default `MaxPacingTicksPerFrame=45` is a conservative safety cap, not proof of exact scheduler behavior.
5. In the project folder, run `python Tools/test_roth_dos_tick_pacing.py`, then `python -m unittest discover -s Tools -p "test_roth_*.py" -v`.

## Verification, limitations

- 14/14 new deterministic Python model tests passed.
- 54/54 locally available Python tests passed (older 0.10.15/0.10.16 suite plus 14 new tests).
- 16/16 checks against the actual committed C# source and new test file passed.
- The full previously documented 0.10.20 suite was **not** rerun here; no C# compilation, Unity Editor or Play Mode verification was possible. Source contracts and Python calculations cannot prove Unity correctness.
- **Movement destination `end-start` remains a provisional legacy approximation.** Auto-revert, repeats, collisions and object translation also remain experimental. The nominal PIT tick clock does not fully reproduce a DOS ISR, nor does the 45-tick gameplay safety cap prove it is used by all original movement paths. Retain the two opt-in toggles for A/B comparison.

**Next:** test an original owned retail RAW map and game room in Unity Play Mode and DOSBox, then use the 0.10.16 command state machine and 0.10.18 four-vertex selection evidence to replace provisional target and return behavior in another independently switchable mode.
