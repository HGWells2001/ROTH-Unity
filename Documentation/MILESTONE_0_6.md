# Milestone 0.6 - Playable data-driven test

Milestone 0.6 moves ROTH Unity from a mostly static map renderer toward a playable runtime test driven by original game data.

Highlights:
- Quick Start GOG window with the preferred installation path `C:\Program Files (x86)\GOG Galaxy\Games\Realms of the Haunting`.
- Automatic discovery of STUDY1.RAW, DEMO.DAS, ADEMO.DAS, FX22.SFX/FXSCRIPT.SFX and DBASE100.DAT.
- FPS test controller using the original RAW spawn metadata.
- Full structural object coverage for STUDY1: 274/274 records recognized through standard images, type-1/type-2 animations, image packs, directional/monster mappings or EXP2 3D objects.
- Eight-direction object/monster view selection relative to the player camera.
- Type-1 delta animation decoding and type-2 RLE animation decoding.
- Positional PCM sound decoding from the 0XFS archive and creation of Unity AudioSources from RAW sound nodes.
- Runtime RAW command monitor for entry triggers and safe state-only commands.
- Object/floor/face/sector click bridges using original Object IDs, Sector IDs and Face IDs.
- DBASE100 header/global-action parser and inspector. Global actions are currently observed, not executed.

Door opcode 47 is recognized and can be reached through the original command chain, but door movement is intentionally not guessed until the original motion semantics are verified.

No original game assets are included.
