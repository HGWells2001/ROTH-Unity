using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ROTHUnity.Core
{
    public sealed class RothDBase100Command
    {
        public int Argument;
        public byte Opcode;
    }

    public sealed class RothDBase100Action
    {
        public int Index;
        public uint FileOffset;
        public ushort HeaderWord;
        public readonly List<RothDBase100Command> Commands = new List<RothDBase100Command>();
    }

    public sealed class RothDBase100Cutscene
    {
        public int Index;
        public string Name;
        public ushort SubtitleLength;
        public uint TextOffset;
        public uint SubtitleOffset;
    }

    public sealed class RothDBase100Header
    {
        public string Signature;
        public uint FileSize;
        public uint Unknown02;
        public uint InventoryCount;
        public uint InventoryOffset;
        public uint ActionCount;
        public uint ActionOffset;
        public uint CutsceneCount;
        public uint CutsceneOffset;
        public uint InterfaceCount;
        public uint InterfaceOffset;
        public uint Unknown11;
    }

    /// <summary>
    /// Minimal read-only DBASE100 reader used by the 0.6 runtime. It intentionally parses only
    /// the header and the global action table. Inventory/cutscene payloads are left untouched
    /// until their dependent DBASE200/300/400 formats are wired into gameplay.
    /// </summary>
    public sealed class RothDBase100Archive
    {
        public string Path { get; private set; }
        public RothDBase100Header Header { get; private set; }
        public IReadOnlyList<RothDBase100Action> Actions { get { return _actions; } }
        public IReadOnlyList<RothDBase100Cutscene> Cutscenes { get { return _cutscenes; } }
        private readonly List<RothDBase100Action> _actions = new List<RothDBase100Action>();
        private readonly List<RothDBase100Cutscene> _cutscenes = new List<RothDBase100Cutscene>();

        public static RothDBase100Archive Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var result = new RothDBase100Archive();
            result.Path = path;
            result.Read();
            return result;
        }

        public RothDBase100Action GetAction(int index)
        {
            return index >= 0 && index < _actions.Count ? _actions[index] : null;
        }

        /// <summary>Resolves the 1-based action references stored by the original game scripts.</summary>
        public RothDBase100Action GetGameAction(int gameIndex)
        {
            int index = gameIndex - 1;
            return index >= 0 && index < _actions.Count ? _actions[index] : null;
        }

        public RothDBase100Cutscene GetCutscene(int index)
        {
            return index >= 0 && index < _cutscenes.Count ? _cutscenes[index] : null;
        }

        private void Read()
        {
            using (var fs = File.OpenRead(Path))
            using (var br = new BinaryReader(fs))
            {
                string sig = Encoding.ASCII.GetString(br.ReadBytes(8)).TrimEnd('\0');
                if (!string.Equals(sig, "DBASE100", StringComparison.Ordinal))
                    throw new InvalidDataException("Not a DBASE100 archive: " + Path);

                Header = new RothDBase100Header
                {
                    Signature = sig,
                    FileSize = br.ReadUInt32(),
                    Unknown02 = br.ReadUInt32(),
                    InventoryCount = br.ReadUInt32(),
                    InventoryOffset = br.ReadUInt32(),
                    ActionCount = br.ReadUInt32(),
                    ActionOffset = br.ReadUInt32(),
                    CutsceneCount = br.ReadUInt32(),
                    CutsceneOffset = br.ReadUInt32(),
                    InterfaceCount = br.ReadUInt32(),
                    InterfaceOffset = br.ReadUInt32(),
                    Unknown11 = br.ReadUInt32()
                };

                if (Header.ActionCount > 100000 || Header.ActionOffset >= fs.Length)
                    throw new InvalidDataException("Implausible DBASE100 action table.");

                if (Header.CutsceneCount < 100000 && Header.CutsceneOffset < fs.Length)
                {
                    fs.Position = Header.CutsceneOffset;
                    for (int i=0; i<Header.CutsceneCount && fs.Position+20<=fs.Length; i++)
                    {
                        string name = Encoding.ASCII.GetString(br.ReadBytes(8)).TrimEnd('\0',' ');
                        br.ReadUInt16();
                        ushort subLen = br.ReadUInt16();
                        uint textOffset = br.ReadUInt32();
                        uint subtitleOffset = br.ReadUInt32();
                        _cutscenes.Add(new RothDBase100Cutscene { Index=i, Name=name, SubtitleLength=subLen, TextOffset=textOffset, SubtitleOffset=subtitleOffset });
                    }
                }

                fs.Position = Header.ActionOffset;
                uint[] offsets = new uint[Header.ActionCount];
                for (int i = 0; i < offsets.Length; i++) offsets[i] = br.ReadUInt32();

                for (int i = 0; i < offsets.Length; i++)
                {
                    var action = new RothDBase100Action { Index = i, FileOffset = offsets[i] };
                    _actions.Add(action);
                    if (offsets[i] == 0 || offsets[i] + 4 > fs.Length) continue;
                    fs.Position = offsets[i];
                    ushort length = br.ReadUInt16();
                    action.HeaderWord = br.ReadUInt16();
                    if (length < 4) continue;
                    int commandCount = (length / 4) - 1;
                    if (commandCount < 0 || commandCount > 4096) continue;
                    for (int c = 0; c < commandCount && fs.Position + 4 <= fs.Length; c++)
                    {
                        int b0 = br.ReadByte();
                        int b1 = br.ReadByte();
                        int b2 = br.ReadByte();
                        byte opcode = br.ReadByte();
                        action.Commands.Add(new RothDBase100Command
                        {
                            Argument = b0 | (b1 << 8) | (b2 << 16),
                            Opcode = opcode
                        });
                    }
                }
            }
        }
    }
}
