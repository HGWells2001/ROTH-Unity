using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ROTHUnity.Core
{
    public sealed class RothSfxEntry
    {
        public int TableIndex;
        public ushort Index, Type;
        public uint Offset, Size;
        public string Name, Description;
        public int SampleRate { get { return Type==3 ? 22050 : 11025; } }
    }

    public sealed class RothSfxArchive : IDisposable
    {
        private readonly FileStream _stream;
        private readonly BinaryReader _reader;
        private readonly List<RothSfxEntry> _entries=new List<RothSfxEntry>();
        public IReadOnlyList<RothSfxEntry> Entries { get { return _entries; } }

        public RothSfxArchive(string path)
        {
            if(string.IsNullOrWhiteSpace(path)||!File.Exists(path))throw new FileNotFoundException("ROTH SFX archive not found",path);
            _stream=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.Read); _reader=new BinaryReader(_stream,Encoding.ASCII,true); Parse();
        }
        private void Parse()
        {
            _stream.Position=0; string sig=Encoding.ASCII.GetString(_reader.ReadBytes(4)); ushort ver=_reader.ReadUInt16(); _reader.ReadUInt16();
            uint fatOff=_reader.ReadUInt32(); _reader.ReadUInt32(); uint fatSize=_reader.ReadUInt32(); uint namesOff=_reader.ReadUInt32(); uint namesSize=_reader.ReadUInt32();
            if(sig!="0XFS"||ver!=1)throw new InvalidDataException("Unsupported ROTH SFX header");
            int count=checked((int)(fatSize/12)); long fatPos=fatOff,namesPos=namesOff;
            for(int i=0;i<count;i++)
            {
                _stream.Position=fatPos; uint off=_reader.ReadUInt32(),size=_reader.ReadUInt32(); ushort idx=_reader.ReadUInt16(),type=_reader.ReadUInt16(); fatPos=_stream.Position;
                _stream.Position=namesPos; ushort nameIdx=_reader.ReadUInt16(); string name=ReadCString(); string desc=ReadCString(); namesPos=_stream.Position;
                if(nameIdx!=idx)throw new InvalidDataException("SFX FAT/name index mismatch at "+i);
                if(off+size>_stream.Length)throw new InvalidDataException("SFX data outside archive at "+i);
                _entries.Add(new RothSfxEntry{TableIndex=i,Index=idx,Type=type,Offset=off,Size=size,Name=name,Description=desc});
            }
        }
        private string ReadCString(){var bytes=new List<byte>();while(_stream.Position<_stream.Length){byte b=_reader.ReadByte();if(b==0)break;bytes.Add(b);}return Encoding.ASCII.GetString(bytes.ToArray());}
        public short[] ReadPcm16(int tableIndex)
        {
            if(tableIndex<0||tableIndex>=_entries.Count)return null; RothSfxEntry e=_entries[tableIndex]; if(e.Type==0||e.Size<2)return null;
            _stream.Position=e.Offset; int samples=checked((int)e.Size/2); short[] pcm=new short[samples]; for(int i=0;i<samples;i++)pcm[i]=_reader.ReadInt16(); return pcm;
        }
        public void Dispose(){_reader.Dispose();_stream.Dispose();}
    }
}
