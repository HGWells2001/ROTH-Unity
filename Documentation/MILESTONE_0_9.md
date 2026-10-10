# Milestone 0.9

Focus: native cutscenes, spoken dialogue and narrative data.

## GDV

- Added `RothGdvArchive` for the retail 8-bit GDV container.
- Supports palette update frames (type 1), unchanged frames (type 3), main delta/LZ frames (type 8) and the alternate LZ codec (type 5).
- PCM and DPCM audio are decoded into a Unity `AudioClip`.
- `RothGdvPlayer` displays one progressively decoded frame texture fullscreen, rather than allocating a Texture2D per frame.
- Player movement and interaction are suspended during playback and restored at finish/skip.
- `ROTH Unity > GDV Inspector` can decode an arbitrary frame for editor diagnostics.

Retail audit: 165 GDV entries were found, one of which (`IPLOGO.GDV`) is a zero-byte placeholder. Across the non-empty streams there are 96,948 frame records: 93,226 type-8, 2,043 type-3, 1,656 type-5 and 23 type-1 frames. Type-5 occurs only in `HAWK03A.GDV`. Independent FFmpeg cross-checks produced exact palette-index pixel matches for frame 100 of `HAWK03A` (type 5) and frame 100 of `ABADON1A` (type 8 after a scaling-state transition).

## Narrative databases

- DBASE100 now parses the 173 cutscene table records as well as global actions.
- Added DBASE400 text lookup and cutscene subtitle parsing.
- Added DBASE500 DPCM/PCM voice decoding.
- Global opcode 5 displays the referenced DBASE400 text and plays its DBASE500 voice when present.
- Global opcode 7 resolves the DBASE100 cutscene name, finds the retail GDV and launches it.
- Global opcode 8 displays the original choice string text; full branch selection remains a later task.
- Cutscene subtitles are displayed by the GDV player. The current default uses 8 subtitle ticks per second, inferred empirically from retail timestamps and kept configurable.

Retail narrative validation finds 163 subtitled cutscenes containing 2,631 subtitle entries with no invalid offsets. The 1,090 global opcode-5 references resolve to 913 unique DBASE400 records, and all 913 have valid DBASE500 voice records.

## Known limits

- Full Start Choice / End Choice branching is not executed yet.
- Start Random / End Random blocks are not executed yet.
- GDV compression types 0/2/6 are not implemented because they do not occur in the retail ROTH frame audit; type 4/7 are invalid in FFmpeg's GDV decoder.
- Subtitle tick rate is currently inferred rather than proven by original engine code.
- Door/moving-sector physics remains observation-only until the original transform rules are established.

## Legal

No original GDV, DBASE, RAW, DAS or SFX assets are included. The runtime reads a legally obtained installation.
