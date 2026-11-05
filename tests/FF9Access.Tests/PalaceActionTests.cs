using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    // Desert Palace SC 9825 -> 9830 (docs/accessibility/story-palace-actions.json). The fixture is the walk a
    // player makes by following only the selected rows through the native state graph (installed walkmeshes,
    // native init/loop/Talk scripts), from the Lobby to the boss trigger. Each step's phase is selected by the
    // real StoryPhasePlan with guards evaluated by the native expression reader on global memory alone.
    internal static class PalaceActionTests
    {
        private static string Find(params string[] parts)
        {
            string dir=AppContext.BaseDirectory;
            while(dir!=null){string candidate=Path.Combine(dir,Path.Combine(parts));if(File.Exists(candidate))return candidate;dir=Path.GetDirectoryName(dir);}
            throw new Exception(String.Join("/",parts)+" not found");
        }
        private static byte[] Hex(string text){var bytes=new byte[text.Length/2];for(int i=0;i<bytes.Length;i++)bytes[i]=Convert.ToByte(text.Substring(2*i,2),16);return bytes;}
        private sealed class Row{public string Id;public int Field;public int[] Uids;public StoryPhase Phase;public List<byte[]> Guards=new List<byte[]>();}
        private static List<Row> _rows;
        private static List<Row> Rows()
        {
            if(_rows!=null)return _rows;
            _rows=new List<Row>();
            using(var doc=JsonDocument.Parse(File.ReadAllText(Find("docs","accessibility","story-palace-actions.json")))) {
                TestAssert.Equal(1,doc.RootElement.GetProperty("schema").GetInt32());
                foreach(var p in doc.RootElement.GetProperty("phases").EnumerateArray()) {
                    var uids=new List<int>();foreach(var u in p.GetProperty("uids").EnumerateArray())uids.Add(u.GetInt32());
                    var directors=new Dictionary<int,string>();foreach(var r in p.GetProperty("rooms").EnumerateArray())directors[r.GetProperty("field").GetInt32()]=r.GetProperty("directorHash").GetString();
                    var row=new Row{Id=p.GetProperty("id").GetString(),Field=p.GetProperty("field").GetInt32(),Uids=uids.ToArray()};
                    foreach(var g in p.GetProperty("guards").EnumerateArray())row.Guards.Add(Hex(g.GetString()));
                    var scope=new List<int>();foreach(var f in p.GetProperty("scope").EnumerateArray())scope.Add(f.GetInt32());
                    row.Phase=new StoryPhase{Floor=p.GetProperty("floor").GetInt32(),Ceiling=p.GetProperty("ceiling").GetInt32(),Directors=directors,ScopeFields=scope.ToArray(),Guards=row.Guards.ToArray(),
                        Stage=new StoryStage{Milestone=p.GetProperty("milestone").GetInt32(),ApproachFields=new[]{row.Field},ActionUids=row.Uids,Instruction=p.GetProperty("action").GetString()}};
                    _rows.Add(row);
                }
            }
            return _rows;
        }
        // Global memory: Gu16[0] scenario counter, Gi16[2] arrival door, then event bits (index>>3, bit index&7).
        private static byte[] Globals(int arrival,IEnumerable<int> bits)
        {
            var g=new byte[2048];g[0]=9825&255;g[1]=9825>>8;g[2]=(byte)(arrival&255);g[3]=(byte)((arrival>>8)&255);
            foreach(int b in bits)g[b>>3]|=(byte)(1<<(b&7));
            return g;
        }
        private static StoryStage Select(StoryPhasePlan plan,Dictionary<int,string> directors,int field,int arrival,IEnumerable<int> bits)
        {
            var memory=Globals(arrival,bits);
            return plan.Select(9825,field,sid=>sid==0?directors[field]:null,guard=>{int? value;return ExitScriptAnalyzer.TryExpression(guard,new ScriptSnapshot(memory,null,null),out value)&&value.HasValue?(bool?)(value.Value!=0):null;});
        }
        private static Dictionary<int,string> Directors()
        {
            var result=new Dictionary<int,string>();
            foreach(var row in Rows())foreach(var d in row.Phase.Directors){string existing;if(result.TryGetValue(d.Key,out existing))TestAssert.Equal(existing,d.Value);result[d.Key]=d.Value;}
            return result;
        }
        public static void TheProvenWalkFromTheLobbyIsGuidedStepByStepToTheBossTrigger()
        {
            var phases=new List<StoryPhase>();foreach(var row in Rows())phases.Add(row.Phase);
            var plan=new StoryPhasePlan(phases);var directors=Directors();int steps=0;
            using(var doc=JsonDocument.Parse(File.ReadAllText(Find("tests","fixtures","palace-chain.json")))) {
                foreach(var step in doc.RootElement.GetProperty("walk").EnumerateArray()) {
                    int field=step.GetProperty("field").GetInt32(),arrival=step.GetProperty("arrival").GetInt32();
                    var bits=new List<int>();foreach(var b in step.GetProperty("bits").EnumerateArray())bits.Add(b.GetInt32());
                    var stage=Select(plan,directors,field,arrival,bits);
                    if(stage==null || stage.InformationOnly)throw new Exception("No phase for "+field+" arrival "+arrival+" bits "+String.Join(",",bits));
                    TestAssert.Equal(step.GetProperty("expectField").GetInt32(),stage.ApproachFields[0]);
                    var uids=new List<int>();foreach(var u in step.GetProperty("expectUids").EnumerateArray())uids.Add(u.GetInt32());
                    TestAssert.Equal(uids.Count,stage.ActionUids.Length);for(int i=0;i<uids.Count;i++)TestAssert.Equal(uids[i],stage.ActionUids[i]);
                    steps++;
                }
                var goal=doc.RootElement.GetProperty("goal");var goalBits=new List<int>();foreach(var b in goal.GetProperty("bits").EnumerateArray())goalBits.Add(b.GetInt32());
                var boss=Select(plan,directors,goal.GetProperty("field").GetInt32(),goal.GetProperty("arrival").GetInt32(),goalBits);
                TestAssert.Equal(2222,boss.ApproachFields[0]);TestAssert.Equal(1,boss.ActionUids.Length);TestAssert.Equal(5,boss.ActionUids[0]);
                // The boss trigger's native Talk guard holds in the goal state: Gb3567 && Gb3565 && Gb3560.
                TestAssert.True(goalBits.Contains(3567) && goalBits.Contains(3565) && goalBits.Contains(3560));
            }
            TestAssert.True(steps>=20);
        }
        public static void GatesThatAreNotOpenNeverSelectTheirLaterStep()
        {
            var phases=new List<StoryPhase>();foreach(var row in Rows())phases.Add(row.Phase);
            var plan=new StoryPhasePlan(phases);var directors=Directors();
            // Fire Chamber from the Light Chamber door, nothing lit: a candle reachable on this side, never the
            // 2216 exit (floor 6 opens only after all eight candles and the centre).
            var fire=Select(plan,directors,2215,71,new int[0]);
            TestAssert.Equal(2215,fire.ApproachFields[0]);TestAssert.Equal(1,fire.ActionUids.Length);
            TestAssert.True(Array.IndexOf(new[]{1,2,3},fire.ActionUids[0])>=0);
            // Top level far side with the final candle and landing candle 11 lit but not landing candle 12:
            // the boss guard is false, so the route goes back down to the Shadow Chamber, never the boss trigger.
            var top=Select(plan,directors,2222,91,new[]{3560,3562,3564,3567});
            TestAssert.Equal(2221,top.ApproachFields[0]);TestAssert.Equal(0,top.ActionUids.Length);
            // Outside the chapter's counter band the plan selects nothing.
            TestAssert.Equal(null,plan.Select(9830,2215,sid=>directors[2215],guard=>true));
            // A different script in the room (changed director hash) is never guided as this puzzle.
            var other=plan.Select(9825,2215,sid=>"other",guard=>true);TestAssert.True(other==null || other.InformationOnly);
        }
        public static void GuardsReadOnlyGlobalMemoryAndRowsStayInThePalaceChapter()
        {
            var palace=new HashSet<int>{2213,2214,2215,2216,2217,2220,2221,2222};
            foreach(var row in Rows()) {
                TestAssert.Equal(9825,row.Phase.Floor);TestAssert.Equal(9830,row.Phase.Ceiling);TestAssert.Equal(9830,row.Phase.Stage.Milestone);
                TestAssert.True(palace.Contains(row.Field));TestAssert.Equal(1,row.Phase.ScopeFields.Length);
                TestAssert.True(row.Phase.Directors.ContainsKey(row.Phase.ScopeFields[0]) && row.Phase.Directors.ContainsKey(row.Field));
                foreach(int room in row.Phase.Directors.Keys)TestAssert.True(palace.Contains(room));
                foreach(var guard in row.Guards) {
                    // Variable operations encode their source in the low two bits: 0 = global memory.
                    for(int p=0;p<guard.Length;) {
                        int op=guard[p++];
                        if(op==0x7f)break;
                        if(op>=0xc0){TestAssert.Equal(0,op&3);p+=op>=0xe0?2:1;}
                        else if(op==0x7d)p+=2;else if(op==0x7e)p+=4;
                        else if(op==0x78 || op==0x79 || op==0x7a)throw new Exception(row.Id+" reads non-global state");
                    }
                }
            }
        }
        public static void RequiredCandlesAreTargetsAndOptionalBloodstonesNeverAre()
        {
            var targets=new HashSet<string>();foreach(var row in Rows())foreach(int uid in row.Uids)targets.Add(row.Field+":"+uid);
            // Fire Chamber candles 1-8 and its centre (region 13), stairwell landing candles 11/12,
            // Shadow Chamber lamps on both sides (2, 4), the final candle and the boss trigger.
            foreach(var required in new[]{"2215:1","2215:2","2215:3","2215:4","2215:5","2215:6","2215:7","2215:8","2215:13","2217:11","2217:12","2221:2","2221:4","2222:6","2222:5"})
                if(!targets.Contains(required))throw new Exception("missing required target "+required);
            foreach(var optional in new[]{"2213:5","2216:4","2217:14","2221:9","2221:10","2215:14"})
                if(targets.Contains(optional))throw new Exception("optional bloodstone/reward offered as a story target "+optional);
        }
    }
}
