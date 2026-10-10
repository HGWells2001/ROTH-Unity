# ROTH Unity milestone 0.4

Milestone 0.4 extends the original-data reconstruction path beyond the 0.3 texture bring-up.

## Added

- RAW mid-platform parsing and sector-to-platform relation resolution.
- RAW object-section parsing for retail ROTH maps.
- Per-sector object lists plus global object records (position, Z, rotation, source/index, flags, lighting, render type and object ID).
- Intermediate floor/ceiling platform geometry in the Unity mesh builder.
- Debug scene markers for parsed map objects.
- Improved floor/ceiling UV reconstruction using ROTH texture scale bits, shifts and flip flags.
- Improved wall UV reconstruction for IMAGE_FIT, HALF_PIXEL, FLIP_X and PIN_BOTTOM.
- RAW inspector counters for platforms and objects.
- `Tools/validate_raw_extended.py` for repeatable platform/object structural validation.

## Ground-truth validation

English retail data supplied for this project:

- STUDY1.RAW: 507 sectors, 16 mid-platforms, 16 resolved platform references, 274 objects in 108 containers, 0 bad containers.
- STUDY2.RAW: 629 sectors, 30 mid-platforms, 30 resolved platform references, 294 objects in 162 containers, 0 bad containers.

## Current limitations

- Object markers are diagnostic cubes. Original object sprites/animations are not rendered yet.
- Animated/compressed DAS images are still deferred.
- Wall UV reconstruction is substantially closer to the original rules but several transparency/override edge cases remain.
- Intermediate-platform edge walls and special platform transparency are not complete.
