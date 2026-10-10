using System;
using System.Collections.Generic;
using System.IO;

namespace ROTHUnity.Core
{
    /// <summary>
    /// Read-only parser for the original Realms of the Haunting .RAW map format.
    /// Read-only retail RAW parser used by the Unity reconstruction pipeline.
    /// Milestone 0.4 covers structural geometry, intermediate platforms and the object section.
    /// </summary>
    public static class RothRawMapReader
    {
        public const ushort ExpectedVersion = 0x0070;
        public const ushort ExpectedSignature = 0x5257; // bytes 57 52 = "WR" on disk
        private const int SectorSize = 0x1A;
        private const int FaceSize = 0x0C;
        private const int VertexSize = 0x0C;
        private const int TextureMappingSize = 0x0A;
        private const int MidPlatformSize = 0x0E;
        private const int ObjectSize = 0x10;

        public static RothRawMap Read(string path)
        {
            using (FileStream stream = File.OpenRead(path))
                return Read(stream);
        }

        public static RothRawMap Read(Stream stream)
        {
            if (stream == null || !stream.CanRead || !stream.CanSeek)
                throw new ArgumentException("ROTH RAW parser requires a readable, seekable stream.", "stream");

            BinaryReader br = new BinaryReader(stream);
            RothRawMap map = new RothRawMap();
            map.Header = ReadHeader(br);
            ValidateHeader(map.Header, stream.Length);

            stream.Position = map.Header.SectorsOffset;
            Dictionary<ushort, int> sectorOffsets = new Dictionary<ushort, int>();
            for (int i = 0; i < map.Header.SectorCount; i++)
            {
                ushort offset = CheckedOffset(stream.Position);
                sectorOffsets[offset] = i;
                map.Sectors.Add(ReadSector(br));
            }

            // A count word is stored immediately before the first face record.
            stream.Position = map.Header.FacesOffset - 2;
            ushort faceCount = br.ReadUInt16();
            stream.Position = map.Header.FacesOffset;
            Dictionary<ushort, int> faceOffsets = new Dictionary<ushort, int>();
            for (int i = 0; i < faceCount; i++)
            {
                ushort offset = CheckedOffset(stream.Position);
                faceOffsets[offset] = i;
                map.Faces.Add(ReadFace(br));
            }

            // A count word is stored immediately before the mapping section.
            stream.Position = map.Header.FaceTextureMapsOffset - 2;
            ushort mappingCount = br.ReadUInt16();
            stream.Position = map.Header.FaceTextureMapsOffset;
            Dictionary<ushort, int> mappingOffsets = new Dictionary<ushort, int>();
            for (int i = 0; i < mappingCount; i++)
            {
                ushort offset = CheckedOffset(stream.Position);
                mappingOffsets[offset] = i;
                RothTextureMapping tm = ReadTextureMapping(br);
                if ((tm.Type & 0x80) != 0)
                {
                    tm.HasAdditionalMetadata = true;
                    tm.ShiftTextureX = br.ReadSByte();
                    tm.ShiftTextureY = br.ReadSByte();
                    tm.FaceId = br.ReadUInt16();
                }
                map.TextureMappings.Add(tm);
            }

            if (map.Header.MidPlatformsSection != 0)
            {
                if (map.Header.MidPlatformsSection < 2 || map.Header.MidPlatformsSection >= stream.Length)
                    throw new InvalidDataException("Invalid ROTH mid-platform section offset.");
                stream.Position = map.Header.MidPlatformsSection - 2;
                ushort platformCount = br.ReadUInt16();
                var platformOffsets = new Dictionary<ushort, int>();
                for (int i = 0; i < platformCount; i++)
                {
                    ushort offset = CheckedOffset(stream.Position);
                    platformOffsets[offset] = i;
                    map.MidPlatforms.Add(ReadMidPlatform(br));
                }
                for (int i = 0; i < map.Sectors.Count; i++)
                {
                    int idx;
                    if (map.Sectors[i].IntermediateFloorOffset != 0 && platformOffsets.TryGetValue(map.Sectors[i].IntermediateFloorOffset, out idx))
                        map.Sectors[i].IntermediateFloorIndex = idx;
                }
            }

            stream.Position = map.Header.MapMetadataOffset;
            map.Metadata = ReadMetadata(br);

            stream.Position = map.Header.VerticesOffset;
            ushort verticesSectionSize = br.ReadUInt16();
            ushort verticesHeaderSize = br.ReadUInt16();
            br.ReadUInt16(); // blank
            ushort vertexCount = br.ReadUInt16();
            if (verticesHeaderSize != 8)
                throw new InvalidDataException("Unexpected ROTH vertex header size: " + verticesHeaderSize);
            if (verticesSectionSize != map.Header.VerticesSectionSize)
                throw new InvalidDataException("ROTH vertex section size does not match map header.");

            Dictionary<ushort, int> vertexRelativeOffsets = new Dictionary<ushort, int>();
            for (int i = 0; i < vertexCount; i++)
            {
                ushort relativeOffset = CheckedOffset(stream.Position - map.Header.VerticesOffset);
                vertexRelativeOffsets[relativeOffset] = i;
                br.ReadUInt16(); br.ReadUInt16(); br.ReadUInt16(); br.ReadUInt16();
                short x = br.ReadInt16();
                short y = br.ReadInt16();
                map.Vertices.Add(new RothVertex(x, y));
            }

            ResolveRelations(map, sectorOffsets, faceOffsets, mappingOffsets, vertexRelativeOffsets);
            ReadCommands(br, map);
            ReadSoundSection(br, map);
            ReadObjects(br, map);
            return map;
        }

