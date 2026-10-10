using System.Collections.Generic;
using UnityEngine;

namespace ROTHUnity.Runtime
{
    /// <summary>
    /// RAW opcode 9 sector translation. Numeric timing and relative-offset interpretation
    /// are experimental until verified against the retail game.
    /// </summary>
    public sealed class RothHorizontalSectorMover : MonoBehaviour
    {
        public RothMapMeshBuilder Builder;
        public CharacterController Player;
        public float RawUnitsPerSecond = 128f;
        [Tooltip("Speed per high-byte flag unit; provisional, compare against retail gameplay")]
        public float RawSpeedPerFlagUnit = 32f;
        public float AutoRevertTickSeconds = 0.1f;
        public bool CarryPlayer = true;
        public bool AvoidPlayerObstruction = true;
        [Tooltip("Emit opt-in RAW9_TRACE lines to the Unity Console for DOS timing comparisons.")]
        public bool TraceRaw9;

        private sealed class Motion
        {
            public ushort SectorId;
            public bool AlongX;
            public float Position;
            public float Destination;
            public float ReturnPosition;
            public float OutwardDestination;
            public float Speed;
            public float ReturnDelay;
            public float ReturnAt = -1f;
            public bool Returning;
            public bool Repeat;
            public short RawStart;
            public short RawEnd;
            public ushort RawFlags;
            public ushort RawRevertTicks;
        }
        private readonly List<Motion> _motions = new List<Motion>();

        public void CancelAll() { _motions.Clear(); }

