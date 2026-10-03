# Binary format observations

These notes contain only observations verified against the supplied English reference data.

## DBASE files

The five primary databases have explicit 8-byte ASCII signatures:

- `DBASE100`
- `DBASE200`
- `DBASE300`
- `DBASE400`
- `DBASE500`

This makes safe file-type identification possible before any internal structure is parsed.

### Early observations

- `DBASE400.DAT` exposes readable English UI/subtitle strings immediately after its header.
- `DBASE500.DAT` begins with `DBASE500` and is immediately followed by RIFF/WAVE-style data in the sampled reference file.
- No assumptions about record tables or offsets are committed yet.

## DAS

The sampled `ADEMO.DAS` begins with ASCII `DASP`.

ROTH Unity currently treats this only as a format signature. The meaning of the following fields is still under investigation.

## GDV

Sampled English cinematics including `INTRO.GDV` and `GREMLOGO.GDV` begin with:

```text
94 19 11 29
```

ROTH Unity uses those four bytes for provisional GDV identification. Header field semantics are not yet encoded in the runtime.

## RAW maps

`STUDY1.RAW` does not expose an obvious ASCII magic value in its initial bytes. It should therefore be reverse-engineered structurally rather than identified from a guessed signature.

The next concrete milestone is to correlate `STUDY1.RAW` with its associated DAS/database resources and identify enough geometry records to render a first room.
