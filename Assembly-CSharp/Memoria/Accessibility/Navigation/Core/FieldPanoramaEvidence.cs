namespace Memoria.Accessibility.Navigation
{
    // Reviewed open passages on the original opening street panoramas.
    // The original shape and assets identify artwork outside the current crop;
    // current script guards, pan limits, renderer and geometry remain required.
    public static class FieldPanoramaEvidence
    {
        private sealed class Review
        {
            public string Map,Script,Background,Walkmesh;public int Sid;public NavigationPoint[] Region;
        }
        private static readonly Review[] Reviews={
            new Review{Map="field:100:FBG_N01_ALXT_MAP016_AT_MSA_0",Sid=15,Script="3609b2b9c4126ac757303a6640ad0aa437611192a05a936a7333c3a606e590d2",Background="b6adc95e7018b8ae7847355c816eed4dea460a1babf4b47c7e6b0d274f4d5bd9",Walkmesh="622833579ae3196d7738e91cab073880343c159c7c77b725349395b7f576f628",Region=new[]{P(-365,7708),P(295,7708),P(355,6538),P(-371,6532)}},
            new Review{Map="field:101:FBG_N01_ALXT_MAP017_AT_MSB_0",Sid=16,Script="b430bae16c7b8beb02a559780f764a6cb679c51df7df3107276200aeb7a395c5",Background="e07c8b7f4b9c555da608bed04a0d12cd674081a04b46a14a21a32a800d3123ba",Walkmesh="7172bf359a0d83d2cfa96354b860091ceb0a1fbf2ff340ecc4e6b860895ae003",Region=new[]{P(-3500,-600),P(-3500,-200),P(-2621,444),P(-2254,-1444)}},
            new Review{Map="field:102:FBG_N01_ALXT_MAP018_AT_MSC_0",Sid=8,Script="b78fb967814e3ea1bbdc7d48989bc24eab0b0003e6c14a19fc7cbbe3be551f63",Background="c8c9a80bb3118c290cd2b7573f83ea06b48ddd7b46d55618745ac6dc2028e20e",Walkmesh="b06b34bf3a369622e5d21cbef37c8a42655058c49e811defe031c69777d24ac0",Region=new[]{P(858,6987),P(468,6987),P(404,5142),P(1134,5019)}}
        };
        private static NavigationPoint P(int x,int z){return new NavigationPoint(x,0,z);}
        private static Review Find(string map,int sid,int camera){if(camera!=0)return null;foreach(var review in Reviews)if(review.Map==map && review.Sid==sid)return review;return null;}
        public static bool IsReviewed(string map,int sid,int camera){return Find(map,sid,camera)!=null;}
        public static bool Matches(string map,int sid,int camera,string script,string background,string walkmesh,NavigationPoint[] region)
        {
            var review=Find(map,sid,camera);if(review==null || review.Script!=script || review.Background!=background || review.Walkmesh!=walkmesh || region==null || region.Length!=review.Region.Length)return false;
            for(int i=0;i<region.Length;i++)if(!region[i].IsFinite || !region[i].Equals(review.Region[i]))return false;
            return true;
        }
    }
}
