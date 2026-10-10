# ROTH RAW format notes - initial pass

Ground truth used for this implementation: English retail/GOG game data supplied for development.

Observed on STUDY1.RAW:
- file size: 84,924 bytes
- version: 0x0070
- signature: 0x5257
- sector count: 507
- face count: 2,454
- vertex count: 1,076
- vertices offset: 53,552 (0xD130)
- vertex section size: 12,920 bytes
- command section size: 9,928 bytes

Observed on STUDY2.RAW:
- file size: 94,306 bytes
- version: 0x0070
- signature: 0x5257
- sector count: 629
- face count: 3,174
- vertex count: 1,311
- vertices offset: 65,084 (0xFE3C)
- vertex section size: 15,740 bytes
- command section size: 4,050 bytes

Implementation reference cross-check: slidick/roth-editor `src/resources/parsers/raw.gd`, which credits earlier ROTH reverse-engineering projects. The Unity code is a clean C# implementation using the documented binary layout and direct validation against the supplied English data.

## Milestone 0.2 geometry reconstruction

The Unity prototype now treats each RAW sector as a 2D polygon described by its contiguous face records.
Floor and ceiling caps are triangulated from the ordered face vertices. Wall generation distinguishes solid
faces from sister-face portals. For portals, only lower and/or upper wall bands are emitted when the adjacent
sector has a different floor or ceiling height. This allows connected sectors to remain physically open.

This is still a structural renderer: original DAS/DBASE texture resolution, intermediate platforms, special
face flags and game command behaviour are not applied yet.
