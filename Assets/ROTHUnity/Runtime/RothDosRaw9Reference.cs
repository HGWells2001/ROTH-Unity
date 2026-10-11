using System;

namespace ROTHUnity.Runtime
{
    /// <summary>
    /// Pure calculation reference for selected RAW opcode-9 x86 basic blocks.
    /// Not connected to gameplay: vertex selection, animations, retries and
    /// exact real-time IRQ behavior require additional DOS/Unity validation.
    /// </summary>
    public static class RothDosRaw9Reference
    {
        // IBM PC nominal PIT rate. The GOG ROTH.EXE IRQ0 handler programs
        // 0x4216 after its initial 0x4242 programming. Actual IRQ cadence can
        // differ owing to interrupt overhead and PIT reprogramming.
        public const double PitInputHz = 1193182.0;
        public const ushort InitialPitDivisor = 0x4242;
        public const ushort RunningPitDivisor = 0x4216;
        public static double NominalTicksPerSecond => PitInputHz / RunningPitDivisor;

        // ROTH explicitly sign-extends the 16-bit counter before subtraction.
        // Do not use a modular unsigned subtraction for this code path.
        public static int SignedCounterDelta(ushort now, ushort previous)
        {
            return (short)now - (short)previous;
        }

        public static short TargetFromCommandWord(short signedCommandBound)
        {
            return unchecked((short)(-2 * (int)signedCommandBound));
        }

        public readonly struct AxisStep
        {
            public readonly short Delta;
            public readonly short Target;
            public readonly bool Reached;
            public readonly bool Clamped;

            public AxisStep(short delta, short target, bool reached, bool clamped)
            {
                Delta = delta;
                Target = target;
                Reached = reached;
                Clamped = clamped;
            }
        }

        /// <summary>
        /// Reproduces the sign-extended bounds, 32-bit speed multiplication,
        /// signed 16-bit compare and overshoot clamp at 0x22C00..0x22C79.
        /// The returned delta applies to four selected RAW map coordinate
        /// words, which Unity does not yet identify reliably per command.
        /// </summary>
        public static AxisStep ComputeAxisStep(short currentCoordinate,
            short signedCommandBound, int elapsedCounterDelta, byte speed,
            bool reverse)
        {
            short target = TargetFromCommandWord(signedCommandBound);
            if (currentCoordinate == target)
                return new AxisStep(0, target, true, false);

            int signedSpeed = unchecked(elapsedCounterDelta * (int)speed);
            if (reverse) signedSpeed = unchecked(-signedSpeed);
            short rawDelta = unchecked((short)signedSpeed);
            short candidate = unchecked((short)(currentCoordinate + rawDelta));
            bool clamp = reverse ? candidate < target : candidate > target;
            if (clamp)
            {
                short overshoot = unchecked((short)(candidate - target));
                rawDelta = unchecked((short)(rawDelta - overshoot));
            }
            bool reached = unchecked((short)(currentCoordinate + rawDelta)) == target;
            return new AxisStep(rawDelta, target, reached, clamp);
        }
    }
}
