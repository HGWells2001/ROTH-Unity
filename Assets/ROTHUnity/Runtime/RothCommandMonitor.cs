using System;
using System.Collections.Generic;
using ROTHUnity.Core;
using UnityEngine;

namespace ROTHUnity.Runtime
{
    /// <summary>Runtime bridge for original RAW command chains. 0.6 observes entry triggers and
    /// safely executes only state-only opcodes whose semantics are understood. Geometry-changing
    /// commands are reported, not guessed.</summary>
    public sealed class RothCommandMonitor : MonoBehaviour
    {
        public RothMapMeshBuilder MapBuilder;
        public Transform Player;
        public bool LogTriggeredChains=true;
        public bool ExperimentalSafeInterpreter=true;
        public string DBase100Path;
        public string DBase300Path;
        public string DBase400Path;
        public string DBase500Path;
        public RothWorldController WorldController;
        public RothNarrativePlayer NarrativePlayer;
        public RothGdvPlayer GdvPlayer;
        public string LastTrigger { get; private set; }
        public string LastCommand { get; private set; }
        public int CurrentSectorIndex { get { return _sector; } }
        public ushort CurrentSectorId { get { return _map!=null && _sector>=0 && _sector<_map.Sectors.Count ? _map.Sectors[_sector].SectorId : (ushort)0; } }
        private RothRawMap _map;
        private int _sector=-1;
        private readonly HashSet<ushort> _flags=new HashSet<ushort>();
        private readonly HashSet<int> _disabledCommands=new HashSet<int>();
        private readonly HashSet<int> _usedEntries=new HashSet<int>();
        private readonly HashSet<int> _usedCommands=new HashSet<int>();
        private readonly Dictionary<ushort,int> _inventory=new Dictionary<ushort,int>();
        private readonly HashSet<ushort> _everHadItems=new HashSet<ushort>();
        private readonly List<ushort> _takenInventory=new List<ushort>();
        public ushort HeldItemId { get; set; }
        public int InventoryCount { get { int total=0; foreach(var kv in _inventory) total+=kv.Value; return total; } }
        private Dictionary<ushort,int> _sectorIdToIndex;
        private RothDBase100Archive _dbase100;
        private RothDBaseNarrativeArchive _narrative;
        private Vector3 _lastPlayerPosition;
        private bool _mapChangedThisStep;
        private int _chainDepth;

        private void Start()
        {
            if(MapBuilder==null) MapBuilder=GetComponent<RothMapMeshBuilder>();
            if(WorldController==null) WorldController=GetComponent<RothWorldController>();
            if(Player==null && Camera.main!=null) Player=Camera.main.transform.root;
            ReloadCurrentMap();
        }