        // Pipe-separated, invariant-culture records: frame, time (scaled seconds),
        // sector, axis, event, previous/next RAW X/Y, command start/end,
        // interpolated position/destination, speed, player XYZ, flags, timeout ticks.
        private void Trace(Motion motion, string action, Vector2Int before, Vector2Int after)
        {
            if (!TraceRaw9) return;
            Vector3 playerPosition = Player != null ? Player.transform.position : Vector3.zero;
            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "RAW9_TRACE|{0}|{1:F6}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11:F4}|{12:F4}|{13:F4}|{14:F4}|{15:F4}|{16:F4}|{17}|{18}",
                Time.frameCount, Time.time, motion.SectorId,
                motion.AlongX ? "X" : "Y", action, before.x, before.y,
                after.x, after.y, motion.RawStart, motion.RawEnd,
                motion.Position, motion.Destination, motion.Speed,
                playerPosition.x, playerPosition.y, playerPosition.z,
                motion.RawFlags, motion.RawRevertTicks), this);
        }

        public bool Move(ushort sectorId, bool alongX, short start, short end,
            ushort revertTicks, bool repeat, ushort flags = 0)
        {
            if (Builder == null) Builder = GetComponent<RothMapMeshBuilder>();
            if (Builder == null) return false;
            Builder.RuntimeSetSectorTextureFollowFlags(sectorId, flags);
            Vector2Int existing;
            if (!Builder.RuntimeGetSectorTranslation(sectorId, out existing)) return false;
            for (int i = _motions.Count - 1; i >= 0; --i)
                if (_motions[i].SectorId == sectorId) _motions.RemoveAt(i);
            Motion motion = new Motion {
                SectorId = sectorId, AlongX = alongX,
                Position = alongX ? existing.x : existing.y,
                // End-start remains an unverified RAW displacement.
                // Re-triggers now begin at the live in-flight sector position.
                Destination = (alongX ? existing.x : existing.y) + (float)end - start,
                OutwardDestination = (alongX ? existing.x : existing.y) + (float)end - start,
                ReturnPosition = alongX ? existing.x : existing.y,
                Speed = Mathf.Max(1f, ((flags >> 8) & 255) == 0 ? RawUnitsPerSecond :
                    ((flags >> 8) & 255) * Mathf.Max(1f, RawSpeedPerFlagUnit)),
                ReturnDelay = revertTicks * Mathf.Max(0f, AutoRevertTickSeconds),
                Repeat = repeat,
                RawStart = start, RawEnd = end,
                RawFlags = flags, RawRevertTicks = revertTicks
            };
            _motions.Add(motion);
            Trace(motion, "trigger", existing, existing);
            return true;
        }

        private void Update()
        {
            if (Builder == null) return;
            if (Player == null && CarryPlayer)
            {
                RothFirstPersonController fp = FindFirstObjectByType<RothFirstPersonController>();
                if (fp != null) Player = fp.GetComponent<CharacterController>();
            }
            for (int i = _motions.Count - 1; i >= 0; --i)
            {
                Motion m = _motions[i];
                Vector2Int before;
                if (!Builder.RuntimeGetSectorTranslation(m.SectorId, out before))
                {
                    _motions.RemoveAt(i);
                    continue;
                }
                float next = Mathf.MoveTowards(m.Position, m.Destination, m.Speed * Time.deltaTime);
                Vector2Int after = before;
                if (m.AlongX) after.x = Mathf.RoundToInt(next);
                else after.y = Mathf.RoundToInt(next);
                Vector2Int delta = after - before;
                if (delta != Vector2Int.zero)
                {
                    short sectorFloor;
                    bool hasFloor = Builder.RuntimeGetSectorHeight(m.SectorId, false, out sectorFloor);
                    bool onPlatform = CarryPlayer && Player != null && hasFloor &&
                        Builder.RuntimePlayerStandsOnFloor(m.SectorId, Player, sectorFloor);
                    Vector3 localDelta = new Vector3(
                        (Builder.MirrorWorldX ? -delta.x : delta.x) * Builder.CoordinateScale,
                        0f, delta.y * Builder.CoordinateScale);
                    Vector3 worldDelta = Builder.transform.TransformVector(localDelta);
                    // Commit geometry first. Failed vertex/object bounds checks
                    // must not move the player.
                    if (!Builder.RuntimeSetSectorTranslation(m.SectorId, after))
                    {
                        Trace(m, "bounds-rejected", before, after);
                        _motions.RemoveAt(i);
                        continue;
                    }
                    if (onPlatform)
                    {
                        Vector3 oldPosition = Player.transform.position;
                        Player.Move(worldDelta);
                        Vector3 achieved = Player.transform.position - oldPosition;
                        float required = worldDelta.magnitude;
                        float along = required > 0.000001f ?
                            Vector3.Dot(achieved, worldDelta) / required : 0f;
                        float tolerance = Mathf.Min(0.001f, required * 0.1f);
                        if (required > 0.000001f && along < required - tolerance)
                        {
                            // Undo the sector and restore the exact rider position.
                            // A compensating CharacterController.Move can itself be blocked.
                            bool restored = Builder.RuntimeSetSectorTranslation(m.SectorId, before);
                            bool enabledBefore = Player.enabled;
                            Player.enabled = false;
                            Player.transform.position = oldPosition;
                            Player.enabled = enabledBefore;
                            Physics.SyncTransforms();
                            Trace(m, restored ? "rider-blocked" : "rollback-failed", after, before);
                            if (!restored) _motions.RemoveAt(i);
                            continue;
                        }
                    }
                    Trace(m, "step", before, after);
                }
                m.Position = next;
                if (!Mathf.Approximately(next, m.Destination)) continue;
                if (m.Returning)
                {
                    if (!m.Repeat)
                    {
                        Trace(m, "returned", after, after);
                        _motions.RemoveAt(i);
                        continue;
                    }
                    m.Returning = false;
                    m.Destination = m.OutwardDestination;
                    m.ReturnAt = -1f;
                    Trace(m, "repeat", after, after);
                    continue;
                }
                if (m.ReturnDelay <= 0f)
                {
                    if (!m.Repeat)
                    {
                        Trace(m, "arrived", after, after);
                        _motions.RemoveAt(i);
                    }
                    else
                    {
                        m.Returning = true;
                        m.Destination = m.ReturnPosition;
                        Trace(m, "return", after, after);
                    }
                    continue;
                }
                if (m.ReturnAt < 0f) m.ReturnAt = Time.time + m.ReturnDelay;
                if (Time.time < m.ReturnAt) continue;
                if (AvoidPlayerObstruction && Player != null &&
                    Builder.RuntimePlayerOccupiesSector(m.SectorId, Player))
                {
                    m.ReturnAt = Time.time + 0.25f;
                    Trace(m, "return-obstructed", after, after);
                    continue;
                }
                m.Returning = true;
                m.Destination = m.ReturnPosition;
                Trace(m, "return", after, after);
            }
        }
    }
}
