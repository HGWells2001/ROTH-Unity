# ROTH Unity 0.10.24: signed DOS wait fix and Play Mode diagnostics

**Scope:** source-only RAW9 corrections and diagnostics. No Unity compilation, runtime, or DOSBox A/B verification is claimed.

## Original DOS: corrected wait overflow

The user-owned GOG ROTH.EXE (SHA-256 e2d54427cd0692798e2df457b1ea8d3bca8cf1383ec2d4ea782165fe0ba56a05) contains the verified x86 wait path at LE object1 +0x23033..+0x23041: SUB word [animation+6], AX; signed JG; clear wait bit 0x40 otherwise.

The 0.10.23 model tested the *signed wrapped result*. This loses the x86 overflow flag (OF). The correct JG condition after SUB is **signed(old remaining) > signed(elapsed word)**, independent of the signedness of the wrapped result. The C# update stores the low16 wrapped subtraction and applies the original-operand signed comparison using explicitly unchecked casts.

- 0x8000 (-32768) minus 1 yields 0x7FFF; JG is **false** despite the positive result.
- 0x7FFF (+32767) minus 0xFFFF (-1) yields 0x8000; JG is **true** despite the negative result.

Tools/test_roth_raw9_dynamic_state.py adds four regression tests, including a matrix of 48 signed-operand combinations.

## Safer RAW9 retriggers

In the optional DOS state-machine mode, RothHorizontalSectorMover now rejects an active command retrigger if it changes map generation, sector axis, geometry backend, timebase, signed coordinate bounds, raw command flags, repeat flag or timeout. It prechecks the four selected original vertices and validates the *opposite direction's* absolute bound **before modifying command-state bits**. This is a defensive Unity restriction, not necessarily original DOS behavior.

## New Play Mode trace checker

Tools/audit_roth_raw9_playmode.py correlates the existing RAW9_TRACE (geometry step), ROTH_PIT_TRACE (nominal DOS integer tick count) and RAW9_DOS_STATE (animation state) logs. It reports per-sector movement and warnings for step > speed×ticks, wrong axis, reversed direction, movement while waiting, absent or duplicate PIT frames, and invalid log fields. It does not assert that a valid Unity trace matches the retail game.

No state trace means **legacy or unverified mode**, not an assumed DOS mode. Diagnostics cannot establish original collision, physics or scripted transitions.

### Usage (PowerShell, in the repository folder)

    python Tools\test_roth_raw9_dynamic_state.py
    python Tools\test_roth_raw9_playmode.py
    python Tools\audit_roth_opcode9_states.py "C:\Program Files (x86)\GOG Galaxy\Games\Realms of the Haunting\ROTH\ROTH.EXE" --json original_opcode9_evidence.json

In Unity 6000.6.0f1, test a previously audited safe RAW sector. All four original-DOS RAW9 experimental options must be enabled to use the dynamic state machine. Enable TraceRaw9 on RothHorizontalSectorMover and TraceTicks on RothDosTickClock. After Play Mode, run:

    python Tools\audit_roth_raw9_playmode.py "C:\Users\YOUR_USER\AppData\Local\Unity\Editor\Editor.log" --json raw9_playmode_024.json

Adjust the log path to your Unity installation. The tool reads logs only. A clean report is not proof of DOS fidelity.

## Evidence, tests and next task

- Revalidated the original user-owned GOG ROTH.EXE SHA-256 and ran its existing static opcode9 audit (30 known x86 instruction signatures).
- 44 local Python tests passed (25 signed-wait/command-state, 19 log-consistency tests).
- The new log checker and test files matched the blobs published on GitHub byte-for-byte.
- A synthetic sample log produced two movement rows, two timer rows, two state rows and no consistency warnings.
- The full existing project Python suite was not rerun; no C# compilation or Unity Play Mode was available.
- Previous experimental movement modes remain OFF by default.

**Next:** analyze an actual retail room in Unity Play Mode using this checker, then compare the same motion under DOSBox and resolve concrete timing, collision and portal differences.

No original game binaries, extracted code objects, or commercial maps are redistributed.
