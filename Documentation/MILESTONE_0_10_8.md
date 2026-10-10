# ROTH Unity 0.10.8 — RAW opcode 9

## Implemented in this experimental milestone
- RAW opcode 9 dispatch and six-argument decoding.
- X/Z horizontal sector translation and dynamic mesh/collider rebuilding.
- Detaching the moving sector vertices to avoid directly shifting neighbouring sectors.
- Moving objects associated with the sector.
- Four independent texture-follow bits: floor, ceiling, intermediate-platform floor and ceiling.
- Auto-return, experimental auto-repeat and readout of speed from the high flag byte.
- Player carry when grounded on the sector, with a provisional collision clipping check.
- Active movement cancellation on map change.
- Fixed pre-existing RothSectorMover C# variable declaration error.

## Retail audit
Using the supplied retail game RAW maps, the read-only validator found **32 unique opcode 9 commands**, all with **six arguments** and sixth argument zero. Fifth argument values were 0, 10, 35, 70 and 80. 14 commands request movement on X; 29 set the auto-repeat bit. The supplied archive includes duplicate map entries, which were deduplicated by map basename and command identity.

The engine currently interprets args 3/4 as signed start/end values, using **end minus start** as sector travel. This is more conservative than treating end as an absolute mesh displacement; the interpretation is still unverified against actual game execution.

## Still outstanding
- Exact retail movement speed and timebase, reversal behavior and sector reference positions.
- Moving portal/wall seams and collisions with general world geometry.
- Full physics-based obstruction handling and carry near sector boundaries.
- Unity Editor compile and live play tests. Static source/ZIP checks alone do not establish a working build.

Tool: `python Tools/validate_raw9.py <owned-retail-zip>`

No original copyrighted assets are included.