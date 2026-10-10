using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ROTHUnity.Core
{
    public sealed class RothDasHeader
    {
        public string Signature;
        public ushort Version, FatSize, FilenamesSize, DirectionalObjectTableSize, Unknown20, SkyIndex;
        public uint FatOffset, PaletteOffset, Unknown10Offset, FilenamesOffset, DirectionalObjectTableOffset;
        public uint ObjectCollisionOffset, MonsterMappingOffset, MonsterMappingSize;
        public ushort FatBlock1Count, FatBlock2Count, FatBlock3Count, FatBlock4Count;
        public uint Unknown38Offset, Unknown40Offset;
        public ushort Unknown38Size, Unknown40Size;
        public int TotalFatCount { get { return FatBlock1Count + FatBlock2Count + FatBlock3Count + FatBlock4Count; } }
    }

    public sealed class RothDasFatEntry
    {
        public int Index;
        public uint Offset;
        public ushort Size;
        public byte Flags1, Flags2;
        public string Name, Description;
    }

    public sealed class RothDasDirectionalViews
    {
        public readonly int[] ResourceIndices = new int[8];
        public readonly int[] PackSubImageIndices = new int[8];
        public readonly bool[] Flipped = new bool[8];
        public bool UsesImagePack;
        public bool IsMonster;
        public RothDasDirectionalViews()
        {
            for (int i=0;i<8;i++) { ResourceIndices[i] = -1; PackSubImageIndices[i] = -1; }
        }
    }

    public sealed class RothDasImage
    {
        public int Index;
        public string Name, Description;
        public byte Modifier, ImageType;
        public byte Flags1, Flags2;
        public short ShiftX, ShiftY;
        public ushort Width, Height;
        public byte AnimationSpeed;
        public byte[] Pixels;
        public bool IsTransparent;
        public bool DrawDownward { get { return (Modifier & 0x10) != 0; } }
        public bool HalfSize { get { return (Modifier & 0x80) != 0; } }
    }


    public sealed class RothDasObjectData
    {
        public int Index;
        public byte Modifier, ImageType;
        public readonly List<RothDasObjectVertex> Vertices = new List<RothDasObjectVertex>();
        public readonly List<RothDasObjectFace> Faces = new List<RothDasObjectFace>();
    }

    public struct RothDasObjectVertex
    {
        public short X, Y, Z;
        public RothDasObjectVertex(short x, short y, short z) { X = x; Y = y; Z = z; }
    }

    public sealed class RothDasObjectFace
    {
        public ushort TextureIndex;
        public byte SubTextureIndex, RenderFlag1, RenderFlag2;
        public ushort[] Edges = Array.Empty<ushort>();
    }

    /// <summary>
    /// Read-only reader for the Gremlin DASP resource archives used by Realms of the Haunting.
    /// Milestone 0.3 supports the DASP header, FAT, embedded palette, filename metadata and
    /// ordinary paletted (non-animation/non-pack/non-object) images. Compressed animations,
    /// image packs and object records are intentionally deferred.
    /// </summary>
    public sealed class RothDasArchive : IDisposable
    {
        private readonly FileStream _stream;
        private readonly BinaryReader _reader;
        private readonly Dictionary<int, RothDasFatEntry> _entries = new Dictionary<int, RothDasFatEntry>();
        private readonly byte[] _paletteRgb = new byte[256 * 3];

        public RothDasHeader Header { get; private set; }
        public IReadOnlyDictionary<int, RothDasFatEntry> Entries { get { return _entries; } }
        public byte[] PaletteRgb { get { return _paletteRgb; } }

        public RothDasArchive(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("DAS archive not found.", path);
            _stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            _reader = new BinaryReader(_stream, Encoding.ASCII, true);
            Header = ReadHeader();
            ValidateHeader();
            ReadFat();
            ReadPalette();
            ReadFilenames();
        }

        public RothDasFatEntry GetEntry(int index)
        {
            RothDasFatEntry e;
            return _entries.TryGetValue(index, out e) ? e : null;
        }

        public RothDasImage ReadImageFirstFrame(int index)
        {
            RothDasFatEntry entry = GetEntry(index);
            if (entry == null || entry.Size == 0 || entry.Offset == 0)
                return null;
            if ((entry.Flags1 & 0x20) != 0) // directional/monster mapping rather than direct image data
                return null;

            long imageHeader = entry.Offset;
            if ((entry.Flags1 & 0x08) != 0)
                imageHeader -= 4; // two signed shift words precede the regular header
            if (imageHeader < 0 || imageHeader + 6 > _stream.Length)
                return null;

            _stream.Position = imageHeader;
            short shiftX = 0, shiftY = 0;
            if ((entry.Flags1 & 0x08) != 0)
            {
                shiftX = _reader.ReadInt16();
                shiftY = _reader.ReadInt16();
            }

            long startOffset = _stream.Position;
            byte modifier = _reader.ReadByte();
            byte imageType = _reader.ReadByte();
            ushort width = _reader.ReadUInt16();
            ushort height = _reader.ReadUInt16();

            // Bit 7 is a true 3D/object-data record and needs a geometry decoder.
            if ((imageType & 0x80) != 0)
                return null;
            if (width == 0 || height == 0)
                return null;

            byte[] pixels;
            if ((modifier & 0x40) != 0)
            {
                // Image packs contain a direction/variant offset table followed by ordinary
                // paletted subimages aligned to 16 bytes.  Milestone 0.5 decodes the first
                // subimage as a safe visual fallback; directional selection comes later.
                _stream.Position = startOffset + 8; // 6-byte image header + size/pack bytes
                while (_stream.Position + 2 <= _stream.Length)
                {
                    ushort word = _reader.ReadUInt16();
                    if (word == 0) break;
                }
                if (_stream.Position + 6 > _stream.Length) return null;
                _reader.ReadUInt16(); // second zero word
                _reader.ReadUInt32(); // zero dword
                while (((_stream.Position - startOffset) & 15) != 0 && _stream.Position < _stream.Length)
                    _reader.ReadByte();
                if (_stream.Position + 6 > _stream.Length) return null;
                byte subModifier = _reader.ReadByte();
                byte subImageType = _reader.ReadByte();
                ushort subWidth = _reader.ReadUInt16();
                ushort subHeight = _reader.ReadUInt16();
                long subCount = (long)subWidth * subHeight;
                if (subWidth == 0 || subHeight == 0 || subCount > int.MaxValue || _stream.Position + subCount > _stream.Length) return null;
                pixels = _reader.ReadBytes((int)subCount);
                if (pixels.Length != subCount) return null;
                width = subWidth; height = subHeight;
                imageType = subImageType;
                // Keep the pack modifier's HALF_SIZE/DRAW_DOWNWARD bits but use subimage alpha semantics.
            }
            else if ((imageType & 0x01) != 0)
            {
                // Animation type 1 stores a complete uncompressed base frame followed by deltas.
                // Type 2 marks numSubImages as 0xFFFE and uses a different RLE stream.
                if (startOffset + 18 > _stream.Length) return null;
                _stream.Position = startOffset + 6;
                _reader.ReadUInt32(); // total block size
                ushort firstImageOffset = _reader.ReadUInt16();
                ushort numSubImages = _reader.ReadUInt16();
                _reader.ReadUInt16(); // normally 0xFFFF
                _reader.ReadByte();   // normally 0xFF
                _reader.ReadByte();   // animation speed
                if (numSubImages == 0xFFFE) return null;

                long firstFrame = startOffset + firstImageOffset;
                if (firstFrame < 0 || firstFrame + 6 > _stream.Length) return null;
                _stream.Position = firstFrame;
                _reader.ReadByte();   // frame modifier
                _reader.ReadByte();   // frame image type
                _reader.ReadUInt16(); // repeated width
                _reader.ReadUInt16(); // repeated height
                long pixelCount = (long)width * height;
                if (pixelCount > int.MaxValue || _stream.Position + pixelCount > _stream.Length) return null;
                pixels = _reader.ReadBytes((int)pixelCount);
                if (pixels.Length != pixelCount) return null;
            }
            else
            {
                long pixelCount = (long)width * height;
                if (pixelCount > int.MaxValue || _stream.Position + pixelCount > _stream.Length)
                    return null;
                pixels = _reader.ReadBytes((int)pixelCount);
                if (pixels.Length != pixelCount)
                    return null;
            }

            bool transparent = (imageType & 0x04) != 0 || (imageType & 0x02) == 0;
            return new RothDasImage {
                Index = index, Name = entry.Name, Description = entry.Description,
                Modifier = modifier, ImageType = imageType, Flags1 = entry.Flags1, Flags2 = entry.Flags2,
                ShiftX = shiftX, ShiftY = shiftY, Width = width, Height = height,
                Pixels = pixels, IsTransparent = transparent
            };
        }

        public List<RothDasImage> ReadAnimationFrames(int index, int maxFrames = 256)
        {
            RothDasFatEntry entry = GetEntry(index);
            if (entry == null || entry.Size == 0 || entry.Offset == 0 || (entry.Flags1 & 0x20) != 0) return null;
            long header = entry.Offset;
            if ((entry.Flags1 & 0x08) != 0) header -= 4;
            if (header < 0 || header + 18 > _stream.Length) return null;
            _stream.Position = header;
            short shiftX=0, shiftY=0;
            if ((entry.Flags1 & 0x08) != 0) { shiftX=_reader.ReadInt16(); shiftY=_reader.ReadInt16(); }
            long start = _stream.Position;
            byte modifier=_reader.ReadByte(), imageType=_reader.ReadByte();
            ushort width=_reader.ReadUInt16(), height=_reader.ReadUInt16();
            if ((imageType & 0x01)==0 || width==0 || height==0) return null;
            _reader.ReadUInt32();
            ushort firstImageOffset=_reader.ReadUInt16();
            ushort numSubImages=_reader.ReadUInt16();
            _reader.ReadUInt16(); _reader.ReadByte(); byte animationSpeed=_reader.ReadByte();
            if (numSubImages==0xFFFE)
            {
                var type2=new List<RothDasImage>();
                _stream.Position=start+16;
                int expectedImages=-1; int safetyFrames=0;
                while(_stream.Position+24<=_stream.Length && safetyFrames++<maxFrames)
                {
                    long frameStart=_stream.Position;
                    ushort subType=_reader.ReadUInt16(); ushort unk02=_reader.ReadUInt16();
                    ushort bufferWidth=_reader.ReadUInt16(), bufferHeight=_reader.ReadUInt16();
                    ushort numImages=_reader.ReadUInt16(), currentIndex=_reader.ReadUInt16();
                    uint currentSize=_reader.ReadUInt32(); ushort xOffset=_reader.ReadUInt16(), patchWidth=_reader.ReadUInt16(), yOffset=_reader.ReadUInt16(), patchHeight=_reader.ReadUInt16();
                    if(expectedImages<0) expectedImages=numImages; else if(numImages!=expectedImages) break;
                    if(currentSize<24 || frameStart+currentSize>_stream.Length || bufferWidth==0 || bufferHeight==0 || patchWidth==0 || patchHeight==0) break;
                    int patchCount=patchWidth*patchHeight; byte[] patch=new byte[patchCount]; int pos=0;
                    long dataEnd=frameStart+currentSize;
                    while(pos<patchCount && _stream.Position<dataEnd)
                    {
                        byte code=_reader.ReadByte();
                        if(code>0xF0)
                        {
                            int run=code&0x0F; if(_stream.Position>=dataEnd) break; byte value=_reader.ReadByte();
                            for(int k=0;k<run && pos<patchCount;k++) patch[pos++]=value;
                        }
                        else patch[pos++]=code;
                    }
                    byte[] full=new byte[bufferWidth*bufferHeight];
                    for(int py=0;py<patchHeight;py++)
                    {
                        int dy=yOffset+py; if(dy<0 || dy>=bufferHeight) continue;
                        int src=py*patchWidth; int dst=dy*bufferWidth+xOffset; int copy=Math.Min((int)patchWidth,Math.Max(0,(int)bufferWidth-(int)xOffset));
                        if(copy>0 && src+copy<=patch.Length && dst>=0 && dst+copy<=full.Length) Array.Copy(patch,src,full,dst,copy);
                    }
                    bool tr=(imageType&0x04)!=0 || (imageType&0x02)==0;
                    type2.Add(new RothDasImage{Index=index,Name=entry.Name,Description=entry.Description,Modifier=modifier,ImageType=imageType,Flags1=entry.Flags1,Flags2=entry.Flags2,ShiftX=shiftX,ShiftY=shiftY,Width=bufferWidth,Height=bufferHeight,AnimationSpeed=(byte)Math.Min(255,Math.Max(1,(int)firstImageOffset)),Pixels=full,IsTransparent=tr});
                    _stream.Position=dataEnd;
                    if(currentIndex+1>=numImages) break;
                }
                return type2.Count>1?type2:null;
            }
            long first=start+firstImageOffset;
            long count=(long)width*height;
            if(first<0 || first+6+count>_stream.Length || count>int.MaxValue) return null;
            _stream.Position=first;
            _reader.ReadByte(); _reader.ReadByte(); _reader.ReadUInt16(); _reader.ReadUInt16();
            byte[] current=_reader.ReadBytes((int)count);
            if(current.Length!=count) return null;
            bool transparent=(imageType & 0x04)!=0 || (imageType & 0x02)==0;
            var frames=new List<RothDasImage>();
            frames.Add(new RothDasImage { Index=index,Name=entry.Name,Description=entry.Description,Modifier=modifier,ImageType=imageType,Flags1=entry.Flags1,Flags2=entry.Flags2,ShiftX=shiftX,ShiftY=shiftY,Width=width,Height=height,AnimationSpeed=animationSpeed,Pixels=(byte[])current.Clone(),IsTransparent=transparent });
            int wanted=Math.Min((int)numSubImages, Math.Max(0,maxFrames-1));
            for(int f=0; f<wanted && _stream.Position<_stream.Length; f++)
            {
                int pos=0; bool endAnimation=false; bool frameEnded=false; int safety=0;
                while(!frameEnded && !endAnimation && _stream.Position<_stream.Length && safety++ < current.Length*4+1024)
                {
                    byte code=_reader.ReadByte();
                    if(code==0)
                    {
                        if(_stream.Position>=_stream.Length) { endAnimation=true; break; }
                        byte run=_reader.ReadByte();
                        if(run==0) { endAnimation=true; break; }
                        if(_stream.Position>=_stream.Length) { endAnimation=true; break; }
                        byte value=_reader.ReadByte();
                        for(int k=0;k<run && pos<current.Length;k++) current[pos++]=value;
                    }
                    else if(code>0x80)
                    {
                        pos=Math.Min(current.Length,pos+(code & 0x7F));
                    }
                    else if(code<0x80)
                    {
                        int n=code;
                        for(int k=0;k<n && pos<current.Length && _stream.Position<_stream.Length;k++) current[pos++]=_reader.ReadByte();
                    }
                    else
                    {
                        if(_stream.Position+2>_stream.Length) { endAnimation=true; break; }
                        ushort word=_reader.ReadUInt16();
                        if(word==0) { frameEnded=true; break; }
                        int n=word & 0x3FFF;
                        if((word & 0x8000)!=0)
                        {
                            if(_stream.Position>=_stream.Length) { endAnimation=true; break; }
                            byte value=_reader.ReadByte();
                            if(value!=0) { frameEnded=true; break; }
                            for(int k=0;k<n && pos<current.Length;k++) current[pos++]=0;
                        }
                        else pos=Math.Min(current.Length,pos+n);
                    }
                }
                if(endAnimation) break;
                frames.Add(new RothDasImage { Index=index,Name=entry.Name,Description=entry.Description,Modifier=modifier,ImageType=imageType,Flags1=entry.Flags1,Flags2=entry.Flags2,ShiftX=shiftX,ShiftY=shiftY,Width=width,Height=height,AnimationSpeed=animationSpeed,Pixels=(byte[])current.Clone(),IsTransparent=transparent });
            }
            return frames.Count>1 ? frames : null;
        }

        public RothDasImage ReadImagePackSubImage(int index, int subImageIndex)
        {
            RothDasFatEntry entry = GetEntry(index);
            if (entry == null || entry.Size == 0 || entry.Offset == 0 || (entry.Flags1 & 0x20) != 0) return null;
            long start = entry.Offset;
            if ((entry.Flags1 & 0x08) != 0) start -= 4;
            if (start < 0 || start + 8 > _stream.Length) return null;
            _stream.Position = start;
            short shiftX = 0, shiftY = 0;
            if ((entry.Flags1 & 0x08) != 0) { shiftX = _reader.ReadInt16(); shiftY = _reader.ReadInt16(); }
            long imageStart = _stream.Position;
            byte modifier = _reader.ReadByte(); byte imageType = _reader.ReadByte();
            _reader.ReadUInt16(); _reader.ReadUInt16(); // outer dimensions
            _reader.ReadByte(); _reader.ReadByte();   // size-of-offsets, pack type
            if ((modifier & 0x40) == 0) return null;

            var uniqueOffsets = new HashSet<int>();
            while (_stream.Position + 2 <= _stream.Length)
            {
                ushort word = _reader.ReadUInt16();
                if (word == 0) break;
                uniqueOffsets.Add(word & 0x7FF);
            }
            if (_stream.Position + 6 > _stream.Length) return null;
            _reader.ReadUInt16(); _reader.ReadUInt32();
            while (((_stream.Position - imageStart) & 15) != 0 && _stream.Position < _stream.Length) _reader.ReadByte();
            if (subImageIndex < 0 || subImageIndex >= uniqueOffsets.Count) return null;

            for (int i = 0; i < uniqueOffsets.Count; i++)
            {
                if (_stream.Position + 6 > _stream.Length) return null;
                byte subModifier = _reader.ReadByte(); byte subType = _reader.ReadByte();
                ushort width = _reader.ReadUInt16(); ushort height = _reader.ReadUInt16();
                long count = (long)width * height;
                if (width == 0 || height == 0 || count > int.MaxValue || _stream.Position + count > _stream.Length) return null;
                byte[] pixels = _reader.ReadBytes((int)count);
                if (i == subImageIndex)
                {
                    bool transparent = (subType & 0x04) != 0 || (subType & 0x02) == 0;
                    return new RothDasImage { Index=index, Name=entry.Name, Description=entry.Description,
                        Modifier=subModifier, ImageType=subType, Flags1=entry.Flags1, Flags2=entry.Flags2,
                        ShiftX=shiftX, ShiftY=shiftY, Width=width, Height=height, Pixels=pixels, IsTransparent=transparent };
                }
                while (((_stream.Position - imageStart) & 15) != 0 && _stream.Position < _stream.Length) _reader.ReadByte();
            }
            return null;
        }

        public RothDasDirectionalViews ReadDirectionalViews(int index)
        {
            RothDasFatEntry entry = GetEntry(index);
            if (entry == null || entry.Size == 0) return null;

            // Direct image-pack. The offset table maps the eight logical directions to
            // unique subimages; the high bit mirrors a view.
            if ((entry.Flags1 & 0x20) == 0)
            {
                long start = entry.Offset;
                if ((entry.Flags1 & 0x08) != 0) start -= 4;
                if (start < 0 || start + 8 > _stream.Length) return null;
                _stream.Position = start;
                if ((entry.Flags1 & 0x08) != 0) { _reader.ReadInt16(); _reader.ReadInt16(); }
                byte modifier = _reader.ReadByte(); _reader.ReadByte();
                _reader.ReadUInt16(); _reader.ReadUInt16(); _reader.ReadByte(); _reader.ReadByte();
                if ((modifier & 0x40) == 0) return null;
                var offsets = new List<int>();
                var flips = new List<bool>();
                var unique = new List<int>();
                while (_stream.Position + 2 <= _stream.Length)
                {
                    ushort word = _reader.ReadUInt16();
                    if (word == 0) break;
                    int off = word & 0x7FF;
                    offsets.Add(off); flips.Add((word & 0x8000) != 0);
                    if (!unique.Contains(off)) unique.Add(off);
                }
                if (offsets.Count < 8) return null;
                unique.Sort();
                int[] order = { 4,3,2,1,0,7,6,5 }; // FRONT ... FRONT_LEFT
                var result = new RothDasDirectionalViews { UsesImagePack = true };
                for (int d=0; d<8; d++)
                {
                    int src = order[d];
                    int sub = unique.IndexOf(offsets[src]);
                    result.ResourceIndices[d] = index;
                    result.PackSubImageIndices[d] = sub;
                    result.Flipped[d] = flips[src];
                }
                return result;
            }

            // Mapping records use Flags2 as table index. Monsters use their walking views;
            // ordinary directional objects use the eight direction FAT indices.
            bool monster = (entry.Flags1 & 0x04) != 0;
            var views = new RothDasDirectionalViews { IsMonster = monster };
            ushort[] raw = new ushort[8];
            if (monster)
            {
                long pos = (long)Header.MonsterMappingOffset + entry.Flags2 * 104L + 20L;
                if (pos < 0 || pos + 16 > _stream.Length) return null;
                _stream.Position = pos;
                // walking_back, back_right, right, front_right, front, front_left, left, back_left
                ushort back=_reader.ReadUInt16(), br=_reader.ReadUInt16(), right=_reader.ReadUInt16(), fr=_reader.ReadUInt16();
                ushort front=_reader.ReadUInt16(), fl=_reader.ReadUInt16(), left=_reader.ReadUInt16(), bl=_reader.ReadUInt16();
                raw = new [] { front, fr, right, br, back, bl, left, fl };
            }
            else
            {
                int count = Header.DirectionalObjectTableSize / 20;
                long pos = (long)Header.DirectionalObjectTableOffset + count * 2L + entry.Flags2 * 18L;
                if (pos < 0 || pos + 18 > _stream.Length) return null;
                _stream.Position = pos;
                _reader.ReadUInt16(); // mapping header
                ushort d1=_reader.ReadUInt16(), d2=_reader.ReadUInt16(), d3=_reader.ReadUInt16(), d4=_reader.ReadUInt16();
                ushort d5=_reader.ReadUInt16(), d6=_reader.ReadUInt16(), d7=_reader.ReadUInt16(), d8=_reader.ReadUInt16();
                raw = new [] { d5,d4,d3,d2,d1,d8,d7,d6 };
            }
            for (int d=0; d<8; d++)
            {
                int v = raw[d];
                views.Flipped[d] = (v & 0x8000) != 0;
                v &= 0x7FFF;
                if (v > 4608) v -= 4608;
                views.ResourceIndices[d] = v;
            }
            return views;
        }

        public RothDasObjectData ReadObjectData(int index)
        {
            RothDasFatEntry entry = GetEntry(index);
            if (entry == null || entry.Size == 0 || entry.Offset == 0 || (entry.Flags1 & 0x20) != 0) return null;
            long start = entry.Offset;
            if ((entry.Flags1 & 0x08) != 0) start -= 4;
            if (start < 0 || start + 22 > _stream.Length) return null;
            _stream.Position = start;
            if ((entry.Flags1 & 0x08) != 0) { _reader.ReadInt16(); _reader.ReadInt16(); }
            byte modifier = _reader.ReadByte(); byte imageType = _reader.ReadByte();
            if ((imageType & 0x80) == 0) return null;
            _reader.ReadUInt16(); _reader.ReadUInt16(); // max X/Y
            _reader.ReadUInt16(); _reader.ReadUInt16(); _reader.ReadUInt32();
            _reader.ReadUInt16(); _reader.ReadUInt32();
            ushort vertexCount = _reader.ReadUInt16();
            var obj = new RothDasObjectData { Index=index, Modifier=modifier, ImageType=imageType };
            for (int i=0;i<vertexCount;i++)
            {
                if (_stream.Position + 16 > _stream.Length) return null;
                short x=_reader.ReadInt16(), y=_reader.ReadInt16(), z=_reader.ReadInt16();
                _reader.ReadUInt32(); _reader.ReadUInt32(); _reader.ReadUInt16();
                obj.Vertices.Add(new RothDasObjectVertex(x,y,z));
            }
            if (_stream.Position + 8 > _stream.Length) return null;
            string sig=Encoding.ASCII.GetString(_reader.ReadBytes(4));
            _reader.ReadUInt16();
            byte hi=_reader.ReadByte(), lo=_reader.ReadByte();
            ushort facesArraySize=(ushort)((hi<<8)|lo);
            if (sig != "EXP2" || facesArraySize < 4) return null;
            int currentSize=4;
            while (currentSize < facesArraySize)
            {
                if (_stream.Position + 54 > _stream.Length) return null;
                _reader.ReadUInt16(); _reader.ReadUInt32(); _reader.ReadUInt16(); _reader.ReadUInt16(); _reader.ReadUInt16();
                byte tHi=_reader.ReadByte(), tLo=_reader.ReadByte();
                ushort textureIndex=(ushort)((tHi<<8)|tLo);
                _reader.ReadUInt16(); _reader.ReadUInt16(); _reader.ReadUInt32();
                byte render1=_reader.ReadByte(), render2=_reader.ReadByte();
                _reader.ReadUInt32(); byte sub=_reader.ReadByte();
                _reader.ReadByte(); _reader.ReadByte(); _reader.ReadByte();
                _reader.ReadUInt32(); _reader.ReadUInt16(); _reader.ReadUInt16(); _reader.ReadUInt32(); _reader.ReadUInt32();
                _reader.ReadUInt16(); _reader.ReadUInt16(); ushort edgeCount=_reader.ReadUInt16();
                int edgeWords=edgeCount+1;
                if (_stream.Position + edgeWords*2 > _stream.Length) return null;
                ushort[] edges=new ushort[edgeWords];
                for(int e=0;e<edgeWords;e++) edges[e]=_reader.ReadUInt16();
                obj.Faces.Add(new RothDasObjectFace { TextureIndex=textureIndex, SubTextureIndex=sub, RenderFlag1=render1, RenderFlag2=render2, Edges=edges });
                currentSize += 54 + edgeWords*2;
            }
            return obj;
        }

        public void Dispose()
        {
            _reader.Dispose();
            _stream.Dispose();
        }

        private RothDasHeader ReadHeader()
        {
            _stream.Position = 0;
            return new RothDasHeader {
                Signature = Encoding.ASCII.GetString(_reader.ReadBytes(4)),
                Version = _reader.ReadUInt16(), FatSize = _reader.ReadUInt16(),
                FatOffset = _reader.ReadUInt32(), PaletteOffset = _reader.ReadUInt32(),
                Unknown10Offset = _reader.ReadUInt32(), FilenamesOffset = _reader.ReadUInt32(),
                FilenamesSize = _reader.ReadUInt16(), DirectionalObjectTableSize = _reader.ReadUInt16(),
                DirectionalObjectTableOffset = _reader.ReadUInt32(), Unknown20 = _reader.ReadUInt16(),
                SkyIndex = _reader.ReadUInt16(), ObjectCollisionOffset = _reader.ReadUInt32(),
                MonsterMappingOffset = _reader.ReadUInt32(), MonsterMappingSize = _reader.ReadUInt32(),
                FatBlock1Count = _reader.ReadUInt16(), FatBlock2Count = _reader.ReadUInt16(),
                FatBlock3Count = _reader.ReadUInt16(), FatBlock4Count = _reader.ReadUInt16(),
                Unknown38Offset = _reader.ReadUInt32(), Unknown38Size = _reader.ReadUInt16(),
                Unknown40Size = _reader.ReadUInt16(), Unknown40Offset = _reader.ReadUInt32()
            };
        }

        private void ValidateHeader()
        {
            if (Header.Signature != "DASP")
                throw new InvalidDataException("Invalid DAS signature: " + Header.Signature);
            if (Header.Version != 5)
                throw new InvalidDataException("Unsupported DAS version: " + Header.Version);
            long expectedFatBytes = (long)Header.TotalFatCount * 8;
            if (Header.FatOffset + expectedFatBytes > _stream.Length)
                throw new InvalidDataException("DAS FAT extends beyond end of file.");
        }

        private void ReadFat()
        {
            _stream.Position = Header.FatOffset;
            for (int i = 0; i < Header.TotalFatCount; i++)
            {
                RothDasFatEntry e = new RothDasFatEntry {
                    Index = i, Offset = _reader.ReadUInt32(), Size = _reader.ReadUInt16(),
                    Flags1 = _reader.ReadByte(), Flags2 = _reader.ReadByte()
                };
                _entries[i] = e;
            }
        }

        private void ReadPalette()
        {
            if (Header.PaletteOffset == 0 || Header.PaletteOffset + 768 > _stream.Length)
            {
                // ADEMO can omit a palette. Keep a deterministic grayscale fallback until the
                // original built-in palette is promoted into this reader.
                for (int i = 0; i < 256; i++)
                {
                    _paletteRgb[i * 3] = (byte)i;
                    _paletteRgb[i * 3 + 1] = (byte)i;
                    _paletteRgb[i * 3 + 2] = (byte)i;
                }
                return;
            }

            _stream.Position = Header.PaletteOffset;
            byte[] raw = _reader.ReadBytes(768);
            for (int i = 0; i < 768; i++)
            {
                // Original entries are VGA 6-bit RGB. This is the same integer expansion used by
                // current community tooling: (v * 259 + 33) >> 6.
                _paletteRgb[i] = (byte)((raw[i] * 259 + 33) >> 6);
            }
        }

        private void ReadFilenames()
        {
            if (Header.FilenamesOffset == 0 || Header.FilenamesOffset + 4 > _stream.Length)
                return;
            _stream.Position = Header.FilenamesOffset;
            ushort section1 = _reader.ReadUInt16();
            ushort section2 = _reader.ReadUInt16();
            int count = section1 + section2;
            for (int i = 0; i < count && _stream.Position < _stream.Length; i++)
            {
                _reader.ReadUInt16(); // record size, retained only by the original editor
                ushort index = _reader.ReadUInt16();
                string name = ReadCString();
                string description = ReadCString();
                RothDasFatEntry e;
                if (_entries.TryGetValue(index, out e))
                {
                    e.Name = name;
                    e.Description = description;
                }
            }
        }

        private string ReadCString()
        {
            var bytes = new List<byte>(32);
            while (_stream.Position < _stream.Length)
            {
                byte b = _reader.ReadByte();
                if (b == 0) break;
                bytes.Add(b);
            }
            return Encoding.ASCII.GetString(bytes.ToArray());
        }
    }
}
