using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SimpleJSON;
using Memoria.Prime;
namespace Memoria.Accessibility.Navigation
{
    // Loads the generated native Story steps and typed transit (docs/accessibility/story-steps.json)
    // and evaluates their guards on the live field state. Reads only: guard expressions run on
    // detached copies (ScriptSnapshot), never through EBin, and nothing requests an event.
    public static class NativeStorySteps
    {
        private static bool _loaded;
        private static StoryStepCatalog _steps;
        private static StoryPhasePlan _phases;
        private static StoryTransit[] _transit;
        private static string _inventory;
        private sealed class LoopAction {public int Field,Sid,Uid,Entry,Prompt,Resume,Floor,Ceiling,Milestone;public string Hash,Action;}
        private static readonly Dictionary<int,List<LoopAction>> _loopActions=new Dictionary<int,List<LoopAction>>();
        public static void Initialize()
        {
            if(_loaded)return;_loaded=true;
            try {
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Memoria.Accessibility.StorySteps.json")) {
                    if(stream==null)throw new InvalidOperationException("Missing native Story step catalog");
                    using(var reader=new StreamReader(stream)) {
                        var root=JSON.Parse(reader.ReadToEnd());if(root["schema"].AsInt!=2)throw new InvalidOperationException("Unsupported native Story step catalog");
                        var steps=new List<StoryStep>();LoadSteps(root["steps"],steps);
                        var phases=new List<StoryPhase>();
                        LoadReview("Memoria.Accessibility.StoryActionChains.json",root["inventorySHA256"],steps,phases);
                        LoadReview("Memoria.Accessibility.StoryPalaceActions.json",root["inventorySHA256"],steps,phases);
                        _phases=new StoryPhasePlan(phases);
                        var rows=root["transit"];
                        var transit=new List<StoryTransit>();
                        for(int i=0;i<rows.Count;i++)transit.Add(new StoryTransit{From=rows[i]["source"].AsInt,To=rows[i]["destination"].AsInt,Kind=StoryStepCatalog.ParseTransit(rows[i]["kind"]),Floor=Optional(rows[i],"floor"),Ceiling=Optional(rows[i],"ceiling"),Scope=rows[i]["scope"],Guards=Expressions(rows[i]["guards"])});
                        if(steps.Count==0 || transit.Count==0)throw new InvalidOperationException("Empty native Story step or transit catalog");
                        _steps=new StoryStepCatalog(steps);_transit=transit.ToArray();_inventory=root["inventorySHA256"];
                    }
                }
            }catch(Exception exception){_steps=null;_transit=null;_inventory=null;_phases=null;_loopActions.Clear();Log.Message("[FF9Access] Native Story steps unavailable: {0}",exception.Message);}
        }
        private static void LoadReview(string name,string inventory,List<StoryStep> steps,List<StoryPhase> phases)
        {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) {
                if(stream==null)throw new InvalidOperationException("Missing reviewed Story action chains: "+name);
                using(var reader=new StreamReader(stream)) {
                    var actions=JSON.Parse(reader.ReadToEnd());
                    if(actions["schema"].AsInt!=1 || actions["inventorySHA256"].Value!=inventory)throw new InvalidOperationException("Reviewed Story action chain identity mismatch: "+name);
                    LoadSteps(actions["steps"],steps);LoadPhases(actions["phases"],phases);
                    var loops=actions["loopActions"];
                    for(int i=0;i<loops.Count;i++) {
                        var r=loops[i];var row=new LoopAction{Field=r["field"].AsInt,Sid=r["sid"].AsInt,Uid=r["uid"].AsInt,Hash=r["hash"],
                            Entry=r["entry"].AsInt,Prompt=r["prompt"].AsInt,Resume=r["resume"].AsInt,Floor=r["floor"].AsInt,Ceiling=r["ceiling"].AsInt,
                            Milestone=r["milestone"].AsInt,Action=r["action"]};
                        if(row.Floor>=row.Ceiling || String.IsNullOrEmpty(row.Action) || String.IsNullOrEmpty(row.Hash))throw new InvalidOperationException("Invalid reviewed Story loop action");
                        List<LoopAction> list;if(!_loopActions.TryGetValue(row.Field,out list)){list=new List<LoopAction>();_loopActions.Add(row.Field,list);}list.Add(row);
                    }
                }
            }
        }
        // Called only after a live owner and its native loop prompt have been
        // verified at an approved approach. The Story stage still selects its UID.
        internal static void AnnotateLoop(int field,int progress,int sid,int uid,string hash,int entry,int prompt,int resume,FieldTargetObservation observation)
        {
            Initialize();List<LoopAction> rows;if(!_loopActions.TryGetValue(field,out rows))return;
            foreach(var row in rows)if(row.Sid==sid && row.Uid==uid && row.Hash==hash && row.Entry==entry && row.Prompt==prompt && row.Resume==resume && progress>=row.Floor && progress<row.Ceiling) {
                observation.StoryStepMilestone=row.Milestone;observation.StoryAction=row.Action;return;
            }
        }
        private static void LoadSteps(JSONNode rows,List<StoryStep> steps)
        {
            for(int i=0;i<rows.Count;i++) {
                var row=rows[i];
                string label=row["label"];
                steps.Add(new StoryStep{Field=row["field"].AsInt,Sid=row["sid"].AsInt,Uid=Optional(row,"uid")??-1,Hash=row["hash"],Kind=StoryStepCatalog.ParseKind(row["kind"]),Source=row["source"],Action=row["action"],TargetLabel=label=="null"?null:label,Priority=Optional(row,"priority")??100,
                    Milestone=row["milestone"].AsInt,Floor=Optional(row,"floor"),Ceiling=Optional(row,"ceiling"),Minimum=Optional(row,"minimum"),Maximum=Optional(row,"maximum"),SupportFloor=Optional(row,"supportFloor"),ApproachFacing=Optional(row,"approachFacing"),MinimumHeight=Optional(row,"minimumHeight"),
                    Axis=row["axis"].Value=="Y"?OpeningGoalAxis.Y:row["axis"].Value=="Z"?OpeningGoalAxis.Z:OpeningGoalAxis.X,PlayerSid=Optional(row,"playerSid")??-1,PlayerUid=Optional(row,"playerUid")??-1,
                    WaitSid=Optional(row["waiting"],"sid")??-1,WaitUid=Optional(row["waiting"],"uid")??-1,WaitHash=row["waiting"]["hash"],WaitIps=Integers(row["waiting"]["ips"]),WaitGuards=Guards(row["waiting"]["guards"]),Effects=Effects(row["effects"]),
                    Guards=Guards(row["guards"]),Completion=Guards(row["completion"])});
            }
        }
        private static void LoadPhases(JSONNode rows,List<StoryPhase> phases)
        {
            for(int i=0;i<rows.Count;i++) {
                var row=rows[i];var directors=new Dictionary<int,string>();var rooms=row["rooms"];
                for(int j=0;j<rooms.Count;j++)directors.Add(rooms[j]["field"].AsInt,rooms[j]["directorHash"]);
                var links=row["links"];var reviewed=new StoryTransit[links.Count];
                for(int j=0;j<reviewed.Length;j++)reviewed[j]=new StoryTransit{From=links[j]["from"].AsInt,To=links[j]["to"].AsInt,ActionUid=links[j]["uid"].AsInt,
                    Kind=StoryTransitKind.Player,Scope="reviewed",Guards=Expressions(links[j]["guards"])};
                int floor=row["floor"].AsInt,ceiling=row["ceiling"].AsInt,field=row["field"].AsInt;
                if(floor>=ceiling || !directors.ContainsKey(field))throw new InvalidOperationException("Invalid reviewed Story phase");
                var local=row["local"];var scope=Integers(row["scope"]);
                var items=row["items"];var requiredItems=new StoryPhaseItem[items.Count];
                for(int j=0;j<items.Count;j++)requiredItems[j]=new StoryPhaseItem{NativeId=items[j]["id"].AsInt,Owned=items[j]["owned"].AsBool};
                string information=row["information"];
                phases.Add(new StoryPhase{Floor=floor,Ceiling=ceiling,Directors=directors,ScopeFields=scope.Length==0?null:scope,Guards=Expressions(row["guards"]),
                    Items=requiredItems.Length==0?null:requiredItems,Local=local.Count==0?null:new StoryPhaseLocal{Field=local["field"].AsInt,Walkmesh=local["walkmesh"],ActiveTriangles=Integers(local["triangles"]),MapGuards=Expressions(local["guards"])},Stage=new StoryStage{
                    Milestone=row["milestone"].AsInt,GoalFields=new[]{field},ActionUids=Integers(row["uids"]),Instruction=row["action"],Goal=row["goal"],
                    InformationOnly=!String.IsNullOrEmpty(information),InformationTitle=row["action"],InformationReason=information,ReviewedLinks=row["nativeTransit"].AsBool?null:reviewed}});
            }
        }
        public static StoryStage SelectPhase(int progress,int field,Func<int,string> scripts,Func<StoryPhaseLocal,bool?> readLocal=null,Func<int,int?> readItem=null)
        {Initialize();return _phases==null?null:_phases.Select(progress,field,scripts,EvaluateGlobalGuard,readLocal,readItem);}
        // A failed load leaves Story transit unavailable.
        public static StoryTransit[] Transit{get{Initialize();return _transit;}}
        internal static string Inventory{get{Initialize();return _inventory;}}
        private static int? Optional(JSONNode row,string key)
        {
            var node=row[key];if(node==null || String.IsNullOrEmpty(node.Value))return null;
            int value;return Int32.TryParse(node.Value,out value)?(int?)value:null;
        }
        private static StoryStepGuard[] Guards(JSONNode rows)
        {
            if(rows==null)return new StoryStepGuard[0];
            var result=new StoryStepGuard[rows.Count];
            for(int i=0;i<rows.Count;i++)result[i]=new StoryStepGuard{Sid=rows[i]["sid"].AsInt,Uid=Optional(rows[i],"uid")??-1,Hash=rows[i]["hash"],Expression=Hex(rows[i]["expr"])};
            return result;
        }
        private static int[] Integers(JSONNode rows){var result=new int[rows.Count];for(int i=0;i<result.Length;i++)result[i]=rows[i].AsInt;return result;}
        private static byte[][] Expressions(JSONNode rows){var result=new byte[rows.Count][];for(int i=0;i<result.Length;i++)result[i]=Hex(rows[i]);return result;}
        private static byte[] _transitGlobals;
        private static readonly Dictionary<byte[],bool?> _transitResults=new Dictionary<byte[],bool?>();
        public static bool RefreshTransitState()
        {
            var current=FF9StateSystem.EventState.gEventGlobal;if(current==null)return false;
            bool same=_transitGlobals!=null && current.Length==_transitGlobals.Length;
            if(same)for(int i=0;i<current.Length;i++)if(current[i]!=_transitGlobals[i]){same=false;break;}
            if(same)return false;_transitGlobals=(byte[])current.Clone();_transitResults.Clear();return true;
        }
        public static bool? EvaluateTransit(StoryTransit edge)
        {
            if(edge==null || edge.Scope=="room" || _transitGlobals==null)return null;
            foreach(var raw in edge.Guards) {
                bool? accepted=EvaluateGlobalGuard(raw);
                if(accepted!=true)return accepted;
            }
            return true;
        }
        private static bool? EvaluateGlobalGuard(byte[] raw)
        {
            if(raw==null || _transitGlobals==null)return null;
            bool? accepted;
            if(!_transitResults.TryGetValue(raw,out accepted)) {
                int? value;var snapshot=new ScriptSnapshot(_transitGlobals,null,null){UserControl=true};
                accepted=ExitScriptAnalyzer.TryExpression(raw,snapshot,out value) && value.HasValue?(bool?)(value.Value!=0):null;
                _transitResults[raw]=accepted;
            }
            return accepted;
        }
        private static StoryStepEffect[] Effects(JSONNode rows)
        {
            var result=new StoryStepEffect[rows.Count];for(int i=0;i<result.Length;i++)result[i]=new StoryStepEffect{Sid=rows[i]["sid"].AsInt,Uid=Optional(rows[i],"uid")??-1,Tag=rows[i]["tag"].AsInt,Offset=rows[i]["offset"].AsInt,Hash=rows[i]["hash"],Proof=String.IsNullOrEmpty(rows[i]["proof"].Value)?"assigned":rows[i]["proof"].Value};return result;
        }
        internal static Obj FindUniqueOwner(EventEngine engine,int sid)
        {
            Obj found=null;
            for(var list=engine.GetActiveObjList();list!=null;list=list.next)if(list.obj!=null && list.obj.sid==sid){if(found!=null)return null;found=list.obj;}
            return found;
        }
        internal static Obj FindOwner(EventEngine engine,int sid,int uid)
        {
            if(engine==null)return null;
            if(uid<0)return FindUniqueOwner(engine,sid);
            for(var list=engine.GetActiveObjList();list!=null;list=list.next)if(list.obj!=null && list.obj.uid==uid)return list.obj.sid==sid?list.obj:null;
            return null;
        }
        private static bool? AtWait(EventEngine engine,StoryStep step,Func<int,string> entryHash,int player=-1,NavigationPoint? position=null)
        {
            if(step.WaitSid<0 || step.WaitIps==null || step.WaitIps.Length==0 || entryHash(step.WaitSid)!=step.WaitHash)return null;
            var owner=FindOwner(engine,step.WaitSid,step.WaitUid);if(owner==null)return null;
            bool at=false;foreach(int ip in step.WaitIps)if(owner.ip==ip)at=true;if(!at)return false;
            foreach(var guard in step.WaitGuards){if(entryHash(guard.Sid)!=guard.Hash)return null;int? value=Evaluate(engine,guard,player,position);if(!value.HasValue)return null;if(value.Value==0)return false;}
            return true;
        }
        private static byte[] Hex(string text)
        {
            if(String.IsNullOrEmpty(text) || text.Length%2!=0)return null;
            var bytes=new byte[text.Length/2];
            for(int i=0;i<bytes.Length;i++)bytes[i]=Convert.ToByte(text.Substring(2*i,2),16);
            return bytes;
        }
        // Live guard evaluation against detached copies of the global, map and owner's instance memory.
        internal static int? Evaluate(EventEngine engine,StoryStepGuard guard,int player=-1,NavigationPoint? position=null)
        {
            try {
                if(engine==null || guard==null || guard.Expression==null)return null;
                var owner=FindOwner(engine,guard.Sid,guard.Uid);if(owner==null || owner.ebData==null)return null;
                var snapshot=new ScriptSnapshot(FF9StateSystem.EventState.gEventGlobal,engine.GetMapVar(),ScriptSnapshot.InstanceBytes(owner.buffer,owner.vofs)){UserControl=true};
                Func<int,int,int?> property=position.HasValue?(Func<int,int,int?>)((uid,index)=>FieldExitDestinations.PlayerProperty(player,position.Value,uid==255 && owner.uid==player?250:uid,index)):null;
                int? value;return ExitScriptAnalyzer.TryExpression(guard.Expression,snapshot,out value,property)?value:null;
            }catch(Exception){return null;}
        }
        public static List<StoryStep> Current(EventEngine engine,int field,int progress,int sid,string hash,Func<int,string> entryHash)
        {
            Initialize();if(_steps==null || engine==null)return new List<StoryStep>();
            return _steps.Current(field,progress,sid,hash,entryHash,guard=>Evaluate(engine,guard),step=>AtWait(engine,step,entryHash));
        }
        public static List<StoryStep> Candidates(EventEngine engine,int field,int progress,int sid,string hash,Func<int,string> entryHash,int player=-1,NavigationPoint? position=null,int uid=-1)
        {
            Initialize();if(_steps==null || engine==null)return new List<StoryStep>();
            var result=_steps.Candidates(field,progress,sid,hash,entryHash,guard=>Evaluate(engine,guard,player,position),step=>AtWait(engine,step,entryHash,player,position),uid);
            var controlled=player<0?null:engine.GetObjUID(player);
            result.RemoveAll(step=>step.PlayerUid>=0 && step.PlayerUid!=player || step.PlayerSid>=0 && (controlled==null || controlled.sid!=step.PlayerSid));
            return result;
        }
        public static List<StoryStep> CurrentPositions(EventEngine engine,int field,int progress,int player,Func<int,string> entryHash,NavigationPoint? position=null)
        {
            Initialize();if(_steps==null || engine==null)return new List<StoryStep>();
            var controlled=engine.GetObjUID(player);if(controlled==null)return new List<StoryStep>();
            return _steps.CurrentPositions(field,progress,controlled.sid,entryHash,guard=>Evaluate(engine,guard,player,position),step=>AtWait(engine,step,entryHash,player,position),controlled.uid);
        }
    }
}
