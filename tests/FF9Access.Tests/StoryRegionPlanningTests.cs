using System.Linq;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class StoryRegionPlanningTests
    {
        public static void FilteredConfirmRegionSurvivesCatalogStoryAndPlanner()
        {
            var context=TestFixtures.Context();context.Position=TestFixtures.P(30,60);
            var geometry=new FieldGeometrySnapshot{Generation=1,Fingerprint="region",Radius=2,ActiveTriangle=0,
                Triangles=new[]{new FieldTriangle{Id=0,Vertices=new[]{TestFixtures.P(0,0),TestFixtures.P(1000,0),TestFixtures.P(0,1000)}}}};
            var region=new[]{TestFixtures.P(20,20),TestFixtures.P(180,20),TestFixtures.P(180,180),TestFixtures.P(20,180)};
            var raw=FieldRegionApproaches.Interaction(region,geometry);
            var approved=FieldStoryRegionApproaches.Read(true,true,raw,p=>p.X>120,p=>p.Z<120,context.Position);
            TestAssert.True(approved.Length>0 && approved.Length<raw.Length);
            var observation=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,15),Visible=true,HasInteraction=true,
                Position=approved[0],InteractionUid=15,InteractionRegion=region,ApproachCandidates=approved,UseApprovedApproaches=true};
            var catalog=new FieldTargetCatalog().Build(context,new[]{observation},null);
            var target=StoryTargetCatalog.Wrap(context,1155,catalog.Targets[0],"Climb the ladder");
            var prepared=FieldTargetApproaches.Prepare(context,target,geometry);
            TestAssert.Equal(approved.Length,prepared.Length);
            TestAssert.True(prepared.All(p=>p.X>120 && p.Z<120));
            var route=new FieldRoutePlanner().Plan(context,target,geometry);
            TestAssert.True(route.Success && approved.Contains(route.ApproachPoint));
        }
        public static void ApprovedApproachCopiesRetainTheirRestriction()
        {
            var context=TestFixtures.Context();var reader=new FieldTargetCatalog();
            var observation=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,15),Visible=true,HasInteraction=true,
                NativePresentationIdentity=7,Position=TestFixtures.P(5,5),InteractionUid=15,InteractionRegion=new[]{TestFixtures.P(0,0),TestFixtures.P(10,0),TestFixtures.P(0,10)},
                ApproachCandidates=new[]{TestFixtures.P(5,5)},UseApprovedApproaches=true};
            reader.Build(context,new[]{observation},null);observation.Visible=false;observation.OutOfView=true;
            var remembered=reader.Build(context,new[]{observation},null).Targets[0];
            var wrapped=StoryTargetCatalog.Wrap(context,1155,remembered,"Ladder");
            TestAssert.True(remembered.UseApprovedApproaches && wrapped.UseApprovedApproaches);
            remembered.ApproachCandidates[0]=TestFixtures.P(900,900);
            TestAssert.Equal(TestFixtures.P(5,5),wrapped.ApproachCandidates[0]);
        }
        public static void ChangedApprovedSetInvalidatesRoutesButReorderingDoesNot()
        {
            var id=new TargetIdentity(TargetKind.FieldActor,"field:100",15);
            var planned=new NavigationTarget{Id=id,UseApprovedApproaches=true,ApproachCandidates=new[]{TestFixtures.P(5,5),TestFixtures.P(8,8)}};
            var current=new NavigationTarget{Id=id,UseApprovedApproaches=true,ApproachCandidates=new[]{TestFixtures.P(8,8),TestFixtures.P(5,5)}};
            TestAssert.False(GuidanceRouteValidation.FieldApproachChanged(planned,current));
            current.ApproachCandidates=new[]{TestFixtures.P(5,5)};
            TestAssert.True(GuidanceRouteValidation.FieldApproachChanged(planned,current));
            planned.UseApprovedApproaches=current.UseApprovedApproaches=false;
            TestAssert.False(GuidanceRouteValidation.FieldApproachChanged(planned,current));
        }
    }
}
