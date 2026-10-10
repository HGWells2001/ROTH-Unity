#if UNITY_EDITOR
using System;
using System.IO;
using ROTHUnity.Core;
using ROTHUnity.Runtime;
using UnityEditor;
using UnityEngine;

namespace ROTHUnity.Editor
{
    public sealed class RothRawMapInspectorWindow : EditorWindow
    {
        private string _path = "";
        private RothRawMap _map;
        private Vector2 _scroll;

        [MenuItem("ROTH Unity/RAW Map Inspector")]
        public static void Open() { GetWindow<RothRawMapInspectorWindow>("ROTH RAW Inspector"); }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Realms of the Haunting RAW Map Inspector", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select an original .RAW map. No original game assets are copied into the Unity project.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            _path = EditorGUILayout.TextField("RAW map", _path);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string p = EditorUtility.OpenFilePanel("Select ROTH RAW map", "", "RAW");
                if (!string.IsNullOrEmpty(p)) { _path = p; Parse(); }
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Parse")) Parse();
            if (_map == null) return;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Version", "0x" + _map.Header.Version.ToString("X4"));
            EditorGUILayout.LabelField("Sectors", _map.Sectors.Count.ToString());
            EditorGUILayout.LabelField("Faces", _map.Faces.Count.ToString());
            EditorGUILayout.LabelField("Texture mappings", _map.TextureMappings.Count.ToString());
            EditorGUILayout.LabelField("Mid platforms", _map.MidPlatforms.Count.ToString());
            EditorGUILayout.LabelField("Vertices", _map.Vertices.Count.ToString());
            EditorGUILayout.LabelField("Objects", _map.Objects.Count.ToString());
            EditorGUILayout.LabelField("Commands", _map.Commands.Count.ToString());
            EditorGUILayout.LabelField("Command entry points", _map.EntryCommandIndices.Count.ToString());
            EditorGUILayout.LabelField("Command categories", _map.CommandCategories.Count.ToString());
            EditorGUILayout.LabelField("Ambient SFX", _map.SoundEffects.Count.ToString());
            EditorGUILayout.LabelField("Sound zones", _map.SoundZones.Count.ToString());
            EditorGUILayout.LabelField("Player start", string.Format("X {0}, Y {1}, Z {2}", _map.Metadata.InitPosX, _map.Metadata.InitPosY, _map.Metadata.InitPosZ));
            EditorGUILayout.Space();

            if (GUILayout.Button("Create Scene Debug View"))
            {
                GameObject go = new GameObject("ROTH RAW - " + Path.GetFileNameWithoutExtension(_path));
                RothMapDebugView view = go.AddComponent<RothMapDebugView>();
                view.RawMapPath = _path;
                view.Reload();
                Selection.activeGameObject = go;
                SceneView.FrameLastActiveSceneView();
            }
            EditorGUILayout.EndScrollView();
        }

        private void Parse()
        {
            _map = null;
            try
            {
                if (!File.Exists(_path)) throw new FileNotFoundException("RAW map not found.", _path);
                _map = RothRawMapReader.Read(_path);
                Debug.Log(string.Format("ROTH RAW parsed: {0} sectors, {1} faces, {2} vertices, {3} platforms, {4} objects, {5} commands", _map.Sectors.Count, _map.Faces.Count, _map.Vertices.Count, _map.MidPlatforms.Count, _map.Objects.Count, _map.Commands.Count));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("ROTH RAW parser", ex.Message, "OK");
            }
        }
    }
}
#endif
