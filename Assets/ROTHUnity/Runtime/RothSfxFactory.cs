using System;
using System.Collections.Generic;
using ROTHUnity.Core;
using UnityEngine;

namespace ROTHUnity.Runtime
{
    public sealed class RothSfxFactory : IDisposable
    {
        private readonly RothSfxArchive _archive;
        private readonly Dictionary<int,AudioClip> _clips=new Dictionary<int,AudioClip>();
        public RothSfxFactory(string path){_archive=new RothSfxArchive(path);}
        public AudioClip GetClip(int index)
        {
            AudioClip c;if(_clips.TryGetValue(index,out c))return c; if(index<0||index>=_archive.Entries.Count)return null;
            RothSfxEntry e=_archive.Entries[index]; short[] pcm=_archive.ReadPcm16(index); if(pcm==null||pcm.Length==0)return null;
            float[] data=new float[pcm.Length];for(int i=0;i<pcm.Length;i++)data[i]=pcm[i]/32768f;
            c=AudioClip.Create(string.IsNullOrEmpty(e.Name)?"ROTH SFX "+index:e.Name,pcm.Length,1,e.SampleRate,false);c.SetData(data,0);_clips[index]=c;return c;
        }
        public RothSfxEntry GetEntry(int index){return index>=0&&index<_archive.Entries.Count?_archive.Entries[index]:null;}
        public void Dispose(){foreach(AudioClip c in _clips.Values)DestroyObject(c);_clips.Clear();_archive.Dispose();}
        private static void DestroyObject(UnityEngine.Object o){if(o==null)return;
#if UNITY_EDITOR
            if(!Application.isPlaying)UnityEngine.Object.DestroyImmediate(o);else UnityEngine.Object.Destroy(o);
#else
            UnityEngine.Object.Destroy(o);
#endif
        }
    }
}
