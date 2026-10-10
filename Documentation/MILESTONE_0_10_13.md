# ROTH Unity 0.10.13: verified LE image structure from the GOG executable

**Status:** structural reverse-engineering milestone, **not** an opcode-9 handler identification or an executable game build.

## Ground-truth specimen

The user supplied a personally owned GOG `ROTH.EXE`; no executable bytes are included in this update.

- SHA-256: `e2d54427cd0692798e2df457b1ea8d3bca8cf1383ec2d4ea782165fe0ba56a05`
- File size: **472,915 bytes** (`0x73753`)
- MZ stub declared size: **12,810 bytes** (`0x320A`)
- MZ e_lfanew: **12,816** (`0x3210`), containing **LE** little-endian magic
- LE `cpu_type=2`: 80386 (per LE specification); runtime entry uses 32-bit instruction encoding
- LE page count **81**, page size **4096**, last page **1875** bytes
- Data pages file offset: `0x23000` (143,360)
- All 81 LE page-map entries point to sequential physical pages 1..81, flags=0
- Three objects: object 1 base `0x10000`, virtual size `0x48CCF`, 73 mapped pages, flags `0x2045`; object 2 base `0x60000`, virtual size `0x13`, one page, flags `0x45`; object 3 base `0x70000`, virtual size `0x38550`, seven pages, flags `0x2043`
- LE entry: **object 1 + `0x337E8`**. Corresponding file offset: **`0x567E8`** (354,280). First byte is `EB`, short jump `0x76`, followed by a **`WATCOM C/C++32`** runtime identification string. Branch target (assuming normal 32-bit short-jump semantics) is object 1 + `0x33860`.
- Fixup page table starts at file offset `0x3469`, fixup records at `0x35B1` (offsets reported from validated LE header; fixups not parsed or applied).

Object 3's declared virtual size is substantially larger than its initialized file pages. This is normal for BSS/uninitialized memory; never equate object virtual size with file length.

## Validated work

`Tools/inspect_roth_le.py` validates the MZ + LE structure, 24-bit LE physical page map, object boundaries, initial EIP location, mapped last-page constraints, and optional **private** object extraction. Its 10 synthetic tests pass on Python 3.

**No DOS command handler has been identified**: the numerous occurrences of `cmp ..., 9` in a linear x86 disassembly do **not** prove a relation to RAW command base 9. The actual interpreter call chain, movement speed, tick duration, auto-return, and flags remain unverified.

## Usage

```powershell
python Tools\inspect_roth_le.py "C:\Program Files (x86)\GOG Galaxy\Games\Realms of the Haunting\ROTH\ROTH.EXE" --json roth_le_report.json
python Tools\test_roth_le.py
```

To extract object bytes **only to your private local disk** (do not commit, share, or attach these extracted files):

```powershell
python Tools\inspect_roth_le.py "C:\Program Files (x86)\GOG Galaxy\Games\Realms of the Haunting\ROTH\ROTH.EXE" --extract-dir .\private-roth-objects
```

The extracted objects are **not relocated**. Use an LE-aware loader in Ghidra instead of importing the unrelocated bytes as flat x86: its object maps and fixups are important to find correct pointers and cross-references.

## Ghidra workflow to locate RAW command 9

1. Download and install an extension version matching Ghidra from `yetmorecode/ghidra-lx-loader`, which explicitly supports DOS/4GW LE files and relocation fixups.
2. Import the user's original `ROTH.EXE` with the LX/LE loader, select 32-bit x86, enable full auto-analysis and fixup processing.
3. Confirm the three LE objects and the object-1 EIP at `0x337E8` (the displayed memory address may differ with loader base settings).
4. Find the interpreter by tracing references from loading/parsing `*.RAW` command structures, **not** by guessing that every compare-to-nine instruction is command 9.
5. Locate the RAW command-base dispatch; trace the branch for opcode `9` to its callers, record operands and data writes. Confirm by observing live DOS execution (or matching snapshots) before altering Unity's experimental numeric constants.
6. Make notes using `ROTH.EXE` SHA, addresses as **object number + object-relative offset**, and stack/register conventions. Raw file offsets and Ghidra virtual addresses are different coordinate systems.

Resources:
- https://github.com/yetmorecode/ghidra-lx-loader
- https://moddingwiki.shikadi.net/wiki/Linear_Executable_(LX/LE)_Format
- https://github.com/slidick/roth-editor (command-format catalog: valuable as a reference, not definitive DOS runtime proof)

## Limitations

No Ghidra or DOSBox debugger is installed in the execution environment; compilation/Play Mode in Unity has not been performed. Binary object extraction is optional and never shipped. This milestone is structurally verified against the uploaded executable and self-tests only.