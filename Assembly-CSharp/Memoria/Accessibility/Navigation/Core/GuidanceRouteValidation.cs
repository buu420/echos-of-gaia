namespace Memoria.Accessibility.Navigation
{
    public static class GuidanceRouteValidation
    {
        public static bool Matches(RoutePlan route,NavigationContext context,NavigationTarget selected)
        {
            return route!=null && route.Success && context!=null && selected!=null && route.Generation==context.Generation && selected.Generation==context.Generation && route.Transport.Equals(context.Transport) && route.TargetId.Equals(selected.Id) && route.PhysicalIdentity.Equals(selected.PhysicalIdentity) && selected.Id.MapKey==context.LogicalMapKey;
        }
        public static bool WorldLeg(WorldGeometrySnapshot geometry,NavigationContext context,NavigationPoint waypoint,NavigationTarget target,NavigationPoint approach)
        {
            if(geometry==null || !geometry.Complete || geometry.Rules==null || context==null || target==null)return false;
            var cache=new PrivateWorldCache(geometry.NativeCache);
            var start=geometry.Probe(context.Position,geometry.Rules.CurrentTerrainId,cache);
            if(start==null)return false;
            int goalCell=target.PhysicalIdentity.Kind==TargetKind.WorldLocation?WorldGeometrySnapshot.Cell(approach):-1;
            WorldSurface end;
            return geometry.Segment(context.Position,waypoint,start,cache,WorldGeometrySnapshot.Cell(context.Position),goalCell,out end);
        }
        public static MovementOutcome Check(RoutePlan route, NavigationTarget planned, NavigationTarget current, string fingerprint, float tolerance, bool world, bool segmentClear)
        {
            if (route == null || planned == null || current == null) return MovementOutcome.None;
            if (route.GeometryFingerprint != fingerprint) return MovementOutcome.RouteChanged;
            if (!world && FieldApproachChanged(planned,current)) return MovementOutcome.TargetMoved;
            float distance = world ? WorldCoordinates.Distance(current.Position, planned.Position) : NavigationPoint.Distance(current.Position, planned.Position);
            if (distance > tolerance) return MovementOutcome.TargetMoved;
            if (route.Success && !segmentClear) return MovementOutcome.RouteChanged;
            return MovementOutcome.None;
        }
        public static bool FieldApproachChanged(NavigationTarget planned,NavigationTarget current)
        {
            if(planned==null || current==null)return true;
            if(!planned.PhysicalIdentity.Equals(current.PhysicalIdentity) || planned.InteractionUid!=current.InteractionUid || planned.UseViewingPoint!=current.UseViewingPoint)return true;
            var a=planned.InteractionRegion;var b=current.InteractionRegion;
            if(a==null || b==null)return a!=b;
            if(a.Length!=b.Length)return true;
            for(int i=0;i<a.Length;i++)if(!a[i].IsFinite || !b[i].IsFinite || NavigationPoint.Distance(a[i],b[i])>0.01f)return true;
            return false;
        }
    }
}
