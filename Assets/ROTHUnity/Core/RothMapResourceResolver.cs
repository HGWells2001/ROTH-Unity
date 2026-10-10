using System;
using System.Collections.Generic;
using System.IO;

namespace ROTHUnity.Core
{
    /// <summary>
    /// Chooses the most likely primary DAS for a RAW map by comparing the map's
    /// referenced texture indices with the FAT entries present in candidate DAS files.
    /// This avoids a hard-coded chapter/map table and is deterministic for retail maps.
    /// </summary>
    public static class RothMapResourceResolver
    {
        public sealed class Match
        {
            public string DasPath;
            public int Referenced;
            public int Resolved;
            public float Coverage;
        }

        public static Match SelectBestDas(string rawPath, IEnumerable<string> candidates)
        {
            if (string.IsNullOrWhiteSpace(rawPath) || !File.Exists(rawPath)) return null;
            RothRawMap map = RothRawMapReader.Read(rawPath);
            HashSet<int> refs = CollectTextureReferences(map);
            Match best = null;
            foreach (string path in candidates)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;
                try
                {
                    using (RothDasArchive das = new RothDasArchive(path))
                    {
                        int resolved = 0;
                        foreach (int index in refs)
                        {
                            RothDasFatEntry e = das.GetEntry(index);
                            if (e != null && (e.Size != 0 || (e.Flags1 & 0x20) != 0)) resolved++;
                        }
                        Match m = new Match
                        {
                            DasPath = path,
                            Referenced = refs.Count,
                            Resolved = resolved,
                            Coverage = refs.Count == 0 ? 1f : (float)resolved / refs.Count
                        };
                        if (best == null || m.Coverage > best.Coverage ||
                            (Math.Abs(m.Coverage - best.Coverage) < 0.0001f && m.Resolved > best.Resolved)) best = m;
                    }
                }
                catch { /* Invalid candidates are simply ignored. */ }
            }
            return best;
        }

        public static HashSet<int> CollectTextureReferences(RothRawMap map)
        {
            HashSet<int> refs = new HashSet<int>();
            if (map == null) return refs;
            foreach (RothSector s in map.Sectors)
            {
                Add(refs, s.FloorTextureIndex); Add(refs, s.CeilingTextureIndex);
            }
            foreach (RothMidPlatform p in map.MidPlatforms)
            {
                Add(refs, p.FloorTextureIndex); Add(refs, p.CeilingTextureIndex);
            }
            foreach (RothTextureMapping t in map.TextureMappings)
            {
                Add(refs, t.MidTextureIndex); Add(refs, t.UpperTextureIndex); Add(refs, t.LowerTextureIndex);
            }
            // Sources 0/1 address the map DAS at +4096 / +4352.
            foreach (RothObject o in map.Objects)
            {
                if (o.TextureSource == 0) Add(refs, o.TextureIndex + 4096);
                else if (o.TextureSource == 1) Add(refs, o.TextureIndex + 4352);
            }
            return refs;
        }

        private static void Add(HashSet<int> refs, int index)
        {
            // >= 0x8000 is used for palette/color encodings rather than FAT texture indices.
            if (index >= 0 && index < 0x8000 && index != 0x7FFF) refs.Add(index);
        }
    }
}