        public void ReloadCurrentMap()
        {
            if(MapBuilder==null || string.IsNullOrEmpty(MapBuilder.RawMapPath)) return;
            try
            {
                _map=RothRawMapReader.Read(MapBuilder.RawMapPath);
                if(_dbase100==null && !string.IsNullOrEmpty(DBase100Path) && System.IO.File.Exists(DBase100Path)) _dbase100=RothDBase100Archive.Load(DBase100Path);
                if(_narrative==null && !string.IsNullOrEmpty(DBase400Path) && System.IO.File.Exists(DBase400Path)) _narrative=RothDBaseNarrativeArchive.Load(DBase400Path,DBase500Path);
                _sector=-1; _usedEntries.Clear(); _usedCommands.Clear(); _disabledCommands.Clear();
                for(int i=0;i<_map.Commands.Count;i++) if((_map.Commands[i].Modifier & (1<<3))!=0) _disabledCommands.Add(i+1);
                BuildSectorMap();
                if(Player!=null) _lastPlayerPosition=Player.position;
                RunAutorunEntries();
            }
            catch(Exception e){Debug.LogException(e,this);}
        }
        private void Update()
        {
            if(_map==null || Player==null) return;
            Vector3 current=Player.position;
            Vector3 movement=current-_lastPlayerPosition;
            int now=FindSector(current);
            if(now!=_sector)
            {
                _sector=now;
                if(now>=0) TriggerEnterSector(_map.Sectors[now].SectorId,movement);
            }
            _lastPlayerPosition=current;
        }
        private void BuildSectorMap(){_sectorIdToIndex=new Dictionary<ushort,int>();for(int i=0;i<_map.Sectors.Count;i++)if(!_sectorIdToIndex.ContainsKey(_map.Sectors[i].SectorId))_sectorIdToIndex.Add(_map.Sectors[i].SectorId,i);}
        private void RunAutorunEntries()
        {
            for(int i=0;i<_map.Commands.Count;i++) if((_map.Commands[i].Modifier&1)!=0 || _map.Commands[i].BaseOpcode==61) RunChain(i+1,"autorun");
        }
        private void TriggerEnterSector(ushort sectorId, Vector3 movement)
        {
            int direction = MovementDirection(movement);
            foreach(int entry in _map.EntryCommandIndices)
            {
                if(entry<=0 || entry>_map.Commands.Count) continue; RothCommand c=_map.Commands[entry-1];
                if(c.BaseOpcode!=19 || c.Arguments.Length<2 || c.Arguments[1]!=sectorId) continue;
                ushort flags=c.Arguments[0];
                // Bits 1..4 are documented as Not East/North/West/South.
                if(direction>=0 && (flags & (1<<direction))!=0) continue;
                if((flags&(1<<4))!=0 && _usedEntries.Contains(entry)) continue; // Only Once
                if((flags&(1<<4))!=0) _usedEntries.Add(entry);
                RunChain(entry,"enter sector "+sectorId);
            }
        }

        private int MovementDirection(Vector3 movement)
        {
            if(movement.sqrMagnitude<0.000001f) return -1;
            float x=movement.x; if(MapBuilder!=null && MapBuilder.MirrorWorldX) x=-x;
            float y=movement.z;
            if(Mathf.Abs(x)>=Mathf.Abs(y)) return x>=0f ? 0 : 2; // East / West
            return y>=0f ? 1 : 3; // North / South
        }
        public void TriggerWorldClick(Vector3 worldPoint, Vector3 worldNormal, bool right)
        {
            if(_map==null || MapBuilder==null) return;
            float rx=(worldPoint.x-MapBuilder.transform.position.x)/MapBuilder.CoordinateScale; if(MapBuilder.MirrorWorldX) rx=-rx;
            float ry=(worldPoint.z-MapBuilder.transform.position.z)/MapBuilder.CoordinateScale;
            int sector=FindSectorRaw(rx,ry); if(sector<0)return; RothSector sec=_map.Sectors[sector];
            if(Mathf.Abs(worldNormal.y)>0.55f)
            {
                TriggerEntryById(right?(byte)49:(byte)25,sec.SectorId,(right?"right-click sector ":"left-click floor ")+sec.SectorId);
                return;
            }
            int face=FindNearestFace(sector,rx,ry); if(face<0)return; RothFace f=_map.Faces[face];
            if(f.TextureMappingIndex<0 || f.TextureMappingIndex>=_map.TextureMappings.Count)return; RothTextureMapping tm=_map.TextureMappings[f.TextureMappingIndex];
            if(!tm.HasAdditionalMetadata)return;
            TriggerEntryById(right?(byte)50:(byte)24,tm.FaceId,(right?"right-click face ":"left-click face ")+tm.FaceId);
        }
        private void TriggerEntryById(byte opcode, ushort id, string reason)
        {
            foreach(int entry in _map.EntryCommandIndices)
            {
                if(entry<=0||entry>_map.Commands.Count)continue; RothCommand c=_map.Commands[entry-1];
                if(c.BaseOpcode!=opcode||c.Arguments.Length<2||c.Arguments[1]!=id)continue;
                bool onlyOnce=(opcode==24||opcode==25||opcode==50) && (c.Arguments[0]&(1<<4))!=0;
                if(onlyOnce&&_usedEntries.Contains(entry))continue; _usedEntries.Add(entry); RunChain(entry,reason);
            }
        }
        private int FindNearestFace(int sector,float x,float y)
        {
            RothSector sec=_map.Sectors[sector]; int best=-1; float bestD=float.MaxValue;
            int end=Math.Min(_map.Faces.Count,sec.FirstFaceIndex+sec.FacesCount);
            for(int i=sec.FirstFaceIndex;i<end;i++)
            {
                RothFace f=_map.Faces[i]; if(f.VertexIndex01<0||f.VertexIndex02<0)continue; RothVertex a=_map.Vertices[f.VertexIndex01],b=_map.Vertices[f.VertexIndex02];
                float d=PointSegmentDistance(x,y,a.X,a.Y,b.X,b.Y); if(d<bestD){bestD=d;best=i;}
            }
            return bestD<=64f?best:-1;
        }
        private static float PointSegmentDistance(float px,float py,float ax,float ay,float bx,float by)
        { float dx=bx-ax,dy=by-ay,l2=dx*dx+dy*dy;if(l2<=0.0001f)return Mathf.Sqrt((px-ax)*(px-ax)+(py-ay)*(py-ay));float t=Mathf.Clamp01(((px-ax)*dx+(py-ay)*dy)/l2);float x=ax+t*dx,y=ay+t*dy;return Mathf.Sqrt((px-x)*(px-x)+(py-y)*(py-y)); }

