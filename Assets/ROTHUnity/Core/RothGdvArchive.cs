using System;
using System.Collections.Generic;
using System.IO;

namespace ROTHUnity.Core
{
    public sealed class RothGdvHeader
    {
        public uint Signature;
        public ushort SizeId, FrameCount, FrameRate, SoundFlags, PlaybackFrequency, ImageType, FrameSize;
        public byte Unknown, Lossyness;
        public ushort Width, Height;
        public bool HasAudio { get { return (SoundFlags & 1) != 0; } }
        public bool Stereo { get { return (SoundFlags & 2) != 0; } }
        public bool Sample16 { get { return (SoundFlags & 4) != 0; } }
        public bool Dpcm { get { return (SoundFlags & 8) != 0; } }
    }

    public sealed class RothGdvFrame
    {
        public ushort Signature, Length;
        public uint TypeFlags;
        public byte[] Data;
    }

    /// <summary>Read-only reader/decoder for the 8-bit GDV variant used by ROTH.</summary>
    public sealed class RothGdvArchive
    {
        public const uint ExpectedSignature = 0x29111994;
        public RothGdvHeader Header { get; private set; }
        public byte[] Palette { get; private set; }
        public IReadOnlyList<RothGdvFrame> Frames { get { return _frames; } }
        public float[] AudioSamples { get; private set; }
        public int AudioChannels { get; private set; }
        public int AudioRate { get; private set; }

        private readonly List<RothGdvFrame> _frames = new List<RothGdvFrame>();
        private static readonly int[] DeltaTable = BuildDeltaTable();

        public static RothGdvArchive Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var g = new RothGdvArchive();
            g.Read(path);
            return g;
        }

        private void Read(string path)
        {
            using (var fs = File.OpenRead(path))
            using (var br = new BinaryReader(fs))
            {
                Header = new RothGdvHeader
                {
                    Signature = br.ReadUInt32(), SizeId = br.ReadUInt16(), FrameCount = br.ReadUInt16(),
                    FrameRate = br.ReadUInt16(), SoundFlags = br.ReadUInt16(), PlaybackFrequency = br.ReadUInt16(),
                    ImageType = br.ReadUInt16(), FrameSize = br.ReadUInt16(), Unknown = br.ReadByte(), Lossyness = br.ReadByte(),
                    Width = br.ReadUInt16(), Height = br.ReadUInt16()
                };
                if (Header.Signature != ExpectedSignature) throw new InvalidDataException("Not a GDV file: " + path);
                if ((Header.ImageType & 1) == 0) throw new NotSupportedException("Only 8-bit paletted GDV is supported currently.");
                Palette = br.ReadBytes(256 * 3);
                if (Palette.Length != 768) throw new EndOfStreamException("Truncated GDV palette.");

                int audioBytesPerFrame = AudioBytesPerFrame(Header);
                var audio = new List<float>();
                int leftState = 0, rightState = 0, monoState = 0;
                for (int i = 0; i < Header.FrameCount && fs.Position < fs.Length; i++)
                {
                    if (Header.HasAudio && audioBytesPerFrame > 0)
                    {
                        byte[] rawAudio = br.ReadBytes(audioBytesPerFrame);
                        DecodeAudio(rawAudio, Header, audio, ref leftState, ref rightState, ref monoState);
                    }
                    if (Header.FrameSize != 0)
                    {
                        if (fs.Position + 8 > fs.Length) break;
                        var f = new RothGdvFrame { Signature = br.ReadUInt16(), Length = br.ReadUInt16(), TypeFlags = br.ReadUInt32() };
                        f.Data = br.ReadBytes(f.Length);
                        _frames.Add(f);
                    }
                }
                AudioChannels = Header.Stereo ? 2 : 1;
                AudioRate = Header.PlaybackFrequency > 0 ? Header.PlaybackFrequency : 22050;
                AudioSamples = audio.ToArray();
            }
        }

        private static int AudioBytesPerFrame(RothGdvHeader h)
        {
            if (!h.HasAudio || h.FrameRate == 0) return 0;
            int amount = h.PlaybackFrequency / h.FrameRate;
            if (h.Stereo) amount *= 2;
            if (h.Sample16) amount *= 2;
            if (h.Dpcm) amount >>= 1;
            return Math.Max(0, amount);
        }

