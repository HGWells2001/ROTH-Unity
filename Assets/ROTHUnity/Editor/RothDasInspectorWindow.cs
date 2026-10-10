#if UNITY_EDITOR
using System;
using System.IO;
using ROTHUnity.Core;
using ROTHUnity.Runtime;
using UnityEditor;
using UnityEngine;

namespace ROTHUnity.Editor
{
    public sealed class RothDasInspectorWindow : EditorWindow
    {
        private string _path = "";
        private int _index = 100;
        private RothDasArchive _archive;
        private Texture2D _preview;
        private RothDasTextureFactory _factory;
        private Vector2 _scroll;

        [MenuItem("ROTH Unity/DAS Texture Inspector")]
        public static void Open() { GetWindow<RothDasInspectorWindow>("ROTH DAS Inspector"); }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Realms of the Haunting DAS Texture Inspector", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Open an original DASP archive such as DATA/M/DEMO.DAS. Milestone 0.3 previews ordinary paletted images; animation/image-pack/object entries are reported as unsupported for now.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            _path = EditorGUILayout.TextField("DAS archive", _path);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string p = EditorUtility.OpenFilePanel("Select ROTH DAS archive", "", "DAS");
                if (!string.IsNullOrEmpty(p)) { _path = p; OpenArchive(); }
            }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Open / Reload DAS")) OpenArchive();
            if (_archive == null) return;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            RothDasHeader h = _archive.Header;
            EditorGUILayout.LabelField("Signature / version", h.Signature + " / " + h.Version);
            EditorGUILayout.LabelField("FAT entries", h.TotalFatCount.ToString());
            EditorGUILayout.LabelField("Blocks", string.Format("{0} + {1} + {2} + {3}", h.FatBlock1Count, h.FatBlock2Count, h.FatBlock3Count, h.FatBlock4Count));
            EditorGUILayout.LabelField("Palette offset", "0x" + h.PaletteOffset.ToString("X"));
            EditorGUILayout.Space();

            _index = EditorGUILayout.IntField("Texture index", _index);
            if (GUILayout.Button("Preview Texture", GUILayout.Height(28))) LoadPreview();
            RothDasFatEntry e = _archive.GetEntry(_index);
            if (e != null)
            {
                EditorGUILayout.LabelField("Name", string.IsNullOrEmpty(e.Name) ? "(unnamed)" : e.Name);
                EditorGUILayout.LabelField("Description", string.IsNullOrEmpty(e.Description) ? "" : e.Description);
                EditorGUILayout.LabelField("FAT", string.Format("offset 0x{0:X}, size {1}, flags {2:X2}/{3:X2}", e.Offset, e.Size, e.Flags1, e.Flags2));
            }
            if (_preview != null)
            {
                float max = Mathf.Min(position.width - 40, 512);
                float aspect = _preview.height > 0 ? _preview.width / (float)_preview.height : 1;
                Rect r = GUILayoutUtility.GetRect(max, Mathf.Max(64, max / Mathf.Max(0.01f, aspect)), GUILayout.ExpandWidth(false));
                EditorGUI.DrawPreviewTexture(r, _preview, null, ScaleMode.ScaleToFit);
                EditorGUILayout.LabelField(_preview.width + " x " + _preview.height);
            }
            EditorGUILayout.EndScrollView();
        }

        private void OpenArchive()
        {
            CloseArchive();
            try
            {
                if (!File.Exists(_path)) throw new FileNotFoundException("DAS archive not found.", _path);
                _archive = new RothDasArchive(_path);
                _factory = new RothDasTextureFactory(_path, null);
                Repaint();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("ROTH DAS parser", ex.Message, "OK");
            }
        }

        private void LoadPreview()
        {
            if (_factory == null) return;
            _preview = _factory.GetTexture(_index);
            if (_preview == null)
                EditorUtility.DisplayDialog("ROTH DAS", "This entry is not a standard image supported by milestone 0.3, or the index is absent.", "OK");
            Repaint();
        }

        private void CloseArchive()
        {
            _preview = null;
            if (_factory != null) { _factory.Dispose(); _factory = null; }
            if (_archive != null) { _archive.Dispose(); _archive = null; }
        }
        private void OnDisable() { CloseArchive(); }
    }
}
#endif
