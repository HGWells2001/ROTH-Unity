#if UNITY_EDITOR
using ROTHUnity.Runtime;
using UnityEditor;
using UnityEngine;

namespace ROTHUnity.Editor
{
    public static class RothUnityMenu
    {
        [MenuItem("ROTH Unity/Create RAW Map Mesh Builder")]
        private static void CreateRawMapMeshBuilder()
        {
            GameObject go = new GameObject("ROTH RAW Map");
            go.AddComponent<RothMapMeshBuilder>();
            Undo.RegisterCreatedObjectUndo(go, "Create ROTH RAW Map");
            Selection.activeGameObject = go;
        }
    }
}
#endif
