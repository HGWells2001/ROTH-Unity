using System;
using System.Collections.Generic;
using ROTHUnity.Core;
using UnityEngine;

namespace ROTHUnity.Runtime
{
    /// <summary>
    /// Opt-in, guarded prototype for moving the four RAW9-selected vertex words
    /// rather than detaching and translating the whole sector polygon.
    /// This geometry backend does NOT claim authentic DOS command timing.
    /// </summary>
    public sealed partial class RothMapMeshBuilder
    {
        private sealed class Raw9SelectedMotion
        {
            public bool AlongX;
            public int SectorIndex;
            public int[] Indices;
            public RothVertex[] InitialVertices;
            public Vector2Int Offset;
        }

        private readonly Dictionary<ushort, Raw9SelectedMotion> _raw9SelectedSectorMotions =
            new Dictionary<ushort, Raw9SelectedMotion>();

        /// <summary>Checks a movable quad before the first mutation; refuses hazards.</summary>
        public bool RuntimeTryPrepareSelectedRaw9Motion(ushort sectorId, bool alongX, out string reason)
        {
            reason = null;
            if (_map == null) { reason = "RAW map has not been loaded"; return false; }
            Raw9SelectedMotion existing;
            if (_raw9SelectedSectorMotions.TryGetValue(sectorId, out existing))
            {
                if (existing.AlongX == alongX) return true;
                reason = "Sector already moves along the other axis";
                return false;
            }
            if (_movingSectorVertices.Count != 0)
            {
                reason = "A legacy moving sector is already active; mixed backends are disabled";
                return false;
            }

            RothRaw9VertexInspector.Selection selection;
            if (!RothRaw9VertexInspector.TrySelect(_map, sectorId, alongX,
                out selection, out reason)) return false;

            RothRaw9TopologyInspector.Report topo = RothRaw9TopologyInspector.Inspect(_map, selection);
            if (topo.HasStaticHazards)
            {
                reason = "Unsafe RAW9 topology: " + topo.Findings[0].Kind;
                return false;
            }

            int sectorIndex = -1;
            for (int i = 0; i < _map.Sectors.Count; i++)
                if (_map.Sectors[i].SectorId == sectorId) { sectorIndex = i; break; }
            if (sectorIndex < 0) { reason = "Sector no longer exists"; return false; }

            RothSector sector = _map.Sectors[sectorIndex];
            if (sector.FacesCount != 4)
            {
                reason = "Experimental backend requires exactly four faces";
                return false;
            }

            // If each face does not lead into the next selected face, moving
            // only four first vertices would break some wall edges.
            for (int i = 0; i < 4; i++)
            {
                RothFace face = _map.Faces[sector.FirstFaceIndex + i];
                RothFace next = _map.Faces[sector.FirstFaceIndex + ((i + 1) % 4)];
                if (face.VertexIndex02 != next.VertexIndex01)
                {
                    reason = "Face edges are not a closed ordered quadrilateral";
                    return false;
                }
            }

            // A single vertex must not be owned by two active raw9 motions.
            foreach (Raw9SelectedMotion active in _raw9SelectedSectorMotions.Values)
                foreach (int index in selection.Indices)
                    if (Array.IndexOf(active.Indices, index) >= 0)
                    {
                        reason = "Selected vertex is already used by an active RAW9 motion";
                        return false;
                    }

            var state = new Raw9SelectedMotion
            {
                AlongX = alongX,
                SectorIndex = sectorIndex,
                Indices = (int[])selection.Indices.Clone(),
                InitialVertices = new RothVertex[4],
                Offset = Vector2Int.zero
            };
            for (int i = 0; i < 4; i++)
                state.InitialVertices[i] = _map.Vertices[state.Indices[i]];
            _raw9SelectedSectorMotions.Add(sectorId, state);
            return true;
        }

        /// <summary>
        /// Apply the requested axis-only RAW offset to the four original vertex
        /// coordinates. Prevalidates vertices and sector objects before commit.
        /// If mesh refresh fails, restores the in-memory state and retries
        /// rebuilding the original mesh. This is an experimental safety gate.
        /// </summary>
        public bool RuntimeSetSelectedRaw9Translation(ushort sectorId, bool alongX,
            Vector2Int offset)
        {
            string reason;
            if (!RuntimeTryPrepareSelectedRaw9Motion(sectorId, alongX, out reason))
            {
                Debug.LogWarning("RAW9 selected-vertex movement rejected: " + reason, this);
                return false;
            }
            Raw9SelectedMotion state = _raw9SelectedSectorMotions[sectorId];
            if ((alongX && offset.y != 0) || (!alongX && offset.x != 0))
                return false;
            if (offset == state.Offset) return true;

            // Guard against integer overflow in the offset and object updates.
            long dx = (long)offset.x - state.Offset.x;
            long dy = (long)offset.y - state.Offset.y;
            if (dx < int.MinValue || dx > int.MaxValue ||
                dy < int.MinValue || dy > int.MaxValue) return false;
            for (int i = 0; i < 4; i++)
            {
                RothVertex original = state.InitialVertices[i];
                long x = (long)original.X + offset.x, y = (long)original.Y + offset.y;
                if (x < short.MinValue || x > short.MaxValue ||
                    y < short.MinValue || y > short.MaxValue) return false;
            }

            foreach (RothObject obj in _map.Objects)
                if (obj.SectorIndex == state.SectorIndex)
                {
                    long x = (long)obj.PosX + dx, y = (long)obj.PosY + dy;
                    if (x < short.MinValue || x > short.MaxValue ||
                        y < short.MinValue || y > short.MaxValue) return false;
                }

            Vector2Int before = state.Offset;
            for (int i = 0; i < 4; i++)
            {
                RothVertex v = state.InitialVertices[i];
                _map.Vertices[state.Indices[i]] =
                    new RothVertex((short)(v.X + offset.x), (short)(v.Y + offset.y));
            }
            foreach (RothObject obj in _map.Objects)
                if (obj.SectorIndex == state.SectorIndex)
                {
                    obj.PosX = (short)(obj.PosX + dx);
                    obj.PosY = (short)(obj.PosY + dy);
                }
            state.Offset = offset;
            try
            {
                RefreshRuntimeGeometry(rebuildObjects: true);
                return true;
            }
            catch (Exception error)
            {
                // Roll back in-memory vertices and objects if Unity rebuilding
                // throws. Keep the motion at its last successful position.
                for (int i = 0; i < 4; i++)
                {
                    RothVertex original = state.InitialVertices[i];
                    _map.Vertices[state.Indices[i]] =
                        new RothVertex((short)(original.X + before.x),
                                       (short)(original.Y + before.y));
                }
                foreach (RothObject obj in _map.Objects)
                    if (obj.SectorIndex == state.SectorIndex)
                    {
                        obj.PosX = (short)(obj.PosX - dx);
                        obj.PosY = (short)(obj.PosY - dy);
                    }
                state.Offset = before;
                try { RefreshRuntimeGeometry(rebuildObjects: true); }
                catch (Exception restoreError) { Debug.LogException(restoreError, this); }
                Debug.LogException(error, this);
                return false;
            }
        }
    }
}
