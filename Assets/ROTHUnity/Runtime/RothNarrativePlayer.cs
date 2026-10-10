using System;
using System.Collections.Generic;
using UnityEngine;
using ROTHUnity.Core;

namespace ROTHUnity.Runtime
{
    /// <summary>
    /// Runtime surface for DBASE400/500 dialogue, DBASE300 display images and DBASE100 choices.
    /// IMGUI keeps the prototype independent from optional Unity UI packages.
    /// </summary>
    public sealed class RothNarrativePlayer : MonoBehaviour
    {
        public string CurrentText { get; private set; }
        public bool Visible { get { return !string.IsNullOrEmpty(CurrentText) || _choices.Count>0 || _displayTexture!=null; } }
        public float DefaultTextSeconds=4f;

        private float _until;
        private AudioSource _voice;
        private AudioClip _clip;
        private readonly Queue<RothDBaseTextEntry> _textQueue=new Queue<RothDBaseTextEntry>();
        private readonly List<string> _choices=new List<string>();
        private Action<int> _choiceCallback;
        private List<string> _pendingChoices;
        private Action<int> _pendingChoiceCallback;
        private Texture2D _displayTexture;
        private float _imageUntil;
        private RothFirstPersonController _pausedFps;
        private RothPlayerInteractor _pausedInteractor;

        public void Show(RothDBaseTextEntry entry,float seconds=0f)
        {
            if(entry==null)return;
            if(IsTextBusy())
            {
                _textQueue.Enqueue(entry);
                return;
            }
            BeginText(entry,seconds);
        }

        private bool IsTextBusy()
        {
            return !string.IsNullOrEmpty(CurrentText) || (_voice!=null && _voice.isPlaying);
        }

        private void BeginText(RothDBaseTextEntry entry,float seconds)
        {
            CurrentText=entry.Text??"";
            float duration=seconds>0f?seconds:Mathf.Max(DefaultTextSeconds,CurrentText.Length*0.055f);
            _until=Time.unscaledTime+duration;
            if(entry.VoiceSamples!=null && entry.VoiceSamples.Length>0)
            {
                if(_voice==null){_voice=GetComponent<AudioSource>();if(_voice==null)_voice=gameObject.AddComponent<AudioSource>();}
                _voice.spatialBlend=0f;_voice.playOnAwake=false;
                DestroyVoiceClip();
                _clip=AudioClip.Create("ROTH Dialogue",entry.VoiceSamples.Length,1,Mathf.Max(8000,entry.VoiceRate),false);
                _clip.SetData(entry.VoiceSamples,0);_voice.clip=_clip;_voice.Play();
                _until=Mathf.Max(_until,Time.unscaledTime+_clip.length+0.2f);
            }
        }

        public void ShowChoices(IList<string> choices,Action<int> callback)
        {
            var copy=new List<string>();if(choices!=null)for(int i=0;i<choices.Count;i++)copy.Add(choices[i]);
            if(IsTextBusy() || _textQueue.Count>0)
            {
                _pendingChoices=copy;_pendingChoiceCallback=callback;
                return;
            }
            BeginChoices(copy,callback);
        }

        private void BeginChoices(IList<string> choices,Action<int> callback)
        {
            _choices.Clear();if(choices!=null)for(int i=0;i<choices.Count;i++)_choices.Add(choices[i]);
            _choiceCallback=callback;_until=float.PositiveInfinity;SetModal(_choices.Count>0);
        }

        private void Update()
        {
            if(_choices.Count==0 && IsTextBusy() && Time.unscaledTime>=_until)
                FinishCurrentText();
            if(_displayTexture!=null && Time.unscaledTime>=_imageUntil) ClearImage();
            if(_displayTexture!=null && Input.GetKeyDown(KeyCode.Escape)) ClearImage();
        }

        private void FinishCurrentText()
        {
            CurrentText=null;_until=0f;if(_voice!=null)_voice.Stop();DestroyVoiceClip();
            if(_textQueue.Count>0){BeginText(_textQueue.Dequeue(),0f);return;}
            if(_pendingChoices!=null)
            {
                List<string> p=_pendingChoices;Action<int> cb=_pendingChoiceCallback;
                _pendingChoices=null;_pendingChoiceCallback=null;BeginChoices(p,cb);
            }
        }

        public bool ShowDBase300Image(string dbase300Path,int gameOffsetUnits,float seconds=6f)
        {
            RothDBase300Image img=RothDBase300Archive.ReadDisplayImage(dbase300Path,gameOffsetUnits);
            if(img==null)return false;
            ClearImage();_displayTexture=RothDBase300Archive.CreateTexture(img);
            if(_displayTexture==null)return false;
            _imageUntil=Time.unscaledTime+Mathf.Max(0.5f,seconds);return true;
        }

        private void OnGUI()
        {
            if(!Visible)return;GUI.depth=-9000;
            float width=Mathf.Min(Screen.width-40f,900f),x=(Screen.width-width)*0.5f;
            if(!string.IsNullOrEmpty(CurrentText))GUI.Box(new Rect(x,Screen.height-150,width,110),CurrentText);
            if(_displayTexture!=null)
            {
                float maxW=Screen.width*0.9f,maxH=Screen.height*0.9f;
                float scale=Mathf.Min(maxW/_displayTexture.width,maxH/_displayTexture.height);
                scale=Mathf.Max(1f,Mathf.Floor(scale));float w=_displayTexture.width*scale,h=_displayTexture.height*scale;
                GUI.DrawTexture(new Rect((Screen.width-w)*0.5f,(Screen.height-h)*0.5f,w,h),_displayTexture,ScaleMode.StretchToFill,false);
            }
            if(_choices.Count>0)
            {
                float h=42f*_choices.Count+20f,y=Mathf.Max(20f,(Screen.height-h)*0.5f);GUI.Box(new Rect(x,y,width,h),"");
                for(int i=0;i<_choices.Count;i++)
                    if(GUI.Button(new Rect(x+10,y+10+i*42,width-20,36),_choices[i]))
                    {
                        Action<int> cb=_choiceCallback;_choices.Clear();_choiceCallback=null;SetModal(false);if(cb!=null)cb(i);break;
                    }
            }
        }

        private void DestroyVoiceClip(){if(_clip!=null){Destroy(_clip);_clip=null;}}
        public void Clear(bool keepChoices=false)
        {
            CurrentText=null;_until=0f;if(_voice!=null)_voice.Stop();DestroyVoiceClip();_textQueue.Clear();
            if(!keepChoices){_choices.Clear();_choiceCallback=null;_pendingChoices=null;_pendingChoiceCallback=null;SetModal(false);}
        }
        public void ClearImage(){_imageUntil=0f;if(_displayTexture!=null){Destroy(_displayTexture);_displayTexture=null;}}
        private void SetModal(bool value)
        {
            if(value)
            {
                if(_pausedFps==null)_pausedFps=GetComponentInChildren<RothFirstPersonController>();
                if(_pausedInteractor==null)_pausedInteractor=GetComponentInChildren<RothPlayerInteractor>();
                if(_pausedFps!=null)_pausedFps.enabled=false;if(_pausedInteractor!=null)_pausedInteractor.enabled=false;
                Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            }
            else
            {
                if(_pausedFps!=null)_pausedFps.enabled=true;if(_pausedInteractor!=null)_pausedInteractor.enabled=true;
            }
        }
        private void OnDestroy(){SetModal(false);Clear(false);ClearImage();}
    }
}
