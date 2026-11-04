using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldScenePresentationTests
    {
        private static FieldCameraPanorama Street(){return new FieldCameraPanorama{HalfWidth=160,HalfHeight=112,MinX=160,MaxX=160,MinY=112,MaxY=480};}
        public static void CurrentVisibleActorOnTheSamePanoramaNeedsNoPriorEncounter()
        {
            TestAssert.True(FieldScenePresentation.Actor(true,true,false,Street(),new NavigationPoint(0,-300,1000)));
            var actor=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,"field:100",7),Position=TestFixtures.P(1,1),IsPerson=true,Visible=FieldScenePresentation.Actor(true,true,false,Street(),new NavigationPoint(0,-300,1000))};
            TestAssert.Equal(1,new FieldTargetCatalog().Build(TestFixtures.Context(),new[]{actor},null).Targets.Count);
        }
        public static void HiddenStagedAndBehindCameraActorsAreNotPresented()
        {
            var p=new NavigationPoint(0,-300,1000);
            TestAssert.False(FieldScenePresentation.Actor(false,true,false,Street(),p));
            TestAssert.False(FieldScenePresentation.Actor(true,false,false,Street(),p));
            TestAssert.False(FieldScenePresentation.Actor(true,true,false,Street(),new NavigationPoint(10000,10000,1000)));
            TestAssert.False(FieldScenePresentation.Actor(true,true,true,Street(),new NavigationPoint(0,0,-1)));
            TestAssert.False(FieldScenePresentation.Actor(true,true,false,null,p));
        }
        public static void CurrentViewportStillWorksForAFixedCamera()
        {
            TestAssert.True(FieldScenePresentation.Actor(true,true,true,null,new NavigationPoint(0,0,1000)));
        }
        public static void NativeAlleySteepleTriggerIsJustBeyondTheRenderedCropEdge()
        {
            // Installed field105, sid11, native floor/region intersection at body radius80.
            // The nearest safe trigger sample projects eight native pixels below the camera pan limit.
            var camera=new FieldCameraPanorama{HalfWidth=160,HalfHeight=112,MinX=160,MaxX=160,MinY=112,MaxY=208};
            var sample=new NavigationPoint(107.46576f,-216.14897f,2057.8086f);
            TestAssert.False(FieldPanoramaPresentation.Contains(camera,sample));
            TestAssert.True(FieldScenePresentation.Passage(true,true,camera,sample));
            TestAssert.False(FieldScenePresentation.Passage(false,true,camera,sample));
            TestAssert.False(FieldScenePresentation.Passage(true,false,camera,sample));
            TestAssert.False(FieldScenePresentation.Passage(true,true,camera,new NavigationPoint(sample.X,-230,sample.Z)));
            TestAssert.False(FieldScenePresentation.Passage(true,true,camera,new NavigationPoint(sample.X,sample.Y,-1)));
            TestAssert.False(FieldScenePresentation.Passage(true,true,camera,new NavigationPoint(float.NaN,sample.Y,sample.Z)));
        }
    }
}
