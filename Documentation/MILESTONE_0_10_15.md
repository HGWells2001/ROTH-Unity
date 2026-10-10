# ROTH Unity 0.10.15: Verified opcode-9 animation dispatch, correction of 0.10.14

**Scope:** Ground-truth reverse engineering of the user-supplied GOG `ROTH.EXE`, not a Unity runtime change or proof of game parity. No retail binary bytes included in repository or distribution.

## Evidence specimen

- `ROTH.EXE` SHA-256: `e2d54427cd0692798e2df457b1ea8d3bca8cf1383ec2d4ea782165fe0ba56a05`.
- MZ/LE, Intel 80386, 3 objects; all 14,968 LE relocation records decoded by the existing audit.
- All offsets below are *object-1 relative*, not file offsets; add preferred object-1 virtual base `0x10000` only when the LE loader preserves it.

## Critical correction of version 0.10.14

The previous report identified index 74 (object 1 + `0x22D51`) as the opcode-9 motion update. **That assignment was incorrect.** Index 74 is the animation update for **RAW opcode 7 (vertical floor/ceiling)**. Its optional 6-bit fractional accumulator therefore cannot be attributed to opcode 9.

The original DOS program uses two indirect calls into *the same relocated 128-entry pointer table* with different bases:

| Dispatch role | x86 indirect-call offset | LE-relocated pointer base | Table index expression |
|---|---|---|---|
| Command initializer | object1 + `0x2598D` | object1 + `0x20740` | `command_type & 0x7f` |
| Animation callback | object1 + `0x247CC` | object1 + `0x2084C` | `animation_type + 67` |

The callback dispatcher reads `movzx ebx, BYTE [eax+4]` immediately before the indirect call. The opcode-9 initializer at object1 + `0x22A99` stores `command[3]` in `animation[4]` at object1 + `0x22BA4`. Consequently, the animation dispatcher calls table index **9+67=76** for opcode 9:

- **Opcode 9 initializer:** table entry 9 → object1 + **`0x22A99`**.
- **Opcode 9 callback:** table entry 76 → object1 + **`0x22BD9`**.
- **Opcode 7 callback:** table entry 74 → object1 + **`0x22D51`**.

The animation call's object-1 relocation is at `0x247CF` and resolves to table base `0x2084C`. The command initializer call's relocation is at `0x25990` and resolves to `0x20740`. These two cross-references establish the mapping independently of code adjacency.

## What the actual opcode-9 callback proves

- object1 + `0x22BEB`: reads a 32-bit frame-delta value, relocated to object3 + `0x1570C`.
- object1 + `0x22C00` and `0x22C3D`: `movzx edx,BYTE [edi+7]`, `imul eax,edx` (two directional branches). Speed is the **unsigned command byte** times the frame-delta value; one branch negates the product.
- The branches compare **signed 16-bit map coordinates** with `-2 * (signed word at command[0xA]/[0xC])` and clamp at the target. The callback updates four referenced 16-bit map coordinates. Do not reduce this to a blind `end-start` offset without verifying which vertices and coordinate axis are selected.
- The low command flag bits 1–4 gate texture offset updates. The motion-state high flags and the command flag `0x20` occur in return / re-trigger logic around object1 + `0x22FD1..0x23032`. Their complete lifecycle needs trace-based tests before rewriting Unity.
- **No 6-bit remainder/`>> 6` step appears in the verified opcode-9 callback.** The fraction signature occurs at object1 + `0x22D6F` in the *opcode-7* update.

## Origin of the frame-delta variable

Object1 + `0x102D2` and object1 + `0x1046D` each read the signed 16-bit counter at object1 + `0x20FAC`, subtract a previous snapshot, then write the difference to the object3 + `0x1570C` global (stores at `0x102DE` and `0x10479`). Relocations validate these global store operands. Some loops initialize the delta to **2** before updates and bound an accumulation variable near **`0x3E`**. These facts do **not** yet establish real-world seconds per tick or a safe Unity conversion factor.

## Reproducible verification

From repository root, using the user's own **unmodified** GOG executable:

```
python Tools/audit_roth_opcode9.py "C:/Program Files (x86)/GOG Galaxy/Games/Realms of the Haunting/ROTH/ROTH.EXE" --json opcode9_corrected.json
python Tools/test_roth_opcode9.py
python Tools/test_roth_le.py
```

The patched audit reads the LE fixups, follows both indirect dispatch sites, checks the opcode-9 and opcode-7 callback signatures, checks the relocated tick reads, and identifies two loop delta writers. On this specimen: **12/12 synthetic regression checks passed**, plus **10/10 tests for the LE parser**. No Unity build has been attempted here.

## Next work

1. Follow the opcode-9 `animation[5]` state transition branches (`0x22FD1..0x23032`) to derive exact auto-return behavior and initial direction.
2. Establish the timer source frequency by tracing writes to object1 + `0x20FAC` in the DOS host (do not guess 60 Hz / 70 Hz).
3. Derive a transform between original signed vertex coordinates, command bounds and Unity map coordinates, then test with original retail map data.
4. Only after that, replace Unity's provisional `RawUnitsPerSecond`, `RawSpeedPerFlagUnit`, `AutoRevertTickSeconds` and `end-start` interpretation.

This is a **corrective source-analysis milestone**, not a playable release. No executable, raw DOS object, map or other proprietary game data is redistributed.