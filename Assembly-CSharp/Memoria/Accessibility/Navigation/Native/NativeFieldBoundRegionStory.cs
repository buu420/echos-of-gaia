namespace Memoria.Accessibility.Navigation
{
    public static partial class NativeFieldTargets
    {
        // A reviewed bound region is not listed separately (NativeFieldActorInteractions.IsBoundRegion), so
        // the Story evidence of its own Confirm handler is read exactly as for a listed region and joins the
        // person it talks to. Reads only; the region's evidence never replaces the person's own.
        internal static void AnnotateBoundRegionStory(NavigationContext context,EventEngine engine,Quad region,FieldTargetObservation person)
        {
            if(context==null || engine==null || region==null || region.ebData==null || person==null)return;
            var own=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,region.uid),NativePresentationIdentity=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(region),
                Visible=person.Visible,ShowsIcon=person.ShowsIcon,OutOfView=person.OutOfView,HasInteraction=true,LabelIdentity=ExitLabelSignature(region)};
            Annotate(context,engine,region,own,EventEngine.tagTalk);
            FieldBoundStory.Merge(own,person);
        }
    }
}
