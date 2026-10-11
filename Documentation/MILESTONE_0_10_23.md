# ROTH Unity 0.10.23: opt-in original RAW9 dynamic command state

**Status:** experimental Unity wiring based on validated DOS basic-block branches. Previous three movement modes remain available and the new state machine is OFF by default. This is not proof of DOS-equivalent gameplay.

## Ground truth: source executable

Supplied, user-owned GOG `ROTH.EXE` SHA-256:
`e2d54427cd0692798e2df457b1ea8d3bca8cf1383ec2d4ea782165fe0ba56a05`.

Re-ran `Tools/audit_roth_opcode9_states.py` directly against that executable. It verified **30 opcode9 x86 instruction signatures** and the LE-relocated dispatchers, producing eight completion-state rows. All addresses below are LE **object 1 offsets**, not file offsets:

- `0x22B93`: command+2 state byte gains active bit 0x20.
- `0x22B99`: command+2 bit 0x02 chooses initial animation direction (reverse bit 0x80 when clear).
- `0x22BB1–0x22BD5`: when a command is already active, an existing animation, zero timeout and command options bit 0x20 permit an active re-trigger: animation+5 XOR 0x80, command+2 XOR 0x02. This **does not** create a fresh animation.
- `0x22BF0–0x22C9D`: animation+5 direction/wait flags, tick delta times command speed, signed-bound comparisons and four original vertex coordinate writes.
- `0x22FD1–0x23032`: command-state and animation-state completion truth table, including an animation high-priority 0x10 branch, timeout-zero completion and nonzero timeout phase 0x20.
- `0x23033–0x23041`: signed-16 countdown, subtract the low16 of the frame delta from animation+6 and clear wait bit 0x40 when the signed remainder is <= 0.

The existing **0.10.15 correction remains valid**: opcode-9 callback is object1+0x22BD9; 0x22D51 is the different opcode-7 callback with its own 6-bit accumulator. Nominal PIT frequency is ~70.53 Hz from the 0x4216 divisor.

## New implementation

- `Assets/ROTHUnity/Runtime/RothDosRaw9CommandState.cs` is a dependency-free C# branch model exposing `CommandState`, `AnimationFlags`, `WaitTicks`, `Active`, `Reverse`, `Waiting` and methods `TryActiveRetrigger`, `ConsumeWait`, `Complete`. It only implements verified branch semantics; **it does not impersonate the complete original DOS scheduler**.
- `RothCommandMonitor.cs` now passes both the **one-based command index** and its original `RothCommand.Modifier` to `RothHorizontalSectorMover.Move` (additional optional arguments preserve existing call sites).
- `RothHorizontalSectorMover.cs`: new **UseDosCommandStateTransitions** checkbox, OFF by default. Requires the existing **UseSelectedRaw9Vertices**, **UseNominalDosTickPacing** and **UseDosSingleShotAbsoluteBounds** experimental switches. When enabled, it retains per-command mutable state indexed by the original RAW command identity and guarded by the map generation number.
- On the first trigger, the original command+2 modifier bit 0x02 sets initial direction. On re-trigger, a valid active zero-timeout repeat command toggles direction and bit 0x02 *without* creating a new sector motion. Unsupported concurrent commands in the same sector fail closed.
- At the signed absolute coordinate bound, the verified completion branch either terminates, arms the 16-bit timeout and reverses direction, or takes the special phase path. A waiting update only decrements the DOS tick timer; movement resumes on the following update once the wait bit is cleared.
- `RAW9_DOS_STATE|frame|commandIndex|sector|event|commandState|animationFlags|waitTicks|active` is emitted when `TraceRaw9` is enabled. Existing `RAW9_TRACE`, `RAW9_DOS_BOUNDS` and `ROTH_PIT_TRACE` formats are unchanged.

## Activation / testing on Windows

1. Back up the Unity 6000.6.0f1 project and use **ROTH > Audit original RAW9 vertex references...** to select a safe four-face retail sector without cross-sector topology hazards.
2. Ensure the GameObject with `RothMapMeshBuilder` also has `RothHorizontalSectorMover`. The interpreter adds it automatically only after the first eligible command; adding it manually before Play Mode makes its switches accessible immediately.
3. In its Inspector enable **all four** switches, in order: `UseSelectedRaw9Vertices`, `UseNominalDosTickPacing`, `UseDosSingleShotAbsoluteBounds`, `UseDosCommandStateTransitions`.
4. Enable `TraceRaw9`; enable `TraceTicks` on `RothDosTickClock` to compare nominal pacing. Trigger both zero and nonzero RAW9 timeout commands, and the repeat-bit flag with and without timeout.
5. Review `RAW9_DOS_STATE` before/during/after a return. Use the original retail DOS game under DOSBox for a real movement/time/physics comparison.

Run offline regression tests:

```powershell
python Tools\test_roth_raw9_dynamic_state.py
python Tools\audit_roth_opcode9_states.py "C:\Program Files (x86)\GOG Galaxy\Games\Realms of the Haunting\ROTH\ROTH.EXE" --json raw9_023_states.json
python -m unittest discover -s Tools -p "test_roth_*.py" -v
```

## Verification and known limitations

- Rechecked original retail executable: 30 instruction signatures and eight completion branches recognized.
- Ran **61/61 Python tests** locally: 21 new independent command-state branch-model tests plus 40 earlier DOS structure/opcode-state checks. These are Python tests, **not** C# compilation or Unity Play Mode.
- Ran **20/20 structural source contracts** against the GitHub C# files and Python test file, covering the state masks, timebase gates, original command-index plumbing, legacy mode preservation and log format.
- No C# compiler, Unity Editor, retail RAW room or DOSBox runtime comparison was available to validate new gameplay. No claim of Play Mode success, real collision fidelity, or complete automatic command scripting is made.
- This remains an **experimental shadow state** owned by the mover: other commands that enable/disable or mutate command state are not yet synchronized with this component; the command monitor's disabled-command set is not driven by animation completion. As a conservative guard, the mover rejects reactivating a command its own state marks disabled.
- The Unity player obstruction policy and geometry-object rebuilding remain prototype behavior, not proven DOS behavior. Only one animated command can own a sector at a time. The nominal PIT counter is still a pacing approximation, not a hardware ISR emulator.
- The documented original animation branches do not by themselves prove how the entire scheduling loop handles all map triggers, ownership and terminal states.

## Recommended next milestone

Capture original DOSBox runs and Unity Play Mode logs for a known retail four-sided RAW sector, and integrate command-state propagation for opcode-23 enable/disable and cross-command control only once its original semantics have been verified. Compare object and player interactions and resolve any mesh/collision discrepancies before promoting this mode beyond opt-in testing.

No retail executable, RAW map, DAS files, copyrighted disassembly or extracted game bytes have been committed to the repository.
