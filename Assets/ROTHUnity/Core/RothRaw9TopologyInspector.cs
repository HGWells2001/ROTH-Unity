using System;
using System.Collections.Generic;

namespace ROTHUnity.Core
{
    /// <summary>
    /// Read-only RAW9 geometry preflight. Reports shared vertex indices,
    /// sister-face portals and distinct vertices at coincident coordinates.
    /// A clean result does not prove Unity collision or DOS fidelity.
    /// </summary>
    public static class RothRaw9TopologyInspector
    {
        public sealed class Finding
        {
            public string Kind;
            public int FaceIndex;
            public int RelatedIndex;
            public override string ToString()
            {
                return Kind + " face=" + FaceIndex + " related=" + RelatedIndex;
            }
        }

        public sealed class Report
        {
            public ushort SectorId;
            public readonly List<Finding> Findings = new List<Finding>();
            public bool NeedsRuntimeReview { get { return true; } }
            public bool HasStaticHazards { get { return Findings.Count != 0; } }
        }

        private static long CoordinateKey(RothVertex vertex)
        {
            return ((long)(ushort)vertex.X << 16) | (ushort)vertex.Y;
        }

        public static Report Inspect(RothRawMap map, RothRaw9VertexInspector.Selection selected)
        {
            if (map == null || selected == null)
                throw new ArgumentNullException(map == null ? "map" : "selected");
            var report = new Report { SectorId = selected.SectorId };
            int sectorIndex = -1;
            for (int i = 0; i < map.Sectors.Count; i++)
                if (map.Sectors[i].SectorId == selected.SectorId) { sectorIndex = i; break; }
            if (sectorIndex < 0) throw new ArgumentException("Selection sector is not in this RAW map");
            var chosen = new HashSet<int>(selected.Indices);
            var positions = new HashSet<long>();
            foreach (int index in chosen)
            {
                if (index < 0 || index >= map.Vertices.Count)
                    throw new ArgumentException("Selection contains an out-of-bounds vertex index");
                positions.Add(CoordinateKey(map.Vertices[index]));
            }
            var seen = new HashSet<string>();
            Action<string, int, int> add = (kind, face, related) =>
            {
                string key = kind + ":" + face + ":" + related;
                if (seen.Add(key))
                    report.Findings.Add(new Finding { Kind = kind, FaceIndex = face, RelatedIndex = related });
            };
            if (selected.FacesCount != 4)
                add("non_four_face_sector", -1, selected.FacesCount);
            for (int i = 0; i < map.Faces.Count; i++)
            {
                RothFace face = map.Faces[i];
                bool external = face.SectorIndex != sectorIndex;
                int[] endpoints = {face.VertexIndex01, face.VertexIndex02};
                bool touches = false;
                foreach (int index in endpoints)
                {
                    if (index < 0 || index >= map.Vertices.Count) continue;
                    if (chosen.Contains(index))
                    {
                        touches = true;
                        if (external) add("shared_vertex_outside_sector", i, index);
                    }
                    else if (external && positions.Contains(CoordinateKey(map.Vertices[index])))
                        add("coincident_external_vertex", i, index);
                }
                if (external || face.SisterFaceOffset == 0 || face.SisterFaceOffset == 0xFFFF)
                    continue;
                int sister = face.SisterFaceIndex;
                if (sister < 0 || sister >= map.Faces.Count)
                {
                    add("invalid_sister_face", i, sister);
                    continue;
                }
                if (map.Faces[sister].SectorIndex != sectorIndex && touches)
                    add("portal_edge_with_moving_vertex", i, sister);
            }
            return report;
        }
    }
}
