# ROTH Unity 0.10.9 — RAW 9 safety and reproducibility

**Status: experimental, not yet a verified faithful reproduction of the Gremlin original.**

Changes:
- New motion triggers start from the live sector position and return to that position.
- Original sector-face vertex indices are prevalidated before detaching shared vertices.
- All vertex and moving-object positions are checked as a single transaction against signed 16-bit RAW limits. No corner-by-corner clamping.
- Geometry commits ahead of rider motion; clipped rider motion requests a rollback.
- Five numerical/static regression tests were added in Tools/test_raw9_regression.py.

Important limitations:
- The original DOS executable's actual opcode-9 routine has not been traced here.
- Start/end distance, speed-byte unit, return tick rate, repeat semantics and independent texture flags are still hypotheses from command fields and retail-map patterns.
- Portal seams, general collisions and CharacterController rollback can fail in live scenes; Unity Editor compilation and play-testing have not been performed.
- This is **not** a full or proven faithful implementation of RAW opcode 9.

Reproducing tests: `python Tools/test_raw9_regression.py`.

No original game retail assets are distributed.
