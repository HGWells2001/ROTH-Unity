# ROTH Unity 0.10.18: verified RAW9 four-vertex references

**Type:** DOS reverse engineering and read-only Unity/RAW diagnostic tooling. The playable movement code has NOT changed and Unity was not compiled.

## Ground truth

Original user-owned GOG ROTH.EXE SHA-256: e2d54427cd0692798e2df457b1ea8d3bca8cf1383ec2d4ea782165fe0ba56a05.

- Opcode9 initializer: LE object1 +0x22A99, vertex-reference setup +0x22AF8..0x22B8F.
- Reads sector first face pointer at sector record byte offset 14.
- Selects first vertex from four consecutive face records, relative offsets 0, 12, 24 and 36 bytes.
- Each first vertex offset is resolved to a 12-byte RAW vertex record. X is at record+8 and Y at record+10; command flag 0x40 chooses X.
- Pairs of signed16 coordinate and vertex reference are bubble sorted in stable ascending order. The original callback at +0x22BD9 applies the same low16 delta to four selected coordinate fields at +0x22C7E..0x22C9D.
- The reference to RAW map memory at instruction +0x22AEE relocates to LE object3+0x20E88; the vertex base at +0x22AF4 relocates to object3+0x20E8C. Two callback vertex-pointer relocations were also checked.

The new read-only DOS verifier confirms 17 short instruction signatures plus 4 relocated global pointers; see Documentation/ROTH_RAW9_VERTEX_EVIDENCE.json. No original game bytes are redistributed.

## New project functionality

- Assets/ROTHUnity/Core/RothRaw9VertexInspector.cs: validated first-four-face selection, signed stable sort, uniqueness and pointer consistency checks without writing map data.
- Assets/ROTHUnity/Editor/RothRaw9VertexAuditMenu.cs: Unity Editor menu ROTH > Audit original RAW9 vertex references... to print all matching original command-sector references, including malformed or non-quadrilateral sectors.
- Tools/audit_roth_raw9_geometry.py: examines a legally owned RAW file, directory or ZIP and produces an optional JSON report.
- Tools/audit_roth_raw9_vertex_x86.py: checks the source DOS executable without extracting commercial code.
- Tools/test_roth_raw9_geometry.py: 16 synthetic regression tests, including instruction corruption and wrong-relocation rejection.

## Commands

PowerShell on Windows:

    python Tools\audit_roth_raw9_geometry.py "C:\path\to\your\map.RAW" --json raw9_vertices.json
    python Tools\audit_roth_raw9_vertex_x86.py "C:\path\to\your\ROTH.EXE" --json raw9_dos_evidence.json
    python -m unittest discover -s Tools -p "test_roth_raw9_geometry.py" -v

## Restrictions and next tasks

The personally supplied executable was verified, but this session did not include any retail RAW maps. Therefore the quad/non-quad behavior of real opcode9 commands remains to be measured on user-owned retail map data. Validated instructions do not prove safe portal/collider behavior in Unity.

The helper and Editor command do NOT change RothHorizontalSectorMover, object positions, physics or collisions. The Unity Editor/compiler was unavailable, so the source has not been compiled in Unity. The next phase is retail RAW validation, followed by an explicit opt-in geometry prototype and Play Mode testing. No original commercial game resources are included in this repository.