        private static void ReadCommands(BinaryReader br, RothRawMap map)
        {
            if (map.Header.CommandSectionSize == 0) return;
            Stream stream = br.BaseStream;
            long sectionStart = (long)map.Header.VerticesOffset + map.Header.VerticesSectionSize;
            long sectionEnd = sectionStart + map.Header.CommandSectionSize;
            if (sectionStart < 0 || sectionEnd > stream.Length || sectionStart + 68 > sectionEnd) return;

            stream.Position = sectionStart;
            ushort signature = br.ReadUInt16();
            if (signature != 30003) return; // 0x7533
            br.ReadUInt16(); // unknown
            ushort commandsOffset = br.ReadUInt16();
            ushort commandCount = br.ReadUInt16();

            for (int category = 1; category <= 15; category++)
            {
                ushort referenceOffset = br.ReadUInt16();
                ushort count = br.ReadUInt16();
                if (count != 0)
                    map.CommandCategories.Add(new RothCommandCategory { Category = category, ReferenceTableOffset = referenceOffset, Count = count });
            }

            ushort[] entryOffsets = new ushort[commandCount];
            for (int i = 0; i < commandCount && stream.Position + 2 <= sectionEnd; i++)
                entryOffsets[i] = br.ReadUInt16();

            long commandsStart = sectionStart + commandsOffset;
            if (commandsStart < stream.Position || commandsStart > sectionEnd) return;
            stream.Position = commandsStart;
            var offsetToIndex = new Dictionary<ushort, int>();

            for (int i = 0; i < commandCount && stream.Position + 6 <= sectionEnd; i++)
            {
                long relLong = stream.Position - sectionStart;
                if (relLong < 0 || relLong > ushort.MaxValue) break;
                ushort rel = (ushort)relLong;
                ushort size = br.ReadUInt16();
                if (size < 6 || (size & 1) != 0 || stream.Position - 2 + size > sectionEnd) break;
                byte modifier = br.ReadByte();
                byte baseOpcode = br.ReadByte();
                ushort next = br.ReadUInt16();
                int argCount = (size - 6) / 2;
                ushort[] args = new ushort[argCount];
                for (int a = 0; a < argCount; a++) args[a] = br.ReadUInt16();
                offsetToIndex[rel] = map.Commands.Count;
                map.Commands.Add(new RothCommand { RelativeOffset = rel, Modifier = modifier, BaseOpcode = baseOpcode, NextCommandIndex = next, Arguments = args });
            }

            for (int i = 0; i < entryOffsets.Length; i++)
            {
                ushort rel = entryOffsets[i];
                int commandIndex;
                if (rel != 0 && offsetToIndex.TryGetValue(rel, out commandIndex))
                    map.EntryCommandIndices.Add(commandIndex + 1);
            }
        }

