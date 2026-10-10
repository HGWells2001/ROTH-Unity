#if UNITY_EDITOR
using ROTHUnity.Core;
using UnityEditor;
using UnityEngine;

namespace ROTHUnity.Editor
{
    public sealed class RothDBase100InspectorWindow : EditorWindow
    {
        private string _path="";
        private RothDBase100Archive _db;
        private Vector2 _scroll;
        private int _action;

        [MenuItem("ROTH Unity/DBASE100 Inspector")]
        public static void Open(){GetWindow<RothDBase100InspectorWindow>("ROTH DBASE100").Show();}

        private void OnGUI()
        {
            EditorGUILayout.LabelField("ROTH DBASE100 Inspector",EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _path=EditorGUILayout.TextField("DBASE100.DAT",_path);
            if(GUILayout.Button("Browse",GUILayout.Width(80)))
            {
                string p=EditorUtility.OpenFilePanel("Select DBASE100.DAT","","DAT");
                if(!string.IsNullOrEmpty(p)){_path=p;Load();}
            }
            if(GUILayout.Button("Load",GUILayout.Width(60)))Load();
            EditorGUILayout.EndHorizontal();
            if(_db==null)return;
            EditorGUILayout.LabelField("Actions",_db.Actions.Count.ToString());
            _action=EditorGUILayout.IntSlider("Action index",_action,0,Mathf.Max(0,_db.Actions.Count-1));
            RothDBase100Action a=_db.GetAction(_action);
            if(a==null)return;
            EditorGUILayout.LabelField("File offset","0x"+a.FileOffset.ToString("X"));
            EditorGUILayout.LabelField("Commands",a.Commands.Count.ToString());
            _scroll=EditorGUILayout.BeginScrollView(_scroll);
            for(int i=0;i<a.Commands.Count;i++)
                EditorGUILayout.LabelField(string.Format("{0,2}: opcode {1,3}   arg {2} (0x{2:X6})",i,a.Commands[i].Opcode,a.Commands[i].Argument));
            EditorGUILayout.EndScrollView();
        }
        private void Load()
        {
            try{_db=RothDBase100Archive.Load(_path);_action=0;Repaint();}
            catch(System.Exception e){Debug.LogException(e);_db=null;}
        }
    }
}
#endif
