using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public enum NavigationCategory { Exits, People, Objects, Towns, Regions, Vehicles, Story, Items }
    public enum VisibilityEvidence { None, RenderedActor, NativeMarker, NativeInteractionIcon, ReviewedBackground, VisibleWalkmeshEdge, ObservedRegion, NativeDoorTile, ObservedFieldLandmark }
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
        public string UnobservedOutcome; // Intentionally never read by the catalog.
        public NavigationPoint[] ApproachCandidates;
        public int InteractionUid = -1;
        public NavigationPoint[] InteractionRegion;
        public bool UseViewingPoint;
        public bool IsItem;
        public int[] StoryMilestones,NextFields;
        public string StoryAction;
        public ExitPresentation Exit;
    }
    public sealed class NavigationTarget
    {
        public TargetIdentity Id; public NavigationCategory Category; public string Label, Availability;
        public NavigationPoint Position; public VisibilityEvidence Evidence; public long Generation;
        public NavigationPoint[] ApproachCandidates; public bool IsInteractive; public int NativeLocationId = -1;
        public int InteractionUid = -1;
        public NavigationPoint[] InteractionRegion;
        public bool UseViewingPoint;
        public TargetIdentity? PhysicalId;
        public TargetIdentity PhysicalIdentity {get{return PhysicalId??Id;}}
        public int[] StoryMilestones,NextFields;
        public string StoryAction;
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
