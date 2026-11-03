using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldPanoramaEvidenceTests
    {
        private static readonly NavigationPoint[] Region={new NavigationPoint(-365,0,7708),new NavigationPoint(295,0,7708),new NavigationPoint(355,0,6538),new NavigationPoint(-371,0,6532)};
        private const string Map="field:100:FBG_N01_ALXT_MAP016_AT_MSA_0",Script="3609b2b9c4126ac757303a6640ad0aa437611192a05a936a7333c3a606e590d2",
            Background="b6adc95e7018b8ae7847355c816eed4dea460a1babf4b47c7e6b0d274f4d5bd9",Walkmesh="622833579ae3196d7738e91cab073880343c159c7c77b725349395b7f576f628";
        public static void OriginalStreetPanoramaMatchesItsReviewedPassage()
        {TestAssert.True(FieldPanoramaEvidence.Matches(Map,15,0,Script,Background,Walkmesh,Region));}
        public static void ReplacingBackgroundWithIdenticalCameraAndRendererBoundsIsRejected()
        {TestAssert.False(FieldPanoramaEvidence.Matches(Map,15,0,Script,"different background",Walkmesh,Region));}
        public static void ChangedSceneCameraEntryMeshOrRegionRejectsOffscreenExpansion()
        {
            TestAssert.False(FieldPanoramaEvidence.Matches(Map+"changed",15,0,Script,Background,Walkmesh,Region));
            TestAssert.False(FieldPanoramaEvidence.Matches(Map,16,0,Script,Background,Walkmesh,Region));
            TestAssert.False(FieldPanoramaEvidence.Matches(Map,15,1,Script,Background,Walkmesh,Region));
            TestAssert.False(FieldPanoramaEvidence.Matches(Map,15,0,"changed",Background,Walkmesh,Region));
            TestAssert.False(FieldPanoramaEvidence.Matches(Map,15,0,Script,Background,"changed",Region));
            var moved=(NavigationPoint[])Region.Clone();moved[0]=new NavigationPoint(moved[0].X+1,moved[0].Y,moved[0].Z);
            TestAssert.False(FieldPanoramaEvidence.Matches(Map,15,0,Script,Background,Walkmesh,moved));
            TestAssert.False(FieldPanoramaEvidence.Matches(Map,15,0,Script,Background,Walkmesh,null));
        }
        public static void FadingOrNonfiniteMaterialsAreNotOpaqueEvidence()
        {
            TestAssert.True(FieldPanoramaPresentation.IsOpaque(1));
            foreach(float alpha in new[]{0f,.5f,.99f,float.NaN,float.PositiveInfinity})TestAssert.False(FieldPanoramaPresentation.IsOpaque(alpha));
        }
    }
}
