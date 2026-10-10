#if UNITY_EDITOR
using System.IO;
using ROTHUnity.Runtime;
using UnityEditor;
using UnityEngine;

namespace ROTHUnity.Editor
{
    [CustomEditor(typeof(RothMapMeshBuilder))]
    public sealed class RothMapMeshBuilderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            GUILayout.Space(8);
            RothMapMeshBuilder builder = (RothMapMeshBuilder)target;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Browse RAW"))
            {
                string p = EditorUtility.OpenFilePanel("Select ROTH RAW map", "", "RAW");
                if (!string.IsNullOrEmpty(p)) { Undo.RecordObject(builder, "Select ROTH RAW"); builder.RawMapPath = p; EditorUtility.SetDirty(builder); }
            }
            if (GUILayout.Button("Browse DAS"))
            {
                string start = !string.IsNullOrEmpty(builder.RawMapPath) ? Path.GetDirectoryName(builder.RawMapPath) : "";
                string p = EditorUtility.OpenFilePanel("Select companion ROTH DAS archive", start, "DAS");
                if (!string.IsNullOrEmpty(p)) { Undo.RecordObject(builder, "Select ROTH DAS"); builder.DasPath = p; EditorUtility.SetDirty(builder); }
            }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Browse Secondary DAS (objects source 2/3)"))
            {
                string start = !string.IsNullOrEmpty(builder.DasPath) ? Path.GetDirectoryName(builder.DasPath) : "";
                string p = EditorUtility.OpenFilePanel("Select optional ROTH secondary DAS archive", start, "DAS");
                if (!string.IsNullOrEmpty(p)) { Undo.RecordObject(builder, "Select ROTH secondary DAS"); builder.SecondaryDasPath = p; EditorUtility.SetDirty(builder); }
            }

            if (GUILayout.Button("Browse SFX archive"))
            {
                string start = !string.IsNullOrEmpty(builder.DasPath) ? Path.GetDirectoryName(Path.GetDirectoryName(builder.DasPath) ?? builder.DasPath) : "";
                string p = EditorUtility.OpenFilePanel("Select original ROTH FX22/FXSCRIPT SFX archive", start, "SFX");
                if (!string.IsNullOrEmpty(p)) { Undo.RecordObject(builder, "Select ROTH SFX"); builder.SfxPath = p; EditorUtility.SetDirty(builder); }
            }

            if (GUILayout.Button("Build / Rebuild ROTH Mesh", GUILayout.Height(32)))
            {
                try
                {
                    Undo.RecordObject(builder.gameObject, "Build ROTH Map Mesh");
                    builder.Rebuild();
                    EditorUtility.SetDirty(builder.gameObject);
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                    EditorUtility.DisplayDialog("ROTH Unity", ex.Message, "OK");
                }
            }

            if (GUILayout.Button("Clear Generated Mesh"))
            {
                builder.ClearGeneratedMesh();
                EditorUtility.SetDirty(builder.gameObject);
            }
        }
    }
}
#endif
