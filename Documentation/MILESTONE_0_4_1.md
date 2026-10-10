# Milestone 0.4.1 - UV fidelity hotfix

This hotfix addresses the visibly incorrect texture mapping in milestone 0.4.

## Fixes

- RAW texture mappings with type bit 7 now parse and retain the four-byte additional metadata record (`shiftTextureX`, `shiftTextureY`, `faceID`).
- Wall UV generation now uses the original ROTH face vertex ordering and the original axis convention: wall height maps to U and face length maps to V.
- `IMAGE_FIT`, `HALF_PIXEL`, `PIN_BOTTOM`, `FLIP_X`, and per-face X/Y shifts are applied in the same coordinate convention as the reference implementation.
- Floor/ceiling grid mapping is calculated in the same mirrored-X coordinate space used to build Unity geometry.
- Transparent portal faces retain their middle texture across the open portal span.
- Original texture materials prefer unlit shaders and point filtering to avoid Unity lighting changing the appearance during bring-up.

## Ground-truth audit

On the supplied English retail data:

- `STUDY1.RAW`: 910 texture mappings, 383 extended mappings, 227 with non-zero X/Y shifts.
- `STUDY2.RAW`: 851 texture mappings, 412 extended mappings, 291 with non-zero X/Y shifts.

The prior milestone skipped the extension bytes for structural parsing but did not preserve or apply the shift values, so a large subset of wall mappings could not be faithful.

This milestone still does not claim pixel-perfect parity. Animated/packed DAS resources and some special transparent/override cases remain future work.
