# ROTH Unity 0.10.19: topology preflight for original RAW9 moving geometry

**Status:** read-only diagnostic implementation; no new Unity gameplay movement. Python tests: 94/94 offline. No Unity Editor compilation or Play Mode validation was performed.

## Why this matters

The user-owned GOG ROTH.EXE has already confirmed that opcode 9 targets four selected vertex coordinate words, not every face vertex in the sector. The existing experimental Unity `RothMapMeshBuilder.RuntimeSetSectorTranslation` instead detaches and moves all vertices in a moving sector. Before changing that behavior, it is necessary to identify vertices and faces that connect to other sectors.

## Added

- `Assets/ROTHUnity/Core/RothRaw9TopologyInspector.cs`: read-only inspection of selected vertex indices referenced by faces outside the moving sector, sister-face portal links touching a selected endpoint, distinct external vertex indices at the same XY coordinates, invalid sister references, and more-than-four-face sectors.
- `Assets/ROTHUnity/Editor/RothRaw9VertexAuditMenu.cs`: existing Editor audit now logs **TOPOLOGY REVIEW** findings and counts flagged commands.
- `Tools/audit_roth_raw9_topology.py`: independent Python reader for owned `.RAW` maps, folders or ZIPs. Emits JSON with sector/command, hazards, external faces and portal pairing; exit code 2 when a map is rejected or review items exist. Never writes to the original map.
- `Tools/test_roth_raw9_topology.py`: 10 synthetic cases covering portals, duplicate coordinates, shared first/second face vertices, invalid sister pointers, non-quads, combined hazards, truncation and nonmutation.
- `Tools/test_roth_raw9_topology_contract.py`: 4 source-level C# contracts (not compiler or physics tests).
- `Documentation/RAW9_TOPOLOGY_SYNTHETIC_EXAMPLES_0_10_19.json`: concrete results for five synthetic maps; no commercial data.

## Run locally

```powershell
python Tools\audit_roth_raw9_topology.py "C:\path\to\your\extracted\map.RAW" --json topology.json
python -m unittest discover -s Tools -p "test_roth_*.py" -v
```

To run from the Unity Editor, choose **ROTH > Audit original RAW9 vertex references...**, select a legally owned RAW map, and review the Console. A result with zero warnings **is not a guarantee** of safe collisions, portal updates, render correctness, or DOS fidelity.

## Evidence versus unverified behavior

The previously verified DOS opcode 9 vertex selection and update remain the ground truth for the four original coordinate references. The NEW topology categories are conservative risk checks derived from RAW face references and vertex positions. They do not assert how the original renderer resolves portals or how neighboring movable sectors interact at runtime.

Retail `.RAW` data was not attached in this session. Therefore **zero retail map cases were assessed**, and no conclusions about particular original rooms can be drawn. The raw commercial executable and game assets are not part of this patch or any commit.

## Integration gate

After testing real retail RAW maps, design a separately toggled geometry mover that preserves and updates ownership/adjacency. Verify movement along both axes and deformation around cross-sector portals in Unity 6000.6.0f1 on Windows. Keep the existing experimental mover available for A/B comparison until successful Play Mode and DOSBox comparisons.
