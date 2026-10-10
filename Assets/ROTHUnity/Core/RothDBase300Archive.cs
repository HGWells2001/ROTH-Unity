using System;
using System.IO;
using UnityEngine;

namespace ROTHUnity.Core
{
    public sealed class RothDBase300Image
    {
        public int Width;
        public int Height;
        public byte[] PaletteRgb;
        public byte[] Pixels;
    }

    /// <summary>Focused DBASE300 reader for runtime narrative assets. 0.10 intentionally supports
    /// the retail DisplayTexture path (IMG1), whose palette is embedded in the record.</summary>
    public static class RothDBase300Archive
    {
        private const uint Img1 = 0x00000001;

        public static RothDBase300Image ReadDisplayImage(string path, int gameOffsetUnits)
        {
            if(string.IsNullOrEmpty(path) || !File.Exists(path) || gameOffsetUnits<=0) return null;
            long offset=(long)gameOffsetUnits*8L;
            using(var fs=File.OpenRead(path))
            using(var br=new BinaryReader(fs))
            {
                if(fs.Length<16) return null;
                string sig=System.Text.Encoding.ASCII.GetString(br.ReadBytes(8));
                if(sig!="DBASE300" || offset<8 || offset+16>fs.Length) return null;
                fs.Position=offset;
                uint size=br.ReadUInt32();
                long end=fs.Position+size;
                if(size<8 || end>fs.Length) return null;
                uint type=br.ReadUInt32();
                if(type!=Img1) return null;
                ushort width=br.ReadUInt16();
                ushort height=br.ReadUInt16();
                if(width==0 || height==0 || width>4096 || height>4096) return null;
                byte[] palette=br.ReadBytes(256*3);
                int expected=width*height;
                byte[] pixels=new byte[expected];
                int outPos=0;
                while(outPos<expected && fs.Position<end)
                {
                    byte value=br.ReadByte();
                    int count=1;
                    if(value>0xF0)
                    {
                        count=value&0x0F;
                        if(fs.Position>=end) return null;
                        value=br.ReadByte();
                    }
                    for(int i=0;i<count && outPos<expected;i++) pixels[outPos++]=value;
                }
                if(outPos!=expected || palette.Length!=768) return null;
                return new RothDBase300Image{Width=width,Height=height,PaletteRgb=palette,Pixels=pixels};
            }
        }

        public static Texture2D CreateTexture(RothDBase300Image image)
        {
            if(image==null || image.Pixels==null || image.PaletteRgb==null) return null;
            var colors=new Color32[image.Pixels.Length];
            for(int i=0;i<colors.Length;i++)
            {
                int p=image.Pixels[i]*3;
                colors[i]=new Color32(image.PaletteRgb[p],image.PaletteRgb[p+1],image.PaletteRgb[p+2],255);
            }
            var tex=new Texture2D(image.Width,image.Height,TextureFormat.RGBA32,false,false);
            tex.name="ROTH DBASE300 DisplayTexture";
            tex.filterMode=FilterMode.Point;
            tex.wrapMode=TextureWrapMode.Clamp;
            tex.SetPixels32(colors);
            tex.Apply(false,false);
            return tex;
        }
    }
}
