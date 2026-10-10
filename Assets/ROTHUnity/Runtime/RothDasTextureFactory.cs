using System;
using System.Collections.Generic;
using ROTHUnity.Core;
using UnityEngine;

namespace ROTHUnity.Runtime
{
    public sealed class RothDasTextureFactory : IDisposable
    {
        private readonly RothDasArchive _archive;
        private readonly Dictionary<int, Texture2D> _textures = new Dictionary<int, Texture2D>();
        private readonly Dictionary<int, RothDasImage> _images = new Dictionary<int, RothDasImage>();
        private readonly Dictionary<int, Material> _materials = new Dictionary<int, Material>();
        private readonly Dictionary<int, Material> _paletteMaterials = new Dictionary<int, Material>();
        private readonly Dictionary<long, Texture2D> _packTextures = new Dictionary<long, Texture2D>();
        private readonly Dictionary<long, Material> _packMaterials = new Dictionary<long, Material>();
        private readonly Dictionary<int, bool> _textureTransparency = new Dictionary<int, bool>();
        private readonly Dictionary<int, Texture2D[]> _animationTextures = new Dictionary<int, Texture2D[]>();
        private readonly Dictionary<int, float> _animationFps = new Dictionary<int, float>();
        private readonly Material _fallback;

        public RothDasTextureFactory(string dasPath, Material fallback)
        {
            _archive = new RothDasArchive(dasPath);
            _fallback = fallback;
        }

        public RothDasHeader Header { get { return _archive.Header; } }

        public bool TryGetTextureSize(int index, out int width, out int height)
        {
            Texture2D tex = GetTexture(index);
            if (tex != null) { width = tex.width; height = tex.height; return true; }
            width = height = 128;
            return false;
        }

        public RothDasImage GetImage(int index)
        {
            RothDasImage image;
            if (_images.TryGetValue(index, out image)) return image;
            image = _archive.ReadImageFirstFrame(index);
            if (image != null) _images[index] = image;
            return image;
        }

        public Texture2D GetTexture(int index)
        {
            Texture2D tex;
            if (_textures.TryGetValue(index, out tex)) return tex;
            RothDasImage image = GetImage(index);
            if (image == null) return null;

            var colors = new Color32[image.Pixels.Length];
            byte[] pal = _archive.PaletteRgb;
            for (int i = 0; i < image.Pixels.Length; i++)
            {
                int p = image.Pixels[i];
                byte alpha = (byte)(image.IsTransparent && p == 0 ? 0 : 255);
                colors[i] = new Color32(pal[p * 3], pal[p * 3 + 1], pal[p * 3 + 2], alpha);
            }

            tex = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false, false);
            tex.name = string.IsNullOrEmpty(image.Name) ? "ROTH_DAS_" + index : image.Name;
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.SetPixels32(colors);
            tex.Apply(false, false);
            _textures[index] = tex;
            _textureTransparency[index] = image.IsTransparent;
            return tex;
        }

        public Texture2D[] GetAnimationTextures(int index, out float framesPerSecond)
        {
            Texture2D[] cached;
            if (_animationTextures.TryGetValue(index, out cached))
            {
                _animationFps.TryGetValue(index, out framesPerSecond);
                return cached;
            }
            var frames=_archive.ReadAnimationFrames(index);
            if(frames==null || frames.Count<2) { framesPerSecond=0f; return null; }
            cached=new Texture2D[frames.Count];
            for(int f=0;f<frames.Count;f++) cached[f]=CreateTexture(frames[f], "ROTH Anim "+index+":"+f);
            int speed=frames[0].AnimationSpeed==0 ? 1 : frames[0].AnimationSpeed;
            framesPerSecond=Mathf.Round(0.05f*speed*speed - 2.50f*speed + 30f);
            if(framesPerSecond<=0f) framesPerSecond=30f;
            _animationTextures[index]=cached; _animationFps[index]=framesPerSecond;
            return cached;
        }

        private Texture2D CreateTexture(RothDasImage image, string name)
        {
            var colors=new Color32[image.Pixels.Length]; byte[] pal=_archive.PaletteRgb;
            for(int i=0;i<image.Pixels.Length;i++) { int p=image.Pixels[i]; byte a=(byte)(image.IsTransparent && p==0?0:255); colors[i]=new Color32(pal[p*3],pal[p*3+1],pal[p*3+2],a); }
            Texture2D tex=new Texture2D(image.Width,image.Height,TextureFormat.RGBA32,false,false); tex.name=name; tex.filterMode=FilterMode.Point; tex.wrapMode=TextureWrapMode.Repeat; tex.SetPixels32(colors); tex.Apply(false,false); return tex;
        }

