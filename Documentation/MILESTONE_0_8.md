# Milestone 0.8

Focus: runtime world mutation and the first executable subset of DBASE100 global logic.

## Runtime map mutations

- Command 10 `Change Floor Texture` now updates the in-memory sector texture, X/Y shift and documented scale bits, then rebuilds generated geometry without rereading the original RAW.
- Command 12 `Change Face Texture Advanced` updates mid/upper/lower textures, eight documented face texture flags and extended X/Y shifts.
- Command 13 `Change Object Texture` updates matching Object IDs and rebuilds generated object visuals.
- Command 52 `Change Face Texture Simple` updates the matching Face ID at runtime.
- All changes are runtime-only. Original game files are never modified.

## DBASE100 safe interpreter

DBASE100 action references are now resolved as the original 1-based game indices rather than array offsets. The safe interpreter executes only global opcodes whose semantics are sufficiently documented:

- 1 / 129: continue if flag set / not set
- 13 / 141: conditionally execute only the next global command
- 4 / 132: set / unset flag
- 2 / 130 / 131: item-count comparisons
- 17 / 145: give / remove item
- 25: play original SFX by FXSCRIPT index
- 29: jump to another DBASE100 action
- 35: callback to a RAW map command index
- 53: callback to another DBASE100 action
- 156: exit global command flow

Logical inventory now stores quantities rather than presence only, while retaining the existing `ever had`, held-item and Take Inventory / Give Back state.

A retail DBASE100 audit contains 3,097 global opcode instances. The safe subset covers the state/control-flow portion while dialog, video, choices, image display, music, health/combat and item-definition opcodes remain observation-only.

## Audio

- Added one-shot playback by FXSCRIPT index for DBASE100 opcode 25.
- Existing positional RAW SFX nodes remain supported.

## Doors and moving sectors

`Open Door` (map opcode 47), floor/ceiling motion and moving sectors remain intentionally observation-only. Public reverse-engineering sources identify their arguments but do not currently document a sufficiently reliable physical transform/speed model. ROTH Unity does not substitute a generic Unity door animation.

## Safety and packaging

- Built-in Unity dependencies remain only `com.unity.modules.audio` and `com.unity.modules.physics`.
- No original RAW/DAS/SFX/DBASE/GDV assets are distributed.
- Target editor remains Unity 6000.6.0f1.
