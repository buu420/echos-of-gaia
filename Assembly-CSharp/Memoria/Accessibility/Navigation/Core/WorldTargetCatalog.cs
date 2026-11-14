using System;
using System.Collections.Generic;

namespace Memoria.Accessibility.Navigation
{
    public sealed class WorldTargetObservation
    {
        public TargetIdentity Id;
        public string Label, Availability;
        public NavigationPoint Position;
        public NavigationPoint[] ApproachCandidates;
        public int[] WorldEntranceTags;
        public float WorldEntranceRadius;
        public bool Available, Visible, Marked, Observed;
        public int NativeIndex, NativeLocationId = -1;
    }

    public sealed class WorldTargetCatalog
    {
        public static CatalogResult Filter(NavigationContext context, CatalogResult catalog, WorldGeometrySnapshot geometry, WorldReachabilityIndex index)
        {
            if (geometry != null && geometry.Rules != null && geometry.Rules.Flying)
                return catalog;

            var result = new CatalogResult();
            foreach (var diagnostic in catalog.Diagnostics)
                result.Diagnostics.Add(diagnostic);

            if (geometry == null || !geometry.Complete || geometry.Rules == null || index == null || !index.Matches(geometry))
            {
                result.UnavailableReason = "World destinations are being checked.";
                return result;
            }
            var start = geometry.Probe(context.Position, geometry.Rules.CurrentTerrainId, new PrivateWorldCache(geometry.NativeCache));
            var components = index.ComponentsAt(geometry, start, WorldGeometrySnapshot.Cell(context.Position), true);
            if (components.Count == 0)
            {
                result.UnavailableReason = "The current world position could not be matched to a reachable area.";
                return result;
            }

            foreach (var target in catalog.Targets)
            {
                var approaches = new List<NavigationPoint>();
                bool hasEntrance = target.WorldEntranceTags != null;
                if (hasEntrance)
                {
                    foreach (int tag in target.WorldEntranceTags)
                        foreach (var candidate in index.EntranceApproaches(geometry, target, tag))
                            if (index.CanReach(geometry, geometry.Find(candidate.Surface), candidate.Cell, components))
                                approaches.Add(candidate.Point);
                }
                else
                {
                    foreach (var point in target.ApproachCandidates ?? new[] { target.Position })
                    {
                        if (!point.IsFinite)
                            continue;
                        var goal = geometry.Probe(new NavigationPoint(point.X, Math.Max(context.Position.Y, point.Y) + 400, point.Z),
                            start.TerrainId, new PrivateWorldCache(geometry.NativeCache));
                        if (index.CanReach(geometry, goal, WorldGeometrySnapshot.Cell(point), components))
                            approaches.Add(point);
                        else if (goal == null && target.Id.Kind == TargetKind.WorldLocation)
                        {
                            // A marker can sit on a building outside the ground probe.
                            foreach (var candidate in index.MarkerApproaches(geometry, target))
                            {
                                var approach = geometry.Probe(candidate, start.TerrainId, new PrivateWorldCache(geometry.NativeCache));
                                if (index.CanReach(geometry, approach, WorldGeometrySnapshot.Cell(candidate), components))
                                    approaches.Add(candidate);
                            }
                        }
                    }
                }
                if (approaches.Count == 0)
                    continue;

                var position = target.Position;
                if (hasEntrance || target.Id.Kind == TargetKind.RegionPoint)
                {
                    position = approaches[0];
                    if (!hasEntrance)
                        foreach (var point in approaches)
                            if (WorldCoordinates.Distance(context.Position, point) < WorldCoordinates.Distance(context.Position, position))
                                position = point;
                }

                // Sort entrance candidates by distance from the landmark, with
                // a stable target position while the player moves around.
                result.Targets.Add(new NavigationTarget
                {
                    Id = target.Id, Category = target.Category, Label = target.Label, Position = position,
                    Evidence = target.Evidence, Generation = target.Generation, NativeLocationId = target.NativeLocationId,
                    IsInteractive = target.IsInteractive, Availability = target.Availability,
                    UseApprovedApproaches = hasEntrance, ApproachCandidates = approaches.ToArray()
                });
            }
            return result;
        }

        public CatalogResult Build(NavigationContext context, IList<WorldTargetObservation> observations, RegionObservations regions)
        {
            var result = new CatalogResult();
            foreach (var item in observations)
            {
                bool vehicle = item.Id.Kind == TargetKind.Vehicle;
                if (item.Id.MapKey != context.LogicalMapKey || !item.Position.IsFinite || string.IsNullOrEmpty(item.Label) ||
                    !item.Available || (!item.Visible && !item.Marked && !item.Observed) || (vehicle && item.Id.NativeId == context.PlayerIdentity) ||
                    result.Contains(item.Id))
                    continue;

                result.Targets.Add(new NavigationTarget
                {
                    Id = item.Id, Label = item.Label, Availability = item.Availability, Position = item.Position,
                    Category = vehicle ? NavigationCategory.Vehicles : NavigationCategory.Towns,
                    Evidence = vehicle && item.Visible ? VisibilityEvidence.RenderedActor : item.Marked ? VisibilityEvidence.NativeMarker : VisibilityEvidence.ObservedWorldLandmark,
                    Generation = context.Generation, NativeLocationId = item.NativeLocationId,
                    WorldEntranceTags = item.WorldEntranceTags,
                    WorldEntranceRadius = item.WorldEntranceRadius,
                    ApproachCandidates = item.ApproachCandidates == null ? new[] { item.Position } : (NavigationPoint[])item.ApproachCandidates.Clone()
                });
            }
            if (regions != null)
                foreach (var target in regions.BuildTargets(context))
                    result.Targets.Add(target);
            return result;
        }
    }
}
