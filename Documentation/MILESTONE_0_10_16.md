# ROTH Unity 0.10.16: evidence-backed opcode 9 completion and countdown

**Status:** reverse-engineering / diagnostic tooling. This milestone does **not** replace the current experimental Unity mover. The original runtime counter frequency, memory ownership and some cross-routine state transitions are still unproven.

## Input and integrity

Analyzed the user-owned GOG `ROTH.EXE`, SHA-256 `e2d54427cd0692798e2df457b1ea8d3bca8cf1383ec2d4ea782165fe0ba56a05`, as MZ+LE code for Intel 80386. All addresses below are **LE object 1 offsets**, not raw file offsets or virtual addresses. This analysis depends on the 0.10.15 double-dispatcher correction: opcode-9 initializer `0x22A99`, movement callback `0x22BD9`, not the opcode-7 callback at `0x22D51`.

## Verified structural details

The state analyzer validates **30 instruction signatures** at fixed offsets **and** the LE-relocated frame delta `object3+0x1570C`. It also calls the earlier verifier, which resolves both dispatchers through the LE fixups. It fails closed if a checked instruction changes.

- Initial command, `0x22B99`–`0x22BA4`: tests the **dynamic command state byte at `command+2` bit `0x02`** before selecting initial animation direction bit `0x80`. This is *not* merely the option flags at `command+6`.
- Existing active command, `0x22BB1`–`0x22BD5`: when the timeout at `command+0xE` is zero, the options byte at `command+6` has bit `0x20`, and the active animation exists, it **XORs `anim+5` with `0x80`** and **XORs `command+2` with `0x02`**. This is a real re-trigger behavior not modeled by the prototype's simple replacement of a Motion record.
- Per-update movement, `0x22BF0`–`0x22C9E`: tests `anim+5` bit `0x40` for countdown and bit `0x80` for direction. The signed 16-bit target is the **low 16 bits of `-2 × signed16(command word at +0xA or +0xC)`**. The movement increment is an unsigned byte speed from `command+7` multiplied by a global per-update delta. The operation adjusts four selected coordinate words.
- Timer, `0x23033`–`0x23041`: subtracts `AX` (low 16 bits of global delta) from `anim+6`, **branches with signed `JG`**, and otherwise clears bit `0x40`. This is a DOS counter countdown, not `Time.time`.
- Completion, `0x22FD1`–`0x23032`: chooses among command-state termination, delayed reversal, and toggling the command's initial-direction state. Its decisions depend on the animation's `0x10` and `0x20` bits, options bit `0x20`, and whether the timeout word is nonzero.

## Completion truth table, assuming animation bit 0x10 = 0

| Timeout word | command+6 bit 0x20 | anim+5 bit 0x20 | Verified branch | Return value |
|---|---|---|---|---:|
| Zero | Clear | Either | `command[2] &= 0xDE; command[2] \|= 0x08` | -1 |
| Zero | Set | Either | `command[2] &= 0xDE; command[2] ^= 0x02` | -1 |
| Nonzero | Clear | Either | `anim[5] ^= 0xE0; anim[6:8] = timeout` | 0 |
| Nonzero | Set | Clear | `anim[5] ^= 0xE0; anim[6:8] = timeout` | 0 |
| Nonzero | Set | Set | `command[2] &= 0xDE` | -1 |

A set **animation bit 0x10** takes priority over the above table and executes `anim[5] ^= 0x90`, returning 0. Return `-1` represents the callback's termination status; this alone does **not** establish that the corresponding command can never be retriggered.

**Important:** The bit historically called "Auto Repeat" (`command+6` bit `0x20`) does **not** directly correspond to the Unity boolean `Repeat`. Some flag combinations appear to enter more than one delayed reversal, but determining the complete runtime lifecycle also requires tracing the scheduler and cross-command interactions. The truth table describes *these machine-code branches only*, not guaranteed gameplay outcomes.

## How to reproduce

From the ROTH Unity repository root, with the unmodified user-owned executable:

```powershell
python Tools\audit_roth_opcode9_states.py "C:\Program Files (x86)\GOG Galaxy\Games\Realms of the Haunting\ROTH\ROTH.EXE" --json raw9_states.json
python Tools\test_roth_opcode9_states.py
python Tools\test_roth_opcode9.py
python Tools\test_roth_le.py
```

On the supplied binary: 30 byte signatures and both relocated dispatcher checks passed. The new file has **18 synthetic Python tests**; the existing 12 opcode-9 and 10 LE parser tests still pass (40 total). They are not Unity integration tests.

## Implications for Unity (not yet wired in)

1. Keep the existing `RothHorizontalSectorMover` experimental. Do not replace `MoveTowards` with guessed "faithful" code.
2. The eventual DOS mode needs to track a command's dynamic state byte, per-animation state byte, a 16-bit timer and a supplied *DOS delta*.
3. Movement goals must be mapped from four selected original RAW coordinates and doubled/negated command bounds before applying any engine coordinate conversion.
4. Retry/trigger behavior should follow the `0x22BB1` gate and XOR semantics; suppressing or replacing a running motion is not faithful for all cases.
5. To calibrate actual-time movement, we still need the provenance/frequency of the 16-bit counter read by the game loop, plus playtests with user-owned maps.

No copyrighted game bytes, disassembly, or extracted LE objects are distributed.