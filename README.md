# ROTH Unity

Open-source Unity reimplementation of **Realms of the Haunting**, designed to use the original game data while recreating its engine, gameplay and presentation on modern systems.

> **Status:** very early development.

## Goals

- Load and validate an original *Realms of the Haunting* installation.
- Document and reimplement the original data formats.
- Recreate maps, rendering, interactions, gameplay, audio and cinematics in Unity.
- Preserve the original game experience before adding optional modern enhancements.
- Never distribute original copyrighted game assets in this repository.

## Current development milestone

The first milestone is a data compatibility layer able to locate an original installation, identify its core files and produce a reproducible inventory for reverse-engineering work.

The English game data is currently used as the primary reference build.

## Original game data

ROTH Unity will require a legally obtained copy of *Realms of the Haunting*. Original game assets are not included in this repository.

## Project structure

```text
Assets/ROTHUnity/
  Scripts/
    Data/       Original-game data discovery and validation
    Formats/    Binary format readers
    Runtime/    Runtime systems

docs/           Reverse-engineering notes and format documentation
```
