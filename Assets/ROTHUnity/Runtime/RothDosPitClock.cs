using System;

namespace ROTHUnity.Runtime
{
    /// <summary>
    /// Optional, unconnected reference clock for ROTH DOS movement reconstruction.
    /// The retail IRQ0 handler programs PIT channel 0 with 0x4216 after an
    /// initial 0x4242 load. That is a nominal hardware rate, not a measured
    /// DOSBox or Unity callback frequency. This class does not mutate gameplay.
    /// </summary>
    public sealed class RothDosPitClock
    {
        public const double PitInputHz = 1193182.0;
        public const int StartupDivisor = 0x4242;
        public const int InterruptDivisor = 0x4216;
        public const double NominalTicksPerSecond = PitInputHz / InterruptDivisor;

        private double _fractionalTicks;
        public ushort Counter { get; private set; }
        public double FractionalTicks { get { return _fractionalTicks; } }

        public void Reset(ushort initialCounter = 0)
        {
            Counter = initialCounter;
            _fractionalTicks = 0.0;
        }

        /// <summary>
        /// Advance a standalone counter from elapsed, *unscaled* real seconds.
        /// The returned count is useful for offline comparison, but driving
        /// the live RAW9 mover from it requires a separate validated opt-in.
        /// </summary>
        public int AdvanceUnscaledSeconds(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0.0)
                throw new ArgumentOutOfRangeException(nameof(seconds));

            double ticks = seconds * NominalTicksPerSecond + _fractionalTicks;
            if (double.IsInfinity(ticks) || ticks >= int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(seconds));

            int whole = (int)Math.Floor(ticks);
            _fractionalTicks = ticks - whole;
            Counter = unchecked((ushort)(Counter + whole));
            return whole;
        }

        /// <summary>
        /// ROTH's main loop MOVSXes both 16-bit timer snapshots and subtracts
        /// as 32-bit integers. A sign boundary is NOT a modulo-16-bit delta.
        /// This is deliberately different from unchecked((ushort)(now - old)).
        /// </summary>
        public static int OriginalSignedFrameDelta(ushort now, ushort previous)
        {
            return unchecked((short)now) - unchecked((short)previous);
        }

        /// <summary>
        /// Opcode 9 target coordinate uses the signed command word doubled,
        /// negated, and truncated to signed 16-bit before the signed comparison.
        /// </summary>
        public static short DecodeHorizontalBound(short commandWord)
        {
            return unchecked((short)(-2 * (int)commandWord));
        }
    }
}
