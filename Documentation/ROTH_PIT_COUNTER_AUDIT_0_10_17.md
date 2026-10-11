# 0.10.17 supplementary PIT-counter audit (independent of the RAW9 step reference)

This note complements `MILESTONE_0_10_17.md` and does **not** replace it. It records the dedicated checks implemented by:

- `Tools/audit_roth_pit_timer.py` (15 instruction sites, 9 relocated address operands).
- `Tools/test_roth_pit_timer.py` (15 synthetic tests with negative corruption cases).
- `Assets/ROTHUnity/Runtime/RothDosPitClock.cs` (optional reference tick accumulator; **not connected to gameplay**).

These new files were checked against the supplied original GOG `ROTH.EXE` with SHA-256 `e2d54427cd0692798e2df457b1ea8d3bca8cf1383ec2d4ea782165fe0ba56a05`.

## Verified chain and correction

- IRQ0 vector 8 points to LE object 1 + `0x2431`.
- Startup PIT divisor is `0x4242` (16962), while the interrupt-time reload at object1+`0x2438` uses `0x4216` (16918).
- The IRQ callback calls object1+`0x23DE`, which **increments a 16-bit word relocated to LE object 3 + `0x20FAC`**.
- The main loops read the same counter and store a sign-extended 32-bit delta at LE object 3 + `0x1570C`; the horizontal RAW opcode 9 callback reads the latter.
- Two special-mode routines (object1+`0x1325` and `0x1370`) also write this counter, so standard IRQ cadence is not its exclusive source under all conditions.

**Erratum:** earlier reports described the counter as belonging to object 1. Its unrelocated immediate value is `0x20FAC`, but the LE fixups prove it belongs to **object 3**.

## Nominal PC PIT timer

Using the conventional 1,193,182 Hz PC-compatible PIT input clock:
- Initial nominal rate: **70.344416932 Hz** = 1193182/16962.
- Timer IRQ's programmed nominal rate: **70.527367301 Hz** = 1193182/16918.
- Nominal tick duration: **14.178893 ms**.

Those are **programmed hardware rates**, not observed frame cadence or proof of DOSBox's scheduling. The original main loop uses **signed-extended 16-bit counter reads and a 32-bit subtraction**, not a simple unsigned modular subtraction. At the signed boundary, naive smoothing changes machine-code behavior. The standalone C# `RothDosPitClock` purposely exposes this detail while remaining outside the active mover.

## Reproducing on Windows

```powershell
python Tools\audit_roth_pit_timer.py "C:\Program Files (x86)\GOG Galaxy\Games\Realms of the Haunting\ROTH\ROTH.EXE" --json pit_evidence.json
python Tools\test_roth_pit_timer.py
```

With this specimen, the auditor passed all 15 instruction-site and 9 relocated-operand checks. The synthetic test module passed **15/15**, and the previous 0.10.13–0.10.16 Python suites passed **40/40** (55 tests in that particular set; other independent test modules may raise the repository-wide total).

No Unity compilation or playtesting was performed. These files contain no proprietary executable image, retail maps or original game assets; the verification scripts do contain very short instruction-byte fingerprints.
