# ROTH Unity

**Current development package: 0.10.2.2**

An open-source Unity reimplementation of **Realms of the Haunting** that reads a legally obtained copy of the original game data. Original game assets are not distributed with this project.

## Milestone 0.10.2.2

Milestone 0.10.2.2 extends the narrative/runtime interpreter: DBASE100 choices are now selectable and execute their original ordered branch, consecutive DBASE400/500 dialogue is queued instead of overwriting itself, consecutive GDV cutscenes are queued with their own subtitles, 25 structurally safe random blocks are executable, and the retail DBASE300 DisplayTexture (IMG1) path is decoded and displayed. Conditional hidden choices preserve their original branch ordinal. Complex random blocks remain observed rather than guessed.

Validation against the English retail data: 29/29 choice actions have balanced option/branch counts; 31 random blocks were found (25 flat/safe, 6 complex/observed); the retail DisplayTexture decodes to 320x200 / 64,000 indexed pixels with an embedded 768-byte RGB palette.

## Milestone 0.9

ROTH Unity now includes a native experimental GDV cutscene pipeline and the first narrative database playback. The engine can parse the retail 8-bit Gremlin Digital Video streams, decode the main type-8 delta codec plus the type-5 LZ variant used by `HAWK03A.GDV`, update the original VGA palette, decode PCM/DPCM audio, and present video fullscreen without adding a Unity UI package dependency.

DBASE100 cutscene records are now parsed alongside global actions. DBASE400 text/subtitle records and DBASE500 DPCM voices are read directly from the original installation. Global opcode 5 can display the original line and play its voice; opcode 7 can resolve a DBASE100 cutscene name and launch its GDV; opcode 8 exposes its original choice string text while full branching choice execution remains under development. Cutscene subtitles are supported with an experimental 8-tick-per-second clock inferred from the retail timing data.

The 0.8 gameplay work remains: whole-game RAW loading, automatic `DEMO*.DAS` resolution, runtime texture mutation, inventory/flags, map warps, SFX and safe DBASE100 callbacks.

## Fastest test

1. Open the project with **Unity 6000.6.0f1**.
2. Choose **ROTH Unity > Quick Start GOG**.
3. The first path checked is `C:\Program Files (x86)\GOG Galaxy\Games\Realms of the Haunting`.
4. Select a discovered retail map and click **Create playable map test**.
5. Enter Play Mode. Use WASD + mouse, Space to jump, left/right click to test original interactions, Esc to release the mouse.

`Mirror World X` should remain OFF for the corrected original orientation.

## Useful tools

- **ROTH Unity > RAW Map Inspector**
- **ROTH Unity > DAS Texture Inspector**
- **ROTH Unity > RAW Command Inspector**
- **ROTH Unity > DBASE100 Inspector**
- **ROTH Unity > Quick Start GOG**
- **ROTH Unity > GDV Inspector**

The development HUD in Play Mode shows current map, Sector ID, inventory count, the latest trigger and the latest command reached.

## Validation status

The English retail data used as ground truth contains 44 `.RAW` maps. The extended structural validator currently passes **44/44 maps**. A separate command audit passes all 44 maps with **5,568 commands**, **1,950 entry references**, zero unresolved entry references and zero invalid next-command indices.

The map-resource resolver obtains 100% primary-DAS coverage for most retail maps. A few maps contain one special reference not present in the selected FAT and continue with a diagnostic fallback. See `Documentation/RETAIL_MAP_RESOURCE_AUDIT.md`.

Across all retail maps, 127 command-59 warps were found. Every named target resolves to an installed `.RAW` except a single `MAS5` reference from `MAUSO1EA`; no alias is invented without evidence.

## Current limits

This remains a development engine, not yet a complete replacement executable. Open Door and other geometry-mutating commands are traced but not visually executed until their behavior is verified. Player Rotation remains observation-only while its packed angle representation is being confirmed. DBASE100 global actions now have a partial safe interpreter plus dialogue text/voice and GDV playback. Choice strings are visible but branching choice execution is not complete. Combat, final inventory UI, full choice/random blocks, complete puzzle execution and save compatibility remain future work.

