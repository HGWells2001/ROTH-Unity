using System;
using System.Collections.Generic;
using UnityEngine;

namespace ROTHUnity.Runtime
{
    /// <summary>
    /// Prototype RAW opcode 7 height animation. Updates the mesh and collision from
    /// live map state. Timing and autoclose units require in-game retail verification.
    /// </summary>
    public sealed class RothSectorMover : MonoBehaviour
    {
        public RothMapMeshBuilder Builder;
        [Tooltip("RAW height units per second; tune against original gameplay.")]
        public float RawUnitsPerSecond = 128f;
        [Tooltip("Limit expensive mesh/collider rebuilds.")]
        public float MeshRefreshInterval = 0.04f;
        [Tooltip("Approximate seconds per RAW autoclose tick. Not retail-verified.")]
        public float AutoCloseTickSeconds = 0.1f;
        [Tooltip("Do not close through a nearby player when possible.")]
        public bool PreventClosingOnPlayer = true;

        private sealed class Motion
        {
            public ushort Id;
            public bool Ceiling;
            public float Current;
            public short End;
            public float Speed;
            public float LastRefresh;
            public short ReturnHeight;
            public short TargetHeight;
            public float AutoCloseTime;
            public float HoldDuration;
            public bool Returning;
            public bool ReachedOpenPosition;
        }

        [Tooltip("CharacterController whose capsule must not be trapped by a closing sector.")]
        public CharacterController Player;
        [Tooltip("Delay in seconds between obstruction rechecks.")]
        public float ObstructionRetrySeconds = 0.25f;

        private readonly List<Motion> _motions = new List<Motion>();

        public bool Move(ushort sectorId, bool ceiling, short start, short end, bool slow, ushort autoCloseTicks = 0)
        {
            if (Builder == null) Builder = GetComponent<RothMapMeshBuilder>();
            if (Builder == null) return false;
            short existing;
            if (!Builder.RuntimeGetSectorHeight(sectorId, ceiling, out existing)) return false;
            for (int i = _motions.Count - 1; i >= 0; --i)
                if (_motions[i].Id == sectorId && _motions[i].Ceiling == ceiling)
                    _motions.RemoveAt(i);
            // Preserve the actual position when a second trigger interrupts a moving sector.
            // The original start/end pair still defines the open/return destinations.
            _motions.Add(new Motion {
                Id = sectorId, Ceiling = ceiling, Current = existing, End = end,
                Speed = Mathf.Max(1f, RawUnitsPerSecond * (slow ? 0.5f : 1f)),
                LastRefresh = Time.time, ReturnHeight = start, TargetHeight = end,
                AutoCloseTime = -1f,
                HoldDuration = autoCloseTicks == 0 ? 0f : autoCloseTicks * Mathf.Max(0f, AutoCloseTickSeconds)
            });
            return true;
        }

        public void CancelAll() { _motions.Clear(); }

        private void Update()
        {
            if (Builder == null) return;
            if (Player == null && PreventClosingOnPlayer)
            {
                RothFirstPersonController fp = FindFirstObjectByType<RothFirstPersonController>();
                if (fp != null) Player = fp.GetComponent<CharacterController>();
            }
            for (int i = _motions.Count - 1; i >= 0; --i)
            {
                Motion m = _motions[i];
                // Reopen if a closing sector becomes obstructed. Do not crush the controller.
                if (m.Returning && PreventClosingOnPlayer && Player != null &&
                    Builder.RuntimePlayerOccupiesSector(m.Id, Player))
                {
                    m.Returning = false;
                    m.End = m.TargetHeight;
                    m.AutoCloseTime = -1f;
                    m.ReachedOpenPosition = false;
                }
                m.Current = Mathf.MoveTowards(m.Current, m.End, m.Speed * Time.deltaTime);
                bool reached = Mathf.Approximately(m.Current, m.End);
                // Every reached position is committed once, regardless of refresh interval.
                if (reached || Time.time - m.LastRefresh >= MeshRefreshInterval)
                {
                    Builder.RuntimeSetSectorHeight(m.Id, m.Ceiling,
                        (short)Mathf.Clamp(Mathf.RoundToInt(m.Current), short.MinValue, short.MaxValue));
                    m.LastRefresh = Time.time;
                }
                if (!reached) continue;
                if (m.Returning || m.HoldDuration <= 0f)
                {
                    _motions.RemoveAt(i);
                    continue;
                }
                // Start the delay ONLY when the open destination is reached.
                if (!m.ReachedOpenPosition)
                {
                    m.ReachedOpenPosition = true;
                    m.AutoCloseTime = Time.time + m.HoldDuration;
                }
                if (Time.time < m.AutoCloseTime) continue;
                if (PreventClosingOnPlayer && Player != null &&
                    Builder.RuntimePlayerOccupiesSector(m.Id, Player))
                {
                    m.AutoCloseTime = Time.time + Mathf.Max(0.05f, ObstructionRetrySeconds);
                    continue;
                }
                m.Returning = true;
                m.End = m.ReturnHeight;
            }
        }
    }
}
