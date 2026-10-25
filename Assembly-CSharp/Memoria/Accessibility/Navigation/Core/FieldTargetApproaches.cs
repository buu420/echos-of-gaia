namespace Memoria.Accessibility.Navigation
{
    // One preparation rule for walking and proximity sounds. Actor origins may
    // be raised; interaction regions and walkmesh support own the approach.
    public static class FieldTargetApproaches
    {
        public static NavigationPoint[] Prepare(NavigationContext context,NavigationTarget target,FieldGeometrySnapshot geometry)
        {
            if(context==null || target==null || geometry==null || geometry.Generation!=context.Generation || target.Generation!=context.Generation)return new NavigationPoint[0];
            var candidates=target.ApproachCandidates??new[]{target.Position};
            if(target.IsInteractive && target.InteractionUid>=0)return FieldRegionApproaches.Interaction(target.InteractionRegion,geometry);
            if(target.UseViewingPoint && !target.IsInteractive && target.PhysicalIdentity.Kind==TargetKind.FieldActor)return FieldRegionApproaches.Viewing(context,target.Position,geometry);
            if(target.IsInteractive && target.PhysicalIdentity.Kind==TargetKind.FieldActor)return FieldActorApproaches.OnSurfaces(candidates,target.Position,geometry);
            return candidates;
        }
    }
}
