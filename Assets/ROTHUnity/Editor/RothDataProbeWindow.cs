#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using RothUnity.Data;

namespace RothUnity.Editor
{
    public sealed class RothDataProbeWindow : EditorWindow
    {
        private string _root = string.Empty;
        private Vector2 _scroll;
        private RothProbeResult _probe;
        private RothAssetInventory _inventory;
        private string _error;

        [MenuItem("Tools/ROTH Unity/Data Probe")]
        public static void Open()
        {
            GetWindow<RothDataProbeWindow>("ROTH Data Probe");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Original Realms of the Haunting data", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select a folder containing your legally obtained game data. " +
                "No original assets are copied into the Unity project.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                _root = EditorGUILayout.TextField("Game root", _root);
                if (GUILayout.Button("Browse", GUILayout.Width(80)))
                {
                    var selected = EditorUtility.OpenFolderPanel("Select ROTH game data", _root, string.Empty);
                    if (!string.IsNullOrEmpty(selected))
                        _root = selected;
                }
            }

            if (GUILayout.Button("Probe data"))
                RunProbe();

            if (!string.IsNullOrEmpty(_error))
                EditorGUILayout.HelpBox(_error, MessageType.Error);

            if (_probe == null)
                return;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                _probe.IsUsable ? "Core data: OK" : "Core data: incomplete",
                EditorStyles.boldLabel);

            foreach (var file in _probe.Files)
                EditorGUILayout.LabelField(file.Name, file.Size.ToString("N0") + " bytes");

            foreach (var missing in _probe.Missing)
                EditorGUILayout.LabelField(missing, "MISSING");

            if (_inventory != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Asset inventory", EditorStyles.boldLabel);
                foreach (var extension in _inventory.Extensions)
                    EditorGUILayout.LabelField(extension, _inventory.Count(extension).ToString());
            }

            EditorGUILayout.EndScrollView();
        }

        private void RunProbe()
        {
            _probe = null;
            _inventory = null;
            _error = null;

            try
            {
                _probe = RothGameDataProbe.Probe(_root);
                _inventory = RothAssetInventory.Scan(Path.GetFullPath(_root));
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
        }
    }
}
#endif