        private static void DecodeAudio(byte[] raw, RothGdvHeader h, List<float> dst, ref int left, ref int right, ref int mono)
        {
            if (raw == null || raw.Length == 0) return;
            if (h.Dpcm)
            {
                if (h.Stereo)
                {
                    for (int i = 0; i + 1 < raw.Length; i += 2)
                    {
                        left = Wrap16(left + DeltaTable[raw[i]]); right = Wrap16(right + DeltaTable[raw[i + 1]]);
                        dst.Add(left / 32768f); dst.Add(right / 32768f);
                    }
                }
                else
                {
                    for (int i = 0; i < raw.Length; i++) { mono = Wrap16(mono + DeltaTable[raw[i]]); dst.Add(mono / 32768f); }
                }
                return;
            }
            if (h.Sample16)
            {
                for (int i = 0; i + 1 < raw.Length; i += 2)
                {
                    short s = (short)(raw[i] | (raw[i + 1] << 8)); dst.Add(s / 32768f);
                }
            }
            else
            {
                for (int i = 0; i < raw.Length; i++) dst.Add((raw[i] - 128) / 128f);
            }
        }

        public RothGdvDecoderSession CreateDecoder() { return new RothGdvDecoderSession(this); }

        public byte[] DecodeFrame(int frameIndex, byte[] previousPixels, byte[] workingPalette)
        {
            var d=CreateDecoder();byte[] result=null;
            for(int i=0;i<=frameIndex&&i<_frames.Count;i++)result=d.DecodeNext();
            if(workingPalette!=null&&workingPalette.Length>=768)Buffer.BlockCopy(d.Palette,0,workingPalette,0,768);
            return result??previousPixels;
        }

        public sealed class RothGdvDecoderSession
        {
            private readonly RothGdvArchive _owner;
            private readonly byte[] _frame;
            private int _next;
            private bool _scaleV,_scaleH;
            public byte[] Palette { get; private set; }
            internal RothGdvDecoderSession(RothGdvArchive owner)
            {
                _owner=owner;Palette=(byte[])owner.Palette.Clone();_frame=new byte[4096+owner.Header.Width*owner.Header.Height];
                int p=0;for(int block=0;block<2;block++)for(int c=0;c<256;c++)for(int k=0;k<8;k++)_frame[p++]=(byte)c;
            }
            public byte[] DecodeNext()
            {
                if(_next>=_owner._frames.Count)return null;RothGdvFrame f=_owner._frames[_next++];int comp=(int)(f.TypeFlags&15);bool newV=(f.TypeFlags&0x10)!=0,newH=(f.TypeFlags&0x20)!=0;
                Rescale(_frame,_owner.Header.Width,_owner.Header.Height,_scaleV,_scaleH,newV,newH);_scaleV=newV;_scaleH=newH;
                int skip=(int)(f.TypeFlags>>8),cur=4096+skip,end=_frame.Length;
                if(comp==1)
                {
                    if(f.Data!=null&&f.Data.Length>=768)Buffer.BlockCopy(f.Data,0,Palette,0,768);
                    byte fill=(byte)(skip==0?0:255);for(int i=4096;i<_frame.Length;i++)_frame[i]=fill;
                }
                else if(comp==3){}
                else if(comp==5&&f.Data!=null)DecodeType5(f.Data,_frame,cur,end);
                else if(comp==8&&f.Data!=null&&f.Data.Length>=4)
                {
                    var r=new BitReader(f.Data);int guard=_frame.Length*4;
                    while(cur<end&&guard-->0)
                    {
                        int tag=r.GetBits(2);bool ok=tag==0?DecodeTag0(r,_frame,ref cur,end):tag==1?DecodeTag1(r,ref cur,end):tag==2?DecodeTag2(r,_frame,ref cur,end):DecodeTag3(r,_frame,ref cur,end);
                        if(!ok||r.EndOfData)break;
                    }
                }
                return Export(_frame,_owner.Header.Width,_owner.Header.Height,_scaleV,_scaleH);
            }
        }

