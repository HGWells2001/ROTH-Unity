using UnityEngine;

namespace ROTHUnity.Runtime
{
    public sealed class RothAnimatedBillboard : MonoBehaviour
    {
        private MeshRenderer _renderer;
        private Texture2D[] _frames;
        private Material _material;
        private float _fps;
        private float _time;
        private int _frame=-1;
        private bool _billboard=true;

        public void Initialize(MeshRenderer renderer, Texture2D[] frames, float fps, Material template, bool billboard=true)
        {
            _renderer=renderer; _frames=frames; _fps=Mathf.Max(0.1f,fps); _billboard=billboard;
            if(template!=null) _material=new Material(template);
            if(_material!=null) _renderer.sharedMaterial=_material;
            SetFrame(0);
        }
        private void LateUpdate()
        {
            Camera cam=Camera.main;
            if(_billboard && cam!=null) { Vector3 d=cam.transform.position-transform.position; d.y=0; if(d.sqrMagnitude>0.0001f) transform.rotation=Quaternion.LookRotation(-d.normalized,Vector3.up); }
            if(_frames==null || _frames.Length<2) return;
            _time+=Time.deltaTime; SetFrame(Mathf.FloorToInt(_time*_fps)%_frames.Length);
        }
        private void SetFrame(int i)
        {
            if(i==_frame || _material==null || _frames==null || i<0 || i>=_frames.Length) return;
            _frame=i; _material.mainTexture=_frames[i];
        }
        private void OnDestroy()
        {
            if(_material==null) return;
#if UNITY_EDITOR
            if(!Application.isPlaying) DestroyImmediate(_material); else Destroy(_material);
#else
            Destroy(_material);
#endif
        }
    }
}
