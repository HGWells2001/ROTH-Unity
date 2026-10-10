using System.Collections.Generic;

namespace ROTHUnity.Core
{
    public sealed class RothCommandInfo
    {
        public byte Opcode; public string Name; public bool Entry;
        public RothCommandInfo(byte opcode,string name,bool entry=false){Opcode=opcode;Name=name;Entry=entry;}
    }

    public static class RothCommandCatalog
    {
        private static readonly Dictionary<byte,RothCommandInfo> Infos = new Dictionary<byte,RothCommandInfo>
        {
            {1,new RothCommandInfo(1,"Empty (No SFX)")},
            {2,new RothCommandInfo(2,"Light Switch",true)}, {3,new RothCommandInfo(3,"Modify Sector",true)},
            {7,new RothCommandInfo(7,"Change Floor/Ceiling Height")}, {8,new RothCommandInfo(8,"On Left-Click Object",true)},
            {9,new RothCommandInfo(9,"Move Sector")}, {10,new RothCommandInfo(10,"Change Floor Texture")},
            {12,new RothCommandInfo(12,"Change Face Texture Advanced")}, {13,new RothCommandInfo(13,"Change Object Texture")},
            {14,new RothCommandInfo(14,"Scroll Sector Texture")}, {15,new RothCommandInfo(15,"Scroll Face Texture")},
            {16,new RothCommandInfo(16,"Activate SFX Node")}, {17,new RothCommandInfo(17,"Flash Lights")},
            {18,new RothCommandInfo(18,"Delay Timer")}, {19,new RothCommandInfo(19,"On Enter Sector",true)},
            {21,new RothCommandInfo(21,"Count")}, {22,new RothCommandInfo(22,"Spawn Object Simple")},
            {23,new RothCommandInfo(23,"Toggle Command")}, {24,new RothCommandInfo(24,"On Left-Click Face",true)},
            {25,new RothCommandInfo(25,"On Left-Click Floor",true)}, {26,new RothCommandInfo(26,"On Attack Face",true)},
            {27,new RothCommandInfo(27,"On Enemy Killed",true)}, {28,new RothCommandInfo(28,"Cycle Texture")},
            {29,new RothCommandInfo(29,"Change Lighting")}, {30,new RothCommandInfo(30,"Modify Count")},
            {31,new RothCommandInfo(31,"Texture Change Count")}, {32,new RothCommandInfo(32,"Cycle Object Texture")},
            {34,new RothCommandInfo(34,"Count (Additional Arg)")}, {35,new RothCommandInfo(35,"Change Object Height")},
            {36,new RothCommandInfo(36,"Rotate Object")}, {37,new RothCommandInfo(37,"On Texture Animation Ends",true)},
            {38,new RothCommandInfo(38,"Set/Unset Flag")}, {39,new RothCommandInfo(39,"If Not Item")},
            {40,new RothCommandInfo(40,"If Not Flag")}, {41,new RothCommandInfo(41,"Give Item")},
            {42,new RothCommandInfo(42,"Remove Item")}, {43,new RothCommandInfo(43,"DBASE100 Command")},
            {45,new RothCommandInfo(45,"Particle Effect")}, {46,new RothCommandInfo(46,"Smash Face Texture")},
            {47,new RothCommandInfo(47,"Open Door")}, {48,new RothCommandInfo(48,"On Right-Click Object",true)},
            {49,new RothCommandInfo(49,"On Right-Click Sector",true)}, {50,new RothCommandInfo(50,"On Right-Click Face",true)},
            {51,new RothCommandInfo(51,"Apply Damage")}, {52,new RothCommandInfo(52,"Change Face Texture Simple")},
            {53,new RothCommandInfo(53,"Face Emits Damage")}, {54,new RothCommandInfo(54,"DBASE100 Command If Next Fails")},
            {55,new RothCommandInfo(55,"Change Texture If Command Chain True",true)}, {56,new RothCommandInfo(56,"Jump If Next Fails")},
            {57,new RothCommandInfo(57,"On Touch Object",true)}, {58,new RothCommandInfo(58,"Change Object ID")},
            {59,new RothCommandInfo(59,"Map Transition / Warp")}, {60,new RothCommandInfo(60,"Spawn Object Advanced")},
            {61,new RothCommandInfo(61,"Autorun Timer")}, {62,new RothCommandInfo(62,"Empty (Allow SFX)")},
            {63,new RothCommandInfo(63,"Player Rotation")}, {64,new RothCommandInfo(64,"Run Map Command")},
            {65,new RothCommandInfo(65,"Slow Player Speed")}, {66,new RothCommandInfo(66,"Take Inventory")}
        };

        public static RothCommandInfo Get(byte opcode)
        { RothCommandInfo info; return Infos.TryGetValue(opcode,out info)?info:new RothCommandInfo(opcode,"Unknown opcode "+opcode); }
    }
}