        private static byte[] Export(byte[] frame,int w,int h,bool scaleV,bool scaleH)
        {
            byte[] dst=new byte[w*h];int sidx=4096,didx=0;
            if(!scaleV&&!scaleH){Buffer.BlockCopy(frame,4096,dst,0,dst.Length);return dst;}
            for(int y=0;y<h;y++)
            {
                if(!scaleV)Buffer.BlockCopy(frame,sidx,dst,didx,w);
                else ScaleUp(dst,didx,frame,sidx,w);
                if(!scaleH||(y&1)==1)sidx+=!scaleV?w:w/2;didx+=w;
            }
            return dst;
        }
        private static void Rescale(byte[] frame,int w,int h,bool oldV,bool oldH,bool newV,bool newH)
        {
            if(oldV==newV&&oldH==newH)return;
            if(oldV)
            {
                for(int j=0;j<h;j++){int y=h-j-1;int dst=4096+y*w;int src=4096+(y>>(oldH?1:0))*(w/2);ScaleUpReverse(frame,dst,frame,src,w);}
            }
            else if(oldH)
            {
                for(int j=0;j<h;j++){int y=h-j-1;Buffer.BlockCopy(frame,4096+(y>>1)*w,frame,4096+y*w,w);}
            }
            if(newH&&newV)
            {
                for(int y=0;y<h/2;y++)ScaleDown(frame,4096+y*(w/2),frame,4096+y*2*w,w/2);
            }
            else if(newH)
            {
                for(int y=0;y<h/2;y++)Buffer.BlockCopy(frame,4096+y*2*w,frame,4096+y*w,w);
            }
            else if(newV)
            {
                for(int y=0;y<h;y++)ScaleDown(frame,4096+y*w,frame,4096+y*w,w/2);
            }
        }
        private static void ScaleUp(byte[] dst,int di,byte[] src,int si,int w){for(int x=0;x<w;x++)dst[di+x]=src[si+(x>>1)];}
        private static void ScaleUpReverse(byte[] dst,int di,byte[] src,int si,int w){for(int x=w-1;x>=0;x--)dst[di+x]=src[si+(x>>1)];}
        private static void ScaleDown(byte[] dst,int di,byte[] src,int si,int halfW){for(int x=0;x<halfW;x++)dst[di+x]=src[si+x*2];}



        private static void DecodeType5(byte[] data, byte[] pixels, int start, int end)
        {
            var r=new BitReader8(data); int cur=Math.Max(4096,start); int guard=pixels.Length*3;
            while(cur<end && !r.EndOfData && guard-->0)
            {
                int tag=r.GetTag(); if(r.EndOfData)break;
                if(tag==0){if(cur<end)pixels[cur++]=(byte)r.GetByte();}
                else if(tag==1)
                {
                    int b=r.GetByte();int len=(b&15)+3;int top=(b>>4)&15;int off=(r.GetByte()<<4)+top-4096;if(!CopyPixels(pixels,ref cur,off,len,end))break;
                }
                else if(tag==2)
                {
                    int b=r.GetByte();if(b==0)break;int len=b!=255?b:(r.GetByte()|(r.GetByte()<<8));cur+=len+1;if(cur>end)cur=end;
                }
                else
                {
                    int b=r.GetByte();int len=(b&3)+2;int off=-(b>>2)-1;if(!CopyPixels(pixels,ref cur,off,len,end))break;
                }
            }
        }

