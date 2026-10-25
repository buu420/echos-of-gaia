namespace Memoria.Accessibility.Navigation
{
    // The native opening hint identifies the candle on this visible table.
    // These hashes bind its four interaction regions to the reviewed room art
    // and floor; no hidden-item spots or future-story positions are supplied.
    public static class OpeningCandleEvidence
    {
        public const string Map="field:50:FBG_N00_TSHP_MAP002_TH_CGR_1";
        public const string Background="f6186eff5303f444e1ab354134460ba66a9623fec54ec40820cc47e0281d8b9b";
        public const string Walkmesh="8241c6b62b300aabd88e778cbd48625bd10d38a50e5f38b63e4b22b66377f6ce";
        // A conservative approach subset leaves over twenty native units at
        // the nearest edge; the owned follower can stop within four units.
        // Readiness still tests the original, full native regions.
        public static NavigationPoint[] ApproachArea{get{
            var area=OpeningStoryCheckpoints.CandleArea;
            for(int i=0;i<area.Length;i++)area[i]=new NavigationPoint(area[i].X*.9f,area[i].Y,350+(area[i].Z-350)*.9f);
            return area;
        }}
        public static bool Available(NavigationContext context,int? phase,int playerSid,string playerHash,string directorHash,string background,string walkmesh)
        {
            return context!=null && context.CanControl && context.LogicalMapKey==Map && context.PlayerIdentity==17 && playerSid==17 &&
                context.StoryProgress>=OpeningStoryCheckpoints.Floor && context.StoryProgress<OpeningStoryCheckpoints.Ceiling &&
                phase==OpeningStoryCheckpoints.CandlePhase && playerHash==OpeningStoryCheckpoints.CargoZidane && directorHash==OpeningStoryCheckpoints.CargoDirector &&
                background==Background && walkmesh==Walkmesh;
        }
        public static bool MatchesRegion(int sid,string hash,NavigationPoint[] polygon)
        {
            foreach(var region in OpeningStoryCheckpoints.CandleRegions)if(region.Sid==sid && region.Hash==hash){
                if(polygon==null || polygon.Length!=region.Polygon.Length)return false;
                for(int i=0;i<polygon.Length;i++)if(!polygon[i].IsFinite || polygon[i].X!=region.Polygon[i].X || polygon[i].Z!=region.Polygon[i].Z)return false;
                return true;
            }
            return false;
        }
    }
}
