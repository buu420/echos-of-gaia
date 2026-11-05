using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class StoryStage {public int Milestone;public int[] GoalFields,ApproachFields,DepartureFields,WorldDepartureFields,WorldLocations,TransitFields,ActionUids;public string Goal,Instruction,RequiredVehicle,InformationTitle,InformationReason;public string[] WorldPlaces;public bool InformationOnly,KeepGoalLabel;public StoryTransit[] ReviewedLinks;}
    // Kind is the native trigger of the MAPJUMP (Export-FF9StorySteps.py). Untyped edges are Player.
    public sealed class StoryTransit {public int From,To,ActionUid=-1;public StoryTransitKind Kind;public int? Floor,Ceiling;public string Scope;public byte[][] Guards=new byte[0][];}
    // A reviewed sub-step that shares a scenario-counter floor with later scenes. It applies only while
    // Floor <= progress < Ceiling, in one of ScopeFields, when every required live script entry hash matches
    // and (if MapByte >= 0) the field's own native phase byte equals MapValue. Unknown state never matches.
    public sealed class StoryCheckpoint {public int Floor,Ceiling,MapByte=-1,MapValue,PlayerIdentity=-1;public int[] ScopeFields;public Dictionary<int,string> RequiredScripts;public StoryStage Stage;}
    public sealed class StoryTargetCatalog
    {
        private readonly StoryStage[] _stages;
        private readonly StoryCheckpoint[] _checkpoints;
        private readonly Dictionary<int,List<StoryTransit>> _incoming=new Dictionary<int,List<StoryTransit>>();
        private readonly Dictionary<int,int> _distance=new Dictionary<int,int>();
        private readonly Dictionary<int,int> _nextField=new Dictionary<int,int>();
        private const int WorldTransferDistance=100000;
        private StoryStage _stage;private int _stageProgress=Int32.MinValue,_stageField=Int32.MinValue;
        private Func<StoryTransit,bool?> _transitEvaluator;
        private Func<int,int,Func<int,string>,StoryStage> _phaseSelector;
        public void SetTransitEvaluator(Func<StoryTransit,bool?> evaluator){_transitEvaluator=evaluator;InvalidateState();}
        public void SetPhaseSelector(Func<int,int,Func<int,string>,StoryStage> selector){_phaseSelector=selector;InvalidateState();}
        public void InvalidateState(){_stageProgress=Int32.MinValue;}
        public StoryTargetCatalog(StoryStage[] stages,StoryTransit[] transit):this(stages,transit,null){}
        public StoryTargetCatalog(StoryStage[] stages,StoryTransit[] transit,StoryCheckpoint[] checkpoints)
        {
            _checkpoints=checkpoints==null?new StoryCheckpoint[0]:(StoryCheckpoint[])checkpoints.Clone();
            _stages=stages==null?new StoryStage[0]:(StoryStage[])stages.Clone();Array.Sort(_stages,(a,b)=>a.Milestone.CompareTo(b.Milestone));
            if(transit!=null)foreach(var edge in transit) {
                if(edge==null)continue;
                List<StoryTransit> list;if(!_incoming.TryGetValue(edge.To,out list)){list=new List<StoryTransit>();_incoming[edge.To]=list;}
                list.Add(edge);
            }
        }
        private StoryStage Checkpoint(int progress,int field,int player,Func<int,int?> mapByte,Func<int,string> scripts)
        {
            foreach(var checkpoint in _checkpoints) {
                if(checkpoint.Stage==null || progress<checkpoint.Floor || progress>=checkpoint.Ceiling || !Contains(checkpoint.ScopeFields,field))continue;
                if(checkpoint.PlayerIdentity>=0 && checkpoint.PlayerIdentity!=player)continue;
                if(checkpoint.MapByte>=0 && (mapByte==null || mapByte(checkpoint.MapByte)!=checkpoint.MapValue))continue;
                bool verified=true;
                if(checkpoint.RequiredScripts!=null)foreach(var required in checkpoint.RequiredScripts)if(scripts==null || scripts(required.Key)!=required.Value){verified=false;break;}
                if(verified)return checkpoint.Stage;
            }
            return null;
        }
        private void SelectStage(int progress,int currentField,int player,Func<int,int?> mapByte,Func<int,string> scripts)
        {
            StoryStage next=Checkpoint(progress,currentField,player,mapByte,scripts);
            if(next==null && _phaseSelector!=null)next=_phaseSelector(progress,currentField,scripts);
            if(next==null)foreach(var stage in _stages)if(stage.Milestone>progress){next=stage;break;}
            if(next==_stage && progress==_stageProgress && currentField==_stageField)return;_stage=next;_stageProgress=progress;_stageField=currentField;_distance.Clear();_nextField.Clear();if(next==null)return;
            var incomingGraph=_incoming;
            if(next.ReviewedLinks!=null) {
                incomingGraph=new Dictionary<int,List<StoryTransit>>();
                foreach(var edge in next.ReviewedLinks) {
                    List<StoryTransit> list;if(!incomingGraph.TryGetValue(edge.To,out list)){list=new List<StoryTransit>();incomingGraph[edge.To]=list;}
                    list.Add(edge);
                }
            }
            var pending=new Queue<int>();var goals=next.ApproachFields!=null && next.ApproachFields.Length!=0?next.ApproachFields:next.DepartureFields!=null && next.DepartureFields.Length!=0?next.DepartureFields:next.GoalFields;
            foreach(int field in goals??new int[0])if(!_distance.ContainsKey(field)){_distance[field]=0;pending.Enqueue(field);}
            WalkBack(pending,incomingGraph,next,progress,currentField,false);
            // A reviewed overland leg can begin several rooms before the
            // world-map exit. Keep a complete in-town route ahead of a transfer
            // and do not use cutscene jumps as shortcuts to the departure gate.
            foreach(int gate in next.WorldDepartureFields??new int[0]) {
                if(_distance.ContainsKey(gate))continue;
                _distance[gate]=WorldTransferDistance-1;
                if(gate!=currentField)pending.Enqueue(gate);
            }
            WalkBack(pending,incomingGraph,next,progress,currentField,true);
            // An informational next-area hint may use a portable outgoing edge.
            // Do not add the current room to the reverse graph: it must never
            // become an intermediate shortcut, and walking still needs a live exit.
            int nearest=Int32.MaxValue;
            if(!_distance.ContainsKey(currentField))foreach(var incoming in incomingGraph.Values)foreach(var edge in incoming) {
                int remaining;
                if(edge.From==currentField && _distance.TryGetValue(edge.To,out remaining) && remaining<nearest && Usable(edge,progress,remaining==0,next.TransitFields)) {
                    nearest=remaining;_nextField[currentField]=edge.To;
                }
            }
        }
        private void WalkBack(Queue<int> pending,Dictionary<int,List<StoryTransit>> graph,StoryStage stage,int progress,int currentField,bool worldTransfer)
        {
            while(pending.Count!=0) {
                int field=pending.Dequeue();List<StoryTransit> from;if(!graph.TryGetValue(field,out from))continue;
                foreach(var edge in from) {
                    int previous=edge.From;
                    // The current hop always needs a live physical exit.
                    if(previous==currentField || _distance.ContainsKey(previous) || stage.TransitFields!=null && !Contains(stage.TransitFields,previous) ||
                        worldTransfer && edge.Kind!=StoryTransitKind.Player || !Usable(edge,progress,_distance[field]==0,stage.TransitFields))continue;
                    _distance[previous]=_distance[field]+1;_nextField[previous]=field;pending.Enqueue(previous);
                }
            }
        }
        private bool UsesReviewedLink(StoryStage stage,int field,NavigationTarget target,int destination)
        {
            if(stage.ReviewedLinks==null)return true;
            foreach(var edge in stage.ReviewedLinks)if(edge.From==field && edge.To==destination &&
                (edge.ActionUid<0 || edge.ActionUid==target.PhysicalIdentity.NativeId || edge.ActionUid==target.InteractionUid) && Usable(edge,_stageProgress,true,null))return true;
            return false;
        }
        // A walking route uses player-triggered transitions (doors, walk-offs, Confirm transitions,
        // position-gated exits). A scripted scene jump only completes the final hop into a goal field:
        // used as an intermediate hop it would invent shortcuts through unrelated or future scenes.
        private bool Usable(StoryTransit edge,int progress,bool intoGoal,int[] reviewedFields)
        {
            if(edge.Floor.HasValue && progress<edge.Floor.Value || edge.Ceiling.HasValue && progress>=edge.Ceiling.Value)return false;
            if(edge.Scope=="room" && !(edge.Kind==StoryTransitKind.Player && reviewedFields!=null && Contains(reviewedFields,edge.From) && Contains(reviewedFields,edge.To)))return false;
            if(edge.Scope!="room" && edge.Guards!=null && edge.Guards.Length!=0 && (_transitEvaluator==null || _transitEvaluator(edge)!=true))return false;
            return edge.Kind!=StoryTransitKind.Scripted || intoGoal;
        }
        private int? RouteRemaining(int field,NavigationTarget target,int destination)
        {
            if(!UsesReviewedLink(_stage,field,target,destination))return null;
            NavigationPoint[] approved;
            if(target.DestinationApproaches!=null && (!target.DestinationApproaches.TryGetValue(destination,out approved) || approved==null || approved.Length==0))return null;
            int here,remaining;bool currentKnown=_distance.TryGetValue(field,out here);
            if(_distance.TryGetValue(destination,out remaining) && (!currentKnown || remaining<here))return remaining;
            if(destination>=9000 && (!currentKnown || here>0) &&
                (_stage.WorldDepartureFields==null || Contains(_stage.WorldDepartureFields,field)))return WorldTransferDistance-1;
            return null;
        }
        public bool AcceptsStep(NavigationContext context,int field,int milestone,Func<int,int?> mapByte,Func<int,string> scripts,int uid=-1)
        {
            if(context==null)return false;SelectStage(context.StoryProgress,field,context.PlayerIdentity,mapByte,scripts);
            if(_stage==null || _stage.InformationOnly)return false;
            int here;bool goal=_distance.TryGetValue(field,out here) && here==0;
            if(uid>=0 && _stage.ActionUids!=null && (!goal || !Contains(_stage.ActionUids,uid)))return false;
            return milestone==_stage.Milestone || milestone<0 && goal;
        }
        // mapByte reads the current field's native map variable bytes; scripts returns the live entry hash for a script id.
        public void Append(NavigationContext context,CatalogResult catalog,int field,Func<int,string> location=null,Func<int,int?> mapByte=null,Func<int,string> scripts=null)
        {
            if(context==null || catalog==null || context.StoryProgress<0 || String.IsNullOrEmpty(context.LogicalMapKey))return;
            SelectStage(context.StoryProgress,field,context.PlayerIdentity,mapByte,scripts);
            if(_stage==null){catalog.Targets.Add(Information(context,context.StoryProgress,"Final story scene.","Continue the game's shown dialogue and battles. No further walking objective is recorded."));return;}
            if(_stage.InformationOnly){
                if(!String.IsNullOrEmpty(_stage.InformationReason)){catalog.Targets.Add(Information(context,_stage.Milestone,_stage.InformationTitle??"Story guidance unavailable.",_stage.InformationReason));return;}
                string shown=location==null?context.CurrentLocation:location(field);if(String.IsNullOrEmpty(shown))shown="current scene";
                catalog.Targets.Add(Information(context,_stage.Milestone,"Continue the story: "+shown+".","Continue the current scene. No story action is available to walk to yet."));return;
            }
            string goal=String.IsNullOrEmpty(_stage.Goal)?"the next story scene":_stage.Goal;
            var selected=new List<NavigationTarget>();int best=Int32.MaxValue;
            bool world=context.LogicalMapKey.StartsWith("world:");int here;bool currentKnown=_distance.TryGetValue(field,out here);
            bool departure=_stage.DepartureFields!=null && _stage.DepartureFields.Length!=0;
            if((world || departure) && _stage.WorldPlaces!=null && _stage.WorldPlaces.Length==1)goal=_stage.WorldPlaces[0];
            foreach(var target in catalog.Targets) {
                if(target.Category==NavigationCategory.Story || target.Generation!=context.Generation || target.Id.MapKey!=context.LogicalMapKey)continue;
                int score=Int32.MaxValue;
                // A field actor annotated before its final native interaction gates is a goal only while interactive.
                bool step=target.StoryStepMilestone!=Int32.MinValue && (target.StoryStepMilestone==_stage.Milestone || target.StoryStepMilestone<0 && currentKnown && here==0);
                bool phaseAction=_stage.ActionUids!=null && currentKnown && here==0 && target.IsInteractive && target.ApproachCandidates!=null &&
                    (target.StoryStepMilestone==_stage.Milestone || Contains(target.StoryMilestones,_stage.Milestone)) &&
                    (Contains(_stage.ActionUids,target.PhysicalIdentity.NativeId) || Contains(_stage.ActionUids,target.InteractionUid));
                if(phaseAction || _stage.ActionUids==null && (step || Contains(target.StoryMilestones,_stage.Milestone))){if(target.PhysicalIdentity.Kind!=TargetKind.FieldActor || target.IsInteractive)score=0;}
                else if(world && target.Category==NavigationCategory.Towns && WorldPlace(target,goal))score=1;
                else if(!world && (target.PhysicalIdentity.Kind==TargetKind.FieldExit || target.IsInteractive)) {
                    foreach(int next in target.NextFields??new int[0]) {
                        int? remaining=RouteRemaining(field,target,next);if(remaining.HasValue)score=Math.Min(score,remaining.Value+1);
                    }
                }
                if(score==Int32.MaxValue || target.ApproachCandidates!=null && target.ApproachCandidates.Length==0)continue;
                if(score<best){selected.Clear();best=score;}if(score==best)selected.Add(target);
            }
            if(selected.Count==0 && world && context.Transport.Kind==TransportKind.Foot) {
                NavigationTarget vessel=null;int rank=0;
                foreach(var target in catalog.Targets)if(target.Category==NavigationCategory.Vehicles) {
                    if(_stage.RequiredVehicle!=null){if(target.Label==_stage.RequiredVehicle)vessel=target;}
                    else {int candidate=target.Label=="Invincible"?3:target.Label=="Hilda Garde III"?2:target.Label=="Blue Narciss"?1:0;if(candidate>rank){rank=candidate;vessel=target;}}
                }
                if(vessel!=null)selected.Add(vessel);
            }
            string scene=goal;
            if(!world && !departure && !_stage.KeepGoalLabel && location!=null && _stage.GoalFields!=null && _stage.GoalFields.Length==1){string shown=location(_stage.GoalFields[0]);if(!String.IsNullOrEmpty(shown))scene=shown;}
            if(selected.Count==0) {
                string help=currentKnown && here==0?"The next story step is in this area, but no verified target for it is presented yet.":"Travel toward "+goal+". A verified route is not currently presented in this area.";
                int nextField;string nextArea=null;if(!world && _nextField.TryGetValue(field,out nextField) && location!=null)nextArea=location(nextField);
                if(!String.IsNullOrEmpty(nextArea))help="Next area on the story route: "+nextArea+". "+help;
                if(!String.IsNullOrEmpty(_stage.Instruction))help=_stage.Instruction+" "+help;
                string heading=String.IsNullOrEmpty(_stage.Instruction)?"Continue the story: "+scene+".":_stage.Instruction.Trim()+" "+scene+".";
                catalog.Targets.Add(Information(context,_stage.Milestone,heading,help));return;
            }
            foreach(var physical in selected) {
                string action=best==0 && _stage.ActionUids!=null?(String.IsNullOrEmpty(physical.StoryAction)?_stage.Instruction:physical.StoryAction):world && physical.Category==NavigationCategory.Vehicles?"Approach "+physical.Label+" for travel toward "+goal:
                    best==0?(!String.IsNullOrEmpty(physical.StoryAction)?physical.StoryAction:!String.IsNullOrEmpty(physical.StoryVerb)?physical.StoryVerb+" "+physical.Label:"Continue the story with "+physical.Label+". "+scene):
                    !String.IsNullOrEmpty(_stage.Instruction)?_stage.Instruction.Trim()+" Continue via "+physical.Label:
                    physical.SelectDestinationInMenu?"Continue via "+physical.Label:"Continue toward "+scene+" via "+physical.Label;
                if(!world && best>0 && location!=null && !physical.SelectDestinationInMenu) {
                    string nextArea=null;int nearest=Int32.MaxValue;
                    foreach(int next in physical.NextFields??new int[0]){int? remaining=RouteRemaining(field,physical,next);if(remaining.HasValue && remaining.Value<nearest){nearest=remaining.Value;nextArea=location(next);}}
                    if(!String.IsNullOrEmpty(nextArea))action="Go to "+nextArea+" via "+physical.Label+". "+(String.IsNullOrEmpty(_stage.Instruction)?"Continue toward "+scene+".":_stage.Instruction.Trim());
                }
                var wrapped=Wrap(context,_stage.Milestone,physical,action);
                if(!world && best>0 && physical.DestinationApproaches!=null) {
                    int nearest=Int32.MaxValue;var nextFields=new List<int>();
                    foreach(int next in physical.NextFields??new int[0]) {
                        int? remaining=RouteRemaining(field,physical,next);if(!remaining.HasValue)continue;
                        if(remaining.Value<nearest){nearest=remaining.Value;nextFields.Clear();}if(remaining.Value==nearest)nextFields.Add(next);
                    }
                    wrapped.ApproachCandidates=FieldExitDestinations.Select(physical.DestinationApproaches,nextFields);wrapped.NextFields=nextFields.ToArray();wrapped.UseApprovedApproaches=true;
                }
                if(departure && best==0)wrapped.Label+=". Next destination: "+goal;
                if((world || departure) && !String.IsNullOrEmpty(_stage.Instruction))wrapped.Availability=(_stage.Instruction+" "+wrapped.Availability).Trim();
                catalog.Targets.Add(wrapped);
            }
        }
        public static NavigationTarget Wrap(NavigationContext context,int milestone,NavigationTarget physical,string label)
        {
            var id=physical.PhysicalIdentity;
            int variant=physical.Id.Variant!=0?physical.Id.Variant:physical.PhysicalId.HasValue?physical.Id.NativeId:0;
            return new NavigationTarget{Id=new TargetIdentity(TargetKind.Story,context.LogicalMapKey,milestone*4096+(int)id.Kind*512+id.NativeId,variant),PhysicalId=id,Category=NavigationCategory.Story,
                Label=label,Availability=physical.Availability,Position=physical.Position,Evidence=physical.Evidence,Generation=context.Generation,
                IsInteractive=physical.IsInteractive,NativeLocationId=physical.NativeLocationId,InteractionUid=physical.InteractionUid,UseViewingPoint=physical.UseViewingPoint,UseApprovedApproaches=physical.UseApprovedApproaches,SelectDestinationInMenu=physical.SelectDestinationInMenu,
                ApproachCandidates=Copy(physical.ApproachCandidates),InteractionRegion=Copy(physical.InteractionRegion),StoryMilestones=Copy(physical.StoryMilestones),NextFields=Copy(physical.NextFields),DestinationApproaches=FieldExitDestinations.Copy(physical.DestinationApproaches),StoryAction=physical.StoryAction,StoryStepMilestone=physical.StoryStepMilestone,StoryVerb=physical.StoryVerb};
        }
        private static NavigationTarget Information(NavigationContext context,int milestone,string label,string help)
        {return new NavigationTarget{Id=new TargetIdentity(TargetKind.Story,context.LogicalMapKey,milestone*4096+4095),Category=NavigationCategory.Story,Label=label,Availability=help,Position=context.Position,Generation=context.Generation,ApproachCandidates=new NavigationPoint[0]};}
        private static bool Contains(int[] values,int value){if(values!=null)foreach(int item in values)if(item==value)return true;return false;}
        private static NavigationPoint[] Copy(NavigationPoint[] points){return points==null?null:(NavigationPoint[])points.Clone();}
        private static int[] Copy(int[] values){return values==null?null:(int[])values.Clone();}
        private static string Normal(string value){var result=new System.Text.StringBuilder();foreach(char c in (value??"").ToLowerInvariant())if(Char.IsLetterOrDigit(c))result.Append(c);return result.ToString();}
        private static bool SamePlace(string label,string goal){var a=Normal(label);foreach(var part in (goal??"").Split(new[]{" or "},StringSplitOptions.None)){var b=Normal(part);if(a.Length>2 && b.Length>2 && (a==b || b=="villageof"+a || b==a+"village"))return true;}return false;}
        private static bool MatchesPlace(string label,string[] places,string goal){if(places!=null)foreach(string place in places)if(SamePlace(label,place))return true;return SamePlace(label,goal);}
        private bool WorldPlace(NavigationTarget target,string goal){return _stage.WorldLocations!=null?Contains(_stage.WorldLocations,target.NativeLocationId>=0?target.NativeLocationId:target.PhysicalIdentity.NativeId):MatchesPlace(target.Label,_stage.WorldPlaces,goal);}
    }
}
