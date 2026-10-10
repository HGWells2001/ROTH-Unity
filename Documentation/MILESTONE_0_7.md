# Milestone 0.7

Focus: whole-game navigation, command-graph correctness, robust interaction and safer runtime state.

## Command engine

- Fixed a major RAW command bug: entry references are now kept as the original **1-based command indices**. Earlier builds stored them zero-based while runtime consumers expected 1-based values.
- Honors command modifier **Start Disabled** at runtime.
- `On Enter Sector` respects documented Not East / Not North / Not West / Not South flags using the player's actual movement direction.
- Added recursion protection for nested command chains.
- Keeps a development trace of last trigger and last command.
- Safe state-only command support now includes flags, command enable/disable, nested map commands and logical inventory operations.
- Logical inventory tracks current items, ever-had items and the Take Inventory / Give Back stash. This allows command 39 conditions to behave meaningfully before the final inventory UI exists.
- `Player Rotation` remains observation-only until the packed Flags/Rotation representation has a verified angle conversion.
- `Open Door` and other geometry-mutating commands remain observation-only rather than inventing unverified movement.

## Whole-game loader

- Added `RothMapResourceResolver`, selecting `DEMO.DAS` through `DEMO4.DAS` by actual RAW resource/FAT coverage rather than a hand-written chapter table.
- Resolver scoring includes sector textures, intermediate-platform textures, face textures and source-0/source-1 object references.
- Quick Start discovers retail RAW maps under `DATA/M`, `ROTH/M` and `M` and can create a playable test for any discovered map.
- Added `RothWorldController` and runtime implementation of command 59 Map Transition / Warp.
  - Named targets load the target RAW and resolve its primary DAS automatically.
  - Empty map name plus Sector ID performs an intra-map warp to the target sector geometry.
  - Intra-map warps preserve player facing rather than resetting orientation.
  - Missing named maps are logged and leave the current map loaded.
- Across the English retail set, 127 command-59 warps were found. One target, `MAS5` from `MAUSO1EA`, has no corresponding RAW in the installation and is deliberately not aliased without evidence.

## Rendering and interaction

- Fallback object markers remain interactive and retain original Object ID/texture metadata, so unsupported visuals do not silently break puzzle click targets.
- Sky-index surfaces use transparent rendering rather than becoming opaque textured caps.
- Added development HUD for current map, Sector ID, inventory count, last trigger and last command.

## Validation

- Extended RAW structural validator passes all **44/44 English retail maps**.
- Batch command audit passes all 44 maps: **5,568 commands**, **1,950 entry references**, zero unresolved entry references and zero invalid next-command indices.
- Primary-DAS coverage is 100% for most maps; exceptional one-reference gaps are documented in `RETAIL_MAP_RESOURCE_AUDIT.md`.
- `Tools/validate_map_resources.py` mirrors the C# resolver inputs, including intermediate platforms and primary-DAS object references.

## Package dependencies

Unity built-in modules explicitly required by this project:

- `com.unity.modules.physics` 1.0.0
- `com.unity.modules.audio` 1.0.0

Do not add `com.unity.modules.inputlegacy`; Unity 6 does not expose it as a resolvable package.

No original game assets are distributed.
