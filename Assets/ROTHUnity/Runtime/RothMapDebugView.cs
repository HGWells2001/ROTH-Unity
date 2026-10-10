using System;
using System.IO;
using ROTHUnity.Core;
using UnityEngine;

namespace ROTHUnity.Runtime
{
    /// <summary>First visual milestone: draws original ROTH sector edges directly from a .RAW map.</summary>
    [ExecuteAlways]
    public sealed class RothMapDebugView : MonoBehaviour
    {
        [Tooltip("Absolute path to an original ROTH .RAW map while prototyping.")]
        public string RawMapPath;
        public float CoordinateScale = 1f / 128f;
        public float HeightScale = 1f / 128f;
        public bool MirrorWorldX = false;
        public bool DrawCeilings;
        public bool DrawPlayerStart = true;

        [NonSerialized] private RothRawMap _map;
        [NonSerialized] private string _loadedPath;

        public void Reload()
        {
            _map = null;
            _loadedPath = null;
            if (!string.IsNullOrWhiteSpace(RawMapPath) && File.Exists(RawMapPath))
            {
                _map = RothRawMapReader.Read(RawMapPath);
                _loadedPath = RawMapPath;
            }
        }

        private void OnDrawGizmos()
        {
            if (_map == null || _loadedPath != RawMapPath) Reload();
            if (_map == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            for (int i = 0; i < _map.Faces.Count; i++)
            {
                RothFace face = _map.Faces[i];
                if (face.VertexIndex01 < 0 || face.VertexIndex02 < 0 || face.SectorIndex < 0) continue;
                RothVertex a = _map.Vertices[face.VertexIndex01];
                RothVertex b = _map.Vertices[face.VertexIndex02];
                RothSector sector = _map.Sectors[face.SectorIndex];

                Vector3 p1 = ConvertPoint(a, sector.FloorHeight);
                Vector3 p2 = ConvertPoint(b, sector.FloorHeight);
                Gizmos.DrawLine(p1, p2);
                if (DrawCeilings)
                {
                    Vector3 c1 = ConvertPoint(a, sector.CeilingHeight);
                    Vector3 c2 = ConvertPoint(b, sector.CeilingHeight);
                    Gizmos.DrawLine(c1, c2);
                    Gizmos.DrawLine(p1, c1);
                }
            }

            if (DrawPlayerStart)
            {
                Vector3 p = new Vector3((MirrorWorldX ? -_map.Metadata.InitPosX : _map.Metadata.InitPosX) * CoordinateScale,
                    _map.Metadata.InitPosZ * HeightScale,
                    _map.Metadata.InitPosY * CoordinateScale);
                Gizmos.DrawWireSphere(p, 0.25f);
            }
        }

        private Vector3 ConvertPoint(RothVertex v, short height)
        {
            // 0.4.2 defaults to original ROTH X orientation; legacy mirroring remains optional.
            return new Vector3((MirrorWorldX ? -v.X : v.X) * CoordinateScale, height * HeightScale, v.Y * CoordinateScale);
        }
    }
}
