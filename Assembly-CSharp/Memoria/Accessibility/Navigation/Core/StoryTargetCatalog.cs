using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class StoryStage {public int Milestone;public int[] GoalFields,DepartureFields,WorldLocations;public string Goal,Instruction,RequiredVehicle;public string[] WorldPlaces;public bool InformationOnly;}
    public sealed class StoryTransit {public int From,To;}
    // A reviewed sub-step that shares a scenario-counter floor with later scenes. It applies only while
    // Floor <= progress < Ceiling, in one of ScopeFields, when every required live script entry hash matches
    // and (if MapByte >= 0) the field's own native phase byte equals MapValue. Unknown state never matches.
    public sealed class StoryCheckpoint {public int Floor,Ceiling,MapByte=-1,MapValue;public int[] ScopeFields;public Dictionary<int,string> RequiredScripts;public StoryStage Stage;}
    public sealed class StoryTargetCatalog
    {
        private readonly StoryStage[] _stages;
        private readonly StoryCheckpoint[] _checkpoints;
        private readonly Dictionary<int,List<int>> _incoming=new Dictionary<int,List<int>>();
        private readonly Dictionary<int,int> _distance=new Dictionary<int,int>();
        private StoryStage _stage;
        public StoryTargetCatalog(StoryStage[] stages,StoryTransit[] transit):this(stages,transit,null){}
        public StoryTargetCatalog(StoryStage[] stages,StoryTransit[] transit,StoryCheckpoint[] checkpoints)
        {
            _checkpoints=checkpoints==null?new StoryCheckpoint[0]:(StoryCheckpoint[])checkpoints.Clone();
            _stages=stages==null?new StoryStage[0]:(StoryStage[])stages.Clone();Array.Sort(_stages,(a,b)=>a.Milestone.CompareTo(b.Milestone));
            if(transit!=null)foreach(var edge in transit) {
                List<int> list;if(!_incoming.TryGetValue(edge.To,out list)){list=new List<int>();_incoming[edge.To]=list;}
                if(!list.Contains(edge.From))list.Add(edge.From);
            }
        }
        private StoryStage Checkpoint(int progress,int field,Func<int,int?> mapByte,Func<int,string> scripts)
        {
            foreach(var checkpoint in _checkpoints) {
                if(checkpoint.Stage==null || progress<checkpoint.Floor || progress>=checkpoint.Ceiling || !Contains(checkpoint.ScopeFields,field))continue;
                if(checkpoint.MapByte>=0 && (mapByte==null || mapByte(checkpoint.MapByte)!=checkpoint.MapValue))continue;
                bool verified=true;
                if(checkpoint.RequiredScripts!=null)foreach(var required in checkpoint.RequiredScripts)if(scripts==null || scripts(required.Key)!=required.Value){verified=false;break;}
                if(verified)return checkpoint.Stage;
            }
            return null;
        }
        private void SelectStage(int progress,int currentField,Func<int,int?> mapByte,Func<int,string> scripts)
        {
            StoryStage next=Checkpoint(progress,currentField,mapByte,scripts);
            if(next==null)foreach(var stage in _stages)if(stage.Milestone>progress){next=stage;break;}
            if(next==_stage)return;_stage=next;_distance.Clear();if(next==null)return;
            var pending=new Queue<int>();var goals=next.DepartureFields!=null && next.DepartureFields.Length!=0?next.DepartureFields:next.GoalFields;
            foreach(int field in goals??new int[0])if(!_distance.ContainsKey(field)){_distance[field]=0;pending.Enqueue(field);}
            while(pending.Count!=0) {
                int field=pending.Dequeue();List<int> from;if(!_incoming.TryGetValue(field,out from))continue;
                foreach(int previous in from)if(!_distance.ContainsKey(previous)){_distance[previous]=_distance[field]+1;pending.Enqueue(previous);}
            }
        }
        // mapByte reads the current field's native map variable bytes; scripts returns the live entry hash for a script id.
        public void Append(NavigationContext context,CatalogResult catalog,int field,Func<int,string> location=null,Func<int,int?> mapByte=null,Func<int,string> scripts=null)
        {
            if(context==null || catalog==null || context.StoryProgress<0 || String.IsNullOrEmpty(context.LogicalMapKey))return;
            SelectStage(context.StoryProgress,field,mapByte,scripts);
            if(_stage==null){catalog.Targets.Add(Information(context,context.StoryProgress,"Final story scene.","Continue the game's shown dialogue and battles. No further walking objective is recorded."));return;}
            if(_stage.InformationOnly){
                string shown=location==null?context.CurrentLocation:location(field);if(String.IsNullOrEmpty(shown))shown="current scene";
                catalog.Targets.Add(Information(context,_stage.Milestone,"Continue the story: "+shown+".","Continue the current scene using the game's shown interactions. No verified story approach is currently presented."));return;
            }
            string goal=String.IsNullOrEmpty(_stage.Goal)?"the next story scene":_stage.Goal;
            var selected=new List<NavigationTarget>();int best=Int32.MaxValue;
            bool world=context.LogicalMapKey.StartsWith("world:");int here;bool currentKnown=_distance.TryGetValue(field,out here);
            bool departure=_stage.DepartureFields!=null && _stage.DepartureFields.Length!=0;
            if((world || departure) && _stage.WorldPlaces!=null && _stage.WorldPlaces.Length==1)goal=_stage.WorldPlaces[0];
            foreach(var target in catalog.Targets) {
                if(target.Category==NavigationCategory.Story || target.Generation!=context.Generation || target.Id.MapKey!=context.LogicalMapKey)continue;
                int score=Int32.MaxValue;
                if(Contains(target.StoryMilestones,_stage.Milestone))score=0;
                else if(world && target.Category==NavigationCategory.Towns && WorldPlace(target,goal))score=1;
                else if(!world && (target.PhysicalIdentity.Kind==TargetKind.FieldExit || target.IsInteractive)) {
                    foreach(int next in target.NextFields??new int[0]) {
                        int remaining;if(_distance.TryGetValue(next,out remaining) && (!currentKnown || remaining<here))score=Math.Min(score,remaining+1);
                        else if((!currentKnown || here>0) && next>=9000)score=Math.Min(score,100000);
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
            if(!world && !departure && location!=null && _stage.GoalFields!=null && _stage.GoalFields.Length==1){string shown=location(_stage.GoalFields[0]);if(!String.IsNullOrEmpty(shown))scene=shown;}
            if(selected.Count==0) {
                string help=currentKnown && here==0?"Continue the current scene using the game's shown interactions. No verified story approach is currently presented.":"Travel toward "+goal+". A verified route is not currently presented in this area.";
                if(!String.IsNullOrEmpty(_stage.Instruction))help=_stage.Instruction+" "+help;
                catalog.Targets.Add(Information(context,_stage.Milestone,"Continue the story: "+scene+".",help));return;
            }
            foreach(var physical in selected) {
                string action=world && physical.Category==NavigationCategory.Vehicles?"Approach "+physical.Label+" for travel toward "+goal:
                    best==0?(!String.IsNullOrEmpty(physical.StoryAction)?physical.StoryAction:"Continue the story with "+physical.Label+". "+scene):"Continue toward "+scene+" via "+physical.Label;
                var wrapped=Wrap(context,_stage.Milestone,physical,action);
                if(departure && best==0)wrapped.Label+=". Next destination: "+goal;
                if((world || departure) && !String.IsNullOrEmpty(_stage.Instruction))wrapped.Availability=(_stage.Instruction+" "+wrapped.Availability).Trim();
                catalog.Targets.Add(wrapped);
            }
        }
        public static NavigationTarget Wrap(NavigationContext context,int milestone,NavigationTarget physical,string label)
        {
            var id=physical.PhysicalIdentity;
            return new NavigationTarget{Id=new TargetIdentity(TargetKind.Story,context.LogicalMapKey,milestone*4096+(int)id.Kind*512+id.NativeId),PhysicalId=id,Category=NavigationCategory.Story,
                Label=label,Availability=physical.Availability,Position=physical.Position,Evidence=physical.Evidence,Generation=context.Generation,
                IsInteractive=physical.IsInteractive,NativeLocationId=physical.NativeLocationId,InteractionUid=physical.InteractionUid,UseViewingPoint=physical.UseViewingPoint,
                ApproachCandidates=Copy(physical.ApproachCandidates),InteractionRegion=Copy(physical.InteractionRegion),StoryMilestones=Copy(physical.StoryMilestones),NextFields=Copy(physical.NextFields),StoryAction=physical.StoryAction};
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
