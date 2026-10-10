# Milestone 0.10.7 — platform carry and clearance

- Player is moved vertically via `CharacterController.Move` while standing on a RAW moving floor.
- Rising floors carry before collider mesh regeneration; descending floors carry after collider update.
- New floor and ceiling values are checked against the player capsule before geometry changes.
- Sector geometry and player vertical offset are committed together when raw height changes.

## Known limitations

- Experimental: no Unity compile/play test in this environment.
- Capsule geometry currently approximated by local center + height, without arbitrary rotation/scale accounting.
- Transport does not include horizontal moving sectors (RAW opcode 9).
- Original timing, obstruction policy, and step behaviour still need retail validation.
- Carry can be affected by collisions with external geometry; more robust rollback and tests are needed.
- No copyrighted retail game files are included.

## Source archive

ROTH-Unity-Milestone-0.10.7-Source.zip, SHA-256: `506095bc5a9ec7a7e11df82460da7618bbada17b1cae6a6dcc85e13af4e49fc1`.
