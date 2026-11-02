using System;
using System.Text;
using Memoria.Accessibility.Navigation;
internal static class OpeningBoothTests
{
    private sealed class Fixture
    {
        internal readonly EventEngine Engine=UnityEngine.Object.FindObjectOfType<EventEngine>();
        internal readonly NavigationContext Context=new NavigationContext{LogicalMapKey=OpeningTicketBoothEvidence.Map,Generation=1,PlayerIdentity=30,Foreground=true,CanControl=true,StoryProgress=1000};
        internal readonly Quad Region=new Quad{uid=28,sid=28,ebData=Encoding.UTF8.GetBytes(OpeningTicketBoothEvidence.Script),level=7};
        internal readonly OpeningTicketBoothPresentation Presentation=new OpeningTicketBoothPresentation{Generation=1,Camera=0,Background=OpeningTicketBoothEvidence.Background,Walkmesh=OpeningTicketBoothEvidence.Walkmesh,ArtPresented=true,RegionOnScreen=true,Region=OpeningTicketBoothEvidence.Region,Approaches=new[]{new NavigationPoint(25,0,-555.5f)}};
        internal Fixture()
        {Engine.gMode=1;FF9StateSystem.Common.FF9.fldMapNo=103;Engine.Objects.Clear();Engine.Controlled.uid=30;Engine.Controlled.sid=30;Engine.Controlled.model=8;Engine.Controlled.ebData=Encoding.UTF8.GetBytes(OpeningTicketBoothEvidence.Player);Engine.Objects[0]=new Obj{uid=0,sid=0,ebData=Encoding.UTF8.GetBytes(OpeningTicketBoothEvidence.Director)};Engine.Objects[28]=Region;}
        internal FieldTargetObservation Read(){return NativeOpeningTicketBooth.Read(Context,Engine,Engine.Controlled,Region,Presentation);}
    }
    private static void Require(bool value,string reason){if(!value)throw new Exception(reason);}
    public static void NativeBoothArtProvidesItsCurrentLocalRegionBeforeAnyBubble()
    {var f=new Fixture();var target=f.Read();Require(target!=null && !target.ShowsIcon && target.Visible,"presented booth required prior discovery");Require(target.Id.MapKey==f.Context.LogicalMapKey && target.Id.NativeId==28 && target.InteractionUid==28,"booth acquired a foreign physical identity");Require(target.StoryMilestones.Length==1 && target.StoryMilestones[0]==1150,"booth did not provide its current Story interaction");}
    public static void NativeBoothRejectsStaleSceneObjectAndPlayerEvidence()
    {var f=new Fixture();f.Presentation.Generation++;Require(f.Read()==null,"stale presentation accepted");f=new Fixture();f.Engine.Objects[28]=new Quad{uid=28,sid=28,ebData=f.Region.ebData};Require(f.Read()==null,"old region reference accepted");f=new Fixture();f.Engine.Objects[0].ebData=Encoding.UTF8.GetBytes("changed");Require(f.Read()==null,"changed director accepted");f=new Fixture();f.Engine.Controlled.model=98;Require(f.Read()==null,"different player accepted");f=new Fixture();f.Context.CanControl=false;Require(f.Read()==null,"blocked frame provided a walking target");}
}
