# ROTH Unity 0.10.10 | RAW 9 rider rollback and instrumented tracing

**Status:** experimental implementation. This source milestone does **not** prove equivalence to the DOS engine. No Unity build or runtime playtest has been performed in this environment.

## Source changes

- `RothHorizontalSectorMover.cs`: replace the squared-distance test for rider clipping with an axial **linear-distance** tolerance (max 1 mm, reduced for short moves). The old additive epsilon could falsely accept a wholly blocked move below roughly 3 cm.
- On a blocked carry, restore sector geometry first and the character's exact pre-step position while its `CharacterController` is briefly disabled, then sync physics transforms. This avoids trying to reverse the rider with another collision-limited `CharacterController.Move`.
- Add opt-in Inspector property `TraceRaw9` to emit diagnostic `RAW9_TRACE|...` lines for trigger, steps, returns, obstruction, rollback and completion. Off by default.
- Preserve the earlier 0.10.9 safety checks and explicitly *avoid guessing* DOS speed, delays or movement direction beyond the existing prototype.

## Trace columns

`RAW9_TRACE|frame|unity_time_sec|sector_id|axis|event|raw_before_x|raw_before_y|raw_after_x|raw_after_y|cmd_start|cmd_end|interp_position|destination|speed_raw_units_per_second|player_world_x|player_world_y|player_world_z|cmd_flags|cmd_revert_ticks`

Use Unity Play Mode, enable `TraceRaw9` on `RothHorizontalSectorMover`, trigger a sector, and copy or export log messages matching `RAW9_TRACE|`. The object's component may be dynamically added by `RothCommandMonitor` on first trigger. Compare sector translations and durations with recordings/debugger observations of the original DOS retail executable. Do not infer DOS equivalence from these Unity logs alone.

## Reproducible static checks

`python Tools/test_raw9_regression.py`

Eight Python tests cover bounds, retrigger arithmetic, short-step clipping and source contracts. They are *static/numeric*, not C# compiler tests, physics tests, or retail observations.

## Remaining validation

- Trace real DOS opcode 9 execution to determine start/end direction, speed unit, tick rate, repeat/autorevert, and flags independently.
- Build in Unity **6000.6.0f1** on Windows, inspect compile logs and run physically blocked rider tests.
- Inspect adjoining portal seams and other moving objects in Play Mode; a rolled-back geometry rebuild can still have nontrivial side effects.
- Package a Windows executable and autoinstaller only after a successful Unity build. The GitHub source snapshot is not an installed game release.

No retail game data or copyright-protected assets are included.
