# English reference data

This document records facts observed directly from the English *Realms of the Haunting* data set used as the initial ROTH Unity reference.

No original game files are stored in this repository.

## Archive inventory

The supplied reference archive contains 395 entries. Relevant extension counts include:

- 165 `.GDV` cinematics
- 90 `.RAW` files
- 12 `.DAS` files
- 10 `.DAT` files
- 3 `.SFX` files

The original `FILELIST.TXT` explicitly references the five core databases, `ROTH.EXE`, `ROTH.RES` and the GDV movie set.

## Core reference files

| File | Size (bytes) | ZIP CRC32 |
| --- | ---: | --- |
| DBASE100.DAT | 42,380 | b6334913 |
| DBASE200.DAT | 1,775,000 | fcd577ff |
| DBASE300.DAT | 22,446,670 | 317366a1 |
| DBASE400.DAT | 176,758 | de9df870 |
| DBASE500.DAT | 127,913,056 | 33e54405 |
| ROTH.EXE | 472,915 | 447209a4 |
| ROTH.RES | 877 | a3256af9 |

These values are identification data only. They should not be treated as a permanent requirement until additional releases and language variants have been compared.

## Initial implementation strategy

The first runtime layer must not assume that every required file lives in one directory. The reference package contains installation data and installed-game data in separate paths, so discovery is intentionally filename-driven with preferred locations followed by a recursive fallback.

Next reverse-engineering targets:

1. Map the relationships between `.RAW` maps and `.DAS` resources.
2. Document the five DBASE files independently.
3. Add a GDV header/parser test fixture using metadata only.
4. Identify the minimum data needed to reconstruct one room, with `STUDY1.RAW` as the first candidate.
