# ROTH Unity 0.10.17: verified DOS PIT IRQ0 and RAW9 coordinate-step reference

**Scope:** Reverse engineering of the user-owned GOG ROTH.EXE, plus an isolated C# calculation reference. No gameplay integration or Unity Editor build was performed.

## Ground truth

- Executable SHA-256: `e2d54427cd0692798e2df457b1ea8d3bca8cf1383ec2d4ea782165fe0ba56a05`
- 32-bit x86 LE object-1 offsets: opcode 9 initializer `0x22A99`, opcode 9 periodic updater `0x22BD9`. Opcode-7 updater `0x22D51` has the previously misattributed six-bit fraction path.
- The independent verifier checks **20 instruction signatures**, **5 LE-relocated address operands**, and the existing two command dispatchers.

## Proven DOS timer chain

1. At object1+`0x2532`, the program registers interrupt vector **8 (IRQ0)** with handler object1+`0x2431`.
2. Setup code at `0x255A..0x2562` programs PIT channel 0: control `0x3E` to port `0x43`; low and high `0x42` to port `0x40`. Initial divisor = `0x4242` (16962).
3. The IRQ0 handler at `0x2438..0x2442` programs PIT `0x3E` again, followed by `0x16` and `0x42` to port `0x40`. Reprogramming divisor = `0x4216` (16918). PIT mode 7 aliases mode 3.
4. From `0x2454`, the handler calls object1+`0x23DE`, which increments a 16-bit variable **relocated to LE object 3 + `0x20FAC`**.
5. The game loops at `0x102D2` and `0x1046D` sign-extend that 16-bit counter, subtract a previous sample, then store a 32-bit frame delta to **object 3 + `0x1570C`**. The opcode-9 callback reads that delta at `0x22BEB`.
6. Routine `0x25BC` restores PIT divisor `0xFFFF`, corresponding to the conventional ~18.2 Hz PC timer.

The *object-3* timer location corrects the earlier confusion between unrelocated operand `0x20FAC` and the relocated object location.

### Derived nominal clock

| Programmed stage | Divisor | Frequency from 1,193,182 Hz PIT |
|---|---:|---:|
| Initial IRQ setup | `0x4242` (16962) | 70.3444169 Hz |
| IRQ reprogramming | `0x4216` (16918) | **70.5273673 Hz** |
| Standard DOS restoration | 65536 | ~18.2065 Hz |

The ~70.53 Hz figure is a **nominal hardware calculation**, not an observed gameplay measurement. In particular, reprogramming during the interrupt may introduce timing deviations. Counter wrap and emulator timing are not yet verified.

Technical background: [PIT and IRQ0 timing](https://www.xtof.info/Timing-on-PC-familly-under-DOS.html), [PIT operating modes](https://wiki.osdev.org/Programmable_Interval_Timer).

## Confirmed RAW9 arithmetic, but only for selected coordinates

The callback at `0x22BD9` computes movement from global frame delta times unsigned speed byte `command[7]`. Forward and reverse branches use signed-16 target `-2 * signed16(command word at +0xA or +0xC)`, respectively. They apply signed-16 overshoot comparisons and add the resulting low-16-bit increment to four coordinate words referenced by the animation record. The mapping of those four words to raw sector polygons and portals is still unresolved.

The old Unity interpretation `end - start` and fixed `Time.deltaTime` velocity is therefore **not proven faithful**. The helper `Assets/ROTHUnity/Runtime/RothDosRaw9Reference.cs` implements the verified low-level step, target wrap and signed timer subtraction but is **not wired into RothHorizontalSectorMover**.

## Reproduction

```powershell
python Tools\audit_roth_pit_raw9.py "C:\Program Files (x86)\GOG Galaxy\Games\Realms of the Haunting\ROTH\ROTH.EXE" --json raw9_pit_017.json
python -m unittest discover -s Tools -p "test_roth_*.py" -v
```

Locally **64 Python tests passed** (40 previous + 18 new arithmetic/audit + 6 new C# static contracts). The C# contracts do **not** compile or run C#. No Unity build or Play Mode checks were possible here.

## Next tasks

- Verify which four RAW vertex references are moved in retail maps.
- Implement a separate optional tick-driven animation state machine, then integrate only after cross-mapping and runtime tests.
- Compare Unity movement against the original running under DOSBox at the recorded hardware tick rates.
- Keep both original commercial executable and assets out of the repository.

The repository contains brief byte signatures for local verification, **not** the ROTH executable, LE object extracts, a full disassembly or RAW/DAS proprietary assets.
