# Milestone 0.3 report

Implemented:

- native DASP v5 header parser
- 4-block FAT parser
- VGA palette decoding
- filename and description table parsing
- ordinary indexed-image decoding
- Unity Texture2D/material generation with point filtering
- direct palette-colour materials used by RAW sectors/faces
- texture-aware RAW mesh submeshes
- first-pass floor/ceiling/wall UVs and original sector texture shifts
- DAS Texture Inspector editor window
- Browse RAW / Browse DAS controls in the mesh-builder inspector
- independent DAS validation script

English retail validation:

- DEMO.DAS: 4,382 FAT entries; 744 standard images; 57 animations; 12 image packs; 20 directional/object entries; 0 malformed ordinary-image entries in validator
- DEMO1.DAS: 4,389 FAT entries; 918 standard images; 0 malformed ordinary-image entries
- DEMO2.DAS: 4,152 FAT entries; 174 standard images; 0 malformed ordinary-image entries
- DEMO3.DAS: 4,133 FAT entries; 286 standard images; 0 malformed ordinary-image entries
- DEMO4.DAS: 4,393 FAT entries; 808 standard images; 0 malformed ordinary-image entries

STUDY1 + DEMO.DAS coverage in the current renderer:

- 311 distinct material references after separating direct palette colours
- 287 direct ordinary DAS textures decoded
- 15 direct palette-colour references handled
- 8 compressed/animated special resources deferred
- 1 empty FAT index
- 302/311 (97.1%) directly renderable by milestone 0.3

Known fidelity gaps:

- exact face texture scaling / pin-bottom / image-fit / half-pixel / flip flags need porting
- floor/ceiling texture scaling flags need porting
- animated and packed DAS images are not decoded yet
- lighting/shading tables are not applied yet
- intermediate platforms are not generated yet
- object sprites and map command behaviour are not generated yet