        public void TriggerObjectLeftClick(ushort objectId) { TriggerObjectEntry(8,objectId,"left-click object "+objectId); }
        public void TriggerObjectRightClick(ushort objectId) { TriggerObjectEntry(48,objectId,"right-click object "+objectId); }
        public void TriggerObjectTouch(ushort objectId) { TriggerObjectEntry(57,objectId,"touch object "+objectId); }
        private void TriggerObjectEntry(byte opcode, ushort objectId, string reason)
        {
            if(_map==null) return;
            foreach(int entry in _map.EntryCommandIndices)
            {
                if(entry<=0 || entry>_map.Commands.Count) continue; RothCommand c=_map.Commands[entry-1];
                if(c.BaseOpcode!=opcode || c.Arguments.Length<2 || c.Arguments[1]!=objectId) continue;
                bool onlyOnce = opcode==8 && (c.Arguments[0]&(1<<4))!=0;
                if(onlyOnce && _usedEntries.Contains(entry)) continue;
                _usedEntries.Add(entry); RunChain(entry,reason);
            }
        }

        private void RunChain(int start,string reason)
        {
            if(_map==null || _chainDepth>=16) { if(LogTriggeredChains) Debug.LogWarning("ROTH command recursion limit reached at #"+start,this); return; }
            _chainDepth++;
            try
            {
            int index=start; int safety=0; bool lastSucceeded=true;
            LastTrigger="Chain #"+start+": "+reason;
            if(LogTriggeredChains) Debug.Log("ROTH command chain #"+start+" triggered: "+reason,this);
            while(index>0 && index<=_map.Commands.Count && safety++<256)
            {
                if(_disabledCommands.Contains(index)) break;
                RothCommand c=_map.Commands[index-1];
                _mapChangedThisStep=false;
                bool ok=ExperimentalSafeInterpreter ? ExecuteSafe(index,c,lastSucceeded) : true;
                LastCommand=string.Format("#{0} {1} [{2}]",index,RothCommandCatalog.Get(c.BaseOpcode).Name,c.BaseOpcode);
                if(LogTriggeredChains) Debug.Log(string.Format("  #{0} {1} opcode={2} next={3} safe={4}",index,RothCommandCatalog.Get(c.BaseOpcode).Name,c.BaseOpcode,c.NextCommandIndex,ok),this);
                if(_mapChangedThisStep) break;
                lastSucceeded=ok; index=c.NextCommandIndex;
            }
            }
            finally { _chainDepth--; }
        }
        private bool ExecuteSafe(int index,RothCommand c,bool previousSucceeded)
        {
            switch(c.BaseOpcode)
            {
                case 1: case 62: return true;
                case 16: // Activate SFX Node
                    if(c.Arguments.Length<2 || MapBuilder==null) return false; return MapBuilder.ActivateSfxNode(c.Arguments[1]);
                case 43: // DBASE100 Command: inspect original global action without guessing its effects.
                    if(c.Arguments.Length<2) return false; return ExecuteGlobalAction(c.Arguments[1], "map opcode 43");
                case 48: case 49: case 50: // Right-click entries carry a DBASE100 global command in arg3.
                    if(c.Arguments.Length>=3) return ExecuteGlobalAction(c.Arguments[2], RothCommandCatalog.Get(c.BaseOpcode).Name); return true;
                case 38: // Set/Unset Flag
                    if(c.Arguments.Length<2) return false;
                    if((c.Arguments[0]&1)!=0) _flags.Remove(c.Arguments[1]); else _flags.Add(c.Arguments[1]); return true;
                case 39: // If Not Item / If Item
                    if(c.Arguments.Length<2) return false;
                    ushort item=c.Arguments[1]; ushort itemFlags=c.Arguments[0];
                    bool present;
                    if((itemFlags&(1<<1))!=0) present=HeldItemId==item; // In Hand
                    else if((itemFlags&(1<<2))!=0) present=_everHadItems.Contains(item); // Ever Had
                    else present=GetItemCount(item)>0;
                    return (itemFlags&1)!=0 ? present : !present;
                case 40: // If Not Flag / If Flag
                    if(c.Arguments.Length<2) return false;
                    bool has=_flags.Contains(c.Arguments[1]); return (c.Arguments[0]&1)!=0 ? has : !has;
                case 41: // Give Item
                    if(c.Arguments.Length<2) return false;
                    AddItem(c.Arguments[1],1); return true;
                case 42: // Remove Item
                    if(c.Arguments.Length<2) return false;
                    if((c.Arguments[0]&(1<<4))!=0 && _usedCommands.Contains(index)) return true;
                    RemoveItem(c.Arguments[1],1); if(HeldItemId==c.Arguments[1] && GetItemCount(c.Arguments[1])==0) HeldItemId=0;
                    if((c.Arguments[0]&(1<<4))!=0) _usedCommands.Add(index); return true;
                case 23: // Toggle command enable state
                    if(c.Arguments.Length<2) return false; int target=c.Arguments[1];
                    if((c.Arguments[0]&(1<<1))!=0) _disabledCommands.Remove(target);
                    else if((c.Arguments[0]&(1<<2))!=0) _disabledCommands.Add(target);
                    else { if(_disabledCommands.Contains(target)) _disabledCommands.Remove(target); else _disabledCommands.Add(target); } return true;
                case 64: // Run Map Command. Only observe; nested invocation is safe and bounded.
                    if(c.Arguments.Length<1) return false; RunChain(c.Arguments[0],"Run Map Command from #"+index); return true;
                case 56: // Jump If Next Fails. The actual branch is represented in arg2.
                    if(previousSucceeded) return true;
                    if(c.Arguments.Length>1) RunChain(c.Arguments[1],"Jump If Next Fails from #"+index); return false;
                case 66: // Take Inventory / Give Back
                    if(c.Arguments.Length<1) return false;
                    if((c.Arguments[0]&1)!=0)
                    { foreach(ushort id in _takenInventory) AddItem(id,1); _takenInventory.Clear(); return true; }
                    _takenInventory.Clear(); foreach(var kv in _inventory) for(int n=0;n<kv.Value;n++) _takenInventory.Add(kv.Key); _inventory.Clear(); HeldItemId=0; return true;
                case 7: // Change Floor/Ceiling Height (prototype; timing not yet retail-verified)
                    if(c.Arguments.Length<4 || MapBuilder==null) return false;
                    RothSectorMover mover=MapBuilder.GetComponent<RothSectorMover>();
                    if(mover==null) mover=MapBuilder.gameObject.AddComponent<RothSectorMover>();
                    mover.Builder=MapBuilder;
                    return mover.Move(c.Arguments[1],(c.Arguments[0]&1)!=0,
                        unchecked((short)c.Arguments[2]),unchecked((short)c.Arguments[3]),
                        (c.Arguments[0]&(1<<2))!=0,
                        c.Arguments.Length > 4 ? c.Arguments[4] : (ushort)0);
                case 9: // RAW horizontal sector translation (prototype)
                    if (c.Arguments.Length < 6 || MapBuilder == null) return false;
                    RothHorizontalSectorMover horizontal = MapBuilder.GetComponent<RothHorizontalSectorMover>();
                    if (horizontal == null) horizontal = MapBuilder.gameObject.AddComponent<RothHorizontalSectorMover>();
                    horizontal.Builder = MapBuilder;
                    return horizontal.Move(c.Arguments[1], (c.Arguments[0] & (1 << 6)) != 0,
                        unchecked((short)c.Arguments[2]), unchecked((short)c.Arguments[3]),
                        c.Arguments[4], (c.Arguments[0] & (1 << 5)) != 0, c.Arguments[0]);
                case 10: // Change Floor Texture
                    if(c.Arguments.Length<4 || MapBuilder==null) return false;
                    return MapBuilder.RuntimeChangeFloorTexture(c.Arguments[1],c.Arguments[2],c.Arguments[3],c.Arguments[0]);
                case 12: // Change Face Texture Advanced
                    if(c.Arguments.Length<7 || MapBuilder==null) return false;
                    return MapBuilder.RuntimeChangeFaceTextureAdvanced(c.Arguments[1],c.Arguments[2],c.Arguments[3],c.Arguments[0],c.Arguments[5],c.Arguments[6]);
                case 13: // Change Object Texture
                    if(c.Arguments.Length<4 || MapBuilder==null) return false;
                    return MapBuilder.RuntimeChangeObjectTexture(c.Arguments[1],c.Arguments[3]);
                case 52: // Change Face Texture Simple
                    if(c.Arguments.Length<3 || MapBuilder==null) return false;
                    return MapBuilder.RuntimeChangeFaceTextureSimple(c.Arguments[1],c.Arguments[2]);
                case 59: // Map Transition / Warp
                    if(c.Arguments.Length<2 || WorldController==null) return false;
                    string mapName=DecodePackedMapName(c.Arguments,2);
                    ushort destination=c.Arguments[1];
                    if(string.IsNullOrEmpty(mapName)) return WorldController.WarpWithinCurrentMap(destination);
                    bool loaded=WorldController.LoadMap(mapName,destination); _mapChangedThisStep=loaded; return loaded;
                default: return true; // observed only; no mutation until implemented faithfully
            }
        }
        private static string DecodePackedMapName(ushort[] args, int start)
        {
            System.Text.StringBuilder b=new System.Text.StringBuilder(8);
            for(int i=start;i<args.Length && i<start+4;i++)
            {
                ushort w=args[i]; char a=(char)(w&0xFF), z=(char)((w>>8)&0xFF);
                if(a!=0) b.Append(a); if(z!=0) b.Append(z);
            }
            return b.ToString().Trim();
        }

