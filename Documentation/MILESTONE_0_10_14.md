> **CORRECTION (0.10.15):** The 0x22D51 callback at table index 74 belongs to RAW opcode **7**, not opcode 9. The real opcode-9 animation callback is **0x22BD9** (table index 76), verified through the second relocated indirect animation dispatcher at 0x247CC. The 6-bit fraction path belongs to opcode 7. See [MILESTONE_0_10_15.md](MILESTONE_0_10_15.md). The remainder below is retained as the historical investigation record.

# ROTH Unity 0.10.14 | Original DOS RAW opcode 9 identified

**Milestone type: evidence and instrumentation.** No Unity physics or geometry semantics were silently changed. This is a significant reverse-engineering milestone but not yet runtime parity.

## Ground truth

Original user-provided GOG `ROTH.EXE`, SHA-256 `e2d54427cd0692798e2df457b1ea8d3bca8cf1383ec2d4ea782165fe0ba56a05`, 472,915 bytes. MZ + Watcom 32-bit LE. There are **14,968 LE relocation records**, all 32-bit internal offsets. The object 1 → object 1 mappings include a long run of relocated function pointers. One suspiciously large signed-looking object-3 target is reported, not silently rewritten.

## New verified control flow

The code at **object 1 + `0x25987`** loads `BYTE [esi+3]`, masks with `0x7F`, then calls through a relocated pointer table:

```asm
mov bl, BYTE [esi+3]
and ebx, 0x7f
call DWORD [ebx*4 + 0x20740]  ; unrelocated object-1 offset
```

LE relocation at object 1 + **`0x25990`** points to **object 1 + `0x20740`**. This confirms the 128-entry opcode dispatcher. Entry **9** is at **object 1 + `0x20764`**; its relocation targets **object 1 + `0x22A99`**, the verified opcode-9 entry function. The first instruction is `sub eax,eax`; the next tests command state bit `BYTE [esi+2] & 0x20`.

With the LE's original object-1 preferred base `0x10000`, Ghidra virtual addresses will be **`0x35987`**, **`0x3598D`** (indirect CALL), **`0x30740`** (pointer table) and **`0x32A99`** (opcode 9). This assumes the loader preserves preferred bases.

## Opcode 9 entry: observed operations

The opcode-9 entry at **object 1 + `0x22A99`**:
- Tests the state bit `command[2] & 0x20`, handles a preexisting motion differently.
- Reads `WORD [esi+8]` and calls object 1 + `0x3F2B0` to resolve a map/sector record (call target is code-relative; its role remains to be examined).
- Allocates an animation/work structure via object 1 + `0x24438` and retains the original command pointer.
- Tests `BYTE [esi+6] & 0x40`, affecting whether a base offset **8** or **10** is used in the coordinate structure.
- Reads four coordinate/vertex references, sorts coordinate pairs, and stores offsets into its motion structure.
- Marks `command[2] |= 0x20` and stores fields for later execution.

This is a **live-sector-based operation**, not proof that `end-start` is the complete movement distance. The existing Unity `Destination = existing + end - start` remains **unverified**.

## Movement update: confirmed arithmetic

The same relocated dispatch table has two candidate animation callback entries: index 74 -> **object 1 + `0x22D51`**, and index 76 -> **object 1 + `0x22BD9`**. The index-74 body is directly compatible with opcode-9 moving-sector work and its location follows the opcode-9 initializer in the same code cluster. Precise registration/dispatch relationships should still be confirmed by cross-reference or DOS tracing.

In object 1 + `0x22D51`, the code does the following:

```asm
mov eax, DWORD [global_from_object_3 + 0x1570C]
movzx edx, BYTE [command+7]
imul eax, edx
...                         ; when command[6] & 4:
add eax, (animation[6] & 63)
mov BYTE [animation+6], al
shr eax, 6
```

The reference to the global is verified by a relocation from object 1 + `0x22D5A` to **object 3 + `0x1570C`**. The bytes at `[command+7]` are the high-byte speed field of the first 16-bit command argument, consistent with the editor's `Flags/Speed` label. A low flag (`0x04`) gates fractional accumulation and a `>> 6` shift. The **meaning and unit of the global time/delta variable, and how other flags affect the operation, still need verification**. Do not replace Unity's speed constants with a made-up numerical conversion.

The update branches inspect signed 16-bit boundaries at `[command+0x0A]` and `[command+0x0C]`, multiply them by two, and directly update signed 16-bit values in the map's vertex/coordinate array. The update invokes additional geometry refresh routines. This is substantially more specific than a generic rigid translation and suggests the current Unity mesh translation needs refinement.

## Negative identification

Another 64-pointer table was found at **object 3 + `0x21E0`**, but the caller around object 1 + `0x27345` computes display/blitter flags and nearby code advances by screen scanline stride `0x140`. That table is **not** evidence of the RAW command-9 dispatcher. The true dispatcher is the 128-entry table in object 1 described above.

## Reproducibility

Place these scripts in the repository's `Tools/` folder:

```
python Tools/inspect_roth_le.py path/to/ROTH.EXE
python Tools/inspect_roth_fixups.py path/to/ROTH.EXE
python Tools/audit_roth_opcode9.py path/to/ROTH.EXE --json opcode9_evidence.json
python Tools/test_roth_opcode9.py
```

The audit looks up opcode 9 through a distinctive command-byte mask and a verified LE relocation to a 128-entry table. Synthetic fixture tests verify this signature chain and reject invalid variants. **8/8 automated tests passed locally.** No proprietary executable pages or disassembly dumps are packaged.

## Unresolved before changing Unity runtime

1. Follow the DOS motion update at object 1 + `0x22D51`/`0x22BD9` through calls to object 1 + `0x248CD` and neighboring handlers.
2. Decode fractional update/state bits independently of texture-follow bits. Some bit labels from third-party editors may conflate fields or have unverified meanings.
3. Prove which command fields correspond to start and end bounds and the exact unit conversion.
4. Identify the update scheduling and origin of object 3 + `0x1570C` to derive speed per real second.
5. Compare actual map movement from DOS runtime with Unity, including retriggers, revert, repeat, collision and texture shifts.

**Unity unchanged:** this milestone deliberately preserves the existing experimental 0.10.10 motion code until these semantics are proven.

## External technical references

- IBM OS/2 Linear Executable (LE/LX) documentation: https://komh.github.io/os2books/os2tk45/lxref.htm
- Open Watcom LE structure/flags: https://github.com/open-watcom/open-watcom-v2/blob/master/bld/watcom/h/exeflat.h
- Ghidra LE loader: https://github.com/yetmorecode/ghidra-lx-loader
- Third-party ROTH command index: https://github.com/slidick/roth-editor

No original game binaries or extracted objects are redistributed.