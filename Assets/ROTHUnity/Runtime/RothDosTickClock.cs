using System;
using System.Globalization;
using UnityEngine;

namespace ROTHUnity.Runtime
{
    /// <summary>
    /// Nominal DOS IRQ0 tick source for A/B pacing experiments.
    /// Never reads DOS memory and does not claim cycle-perfect emulation.
    /// Independently tracked from Unity wall time so all moving sectors
    /// can share one per-frame integer tick delta.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public sealed class RothDosTickClock : MonoBehaviour
    {
        // Observed in user-owned GOG ROTH.EXE: PIT channel 0, divider 0x4216
        // reprogrammed inside IRQ0 (after an initial divider 0x4242).
        public const double PitInputHz = 1193182.0;
        public const int RunningDivisor = 0x4216;
        public const int InitialDivisor = 0x4242;
        public const int DosGameFrameCap = 45;
        public static double NominalTicksPerSecond
        {
            get { return PitInputHz / RunningDivisor; }
        }

        [Tooltip("Use scaled Unity time so pausing via timeScale also pauses the simulated DOS ticks.")]
        public bool UseScaledTime = true;
        [Tooltip("Conservative per-frame safety cap seen in one DOS game loop; the exact scheduler path is unverified.")]
        [Range(1, DosGameFrameCap)]
        public int MaxPacingTicksPerFrame = DosGameFrameCap;
        [Tooltip("Emit opt-in ROTH_PIT_TRACE events for frame/timer comparisons.")]
        public bool TraceTicks;

        public ushort Counter { get; private set; }
        public int ElapsedTicksThisFrame { get; private set; }
        public int PacingTicksThisFrame { get; private set; }
        private double _fraction;

        private void Update()
        {
            Advance(UseScaledTime ? Time.deltaTime : Time.unscaledDeltaTime);
            if (!TraceTicks) return;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "ROTH_PIT_TRACE|{0}|{1:F6}|{2}|{3}|{4}|{5:F6}",
                Time.frameCount, Time.time, Counter,
                ElapsedTicksThisFrame, PacingTicksThisFrame, _fraction), this);
        }

        /// <summary>
        /// Advance the nominal IRQ counter from elapsed seconds. Public to
        /// allow deterministic manual tests in Unity. Does not accelerate
        /// animations from a frame stall beyond MaxPacingTicksPerFrame.
        /// </summary>
        public void Advance(double deltaSeconds)
        {
            ElapsedTicksThisFrame = 0;
            PacingTicksThisFrame = 0;
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds)
                || deltaSeconds < 0 || deltaSeconds > 3600) return;

            double exactTicks = _fraction + deltaSeconds * NominalTicksPerSecond;
            long whole = (long)Math.Floor(exactTicks);
            _fraction = exactTicks - whole;
            Counter = unchecked((ushort)(Counter + (int)(whole & 0xFFFF)));
            ElapsedTicksThisFrame = (int)Math.Min(whole, int.MaxValue);
            PacingTicksThisFrame = Math.Min(ElapsedTicksThisFrame,
                Math.Max(1, Math.Min(DosGameFrameCap, MaxPacingTicksPerFrame)));
        }

        public void ResetCounter()
        {
            Counter = 0;
            _fraction = 0;
            ElapsedTicksThisFrame = 0;
            PacingTicksThisFrame = 0;
        }
    }
}
