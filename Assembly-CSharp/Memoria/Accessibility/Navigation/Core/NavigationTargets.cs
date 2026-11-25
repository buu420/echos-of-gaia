using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public enum NavigationCategory { Exits, People, Objects, Towns, Regions, Vehicles, Story, Items }
    public enum VisibilityEvidence { None, RenderedActor, NativeMarker, NativeInteractionIcon, ReviewedBackground, VisibleWalkmeshEdge, ObservedRegion, NativeDoorTile, ObservedFieldLandmark, ObservedWorldLandmark }
    public sealed class ExitPresentation
    {
        public bool Visible, ApproachEnabled; public NavigationPoint Approach; public string Cue, SourceFingerprint, Availability;
        public NavigationPoint[] ApproachCandidates;
        public VisibilityEvidence Evidence = VisibilityEvidence.ReviewedBackground;
    }
    public sealed class FieldTargetObservation
    {
        public TargetIdentity Id; public NavigationPoint Position; public bool Visible, HasInteraction, IsPerson, ShowsIcon, ContainsJump;
        public bool OutOfView; // Native presentation remains enabled, but the camera has scrolled away.
        public int NativePresentationIdentity;
        public string VisibleLabel,LabelIdentity;
        public string UnobservedOutcome; // Intentionally never read by the catalog.
        public NavigationPoint[] ApproachCandidates;
        // Current native selection/presentation already filtered these points.
        public bool UseApprovedApproaches;
        public FieldActorInteractionRange ActorInteractionRange;
        public int InteractionUid = -1;
        public NavigationPoint[] InteractionRegion;
        public bool UseViewingPoint;
        public bool IsItem;
        public int[] StoryMilestones,NextFields;
        public Dictionary<int,NavigationPoint[]> DestinationApproaches;
        public string StoryAction;
        // A current native Story step owned by this entry (StoryStepCatalog). StoryStepMilestone is the
        // counter its chain writes, -1 when the step advances the current scene without a counter write.
        public int StoryStepMilestone=Int32.MinValue;public string StoryVerb;
        public ExitPresentation Exit;
    }
    public sealed class NavigationTarget
    {
        public TargetIdentity Id; public NavigationCategory Category; public string Label, Availability;
        public NavigationPoint Position; public VisibilityEvidence Evidence; public long Generation;
        public NavigationPoint[] ApproachCandidates; public bool IsInteractive; public int NativeLocationId = -1;
        // Null for a map pin with no analyzed walking event. An empty array means
        // the entrance was found but no event branch accepts the current state.
        public int[] WorldEntranceTags;
        public float WorldEntranceRadius;
        public bool UseApprovedApproaches;
        public FieldActorInteractionRange ActorInteractionRange;
        public int InteractionUid = -1;
        public NavigationPoint[] InteractionRegion;
        public bool UseViewingPoint;
        public bool RequireInwardApproach;
        public bool SelectDestinationInMenu;
        public TargetIdentity? PhysicalId;
        internal FieldExitContinuation ExitContinuation;
        public TargetIdentity PhysicalIdentity {get{return PhysicalId??Id;}}
        public int[] StoryMilestones,NextFields;
        public Dictionary<int,NavigationPoint[]> DestinationApproaches;
        public string StoryAction;
        public int StoryStepMilestone=Int32.MinValue;public string StoryVerb;
    }
    public sealed class CatalogResult
    {
        public string UnavailableReason;
        public readonly IList<NavigationTarget> Targets = new List<NavigationTarget>();
        public readonly IList<string> Diagnostics = new List<string>();
        public bool Contains(TargetIdentity id) { return Find(id) != null; }
        public NavigationTarget Find(TargetIdentity id) { foreach (var target in Targets) if (target.Id.Equals(id)) return target; return null; }
    }
}
