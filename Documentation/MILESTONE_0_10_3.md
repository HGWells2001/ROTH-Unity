# ROTH Unity 0.10.3: sector height prototype

- Adds `RothSectorMover` and RAW map opcode 7 (floor/ceiling height).
- Animates from original signed start/end RAW heights and rebuilds geometry plus mesh collider.
- Cancels active movements when a new map loads.
- `RawUnitsPerSecond` and `MeshRefreshInterval` are configurable.
- **Not yet retail-verified:** movement speed, collision/player carry, auto-revert timeout and synchronization with downstream commands.
- No original game assets are included. Unity compilation has not been run in this environment.
