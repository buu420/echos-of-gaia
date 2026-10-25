using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class FieldTargetCatalog
    {
        private readonly Dictionary<TargetIdentity, string> _labels = new Dictionary<TargetIdentity, string>();
        private readonly Dictionary<TargetIdentity, NavigationCategory> _labelCategories=new Dictionary<TargetIdentity, NavigationCategory>();
        private readonly Dictionary<NavigationCategory, int> _counts = new Dictionary<NavigationCategory, int>();
        private sealed class ObservedTarget {public NavigationTarget Target;public int PresentationIdentity;}
        private readonly Dictionary<TargetIdentity, ObservedTarget> _lastSeen = new Dictionary<TargetIdentity, ObservedTarget>();
        private string _map;
        private long _generation;private int _player;private string _save;
        public CatalogResult Build(NavigationContext context, IList<FieldTargetObservation> observations, ObservedIdentityStore identities)
        {
            var result = new CatalogResult();
            if (_map != context.LogicalMapKey) { _labels.Clear();_labelCategories.Clear(); _counts.Clear(); _lastSeen.Clear(); _map = context.LogicalMapKey; }
            if (_generation != context.Generation || _player != context.PlayerIdentity || _save != context.SaveIdentity) {
                _lastSeen.Clear();_generation=context.Generation;_player=context.PlayerIdentity;_save=context.SaveIdentity;
            }
            var active=new HashSet<TargetIdentity>();
            var ordered = new List<FieldTargetObservation>(observations); ordered.Sort((a,b) => a.Id.NativeId.CompareTo(b.Id.NativeId));
            foreach (var observation in ordered) {
                if (observation.Id.MapKey != context.LogicalMapKey || !observation.Position.IsFinite) {_lastSeen.Remove(observation.Id);continue;}
                active.Add(observation.Id);
                bool exit = observation.Id.Kind == TargetKind.FieldExit;
                bool presented=exit?observation.Exit!=null && observation.Exit.Visible:observation.Visible || observation.ShowsIcon;
                if(!presented) {
                    ObservedTarget seen;
                    if(observation.OutOfView && _lastSeen.TryGetValue(observation.Id,out seen) && seen.PresentationIdentity==observation.NativePresentationIdentity) {
                        var remembered=Copy(seen.Target);remembered.Generation=context.Generation;remembered.Evidence=VisibilityEvidence.ObservedFieldLandmark;
                        // Preserve only the observed position. Story outcomes
                        // must use today's native guards, even outside the view.
                        if(observation.StoryMilestones!=null)remembered.StoryMilestones=(int[])observation.StoryMilestones.Clone();
                        if(observation.NextFields!=null)remembered.NextFields=(int[])observation.NextFields.Clone();
                        remembered.StoryAction=observation.StoryAction;
                        string availability=seen.Target.Availability;
                        if(!exit){
                            remembered.Category=Category(observation);remembered.Label=Label(observation,remembered.Category);
                            bool sameInteraction=seen.Target.InteractionUid==observation.InteractionUid && SameRegion(seen.Target.InteractionRegion,observation.InteractionRegion);
                            remembered.IsInteractive=observation.HasInteraction && sameInteraction;remembered.UseViewingPoint=observation.UseViewingPoint || !remembered.IsInteractive;
                            availability=!observation.HasInteraction?"interaction unavailable":sameInteraction?null:"interaction approach changed outside the current view";
                        }
                        else if(observation.Exit!=null){remembered.ApproachCandidates=observation.Exit.ApproachEnabled?observation.Exit.ApproachCandidates??new[]{observation.Exit.Approach}:new NavigationPoint[0];availability=observation.Exit.Availability;}
                        remembered.Availability=(String.IsNullOrEmpty(availability)?"":availability+"; ")+"last seen, outside the current view";
                        string known;if(identities!=null && identities.TryGetLabel(observation.Id,out known))remembered.Label=known;
                        result.Targets.Add(remembered);
                    }else {
                        _lastSeen.Remove(observation.Id);
                        if(exit && observation.ContainsJump)result.Diagnostics.Add("An entrance candidate has unresolved presentation evidence.");
                    }
                    continue;
                }
                var category=Category(observation);string label=Label(observation,category);
                string observed; if (identities != null && identities.TryGetLabel(observation.Id, out observed)) label = observed;
                result.Targets.Add(new NavigationTarget { Id = observation.Id, Category = category, Label = label, Position = observation.Position, Generation = context.Generation, Evidence = exit ? observation.Exit.Evidence : observation.ShowsIcon?VisibilityEvidence.NativeInteractionIcon:VisibilityEvidence.RenderedActor, IsInteractive = exit || observation.HasInteraction, Availability = exit ? observation.Exit.Availability : observation.HasInteraction ? null : "interaction unavailable", ApproachCandidates = exit?(observation.Exit.ApproachEnabled?observation.Exit.ApproachCandidates??new[]{observation.Exit.Approach}:new NavigationPoint[0]):observation.ApproachCandidates??new[]{observation.Position},InteractionUid=observation.InteractionUid,InteractionRegion=observation.InteractionRegion==null?null:(NavigationPoint[])observation.InteractionRegion.Clone(),UseViewingPoint=observation.UseViewingPoint });
                var added=result.Targets[result.Targets.Count-1];if(!exit && !observation.HasInteraction)added.UseViewingPoint=true;added.StoryMilestones=observation.StoryMilestones==null?null:(int[])observation.StoryMilestones.Clone();added.NextFields=observation.NextFields==null?null:(int[])observation.NextFields.Clone();added.StoryAction=observation.StoryAction;
                _lastSeen[observation.Id]=new ObservedTarget{Target=Copy(added),PresentationIdentity=observation.NativePresentationIdentity};
            }
            var removed=new List<TargetIdentity>();foreach(var seen in _lastSeen)if(!active.Contains(seen.Key))removed.Add(seen.Key);foreach(var id in removed)_lastSeen.Remove(id);
            return result;
        }
        private static NavigationTarget Copy(NavigationTarget source)
        {return new NavigationTarget{Id=source.Id,Category=source.Category,Label=source.Label,Availability=source.Availability,Position=source.Position,Evidence=source.Evidence,Generation=source.Generation,IsInteractive=source.IsInteractive,NativeLocationId=source.NativeLocationId,ApproachCandidates=source.ApproachCandidates==null?null:(NavigationPoint[])source.ApproachCandidates.Clone(),InteractionUid=source.InteractionUid,InteractionRegion=source.InteractionRegion==null?null:(NavigationPoint[])source.InteractionRegion.Clone(),UseViewingPoint=source.UseViewingPoint,PhysicalId=source.PhysicalId,StoryMilestones=source.StoryMilestones==null?null:(int[])source.StoryMilestones.Clone(),NextFields=source.NextFields==null?null:(int[])source.NextFields.Clone(),StoryAction=source.StoryAction};}
        private static bool SameRegion(NavigationPoint[] a,NavigationPoint[] b)
        {if(a==null || b==null)return a==b;if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(!a[i].Equals(b[i]))return false;return true;}
        private static NavigationCategory Category(FieldTargetObservation observation){return observation.Id.Kind==TargetKind.FieldExit?NavigationCategory.Exits:observation.IsPerson?NavigationCategory.People:observation.IsItem?NavigationCategory.Items:NavigationCategory.Objects;}
        private string Label(FieldTargetObservation observation,NavigationCategory category)
        {
            string label;NavigationCategory previous;
            if(!_labels.TryGetValue(observation.Id,out label) || !_labelCategories.TryGetValue(observation.Id,out previous) || previous!=category) {
                int count;_counts.TryGetValue(category,out count);_counts[category]=count+1;
                label=(category==NavigationCategory.Exits?"Exit ":category==NavigationCategory.People?"Person ":category==NavigationCategory.Items?"Item ":"Object ")+Letters(count);_labels[observation.Id]=label;_labelCategories[observation.Id]=category;
            }return label;
        }
        private static string Letters(int value) { string result = ""; do { result = (char)('A' + value % 26) + result; value = value / 26 - 1; } while (value >= 0); return result; }
    }
}
