using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class FieldTargetCatalog
    {
        // Per map: the targets presented with each category and description, in first-presented order.
        private readonly Dictionary<string,List<TargetIdentity>> _occurrences=new Dictionary<string,List<TargetIdentity>>();
        private readonly HashSet<string> _plain=new HashSet<string>();
        private sealed class ObservedTarget {public NavigationTarget Target;public int PresentationIdentity;public string Scope;}
        private readonly Dictionary<TargetIdentity, ObservedTarget> _lastSeen = new Dictionary<TargetIdentity, ObservedTarget>();
        // The screen side an exit was last presented at (FieldExitLabel), for exits now outside the view.
        private readonly Dictionary<TargetIdentity,string> _exitSides=new Dictionary<TargetIdentity,string>();
        private string _map;
        private long _generation;private int _player;private string _save;
        public CatalogResult Build(NavigationContext context, IList<FieldTargetObservation> observations, ObservedIdentityStore identities)
        {
            var result = new CatalogResult();
            string presentedRoom=context.SubArea==0?context.LogicalMapKey:context.LogicalMapKey+":"+context.SubArea;
            if (_map != presentedRoom) { _occurrences.Clear();_plain.Clear(); _lastSeen.Clear();_exitSides.Clear(); _map = presentedRoom; }
            if (_generation != context.Generation || _player != context.PlayerIdentity || _save != context.SaveIdentity) {
                _lastSeen.Clear();_generation=context.Generation;_player=context.PlayerIdentity;_save=context.SaveIdentity;
            }
            var active=new HashSet<TargetIdentity>();var exits=new Dictionary<NavigationTarget,FieldTargetObservation>();
            var ordered = new List<FieldTargetObservation>(observations); ordered.Sort((a,b) => a.Id.NativeId.CompareTo(b.Id.NativeId));
            // Register every presented description first, so the first of two identical targets is
            // already numbered in the same build.
            foreach(var observation in ordered)
                if(observation.Id.Kind!=TargetKind.FieldExit && observation.Id.MapKey==context.LogicalMapKey && observation.Position.IsFinite && (observation.Visible || observation.ShowsIcon))Register(observation,Category(observation));
            foreach (var observation in ordered) {
                if (observation.Id.MapKey != context.LogicalMapKey || !observation.Position.IsFinite) {_lastSeen.Remove(observation.Id);continue;}
                active.Add(observation.Id);
                bool exit = observation.Id.Kind == TargetKind.FieldExit;
                bool presented=exit?observation.Exit!=null && observation.Exit.Visible:observation.Visible || observation.ShowsIcon;
                if(exit && !String.IsNullOrEmpty(observation.VisibleLabel))_exitSides[observation.Id]=observation.VisibleLabel;
                if(!presented) {
                    ObservedTarget seen;
                    if(observation.OutOfView && _lastSeen.TryGetValue(observation.Id,out seen) && seen.PresentationIdentity==observation.NativePresentationIdentity && seen.Scope==observation.LabelIdentity) {
                        var remembered=Copy(seen.Target);remembered.Generation=context.Generation;remembered.Evidence=VisibilityEvidence.ObservedFieldLandmark;
                        // Preserve only the observed position. Story outcomes
                        // must use today's native guards, even outside the view.
                        if(observation.StoryMilestones!=null)remembered.StoryMilestones=(int[])observation.StoryMilestones.Clone();
                        if(observation.NextFields!=null)remembered.NextFields=(int[])observation.NextFields.Clone();
                        remembered.StoryAction=observation.StoryAction;remembered.StoryStepMilestone=observation.StoryStepMilestone;remembered.StoryVerb=observation.StoryVerb;
                        remembered.DestinationApproaches=FieldExitDestinations.Copy(observation.DestinationApproaches);
                        string availability=seen.Target.Availability;
                        if(!exit){
                            remembered.Category=Category(observation);remembered.Label=Label(observation,remembered.Category);
                            bool sameInteraction=SamePoint(remembered.Position,observation.Position) &&
                                remembered.InteractionUid==observation.InteractionUid && SameRegion(remembered.InteractionRegion,observation.InteractionRegion);
                            remembered.IsInteractive=observation.HasInteraction && sameInteraction &&
                                (observation.ApproachCandidates==null || observation.ApproachCandidates.Length>0);
                            remembered.UseViewingPoint=observation.UseViewingPoint || !remembered.IsInteractive;
                            availability=remembered.IsInteractive?null:"interaction unavailable";
                            if(remembered.IsInteractive) {
                                // The speaker has not moved. The current approaches may still
                                // change as the player moves or the native collision state changes.
                                remembered.ApproachCandidates=observation.ApproachCandidates==null?new[]{remembered.Position}:(NavigationPoint[])observation.ApproachCandidates.Clone();
                                remembered.UseApprovedApproaches=observation.UseApprovedApproaches;
                            }else {
                                // A last seen landmark cannot carry an actionable Story step or
                                // reveal an interaction point the player has never observed.
                                remembered.StoryMilestones=new int[0];remembered.NextFields=new int[0];
                                remembered.StoryAction=null;remembered.StoryVerb=null;remembered.StoryStepMilestone=Int32.MinValue;
                                remembered.UseApprovedApproaches=false;
                                remembered.DestinationApproaches=null;
                            }
                        }
                        else if(observation.Exit!=null){remembered.ApproachCandidates=observation.Exit.ApproachEnabled?observation.Exit.ApproachCandidates??new[]{observation.Exit.Approach}:new NavigationPoint[0];availability=observation.Exit.Availability;}
                        remembered.Availability=(String.IsNullOrEmpty(availability)?"":availability+"; ")+"last seen, outside the current view";
                        string known;if(identities!=null && identities.TryGetLabel(observation.Id,observation.LabelIdentity,out known))remembered.Label=known;
                        else if(exit)remembered.Label=ExitSide(observation);
                        result.Targets.Add(remembered);if(exit)exits[remembered]=observation;
                    }else {
                        _lastSeen.Remove(observation.Id);
                        if(exit && observation.ContainsJump)result.Diagnostics.Add("An entrance candidate has unresolved presentation evidence.");
                    }
                    continue;
                }
                var category=Category(observation);string label=exit?ExitSide(observation):Label(observation,category);
                string observed; if (identities != null && identities.TryGetLabel(observation.Id,observation.LabelIdentity, out observed)) label = observed;
                result.Targets.Add(new NavigationTarget { Id = observation.Id, Category = category, Label = label, Position = observation.Position, Generation = context.Generation, Evidence = exit ? observation.Exit.Evidence : observation.ShowsIcon?VisibilityEvidence.NativeInteractionIcon:VisibilityEvidence.RenderedActor, IsInteractive = exit || observation.HasInteraction, Availability = exit ? observation.Exit.Availability : observation.HasInteraction ? null : "interaction unavailable", ApproachCandidates = exit?(observation.Exit.ApproachEnabled?observation.Exit.ApproachCandidates??new[]{observation.Exit.Approach}:new NavigationPoint[0]):observation.ApproachCandidates??new[]{observation.Position},InteractionUid=observation.InteractionUid,InteractionRegion=observation.InteractionRegion==null?null:(NavigationPoint[])observation.InteractionRegion.Clone(),UseViewingPoint=observation.UseViewingPoint });
                var added=result.Targets[result.Targets.Count-1];added.UseApprovedApproaches=observation.UseApprovedApproaches;if(!exit && !observation.HasInteraction)added.UseViewingPoint=true;added.StoryMilestones=observation.StoryMilestones==null?null:(int[])observation.StoryMilestones.Clone();added.NextFields=observation.NextFields==null?null:(int[])observation.NextFields.Clone();added.StoryAction=observation.StoryAction;added.StoryStepMilestone=observation.StoryStepMilestone;added.StoryVerb=observation.StoryVerb;
                added.DestinationApproaches=FieldExitDestinations.Copy(observation.DestinationApproaches);if(exit)exits[added]=observation;
                _lastSeen[observation.Id]=new ObservedTarget{Target=Copy(added),PresentationIdentity=observation.NativePresentationIdentity,Scope=observation.LabelIdentity};
            }
            NameExits(context,result,exits);
            var removed=new List<TargetIdentity>();foreach(var seen in _lastSeen)if(!active.Contains(seen.Key))removed.Add(seen.Key);foreach(var id in removed)_lastSeen.Remove(id);
            return result;
        }
        // An exit is named only from its current native destination proof (FieldExitRoutes); otherwise it
        // keeps an observed journey label or the side of the screen where it was presented.
        private string ExitSide(FieldTargetObservation observation)
        {string side;return !String.IsNullOrEmpty(observation.VisibleLabel)?observation.VisibleLabel:_exitSides.TryGetValue(observation.Id,out side)?side:"Exit";}
        private static void NameExits(NavigationContext context,CatalogResult result,Dictionary<NavigationTarget,FieldTargetObservation> exits)
        {
            if(exits.Count==0)return;
            var ordered=new List<NavigationTarget>();foreach(var target in result.Targets)if(exits.ContainsKey(target))ordered.Add(target);
            var named=FieldExitNames.Apply(ordered,target=>FieldExitRoutes.For(context,target.Id,exits[target].NativePresentationIdentity),target=>target.Label,context.CurrentLocation);
            var replaced=new List<NavigationTarget>();int next=0;
            foreach(var target in result.Targets) {
                if(!exits.ContainsKey(target)){replaced.Add(target);continue;}
                while(next<named.Count && named[next].PhysicalIdentity.Equals(target.PhysicalIdentity))replaced.Add(named[next++]);
            }
            result.Targets.Clear();foreach(var target in replaced)result.Targets.Add(target);
        }
        internal static NavigationTarget Copy(NavigationTarget source)
        {return new NavigationTarget{Id=source.Id,Category=source.Category,Label=source.Label,Availability=source.Availability,Position=source.Position,Evidence=source.Evidence,Generation=source.Generation,IsInteractive=source.IsInteractive,NativeLocationId=source.NativeLocationId,ApproachCandidates=source.ApproachCandidates==null?null:(NavigationPoint[])source.ApproachCandidates.Clone(),InteractionUid=source.InteractionUid,InteractionRegion=source.InteractionRegion==null?null:(NavigationPoint[])source.InteractionRegion.Clone(),UseViewingPoint=source.UseViewingPoint,UseApprovedApproaches=source.UseApprovedApproaches,PhysicalId=source.PhysicalId,StoryMilestones=source.StoryMilestones==null?null:(int[])source.StoryMilestones.Clone(),NextFields=source.NextFields==null?null:(int[])source.NextFields.Clone(),DestinationApproaches=FieldExitDestinations.Copy(source.DestinationApproaches),StoryAction=source.StoryAction,StoryStepMilestone=source.StoryStepMilestone,StoryVerb=source.StoryVerb};}
        private static bool SameRegion(NavigationPoint[] a,NavigationPoint[] b)
        {if(a==null || b==null)return a==b;if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(!SamePoint(a[i],b[i]))return false;return true;}
        private static bool SamePoint(NavigationPoint a,NavigationPoint b){return a.X==b.X && a.Y==b.Y && a.Z==b.Z;}
        private static NavigationCategory Category(FieldTargetObservation observation){return observation.Id.Kind==TargetKind.FieldExit?NavigationCategory.Exits:observation.IsPerson?NavigationCategory.People:observation.IsItem?NavigationCategory.Items:NavigationCategory.Objects;}
        // A unique description is spoken as it is ("Innkeeper", "Lever"). Targets sharing a description in
        // the same category receive occurrence numbers in the order they were first presented in this map
        // ("Chest 1", "Chest 2"); a number, once given, is kept for the map. An unknown description keeps
        // the honest generic word for its category.
        private string Label(FieldTargetObservation observation,NavigationCategory category)
        {
            string description;var occurrences=Register(observation,category,out description);
            string spoken=(int)category+"\n"+description+"\n"+observation.Id.NativeId;
            // A target already spoken alone keeps its plain name; later twins are numbered from 2.
            if(occurrences.Count==1){_plain.Add(spoken);return description;}
            return _plain.Contains(spoken)?description:description+" "+(occurrences.IndexOf(observation.Id)+1);
        }
        private void Register(FieldTargetObservation observation,NavigationCategory category){string description;Register(observation,category,out description);}
        private List<TargetIdentity> Register(FieldTargetObservation observation,NavigationCategory category,out string description)
        {
            description=string.IsNullOrEmpty(observation.VisibleLabel)?category==NavigationCategory.Exits?"Exit":category==NavigationCategory.People?"Person":category==NavigationCategory.Items?"Item":"Object":observation.VisibleLabel;
            string key=(int)category+"\n"+description;List<TargetIdentity> occurrences;
            if(!_occurrences.TryGetValue(key,out occurrences)){occurrences=new List<TargetIdentity>();_occurrences[key]=occurrences;}
            if(!occurrences.Contains(observation.Id))occurrences.Add(observation.Id);
            return occurrences;
        }
    }
}