        private bool ExecuteGlobalAction(int actionIndex, string reason)
        {
            if(_dbase100==null) return false;
            if(_chainDepth>=16) return false;
            RothDBase100Action a=_dbase100.GetGameAction(actionIndex);
            if(a==null) { if(LogTriggeredChains) Debug.Log("ROTH DBASE100 action #"+actionIndex+" missing ("+reason+")",this); return false; }
            if(LogTriggeredChains) Debug.Log("ROTH DBASE100 action #"+actionIndex+" ("+reason+") commands="+a.Commands.Count,this);

            // Choice actions have a stable retail layout: option strings precede the first Start Choice (9),
            // followed by one 9..10 block per original option. Conditional opcodes can hide individual strings,
            // but branch ordinals remain unchanged. Parse the entire construct before the linear interpreter.
            int firstChoiceBlock=FindOpcode(a,9,0);
            if(firstChoiceBlock>=0 && NarrativePlayer!=null && _narrative!=null)
                return BeginChoiceAction(actionIndex,a,firstChoiceBlock,reason);

            return ExecuteGlobalRange(actionIndex,a,0,a.Commands.Count,reason);
        }

        private int FindOpcode(RothDBase100Action action, byte opcode, int start)
        {
            if(action==null) return -1;
            for(int i=Mathf.Max(0,start);i<action.Commands.Count;i++) if(action.Commands[i].Opcode==opcode) return i;
            return -1;
        }

