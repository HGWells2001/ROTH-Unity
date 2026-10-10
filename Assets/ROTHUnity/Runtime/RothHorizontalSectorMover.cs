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
        public float AutoRevertTickSeconds = 0.1f;
        public bool CarryPlayer = true;
        public bool AvoidPlayerObstruction = true;

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
        }
        private readonly List<Motion> _motions = new List<Motion>();

        public void CancelAll() { _motions.Clear(); }

        public bool Move(ushort sectorId, bool alongX, short start, short end,
            ushort revertTicks, bool repeat)
        {
            if (Builder == null) Builder = GetComponent<RothMapMeshBuilder>();
            if (Builder == null) return false;
            Vector2Int existing;
            if (!Builder.RuntimeGetSectorTranslation(sectorId, out existing)) return false;
            for (int i = _motions.Count - 1; i >= 0; --i)
                if (_motions[i].SectorId == sectorId) _motions.RemoveAt(i);
            _motions.Add(new Motion {
                SectorId = sectorId, AlongX = alongX,
                Position = alongX ? existing.x : existing.y,
                Destination = end,
                OutwardDestination = end,
                ReturnPosition = start,
                Speed = Mathf.Max(1f, RawUnitsPerSecond),
                ReturnDelay = revertTicks * Mathf.Max(0f, AutoRevertTickSeconds),
                Repeat = repeat
            });
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
                    bool onSector = Player != null &&
                        Builder.RuntimePlayerOccupiesSector(m.SectorId, Player);
                    Vector3 localDelta = new Vector3(
                        (Builder.MirrorWorldX ? -delta.x : delta.x) * Builder.CoordinateScale,
                        0f, delta.y * Builder.CoordinateScale);
                    Vector3 worldDelta = Builder.transform.TransformVector(localDelta);
                    if (onSector && CarryPlayer) Player.Move(worldDelta);
                    if (!Builder.RuntimeSetSectorTranslation(m.SectorId, after))
                    {
                        _motions.RemoveAt(i);
                        continue;
                    }
                }
                m.Position = next;
                if (!Mathf.Approximately(next, m.Destination)) continue;
                if (m.Returning)
                {
                    if (!m.Repeat) { _motions.RemoveAt(i); continue; }
                    m.Returning = false;
                    m.Destination = m.OutwardDestination;
                    m.ReturnAt = -1f;
                    continue;
                }
                if (m.ReturnDelay <= 0f) { _motions.RemoveAt(i); continue; }
                if (m.ReturnAt < 0f) m.ReturnAt = Time.time + m.ReturnDelay;
                if (Time.time < m.ReturnAt) continue;
                if (AvoidPlayerObstruction && Player != null &&
                    Builder.RuntimePlayerOccupiesSector(m.SectorId, Player))
                {
                    m.ReturnAt = Time.time + 0.25f;
                    continue;
                }
                m.Returning = true;
                m.Destination = m.ReturnPosition;
            }
        }
    }
}
