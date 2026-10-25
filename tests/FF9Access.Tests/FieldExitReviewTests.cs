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
    }
}
