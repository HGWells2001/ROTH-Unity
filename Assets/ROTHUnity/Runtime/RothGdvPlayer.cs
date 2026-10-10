using System;
using System.Collections.Generic;
using ROTHUnity.Core;
using UnityEngine;

namespace ROTHUnity.Runtime
{
    /// <summary>Minimal fullscreen GDV player. Uses IMGUI so no Unity UI package is required.</summary>
    public sealed class RothGdvPlayer : MonoBehaviour
    {
        public bool IsPlaying { get; private set; }
        public string CurrentPath { get; private set; }
        public bool AllowSkip = true;
        public KeyCode SkipKey = KeyCode.Escape;
        public float SubtitleTicksPerSecond = 8f;
        public event Action PlaybackFinished;
        public RothFirstPersonController PlayerController;
        public RothPlayerInteractor PlayerInteractor;

        private RothGdvArchive _gdv;
        private Texture2D _texture;
        private byte[] _pixels;
        private byte[] _palette;
        private RothGdvArchive.RothGdvDecoderSession _decoder;
        private int _frame;
        private float _startTime;
        private AudioSource _audio;
        private AudioClip _clip;
        private readonly List<RothDBaseSubtitleEntry> _subtitles=new List<RothDBaseSubtitleEntry>();
        private string _subtitleText;
        private sealed class PendingGdv
        {
            public string Path;
            public readonly List<RothDBaseSubtitleEntry> Subtitles=new List<RothDBaseSubtitleEntry>();
        }
        private readonly Queue<PendingGdv> _pending=new Queue<PendingGdv>();

        public void SetSubtitles(IList<RothDBaseSubtitleEntry> subtitles){_subtitles.Clear();if(subtitles!=null)for(int i=0;i<subtitles.Count;i++)_subtitles.Add(subtitles[i]);}

        public bool Play(string path)
        {
            _pending.Clear();
            StopCurrent(false);
            return PlayImmediate(path,null);
        }

        public bool PlayWithSubtitles(string path,IList<RothDBaseSubtitleEntry> subtitles)
        {
            if(string.IsNullOrEmpty(path))return false;
            if(IsPlaying)
            {
                var req=new PendingGdv{Path=path};
                if(subtitles!=null)for(int i=0;i<subtitles.Count;i++)req.Subtitles.Add(subtitles[i]);
                _pending.Enqueue(req);return true;
            }
            return PlayImmediate(path,subtitles);
        }

        private bool PlayImmediate(string path,IList<RothDBaseSubtitleEntry> subtitles)
        {
            try
            {
                _gdv = RothGdvArchive.Load(path);
                if (_gdv == null || _gdv.Header == null || _gdv.Frames.Count == 0) return false;
                CurrentPath = path; _decoder=_gdv.CreateDecoder(); _palette = _decoder.Palette; _pixels = null; _frame = -1;
                if(PlayerController==null)PlayerController=FindFirstObjectByType<RothFirstPersonController>();if(PlayerInteractor==null)PlayerInteractor=FindFirstObjectByType<RothPlayerInteractor>();
                if(PlayerController!=null)PlayerController.enabled=false;if(PlayerInteractor!=null)PlayerInteractor.enabled=false;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
                _texture = new Texture2D(_gdv.Header.Width, _gdv.Header.Height, TextureFormat.RGBA32, false);
                _texture.filterMode = FilterMode.Point; _texture.wrapMode = TextureWrapMode.Clamp;
                if (_gdv.AudioSamples != null && _gdv.AudioSamples.Length > 0)
                {
                    _audio = GetComponent<AudioSource>(); if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
                    _audio.playOnAwake = false; _audio.spatialBlend = 0f;
                    int channels = Mathf.Max(1, _gdv.AudioChannels); int frames = Mathf.Max(1, _gdv.AudioSamples.Length / channels);
                    _clip = AudioClip.Create("ROTH GDV Audio", frames, channels, Mathf.Max(8000, _gdv.AudioRate), false);
                    _clip.SetData(_gdv.AudioSamples, 0); _audio.clip = _clip; _audio.Play();
                }
                SetSubtitles(subtitles);
                _startTime = Time.unscaledTime; IsPlaying = true; AdvanceToFrame(0); return true;
            }
            catch (Exception e) { Debug.LogException(e, this); StopCurrent(false); return false; }
        }

