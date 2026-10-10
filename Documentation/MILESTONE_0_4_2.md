# Milestone 0.4.2 - World orientation hotfix

User testing of 0.4.1 showed that texture mapping was largely corrected but the whole level appeared mirrored.

Changes:
- Original ROTH X orientation is now the default (`MirrorWorldX = false`).
- Legacy 0.4.1 X mirroring can be toggled for A/B comparison.
- Floor, ceiling, platform and wall triangle winding follows the selected handedness.
- Sector UV grid uses the same X coordinate convention as generated geometry.
- Parsed object positions and rotations follow the selected convention.
- Debug view and player-start marker follow the same convention.

No original game assets are distributed.
