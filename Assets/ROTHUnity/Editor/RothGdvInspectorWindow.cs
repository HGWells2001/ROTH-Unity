#if UNITY_EDITOR
using System;
using System.IO;
using ROTHUnity.Core;
using UnityEditor;
using UnityEngine;

namespace ROTHUnity.Editor
{
    public sealed class RothGdvInspectorWindow : EditorWindow
    {
        private string _path;
        private RothGdvArchive _gdv;
        private Texture2D _preview;
        private int _frame;

        [MenuItem("ROTH Unity/GDV Inspector")]
        public static void Open(){GetWindow<RothGdvInspectorWindow>("ROTH GDV").Show();}

        private void OnGUI()
        {
            EditorGUILayout.LabelField("ROTH GDV Inspector",EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();_path=EditorGUILayout.TextField("GDV",_path);
            if(GUILayout.Button("Browse",GUILayout.Width(80))){string p=EditorUtility.OpenFilePanel("ROTH GDV",string.IsNullOrEmpty(_path)?"":Path.GetDirectoryName(_path),"gdv");if(!string.IsNullOrEmpty(p)){_path=p;Load();}}
            EditorGUILayout.EndHorizontal();
            if(GUILayout.Button("Load"))Load();
            if(_gdv==null)return;
            RothGdvHeader h=_gdv.Header;
            EditorGUILayout.LabelField("Frames",h.FrameCount.ToString());EditorGUILayout.LabelField("Frame rate",h.FrameRate.ToString());EditorGUILayout.LabelField("Size",h.Width+" x "+h.Height);
            EditorGUILayout.LabelField("Audio",h.HasAudio?(h.PlaybackFrequency+" Hz "+(h.Stereo?"stereo":"mono")+(h.Dpcm?" DPCM":" PCM")):"none");
            EditorGUILayout.LabelField("Decoded frame records",_gdv.Frames.Count.ToString());
            _frame=EditorGUILayout.IntSlider("Preview frame",_frame,0,Mathf.Max(0,_gdv.Frames.Count-1));
            if(GUILayout.Button("Decode preview"))DecodePreview();
            if(_preview!=null){float w=Mathf.Min(position.width-20,640),a=(float)_preview.height/_preview.width;Rect r=GUILayoutUtility.GetRect(w,w*a);EditorGUI.DrawPreviewTexture(r,_preview,null,ScaleMode.ScaleToFit);}
        }
        private void Load(){try{_gdv=RothGdvArchive.Load(_path);_frame=0;DecodePreview();}catch(Exception e){Debug.LogException(e);_gdv=null;}}
        private void DecodePreview()
        {
            if(_gdv==null||_gdv.Frames.Count==0)return;byte[] p=null;var dec=_gdv.CreateDecoder();byte[] pal=dec.Palette;int target=Mathf.Clamp(_frame,0,_gdv.Frames.Count-1);
            for(int i=0;i<=target;i++)p=dec.DecodeNext();if(p==null)return;
            if(_preview!=null)DestroyImmediate(_preview);_preview=new Texture2D(_gdv.Header.Width,_gdv.Header.Height,TextureFormat.RGBA32,false);_preview.filterMode=FilterMode.Point;
            Color32[] c=new Color32[p.Length];for(int i=0;i<p.Length;i++){int q=p[i]*3;c[i]=new Color32(Expand(pal[q]),Expand(pal[q+1]),Expand(pal[q+2]),255);} _preview.SetPixels32(c);_preview.Apply();Repaint();
        }
        private static byte Expand(byte v){return (byte)((v*259+33)>>6);}
        private void OnDisable(){if(_preview!=null)DestroyImmediate(_preview);_preview=null;}
    }
}
#endif
