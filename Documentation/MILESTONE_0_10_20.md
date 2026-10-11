# ROTH Unity 0.10.20: opt-in four-vertex RAW9 geometry backend

**Scope:** An experimental, guarded geometry mode, not a complete DOS-equivalent animation system. Default gameplay remains on the original prototype implementation.

## Added functionality

- `Assets/ROTHUnity/Runtime/RothMapMeshBuilder.Raw9Selected.cs`: a new partial-class backend which retains the four selected original RAW vertex indices and initial coordinates. It changes only the original RAW9-selected X or Y coordinate words (rather than detaching every face vertex). Mesh geometry, collider and objects are rebuilt by `RefreshRuntimeGeometry(rebuildObjects: true)`.
- `RothMapMeshBuilder.cs`: changed into a partial class, exposes the active selected-mode sector translation in `RuntimeGetSectorTranslation`, updates texture-follow logic and clears experimental state on map reload. A monotonically incremented map-generation counter guards against stale motions.
- `RothHorizontalSectorMover.cs`: the new inspector checkbox **UseSelectedRaw9Vertices** (OFF by default). A movement retains its chosen mode even if the toggle changes. Player transport and failed-collision rollback dispatch through the selected geometry backend or the original backend consistently.
- `Tools/test_roth_raw9_selected_simulation.py`: 16 purely synthetic geometry model tests. They verify only four X/Y coordinate words are changed, other vertices are retained, opposite movements undo correctly, and preflight rejects shared vertices, cross-sector sister-portals, coincident external vertices, nonquadrangles, open edge rings, axis mismatch and out-of-range signed 16-bit coordinates.

## Conservative safety gates

The experimental backend refuses to start unless:

1. The RAW map and requested sector exist, and `RothRaw9VertexInspector` resolves exactly four unique original first-face vertices.
2. `RothRaw9TopologyInspector` produces **zero static hazard findings**, including other-sector vertex aliases and risky cross-sector sister faces.
3. Exactly four sector faces form a closed ordered ring: each face's second vertex is the next face's first vertex.
4. No old-style sector translation has been prepared on the map, and no other experimental motion owns the selected vertices.
5. The requested axis is unchanged, the other axis offset is zero, all proposed vertex coordinates remain within signed 16-bit bounds, and sector object positions will not overflow.
6. Cached vertex coordinates have not been modified by a separate system since the last successful selected-mode update.

The implementation updates vertices and sector objects after validating all numeric bounds, then rebuilds Unity mesh/collision geometry. If mesh refreshing throws, it attempts to restore the old in-memory coordinates and objects, then rebuild again. That recovery is a best-effort rollback, **not** proof of Unity scene integrity after an exceptional renderer failure.

## How to test in Unity 6000.6.0f1

1. Open the native Unity project from the latest GitHub source. Keep an original copy of the map and project.
2. Select the GameObject containing `RothHorizontalSectorMover` (the command interpreter may add it automatically after triggering command 9).
3. Turn ON **Use Selected Raw9 Vertices** in the component inspector **before** triggering the RAW command. Leave this toggle off to use the existing legacy motion.
4. Use `ROTH > Audit original RAW9 vertex references...` on your original map first. Begin with a four-face sector having zero topology warnings and a closed vertex ring.
5. Test motion along both axes, render continuity, MeshCollider behavior, player carry, obstruction rollback, map transition and retriggering. On refusal, inspect the Unity Console for the diagnostic reason.
6. After toggling back to the legacy backend for the **same active sector**, rebuild the map to reset its previous backend assignment.

## Reproducible non-Unity tests

```powershell
python -m unittest discover -s Tools -p "test_roth_*.py" -v
python Tools\audit_roth_raw9_topology.py "C:\path\to\your\MAP.RAW" --json topology.json
```

In the local reconstructed source-plus-patches checkout, **118 Python tests pass**, including the 16 new pure-model checks. Python tests do not compile or run the changed C# source.

## Accuracy and unverified areas

**Original DOS observations:** the RAW opcode 9 constructor selects four first-face vertices (sorted by signed16 X or Y), and the periodic update mutates those four coordinate fields. The IRQ0 nominal counter frequency is approximately 70.53 Hz. The DOS updater applies a signed coordinate delta constrained by original command bounds.

**Still experimental in Unity:** `RothHorizontalSectorMover` continues to compute destination from `end-start` and interpolate using `Time.deltaTime`. Object translation in the new backend follows existing prototype behavior and has **not** been proven original. Topology preflight may reject valid retail cases deliberately. No original retail RAW map was supplied for a concrete collision scenario, and no Unity executable, compiler or Play Mode verification was available in this environment.

**Next milestone:** use the user's retail RAW maps to measure rejection rates and select a safe map/sector, then run Unity Play Mode A/B comparisons and implement an independently toggleable tick-driven DOS state machine only after verified timing and collision behavior.

No executable, RAW map, DAS archive or other proprietary game assets are committed.
