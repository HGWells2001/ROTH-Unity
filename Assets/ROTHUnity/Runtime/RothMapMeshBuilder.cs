using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ROTHUnity.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ROTHUnity.Runtime
{
    /// <summary>
    /// Builds Unity geometry directly from original Realms of the Haunting RAW map data.
    /// Milestone 0.4.2 fixes the global X orientation while preserving corrected UV mapping, platforms and objects.
    /// </summary>
    [ExecuteAlways]
    public sealed class RothMapMeshBuilder : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("Absolute path to an original Realms of the Haunting .RAW file.")]
        public string RawMapPath;
        [Tooltip("Companion original .DAS archive. STUDY1/STUDY2 are in DEMO.DAS in the English data set.")]
        public string DasPath;
        [Tooltip("Optional second DAS archive used by RAW objects with textureSource 2/3.")]
        public string SecondaryDasPath;
        [Tooltip("Original FX22.SFX / FXSCRIPT.SFX archive for positional sound effects.")]
        public string SfxPath;
        public bool UseOriginalTextures = true;

        [Header("Scale")]
        public float CoordinateScale = 1f / 128f;
        public float HeightScale = 1f / 128f;
        [Tooltip("Compatibility switch for the old 0.4.1 coordinate conversion. Leave OFF for original ROTH orientation.")]
        public bool MirrorWorldX = false;
        [Tooltip("World-space tiling divisor used by the first-pass UV reconstruction.")]
        public float TextureWorldScale = 1f;

        [Header("Geometry")]
        public bool BuildFloors = true;
        public bool BuildCeilings = true;
        public bool BuildWalls = true;
        public bool BuildIntermediatePlatforms = true;
        [Tooltip("Render supported original object sprites directly from DAS resources.")]
        public bool BuildOriginalObjects = true;
        [Tooltip("Keep a debug marker only for objects whose DAS format is not decoded yet.")]
        public bool BuildUnsupportedObjectMarkers = true;
        public bool BuildAmbientAudio = true;
        public float ObjectMarkerSize = 0.18f;
        [Tooltip("When enabled, sister faces are treated as portals. Only height differences are filled with wall bands.")]
        public bool RespectPortals = true;
        public bool AddMeshCollider = true;

        [Header("Fallback Materials")]
        public Material FloorMaterial;
        public Material CeilingMaterial;
        public Material WallMaterial;
        public Material ObjectMarkerMaterial;

        [NonSerialized] private RothRawMap _map;
        [NonSerialized] private RothDasTextureFactory _textureFactory;
        [NonSerialized] private RothDasTextureFactory _secondaryTextureFactory;
        [NonSerialized] private RothSfxFactory _sfxFactory;
        [NonSerialized] private readonly Dictionary<ushort,List<AudioSource>> _sfxNodes = new Dictionary<ushort,List<AudioSource>>();

        private const int FallbackFloor = -1;
        private const int FallbackCeiling = -2;
        private const int FallbackWall = -3;
        private const int SkyMaterialKey = -4;
        private const int PaletteKeyBase = 100000;

        public void Rebuild()
        {
            if (string.IsNullOrWhiteSpace(RawMapPath) || !File.Exists(RawMapPath))
                throw new FileNotFoundException("Select a valid original ROTH .RAW map.", RawMapPath);

            DisposeTextureFactory();
            RuntimeClearSectorTranslations();
            _map = RothRawMapReader.Read(RawMapPath);
            bool textured = UseOriginalTextures && !string.IsNullOrWhiteSpace(DasPath) && File.Exists(DasPath);
            if (textured)
                _textureFactory = new RothDasTextureFactory(DasPath, WallMaterial);
            if (BuildOriginalObjects)
            {
                string secondary = SecondaryDasPath;
                if ((string.IsNullOrWhiteSpace(secondary) || !File.Exists(secondary)) && !string.IsNullOrWhiteSpace(DasPath))
                {
                    string candidate = Path.Combine(Path.GetDirectoryName(DasPath) ?? string.Empty, "ADEMO.DAS");
                    if (File.Exists(candidate)) secondary = candidate;
                }
                if (!string.IsNullOrWhiteSpace(secondary) && File.Exists(secondary))
                    _secondaryTextureFactory = new RothDasTextureFactory(secondary, ObjectMarkerMaterial != null ? ObjectMarkerMaterial : WallMaterial);
            }
            if (BuildAmbientAudio && !string.IsNullOrWhiteSpace(SfxPath) && File.Exists(SfxPath))
                _sfxFactory = new RothSfxFactory(SfxPath);

            List<int> materialKeys;
            Mesh mesh = BuildMesh(_map, textured, out materialKeys);

            MeshFilter filter = GetOrAdd<MeshFilter>();
            MeshRenderer renderer = GetOrAdd<MeshRenderer>();
            ReplaceMesh(filter, mesh);
            renderer.sharedMaterials = BuildMaterials(materialKeys, textured);
            RebuildObjects();
            RebuildAmbientAudio();

            if (AddMeshCollider)
            {
                MeshCollider collider = GetOrAdd<MeshCollider>();
                collider.sharedMesh = null;
                collider.sharedMesh = mesh;
            }
            else
            {
                MeshCollider collider = GetComponent<MeshCollider>();
                if (collider != null) collider.sharedMesh = null;
            }
        }

        /// <summary>Rebuilds generated geometry from the in-memory RAW state without rereading the original file.</summary>
        public void RefreshRuntimeGeometry(bool rebuildObjects = false)
        {
            if (_map == null) return;
            bool textured = UseOriginalTextures && _textureFactory != null;
            List<int> materialKeys;
            Mesh mesh = BuildMesh(_map, textured, out materialKeys);
            MeshFilter filter = GetOrAdd<MeshFilter>();
            MeshRenderer renderer = GetOrAdd<MeshRenderer>();
            ReplaceMesh(filter, mesh);
            renderer.sharedMaterials = BuildMaterials(materialKeys, textured);
            MeshCollider collider = GetComponent<MeshCollider>();
            if (AddMeshCollider)
            {
                if (collider == null) collider = gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = null;
                collider.sharedMesh = mesh;
            }
            else if (collider != null) collider.sharedMesh = null;
            if (rebuildObjects) RebuildObjects();
        }

        /// <summary>Sets the RAW height in memory and refreshes walls, portals and colliders.</summary>
        public bool RuntimeSetSectorHeight(ushort sectorId, bool ceiling, short rawHeight)
        {
            RothSector sector = FindSectorById(sectorId);
            if (sector == null) return false;
            if (ceiling) sector.CeilingHeight = rawHeight;
            else sector.FloorHeight = rawHeight;
            RefreshRuntimeGeometry();
            return true;
        }

        public bool RuntimeGetSectorHeight(ushort sectorId, bool ceiling, out short rawHeight)
        {
            RothSector sector = FindSectorById(sectorId);
            rawHeight = sector == null ? (short)0 :
                (ceiling ? sector.CeilingHeight : sector.FloorHeight);
            return sector != null;
        }

        /// <summary>Checks if a character capsule intersects the horizontal footprint of a RAW sector.</summary>
        public bool RuntimePlayerOccupiesSector(ushort sectorId, CharacterController player, float margin = 0.12f)
        {
            if (_map == null || player == null || !player.enabled) return false;
            RothSector sector = FindSectorById(sectorId);
            if (sector == null) return false;
            List<RothVertex> polygon = GetSectorPolygon(_map, sector);
            if (polygon.Count < 3) return false;
            Vector3 local = transform.InverseTransformPoint(player.transform.position);
            float sx = (MirrorWorldX ? -local.x : local.x) / CoordinateScale;
            float sy = local.z / CoordinateScale;
            float radius = (player.radius + margin) / Mathf.Max(0.00001f, Mathf.Abs(CoordinateScale));
            // A capsule occupying an adjacent sector can still obstruct the threshold.
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                float ax = polygon[j].X, ay = polygon[j].Y;
                float bx = polygon[i].X, by = polygon[i].Y;
                if (((ay > sy) != (by > sy)) &&
                    (sx < (bx - ax) * (sy - ay) / (by - ay) + ax)) inside = !inside;
                float dx = bx - ax, dy = by - ay;
                float t = Mathf.Clamp01(((sx - ax) * dx + (sy - ay) * dy) / Mathf.Max(0.00001f, dx * dx + dy * dy));
                float ex = ax + t * dx - sx, ey = ay + t * dy - sy;
                if (ex * ex + ey * ey <= radius * radius) return true;
            }
            return inside;
        }

        /// <summary>
        /// Checks the proposed sector clearance against the player's vertical capsule.
        /// Conservatively refuses any floor/ceiling motion that would overlap the capsule.
        /// </summary>
        public bool RuntimeWouldCrushPlayer(ushort sectorId, CharacterController player,
            short proposedFloor, short proposedCeiling, float safetyMargin = 0.04f)
        {
            if (player == null || !player.enabled || !RuntimePlayerOccupiesSector(sectorId, player))
                return false;
            Vector3 center = player.transform.TransformPoint(player.center);
            Vector3 localCenter = transform.InverseTransformPoint(center);
            float playerBottom = localCenter.y - player.height * 0.5f;
            float playerTop = localCenter.y + player.height * 0.5f;
            float floorY = proposedFloor * HeightScale;
            float ceilingY = proposedCeiling * HeightScale;
            if (floorY > ceilingY) return true;
            return floorY > playerBottom + safetyMargin ||
                   ceilingY < playerTop - safetyMargin;
        }

        /// <summary>True while a CharacterController stands on the specified floor.</summary>
        public bool RuntimePlayerStandsOnFloor(ushort sectorId, CharacterController player,
            short floorHeight, float tolerance = 0.16f)
        {
            if (player == null || !player.enabled || !RuntimePlayerOccupiesSector(sectorId, player))
                return false;
            Vector3 center = transform.InverseTransformPoint(player.transform.TransformPoint(player.center));
            float bottom = center.y - player.height * 0.5f;
            return Mathf.Abs(bottom - floorHeight * HeightScale) <= tolerance;
        }

        // Isolate vertices before translating RAW sectors; keep neighboring vertex indices intact.
        private readonly Dictionary<ushort, Dictionary<int, RothVertex>> _movingSectorVertices =
            new Dictionary<ushort, Dictionary<int, RothVertex>>();
        private readonly Dictionary<ushort, Vector2Int> _movingSectorOffsets =
            new Dictionary<ushort, Vector2Int>();
        private readonly Dictionary<ushort, ushort> _movingSectorTextureFlags =
            new Dictionary<ushort, ushort>();

        /// <summary>RAW9 bits 0..3: floor, ceiling, platform floor and platform ceiling.</summary>
        public void RuntimeSetSectorTextureFollowFlags(ushort id, ushort flags)
        {
            _movingSectorTextureFlags[id] = (ushort)(flags & 15);
        }

        private Vector2Int TextureMotionOffset(RothSector sector, int followBit)
        {
            ushort flags;
            Vector2Int offset;
            return _movingSectorTextureFlags.TryGetValue(sector.SectorId, out flags) &&
                (flags & (1 << followBit)) != 0 &&
                _movingSectorOffsets.TryGetValue(sector.SectorId, out offset)
                    ? offset : Vector2Int.zero;
        }

        public void RuntimeClearSectorTranslations()
        {
            _movingSectorVertices.Clear();
            _movingSectorOffsets.Clear();
            _movingSectorTextureFlags.Clear();
        }

        public bool RuntimeGetSectorTranslation(ushort id, out Vector2Int offset)
        {
            offset = Vector2Int.zero;
            if (_map == null || FindSectorById(id) == null) return false;
            _movingSectorOffsets.TryGetValue(id, out offset);
            return true;
        }

        private bool PrepareSectorTranslation(ushort id)
        {
            if (_map == null) return false;
            if (_movingSectorVertices.ContainsKey(id)) return true;
            RothSector sector = FindSectorById(id);
            if (sector == null || sector.FirstFaceIndex < 0) return false;
            // Validate every vertex before detaching to avoid partial mutation.
            int end = Math.Min(_map.Faces.Count, sector.FirstFaceIndex + sector.FacesCount);
            for (int f = sector.FirstFaceIndex; f < end; f++)
            {
                RothFace candidate = _map.Faces[f];
                if (candidate.VertexIndex01 < 0 || candidate.VertexIndex02 < 0 ||
                    candidate.VertexIndex01 >= _map.Vertices.Count ||
                    candidate.VertexIndex02 >= _map.Vertices.Count) return false;
            }
            var originals = new Dictionary<int, RothVertex>();
            var remapped = new Dictionary<int, int>();
            for (int f = sector.FirstFaceIndex; f < end; f++)
            {
                RothFace face = _map.Faces[f];
                int[] indices = {face.VertexIndex01, face.VertexIndex02};
                for (int a = 0; a < 2; a++)
                {
                    int idx = indices[a];
                    if (idx < 0 || idx >= _map.Vertices.Count) return false;
                    int clone;
                    if (!remapped.TryGetValue(idx, out clone))
                    {
                        clone = _map.Vertices.Count;
                        originals.Add(clone, _map.Vertices[idx]);
                        _map.Vertices.Add(_map.Vertices[idx]);
                        remapped.Add(idx, clone);
                    }
                    if (a == 0) face.VertexIndex01 = clone;
                    else face.VertexIndex02 = clone;
                }
            }
            _movingSectorVertices.Add(id, originals);
            _movingSectorOffsets[id] = Vector2Int.zero;
            return true;
        }

        public bool RuntimeSetSectorTranslation(ushort sectorId, Vector2Int rawOffset)
        {
            if (!PrepareSectorTranslation(sectorId)) return false;
            var original = _movingSectorVertices[sectorId];
            // Validate all vertices and sector objects before mutating anything.
            foreach (var entry in original)
            {
                RothVertex v = entry.Value;
                long x = (long)v.X + rawOffset.x, y = (long)v.Y + rawOffset.y;
                if (x < short.MinValue || x > short.MaxValue ||
                    y < short.MinValue || y > short.MaxValue) return false;
            }
            Vector2Int previous = _movingSectorOffsets[sectorId];
            Vector2Int delta = rawOffset - previous;
            int sectorIndex = _map.Sectors.IndexOf(FindSectorById(sectorId));
            foreach (RothObject obj in _map.Objects)
            {
                if (obj.SectorIndex != sectorIndex) continue;
                long x = (long)obj.PosX + delta.x, y = (long)obj.PosY + delta.y;
                if (x < short.MinValue || x > short.MaxValue ||
                    y < short.MinValue || y > short.MaxValue) return false;
            }
            foreach (var entry in original)
            {
                RothVertex v = entry.Value;
                _map.Vertices[entry.Key] = new RothVertex(
                    (short)(v.X + rawOffset.x), (short)(v.Y + rawOffset.y));
            }
            _movingSectorOffsets[sectorId] = rawOffset;
            foreach (RothObject obj in _map.Objects)
                if (obj.SectorIndex == sectorIndex)
                {
                    obj.PosX = (short)(obj.PosX + delta.x);
                    obj.PosY = (short)(obj.PosY + delta.y);
                }
            RefreshRuntimeGeometry(rebuildObjects: delta != Vector2Int.zero);
            return true;
        }

        public bool RuntimeChangeFloorTexture(ushort sectorId, ushort textureIndex, ushort packedShift, ushort flags)
        {
            if (_map == null) return false;
            RothSector sector = FindSectorById(sectorId);
            if (sector == null) return false;
            sector.FloorTextureIndex = textureIndex;
            sector.FloorTextureShiftX = (byte)(packedShift & 0xFF);
            sector.FloorTextureShiftY = (byte)((packedShift >> 8) & 0xFF);
            // Command flags 9/10 correspond to the original floor scale A/B bits.
            sector.SectorFlags = (byte)(sector.SectorFlags & ~((1 << 4) | (1 << 5)));
            if ((flags & (1 << 8)) != 0) sector.SectorFlags |= (1 << 4);
            if ((flags & (1 << 9)) != 0) sector.SectorFlags |= (1 << 5);
            RefreshRuntimeGeometry(false);
            return true;
        }

        public bool RuntimeChangeFaceTextureSimple(ushort faceId, ushort textureIndex)
        {
            RothTextureMapping mapping = FindTextureMappingByFaceId(faceId);
            if (mapping == null) return false;
            mapping.MidTextureIndex = textureIndex;
            RefreshRuntimeGeometry(false);
            return true;
        }

        public bool RuntimeChangeFaceTextureAdvanced(ushort faceId, ushort midTexture, ushort packedShift, ushort flags, ushort upperTexture, ushort lowerTexture)
        {
            RothTextureMapping mapping = FindTextureMappingByFaceId(faceId);
            if (mapping == null) return false;
            mapping.MidTextureIndex = midTexture;
            mapping.UpperTextureIndex = upperTexture;
            mapping.LowerTextureIndex = lowerTexture;
            // Command flags 9..16 map directly to the eight texture flag bits documented by ROTH.
            mapping.TextureFlags = (ushort)((flags >> 8) & 0xFF);
            mapping.HasAdditionalMetadata = true;
            mapping.ShiftTextureX = unchecked((sbyte)(packedShift & 0xFF));
            mapping.ShiftTextureY = unchecked((sbyte)((packedShift >> 8) & 0xFF));
            RefreshRuntimeGeometry(false);
            return true;
        }

        public bool RuntimeChangeObjectTexture(ushort objectId, ushort textureIndex)
        {
            if (_map == null || textureIndex > 255) return false;
            bool changed = false;
            for (int i = 0; i < _map.Objects.Count; i++)
            {
                RothObject o = _map.Objects[i];
                if (o.ObjectId != objectId) continue;
                o.TextureIndex = (byte)textureIndex;
                changed = true;
            }
            if (changed) RebuildObjects();
            return changed;
        }

        public bool RuntimeChangeObjectHeight(ushort objectId, short height)
        {
            if (_map == null) return false;
            bool changed = false;
            for (int i = 0; i < _map.Objects.Count; i++)
            {
                RothObject o = _map.Objects[i];
                if (o.ObjectId != objectId) continue;
                o.PosZ = height;
                changed = true;
            }
            if (changed) RebuildObjects();
            return changed;
        }

        public bool RuntimeRotateObject(ushort objectId, bool counterClockwise, byte amount)
        {
            if (_map == null) return false;
            bool changed = false;
            int delta = Math.Max(1, (int)amount);
            for (int i = 0; i < _map.Objects.Count; i++)
            {
                RothObject o = _map.Objects[i];
                if (o.ObjectId != objectId) continue;
                int r = o.Rotation + (counterClockwise ? -delta : delta);
                o.Rotation = unchecked((byte)r);
                changed = true;
            }
            if (changed) RebuildObjects();
            return changed;
        }

        private RothSector FindSectorById(ushort sectorId)
        {
            if (_map == null) return null;
            for (int i = 0; i < _map.Sectors.Count; i++) if (_map.Sectors[i].SectorId == sectorId) return _map.Sectors[i];
            return null;
        }

        private RothTextureMapping FindTextureMappingByFaceId(ushort faceId)
        {
            if (_map == null) return null;
            for (int i = 0; i < _map.TextureMappings.Count; i++)
            {
                RothTextureMapping m = _map.TextureMappings[i];
                if (m.HasAdditionalMetadata && m.FaceId == faceId) return m;
            }
            return null;
        }

        public void GetOriginalPlayerSetup(out Vector3 position, out float yawDegrees, out float playerHeight, out float moveSpeed)
        {
            if (_map == null)
            {
                if (string.IsNullOrWhiteSpace(RawMapPath) || !File.Exists(RawMapPath))
                    throw new FileNotFoundException("Select a valid original ROTH .RAW map.", RawMapPath);
                _map = RothRawMapReader.Read(RawMapPath);
            }
            RothMapMetadata m = _map.Metadata;
            float x = (MirrorWorldX ? -m.InitPosX : m.InitPosX) * CoordinateScale;
            float y = m.InitPosZ * HeightScale;
            float z = m.InitPosY * CoordinateScale;
            position = new Vector3(x, y + 0.03f, z);
            // Inverse of the original editor conversion: rotation=((degrees+180)*128)/90.
            yawDegrees = (m.Rotation * 90f / 128f) - 180f;
            if (MirrorWorldX) yawDegrees = -yawDegrees;
            playerHeight = Mathf.Max(1.2f, m.PlayerHeight * 2f * HeightScale);
            moveSpeed = m.MoveSpeed > 0 ? m.MoveSpeed : 5f;
        }

        public void ClearGeneratedMesh()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                Mesh old = filter.sharedMesh;
                filter.sharedMesh = null;
                DestroyGenerated(old);
            }
            MeshCollider collider = GetComponent<MeshCollider>();
            if (collider != null) collider.sharedMesh = null;
            ClearGeneratedObjects();
            ClearGeneratedAudio();
            DisposeTextureFactory();
        }

        private Mesh BuildMesh(RothRawMap map, bool textured, out List<int> materialKeys)
        {
            var vertices = new List<Vector3>(map.Faces.Count * 8);
            var uvs = new List<Vector2>(map.Faces.Count * 8);
            var groups = new Dictionary<int, List<int>>();

            for (int sectorIndex = 0; sectorIndex < map.Sectors.Count; sectorIndex++)
            {
                if (BuildFloors || BuildCeilings)
                    BuildSectorCaps(map, sectorIndex, textured, vertices, uvs, groups);
                if (BuildIntermediatePlatforms)
                    BuildPlatformCaps(map, sectorIndex, textured, vertices, uvs, groups);
            }

            if (BuildWalls)
                for (int faceIndex = 0; faceIndex < map.Faces.Count; faceIndex++)
                    BuildFaceWalls(map, faceIndex, textured, vertices, uvs, groups);

            materialKeys = groups.Keys.OrderBy(k => k).ToList();
            Mesh mesh = new Mesh();
            mesh.name = "ROTH_" + Path.GetFileNameWithoutExtension(RawMapPath);
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = materialKeys.Count;
            for (int i = 0; i < materialKeys.Count; i++)
                mesh.SetTriangles(groups[materialKeys[i]], i, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void BuildSectorCaps(RothRawMap map, int sectorIndex, bool textured,
            List<Vector3> vertices, List<Vector2> uvs, Dictionary<int, List<int>> groups)
        {
            RothSector sector = map.Sectors[sectorIndex];
            if (sector.FirstFaceIndex < 0 || sector.FacesCount < 3) return;
            List<RothVertex> polygon = GetSectorPolygon(map, sector);
            if (polygon.Count < 3) return;
            List<int> triangles = TriangulatePolygon(polygon);
            if (triangles.Count < 3) return;

            if (BuildFloors)
            {
                int key = textured ? SectorMaterialKey(sector.FloorTextureIndex, FallbackFloor) : FallbackFloor;
                List<int> dest = GetGroup(groups, key);
                int baseIndex = vertices.Count;
                for (int i = 0; i < polygon.Count; i++)
                {
                    Vector3 p = ConvertPoint(polygon[i], sector.FloorHeight);
                    vertices.Add(p);
                    uvs.Add(SectorUv(polygon[i], polygon, sector.FloorTextureIndex, sector.FloorTextureShiftX, sector.FloorTextureShiftY, sector.SectorFlags, sector.AdditionalSectorFlags, false, textured, TextureMotionOffset(sector, 0)));
                }
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    if (MirrorWorldX) AddTriangle(dest, baseIndex + triangles[i], baseIndex + triangles[i + 1], baseIndex + triangles[i + 2]);
                    else AddTriangle(dest, baseIndex + triangles[i], baseIndex + triangles[i + 2], baseIndex + triangles[i + 1]);
                }
            }

            if (BuildCeilings)
            {
                int key = textured ? (IsSkyTexture(sector.CeilingTextureIndex) ? SkyMaterialKey : SectorMaterialKey(sector.CeilingTextureIndex, FallbackCeiling)) : FallbackCeiling;
                List<int> dest = GetGroup(groups, key);
                int baseIndex = vertices.Count;
                for (int i = 0; i < polygon.Count; i++)
                {
                    Vector3 p = ConvertPoint(polygon[i], sector.CeilingHeight);
                    vertices.Add(p);
                    uvs.Add(SectorUv(polygon[i], polygon, sector.CeilingTextureIndex, sector.CeilingTextureShiftX, sector.CeilingTextureShiftY, sector.SectorFlags, sector.AdditionalSectorFlags, true, textured, TextureMotionOffset(sector, 1)));
                }
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    if (MirrorWorldX) AddTriangle(dest, baseIndex + triangles[i], baseIndex + triangles[i + 2], baseIndex + triangles[i + 1]);
                    else AddTriangle(dest, baseIndex + triangles[i], baseIndex + triangles[i + 1], baseIndex + triangles[i + 2]);
                }
            }
        }

        private void BuildFaceWalls(RothRawMap map, int faceIndex, bool textured,
            List<Vector3> vertices, List<Vector2> uvs, Dictionary<int, List<int>> groups)
        {
            RothFace face = map.Faces[faceIndex];
            if (face.VertexIndex01 < 0 || face.VertexIndex02 < 0 || face.SectorIndex < 0) return;
            RothSector sector = map.Sectors[face.SectorIndex];
            RothVertex a = map.Vertices[face.VertexIndex01];
            RothVertex b = map.Vertices[face.VertexIndex02];
            RothTextureMapping mapping = face.TextureMappingIndex >= 0 && face.TextureMappingIndex < map.TextureMappings.Count
                ? map.TextureMappings[face.TextureMappingIndex] : null;

            int mid = textured && mapping != null ? (IsSkyTexture(mapping.MidTextureIndex) ? SkyMaterialKey : FaceMaterialKey(mapping.MidTextureIndex, FallbackWall)) : FallbackWall;
            if (!RespectPortals || face.SisterFaceIndex < 0 || face.SisterFaceIndex >= map.Faces.Count)
            {
                AddWallQuad(vertices, uvs, GetGroup(groups, mid), a, b, sector.FloorHeight, sector.CeilingHeight, mapping, mapping != null ? mapping.MidTextureIndex : (ushort)0, textured);
                return;
            }

            RothFace sister = map.Faces[face.SisterFaceIndex];
            if (sister.SectorIndex < 0 || sister.SectorIndex >= map.Sectors.Count)
            {
                AddWallQuad(vertices, uvs, GetGroup(groups, mid), a, b, sector.FloorHeight, sector.CeilingHeight, mapping, mapping != null ? mapping.MidTextureIndex : (ushort)0, textured);
                return;
            }

            RothSector neighbour = map.Sectors[sister.SectorIndex];

            // Transparent portal faces retain their middle texture across the open overlap.
            // This is used for grilles, translucent/masked surfaces and similar ROTH faces.
            if (mapping != null && (mapping.TextureFlags & (1 << 0)) != 0)
            {
                short portalBottom = (short)Math.Max(sector.FloorHeight, neighbour.FloorHeight);
                short portalTop = (short)Math.Min(sector.CeilingHeight, neighbour.CeilingHeight);
                if (portalTop > portalBottom)
                    AddWallQuad(vertices, uvs, GetGroup(groups, mid), a, b, portalBottom, portalTop, mapping, mapping.MidTextureIndex, textured);
            }

            short lowerTop = (short)Math.Min(sector.CeilingHeight, neighbour.FloorHeight);
            if (lowerTop > sector.FloorHeight)
            {
                int key = textured && mapping != null ? (IsSkyTexture(mapping.LowerTextureIndex) ? SkyMaterialKey : FaceMaterialKey(mapping.LowerTextureIndex, mid)) : FallbackWall;
                AddWallQuad(vertices, uvs, GetGroup(groups, key), a, b, sector.FloorHeight, lowerTop, mapping, mapping != null ? mapping.LowerTextureIndex : (ushort)0, textured);
            }

            short upperBottom = (short)Math.Max(sector.FloorHeight, neighbour.CeilingHeight);
            if (sector.CeilingHeight > upperBottom)
            {
                int key = textured && mapping != null ? (IsSkyTexture(mapping.UpperTextureIndex) ? SkyMaterialKey : FaceMaterialKey(mapping.UpperTextureIndex, mid)) : FallbackWall;
                AddWallQuad(vertices, uvs, GetGroup(groups, key), a, b, upperBottom, sector.CeilingHeight, mapping, mapping != null ? mapping.UpperTextureIndex : (ushort)0, textured);
            }
        }

        private void AddWallQuad(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles,
            RothVertex a, RothVertex b, short bottom, short top, RothTextureMapping mapping, ushort textureIndex, bool textured)
        {
            if (top <= bottom) return;

            // Match the original ROTH face orientation used by the reference implementation:
            // v1-bottom, v1-top, v2-top, v2-bottom.  Keeping this ordering is important
            // because ROTH wall UV axes are height (U) and face length (V), not the
            // conventional Unity quad ordering used by milestone 0.4.
            Vector3 p0 = ConvertPoint(a, bottom);
            Vector3 p1 = ConvertPoint(a, top);
            Vector3 p2 = ConvertPoint(b, top);
            Vector3 p3 = ConvertPoint(b, bottom);
            int start = vertices.Count;
            vertices.Add(p0); vertices.Add(p1); vertices.Add(p2); vertices.Add(p3);

            float faceLengthRaw = Mathf.Sqrt((b.X-a.X)*(b.X-a.X) + (b.Y-a.Y)*(b.Y-a.Y));
            float heightRaw = Mathf.Abs(top-bottom);
            float textureWidth = 128f, textureHeight = 128f;
            if (textured && _textureFactory != null)
            {
                int w,h;
                if (_textureFactory.TryGetTextureSize(textureIndex, out w, out h))
                {
                    // ROTH's face mapper intentionally treats image dimensions transposed.
                    textureWidth = h;
                    textureHeight = w;
                }
            }

            ushort flags = mapping != null ? mapping.TextureFlags : (ushort)0;
            bool halfPixel = (flags & (1<<5)) != 0;
            if (halfPixel) { textureWidth *= 0.5f; textureHeight *= 0.5f; }
            bool imageFit = (flags & (1<<2)) != 0;

            float scaleU = imageFit ? 1f : heightRaw / Mathf.Max(1f, 2f * textureHeight);
            float scaleV = imageFit ? 1f : faceLengthRaw / Mathf.Max(1f, 2f * textureWidth);
            float offsetU = 0f, offsetV = 0f;

            // Type bit 7 appends per-face texture shifts.  X shifts run along the wall
            // (V in the original mapper), Y shifts run vertically (U).
            if (mapping != null && mapping.HasAdditionalMetadata && !imageFit)
            {
                if (mapping.ShiftTextureX != 0)
                {
                    float shift = mapping.ShiftTextureX;
                    if (halfPixel) shift *= 0.5f;
                    offsetV = (shift > 0f ? shift : shift + 256f) / Mathf.Max(1f, textureWidth);
                }
                if (mapping.ShiftTextureY != 0)
                {
                    float shift = mapping.ShiftTextureY;
                    if (halfPixel) shift *= 0.5f;
                    offsetU = (shift > 0f ? shift : shift + 256f) / Mathf.Max(1f, textureHeight);
                }
            }

            if ((flags & (1<<7)) != 0) // PIN_BOTTOM
            {
                scaleU *= -1f;
                offsetU *= -1f;
            }
            if ((flags & (1<<1)) != 0) // FLIP_X in the original format flips wall V
            {
                scaleV *= -1f;
                offsetV *= -1f;
            }

            // Original base UVs are (1,0), (0,0), (0,1), (1,1).
            uvs.Add(new Vector2(scaleU + offsetU, offsetV));
            uvs.Add(new Vector2(offsetU, offsetV));
            uvs.Add(new Vector2(offsetU, scaleV + offsetV));
            uvs.Add(new Vector2(scaleU + offsetU, scaleV + offsetV));

            if (MirrorWorldX)
            {
                AddTriangle(triangles, start, start + 2, start + 1);
                AddTriangle(triangles, start, start + 3, start + 2);
            }
            else
            {
                AddTriangle(triangles, start, start + 1, start + 2);
                AddTriangle(triangles, start, start + 2, start + 3);
            }
        }

        private Material[] BuildMaterials(List<int> keys, bool textured)
        {
            var mats = new Material[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                int key = keys[i];
                if (key == FallbackFloor) mats[i] = FloorMaterial != null ? FloorMaterial : WallMaterial;
                else if (key == FallbackCeiling) mats[i] = CeilingMaterial != null ? CeilingMaterial : WallMaterial;
                else if (key == FallbackWall) mats[i] = WallMaterial;
                else if (key == SkyMaterialKey) mats[i] = CreateSkyMaterial();
                else if (textured && _textureFactory != null && key >= PaletteKeyBase) mats[i] = _textureFactory.GetPaletteMaterial(key - PaletteKeyBase);
                else if (textured && _textureFactory != null) mats[i] = _textureFactory.GetMaterial(key);
                if (mats[i] == null) mats[i] = WallMaterial;
            }
            return mats;
        }

        private bool IsSkyTexture(ushort index)
        { return _textureFactory != null && _textureFactory.Header != null && index == _textureFactory.Header.SkyIndex; }

        private static Material CreateSkyMaterial()
        {
            Shader shader=Shader.Find("Sprites/Default");
            if(shader==null) shader=Shader.Find("Unlit/Color");
            Material m=shader!=null?new Material(shader):null;
            if(m!=null){m.name="ROTH Sky Transparent";m.color=new Color(0f,0f,0f,0f);m.renderQueue=3000;}
            return m;
        }

        private static int SectorMaterialKey(ushort value, int fallback)
        {
            // Sector floor/ceiling values 0xFF00..0xFFFF encode palette colours.
            if (value >= 65280) return PaletteKeyBase + (value - 65280);
            return value;
        }

        private static int FaceMaterialKey(ushort value, int fallback)
        {
            // Wall faces use 0x8000..0x80FF as direct palette colours. 0xFFFF is also palette 255.
            if (value == 65535) return PaletteKeyBase + 255;
            if (value >= 32768 && value <= 33023) return PaletteKeyBase + (value - 32768);
            if (value >= 32768) return fallback;
            return value;
        }

        private Vector2 SectorUv(RothVertex point, List<RothVertex> polygon, ushort textureIndex, byte shiftX, byte shiftY, byte sectorFlags, ushort additionalFlags, bool ceiling, bool textured, Vector2Int moveTextureOffset = default(Vector2Int))
        {
            // Evaluate the texture grid in the same coordinate space used by geometry.
            // 0.4.1 always mirrored X; 0.4.2 defaults to the original ROTH orientation.
            int minX = int.MaxValue, minY = int.MaxValue;
            for (int i=0;i<polygon.Count;i++)
            {
                int px = MirrorWorldX ? -(polygon[i].X - moveTextureOffset.x) : polygon[i].X - moveTextureOffset.x;
                minX=Math.Min(minX, px);
                minY=Math.Min(minY, polygon[i].Y - moveTextureOffset.y);
            }
            float texW=128f, texH=128f;
            if (textured && _textureFactory != null) { int w,h; if (_textureFactory.TryGetTextureSize(textureIndex,out w,out h)) { texW=w; texH=h; } }
            if (texW == 256f) { texW *= 0.5f; texH *= 0.5f; }
            int aBit = ceiling ? 2 : 4, bBit = ceiling ? 3 : 5;
            int a=(sectorFlags>>aBit)&1, b=(sectorFlags>>bBit)&1;
            float sizeFactor, shiftFactor;
            if (a==0 && b==0) { sizeFactor=1f; shiftFactor=0.5f; }
            else if (a==1 && b==0) { sizeFactor=2f; shiftFactor=1f; }
            else if (a==0 && b==1) { sizeFactor=4f; shiftFactor=2f; }
            else { sizeFactor=8f; shiftFactor=4f; }
            float gridX = Mod(minX,1024), gridY = Mod(minY,1024);
            float mappedX = MirrorWorldX ? -(point.X - moveTextureOffset.x) : point.X - moveTextureOffset.x;
            float u = (mappedX-minX + gridX + shiftX*shiftFactor)/(texW*sizeFactor);
            float v = (point.Y-moveTextureOffset.y-minY + gridY - shiftY*shiftFactor)/(texH*sizeFactor);
            int flipX = ceiling ? 10 : 8, flipY = ceiling ? 11 : 9;
            if ((additionalFlags & (1<<flipX)) != 0) u=-u;
            if ((additionalFlags & (1<<flipY)) != 0) v=-v;
            return new Vector2(u,v);
        }

        private static int Mod(int value, int modulus)
        { int r=value%modulus; return r<0 ? r+modulus : r; }

        private void BuildPlatformCaps(RothRawMap map, int sectorIndex, bool textured, List<Vector3> vertices, List<Vector2> uvs, Dictionary<int,List<int>> groups)
        {
            RothSector sector=map.Sectors[sectorIndex];
            if (sector.IntermediateFloorIndex < 0 || sector.IntermediateFloorIndex >= map.MidPlatforms.Count) return;
            List<RothVertex> polygon=GetSectorPolygon(map,sector);
            if (polygon.Count<3) return;
            List<int> triangles=TriangulatePolygon(polygon);
            if (triangles.Count<3) return;
            RothMidPlatform platform=map.MidPlatforms[sector.IntermediateFloorIndex];
            AddPlatformSurface(polygon, triangles, platform.FloorHeight, platform.FloorTextureIndex, platform.FloorTextureShiftX, platform.FloorTextureShiftY, platform.FloorTextureScale, false, textured, vertices, uvs, groups, TextureMotionOffset(sector, 2));
            AddPlatformSurface(polygon, triangles, platform.CeilingHeight, platform.CeilingTextureIndex, platform.CeilingTextureShiftX, platform.CeilingTextureShiftY, platform.FloorTextureScale, true, textured, vertices, uvs, groups, TextureMotionOffset(sector, 3));
        }

        private void AddPlatformSurface(List<RothVertex> polygon,List<int> triangles,short height,ushort textureIndex,byte sx,byte sy,byte scaleFlags,bool ceiling,bool textured,List<Vector3> vertices,List<Vector2> uvs,Dictionary<int,List<int>> groups, Vector2Int textureOffset)
        {
            int key=textured ? (ceiling && IsSkyTexture(textureIndex) ? SkyMaterialKey : SectorMaterialKey(textureIndex, ceiling?FallbackCeiling:FallbackFloor)) : (ceiling?FallbackCeiling:FallbackFloor);
            List<int> dest=GetGroup(groups,key); int baseIndex=vertices.Count;
            byte flags=(byte)(scaleFlags & 0x3C);
            for(int i=0;i<polygon.Count;i++) { vertices.Add(ConvertPoint(polygon[i],height)); uvs.Add(SectorUv(polygon[i],polygon,textureIndex,sx,sy,flags,0,ceiling,textured,textureOffset)); }
            for(int i=0;i<triangles.Count;i+=3)
            {
                bool reverse = ceiling ? MirrorWorldX : !MirrorWorldX;
                if (reverse) AddTriangle(dest,baseIndex+triangles[i],baseIndex+triangles[i+2],baseIndex+triangles[i+1]);
                else AddTriangle(dest,baseIndex+triangles[i],baseIndex+triangles[i+1],baseIndex+triangles[i+2]);
            }
        }

        private void RebuildObjects()
        {
            ClearGeneratedObjects();
            if (_map == null || _map.Objects.Count == 0) return;

            GameObject root = new GameObject("ROTH Objects");
            root.transform.SetParent(transform, false);

            int rendered = 0, unsupported = 0;
            for (int i = 0; i < _map.Objects.Count; i++)
            {
                RothObject o = _map.Objects[i];
                if (BuildOriginalObjects && TryBuildOriginalObject(root.transform, i, o))
                {
                    rendered++;
                    continue;
                }
                unsupported++;
                if (BuildUnsupportedObjectMarkers) BuildObjectMarker(root.transform, i, o);
            }
            root.name = string.Format("ROTH Objects (rendered {0}, unsupported {1})", rendered, unsupported);
        }

        private bool TryBuildOriginalObject(Transform parent, int objectIndex, RothObject o)
        {
            RothDasTextureFactory factory;
            int dasIndex;
            if (o.TextureSource == 0) { factory = _textureFactory; dasIndex = o.TextureIndex + 4096; }
            else if (o.TextureSource == 1) { factory = _textureFactory; dasIndex = o.TextureIndex + 4096 + 256; }
            else if (o.TextureSource == 2) { factory = _secondaryTextureFactory; dasIndex = o.TextureIndex; }
            else if (o.TextureSource == 3) { factory = _secondaryTextureFactory; dasIndex = o.TextureIndex + 256; }
            else return false;

            if (factory == null) return false;

            RothDasDirectionalViews directional = factory.GetDirectionalViews(dasIndex);
            if (directional != null)
                return BuildDirectionalObject(parent, objectIndex, o, factory, dasIndex, directional);

            RothDasObjectData objectData = factory.GetObjectData(dasIndex);
            if (objectData != null)
                return BuildThreeDimensionalObject(parent, objectIndex, o, factory, objectData);

            RothDasImage image = factory.GetImage(dasIndex);
            if (image == null) return false;
            Texture2D texture = factory.GetTexture(dasIndex);
            Material material = factory.GetMaterial(dasIndex);
            if (texture == null || material == null) return false;

            float halfWidth = image.Height * CoordinateScale;
            float halfHeight = image.Width * CoordinateScale;
            if (image.HalfSize) { halfWidth *= 0.5f; halfHeight *= 0.5f; }
            float lowY = 0f, highY = halfHeight * 2f;
            if (image.DrawDownward) { lowY -= highY; highY = 0f; }

            var verts = new Vector3[] {
                new Vector3( halfWidth, lowY, 0f),
                new Vector3(-halfWidth, lowY, 0f),
                new Vector3( halfWidth, highY, 0f),
                new Vector3(-halfWidth, highY, 0f)
            };
            Vector2[] uv;
            if ((o.Flags & (1 << 4)) != 0)
                uv = new [] { new Vector2(1,0), new Vector2(1,1), new Vector2(0,0), new Vector2(0,1) };
            else
                uv = new [] { new Vector2(0,0), new Vector2(1,0), new Vector2(0,1), new Vector2(1,1) };
            int[] tris = { 0,2,1, 2,3,1 };

            Mesh mesh = new Mesh();
            mesh.name = "ROTH Object " + dasIndex;
            mesh.vertices = verts; mesh.uv = uv; mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();

            GameObject go = new GameObject(string.Format("Obj {0:D4} ID={1} DAS={2}", objectIndex, o.ObjectId, dasIndex));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = ConvertObjectPosition(o);
            MeshFilter mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = material;
            AddObjectInteraction(go,objectIndex,o,mesh);

            bool fixedRotation = (o.RenderType & (1 << 7)) != 0;
            if (fixedRotation)
                go.transform.localRotation = Quaternion.Euler(0f, 180f - o.Rotation * (360f / 256f), 0f);

            if ((image.ImageType & 0x01) != 0)
            {
                float fps; Texture2D[] frames = factory.GetAnimationTextures(dasIndex, out fps);
                if (frames != null && frames.Length > 1)
                {
                    RothAnimatedBillboard anim = go.AddComponent<RothAnimatedBillboard>();
                    anim.Initialize(mr, frames, fps, material, !fixedRotation);
                }
                else if (!fixedRotation) go.AddComponent<RothBillboard>();
            }
            else if (!fixedRotation)
                go.AddComponent<RothBillboard>();

            return true;
        }

        private bool BuildDirectionalObject(Transform parent, int objectIndex, RothObject o, RothDasTextureFactory factory, int dasIndex, RothDasDirectionalViews views)
        {
            Texture2D first = null; Material template = null;
            for (int d=0; d<8 && first==null; d++)
            {
                if (views.UsesImagePack)
                {
                    int sub = views.PackSubImageIndices[d];
                    if (sub >= 0) { first = factory.GetImagePackTexture(dasIndex, sub); template = factory.GetImagePackMaterial(dasIndex, sub); }
                }
                else if (views.ResourceIndices[d] >= 0)
                {
                    first = factory.GetTexture(views.ResourceIndices[d]);
                    template = factory.GetMaterial(views.ResourceIndices[d]);
                }
            }
            if (first == null || template == null) return false;

            float halfWidth = first.height * CoordinateScale;
            float height = first.width * CoordinateScale * 2f;
            var verts = new [] {
                new Vector3( halfWidth, 0f, 0f), new Vector3(-halfWidth, 0f, 0f),
                new Vector3( halfWidth, height, 0f), new Vector3(-halfWidth, height, 0f) };
            var uv = new [] { new Vector2(0,0), new Vector2(1,0), new Vector2(0,1), new Vector2(1,1) };
            int[] tris = { 0,2,1, 2,3,1 };
            Mesh mesh = new Mesh(); mesh.name = "ROTH Directional " + dasIndex;
            mesh.vertices=verts; mesh.uv=uv; mesh.triangles=tris; mesh.RecalculateNormals(); mesh.RecalculateBounds();

            GameObject go = new GameObject(string.Format("Obj {0:D4} ID={1} DIR={2}", objectIndex, o.ObjectId, dasIndex));
            go.transform.SetParent(parent,false); go.transform.localPosition=ConvertObjectPosition(o);
            MeshFilter mf=go.AddComponent<MeshFilter>(); mf.sharedMesh=mesh;
            MeshRenderer mr=go.AddComponent<MeshRenderer>(); mr.sharedMaterial=template;
            AddObjectInteraction(go,objectIndex,o,mesh);
            RothDirectionalBillboard billboard=go.AddComponent<RothDirectionalBillboard>();
            billboard.Initialize(mr,factory,views,o.Rotation,template);
            return true;
        }

        private bool BuildThreeDimensionalObject(Transform parent, int objectIndex, RothObject o,
            RothDasTextureFactory factory, RothDasObjectData data)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var groups = new Dictionary<long, List<int>>();

            for (int f = 0; f < data.Faces.Count; f++)
            {
                RothDasObjectFace face = data.Faces[f];
                int uniqueCount = Math.Max(0, face.Edges.Length - 1); // final edge closes the polygon
                if (uniqueCount != 3 && uniqueCount != 4) continue;
                int[] source = new int[uniqueCount];
                bool valid = true;
                for (int e=0;e<uniqueCount;e++)
                {
                    source[e] = face.Edges[e] >> 4;
                    if (source[e] < 0 || source[e] >= data.Vertices.Count) { valid=false; break; }
                }
                if (!valid) continue;

                long key = ((long)face.TextureIndex << 32) | face.SubTextureIndex;
                List<int> tris;
                if (!groups.TryGetValue(key, out tris)) { tris = new List<int>(); groups[key] = tris; }
                int baseIndex=vertices.Count;
                for (int e=0;e<uniqueCount;e++)
                {
                    RothDasObjectVertex v=data.Vertices[source[e]];
                    float x=(MirrorWorldX ? -v.X : v.X)*CoordinateScale;
                    vertices.Add(new Vector3(x,v.Y*HeightScale,v.Z*CoordinateScale));
                }
                if (uniqueCount==3)
                {
                    uvs.Add(new Vector2(0,0)); uvs.Add(new Vector2(0,1)); uvs.Add(new Vector2(1,1));
                    if ((face.RenderFlag1 & (1<<1)) != 0) for(int u=baseIndex;u<baseIndex+3;u++){ Vector2 t=uvs[u]; t.y=1f-t.y; uvs[u]=t; }
                    if (MirrorWorldX) AddTriangle(tris,baseIndex,baseIndex+1,baseIndex+2);
                    else AddTriangle(tris,baseIndex,baseIndex+2,baseIndex+1);
                }
                else
                {
                    uvs.Add(new Vector2(0,0)); uvs.Add(new Vector2(0,1)); uvs.Add(new Vector2(1,1)); uvs.Add(new Vector2(1,0));
                    if ((face.RenderFlag1 & (1<<1)) != 0) for(int u=baseIndex;u<baseIndex+4;u++){ Vector2 t=uvs[u]; t.y=1f-t.y; uvs[u]=t; }
                    if (MirrorWorldX)
                    {
                        AddTriangle(tris,baseIndex,baseIndex+1,baseIndex+2);
                        AddTriangle(tris,baseIndex,baseIndex+2,baseIndex+3);
                    }
                    else
                    {
                        AddTriangle(tris,baseIndex,baseIndex+2,baseIndex+1);
                        AddTriangle(tris,baseIndex,baseIndex+3,baseIndex+2);
                    }
                }
            }
            if (vertices.Count==0 || groups.Count==0) return false;

            List<long> keys=groups.Keys.OrderBy(k=>k).ToList();
            Mesh mesh=new Mesh(); mesh.name="ROTH 3D Object "+data.Index;
            if(vertices.Count>65535) mesh.indexFormat=IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetUVs(0,uvs); mesh.subMeshCount=keys.Count;
            var materials=new Material[keys.Count];
            for(int i=0;i<keys.Count;i++)
            {
                mesh.SetTriangles(groups[keys[i]],i,false);
                ushort textureIndex=(ushort)(keys[i]>>32);
                int sub=(int)(keys[i]&0xffffffffL);
                Material m=null;
                if(textureIndex>=65280) m=factory.GetPaletteMaterial(textureIndex-65280);
                else
                {
                    m=factory.GetImagePackMaterial(textureIndex,sub);
                    if(m==null) m=factory.GetMaterial(textureIndex);
                }
                materials[i]=m!=null?m:(ObjectMarkerMaterial!=null?ObjectMarkerMaterial:WallMaterial);
            }
            mesh.RecalculateNormals(); mesh.RecalculateBounds();

            GameObject go=new GameObject(string.Format("Obj {0:D4} ID={1} 3D DAS={2}",objectIndex,o.ObjectId,data.Index));
            go.transform.SetParent(parent,false); go.transform.localPosition=ConvertObjectPosition(o);
            go.transform.localRotation=Quaternion.Euler(0f,(MirrorWorldX?-1f:1f)*o.Rotation*(360f/256f),0f);
            MeshFilter mf=go.AddComponent<MeshFilter>(); mf.sharedMesh=mesh;
            MeshRenderer mr=go.AddComponent<MeshRenderer>(); mr.sharedMaterials=materials;
            AddObjectInteraction(go,objectIndex,o,mesh);
            return true;
        }

        private static void AddObjectInteraction(GameObject go, int objectIndex, RothObject o, Mesh mesh)
        {
            RothObjectTag tag=go.AddComponent<RothObjectTag>(); tag.ObjectIndex=objectIndex; tag.ObjectId=o.ObjectId; tag.TextureSource=o.TextureSource; tag.TextureIndex=o.TextureIndex;
            if(mesh!=null)
            {
                BoxCollider box=go.AddComponent<BoxCollider>(); box.center=mesh.bounds.center; box.size=mesh.bounds.size + new Vector3(0.02f,0.02f,0.08f); box.isTrigger=true;
            }
        }

        private void BuildObjectMarker(Transform parent, int i, RothObject o)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = string.Format("Unsupported Obj {0:D4} ID={1} Tex={2}:{3}", i, o.ObjectId, o.TextureSource, o.TextureIndex);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = ConvertObjectPosition(o);
            go.transform.localRotation = Quaternion.Euler(0f, (MirrorWorldX ? -1f : 1f) * o.Rotation * (360f / 256f), 0f);
            go.transform.localScale = Vector3.one * Mathf.Max(0.02f, ObjectMarkerSize);
            Collider c = go.GetComponent<Collider>(); if (c != null) c.isTrigger = true;
            RothObjectTag tag=go.AddComponent<RothObjectTag>(); tag.ObjectIndex=i; tag.ObjectId=o.ObjectId; tag.TextureSource=o.TextureSource; tag.TextureIndex=o.TextureIndex;
            Renderer r = go.GetComponent<Renderer>(); if (r != null && ObjectMarkerMaterial != null) r.sharedMaterial = ObjectMarkerMaterial;
        }

        private Vector3 ConvertObjectPosition(RothObject o)
        {
            return new Vector3((MirrorWorldX ? -o.PosX : o.PosX) * CoordinateScale, o.PosZ * HeightScale, o.PosY * CoordinateScale);
        }

        public bool ActivateSfxNode(ushort sfxId)
        {
            List<AudioSource> list; if(!_sfxNodes.TryGetValue(sfxId,out list)) return false; bool played=false;
            for(int i=0;i<list.Count;i++) if(list[i]!=null && list[i].clip!=null){list[i].Play();played=true;} return played;
        }

        public bool PlaySfxIndex(ushort sfxIndex)
        {
            if (_sfxFactory == null) return false;
            AudioClip clip = _sfxFactory.GetClip(sfxIndex);
            if (clip == null) return false;
            GameObject go = new GameObject("ROTH OneShot SFX " + sfxIndex);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 0f;
            source.playOnAwake = false;
            source.Play();
#if UNITY_EDITOR
            if (!Application.isPlaying) return true;
#endif
            Destroy(go, Mathf.Max(0.1f, clip.length + 0.1f));
            return true;
        }

        private void RebuildAmbientAudio()
        {
            ClearGeneratedAudio(); if(!BuildAmbientAudio || _map==null || _sfxFactory==null) return;
            GameObject root=new GameObject("ROTH SFX"); root.transform.SetParent(transform,false);
            for(int i=0;i<_map.SoundEffects.Count;i++)
            {
                RothSoundEffect sfx=_map.SoundEffects[i]; AudioClip clip=_sfxFactory.GetClip(sfx.SfxIndex); if(clip==null) continue;
                GameObject go=new GameObject(string.Format("SFX {0:D3} ID={1} IDX={2}",i,sfx.SfxId,sfx.SfxIndex)); go.transform.SetParent(root.transform,false);
                short floor=FindFloorAt(sfx.PosX,sfx.PosY);
                go.transform.localPosition=new Vector3((MirrorWorldX?-sfx.PosX:sfx.PosX)*CoordinateScale,floor*HeightScale+0.8f,sfx.PosY*CoordinateScale);
                AudioSource source=go.AddComponent<AudioSource>(); source.clip=clip; source.spatialBlend=1f; source.dopplerLevel=0f;
                source.volume=sfx.Volume>0 ? Mathf.Clamp01(sfx.Volume/255f) : 1f; source.loop=(sfx.Flags&1)!=0;
                source.playOnAwake=(sfx.Flags&0x80)!=0; source.minDistance=0.5f; source.maxDistance=Mathf.Max(2f,sfx.AudibleRadius*CoordinateScale);
                List<AudioSource> list;if(!_sfxNodes.TryGetValue(sfx.SfxId,out list)){list=new List<AudioSource>();_sfxNodes[sfx.SfxId]=list;}list.Add(source);
            }
        }

        private short FindFloorAt(short rawX, short rawY)
        {
            if(_map==null)return 0;
            for(int s=0;s<_map.Sectors.Count;s++)
            {
                RothSector sec=_map.Sectors[s]; if(sec.FirstFaceIndex<0||sec.FacesCount<3)continue; bool inside=false;
                for(int i=0,j=sec.FacesCount-1;i<sec.FacesCount;j=i++)
                {
                    RothFace fi=_map.Faces[sec.FirstFaceIndex+i],fj=_map.Faces[sec.FirstFaceIndex+j]; if(fi.VertexIndex01<0||fj.VertexIndex01<0)continue;
                    RothVertex a=_map.Vertices[fi.VertexIndex01],b=_map.Vertices[fj.VertexIndex01];
                    if(((a.Y>rawY)!=(b.Y>rawY)) && rawX < (b.X-a.X)*(rawY-a.Y)/(float)(b.Y-a.Y)+a.X) inside=!inside;
                }
                if(inside)return sec.FloorHeight;
            } return 0;
        }

        private void ClearGeneratedAudio()
        {
            _sfxNodes.Clear();
            for(int i=transform.childCount-1;i>=0;i--){Transform c=transform.GetChild(i);if(c.name=="ROTH SFX")DestroyGenerated(c.gameObject);}
        }

        private void ClearGeneratedObjects()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("ROTH Objects", StringComparison.Ordinal)) DestroyGenerated(child.gameObject);
            }
        }

        private static List<int> GetGroup(Dictionary<int, List<int>> groups, int key)
        {
            List<int> list;
            if (!groups.TryGetValue(key, out list))
            {
                list = new List<int>();
                groups[key] = list;
            }
            return list;
        }

        private List<RothVertex> GetSectorPolygon(RothRawMap map, RothSector sector)
        {
            var polygon = new List<RothVertex>(sector.FacesCount);
            int end = Math.Min(map.Faces.Count, sector.FirstFaceIndex + sector.FacesCount);
            for (int i = sector.FirstFaceIndex; i < end; i++)
            {
                RothFace face = map.Faces[i];
                if (face.VertexIndex01 < 0 || face.VertexIndex01 >= map.Vertices.Count) continue;
                RothVertex v = map.Vertices[face.VertexIndex01];
                if (polygon.Count == 0 || polygon[polygon.Count - 1].X != v.X || polygon[polygon.Count - 1].Y != v.Y)
                    polygon.Add(v);
            }
            if (polygon.Count > 2 && polygon[0].X == polygon[polygon.Count - 1].X && polygon[0].Y == polygon[polygon.Count - 1].Y)
                polygon.RemoveAt(polygon.Count - 1);
            return polygon;
        }

        private static List<int> TriangulatePolygon(List<RothVertex> polygon)
        {
            var result = new List<int>((polygon.Count - 2) * 3);
            if (polygon.Count < 3) return result;
            var remaining = new List<int>(polygon.Count);
            bool clockwise = SignedArea(polygon) < 0f;
            if (clockwise) for (int i = polygon.Count - 1; i >= 0; i--) remaining.Add(i);
            else for (int i = 0; i < polygon.Count; i++) remaining.Add(i);

            int guard = polygon.Count * polygon.Count;
            while (remaining.Count > 2 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int prev = remaining[(i - 1 + remaining.Count) % remaining.Count];
                    int curr = remaining[i];
                    int next = remaining[(i + 1) % remaining.Count];
                    if (!IsConvex(polygon[prev], polygon[curr], polygon[next])) continue;
                    if (ContainsOtherPoint(polygon, remaining, prev, curr, next)) continue;
                    result.Add(prev); result.Add(curr); result.Add(next);
                    remaining.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) break;
            }
            if (result.Count != (polygon.Count - 2) * 3)
            {
                result.Clear();
                for (int i = 1; i < polygon.Count - 1; i++) { result.Add(0); result.Add(i); result.Add(i + 1); }
            }
            return result;
        }

        private static bool ContainsOtherPoint(List<RothVertex> polygon, List<int> remaining, int a, int b, int c)
        {
            for (int i = 0; i < remaining.Count; i++)
            {
                int p = remaining[i];
                if (p == a || p == b || p == c) continue;
                if (PointInTriangle(polygon[p], polygon[a], polygon[b], polygon[c])) return true;
            }
            return false;
        }
        private static bool IsConvex(RothVertex a, RothVertex b, RothVertex c) { return Cross(a, b, c) > 0f; }
        private static bool PointInTriangle(RothVertex p, RothVertex a, RothVertex b, RothVertex c)
        {
            float c1 = Cross(a, b, p), c2 = Cross(b, c, p), c3 = Cross(c, a, p);
            const float e = 0.0001f;
            return c1 >= -e && c2 >= -e && c3 >= -e;
        }
        private static float Cross(RothVertex a, RothVertex b, RothVertex c)
        { return (b.X - a.X) * (float)(c.Y - a.Y) - (b.Y - a.Y) * (float)(c.X - a.X); }
        private static float SignedArea(List<RothVertex> polygon)
        {
            double area = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                RothVertex a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                area += (double)a.X * b.Y - (double)b.X * a.Y;
            }
            return (float)(area * 0.5);
        }

        private Vector3 ConvertPoint(RothVertex v, short height)
        { return new Vector3((MirrorWorldX ? -v.X : v.X) * CoordinateScale, height * HeightScale, v.Y * CoordinateScale); }
        private static void AddTriangle(List<int> triangles, int a, int b, int c)
        { triangles.Add(a); triangles.Add(b); triangles.Add(c); }

        private void ReplaceMesh(MeshFilter filter, Mesh mesh)
        {
            Mesh old = filter.sharedMesh;
            filter.sharedMesh = mesh;
            if (old != null && old != mesh) DestroyGenerated(old);
        }
        private T GetOrAdd<T>() where T : Component
        { T c = GetComponent<T>(); return c != null ? c : gameObject.AddComponent<T>(); }
        private void DisposeTextureFactory()
        {
            if (_textureFactory != null) { _textureFactory.Dispose(); _textureFactory = null; }
            if (_secondaryTextureFactory != null) { _secondaryTextureFactory.Dispose(); _secondaryTextureFactory = null; }
            if (_sfxFactory != null) { _sfxFactory.Dispose(); _sfxFactory = null; }
        }
        private void OnDestroy() { DisposeTextureFactory(); }
        private static void DestroyGenerated(UnityEngine.Object o)
        {
            if (o == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(o); else Destroy(o);
#else
            Destroy(o);
#endif
        }
    }
}
