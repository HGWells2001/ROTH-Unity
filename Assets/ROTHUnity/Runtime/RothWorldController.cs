using System;
using System.Collections.Generic;
using System.IO;
using ROTHUnity.Core;
using UnityEngine;

namespace ROTHUnity.Runtime
{
    /// <summary>Owns map changes for the playable prototype and resolves the correct retail DAS automatically.</summary>
    public sealed class RothWorldController : MonoBehaviour
    {
        public string GameRoot;
        public RothMapMeshBuilder MapBuilder;
        public RothCommandMonitor Commands;
        public Transform Player;
        public bool LogTransitions = true;

        private readonly List<string> _dasCandidates = new List<string>();

        private void Awake()
        {
            if (MapBuilder == null) MapBuilder = GetComponent<RothMapMeshBuilder>();
            RefreshDasCandidates();
        }

        public void RefreshDasCandidates()
        {
            _dasCandidates.Clear();
            if (string.IsNullOrWhiteSpace(GameRoot)) return;
            for (int i = 0; i <= 4; i++)
            {
                string name = i == 0 ? "DEMO.DAS" : "DEMO" + i + ".DAS";
                string p = RothInstallLocator.FindMapFile(GameRoot, name);
                if (!string.IsNullOrEmpty(p) && !_dasCandidates.Contains(p)) _dasCandidates.Add(p);
            }
        }

        public bool LoadMap(string mapName, ushort destinationSectorId = 0)
        {
            if (MapBuilder == null || string.IsNullOrWhiteSpace(GameRoot) || string.IsNullOrWhiteSpace(mapName)) return false;
            string clean = Path.GetFileNameWithoutExtension(mapName).Trim('\0', ' ', '\t');
            if (clean.Length == 0) return false;
            string raw = RothInstallLocator.FindMapFile(GameRoot, clean + ".RAW");
            if (string.IsNullOrEmpty(raw) || !File.Exists(raw))
            {
                if (LogTransitions) Debug.LogWarning("ROTH transition target RAW not found: " + clean, this);
                return false;
            }
            if (_dasCandidates.Count == 0) RefreshDasCandidates();
            RothMapResourceResolver.Match match = RothMapResourceResolver.SelectBestDas(raw, _dasCandidates);
            if (match == null)
            {
                if (LogTransitions) Debug.LogWarning("ROTH could not resolve DAS for " + clean, this);
                return false;
            }

            RothSectorMover mover = MapBuilder.GetComponent<RothSectorMover>();
            if (mover != null) mover.CancelAll();
            RothHorizontalSectorMover horizontal = MapBuilder.GetComponent<RothHorizontalSectorMover>();
            if (horizontal != null) horizontal.CancelAll();
            MapBuilder.RawMapPath = raw;
            MapBuilder.DasPath = match.DasPath;
            MapBuilder.Rebuild();
            PlacePlayer(destinationSectorId, false);
            if (Commands != null) Commands.ReloadCurrentMap();
            if (LogTransitions)
                Debug.Log(string.Format("ROTH loaded {0} with {1} ({2}/{3}, {4:P1})", clean, Path.GetFileName(match.DasPath), match.Resolved, match.Referenced, match.Coverage), this);
            return true;
        }

        public bool WarpWithinCurrentMap(ushort destinationSectorId)
        {
            if (MapBuilder == null || destinationSectorId == 0) return false;
            return PlacePlayer(destinationSectorId, true);
        }

        private bool PlacePlayer(ushort destinationSectorId, bool preserveRotation)
        {
            if (Player == null || MapBuilder == null) return false;
            try
            {
                RothRawMap map = RothRawMapReader.Read(MapBuilder.RawMapPath);
                Vector3 pos; float yaw, ph, speed;
                MapBuilder.GetOriginalPlayerSetup(out pos, out yaw, out ph, out speed);
                if (destinationSectorId != 0)
                {
                    Vector3 sectorPos;
                    if (TrySectorCenter(map, destinationSectorId, out sectorPos)) pos = sectorPos;
                }
                CharacterController cc = Player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                Quaternion oldRotation=Player.localRotation;
                Player.localPosition = pos;
                Player.localRotation = preserveRotation ? oldRotation : Quaternion.Euler(0f, yaw, 0f);
                if (cc != null) cc.enabled = true;
                RothFirstPersonController fps = Player.GetComponent<RothFirstPersonController>();
                if (fps != null) fps.MoveSpeed = Mathf.Clamp(speed, 2f, 8f);
                return true;
            }
            catch (Exception e) { Debug.LogException(e, this); return false; }
        }

        private bool TrySectorCenter(RothRawMap map, ushort sectorId, out Vector3 world)
        {
            world = Vector3.zero;
            for (int s = 0; s < map.Sectors.Count; s++)
            {
                RothSector sec = map.Sectors[s];
                if (sec.SectorId != sectorId || sec.FirstFaceIndex < 0 || sec.FacesCount == 0) continue;
                float x = 0f, z = 0f; int count = 0;
                int end = Math.Min(map.Faces.Count, sec.FirstFaceIndex + sec.FacesCount);
                for (int i = sec.FirstFaceIndex; i < end; i++)
                {
                    RothFace f = map.Faces[i]; if (f.VertexIndex01 < 0) continue;
                    RothVertex v = map.Vertices[f.VertexIndex01]; x += v.X; z += v.Y; count++;
                }
                if (count == 0) return false;
                x /= count; z /= count;
                if (MapBuilder.MirrorWorldX) x = -x;
                world = new Vector3(x * MapBuilder.CoordinateScale, sec.FloorHeight * MapBuilder.HeightScale + 0.03f, z * MapBuilder.CoordinateScale);
                return true;
            }
            return false;
        }
    }
}
