using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class OpeningCandleTests
    {
        private static NavigationContext Context(){return new NavigationContext{LogicalMapKey=OpeningCandleEvidence.Map,Generation=9,PlayerIdentity=17,CanControl=true,StoryProgress=1000};}
        private static bool Available(NavigationContext context,int? phase=1,int sid=17,string hash=null,string director=null,string background=null,string walkmesh=null)
        {return OpeningCandleEvidence.Available(context,phase,sid,hash??OpeningStoryCheckpoints.CargoZidane,director??OpeningStoryCheckpoints.CargoDirector,background??OpeningCandleEvidence.Background,walkmesh??OpeningCandleEvidence.Walkmesh);}
        public static void ShownOpeningCandleRequiresCurrentScenePhaseAndPlayerControl()
        {
            var context=Context();TestAssert.True(Available(context));
            context.CanControl=false;TestAssert.False(Available(context));context.CanControl=true;
            TestAssert.False(Available(context,null));TestAssert.False(Available(context,0));TestAssert.False(Available(context,2));
            context.StoryProgress=1150;TestAssert.False(Available(context));context.StoryProgress=999;TestAssert.False(Available(context));
            context.StoryProgress=1000;context.LogicalMapKey="field:52:another-scene";TestAssert.False(Available(context));
        }
        public static void ChangedScriptsOrBackgroundCannotSupplyTheCandlePosition()
        {
            var context=Context();TestAssert.False(Available(context,1,18));
            TestAssert.False(Available(context,hash:"changed"));TestAssert.False(Available(context,director:"changed"));
            TestAssert.False(Available(context,background:"changed"));TestAssert.False(Available(context,walkmesh:"changed"));
        }
        public static void RegionsMustMatchTheActualCandlePolygonsAndNeverHiddenItemSpots()
        {
            foreach(var region in OpeningStoryCheckpoints.CandleRegions){
                TestAssert.True(OpeningCandleEvidence.MatchesRegion(region.Sid,region.Hash,region.Polygon));
                var changed=(NavigationPoint[])region.Polygon.Clone();changed[0]=new NavigationPoint(10,0,350);
                TestAssert.False(OpeningCandleEvidence.MatchesRegion(region.Sid,region.Hash,changed));
                TestAssert.False(OpeningCandleEvidence.MatchesRegion(8,region.Hash,region.Polygon));
            }
        }
    }
}
