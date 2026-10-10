# ROTH Unity 0.10.4 — sector mover prototype

Work-in-progress local milestone. This document tracks the matching source ZIP produced for testing.

## Changes

- RAW opcode 7 floor/ceiling motion now optionally returns to its original height after its auto-close timeout argument.
- Multiple movement triggers replace an already active motion for the same floor or ceiling.
- Motions cancel on map changes.
- Mesh and collision updates remain driven from sector heights.

## Verification status

- Unity build: **not run**.
- Retail timing: **not yet validated**. `AutoCloseTickSeconds` is an experimental 0.1-second conversion, not a verified specification.
- Player obstruction/anti-crush logic: **not implemented**; `PreventClosingOnPlayer` is not active yet.
- Source ZIP SHA-256: `cc569370c5665a437e42f8f8c6ef8eb8b38c9875a2ff1d51a3eb29133d9bac79`.

## Repository synchronization

**Documentation only** in this commit. The milestone 0.10.4 source tree is packaged separately and must still be merged into the repository before GitHub can be treated as the canonical source. The older `Assets/ROTHUnity/Scripts` scaffold was deliberately not overwritten.

No copyrighted retail game data is included.
