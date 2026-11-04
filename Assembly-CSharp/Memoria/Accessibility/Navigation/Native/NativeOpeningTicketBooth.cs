namespace Memoria.Accessibility.Navigation
{
    public sealed class OpeningTicketBoothPresentation
    {
        public long Generation;public int Camera;public string Background,Walkmesh;
        public bool ArtPresented,RegionOnScreen,ShowsIcon;public float Height;
        public NavigationPoint[] Region,Approaches;
    }
    public static class NativeOpeningTicketBooth
    {
        public static FieldTargetObservation Read(NavigationContext context,EventEngine engine,Actor player,Quad region,OpeningTicketBoothPresentation presentation)
        {
            if(context==null || !context.Foreground || !context.CanControl || context.LogicalMapKey!=OpeningTicketBoothEvidence.Map || FF9StateSystem.Common.FF9.fldMapNo!=103 ||
                engine==null || engine.gMode!=1 || player==null || !System.Object.ReferenceEquals(engine.GetControlChar(),player) || player.fieldMapActorController==null || !player.fieldMapActorController.isPlayer ||
                region==null || region.uid!=28 || !System.Object.ReferenceEquals(engine.GetObjUID(region.uid),region) || presentation==null || presentation.Generation!=context.Generation ||
                System.Single.IsNaN(presentation.Height) || System.Single.IsInfinity(presentation.Height))return null;
            var director=engine.GetObjUID(0) as Obj;if(director==null || director.sid!=0 || director.ebData==null || region.ebData==null || player.ebData==null)return null;
            var state=new OpeningTicketBoothState{Context=context,Camera=presentation.Camera,PlayerUid=player.uid,PlayerSid=player.sid,PlayerModel=player.model,
                PlayerHash=NativeFieldTargets.Fingerprint(player.ebData),DirectorHash=NativeFieldTargets.Fingerprint(director.ebData),RegionSid=region.sid,RegionHash=NativeFieldTargets.Fingerprint(region.ebData),
                BackgroundHash=presentation.Background,WalkmeshHash=presentation.Walkmesh,ArtPresented=presentation.ArtPresented,RegionOnScreen=presentation.RegionOnScreen,
                // The exact original entry includes its tag3 listener. Listing
                // its approach does not depend on the player's current facing.
                NativeInteractionAllowed=region.level>1,Region=presentation.Region};
            if(!OpeningTicketBoothEvidence.Available(state))return null;
            float x=0,z=0;foreach(var p in presentation.Region){x+=p.X;z+=p.Z;}
            var observation=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,region.uid),
                NativePresentationIdentity=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(region),LabelIdentity=OpeningTicketBoothEvidence.Script+":"+OpeningTicketBoothEvidence.Background,
                Position=new NavigationPoint(x/presentation.Region.Length,presentation.Height,z/presentation.Region.Length),Visible=true,ShowsIcon=presentation.ShowsIcon,HasInteraction=true,
                InteractionUid=region.uid,InteractionRegion=(NavigationPoint[])presentation.Region.Clone(),UseApprovedApproaches=true,ApproachCandidates=presentation.Approaches==null?new NavigationPoint[0]:(NavigationPoint[])presentation.Approaches.Clone()};
            OpeningTicketBoothEvidence.Annotate(context,observation);return observation;
        }
    }
}
