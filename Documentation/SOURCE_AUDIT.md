# English source archive audit

The supplied source is an 8-part binary split ZIP:

- parts 001-007: 262,144,000 bytes each
- part 008: 3,217,484 bytes
- logical total: 1,838,225,484 bytes
- ZIP entries: 395
- ZIP integrity test: passed (no corrupt entry)

Important English data observed:

- `DATA/DBASE300.DAT`: 22,446,670 bytes
- `DATA/DBASE500.DAT`: 127,913,056 bytes
- `DATA/M/STUDY1.RAW`: 84,924 bytes
- `DATA/M/STUDY2.RAW`: 94,306 bytes
- GDV cinematics are present under `DATA/GDV/`
- map RAW and DAS files are present under `DATA/M/`

The archive also contains an installed `ROTH/` tree. Development should resolve game data from a user-selected installation directory rather than embedding these files in the repository.

## 0.6 additions
- English retail/GOG data supplied by the project owner is used as ground truth for RAW, DAS, SFX and DBASE100 validation.
- Public `slidick/roth-editor` reverse-engineering code was consulted for binary field semantics, command names and format layout. ROTH Unity code is an independent C# implementation.
- User-provided original assets are not included in source or installer packages.