        private static void ReadSoundSection(BinaryReader br, RothRawMap map)
        {
            if (map.Header.Section7Size == 0) return;
            Stream stream = br.BaseStream;
            long start = (long)map.Header.VerticesOffset + map.Header.VerticesSectionSize + map.Header.CommandSectionSize;
            long end = start + map.Header.Section7Size;
            if (start < 0 || start + 4 > stream.Length || end > stream.Length) return;
            stream.Position = start;
            ushort sizeA = br.ReadUInt16();
            ushort count = br.ReadUInt16();
            if (sizeA < 4 || start + sizeA > end) return;

            for (int i = 0; i < count && stream.Position + 18 <= start + sizeA; i++)
            {
                map.SoundEffects.Add(new RothSoundEffect {
                    PosX = br.ReadInt16(), PosY = br.ReadInt16(), SfxIndex = br.ReadUInt16(), SfxId = br.ReadUInt16(),
                    Flags = br.ReadByte(), ZoneIndex = br.ReadByte(), AudibleRadius = br.ReadUInt16(), LoopDelay = br.ReadUInt16(),
                    Unknown0E = br.ReadUInt16(), Volume = br.ReadByte(), Unknown11 = br.ReadByte()
                });
            }

            stream.Position = start + sizeA;
            while (stream.Position + 32 <= end)
            {
                var zone = new RothSoundZone { ZoneCount = br.ReadUInt16() };
                for (int z = 0; z < 3; z++)
                {
                    zone.Dampen[z] = br.ReadByte(); zone.Flags[z] = br.ReadByte();
                    zone.XLower[z] = br.ReadInt16(); zone.YLower[z] = br.ReadInt16();
                    zone.XUpper[z] = br.ReadInt16(); zone.YUpper[z] = br.ReadInt16();
                }
                map.SoundZones.Add(zone);
            }
        }

        private static RothRawHeader ReadHeader(BinaryReader br)
        {
            return new RothRawHeader {
                VerticesOffset = br.ReadUInt16(), Version = br.ReadUInt16(), SectorsOffset = br.ReadUInt16(),
                FacesOffset = br.ReadUInt16(), FaceTextureMapsOffset = br.ReadUInt16(), MapMetadataOffset = br.ReadUInt16(),
                VerticesOffsetRepeat = br.ReadUInt16(), Signature = br.ReadUInt16(), MidPlatformsSection = br.ReadUInt16(),
                Section7Size = br.ReadUInt16(), VerticesSectionSize = br.ReadUInt16(), ObjectsSectionsSize = br.ReadUInt16(),
                FooterSize = br.ReadUInt16(), CommandSectionSize = br.ReadUInt16(), SectorCount = br.ReadUInt16()
            };
        }

        private static RothSector ReadSector(BinaryReader br)
        {
            return new RothSector {
                CeilingHeight = br.ReadInt16(), FloorHeight = br.ReadInt16(), Unknown04 = br.ReadUInt16(),
                CeilingTextureIndex = br.ReadUInt16(), FloorTextureIndex = br.ReadUInt16(), SectorFlags = br.ReadByte(),
                Lighting = br.ReadByte(), TextureMapOverride = br.ReadSByte(), FacesCount = br.ReadByte(),
                FirstFaceOffset = br.ReadUInt16(), CeilingTextureShiftX = br.ReadByte(), CeilingTextureShiftY = br.ReadByte(),
                FloorTextureShiftX = br.ReadByte(), FloorTextureShiftY = br.ReadByte(), SectorId = br.ReadUInt16(),
                AdditionalSectorFlags = br.ReadUInt16(), IntermediateFloorOffset = br.ReadUInt16()
            };
        }


        private static RothMidPlatform ReadMidPlatform(BinaryReader br)
        {
            return new RothMidPlatform {
                CeilingTextureIndex = br.ReadUInt16(), CeilingHeight = br.ReadInt16(),
                CeilingTextureShiftX = br.ReadByte(), CeilingTextureShiftY = br.ReadByte(),
                FloorTextureIndex = br.ReadUInt16(), FloorHeight = br.ReadInt16(),
                FloorTextureShiftX = br.ReadByte(), FloorTextureShiftY = br.ReadByte(),
                FloorTextureScale = br.ReadByte(), Padding = br.ReadByte()
            };
        }

        private static void ReadObjects(BinaryReader br, RothRawMap map)
        {
            Stream stream = br.BaseStream;
            long objectStart = (long)map.Header.VerticesOffset + map.Header.VerticesSectionSize +
                map.Header.CommandSectionSize + map.Header.Section7Size;
            if (objectStart < 0 || objectStart + 2 > stream.Length) return;

            stream.Position = objectStart;
            ushort sectionSize = br.ReadUInt16();
            if (sectionSize < 2 + map.Sectors.Count * 2 || objectStart + sectionSize > stream.Length)
                return;

            long tableStart = stream.Position;
            ushort[] relativeOffsets = new ushort[map.Sectors.Count];
            for (int i = 0; i < relativeOffsets.Length; i++)
                relativeOffsets[i] = br.ReadUInt16();

            for (int sectorIndex = 0; sectorIndex < relativeOffsets.Length; sectorIndex++)
            {
                ushort rel = relativeOffsets[sectorIndex];
                if (rel == 0) continue;
                long container = objectStart + rel;
                if (container < tableStart || container + 2 > objectStart + sectionSize) continue;
                stream.Position = container;
                byte count = br.ReadByte();
                br.ReadByte(); // repeated count
                for (int j = 0; j < count; j++)
                {
                    if (stream.Position + ObjectSize > objectStart + sectionSize) break;
                    RothObject obj = new RothObject {
                        PosX = br.ReadInt16(), PosY = br.ReadInt16(), TextureIndex = br.ReadByte(),
                        TextureSource = br.ReadByte(), Rotation = br.ReadByte(), Flags = br.ReadByte(),
                        Lighting = br.ReadByte(), RenderType = br.ReadByte(), PosZ = br.ReadInt16(),
                        Unknown0C = br.ReadUInt16(), ObjectId = br.ReadUInt16(), SectorIndex = sectorIndex
                    };
                    int idx = map.Objects.Count;
                    map.Objects.Add(obj);
                    map.Sectors[sectorIndex].ObjectIndices.Add(idx);
                }
            }
        }

