# ROTH Unity 0.10.5: player-safe sector closing

Milestone source archive SHA-256: `6646e8193f019d5259af4cf5db04a8f56853ad0db8e21259cbfad6cc1ed90149`.

## Source changes in the 0.10.5 archive

- `Assets/ROTHUnity/Runtime/RothMapMeshBuilder.cs`: Added `RuntimePlayerOccupiesSector(ushort sectorId, CharacterController player, float margin = 0.12f)`. Uses the original RAW polygon vertices, point-in-polygon ray casting and squared distance from the player capsule center to each sector edge.
- `Assets/ROTHUnity/Runtime/RothSectorMover.cs`: Added `Player`, `ObstructionRetrySeconds`; auto-discovery of the first-person player's `CharacterController`; postpones auto-close while occupied; reopens a closing sector when the player enters the footprint.
- `Documentation/MILESTONE_0_10_5.md`: notes the limits and testing status.
- `README.md`: updated milestone index.

### Important limits

This is a **horizontal-footprint approximation**, deliberately conservative. It does not implement character carry on moving platforms or reproduce the original game's obstruction physics. The Unity project has **not** been compiled or run in this execution environment. The original game's retail files are not distributed.

### Repository status

This commit publishes the milestone design/status, **not the complete source tree**. The downloadable 0.10.5 ZIP remains the current complete local implementation. The repository still contains an older `Assets/ROTHUnity/Scripts` scaffold, so a full deliberate migration to the new `Core`/`Runtime` layout is required. Do not treat the GitHub branch as a verified build of 0.10.5.
