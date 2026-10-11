# ROTH Unity 0.10.22: guarded opt-in original DOS RAW9 absolute coordinate endpoints

**Scope:** first conservative, runnable *single-shot* original-coordinate movement prototype, on top of 0.10.21. This is not a complete emulator of the original command scheduler or a Unity-verified build.

## Original game executable: independently checked again

The user-provided GOG `ROTH.EXE` SHA-256 is `e2d54427cd0692798e2df457b1ea8d3bca8cf1383ec2d4ea782165fe0ba56a05`. Local disassembly of **LE object 1** verified four opcode-9 update instruction sequences:

- `0x22C00–0x22C12`: forward speed = tick delta × unsigned byte at command+7; bound = low16(-2 × signed16(command+0xA)).
- `0x22C1A–0x22C22`: forward comparison obtains selected vertex coordinate from animation+0x14, i.e. **the maximum coordinate** after the initializer's stable ascending sort.
- `0x22C3D–0x22C51`: reverse uses negative tick delta × speed; bound = low16(-2 × signed16(command+0xC)).
- `0x22C59–0x22C61`: reverse comparison obtains selected vertex coordinate at animation+0x0E, **the minimum coordinate**.
- `0x22C7E–0x22C9D`: updates all four selected RAW vertex coordinate words by the signed-short delta. The sign and overshoot comparisons are from the previously independently checked `RothDosRaw9Reference.ComputeAxisStep`.

The command initializer `0x22B99–0x22BA4` reads a *dynamic* command-state bit `command[2]&0x02`, NOT only the static arguments: when clear, the initial animation direction bit 0x80 is set (reverse). The Unity command interpreter does not yet track that dynamic state. **Do not assume that the default inspector setting reconstructs an already-activated retail command.**

## Added implementation

1. `Assets/ROTHUnity/Runtime/RothDosRaw9Bounds.cs`: a pure, read-only planner translates the verified signed-16 absolute coordinate target to the already-selected geometry backend's relative offset, using minimum/maximum and checked 64-bit intermediates. Rejects opposite-direction bounds and Int32 overflow rather than permitting a spurious move.
2. `RothMapMeshBuilder.Raw9Selected.cs`: `RuntimeTryReadRaw9SelectedRange` reads the four current selected coordinates without mutating/claiming the sector. Existing selections must still match their cache (to catch another system changing vertices).
3. `RothHorizontalSectorMover.cs`: new `UseDosSingleShotAbsoluteBounds` toggle, **OFF by default**. Requires `UseSelectedRaw9Vertices` AND `UseNominalDosTickPacing`. Rejects timeout != 0, repeat, and same-sector in-flight retriggers, since complete DOS animation states are not implemented. It snapshots the mode and `DosInitialCommandStateBit02` when triggered and advances by `RothDosRaw9Reference.ComputeAxisStep` from the current minimum/maximum each frame, rather than `Time.deltaTime` or `end-start`.
4. Opt-in `RAW9_DOS_BOUNDS|sector|axis|direction|min|max|target|destinationOffset` trace, in addition to existing `RAW9_TRACE` and `ROTH_PIT_TRACE`.
5. `Tools/test_roth_dos_absolute_bounds.py`: 16 synthetic signed-short plan/step model cases, including rejection and overshoot tests. No commercial bytes.

## Testing and activation

Run the source checks and model tests:

```powershell
python Tools\test_roth_dos_absolute_bounds.py
python Tools\test_roth_dos_tick_pacing.py
python -m unittest discover -s Tools -p "test_roth_*.py" -v
```

To test gameplay in Unity 6000.6.0f1:

- Back up your project and original retail RAW maps. Use the 0.10.20 topology inspector to select a safe four-face sector with zero hazard warnings.
- On `RothHorizontalSectorMover`, enable `UseSelectedRaw9Vertices`, `UseNominalDosTickPacing`, AND `UseDosSingleShotAbsoluteBounds`.
- Leave `DosInitialCommandStateBit02` clear to reproduce the DOS first-trigger initializer **assuming** command dynamic state bit 0x02 was initially clear; set it to compare the opposite initial state. This value is **not** automatically recovered from the DOS command scheduler.
- Trigger an opcode-9 command whose timeout argument is zero and auto-repeat flag is clear. Only reachable-direction bounds are accepted. A zero speed byte legitimately yields a stationary movement.
- Turn on `TraceRaw9` and `TraceTicks` and compare X/Y vertices and stop coordinates with the DOS game.

## Verification and limitations

- Direct local audit: 4 original x86 signatures confirmed against the user-uploaded executable.
- 16/16 new Python synthetic model tests passed locally.
- 20/20 static structural checks passed against the exact C# and test files published in GitHub.
- Unity/C# compiler, Editor, Play Mode, actual retail map runtime, physics/mesh exceptions and DOSBox A/B behavior **were not tested**. These checks are *not* proof of a working Unity binary.
- The existing opt-in 0.10.21 mode and legacy movement remain unchanged when the new toggle is off.
- In selected DOS bounds mode, only one-way single-shot completion is implemented. The authoritative command-state XOR/reverse timeout and scheduler callback transitions analyzed in 0.10.16 are not yet integrated. Other actor movement and collision response are still the Unity prototypes.
- The original executable and extracted LE code are never published.

**Next:** inspect original retail RAW maps for valid static sectors, compare in Unity Play Mode, then replace the manual initial command-state inspector control with a dynamic state tracked in the RAW command interpreter and add a separate DOS countdown/return branch with regression tests.
