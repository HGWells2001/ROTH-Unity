using System;
using System.IO;
using System.Text;

namespace ROTHUnity.Core
{
    public sealed class RothDBaseTextEntry
    {
        public uint DBase500Offset;
        public ushort FontColor;
        public string Text;
        public float[] VoiceSamples;
        public int VoiceRate;
    }

    public sealed class RothDBaseSubtitleEntry
    {
        public ushort Timestamp;
        public byte FontColor;
        public string Text;
    }

    public sealed class RothDBaseNarrativeArchive
    {
        public string DBase400Path { get; private set; }
        public string DBase500Path { get; private set; }
        private static readonly int[] DeltaTable = BuildDeltaTable();

        public static RothDBaseNarrativeArchive Load(string dbase400, string dbase500)
        {
            if (string.IsNullOrEmpty(dbase400) || !File.Exists(dbase400)) return null;
            return new RothDBaseNarrativeArchive { DBase400Path = dbase400, DBase500Path = dbase500 };
        }

        public RothDBaseTextEntry ReadText(uint offset, bool decodeVoice)
        {
            using (var fs = File.OpenRead(DBase400Path))
            using (var br = new BinaryReader(fs))
            {
                if (offset == 0 || offset + 8 > fs.Length) return null;
                fs.Position = offset;
                uint voice = br.ReadUInt32(); ushort len = br.ReadUInt16(); ushort color = br.ReadUInt16();
                if (len == 0 || fs.Position + len > fs.Length) return null;
                byte[] raw = br.ReadBytes(len); int n = Array.IndexOf(raw, (byte)0); if (n < 0) n = raw.Length;
                var e = new RothDBaseTextEntry { DBase500Offset = voice, FontColor = color, Text = Encoding.ASCII.GetString(raw, 0, n) };
                if (decodeVoice && voice != 0) DecodeVoice(e);
                return e;
            }
        }


        public System.Collections.Generic.List<RothDBaseSubtitleEntry> ReadCutsceneSubtitles(uint offset)
        {
            var result=new System.Collections.Generic.List<RothDBaseSubtitleEntry>();
            if(offset==0)return result;
            using(var fs=File.OpenRead(DBase400Path))using(var br=new BinaryReader(fs))
            {
                if(offset>=fs.Length)return result;fs.Position=offset;int guard=10000;
                while(fs.Position+4<=fs.Length&&guard-->0)
                {
                    ushort length=br.ReadUInt16();ushort timestamp=br.ReadUInt16();
                    if(timestamp==0xFFFF)break;
                    if(length==0){if(fs.Position>=2)fs.Position-=2;continue;}
                    if(length<5||fs.Position+(length-4)>fs.Length)break;
                    byte color=br.ReadByte();int textBytes=length-5;byte[] raw=br.ReadBytes(textBytes);int n=Array.IndexOf(raw,(byte)0);if(n<0)n=raw.Length;
                    string text=Encoding.ASCII.GetString(raw,0,n);result.Add(new RothDBaseSubtitleEntry{Timestamp=timestamp,FontColor=color,Text=text});
                    if((fs.Position&1)!=0)fs.Position++;
                }
            }
            return result;
        }

        private void DecodeVoice(RothDBaseTextEntry e)
        {
            if (string.IsNullOrEmpty(DBase500Path) || !File.Exists(DBase500Path)) return;
            using (var fs = File.OpenRead(DBase500Path))
            using (var br = new BinaryReader(fs))
            {
                long pos = (long)e.DBase500Offset * 8L; if (pos < 0 || pos + 44 > fs.Length) return; fs.Position = pos;
                string chunk = Encoding.ASCII.GetString(br.ReadBytes(4)); if (chunk != "FFIR") return;
                br.ReadUInt32(); string wave = Encoding.ASCII.GetString(br.ReadBytes(4)); if (wave != "WAVE") return;
                br.ReadBytes(4); br.ReadUInt32(); ushort format = br.ReadUInt16(); ushort channels = br.ReadUInt16(); uint rate = br.ReadUInt32(); br.ReadUInt32(); br.ReadUInt16(); br.ReadUInt16();
                br.ReadBytes(4); uint size = br.ReadUInt32(); if (size > fs.Length - fs.Position) size = (uint)(fs.Length - fs.Position);
                byte[] data = br.ReadBytes((int)size); e.VoiceRate = rate > 0 && rate < 192000 ? (int)rate : 22050;
                if (format == 42)
                {
                    float[] samples = new float[data.Length]; int state = 0;
                    for (int i = 0; i < data.Length; i++) { state = Wrap16(state + DeltaTable[data[i]]); samples[i] = state / 32768f; }
                    e.VoiceSamples = samples;
                }
                else if (format == 1)
                {
                    if (channels == 0) channels = 1; int count = data.Length / 2 / channels; float[] samples = new float[count];
                    for (int i=0;i<count;i++) { int p=i*2*channels; short s=(short)(data[p]|(data[p+1]<<8)); samples[i]=s/32768f; }
                    e.VoiceSamples=samples;
                }
            }
        }
        private static int Wrap16(int v){while(v>32767)v-=65536;while(v<-32768)v+=65536;return v;}
        private static int[] BuildDeltaTable(){int[]t=new int[256];int d=0,c=64,s=45;for(int i=1;i<254;i+=2){d+=(c>>5);c+=s;s+=2;t[i]=d;t[i+1]=-d;}t[255]=d+(c>>5);return t;}
    }
}
