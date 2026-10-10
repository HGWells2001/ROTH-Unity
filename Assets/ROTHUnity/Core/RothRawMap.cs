using System;
using System.Collections.Generic;

namespace ROTHUnity.Core
{
    [Serializable]
    public sealed class RothRawMap
    {
        public RothRawHeader Header = new RothRawHeader();
        public RothMapMetadata Metadata = new RothMapMetadata();
        public readonly List<RothSector> Sectors = new List<RothSector>();
        public readonly List<RothFace> Faces = new List<RothFace>();
        public readonly List<RothTextureMapping> TextureMappings = new List<RothTextureMapping>();
        public readonly List<RothMidPlatform> MidPlatforms = new List<RothMidPlatform>();
        public readonly List<RothVertex> Vertices = new List<RothVertex>();
        public readonly List<RothObject> Objects = new List<RothObject>();
        public readonly List<RothCommand> Commands = new List<RothCommand>();
        public readonly List<int> EntryCommandIndices = new List<int>();
        public readonly List<RothCommandCategory> CommandCategories = new List<RothCommandCategory>();
        public readonly List<RothSoundEffect> SoundEffects = new List<RothSoundEffect>();
        public readonly List<RothSoundZone> SoundZones = new List<RothSoundZone>();
    }

    [Serializable]
    public sealed class RothRawHeader
    {
        public ushort VerticesOffset, Version, SectorsOffset, FacesOffset, FaceTextureMapsOffset;
        public ushort MapMetadataOffset, VerticesOffsetRepeat, Signature, MidPlatformsSection;
        public ushort Section7Size, VerticesSectionSize, ObjectsSectionsSize, FooterSize;
        public ushort CommandSectionSize, SectorCount;
    }

    [Serializable]
    public sealed class RothSector
    {
        public short CeilingHeight, FloorHeight;
        public ushort Unknown04, CeilingTextureIndex, FloorTextureIndex;
        public byte SectorFlags, Lighting;
        public sbyte TextureMapOverride;
        public byte FacesCount;
        public ushort FirstFaceOffset;
        public byte CeilingTextureShiftX, CeilingTextureShiftY, FloorTextureShiftX, FloorTextureShiftY;
        public ushort SectorId, AdditionalSectorFlags, IntermediateFloorOffset;
        public int FirstFaceIndex = -1;
        public int IntermediateFloorIndex = -1;
        public readonly List<int> ObjectIndices = new List<int>();
    }

    [Serializable]
    public sealed class RothFace
    {
        public ushort VertexOffset01, VertexOffset02, TextureMapOffset, SectorOffset, SisterFaceOffset, FaceFlags;
        public int VertexIndex01 = -1, VertexIndex02 = -1, TextureMappingIndex = -1, SectorIndex = -1, SisterFaceIndex = -1;
    }

    [Serializable]
    public sealed class RothTextureMapping
    {
        public byte Unknown00, Type;
        public ushort MidTextureIndex, UpperTextureIndex, LowerTextureIndex, TextureFlags;
        public bool HasAdditionalMetadata;
        public sbyte ShiftTextureX, ShiftTextureY;
        public ushort FaceId;
    }


    [Serializable]
    public sealed class RothMidPlatform
    {
        public ushort CeilingTextureIndex;
        public short CeilingHeight;
        public byte CeilingTextureShiftX, CeilingTextureShiftY;
        public ushort FloorTextureIndex;
        public short FloorHeight;
        public byte FloorTextureShiftX, FloorTextureShiftY, FloorTextureScale, Padding;
    }

    [Serializable]
    public sealed class RothObject
    {
        public short PosX, PosY;
        public byte TextureIndex, TextureSource, Rotation, Flags, Lighting, RenderType;
        public short PosZ;
        public ushort Unknown0C, ObjectId;
        public int SectorIndex = -1;
    }

    [Serializable]
    public struct RothVertex
    {
        public short X;
        public short Y;
        public RothVertex(short x, short y) { X = x; Y = y; }
    }


    [Serializable]
    public sealed class RothCommand
    {
        public ushort RelativeOffset;
        public byte Modifier, BaseOpcode;
        public ushort NextCommandIndex;
        public ushort[] Arguments = Array.Empty<ushort>();
    }

    [Serializable]
    public sealed class RothCommandCategory
    {
        public int Category;
        public ushort ReferenceTableOffset, Count;
    }


    [Serializable]
    public sealed class RothSoundEffect
    {
        public short PosX, PosY;
        public ushort SfxIndex, SfxId;
        public byte Flags, ZoneIndex;
        public ushort AudibleRadius, LoopDelay, Unknown0E;
        public byte Volume, Unknown11;
    }

    [Serializable]
    public sealed class RothSoundZone
    {
        public ushort ZoneCount;
        public byte[] Dampen = new byte[3];
        public byte[] Flags = new byte[3];
        public short[] XLower = new short[3];
        public short[] YLower = new short[3];
        public short[] XUpper = new short[3];
        public short[] YUpper = new short[3];
    }

    [Serializable]
    public sealed class RothMapMetadata
    {
        public short InitPosX, InitPosZ, InitPosY, Rotation;
        public ushort MoveSpeed, PlayerHeight, MaxClimb, MinFit, Unknown10;
        public short CandleGlow;
        public ushort LightAmbience, Unknown16, SkyTexture, Unknown1A;
    }
}
