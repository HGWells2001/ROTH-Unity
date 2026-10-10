# ROTH Unity 0.10.4

Prototype of timed auto-return for RAW opcode 7 floor/ceiling heights.

- Retains starting heights and schedules a return after the fifth RAW argument (`AutoClose Timeout`).
- Allows a fresh movement command to replace an in-flight motion.
- Cancels animations on map transitions.
- Assumes `AutoCloseTickSeconds = 0.1`; **unverified** against original gameplay.
- `PreventClosingOnPlayer` is a placeholder, collision-based obstruction detection is not implemented.
- No Unity editor/compiler validation has been performed.
- This package contains no original copyrighted game assets.