See `Documentation/MILESTONE_0_9.md` for details.

## Legal

This repository contains no original *Realms of the Haunting* copyrighted assets. A legally obtained installation is required.


## 0.10.3 prototype
RAW opcode 7 animates floor/ceiling heights via RothSectorMover; see Documentation/MILESTONE_0_10_3.md. Timing and player carry remain experimental.

## 0.10.4 (experimental)
RAW sector movers now optionally return to their initial height after the configured timeout. Requires retail verification; see Documentation/MILESTONE_0_10_4.md.


## 0.10.5
Prototype automatic door obstruction prevention using RAW sector polygon footprints. See Documentation/MILESTONE_0_10_5.md.


## 0.10.6
Sector-mover countdown begins after reaching the open position; re-triggers preserve current position. See Documentation/MILESTONE_0_10_6.md.


## 0.10.7
Experimental CharacterController transport on moving floors and capsule clearance guard for floor/ceiling motion. See Documentation/MILESTONE_0_10_7.md.


## 0.10.8 — RAW moving sectors
Experimental RAW opcode 9 horizontal movement (X/Z), isolated vertices, sector objects and grounded rider carry, separate floor/ceiling/platform texture-follow flags, auto-return and auto-repeat. Retail audit of 32 unique opcode 9 commands included as `Tools/validate_raw9.py`. See Documentation/MILESTONE_0_10_8.md for limitations. Unity build not verified.


## 0.10.9 — RAW9 safety
Added overflow-atomic sector translations, position-preserving motion retriggers, rider rollback and regression tests. This is **not** a proven retail-faithful implementation. See `Documentation/MILESTONE_0_10_9.md`.


## 0.10.10 — RAW9 carry and trace
Fixes small-step rider obstruction detection and deterministic position rollback. Adds opt-in structured RAW9 trace logs for later DOS comparisons. Experimental and not yet Unity-playtested. See `Documentation/MILESTONE_0_10_10.md`.


## 0.10.11 — RAW9 trace audit
Read-only Unity log parser with per-sector JSON summaries, anomaly diagnostics, and eight synthetic Python tests. Does not establish DOS fidelity. See `Documentation/MILESTONE_0_10_11.md`.


## 0.10.13 — Verified GOG executable LE layout
Analyzed an original user-owned ROTH.EXE (SHA-256 `e2d54427cd0692798e2df457b1ea8d3bca8cf1383ec2d4ea782165fe0ba56a05`): confirmed 80386 MZ+LE image, 81 page mappings, three objects and Watcom startup entry. Added `Tools/inspect_roth_le.py` and 10 synthetic tests. See `Documentation/MILESTONE_0_10_13.md`. **The command-9 DOS handler is not yet located; no retail binary is committed.**


## 0.10.14 — Original RAW opcode 9 located in ROTH.EXE
Validated 14,968 LE fixup records from the user's own GOG executable and traced the 128-entry RAW command dispatcher to opcode 9 (object 1 + `0x22A99`). Identified initial movement update candidates. **CORRECTED in 0.10.15:** the 6-bit fractional speed path belongs to opcode 7, not opcode 9. This does **not** yet prove DOS timing or the Unity translation algorithm, which remain experimental. See `Documentation/MILESTONE_0_10_14.md` and `Documentation/ROTH_OPCODE9_ADDRESSES.json`. No proprietary game bytes are committed.


## 0.10.15 — Verified horizontal movement callback, correcting 0.10.14
Original LE animation runner at object1+0x247CC uses the same relocated function table with index offset +67. This proves **opcode 9 update = 0x22BD9**, **opcode 7 update = 0x22D51** and assigns the 6-bit fractional path to opcode 7, correcting earlier claims. The frame delta derives from a 16-bit counter, but real-time tick frequency is not established. Audit and 12 tests updated; Unity movement remains experimental and unchanged. See `Documentation/MILESTONE_0_10_15.md`.