        private bool BeginChoiceAction(int actionIndex, RothDBase100Action action, int firstChoiceBlock, string reason)
        {
            var visibleTexts=new List<string>();
            var visibleSlots=new List<int>();
            int choiceSlot=0;

            // Execute the prefix and collect option text. Opcodes 13/141 conditionally suppress exactly the
            // following command in the retail scripts, so preserve the hidden option's ordinal even when skipped.
            for(int i=0;i<firstChoiceBlock;i++)
            {
                RothDBase100Command g=action.Commands[i];
                int arg=g.Argument;
                if(g.Opcode==13 || g.Opcode==141)
                {
                    bool condition = g.Opcode==13 ? _flags.Contains((ushort)arg) : !_flags.Contains((ushort)arg);
                    if(i+1<firstChoiceBlock && action.Commands[i+1].Opcode==8)
                    {
                        RothDBase100Command option=action.Commands[++i];
                        if(condition)
                        {
                            RothDBaseTextEntry e=_narrative.ReadText((uint)option.Argument,false);
                            visibleTexts.Add(e!=null && !string.IsNullOrEmpty(e.Text)?e.Text:("Choice "+(choiceSlot+1)));
                            visibleSlots.Add(choiceSlot);
                        }
                        choiceSlot++;
                        continue;
                    }
                    if(!condition) i++;
                    continue;
                }
                if(g.Opcode==8)
                {
                    RothDBaseTextEntry e=_narrative.ReadText((uint)arg,false);
                    visibleTexts.Add(e!=null && !string.IsNullOrEmpty(e.Text)?e.Text:("Choice "+(choiceSlot+1)));
                    visibleSlots.Add(choiceSlot++);
                    continue;
                }
                if(!ExecuteGlobalSingle(actionIndex,g,ref i,action,reason)) return false;
            }

            var branches=new List<Vector2Int>();
            int cursor=firstChoiceBlock;
            while(cursor<action.Commands.Count)
            {
                if(action.Commands[cursor].Opcode!=9){cursor++;continue;}
                int end=FindMatchingEndChoice(action,cursor+1);
                if(end<0) break;
                branches.Add(new Vector2Int(cursor+1,end));
                cursor=end+1;
            }
            if(visibleTexts.Count==0 || branches.Count==0) return true;
            if(LogTriggeredChains) Debug.Log("ROTH choice action #"+actionIndex+" visible="+visibleTexts.Count+" branches="+branches.Count,this);
            Action showChoices = null;
            showChoices = () =>
            {
                if(GdvPlayer!=null) GdvPlayer.PlaybackFinished-=showChoices;
                NarrativePlayer.ShowChoices(visibleTexts, selected =>
                {
                    if(selected<0 || selected>=visibleSlots.Count) return;
                    int slot=visibleSlots[selected];
                    if(slot<0 || slot>=branches.Count) return;
                    Vector2Int r=branches[slot];
                    ExecuteGlobalRange(actionIndex,action,r.x,r.y,"choice "+(slot+1)+" from action #"+actionIndex);
                });
            };
            if(GdvPlayer!=null && GdvPlayer.IsPlaying) GdvPlayer.PlaybackFinished+=showChoices; else showChoices();
            return true;
        }

