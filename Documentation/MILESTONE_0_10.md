# Milestone 0.10

Focus: DBASE100 narrative flow and DBASE300 display images.

- Selectable retail dialogue choices. Option ordinals remain tied to the corresponding Start Choice/End Choice branch even when conditions hide choices.
- Narrative text/voice queue for consecutive opcode 5 records.
- GDV playback queue for consecutive opcode 7 records, retaining per-cutscene subtitles.
- Flat safe random blocks (opcodes 11/12) execute one random atomic command; complex random blocks remain diagnostic-only.
- DBASE300 IMG1 DisplayTexture decoder and runtime presentation for opcode 14.
- Quick Start locates DBASE300.DAT automatically.
- No new Unity package dependencies.

Retail validation:
- 697 DBASE100 actions.
- 29 choice actions; 0 option/branch mismatches.
- 31 random blocks: 25 flat safe, 6 complex observed.
- Retail DisplayTexture record: 320x200, 64,000 decoded pixels, 768-byte embedded 8-bit RGB palette.

Known gaps:
- Complex random groups are not executed until grouping semantics are proven.
- HMP music is parsed by reference tooling but Unity has no built-in MIDI/HMP synthesizer, so opcode 26 remains observed.
- Door/sector physical movement still remains unimplemented until the original transform semantics are sufficiently verified.
