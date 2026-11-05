using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using SimpleJSON;
using Memoria.Prime;
using Assets.Sources.Scripts.UI.Common;
namespace Memoria.Accessibility.Navigation
{
    public static class NativeStoryTargets
    {
        private sealed class Binding {internal string Hash;internal int[] Milestones,Destinations;internal bool Pickup;}
        private sealed class Listener {internal int PlayerSid,Minimum,Maximum;internal int[] Milestones;internal string Hash,PlayerHash,Action;}
        private static readonly Dictionary<int,Dictionary<int,Binding>> _fields=new Dictionary<int,Dictionary<int,Binding>>();
        private static readonly Dictionary<int,Dictionary<int,Listener>> _listeners=new Dictionary<int,Dictionary<int,Listener>>();
        private static StoryTargetCatalog _catalog;private static bool _loaded;
        private static long _generation;private static string _save;private static int _player=-1;
        private static readonly Dictionary<TargetIdentity,int> _observedItems=new Dictionary<TargetIdentity,int>();
        private static EventEngine _engine;private static int _engineFrame=-1;
        internal static void PrepareEngine(EventEngine engine){_engine=engine;_engineFrame=UnityEngine.Time.frameCount;}
        private static EventEngine ReadEngine()
        {
            if(_engineFrame!=UnityEngine.Time.frameCount || _engine==null)PrepareEngine(UnityEngine.Object.FindObjectOfType<EventEngine>());
            return _engine;
        }
        public static void Initialize()
        {
            if(_loaded)return;_loaded=true;
            NativeStoryPrerequisites.Initialize();
            try {
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Memoria.Accessibility.StoryNavigation.json")) {
                    if(stream==null)throw new InvalidOperationException("Missing Story navigation catalog");
                    using(var reader=new StreamReader(stream)) {
                        var root=JSON.Parse(reader.ReadToEnd());if(root["schema"].AsInt!=1)throw new InvalidOperationException("Unsupported Story navigation catalog");
                        var stages=new List<StoryStage>();var rows=root["stages"];
                        for(int i=0;i<rows.Count;i++)stages.Add(new StoryStage{Milestone=rows[i]["milestone"].AsInt,GoalFields=Integers(rows[i]["fields"]),ApproachFields=Integers(rows[i]["approachFields"]),DepartureFields=Integers(rows[i]["departureFields"]),WorldDepartureFields=rows[i]["worldDepartureFields"] is JSONArray?Integers(rows[i]["worldDepartureFields"]):null,WorldLocations=Integers(rows[i]["worldLocations"]),Goal=rows[i]["goal"],Instruction=rows[i]["instruction"],WorldPlaces=Strings(rows[i]["worldPlaces"]),RequiredVehicle=rows[i]["vehicle"]});
                        rows=root["fields"];for(int i=0;i<rows.Count;i++) {
                            var entries=new Dictionary<int,Binding>();var data=rows[i]["entries"];
                            for(int j=0;j<data.Count;j++)entries[data[j]["sid"].AsInt]=new Binding{Hash=data[j]["hash"],Milestones=Integers(data[j]["milestones"]),Destinations=Integers(data[j]["destinations"]),Pickup=data[j]["pickup"].AsBool};
                            _fields[rows[i]["field"].AsInt]=entries;
                        }
                        rows=root["listeners"];for(int i=0;i<rows.Count;i++) {
                            int field=rows[i]["field"].AsInt;Dictionary<int,Listener> listeners;
                            if(!_listeners.TryGetValue(field,out listeners)){listeners=new Dictionary<int,Listener>();_listeners[field]=listeners;}
                            listeners[rows[i]["sid"].AsInt]=new Listener{PlayerSid=rows[i]["playerSid"].AsInt,Minimum=rows[i]["minimum"].AsInt,Maximum=rows[i]["maximum"].AsInt,Milestones=Integers(rows[i]["milestones"]),Hash=rows[i]["hash"],PlayerHash=rows[i]["playerHash"],Action=rows[i]["action"]};
                        }
                        // The untyped inventory also contains automatic scene jumps. A bad
                        // Story resource must never turn those into ordinary walking routes.
                        var typed=NativeStorySteps.Transit;
                        if(typed==null || typed.Length==0 || String.IsNullOrEmpty(NativeStorySteps.Inventory) || NativeStorySteps.Inventory!=root["inventorySHA256"].Value) {
                            // Keep script identities for ordinary entities and exit names.
                            Log.Message("[FF9Access] Story guidance unavailable: typed Story resources do not match the field inventory.");
                            return;
                        }
                        _catalog=new StoryTargetCatalog(stages.ToArray(),typed,OpeningStoryCheckpoints.All);
                        _catalog.SetTransitEvaluator(NativeStorySteps.EvaluateTransit);
                        _catalog.SetPhaseSelector((progress,field,scripts)=>NativeStorySteps.SelectPhase(progress,field,scripts,ReadPhaseLocal,ReadPhaseItem));
                    }
                }
            }catch(Exception exception){_fields.Clear();_listeners.Clear();Log.Message("[FF9Access] Story catalog unavailable: {0}",exception.Message);}
        }
        private static int[] Integers(JSONNode node){var result=new int[node.Count];for(int i=0;i<result.Length;i++)result[i]=node[i].AsInt;return result;}
        private static string[] Strings(JSONNode node){var result=new string[node.Count];for(int i=0;i<result.Length;i++)result[i]=node[i];return result;}
        public static bool Matches(NavigationContext context,int scriptId,string scriptHash)
        {
            Initialize();Dictionary<int,Binding> entries;Binding binding;
            return _fields.TryGetValue(FieldNumber(context),out entries) && entries.TryGetValue(scriptId,out binding) && binding.Hash==scriptHash;
        }
        // The entry's catalog binding expects counter milestones from its own handlers.
        internal static bool HasMilestoneBinding(NavigationContext context,int scriptId,string scriptHash)
        {
            Initialize();Dictionary<int,Binding> entries;Binding binding;
            return _fields.TryGetValue(FieldNumber(context),out entries) && entries.TryGetValue(scriptId,out binding) && binding.Hash==scriptHash && binding.Milestones.Length!=0;
        }
        public static bool ReviewedListener(NavigationContext context,int scriptId,string scriptHash,Func<int,string,bool> playerMatches)
        {
            Initialize();Dictionary<int,Listener> listeners;Listener listener;
            return _listeners.TryGetValue(FieldNumber(context),out listeners) && listeners.TryGetValue(scriptId,out listener) && listener.Hash==scriptHash && context.StoryProgress>=listener.Minimum && context.StoryProgress<listener.Maximum && playerMatches!=null && playerMatches(listener.PlayerSid,listener.PlayerHash);
        }
        public static void Annotate(NavigationContext context,int scriptId,string scriptHash,FieldTargetObservation observation,Func<ScriptStoryEvidence> read,Func<int,string,bool> playerMatches,NavigationPoint? at=null,Func<ScriptStoryEvidence> readPrefix=null)
        {
            ScriptStoryEvidence cached=null;Func<ScriptStoryEvidence> evidenceNow=()=>cached??(cached=read());
            observation.StoryMilestones=new int[0];observation.NextFields=new int[0];observation.StoryAction=null;observation.StoryStepMilestone=Int32.MinValue;observation.StoryVerb=null;
            Initialize();int field=FieldNumber(context);Dictionary<int,Binding> entries;Binding binding;
            if(!_fields.TryGetValue(field,out entries) || !entries.TryGetValue(scriptId,out binding) || binding.Hash!=scriptHash)return;
            if(_generation!=context.Generation || _save!=context.SaveIdentity || _player!=context.PlayerIdentity){_observedItems.Clear();_generation=context.Generation;_save=context.SaveIdentity;_player=context.PlayerIdentity;}
            if((binding.Milestones.Length!=0 || binding.Destinations.Length!=0) && (observation.Visible || observation.ShowsIcon || observation.OutOfView || observation.Exit!=null && observation.Exit.Visible)) {
                var evidence=evidenceNow();observation.StoryMilestones=new int[evidence.Milestones.Count];evidence.Milestones.CopyTo(observation.StoryMilestones);
                observation.NextFields=new int[evidence.Destinations.Count];evidence.Destinations.CopyTo(observation.NextFields);
            }
            Dictionary<int,Listener> listeners;Listener listener;
            if(ReviewedListener(context,scriptId,scriptHash,playerMatches) && _listeners.TryGetValue(field,out listeners) && listeners.TryGetValue(scriptId,out listener)) {
                observation.HasInteraction=true;observation.StoryMilestones=listener.Milestones;observation.StoryAction=listener.Action;
            }
            // A hidden script reward is not a presented pickup. Learn pickup
            // classification from the ordinary interaction cue, never its value.
            // Field 50 candle regions (MAP5[38] setters) during the native candle phase only.
            if(field==OpeningStoryCheckpoints.CargoField && scriptId>=4 && scriptId<=7 && context.StoryProgress>=OpeningStoryCheckpoints.Floor && context.StoryProgress<OpeningStoryCheckpoints.Ceiling &&
               OpeningStoryCheckpoints.IsCandleRegion(field,scriptId,scriptHash,context.StoryProgress,MapByte(OpeningStoryCheckpoints.PhaseByte))) {
                observation.StoryMilestones=new[]{OpeningStoryCheckpoints.CandleMilestone};observation.StoryAction=OpeningStoryCheckpoints.CandleAction;
            }
            // Alley walk-in/boy and Steeple ladder: reviewed native phase guards without a counter write
            // in their own handlers. The boy counts only while rendered and talkable.
            if(field==OpeningStoryCheckpoints.AlleyField || field==OpeningStoryCheckpoints.SteepleField) {
                var opening=OpeningStoryCheckpoints.Annotation(field,scriptId,scriptHash,context.StoryProgress,MapByte(OpeningStoryCheckpoints.PhaseByte),EntryHash);
                bool presented=scriptId!=OpeningStoryCheckpoints.AlleyRatKidSid || field!=OpeningStoryCheckpoints.AlleyField || (observation.Visible || observation.OutOfView) && observation.HasInteraction;
                if(opening!=null && presented){observation.StoryMilestones=new[]{opening.Milestone};observation.StoryAction=opening.Action;}
            }
            AnnotateSteps(context,field,scriptId,scriptHash,observation,evidenceNow,at??context.Position,readPrefix);
            int identity=observation.NativePresentationIdentity^scriptHash.GetHashCode(),observed;
            if(binding.Pickup && !observation.IsPerson && observation.ShowsIcon)_observedItems[observation.Id]=identity;
            observation.IsItem=!observation.IsPerson && _observedItems.TryGetValue(observation.Id,out observed) && observed==identity;
        }
        // Generic native steps (StoryStepCatalog): the entry's own current trigger. A person or prop
        // must be presented now; a step region is presented by ReadStoryRegion before this runs.
        private static void AnnotateSteps(NavigationContext context,int field,int scriptId,string scriptHash,FieldTargetObservation observation,Func<ScriptStoryEvidence> read,NavigationPoint at,Func<ScriptStoryEvidence> readPrefix)
        {
            bool exit=observation.Id.Kind==TargetKind.FieldExit;
            if(!(observation.Visible || observation.ShowsIcon || observation.OutOfView || exit && observation.Exit!=null && observation.Exit.Visible))return;
            try {
                var engine=ReadEngine();if(engine==null || engine.gMode!=1)return;
                var steps=NativeStorySteps.Candidates(engine,field,context.StoryProgress,scriptId,scriptHash,EntryHash,context.PlayerIdentity,at,observation.Id.NativeId);
                ScriptStoryEvidence prefix=null;
                steps.RemoveAll(step=>exit && step.Kind!=StoryStepKind.RegionWalk || !AcceptsStep(context,field,step.Milestone,observation.Id.NativeId) || !StoryStepCatalog.HasEffect(step,step.Source=="reviewed-prefix"?(readPrefix==null?null:prefix??(prefix=readPrefix())):read(),EntryHash));
                if(steps.Count==0)return;
                var chosen=steps[0];
                foreach(var step in steps)if(step.Priority<chosen.Priority || step.Priority==chosen.Priority && step.Milestone>=0 && (chosen.Milestone<0 || step.Milestone<chosen.Milestone))chosen=step;
                observation.StoryStepMilestone=chosen.Milestone;observation.StoryVerb=StoryStepCatalog.Verb(chosen.Kind);
                if(!String.IsNullOrEmpty(chosen.Action))observation.StoryAction=chosen.Action;
                if(!String.IsNullOrEmpty(chosen.TargetLabel))observation.VisibleLabel=chosen.TargetLabel;
            }catch(Exception exception){Log.Message("[FF9Access] Native Story step state unavailable: {0}",exception.Message);}
        }
        // A reviewed or generic current step exists for this live entry.
        internal static bool HasCurrentStep(NavigationContext context,EventEngine engine,int scriptId,string scriptHash,StoryStepKind kind,int uid=-1,bool includeReviewedWalk=false)
        {
            int field=FieldNumber(context);
            var steps=NativeStorySteps.Candidates(engine,field,context.StoryProgress,scriptId,scriptHash,EntryHash,context.PlayerIdentity,context.Position,uid);
            return steps.Exists(step=>(step.Kind==kind || includeReviewedWalk && step.Kind==StoryStepKind.RegionWalk && step.Source=="reviewed-prefix") && AcceptsStep(context,field,step.Milestone,uid));
        }
        internal static int? ReviewedApproachFacing(NavigationContext context,EventEngine engine,int scriptId,string scriptHash,int uid)
        {
            int field=FieldNumber(context);
            var steps=NativeStorySteps.Candidates(engine,field,context.StoryProgress,scriptId,scriptHash,EntryHash,context.PlayerIdentity,context.Position,uid);
            foreach(var step in steps)if(step.Kind==StoryStepKind.RegionConfirm && step.Source=="reviewed-prefix" &&
                step.ApproachFacing.HasValue && step.ApproachFacing.Value>=0 && step.ApproachFacing.Value<=255 && AcceptsStep(context,field,step.Milestone,uid))return step.ApproachFacing;
            return null;
        }
        internal static bool AcceptsStep(NavigationContext context,int field,int milestone,int uid=-1)
        {Initialize();RefreshTransit();return _catalog!=null && _catalog.AcceptsStep(context,field,milestone,MapByte,EntryHash,uid);}
        private static void RefreshTransit(){if(NativeStorySteps.RefreshTransitState() && _catalog!=null)_catalog.InvalidateState();}
        private static bool? ReadPhaseLocal(StoryPhaseLocal condition)
        {
            var engine=ReadEngine();if(engine==null || engine.gMode!=1 || FF9StateSystem.Common.FF9.fldMapNo!=condition.Field)return null;
            if(condition.MapGuards!=null && condition.MapGuards.Length!=0) {
                var map=engine.GetMapVar();if(map==null)return null;
                var snapshot=new ScriptSnapshot(FF9StateSystem.EventState.gEventGlobal,map,null){UserControl=engine.GetUserControl()};
                foreach(var expression in condition.MapGuards) {
                    int? value;
                    if(!ExitScriptAnalyzer.TryExpression(expression,snapshot,out value) || !value.HasValue)return null;
                    if(value.Value==0)return false;
                }
            }
            if(String.IsNullOrEmpty(condition.Walkmesh))return true;
            var actor=engine.GetControlChar() as Actor;var controller=actor==null?null:actor.fieldMapActorController;
            if(controller==null || !controller.isPlayer || controller.activeTri<0 || controller.fieldMap==null ||
                NativeFieldTargets.CurrentBgiHash(controller.fieldMap)!=condition.Walkmesh)return null;
            return Array.IndexOf(condition.ActiveTriangles,controller.activeTri)>=0;
        }
        private static int? ReadPhaseItem(int nativeId)
        {
            try{return ff9item.FF9Item_GetCount_Generic(nativeId);}
            catch(Exception){return null;}
        }
        // Read-only copies of the current field's native state for reviewed checkpoints.
        private static int? MapByte(int index)
        {
            try {
                var engine=ReadEngine();if(engine==null || engine.gMode!=1)return null;
                var map=engine.GetMapVar();return map!=null && index>=0 && index<map.Length?(int?)map[index]:null;
            }catch(Exception){return null;}
        }
        private static readonly Dictionary<byte[],string> _entryHashes=new Dictionary<byte[],string>();
        private static string EntryHash(int sid)
        {
            try {
                var engine=ReadEngine();if(engine==null || engine.gMode!=1)return null;
                string result=null;
                for(var list=engine.GetActiveObjList();list!=null;list=list.next) {
                    var obj=list.obj;if(obj==null || obj.sid!=sid)continue;if(obj.ebData==null)return null;
                    string hash;if(!_entryHashes.TryGetValue(obj.ebData,out hash)) {
                        if(_entryHashes.Count>256)_entryHashes.Clear();
                        using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(obj.ebData)).Replace("-","").ToLowerInvariant();
                        _entryHashes[obj.ebData]=hash;
                    }
                    if(result!=null && result!=hash)return null;result=hash;
                }
                return result;
            }catch(Exception){return null;}
        }
        private static int FieldNumber(NavigationContext context)
        {
            int field=-1;if(context==null || context.LogicalMapKey==null || !context.LogicalMapKey.StartsWith("field:"))return field;
            var parts=context.LogicalMapKey.Split(':');if(parts.Length<=1 || !Int32.TryParse(parts[1],out field))return -1;return field;
        }
        // Same-name fields keep the distinct area labels used by exits and announcements.
        private static string AreaName(int field)
        {string scene;return NativeFieldAreaNames.Read(field,EventEngineUtils.eventIDToFBGID.TryGetValue(field,out scene)?scene:null);}
        // Current reviewed native position wait (rooftop control windows, or the hidden alley boy at 1151).
        // Callers supply walkable floor points satisfying goal.Satisfied; the goal itself is not a position.
        public static OpeningPositionGoal CurrentPositionGoal(NavigationContext context)
        {
            int field=FieldNumber(context);
            if(field==OpeningCastleStory.HallField)return OpeningCastleStory.CurrentGoal(context,MapByte(OpeningCastleStory.PhaseByte),EntryHash);
            if(field==OpeningRooftopEvidence.Field)return OpeningRooftopEvidence.Current(context,MapByte(OpeningStoryCheckpoints.PhaseByte),EntryHash);
            if(field!=OpeningStoryCheckpoints.AlleyField)return GenericPositionGoal(context,field);
            bool shown;
            try {
                var engine=UnityEngine.Object.FindObjectOfType<EventEngine>();if(engine==null || engine.gMode!=1)return null;
                var boy=engine.GetObjUID(OpeningStoryCheckpoints.AlleyRatKidSid);
                // A missing boy entry is not a hidden boy.
                if(boy==null || boy.sid!=OpeningStoryCheckpoints.AlleyRatKidSid)return null;
                shown=(boy.flags&EventEngine.flagShow)!=0;
            }catch(Exception){return null;}
            return OpeningStoryCheckpoints.AlleyBoyGoal(context,MapByte(OpeningStoryCheckpoints.PhaseByte),EntryHash,shown);
        }
        // The controlled player's own current native position wait in any other field. Several
        // simultaneous different waits are ambiguous and give no goal.
        private static OpeningPositionGoal GenericPositionGoal(NavigationContext context,int field)
        {
            if(context==null || !context.CanControl || field<0)return null;
            try {
                var engine=UnityEngine.Object.FindObjectOfType<EventEngine>();if(engine==null || engine.gMode!=1)return null;
                var steps=NativeStorySteps.CurrentPositions(engine,field,context.StoryProgress,context.PlayerIdentity,EntryHash,context.Position);
                Initialize();steps.RemoveAll(step=>!AcceptsStep(context,field,step.Milestone));
                var selected=StoryStepCatalog.SelectPosition(steps);
                return selected==null?null:StoryStepCatalog.Goal(selected,context.LogicalMapKey,100000+selected.Sid*4+(int)selected.Axis);
            }catch(Exception){return null;}
        }
        public static void Append(NavigationContext context,CatalogResult catalog)
        {
            if(TryAppendRequiredAte(context,catalog))return;
            if(NativeStoryPrerequisites.TryAppend(context,catalog))return;
            Initialize();RefreshTransit();if(_catalog!=null)_catalog.Append(context,catalog,FieldNumber(context),AreaName,MapByte,EntryHash);
            else if(context!=null && context.StoryProgress>=0)catalog.Targets.Add(new NavigationTarget{Id=new TargetIdentity(TargetKind.Story,context.LogicalMapKey,-1),Category=NavigationCategory.Story,Generation=context.Generation,Position=context.Position,Label="Story guidance unavailable.",Availability="The Story catalog could not be read.",ApproachCandidates=new NavigationPoint[0]});
        }
        private static bool TryAppendRequiredAte(NavigationContext context,CatalogResult catalog)
        {
            if(context==null || catalog==null || !context.Foreground || context.StoryProgress<10010 || context.StoryProgress>=10050)return false;
            int field=FieldNumber(context);
            if(field!=2161 && field!=2162 && field!=2163 && field!=2168 && field!=2169 && field!=2170)return false;
            var hud=UIManager.Field;
            if(EIcon.AIconMode==0 || hud==null || hud.ATEGameObject==null || !hud.ATEGameObject.activeInHierarchy)return false;
            int milestone=context.StoryProgress<10030?10030:10050;
            catalog.Targets.Add(new NavigationTarget{Id=new TargetIdentity(TargetKind.Story,context.LogicalMapKey,milestone),Category=NavigationCategory.Story,
                Generation=context.Generation,Position=context.Position,Label="Active Time Event available.",
                Availability="Use the game's Active Time Event control when prompted and read the offered event.",ApproachCandidates=new NavigationPoint[0]});
            return true;
        }
    }
}