        public RothDasObjectData GetObjectData(int index)
        {
            return _archive.ReadObjectData(index);
        }

        public RothDasDirectionalViews GetDirectionalViews(int index)
        {
            return _archive.ReadDirectionalViews(index);
        }

        public Texture2D GetImagePackTexture(int index, int subImageIndex)
        {
            long key = ((long)index << 32) | (uint)subImageIndex;
            Texture2D tex;
            if (_packTextures.TryGetValue(key, out tex)) return tex;
            Material material = GetImagePackMaterial(index, subImageIndex);
            if (material == null) return null;
            return _packTextures.TryGetValue(key, out tex) ? tex : material.mainTexture as Texture2D;
        }

        public Material GetImagePackMaterial(int index, int subImageIndex)
        {
            long key = ((long)index << 32) | (uint)subImageIndex;
            Material m;
            if (_packMaterials.TryGetValue(key, out m)) return m;
            RothDasImage image = _archive.ReadImagePackSubImage(index, subImageIndex);
            if (image == null) return null;
            Texture2D tex;
            if (!_packTextures.TryGetValue(key, out tex))
            {
                var colors = new Color32[image.Pixels.Length];
                byte[] pal = _archive.PaletteRgb;
                for (int i=0;i<image.Pixels.Length;i++)
                {
                    int p=image.Pixels[i];
                    byte alpha=(byte)(image.IsTransparent && p==0 ? 0 : 255);
                    colors[i]=new Color32(pal[p*3],pal[p*3+1],pal[p*3+2],alpha);
                }
                tex=new Texture2D(image.Width,image.Height,TextureFormat.RGBA32,false,false);
                tex.name="ROTH Pack "+index+":"+subImageIndex;
                tex.filterMode=FilterMode.Point; tex.wrapMode=TextureWrapMode.Repeat;
                tex.SetPixels32(colors); tex.Apply(false,false);
                _packTextures[key]=tex;
            }
            Shader shader=Shader.Find(image.IsTransparent ? "Unlit/Transparent" : "Unlit/Texture");
            if(shader==null) shader=Shader.Find("Universal Render Pipeline/Unlit");
            if(shader==null) shader=Shader.Find("Standard");
            if(shader==null) return _fallback;
            m=new Material(shader); m.name="ROTH Pack Material "+index+":"+subImageIndex; m.mainTexture=tex;
            _packMaterials[key]=m;
            return m;
        }

        public Material GetPaletteMaterial(int paletteIndex)
        {
            if (paletteIndex < 0 || paletteIndex > 255) return _fallback;
            Material m;
            if (_paletteMaterials.TryGetValue(paletteIndex, out m)) return m;
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return _fallback;
            byte[] pal = _archive.PaletteRgb;
            m = new Material(shader);
            m.name = "ROTH Palette " + paletteIndex;
            m.color = new Color32(pal[paletteIndex * 3], pal[paletteIndex * 3 + 1], pal[paletteIndex * 3 + 2], 255);
            _paletteMaterials[paletteIndex] = m;
            return m;
        }

        public Material GetMaterial(int index)
        {
            Material m;
            if (_materials.TryGetValue(index, out m)) return m;
            Texture2D tex = GetTexture(index);
            if (tex == null) return _fallback;

            bool transparent;
            _textureTransparency.TryGetValue(index, out transparent);
            Shader shader = Shader.Find(transparent ? "Unlit/Transparent" : "Unlit/Texture");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return _fallback;

            m = new Material(shader);
            m.name = "ROTH Material " + index + " " + tex.name;
            m.mainTexture = tex;
            _materials[index] = m;
            return m;
        }

        public void Dispose()
        {
            _archive.Dispose();
            foreach (Material m in _materials.Values) DestroyObject(m);
            foreach (Material m in _paletteMaterials.Values) DestroyObject(m);
            foreach (Material m in _packMaterials.Values) DestroyObject(m);
            foreach (Texture2D t in _textures.Values) DestroyObject(t);
            foreach (Texture2D t in _packTextures.Values) DestroyObject(t);
            foreach (Texture2D[] arr in _animationTextures.Values) foreach (Texture2D t in arr) DestroyObject(t);
            _materials.Clear();
            _paletteMaterials.Clear();
            _packMaterials.Clear();
            _textures.Clear();
            _packTextures.Clear();
            _images.Clear();
            _textureTransparency.Clear();
            _animationTextures.Clear();
            _animationFps.Clear();
        }

        private static void DestroyObject(UnityEngine.Object o)
        {
            if (o == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(o);
            else UnityEngine.Object.Destroy(o);
#else
            UnityEngine.Object.Destroy(o);
#endif
        }
    }
}