        private static bool DecodeTag0(BitReader r, byte[] px, ref int cur, int end)
        {
            if (r.GetBits(1) == 0) return WriteByte(r, px, ref cur, end);
            int length = 2, count = 0;
            while (count < 16)
            {
                count++; int step = r.GetBits(count); length += step;
                if (step != ((1 << count) - 1)) break;
            }
            for (int i = 0; i < length; i++) if (!WriteByte(r, px, ref cur, end)) return false;
            return true;
        }
        private static bool DecodeTag1(BitReader r, ref int cur, int end)
        {
            if (r.GetBits(1) == 0) cur += r.GetBits(4) + 2;
            else { int length = r.GetByte(); cur += (length & 0x80) == 0 ? length + 18 : (((length & 0x7F) << 8) | r.GetByte()) + 146; }
            if (cur > end) cur = end; return true;
        }
        private static bool DecodeTag2(BitReader r, byte[] px, ref int cur, int end)
        {
            int sub = r.GetBits(2), offset, length;
            if (sub == 3)
            {
                offset = r.GetByte(); length = 2 + (((offset & 0x80) != 0) ? 1 : 0); offset &= 0x7F;
                if (offset == 0) { for (int i = 0; i < length; i++) { if (cur >= end) return false; px[cur] = cur == 0 ? (byte)255 : px[cur - 1]; cur++; } return true; }
                return CopyPixels(px, ref cur, -(offset + 1), length, end);
            }
            int next4 = r.GetBits(4), next = r.GetByte(); offset = (next4 << 8) | next;
            if (sub == 0 && offset == 0xFFF) return false;
            if (sub == 0 && offset > 0xF80)
            {
                length = (offset & 0x0F) + 2; offset = (offset >> 4) & 7;
                int a = cur - (offset + 1), b = cur - offset;
                if (a < 0 || b < 0) return false;
                byte p1 = px[a], p2 = px[b];
                for (int i = 0; i < length; i++) { if (cur + 1 >= end) return false; px[cur++] = p1; px[cur++] = p2; }
                return true;
            }
            length = sub + 3;
            if (offset == 0xFFF) { for (int i = 0; i < length; i++) { if (cur >= end) return false; px[cur] = cur == 0 ? (byte)255 : px[cur - 1]; cur++; } return true; }
            return CopyPixels(px, ref cur, offset-4096, length, end);
        }
        private static bool DecodeTag3(BitReader r, byte[] px, ref int cur, int end)
        {
            int first = r.GetByte(), length, offset;
            if ((first & 0xC0) == 0xC0) { length = (first & 0x3F) + 8; offset = (r.GetBits(4) << 8) | r.GetByte(); return CopyPixels(px, ref cur, offset + 1, length, end); }
            if ((first & 0x80) == 0) { length = (first >> 4) + 6; offset = ((first & 0x0F) << 8) | r.GetByte(); }
            else { length = 14 + (first & 0x3F); offset = (r.GetBits(4) << 8) | r.GetByte(); }
            if (offset == 0xFFF) { for (int i = 0; i < length; i++) { if (cur >= end) return false; px[cur] = cur == 0 ? (byte)255 : px[cur - 1]; cur++; } return true; }
            return CopyPixels(px, ref cur, offset-4096, length, end);
        }
        private static bool WriteByte(BitReader r, byte[] px, ref int cur, int end) { if (cur >= end || r.EndOfData) return false; px[cur++] = (byte)r.GetByte(); return true; }
        private static bool CopyPixels(byte[] px, ref int cur, int offset, int length, int end = -1)
        {
            if(end<0||end>px.Length)end=px.Length;
            for (int i = 0; i < length; i++) { int src = cur + offset; if (cur < 0 || cur >= end || src < 0 || src >= px.Length) return false; px[cur++] = px[src]; }
            return true;
        }
        private static int Wrap16(int v) { while (v > 32767) v -= 65536; while (v < -32768) v += 65536; return v; }
        private static int[] BuildDeltaTable()
        {
            int[] t = new int[256]; int delta = 0, code = 64, step = 45; t[0] = 0;
            for (int i = 1; i < 254; i += 2) { delta += (code >> 5); code += step; step += 2; t[i] = delta; t[i + 1] = -delta; }
            t[255] = delta + (code >> 5); return t;
        }
        private sealed class BitReader8
        {
            private readonly byte[] _d; private int _pos; private int _queue; private int _fill;
            public bool EndOfData { get { return _pos>=_d.Length && _fill==0; } }
            public BitReader8(byte[] d){_d=d??new byte[0];}
            public int GetTag(){if(_fill==0){if(_pos>=_d.Length)return 3;_queue=_d[_pos++];_fill=8;}int v=(_queue>>6)&3;_queue=(_queue<<2)&255;_fill-=2;return v;}
            public int GetByte(){return _pos<_d.Length?_d[_pos++]:0;}
        }

        private sealed class BitReader
        {
            private readonly byte[] _d;
            private int _pos;
            private uint _queue;
            private int _size;
            public bool EndOfData { get { return _pos >= _d.Length && _size <= 0; } }
            public BitReader(byte[] data)
            {
                _d=data??new byte[0]; _pos=0; _queue=0; _size=0;
                if(_d.Length>=4)
                {
                    _queue=(uint)(_d[0]|(_d[1]<<8)|(_d[2]<<16)|(_d[3]<<24));
                    _pos=4; _size=32;
                }
            }
            public int GetBits(int n)
            {
                if(n<=0)return 0;
                uint mask=n>=32?uint.MaxValue:((1u<<n)-1u);
                int val=(int)(_queue&mask); _queue>>=n; _size-=n;
                if(_size<=16 && _pos+1<_d.Length)
                {
                    uint w=(uint)(_d[_pos]|(_d[_pos+1]<<8));
                    _queue|=w<<_size; _pos+=2; _size+=16;
                }
                return val;
            }
            public int GetByte(){return _pos<_d.Length?_d[_pos++]:0;}
        }
    }
}
