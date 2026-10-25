using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class WorldTargetObservation
    {
        public TargetIdentity Id; public string Label; public NavigationPoint Position; public NavigationPoint[] ApproachCandidates; public bool Available,Visible,Marked;public int NativeIndex,NativeLocationId=-1;
    }
    public sealed class WorldTargetCatalog
    {
        public static CatalogResult Filter(NavigationContext context,CatalogResult catalog,WorldGeometrySnapshot geometry,WorldReachabilityIndex index)
        {
            var result=new CatalogResult();foreach(var diagnostic in catalog.Diagnostics)result.Diagnostics.Add(diagnostic);
            if(geometry==null || !geometry.Complete || geometry.Rules==null) {result.UnavailableReason="World destinations are being checked.";return result;}
            // Flight is not restricted to a connected patch of ground. Native
            // availability/visibility was already checked by Build above.
            if(geometry.Rules.Flying)return catalog;
            if(index==null || !index.Matches(geometry)){result.UnavailableReason="World destinations are being checked.";return result;}
            var start=geometry.Probe(context.Position,geometry.Rules.CurrentTerrainId,new PrivateWorldCache(geometry.NativeCache));
            var components=index.ComponentsAt(geometry,start,WorldGeometrySnapshot.Cell(context.Position),true);
            if(components.Count==0){result.UnavailableReason="The current world position could not be matched to a reachable area.";return result;}
            foreach(var target in catalog.Targets) {
                var approaches=new List<NavigationPoint>();
                foreach(var point in target.ApproachCandidates??new[]{target.Position}) {
                    if(!point.IsFinite)continue;
                    var goal=geometry.Probe(new NavigationPoint(point.X,Math.Max(context.Position.Y,point.Y)+400,point.Z),start.TerrainId,new PrivateWorldCache(geometry.NativeCache));
                    if(index.CanReach(geometry,goal,WorldGeometrySnapshot.Cell(point),components))approaches.Add(point);
                    else if(goal==null && target.Id.Kind==TargetKind.WorldLocation) {
                        // A map marker can sit on a non-walkable building, as
                        // Chocobo's Forest does. Use actual ground in its native
                        // arrival cell, rather than requiring the marker's roof.
                        foreach(var candidate in index.MarkerApproaches(geometry,target)) {
                            var approach=geometry.Probe(candidate,start.TerrainId,new PrivateWorldCache(geometry.NativeCache));
                            if(index.CanReach(geometry,approach,WorldGeometrySnapshot.Cell(candidate),components))approaches.Add(candidate);
                        }
                    }
                }
                if(approaches.Count==0)continue;
                var position=target.Position;
                if(target.Id.Kind==TargetKind.RegionPoint){position=approaches[0];foreach(var point in approaches)if(WorldCoordinates.Distance(context.Position,point)<WorldCoordinates.Distance(context.Position,position))position=point;}
                result.Targets.Add(new NavigationTarget{Id=target.Id,Category=target.Category,Label=target.Label,Position=position,Evidence=target.Evidence,Generation=target.Generation,NativeLocationId=target.NativeLocationId,IsInteractive=target.IsInteractive,Availability=target.Availability,ApproachCandidates=approaches.ToArray()});
            }return result;
        }
        public CatalogResult Build(NavigationContext context,IList<WorldTargetObservation> observations,RegionObservations regions)
        {
            var result=new CatalogResult();
            foreach(var item in observations) {
                bool vehicle=item.Id.Kind==TargetKind.Vehicle;
                if(item.Id.MapKey!=context.LogicalMapKey || !item.Position.IsFinite || string.IsNullOrEmpty(item.Label) || !item.Available || vehicle && !item.Visible && !item.Marked || vehicle && item.Id.NativeId==context.PlayerIdentity)continue;
                if(result.Contains(item.Id))continue;
                result.Targets.Add(new NavigationTarget {Id=item.Id,Label=item.Label,Position=item.Position,Category=vehicle?NavigationCategory.Vehicles:NavigationCategory.Towns,Evidence=vehicle&&item.Visible?VisibilityEvidence.RenderedActor:VisibilityEvidence.NativeMarker,Generation=context.Generation,NativeLocationId=item.NativeLocationId,ApproachCandidates=item.ApproachCandidates==null?new[]{item.Position}:(NavigationPoint[])item.ApproachCandidates.Clone()});
            }
            if(regions!=null)foreach(var target in regions.BuildTargets(context))result.Targets.Add(target);
            return result;
        }
    }
}