        private int FindMatchingEndChoice(RothDBase100Action action,int start)
        {
            int depth=0;
            for(int i=start;i<action.Commands.Count;i++)
            {
                byte op=action.Commands[i].Opcode;
                if(op==9) depth++;
                else if(op==10){if(depth==0)return i;depth--;}
            }
            return -1;
        }

        private bool ExecuteGlobalRange(int actionIndex,RothDBase100Action action,int start,int end,string reason)
        {
            if(_chainDepth>=16) return false;
            _chainDepth++;
            try
            {
                for(int i=Mathf.Max(0,start);i<Mathf.Min(end,action.Commands.Count);i++)
                {
                    if(!ExecuteGlobalSingle(actionIndex,action.Commands[i],ref i,action,reason)) return false;
                }
                return true;
            }
            finally { _chainDepth--; }
        }

        private bool ExecuteGlobalSingle(int actionIndex,RothDBase100Command g,ref int i,RothDBase100Action action,string reason)
        {
            int arg=g.Argument;
            if(LogTriggeredChains) Debug.Log("    global["+i+"] opcode="+g.Opcode+" arg="+arg,this);
            switch(g.Opcode)
            {
                case 1: if(!_flags.Contains((ushort)arg)) return false; break;
                case 129: if(_flags.Contains((ushort)arg)) return false; break;
                case 13: if(!_flags.Contains((ushort)arg)) i++; break;
                case 141: if(_flags.Contains((ushort)arg)) i++; break;
                case 4: _flags.Add((ushort)arg); break;
                case 132: _flags.Remove((ushort)arg); break;
                case 2: if(GetItemCount((ushort)(arg&0xFFFF)) <= ((arg>>16)&0xFF)) return false; break;
                case 130: if(GetItemCount((ushort)(arg&0xFFFF)) > ((arg>>16)&0xFF)) return false; break;
                case 131: if(GetItemCount((ushort)(arg&0xFFFF)) != ((arg>>16)&0xFF)) return false; break;
                case 5:
                    if(_narrative!=null && NarrativePlayer!=null) NarrativePlayer.Show(_narrative.ReadText((uint)arg,true));
                    break;
                case 7: PlayCutscene(arg); break;
                case 8:
                    if(_narrative!=null && NarrativePlayer!=null) NarrativePlayer.Show(_narrative.ReadText((uint)arg,false));
                    break;
                case 9: case 10: break; // handled by BeginChoiceAction when a complete choice construct exists
                case 11:
                    {
                        int endRandom=FindOpcode(action,12,i+1);
                        if(endRandom>i+1)
                        {
                            var candidates=new List<int>();
                            bool flat=true;
                            for(int r=i+1;r<endRandom;r++)
                            {
                                byte op=action.Commands[r].Opcode;
                                if(op==5 || op==7 || op==25 || op==17 || op==145) candidates.Add(r);
                                else { flat=false; break; }
                            }
                            if(flat && candidates.Count>0)
                            {
                                int picked=candidates[UnityEngine.Random.Range(0,candidates.Count)];
                                int tmp=picked;
                                ExecuteGlobalSingle(actionIndex,action.Commands[picked],ref tmp,action,"random block from action #"+actionIndex);
                            }
                            else if(LogTriggeredChains) Debug.Log("ROTH complex random block observed but not executed in action #"+actionIndex,this);
                            i=endRandom;
                        }
                    }
                    break;
                case 12: break;
                case 14:
                    if(NarrativePlayer!=null) NarrativePlayer.ShowDBase300Image(DBase300Path,arg);
                    break;
                case 17: AddItem((ushort)arg,1); break;
                case 145: RemoveItem((ushort)arg,1); break;
                case 25: if(MapBuilder!=null) MapBuilder.PlaySfxIndex((ushort)arg); break;
                case 29: if(!ExecuteGlobalAction(arg,"global jump from action #"+actionIndex)) return false; break;
                case 35: RunChain(arg,"DBASE100 map callback from action #"+actionIndex); break;
                case 53: if(!ExecuteGlobalAction(arg,"DBASE100 callback from action #"+actionIndex)) return false; break;
                case 156: i=action.Commands.Count; return true;
                default: break;
            }
            return true;
        }

