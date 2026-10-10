#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ROTHUnity.Core;
using ROTHUnity.Runtime;
using UnityEditor;
using UnityEngine;

namespace ROTHUnity.Editor
{
    public sealed class RothQuickStartWindow : EditorWindow
    {
        private string _root = RothInstallLocator.PreferredGogGalaxyPath;
        private RothInstallPaths _paths;
        private string _status;
        private List<string> _maps = new List<string>();
        private int _mapIndex;

        [MenuItem("ROTH Unity/Quick Start GOG")]
        public static void Open() { GetWindow<RothQuickStartWindow>("ROTH Quick Start").Show(); }
        private void OnEnable() { Detect(); }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("ROTH Unity Quick Start", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Detects the original GOG installation, resolves the correct DEMO*.DAS for a RAW map and creates a playable test scene.", MessageType.Info);
            _root = EditorGUILayout.TextField("Game folder", _root);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Detect installation")) Detect();
            if (GUILayout.Button("Browse..."))
            {
                string p = EditorUtility.OpenFolderPanel("Realms of the Haunting folder", _root, "");
                if (!string.IsNullOrEmpty(p)) { _root = p; _paths = RothInstallLocator.ResolveRoot(_root); RefreshMaps(); UpdateStatus(); }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(_status ?? "Not detected", EditorStyles.wordWrappedLabel);

            if (_maps.Count > 0)
            {
                string[] labels = new string[_maps.Count];
                for (int i=0;i<_maps.Count;i++) labels[i]=Path.GetFileNameWithoutExtension(_maps[i]);
                _mapIndex = Mathf.Clamp(_mapIndex,0,_maps.Count-1);
                _mapIndex = EditorGUILayout.Popup("Map",_mapIndex,labels);
            }

            GUI.enabled = _paths != null && _paths.IsComplete && _maps.Count > 0;
            if (GUILayout.Button("Create playable map test", GUILayout.Height(34))) CreateSelectedMap();
            GUI.enabled = true;
        }

        private void Detect()
        {
            _paths = RothInstallLocator.Locate(_root);
            if (_paths != null) _root = _paths.Root;
            RefreshMaps(); UpdateStatus();
        }

        private void RefreshMaps()
        {
            _maps = _paths == null ? new List<string>() : RothInstallLocator.ListMapFiles(_paths.Root);
            int study = _maps.FindIndex(p=>string.Equals(Path.GetFileName(p),"STUDY1.RAW",StringComparison.OrdinalIgnoreCase));
            if (study >= 0) _mapIndex = study;
        }

        private void UpdateStatus()
        {
            if (_paths == null) { _status = "Installation not found."; return; }
            _status = string.Format("Root: {0}\nMaps: {1}\nADEMO.DAS: {2}\nSFX: {3}\nDBASE100: {4}\nDBASE300: {5}\nDBASE400: {6}\nDBASE500: {7}\nGDV: {8}",
                _paths.Root, _maps.Count, _paths.AdemoDas ?? "missing", _paths.SfxArchive ?? "missing", _paths.DBase100 ?? "missing", _paths.DBase300 ?? "missing", _paths.DBase400 ?? "missing", _paths.DBase500 ?? "missing", _paths.GdvDirectory ?? "missing");
        }

        private void CreateSelectedMap()
        {
            string raw = _maps[_mapIndex];
            RothMapResourceResolver.Match match = RothMapResourceResolver.SelectBestDas(raw,RothInstallLocator.ListPrimaryDasFiles(_paths.Root));
            if (match == null) { EditorUtility.DisplayDialog("ROTH Unity","Could not resolve a primary DAS for "+Path.GetFileName(raw),"OK"); return; }
            string mapName = Path.GetFileNameWithoutExtension(raw);
            GameObject old = GameObject.Find("ROTH World");
            if (old != null && EditorUtility.DisplayDialog("ROTH Unity", "Replace the existing ROTH World object?", "Replace", "Cancel")) DestroyImmediate(old);
            else if (old != null) return;

            GameObject root = new GameObject("ROTH World");
            Undo.RegisterCreatedObjectUndo(root, "Create ROTH World");
            RothMapMeshBuilder builder = root.AddComponent<RothMapMeshBuilder>();
            builder.RawMapPath=raw; builder.DasPath=match.DasPath; builder.SecondaryDasPath=_paths.AdemoDas; builder.SfxPath=_paths.SfxArchive;
            builder.UseOriginalTextures=true; builder.MirrorWorldX=false; builder.BuildOriginalObjects=true; builder.BuildUnsupportedObjectMarkers=true; builder.Rebuild();

            Vector3 spawn; float yaw, playerHeight, moveSpeed;
            builder.GetOriginalPlayerSetup(out spawn,out yaw,out playerHeight,out moveSpeed);
            GameObject player = new GameObject("ROTH Test Player"); player.transform.SetParent(root.transform,false); player.transform.localPosition=spawn; player.transform.localRotation=Quaternion.Euler(0f,yaw,0f);
            CharacterController cc=player.AddComponent<CharacterController>(); cc.height=Mathf.Max(1.2f,playerHeight); cc.radius=Mathf.Clamp(cc.height*0.18f,0.22f,0.45f); cc.center=new Vector3(0f,cc.height*0.5f,0f); cc.stepOffset=Mathf.Min(0.4f,cc.height*0.2f);
            GameObject cameraGo=new GameObject("Player Camera"); cameraGo.tag="MainCamera"; cameraGo.transform.SetParent(player.transform,false); cameraGo.transform.localPosition=new Vector3(0f,cc.height*0.86f,0f);
            Camera cam=cameraGo.AddComponent<Camera>(); cam.nearClipPlane=0.02f; cameraGo.AddComponent<AudioListener>();
            RothFirstPersonController fps=player.AddComponent<RothFirstPersonController>(); fps.View=cameraGo.transform; fps.MoveSpeed=Mathf.Clamp(moveSpeed,2f,8f);

            RothNarrativePlayer narrative=root.AddComponent<RothNarrativePlayer>();
            RothGdvPlayer gdv=root.AddComponent<RothGdvPlayer>();
            RothCommandMonitor commands=root.AddComponent<RothCommandMonitor>(); commands.MapBuilder=builder; commands.Player=player.transform; commands.LogTriggeredChains=true; commands.DBase100Path=_paths.DBase100; commands.DBase300Path=_paths.DBase300; commands.DBase400Path=_paths.DBase400; commands.DBase500Path=_paths.DBase500; commands.NarrativePlayer=narrative; commands.GdvPlayer=gdv;
            RothWorldController world=root.AddComponent<RothWorldController>(); world.GameRoot=_paths.Root; world.MapBuilder=builder; world.Commands=commands; world.Player=player.transform;
            commands.WorldController=world;
            RothRuntimeHud hud=root.AddComponent<RothRuntimeHud>(); hud.MapBuilder=builder; hud.Commands=commands;
            RothPlayerInteractor interactor=player.AddComponent<RothPlayerInteractor>(); interactor.ViewCamera=cam; interactor.Commands=commands;

            Selection.activeGameObject=root; EditorGUIUtility.PingObject(root);
            _status += string.Format("\nCreated {0} with {1} ({2:P1} texture coverage). Enter Play Mode; WASD + mouse, Space to jump, Esc releases mouse.",mapName,Path.GetFileName(match.DasPath),match.Coverage);
        }
    }
}
#endif
