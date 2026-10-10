#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ROTHUnity.Core;
using UnityEditor;
using UnityEngine;

namespace ROTHUnity.Editor
{
    public sealed class RothRetailMapAuditWindow : EditorWindow
    {
        private string _root = RothInstallLocator.PreferredGogGalaxyPath;
        private Vector2 _scroll;
        private readonly List<Row> _rows = new List<Row>();
        private string _status = "Not scanned.";

        private sealed class Row
        {
            public string Map;
            public string Das;
            public int Resolved;
            public int Referenced;
            public float Coverage;
        }

        [MenuItem("ROTH Unity/Retail Map Resource Audit")]
        public static void Open() { GetWindow<RothRetailMapAuditWindow>("ROTH Resource Audit").Show(); }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Retail Map Resource Audit", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Scans installed RAW maps and selects DEMO.DAS..DEMO4.DAS by the same FAT-coverage resolver used at runtime. This does not modify original game files.", MessageType.Info);
            _root = EditorGUILayout.TextField("Game folder", _root);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan")) Scan();
            if (GUILayout.Button("Browse..."))
            {
                string p = EditorUtility.OpenFolderPanel("Realms of the Haunting folder", _root, "");
                if (!string.IsNullOrEmpty(p)) { _root = p; Scan(); }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(_status, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (Row row in _rows)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(row.Map, GUILayout.Width(110));
                EditorGUILayout.LabelField(row.Das, GUILayout.Width(90));
                EditorGUILayout.LabelField(string.Format("{0}/{1}", row.Resolved, row.Referenced), GUILayout.Width(80));
                EditorGUILayout.LabelField(row.Coverage.ToString("P1"), GUILayout.Width(70));
                if (row.Coverage < 0.95f) GUILayout.Label("CHECK", EditorStyles.boldLabel, GUILayout.Width(55));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void Scan()
        {
            _rows.Clear();
            RothInstallPaths paths = RothInstallLocator.Locate(_root) ?? RothInstallLocator.ResolveRoot(_root);
            if (paths == null || string.IsNullOrWhiteSpace(paths.Root) || !Directory.Exists(paths.Root))
            {
                _status = "Installation not found.";
                return;
            }
            _root = paths.Root;
            List<string> maps = RothInstallLocator.ListMapFiles(_root);
            List<string> dasFiles = RothInstallLocator.ListPrimaryDasFiles(_root);
            int exact = 0;
            foreach (string raw in maps)
            {
                RothMapResourceResolver.Match match = RothMapResourceResolver.SelectBestDas(raw, dasFiles);
                if (match == null) continue;
                _rows.Add(new Row {
                    Map = Path.GetFileNameWithoutExtension(raw),
                    Das = Path.GetFileName(match.DasPath),
                    Resolved = match.Resolved,
                    Referenced = match.Referenced,
                    Coverage = match.Coverage
                });
                if (match.Coverage >= 0.99999f) exact++;
            }
            _status = string.Format("Scanned {0} maps. Exact FAT coverage: {1}/{0}. Rows below 95% are flagged for manual investigation.", _rows.Count, exact);
        }
    }
}
#endif
