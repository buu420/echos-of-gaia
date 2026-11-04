using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    // Reviewed Alexandria Castle checkpoints after the play (installed US scripts; audit
    // research/2026-11-04-play-steiner-navigation/claude). The counter stays 1155 through the play
    // (61-63), the sword fight (64) and the guard-room scene (150 G6[2]=325 -> phase 0) where Blank
    // (150 entry 3, phase 10) writes 1190. It then stays 1190 until 55 entry 10 writes 1400:
    // - EVT_ALEX1_AC_H2F (153) phase 6 is Zidane's free state. Blank's phase-1..5 lines point at the
    //   royal box "right above these stairs"; Zidane's tag-1 phase-6 case (offset 859) runs
    //   EXPR {OBJ(255).1 -450 GT}; JMP_IFN +3; JMP 2964, i.e. it continues to the hooded-girl scene
    //   (phase 17) only once -round(pos.y) <= -450. Regions 26/27 (no jump) on the doorways start the
    //   "Look out!" detours, phases 7-16, which return to phase 6. Exit 28 leads back to 150.
    // - EVT_ALEX1_AC_GUARD (150) phase 11 is either Zidane backtracking (G6[2]==5: entry 2, exit 18 -> 153)
    //   or Steiner (default: entry 23, exit 19 -> 153).
    // - Zorn/Thorn and the queen (154 phase 1, 153 phase 100+, 151 phase 1) and Steiner's arrival
    //   (153 phases 40-48) are scenes. Steiner (model 5489) then searches the castle; the only chain that
    //   reaches the 1400 writer is 153 -> 154 -> 158 -> 159 -> 160 -> 162..166 -> 55, all ordinary exits.
    public static class OpeningCastleStory
    {
        // The 1400 writer is 55's scene, reached only from Steiner's tower-top scene (166 entry 6 MAPJUMP 55).
        // The route goal is 166: the shared transit graph also contains scripted shortcuts (e.g. 153 -> 204)
        // that a field-55 goal would prefer over the castle's actual exits.
        public const int PhaseByte=24,PlayFloor=1155,CastleFloor=1190,CastleCeiling=1400,SearchMilestone=1400,SearchGoalField=166;
        public const int HallField=153,HallZidaneUid=3,HallStairsPhase=6,StairsMinimumY=450,GuardField=150,GuardZidanePhase=11;
        public const string HallMap="field:153:FBG_N02_ALXC_MAP041A_AC_H2F_1";
        public const string HallDirector="a9df60cc3d768298474fa93a6c1bc283285332b34897cd0ec95be8c4b748a493";
        public const string HallPhases="474f8cc14bb43335dcf9b46e3df415a57f90080cd842e6c20d044854028f36ec";
        public const string HallZidane="aa0452c6d45bab20aac744146d789380fe1cb31debe05a983645a584873d5057";
        public const string HallBlank="d332c2a6d59a5633ec7c27df72bf53cfe01016f78f75219f8fcd87d4c8a99228";
        public const string HallZorn="6c820fc717e5f4c1c576bcabb918de3194a9c0fa3dd3d533c5d5d38722719c99";
        public const string EntranceZorn="b1bcc69dfb329e180f944c1b62294093dbfedf2ce23f691807d0a5051accc361";
        public const string SeatZorn="9a5df55fcc15210d901df7f690ceba69a5b44b28ae4e23e891d87d1d0b1298fe";
        public const string GuardDirector="44e0dec90f53fb5a63e775ddc67cfeab9b4c915af8cd6442ec8669ad25f598f8";
        public const string GuardZidane="77480841d07a28e1bad7607e87b86c91155c2df1e41b2bbadca40719d779431f";
        // Wording restates what is shown: Blank's "royal box ... above these stairs" and the queen's order.
        public const string StairsAction="Climb the stairs to the royal box";
        private const string StairsInstruction=StairsAction+".";
        private const string ReturnInstruction="Return to the stairs to the royal box.";
        private const string SearchInstruction="Search the castle for Princess Garnet.";
        private static readonly int[] PlayFields={61,62,63,64,150};
        private static readonly int[] CastleFields={64,150,151,153,154,155,156,157,158,159,160,161,162,163,164,165,166,167};
        // Field -> (live Steiner script id, its entry hash) in the Steiner search state.
        private static readonly Dictionary<int,KeyValuePair<int,string>> _steiner=new Dictionary<int,KeyValuePair<int,string>>{
            {150,P(23,"099b5c5a71f7f8b5d45af33e8652c18b3a001dbcba323e4f6a84f9292c78b861")},
            {151,P(12,"f8e4a8d120221955d6978a71cea77cca5e27704c52656fbb3dce2d74ba0e15b8")},
            {153,P(32,"584ee24ebd9e90ed64823109e6c272c3fe7bb0099a225b11a235c1ff459eba6a")},
            {154,P(15,"1c926b11cbcc3b77888517591ce1f99648288c37b83c6554c1bed89de21996b1")},
            {155,P(13,"22ed29fae99a141e89dd6befaae2ab89a68ded0b4a16adfb8d4aec621b7223f6")},
            {156,P(12,"7c5784d46771c208d9b0d13168321d6d7cd64d87d9e8e1999e15b6fdd67017fd")},
            {157,P(11,"577e22d0ee6ae80cd2baf27e08886e499b9d27034ebd50239a15b601c0f8fc60")},
            {158,P(6,"8273232d3f64ad1fe12d904ac5118e124bbb5f0dcf532abaf1f00733432378dd")},
            {159,P(16,"31e8b3d9575df9b381c0f3bad37a748c5e1966ae95e3abd5059a35ce6f77b96a")},
            {160,P(9,"d34e4fc8e4055be539392885781d2b2ceb4fc2045a95358c96b925468ebda62e")},
            {161,P(6,"366ced4a4c65541f427c6daf9b42755e1cbd1fe6cba5b3a5e6af0c48cc721fb2")},
            {162,P(7,"09afef8c6229b8cc7e2992eac1cf4c2fd62f4ad14b087731eb3c2e1d04cba434")},
            {163,P(7,"099375f93a9cc3d58bf61791ce7560398437e0abb25f363a324aa3ad0cef9269")},
            {164,P(7,"904163a638de7ba145242765416a0ed39d9d7706ff1b163534b1311c89409589")},
            {165,P(7,"c170def030e24025979ee7e79419430f390718585d680d422d4a827d8e7d6eb4")},
            {166,P(6,"d38b30e5fab8e88920893575fbf53711b4f3f65b1b0a5077b164080ea11d672b")},
            {167,P(9,"ff7d0d44d586db2e6111eb447ca32f7bb6559b8390555321e71cc61898aaf618")},
            {64,P(16,"1c0c778074808282f2681a754a1e14b729f2b4a785a3bf4db6833c8a7a9affce")},
        };
        public static IDictionary<int,KeyValuePair<int,string>> SteinerEntries{get{return new Dictionary<int,KeyValuePair<int,string>>(_steiner);}}
        // Ordered: the first verified match wins (StoryTargetCatalog.Checkpoint).
        public static StoryCheckpoint[] Checkpoints{get{
            var result=new List<StoryCheckpoint>{
                // The play, the sword fight and the guard-room scene are shown scenes.
                Info(PlayFloor,CastleFloor,PlayFields,null,-1,PlayFloor),
                // Zidane: the stairs gate, his backtrack, and every other Zidane phase (scenes and detours).
                new StoryCheckpoint{Floor=CastleFloor,Ceiling=CastleCeiling,ScopeFields=new[]{HallField},PlayerIdentity=HallZidaneUid,MapByte=PhaseByte,MapValue=HallStairsPhase,RequiredScripts=HallScripts(),
                    Stage=new StoryStage{Milestone=CastleFloor,GoalFields=new[]{HallField},Goal="Alexandria Castle",Instruction=StairsInstruction}},
                Info(CastleFloor,CastleCeiling,new[]{HallField},HallScripts(),-1,CastleFloor,HallZidaneUid),
                new StoryCheckpoint{Floor=CastleFloor,Ceiling=CastleCeiling,ScopeFields=new[]{GuardField},PlayerIdentity=2,MapByte=PhaseByte,MapValue=GuardZidanePhase,RequiredScripts=Scripts(0,GuardDirector,2,GuardZidane),
                    Stage=new StoryStage{Milestone=CastleFloor,GoalFields=new[]{HallField},Goal="Alexandria Castle",Instruction=ReturnInstruction}},
                Info(CastleFloor,CastleCeiling,new[]{GuardField},Scripts(0,GuardDirector,2,GuardZidane),-1,CastleFloor,2),
                // Zorn/Thorn and the queen.
                Info(CastleFloor,CastleCeiling,new[]{HallField},Scripts(18,HallZorn),-1,CastleFloor),
                Info(CastleFloor,CastleCeiling,new[]{154},Scripts(2,EntranceZorn),-1,CastleFloor),
                Info(CastleFloor,CastleCeiling,new[]{151},Scripts(4,SeatZorn),-1,CastleFloor),
            };
            // Steiner's arrival scene in 153.
            for(int phase=40;phase<=48;phase++)result.Add(Info(CastleFloor,CastleCeiling,new[]{HallField},Scripts(_steiner[HallField].Key,_steiner[HallField].Value),phase,SearchMilestone));
            // Steiner's search: the current field's live Steiner entry; the route uses live exits only.
            foreach(var steiner in _steiner)
                result.Add(new StoryCheckpoint{Floor=CastleFloor,Ceiling=CastleCeiling,ScopeFields=new[]{steiner.Key},PlayerIdentity=steiner.Value.Key,RequiredScripts=Scripts(steiner.Value.Key,steiner.Value.Value),
                    Stage=new StoryStage{Milestone=SearchMilestone,GoalFields=new[]{SearchGoalField},TransitFields=(int[])CastleFields.Clone(),KeepGoalLabel=true,Goal="Alexandria Castle",Instruction=SearchInstruction}});
            // Changed or unavailable scripts keep the current scene without future advice.
            result.Add(Info(CastleFloor,CastleCeiling,CastleFields,null,-1,CastleFloor));
            return result.ToArray();
        }}
        // The current native stairs wait in 153 phase 6. Callers supply floor points; the goal is not a position.
        public static OpeningPositionGoal CurrentGoal(NavigationContext context,int? phase,Func<int,string> scripts)
        {
            if(context==null || !context.CanControl || context.LogicalMapKey!=HallMap || context.PlayerIdentity!=HallZidaneUid ||
               context.StoryProgress<CastleFloor || context.StoryProgress>=CastleCeiling || phase!=HallStairsPhase ||
               !OpeningStoryCheckpoints.Verified(scripts,HallScripts()))return null;
            return new OpeningPositionGoal{Field=HallField,Map=HallMap,Phase=HallStairsPhase,Milestone=CastleFloor,Action=StairsAction,Axis=OpeningGoalAxis.Y,Minimum=StairsMinimumY};
        }
        // "field:<n>:<scene>" -> n, or -1.
        public static int FieldNumber(string logicalMapKey)
        {
            int field;if(logicalMapKey==null || !logicalMapKey.StartsWith("field:"))return -1;
            var parts=logicalMapKey.Split(':');return parts.Length>1 && Int32.TryParse(parts[1],out field)?field:-1;
        }
        private static Dictionary<int,string> HallScripts(){return Scripts(0,HallDirector,2,HallPhases,3,HallZidane,7,HallBlank);}
        private static StoryCheckpoint Info(int floor,int ceiling,int[] fields,Dictionary<int,string> scripts,int phase,int milestone,int player=-1)
        {
            return new StoryCheckpoint{Floor=floor,Ceiling=ceiling,ScopeFields=fields,PlayerIdentity=player,MapByte=phase>=0?PhaseByte:-1,MapValue=phase,RequiredScripts=scripts,
                Stage=new StoryStage{Milestone=milestone,Goal="Alexandria Castle",Instruction="",InformationOnly=true}};
        }
        private static Dictionary<int,string> Scripts(params object[] pairs)
        {var result=new Dictionary<int,string>();for(int i=0;i+1<pairs.Length;i+=2)result[(int)pairs[i]]=(string)pairs[i+1];return result;}
        private static KeyValuePair<int,string> P(int sid,string hash){return new KeyValuePair<int,string>(sid,hash);}
    }
}
