#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ROTHUnity.Core;
using UnityEditor;
using UnityEngine;

namespace ROTHUnity.Editor
{
    public sealed class RothCommandInspectorWindow : EditorWindow
    {
        private string _rawPath;
        private RothRawMap _map;
        private Vector2 _scroll;
        private string _filter="";
        [MenuItem("ROTH Unity/RAW Command Inspector")]
        public static void Open(){GetWindow<RothCommandInspectorWindow>("ROTH Commands").Show();}
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Original RAW command graph",EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal(); _rawPath=EditorGUILayout.TextField("RAW",_rawPath);
            if(GUILayout.Button("Browse",GUILayout.Width(70))) { string p=EditorUtility.OpenFilePanel("ROTH RAW","","raw"); if(!string.IsNullOrEmpty(p)){_rawPath=p;Load();} }
            if(GUILayout.Button("Load",GUILayout.Width(60))) Load(); EditorGUILayout.EndHorizontal();
            if(_map==null) return;
            EditorGUILayout.LabelField(string.Format("Commands: {0}   Entry refs: {1}   Categories: {2}",_map.Commands.Count,_map.EntryCommandIndices.Count,_map.CommandCategories.Count));
            _filter=EditorGUILayout.TextField("Filter",_filter);
            var entries=new HashSet<int>(_map.EntryCommandIndices);
            _scroll=EditorGUILayout.BeginScrollView(_scroll);
            for(int i=0;i<_map.Commands.Count;i++)
            {
                RothCommand c=_map.Commands[i]; RothCommandInfo info=RothCommandCatalog.Get(c.BaseOpcode);
                string args=string.Join(", ",c.Arguments.Select(x=>x.ToString()).ToArray());
                string line=string.Format("#{0} {1} [{2}] mod=0x{3:X2} next={4} args=({5})",i+1,info.Name,c.BaseOpcode,c.Modifier,c.NextCommandIndex,args);
                if(!string.IsNullOrEmpty(_filter) && line.ToLowerInvariant().IndexOf(_filter.ToLowerInvariant())<0) continue;
                EditorGUILayout.LabelField((entries.Contains(i+1)?"ENTRY  ":"       ")+line,EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndScrollView();
        }
        private void Load(){ try { if(File.Exists(_rawPath)) _map=RothRawMapReader.Read(_rawPath); } catch(System.Exception e){Debug.LogException(e);_map=null;} }
    }
}
#endif
