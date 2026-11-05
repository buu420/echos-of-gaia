using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    // Real installed region scripts (tools/Export-FF9ExitNativeCases.py -> tests/fixtures/exit-native-cases.json)
    // replayed with FieldExitPaths: the B.D. Station air cab, the Lindblum Castle lift, the Burmecia ledge and
    // the Evil Forest walk-in into the Prima Vista scene. Memory starts cleared; the player is in the region.
    internal static class FieldExitNativeCaseTests
    {
        private sealed class Case{public int Field,Sid,Player,Tag;public Dictionary<string,List<ScriptInstruction>> Functions=new Dictionary<string,List<ScriptInstruction>>();}
        private static Dictionary<string,int> _opcodes;private static Dictionary<int,Case> _cases;
        private static Case Load(int field)
        {
            if(_cases==null) {
                string dir=AppContext.BaseDirectory,path=Environment.GetEnvironmentVariable("FF9_EXIT_CASES");
                while(path==null && dir!=null){string candidate=Path.Combine(dir,"tests","fixtures","exit-native-cases.json");if(File.Exists(candidate)){path=candidate;break;}dir=Path.GetDirectoryName(dir);}
                if(path==null)throw new Exception("exit-native-cases.json fixture not found");
                using(var document=JsonDocument.Parse(File.ReadAllText(path))) {
                    _opcodes=new Dictionary<string,int>();foreach(var op in document.RootElement.GetProperty("opcodes").EnumerateObject())_opcodes[op.Name]=op.Value.GetInt32();
                    _cases=new Dictionary<int,Case>();
                    foreach(var item in document.RootElement.GetProperty("cases").EnumerateArray()) {
                        var c=new Case{Field=item.GetProperty("field").GetInt32(),Sid=item.GetProperty("sid").GetInt32(),Player=item.GetProperty("player").GetInt32(),Tag=item.GetProperty("tag").GetInt32()};
                        foreach(var function in item.GetProperty("functions").EnumerateObject()) {
                            var code=new List<ScriptInstruction>();
                            foreach(var x in function.Value.EnumerateArray()) {
                                var args=x.GetProperty("args");var exprs=x.GetProperty("exprs");int n=args.GetArrayLength();
                                var item2=new ScriptInstruction(x.GetProperty("offset").GetInt32(),x.GetProperty("next").GetInt32(),x.GetProperty("opcode").GetInt32()){Arguments=new int?[n],ArgumentExpressions=new byte[n][]};
                                for(int i=0;i<n;i++) {
                                    if(args[i].ValueKind==JsonValueKind.Number)item2.Arguments[i]=args[i].GetInt32();
                                    if(exprs[i].ValueKind==JsonValueKind.String)item2.ArgumentExpressions[i]=Convert.FromHexString(exprs[i].GetString());
                                }
                                item2.Argument=item2.Constant(0);item2.Expression=item2.ArgumentExpression(0);code.Add(item2);
                            }
                            c.Functions[function.Name]=code;
                        }
                        _cases[c.Field]=c;
                    }
                }
            }
            return _cases[field];
        }
        private static FieldExitPathEvidence Read(int field,Func<int,int,int,bool?> requestable=null,byte[] global=null,
            NavigationPoint? position=null,NavigationPoint? projected=null,int? facing=null)
        {
            var c=Load(field);var start=c.Functions[c.Sid+":"+c.Tag];var point=position??new NavigationPoint();
            Func<ScriptStoryFunction> root=()=>new ScriptStoryFunction{Identity=c.Sid*256+c.Tag,ScriptId=c.Sid,Tag=c.Tag,Code=start,Snapshot=new ScriptSnapshot(global??new byte[2048],new byte[1024],new byte[256]){UserControl=true}};
            Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> request=(uid,tag,caller,snapshot)=> {
                int sid=uid==255?caller/256:uid;List<ScriptInstruction> code;
                if(!c.Functions.TryGetValue(sid+":"+tag,out code))return null;
                int entry=uid==250?c.Player:sid;
                return new ScriptStoryFunction{Identity=(uid==250?250:sid)*256+tag,ScriptId=entry,Tag=tag,Code=code,Snapshot=snapshot.WithInstance(new byte[256])};
            };
            var dialogue=new[]{_opcodes["MES"],_opcodes["MESN"],_opcodes["MESA"],_opcodes["MESAN"],_opcodes["MESB"]};
            return FieldExitPaths.Read(root,request,_opcodes["MAPJUMP"],_opcodes["WMAPJUMP"],new[]{_opcodes["REQ"],_opcodes["REQSW"],_opcodes["REQEW"]},
                (uid,property)=>FieldExitDestinations.PlayerProperty(250,point,uid,property),character=>false,_opcodes["UCOFF"],_opcodes["UCON"],new[]{_opcodes["MOVE"],_opcodes["MOVQ"]},
                new FieldExitPathOptions{DialogueOpcodes=dialogue,AsyncRequest=_opcodes["REQ"],PlayerUid=250,Requestable=requestable??((uid,level,caller)=>true),
                    CalculateExitPosition=_opcodes["MJPOS"],ExitPosition=projected,EntryFacing=facing,Angle2=(x,z)=>49});
        }
        private static void Check(bool value,string detail){if(!value)throw new Exception(detail);}
        private static string Describe(FieldExitPathEvidence e){return e.Kind+" ["+String.Join(",",new List<int>(e.Destinations).ConvertAll(x=>x.ToString()).ToArray())+"] choice="+e.UsesChoice+" dialogue="+e.Dialogue+" counter="+e.StoryCounter+" buttons="+e.Buttons.ToString("x")+" stop="+e.Stop;}
        public static void TheAirCabMenuOffersOnlyItsOtherStationsAndIsAService()
        {
            var cab=Read(551);
            Check(cab.Kind==FieldExitPathKind.ChoiceMenu,Describe(cab));Check(cab.UsesChoice,Describe(cab));
            Check(!(cab.Destinations.Contains(551)),Describe(cab));Check(cab.Destinations.Count>=2,Describe(cab));
            foreach(int station in cab.Destinations)Check(Array.IndexOf(new[]{563,572,610,1310,1360,2110,2160},station)>=0,Describe(cab));
            TestAssert.Equal(FieldExitAdmission.Service,FieldExitAdmissions.Admit("Air cab",cab));
            // The same region with the cab entry busy (it cannot take the request now) is not admitted.
            var busy=Read(551,(uid,level,caller)=>uid!=12);
            TestAssert.Equal(FieldExitAdmission.None,FieldExitAdmissions.Admit("Air cab",busy));
        }
        public static void TheCastleLiftIsAServiceMenu()
        {
            var lift=Read(601);
            Check(lift.UsesChoice,Describe(lift));Check(lift.Destinations.Contains(603),Describe(lift));
            TestAssert.Equal(FieldExitAdmission.Service,FieldExitAdmissions.Admit("Castle lift",lift));
        }
        public static void TheBurmeciaLedgeIsAConfirmGatedWalkExit()
        {
            var ledge=Read(756);
            // Confirm at the ledge, then the game asks before the jump.
            Check(ledge.Kind==FieldExitPathKind.ChoiceSingle && ledge.UsesChoice && ledge.CanDecline,Describe(ledge));
            Check(ledge.Destinations.Contains(752),Describe(ledge));TestAssert.Equal("Confirm",FieldExitNames.Button(ledge.Buttons));
            TestAssert.Equal(FieldExitAdmission.Walk,FieldExitAdmissions.Admit(null,ledge));
        }
        public static void TheTowerRequiresTheIncomingHeadingOnBothStoryBranches()
        {
            var point=new NavigationPoint(-21980,5030,-9295);
            var projected=FieldExitDestinations.JumpPoint(point,-21410,-9417,-21598,-9690);
            foreach(int counter in new[]{4450,4470}) {
                var global=new byte[2048];global[0]=(byte)counter;global[1]=(byte)(counter>>8);
                var incoming=Read(905,global:global,position:point,projected:projected,facing:49);
                Check(incoming.Kind==FieldExitPathKind.Determined && incoming.Destinations.SetEquals(new[]{913}),Describe(incoming));
                var away=Read(905,global:global,position:point,projected:projected,facing:177);
                Check(away.Kind==FieldExitPathKind.NoJump,Describe(away));
            }
        }
        public static void TheInvincibleDepartureChoicesCanDeclineWithoutJumping()
        {
            foreach(int field in new[]{2750,2751}) {
                var global=new byte[2048];const int counter=11100;global[0]=counter&255;global[1]=counter>>8;
                var departure=Read(field,global:global);
                Check(departure.UsesChoice && departure.CanDecline && departure.Destinations.Count==1,Describe(departure));
                foreach(int map in departure.Destinations)TestAssert.Equal("World Map",FieldExitNames.Destination(map,null));
            }
        }
        public static void EveryNativeCaseIsReported()
        {
            Load(0==1?0:551);
            string[] functions={"551:Air cab","563:Air cab","572:Air cab","610:Air cab","1301:Air cab","1310:Air cab","1360:Air cab","2101:Air cab","2110:Air cab","2160:Air cab","601:Castle lift"};
            foreach(int field in new List<int>(_cases.Keys)) {
                string function=null;foreach(var f in functions)if(f.StartsWith(field+":"))function=f.Substring(f.IndexOf(':')+1);
                var evidence=Read(field);Console.WriteLine("native exit case "+field+": "+Describe(evidence)+" admission="+FieldExitAdmissions.Admit(function,evidence));
            }
        }
        public static void TheEvilForestWalkInToThePrimaVistaSceneIsNotAnExit()
        {
            var scene=Read(251);
            TestAssert.Equal(FieldExitAdmission.None,FieldExitAdmissions.Admit(null,scene));
        }
    }
}
