using ROTHUnity.Core;
using UnityEngine;

namespace ROTHUnity.Runtime
{
    public sealed class RothDirectionalBillboard : MonoBehaviour
    {
        private MeshRenderer _renderer;
        private RothDasTextureFactory _factory;
        private RothDasDirectionalViews _views;
        private byte _objectRotation;
        private Material _instanceMaterial;
        private int _lastDirection = -1;

        public void Initialize(MeshRenderer renderer, RothDasTextureFactory factory, RothDasDirectionalViews views, byte objectRotation, Material template)
        {
            _renderer = renderer; _factory = factory; _views = views; _objectRotation = objectRotation;
            if (template != null) _instanceMaterial = new Material(template);
            else
            {
                Shader shader = Shader.Find("Unlit/Transparent");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null) _instanceMaterial = new Material(shader);
            }
            if (_instanceMaterial != null) _renderer.sharedMaterial = _instanceMaterial;
            Refresh(true);
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 flat = cam.transform.position - transform.position; flat.y = 0f;
                if (flat.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(-flat.normalized, Vector3.up);
            }
            Refresh(false);
        }

        private void Refresh(bool force)
        {
            if (_renderer == null || _factory == null || _views == null || _instanceMaterial == null) return;
            Camera cam = Camera.main;
            float cameraYaw = cam != null ? cam.transform.eulerAngles.y + 180f : 0f;
            float objectYaw = -(_objectRotation / 256f) * 360f;
            float diff = Mathf.DeltaAngle(objectYaw, cameraYaw);
            int direction = Mathf.RoundToInt(diff / 45f);
            direction = ((direction % 8) + 8) % 8;
            if (!force && direction == _lastDirection) return;
            _lastDirection = direction;

            Texture2D tex = _views.UsesImagePack
                ? _factory.GetImagePackTexture(_views.ResourceIndices[direction], _views.PackSubImageIndices[direction])
                : _factory.GetTexture(_views.ResourceIndices[direction]);
            if (tex == null) return;
            _instanceMaterial.mainTexture = tex;
            _instanceMaterial.mainTextureScale = new Vector2(_views.Flipped[direction] ? -1f : 1f, 1f);
            _instanceMaterial.mainTextureOffset = new Vector2(_views.Flipped[direction] ? 1f : 0f, 0f);
        }

        private void OnDestroy()
        {
            if (_instanceMaterial != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(_instanceMaterial); else Destroy(_instanceMaterial);
#else
                Destroy(_instanceMaterial);
#endif
            }
        }
    }
}
