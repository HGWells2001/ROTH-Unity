using System;
using System.Collections.Generic;

namespace ROTHUnity.Core
{
    /// <summary>
    /// Read-only interpretation of the four vertex references used by DOS RAW9
    /// initializer object1+0x22AF8..0x22B8F. Never changes map geometry.
    /// </summary>
    public static class RothRaw9VertexInspector
    {
        public sealed class Selection
        {
            public ushort SectorId;
            public bool AlongX;
            public byte FacesCount;
            public readonly int[] Indices = new int[4];
            public readonly short[] Coordinates = new short[4];
            public readonly ushort[] RawVertexOffsets = new ushort[4];
            public short Minimum => Coordinates[0];
            public short Maximum => Coordinates[3];
        }

        /// <summary>
        /// The DOS code reads the first vertex offset at face offsets +0,+12,+24,+36,
        /// then the coordinate from vertex record +8 (X) or +10 (Y).
        /// Its signed-short bubble sort swaps a coordinate together with its RAW offset.
        /// This method retains that stable ordering, including ties.
        /// </summary>
        public static bool TrySelect(RothRawMap map, ushort sectorId, bool alongX,
            out Selection result, out string reason)
        {
            result = null;
            reason = null;
            if (map == null) { reason = "Null RAW map"; return false; }
            RothSector sector = null;
            int sectorIndex = -1;
            for (int i = 0; i < map.Sectors.Count; i++)
                if (map.Sectors[i].SectorId == sectorId)
                { sector = map.Sectors[i]; sectorIndex = i; break; }
            if (sector == null) { reason = "Unknown sector ID"; return false; }
            if (sector.FacesCount < 4 || sector.FirstFaceIndex < 0 ||
                sector.FirstFaceIndex > map.Faces.Count - 4)
            { reason = "Sector has fewer than four usable consecutive faces"; return false; }
            if (map.Header.FacesOffset + sector.FirstFaceIndex * 12 != sector.FirstFaceOffset)
            { reason = "Face pointer does not match parsed contiguous face table"; return false; }

            Selection selected = new Selection { SectorId = sectorId, AlongX = alongX, FacesCount = sector.FacesCount };
            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < 4; i++)
            {
                RothFace face = map.Faces[sector.FirstFaceIndex + i];
                int index = face.VertexIndex01;
                if (face.SectorIndex != sectorIndex || index < 0 || index >= map.Vertices.Count)
                { reason = "Invalid face-sector or first-vertex reference at slot " + i; return false; }
                if (!seen.Add(index))
                { reason = "Duplicate first-vertex reference at slot " + i; return false; }
                if (8 + index * 12 != face.VertexOffset01)
                { reason = "Vertex offset does not match parsed RAW vertex table at slot " + i; return false; }
                selected.Indices[i] = index;
                selected.RawVertexOffsets[i] = face.VertexOffset01;
                RothVertex vertex = map.Vertices[index];
                selected.Coordinates[i] = alongX ? vertex.X : vertex.Y;
            }
            // Exactly three passes with three adjacent comparisons, matching x86.
            for (int pass = 0; pass < 3; pass++)
                for (int j = 0; j < 3; j++)
                    if (selected.Coordinates[j] > selected.Coordinates[j + 1])
                    {
                        Swap(selected.Coordinates, j);
                        Swap(selected.Indices, j);
                        Swap(selected.RawVertexOffsets, j);
                    }
            result = selected;
            return true;
        }

        private static void Swap<T>(T[] a, int i)
        {
            T value = a[i]; a[i] = a[i + 1]; a[i + 1] = value;
        }
    }
}