        private static RothFace ReadFace(BinaryReader br)
        {
            return new RothFace {
                VertexOffset01 = br.ReadUInt16(), VertexOffset02 = br.ReadUInt16(), TextureMapOffset = br.ReadUInt16(),
                SectorOffset = br.ReadUInt16(), SisterFaceOffset = br.ReadUInt16(), FaceFlags = br.ReadUInt16()
            };
        }

        private static RothTextureMapping ReadTextureMapping(BinaryReader br)
        {
            return new RothTextureMapping {
                Unknown00 = br.ReadByte(), Type = br.ReadByte(), MidTextureIndex = br.ReadUInt16(),
                UpperTextureIndex = br.ReadUInt16(), LowerTextureIndex = br.ReadUInt16(), TextureFlags = br.ReadUInt16()
            };
        }

        private static RothMapMetadata ReadMetadata(BinaryReader br)
        {
            return new RothMapMetadata {
                InitPosX = br.ReadInt16(), InitPosZ = br.ReadInt16(), InitPosY = br.ReadInt16(), Rotation = br.ReadInt16(),
                MoveSpeed = br.ReadUInt16(), PlayerHeight = br.ReadUInt16(), MaxClimb = br.ReadUInt16(), MinFit = br.ReadUInt16(),
                Unknown10 = br.ReadUInt16(), CandleGlow = br.ReadInt16(), LightAmbience = br.ReadUInt16(),
                Unknown16 = br.ReadUInt16(), SkyTexture = br.ReadUInt16(), Unknown1A = br.ReadUInt16()
            };
        }

        private static void ResolveRelations(RothRawMap map, Dictionary<ushort,int> sectors, Dictionary<ushort,int> faces,
            Dictionary<ushort,int> mappings, Dictionary<ushort,int> vertices)
        {
            for (int i = 0; i < map.Sectors.Count; i++)
            {
                RothSector s = map.Sectors[i];
                int first;
                if (faces.TryGetValue(s.FirstFaceOffset, out first)) s.FirstFaceIndex = first;
            }

            for (int i = 0; i < map.Faces.Count; i++)
            {
                RothFace f = map.Faces[i];
                int value;
                if (vertices.TryGetValue(f.VertexOffset01, out value)) f.VertexIndex01 = value;
                if (vertices.TryGetValue(f.VertexOffset02, out value)) f.VertexIndex02 = value;
                if (mappings.TryGetValue(f.TextureMapOffset, out value)) f.TextureMappingIndex = value;
                if (sectors.TryGetValue(f.SectorOffset, out value)) f.SectorIndex = value;
                if (f.SisterFaceOffset != 0xFFFF && f.SisterFaceOffset != 0 && faces.TryGetValue(f.SisterFaceOffset, out value))
                    f.SisterFaceIndex = value;
            }
        }

        private static void ValidateHeader(RothRawHeader h, long length)
        {
            if (h.Version != ExpectedVersion) throw new InvalidDataException("Unsupported ROTH RAW version: 0x" + h.Version.ToString("X4"));
            if (h.Signature != ExpectedSignature) throw new InvalidDataException("Invalid ROTH RAW signature: 0x" + h.Signature.ToString("X4"));
            if (h.VerticesOffset != h.VerticesOffsetRepeat && h.VerticesOffsetRepeat != h.VerticesOffset - 2)
                throw new InvalidDataException("Unexpected duplicated vertex offset fields.");
            if (h.SectorsOffset >= length || h.FacesOffset >= length || h.FaceTextureMapsOffset >= length || h.VerticesOffset >= length)
                throw new InvalidDataException("ROTH RAW section offset points outside the file.");
        }

        private static ushort CheckedOffset(long value)
        {
            if (value < 0 || value > ushort.MaxValue) throw new InvalidDataException("ROTH RAW 16-bit offset overflow: " + value);
            return (ushort)value;
        }
    }
}
