using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class StoryRegionApproachTests
    {
        public static void StoryWalkInCanBeSelectedBeforeAnyNearbyIcon()
        {
            var points=new[]{TestFixtures.P(2,2),TestFixtures.P(5,5)};
            var approaches=FieldStoryRegionApproaches.Read(true,true,points,p=>true,p=>true,TestFixtures.P(0,0));
            TestAssert.Equal(2,approaches.Length);TestAssert.Equal(2f,approaches[0].X);
        }
        public static void NoCurrentStoryProofOrNativeTriggerPermissionMeansNoApproach()
        {
            var points=new[]{TestFixtures.P(2,2)};
            TestAssert.Equal(0,FieldStoryRegionApproaches.Read(false,true,points,p=>true,p=>true,TestFixtures.P(0,0)).Length);
            TestAssert.Equal(0,FieldStoryRegionApproaches.Read(true,false,points,p=>true,p=>true,TestFixtures.P(0,0)).Length);
        }
        public static void HiddenAndShadowedRegionsCannotSupplyAGoal()
        {
            var points=new[]{TestFixtures.P(2,2),TestFixtures.P(5,5),new NavigationPoint(float.NaN,0,0)};
            TestAssert.Equal(0,FieldStoryRegionApproaches.Read(true,true,points,p=>true,p=>false,TestFixtures.P(0,0)).Length);
            TestAssert.Equal(0,FieldStoryRegionApproaches.Read(true,true,points,p=>false,p=>true,TestFixtures.P(0,0)).Length);
            var result=FieldStoryRegionApproaches.Read(true,true,points,p=>p.X==5,p=>true,TestFixtures.P(0,0));
            TestAssert.Equal(1,result.Length);TestAssert.Equal(5f,result[0].X);
        }
    }
}