        private void Update()
        {
            if (!IsPlaying || _gdv == null) return;
            if (AllowSkip && Input.GetKeyDown(SkipKey)) { FinishCurrent(); return; }
            int rate = Mathf.Max(1, _gdv.Header.FrameRate); int target = Mathf.FloorToInt((Time.unscaledTime - _startTime) * rate);
            if (target >= _gdv.Frames.Count) { FinishCurrent(); return; }
            if (target > _frame) AdvanceToFrame(target); UpdateSubtitle(Time.unscaledTime-_startTime);
        }

        private void AdvanceToFrame(int target)
        {
            if (_gdv == null) return;
            target = Mathf.Clamp(target, 0, _gdv.Frames.Count - 1);
            while (_frame < target)
            {
                _frame++; _pixels = _decoder.DecodeNext();
            }
            if (_pixels == null) return;
            Color32[] rgba = new Color32[_pixels.Length];
            for (int i=0;i<_pixels.Length;i++)
            {
                int p=_pixels[i]*3; byte r=Expand(_palette[p]),g=Expand(_palette[p+1]),b=Expand(_palette[p+2]); rgba[i]=new Color32(r,g,b,255);
            }
            _texture.SetPixels32(rgba); _texture.Apply(false,false);
        }
        private static byte Expand(byte v){return (byte)((v*259+33)>>6);}


        private void UpdateSubtitle(float elapsed)
        {
            _subtitleText=null;if(_subtitles.Count==0||SubtitleTicksPerSecond<=0f)return;float tick=elapsed*SubtitleTicksPerSecond;
            for(int i=0;i<_subtitles.Count;i++)
            {
                if(_subtitles[i].Timestamp>tick)break;
                ushort next=(ushort)(i+1<_subtitles.Count?_subtitles[i+1].Timestamp:_subtitles[i].Timestamp+(ushort)Mathf.Max(16f,SubtitleTicksPerSecond*4f));
                if(tick<next&&!string.IsNullOrWhiteSpace(_subtitles[i].Text))_subtitleText=_subtitles[i].Text;
            }
        }

        private void OnGUI()
        {
            if(!IsPlaying || _texture==null)return;
            GUI.depth=-10000; GUI.color=Color.black; GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture,ScaleMode.StretchToFill);
            float aspect=(float)_texture.width/_texture.height; float w=Screen.width,h=w/aspect;if(h>Screen.height){h=Screen.height;w=h*aspect;}
            Rect r=new Rect((Screen.width-w)*0.5f,(Screen.height-h)*0.5f,w,h); GUI.color=Color.white; GUI.DrawTexture(r,_texture,ScaleMode.StretchToFill,false);
            if(!string.IsNullOrEmpty(_subtitleText)){GUIStyle st=new GUIStyle(GUI.skin.box);st.fontSize=Mathf.Clamp(Screen.height/32,16,34);st.alignment=TextAnchor.MiddleCenter;st.wordWrap=true;st.normal.textColor=Color.white;GUI.Box(new Rect(Screen.width*0.1f,Screen.height*0.78f,Screen.width*0.8f,Screen.height*0.16f),_subtitleText,st);}
        }

        public void Stop(bool notify=true)
        {
            _pending.Clear();
            StopCurrent(notify);
        }

        private void FinishCurrent()
        {
            StopCurrent(false);
            if(_pending.Count>0)
            {
                PendingGdv next=_pending.Dequeue();
                if(PlayImmediate(next.Path,next.Subtitles))return;
                FinishCurrent();return;
            }
            if(PlaybackFinished!=null)PlaybackFinished();
        }

        private void StopCurrent(bool notify)
        {
            bool was=IsPlaying; IsPlaying=false;
            if(_audio!=null)_audio.Stop(); if(_clip!=null){Destroy(_clip);_clip=null;} if(_texture!=null){Destroy(_texture);_texture=null;}
            _gdv=null;_decoder=null;_pixels=null;_palette=null;_frame=-1;CurrentPath=null;_subtitleText=null;_subtitles.Clear();
            if(PlayerController!=null)PlayerController.enabled=true;if(PlayerInteractor!=null)PlayerInteractor.enabled=true;
            if(was&&notify&&PlaybackFinished!=null)PlaybackFinished();
        }
        private void OnDestroy(){Stop(false);}
    }
}