        private bool PlayCutscene(int index)
        {
            if(_dbase100==null || GdvPlayer==null) return false;
            RothDBase100Cutscene c=_dbase100.GetCutscene(index);
            if(c==null && index>0) c=_dbase100.GetCutscene(index-1);
            if(c==null || string.IsNullOrEmpty(c.Name)) return false;
            string root=WorldController!=null?WorldController.GameRoot:null;
            string path=RothInstallLocator.FindGdvFile(root,c.Name);
            if(string.IsNullOrEmpty(path))
            {
                if(LogTriggeredChains) Debug.LogWarning("ROTH GDV not found for cutscene "+c.Name+" (#"+index+")",this);
                return false;
            }
            if(c.TextOffset!=0 && _narrative!=null && NarrativePlayer!=null) NarrativePlayer.Show(_narrative.ReadText(c.TextOffset,false));
            IList<RothDBaseSubtitleEntry> subs=null;
            if(_narrative!=null && c.SubtitleOffset!=0) subs=_narrative.ReadCutsceneSubtitles(c.SubtitleOffset);
            return GdvPlayer.PlayWithSubtitles(path,subs);
        }

        private int GetItemCount(ushort item)
        {
            int count; return _inventory.TryGetValue(item,out count)?count:0;
        }
        private void AddItem(ushort item,int amount)
        {
            if(amount<=0)return; int count=GetItemCount(item); _inventory[item]=count+amount; _everHadItems.Add(item);
        }
        private void RemoveItem(ushort item,int amount)
        {
            if(amount<=0)return; int count=GetItemCount(item)-amount; if(count>0)_inventory[item]=count;else _inventory.Remove(item);
        }

