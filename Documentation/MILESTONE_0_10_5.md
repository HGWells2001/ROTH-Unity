# ROTH Unity 0.10.5: player-safe prototype doors

## Implemented
- Sector-footprint obstruction detection from the original RAW face vertices (point-in-polygon plus capsule horizontal radius-to-edge test).
- Optional `CharacterController` obstruction checks on sector auto-close.
- If the player occupies the sector, delay automatic return and retry.
- If obstruction starts during a return, reopen the moving sector.
- Preserve the existing movement speed and delay controls.

## Limitations
- Uses **horizontal footprint** only, so this is deliberately conservative and can defer some safe closures.
- Does not reproduce retail engine obstruction physics and does not carry the player on moving platforms.
- Unity project not compiled in this environment. Timing remains experimental.
- This project contains no copyrighted original ROTH game assets.
