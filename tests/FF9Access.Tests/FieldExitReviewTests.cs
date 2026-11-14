using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldExitReviewTests
    {
        private static NavigationPoint[] Region(){return new[]{new NavigationPoint(65,0,-157),new NavigationPoint(-153,0,-157),new NavigationPoint(-153,0,-300),new NavigationPoint(90,0,-300)};}
        public static void ReviewedOpeningRequiresExactSceneScriptsAssetsCameraAndLivePolygon()
        {
            var proof=new FieldExitSurfaceReview{Scene="field:1608:scene",ScriptId=6,Camera=0,Script="script",Background="background",Walkmesh="walkmesh",Region=Region()};
            TestAssert.True(proof.Matches(proof.Scene,6,0,"script","background","walkmesh",Region()));
            TestAssert.False(proof.Matches("field:1707:scene",6,0,"script","background","walkmesh",Region()));
            TestAssert.False(proof.Matches(proof.Scene,7,0,"script","background","walkmesh",Region()));
            TestAssert.False(proof.Matches(proof.Scene,6,1,"script","background","walkmesh",Region()));
            TestAssert.False(proof.Matches(proof.Scene,6,0,"changed","background","walkmesh",Region()));
            TestAssert.False(proof.Matches(proof.Scene,6,0,"script","changed","walkmesh",Region()));
            TestAssert.False(proof.Matches(proof.Scene,6,0,"script","background","changed",Region()));
            var moved=Region();moved[0]=new NavigationPoint(65,0,-257);TestAssert.False(proof.Matches(proof.Scene,6,0,"script","background","walkmesh",moved));
            TestAssert.False(proof.Matches(proof.Scene,6,0,"script","background","walkmesh",null));
        }
        public static void CroppedSouthGatePassageFollowsTheNativeCameraPan()
        {
            // Installed South Gate Rest Area: camera 0 is 560 by 304.
            var camera=new FieldCameraPanorama{HalfWidth=160,HalfHeight=112,MinX=160,MaxX=400,MinY=112,MaxY=192};
            var visibleTrail=new NavigationPoint(-8.5f,-178.7f,1200f);
            var croppedTrigger=new NavigationPoint(-8.5f,-208.3f,1200f);
            TestAssert.False(FieldScenePresentation.Passage(true,true,camera,croppedTrigger));
            TestAssert.True(FieldScenePresentation.ReviewedWalkOut(true,false,true,true,camera,visibleTrail));
            TestAssert.False(FieldScenePresentation.ReviewedWalkOut(true,false,false,true,camera,visibleTrail));
            TestAssert.False(FieldScenePresentation.ReviewedWalkOut(true,false,true,false,camera,visibleTrail));
            TestAssert.False(FieldScenePresentation.ReviewedWalkOut(false,true,true,true,camera,visibleTrail));
            TestAssert.False(FieldScenePresentation.ReviewedWalkOut(true,false,true,true,camera,croppedTrigger));
        }
    }
}