        private int FindSector(Vector3 world)
        {
            float rx=(world.x-MapBuilder.transform.position.x)/MapBuilder.CoordinateScale; if(MapBuilder.MirrorWorldX) rx=-rx;
            float ry=(world.z-MapBuilder.transform.position.z)/MapBuilder.CoordinateScale; return FindSectorRaw(rx,ry);
        }
        private int FindSectorRaw(float px,float py)
        {
            for(int s=0;s<_map.Sectors.Count;s++)
            {
                RothSector sec=_map.Sectors[s]; if(sec.FirstFaceIndex<0||sec.FacesCount<3)continue; bool inside=false;
                for(int i=0,j=sec.FacesCount-1;i<sec.FacesCount;j=i++)
                {
                    RothFace fi=_map.Faces[sec.FirstFaceIndex+i],fj=_map.Faces[sec.FirstFaceIndex+j];if(fi.VertexIndex01<0||fj.VertexIndex01<0)continue;
                    RothVertex a=_map.Vertices[fi.VertexIndex01],b=_map.Vertices[fj.VertexIndex01]; bool hit=((a.Y>py)!=(b.Y>py))&&(px<(b.X-a.X)*(py-a.Y)/(float)(b.Y-a.Y)+a.X);if(hit)inside=!inside;
                }
                if(inside)return s;
            } return -1;
        }
    }
}
