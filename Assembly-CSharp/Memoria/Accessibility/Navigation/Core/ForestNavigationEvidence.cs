namespace Memoria.Accessibility.Navigation
{
    public static class ForestNavigationEvidence
    {
        public const string Map="field:2950:FBG_N14_CHCB_MAP300_CH_FST_0";
        public const string MeneScript="78091e48362e89ad4367f18fb1fda1bf7d7e431b5957b91d1b9b3ca4f16e82d8";
        public const string TalkRegionScript="be5e19ef03b606497d3b6c6f8ed6f4b4b5c5bf70b7dd9cf6acc92bb3812c4a9d";
        public const string ObjectScript="e144fca084a41f30fbec9337bec9796efcff057e2fe13de49b5613fc630e2184";
        public static bool MeneRegion(string map,int actorSid,int model,string actorScript,int regionSid,string regionScript,NavigationPoint observed)
        {
            return map==Map && actorSid==9 && model==220 && actorScript==MeneScript && regionSid==19 && regionScript==TalkRegionScript && observed.IsFinite && observed.Y>500;
        }
        public static bool NoninteractiveObject(string map,int actorSid,int model,string script,bool interactive)
        {
            return map==Map && actorSid==11 && model==241 && script==ObjectScript && !interactive;
        }
    }
}
