# ROTH Unity Milestone 0.5

This milestone moves the project from map-only reconstruction toward a data-driven runtime.

## Added

- RAW command parser: variable-sized commands, category table and entry references.
- RAW ambient sound parser: positional SFX records and 3-zone attenuation records.
- Object texture-source mapping for source 0/1 (primary DAS) and source 2/3 (secondary DAS).
- Automatic secondary archive discovery using `ADEMO.DAS` next to the selected primary DAS.
- Standard sprite objects.
- Type-1 animated resources displayed using their original uncompressed base frame.
- Image-pack first-view decoding as an interim directional-object fallback.
- `EXP2` 3D object parsing and Unity mesh generation, including image-pack subtextures and palette faces.
- Runtime Y-axis billboard component for sprite objects.

## English STUDY1 ground-truth checks

- 507 sectors, 2,454 faces, 1,076 vertices.
- 16 intermediate platforms and 274 objects.
- 672 commands with 226 resolved entry references and 0 unresolved command references.
- 104 positional SFX records and 32 sound zones.
- DEMO.DAS + ADEMO.DAS provide direct/first-frame/image-pack rendering for most sprite records; 3D objects are now parsed separately.

## Still incomplete

- Full animation playback rather than base-frame display.
- 8-direction image-pack switching based on camera/object angle.
- Monster/directional mapping execution.
- Command opcode semantics and gameplay execution.
- SFX archive decoding/playback.
- Object collision fidelity and scripted doors.
