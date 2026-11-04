using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace Memoria.Accessibility.Navigation
{
    public sealed class OpeningCandleRegion
    {
        public readonly int Sid;public readonly string Hash;public readonly NavigationPoint[] Polygon;
        public OpeningCandleRegion(int sid,string hash,params NavigationPoint[] polygon){Sid=sid;Hash=hash;Polygon=polygon;}
    }
    public sealed class OpeningStoryAnnotation
    {
        public readonly int Milestone;public readonly string Action;
        public OpeningStoryAnnotation(int milestone,string action){Milestone=milestone;Action=action;}
    }
    // Reviewed opening checkpoints at the scenario-counter floor 1000 (installed US scripts).
    // EVT_ALEX1_TS_CARGO_0 (field 50): Zidane entry 17 tag 1 writes counter 1000 at 0x06fe/0x070c just
    // before UCON; the counter stays 1000 through the candle, Tantalus scene, Baku battle (ENCOUNT 336),
    // the meeting (field 52, MAPJUMP 100) and Vivi's walk until field 104 writes 1150. Field 50 phase is
    // MAP5[24] (map byte 24): director entry 3 raises it to 1 after the opening walk; Zidane's tag-1
    // switch sends phase 1 to the candle check (0x0756: Confirm or Special while MAP5[38]==1).
    // Regions 4-7 set MAP5[38]=1 on touch and present the native "?" bubble (FICON 0) in that phase.
    public static class OpeningStoryCheckpoints
    {
        public const int CargoField=50,MeetingField=52,Floor=1000,Ceiling=1150,PhaseByte=24,CandlePhase=1,CandleMilestone=1000;
        public const string CargoDirector="45500d5d487fbc8c5b934a1649af0398cb2c1403d4f73c8bcea46ece42f194e4";
        public const string CargoZidane="254774de354b33e584684b6a3deeea3bd01c212a47ee828d443160f9b75fd001";
        public const string MeetingBaku="73e55b0c8bd53902b50150147d1c6e2afc3bc6af011fbd8372b535bf6a6dbbc7";
        // Native hint (field 50 text 36): "Light the candle in the middle of the room! Press the [CROSS] button when the ? appears."
        public const string CandleAction="Light the candle in the middle of the room";
        private const string CandleInstruction="Light the candle in the middle of the room. Press Confirm when the question-mark bubble appears.";
        private static readonly OpeningCandleRegion[] _regions={
            new OpeningCandleRegion(4,"80956c767770d9453d49f29525b21629135e2b0e6ece700aaaf0bcaca45d433e",P(0,350),P(210,120),P(300,350),P(210,650)),
            new OpeningCandleRegion(5,"de9ee655ff362e06576814afd1e2e0e4a29bbb5ab6dc0a849c92faa32700eecb",P(0,350),P(-210,120),P(-300,350),P(-210,650)),
            new OpeningCandleRegion(6,"2beb91843c0db2d2ee3b61612f932bc243dfd8faa76dcada6515d0150a1f7967",P(0,350),P(210,650),P(-210,650)),
            new OpeningCandleRegion(7,"d79947040bbb8bde9a01d2b411e6224ec47ab959b8be951eaea04eb21b6a5418",P(0,350),P(-210,120),P(210,120)),
        };
        // Union of the four native regions around the cargo-room table.
        private static readonly NavigationPoint[] _area={P(300,350),P(210,650),P(-210,650),P(-300,350),P(-210,120),P(210,120)};
        public static IList<OpeningCandleRegion> CandleRegions{get{return new ReadOnlyCollection<OpeningCandleRegion>(_regions);}}
        public static NavigationPoint[] CandleArea{get{return (NavigationPoint[])_area.Clone();}}
        public static StoryCheckpoint[] All{get{return new[]{
            new StoryCheckpoint{Floor=Floor,Ceiling=Ceiling,ScopeFields=new[]{CargoField},MapByte=PhaseByte,MapValue=CandlePhase,RequiredScripts=Scripts(3,CargoDirector,17,CargoZidane),
                Stage=new StoryStage{Milestone=CandleMilestone,GoalFields=new[]{CargoField},Goal="Prima Vista",Instruction=CandleInstruction}},
            // Every other phase of the cargo room and the meeting is shown cutscene, battle or dialogue.
            new StoryCheckpoint{Floor=Floor,Ceiling=Ceiling,ScopeFields=new[]{CargoField},RequiredScripts=Scripts(3,CargoDirector,17,CargoZidane),
                Stage=new StoryStage{Milestone=CandleMilestone,GoalFields=new[]{CargoField},Goal="Prima Vista",Instruction="",InformationOnly=true}},
            new StoryCheckpoint{Floor=Floor,Ceiling=Ceiling,ScopeFields=new[]{MeetingField},RequiredScripts=Scripts(3,MeetingBaku),
                Stage=new StoryStage{Milestone=CandleMilestone,GoalFields=new[]{MeetingField},Goal="Prima Vista",Instruction="",InformationOnly=true}},
            // A changed or unavailable script cannot prove a candle action.
            // Keep only the observed current room, without future-scene advice
            // or wrapping any physical targets annotated by unverified code.
            new StoryCheckpoint{Floor=Floor,Ceiling=Ceiling,ScopeFields=new[]{CargoField,MeetingField},
                Stage=new StoryStage{Milestone=CandleMilestone,Goal="Prima Vista",Instruction="",InformationOnly=true}},
            // Steeple (115): the counter stays 1154 until the first ladder examine and 1155 until the
            // rooftop scenes end; the next catalog stage (1190) is a castle scene, never this route.
            new StoryCheckpoint{Floor=1154,Ceiling=1155,ScopeFields=new[]{SteepleField},MapByte=PhaseByte,MapValue=SteepleExaminePhase,RequiredScripts=SteepleScripts(),
                Stage=new StoryStage{Milestone=SteepleMilestone,GoalFields=new[]{SteepleField},Goal="Alexandria",Instruction=LadderExamineInstruction}},
            new StoryCheckpoint{Floor=SteepleMilestone,Ceiling=SteepleCeiling,ScopeFields=new[]{SteepleField},MapByte=PhaseByte,MapValue=SteepleClimbPhase,RequiredScripts=SteepleScripts(),
                Stage=new StoryStage{Milestone=SteepleMilestone,GoalFields=new[]{SteepleField},Goal="Alexandria",Instruction=LadderClimbInstruction}},
            new StoryCheckpoint{Floor=1154,Ceiling=SteepleCeiling,ScopeFields=new[]{SteepleField},RequiredScripts=SteepleScripts(),
                Stage=new StoryStage{Milestone=SteepleMilestone,GoalFields=new[]{SteepleField},Goal="Alexandria",Instruction="",InformationOnly=true}},
            // Rooftop (116): two native control windows that wait for Vivi's position.
            Rooftop(3,RooftopFirstInstruction),Rooftop(4,RooftopFirstInstruction),Rooftop(8,RooftopSecondInstruction),Rooftop(9,RooftopSecondInstruction),
            new StoryCheckpoint{Floor=SteepleMilestone,Ceiling=SteepleCeiling,ScopeFields=new[]{OpeningRooftopEvidence.Field},RequiredScripts=RooftopScripts(),
                Stage=new StoryStage{Milestone=SteepleMilestone,GoalFields=new[]{OpeningRooftopEvidence.Field},Goal="Alexandria",Instruction="",InformationOnly=true}},
            // The opening streets at 1155: the next native step is still the Steeple ladder (115 phase 10),
            // not the 1190 castle stage. Movement still needs current live exits with native destinations.
            new StoryCheckpoint{Floor=SteepleMilestone,Ceiling=SteepleCeiling,ScopeFields=OpeningStreets,
                Stage=new StoryStage{Milestone=SteepleMilestone,GoalFields=new[]{SteepleField},Goal="Alexandria",Instruction=ReturnToLadderInstruction}},
            // Changed or unavailable scripts keep the current scene without the castle route.
            new StoryCheckpoint{Floor=SteepleMilestone,Ceiling=SteepleCeiling,ScopeFields=new[]{SteepleField,OpeningRooftopEvidence.Field},
                Stage=new StoryStage{Milestone=SteepleMilestone,Goal="Alexandria",Instruction="",InformationOnly=true}},
        };}}
        // True only for the reviewed regions, in field 50, at the counter floor, during the native candle phase.
        public static bool IsCandleRegion(int field,int sid,string hash,int progress,int? phase)
        {
            if(field!=CargoField || progress<Floor || progress>=Ceiling || phase!=CandlePhase || hash==null)return false;
            foreach(var region in _regions)if(region.Sid==sid && region.Hash==hash)return true;
            return false;
        }
        public const int AlleyField=105,AlleyTriggerSid=12,AlleyRatKidSid=3,AlleyPlayerUid=14,AlleyTriggerPhase=1,AlleyBoyPhase=15;
        public const string AlleyMap="field:105:FBG_N01_ALXT_MAP021_AT_BST_0";
        public const string AlleyDirector="8a90a78df8b24b1d11d2a1e847079bbe1c0fa6f4496b2c586405179c556a22af";
        public const string AlleyPhases="1425ffd7b67b05a78a5a2005c1f17f766dcabb98e8a4ce8245cdda952ecc0cb4";
        public const string AlleyRatKid="5bacab2433f84d8c26c4afe802bcaf289d31553a9494ee4b5a9ba0cc11b9d3ef";
        public const string AlleyTrigger="79ddf01f037ffde35b873b73c1f35a5b6bba7ec9ca5a5832289f91e6f7bf9cf0";
        public const string AlleyVivi="b6b0fd07d2ce9d804a675aca49fc40bb68775fdbb0dce6597b76a7c353cbe202";
        public const int SteepleField=115,SteepleLadderSid=15,SteepleExaminePhase=2,SteepleClimbPhase=10,SteepleMilestone=1155;
        public const string SteepleDirector="f518dcd52ee5c63171e902e423e3fb6f0c4c60123ae03ba88dc1750d4e689844";
        public const string SteepleRatKid="0d12c5ead063aae336d0226d8bf05f022b2e1a2f970e923508819ea406ef2ce5";
        public const string SteepleLadder="442070aaf58c831db9bd8af7a1db98b766794821663efa9705eebf961303654e";
        public const string SteepleVivi="b058a488dacd70db20eba45c9df5c0bb1115afae26ba0523fb35d5ee2412920c";
        public const int SteepleCeiling=1190;
        // No native hint text covers these steps; the actions name only what is drawn
        // (the ladder, the boy) and the native "!" bubble, never the outcome of the scene.
        public const string AlleyTriggerAction="Walk farther into the alley";
        public const string AlleyBoyAction="Talk to the boy in the alley";
        public const string LadderExamineAction="Examine the ladder. Press Confirm when the exclamation-mark bubble appears";
        public const string LadderClimbAction="Climb the ladder. Press Confirm when the exclamation-mark bubble appears, then hold Up to climb to the top";
        private const string LadderExamineInstruction=LadderExamineAction+".";
        private const string LadderClimbInstruction=LadderClimbAction+".";
        private const string ReturnToLadderInstruction="Return to the steeple and climb the ladder.";
        // EVT_ALEX1_AT_* town fields 100-114 (streets, square, alley, shops, inn, pub, houses).
        private static readonly int[] OpeningStreets={100,101,102,103,104,105,106,107,108,109,110,111,112,113,114};
        private const string RooftopFirstInstruction=OpeningRooftopEvidence.FirstAction+".";
        private const string RooftopSecondInstruction=OpeningRooftopEvidence.SecondAction+".";
        // EVT_ALEX1_AT_BACK_STR (105): entry 0 init sets MAP5[24]=15 below counter 1152 and 1 below 1151;
        // region 12 (touch, 1150 <= counter < 1152) sets MAP5[30]=1 and starts the scene in which the boy
        // (entry 3) writes 1151. On a later visit at 1151 (phase 15) the boy waits hidden until
        // 2800 <= OBJ(250).z <= 2900, is then shown, and his talk (phase 15 -> 5) leads to Vivi's 1152 write.
        // EVT_ALEX1_AT_SENTOU (115): init maps 1152/1153 -> phase 1, 1154 -> 2, 1155 -> 10. Region 15
        // touch shows FICON 1 while MAP5[49]==0; its talk sets MAP5[49]=1 (Vivi's phase-2 wait) and in
        // phase 10 requests Vivi tag 15, which climbs on held Up (KEY 16) or Left and descends on Down/Right;
        // Vivi's phase-10 block advances to the MAPJUMP 116 scene only once she is at the top.
        public static OpeningStoryAnnotation Annotation(int field,int sid,string hash,int progress,int? phase,Func<int,string> scripts)
        {
            if(hash==null || scripts==null || !phase.HasValue)return null;
            if(field==AlleyField) {
                if(sid==AlleyTriggerSid && hash==AlleyTrigger && progress==1150 && phase==AlleyTriggerPhase && Verified(scripts,AlleyTriggerScripts()))return new OpeningStoryAnnotation(1151,AlleyTriggerAction);
                if(sid==AlleyRatKidSid && hash==AlleyRatKid && progress==1151 && phase==AlleyBoyPhase && Verified(scripts,AlleyBoyScripts()))return new OpeningStoryAnnotation(1152,AlleyBoyAction);
            }
            if(field==SteepleField && sid==SteepleLadderSid && hash==SteepleLadder && Verified(scripts,SteepleScripts())) {
                if(progress==1154 && phase==SteepleExaminePhase)return new OpeningStoryAnnotation(SteepleMilestone,LadderExamineAction);
                if(progress>=SteepleMilestone && progress<SteepleCeiling && phase==SteepleClimbPhase)return new OpeningStoryAnnotation(SteepleMilestone,LadderClimbAction);
            }
            return null;
        }
        // The hidden boy's native position wait at 1151; boyShown is his current native show flag.
        public static OpeningPositionGoal AlleyBoyGoal(NavigationContext context,int? phase,Func<int,string> scripts,bool boyShown)
        {
            if(context==null || !context.CanControl || boyShown || context.LogicalMapKey!=AlleyMap || context.PlayerIdentity!=AlleyPlayerUid || context.StoryProgress!=1151 ||
               phase!=AlleyBoyPhase || !Verified(scripts,AlleyBoyScripts()))return null;
            return new OpeningPositionGoal{Field=AlleyField,Map=AlleyMap,Phase=AlleyBoyPhase,Milestone=1152,Action=AlleyTriggerAction,Axis=OpeningGoalAxis.Z,Minimum=2800,Maximum=2900};
        }
        internal static bool Verified(Func<int,string> scripts,Dictionary<int,string> required)
        {
            if(scripts==null)return false;
            foreach(var entry in required)if(scripts(entry.Key)!=entry.Value)return false;
            return true;
        }
        // Region 12 deletes itself after its touch (DELETE 255), so a return visit at 1151 has no live
        // entry for it; the boy's proof uses only the entries that are active in that state.
        private static Dictionary<int,string> AlleyBoyScripts(){return Scripts(0,AlleyDirector,1,AlleyPhases,3,AlleyRatKid,14,AlleyVivi);}
        private static Dictionary<int,string> AlleyTriggerScripts(){var result=AlleyBoyScripts();result[AlleyTriggerSid]=AlleyTrigger;return result;}
        private static Dictionary<int,string> SteepleScripts(){return Scripts(0,SteepleDirector,1,SteepleRatKid,15,SteepleLadder,17,SteepleVivi);}
        internal static Dictionary<int,string> RooftopScripts(){return Scripts(0,OpeningRooftopEvidence.Director,2,OpeningRooftopEvidence.RatKid,21,OpeningRooftopEvidence.Player);}
        private static StoryCheckpoint Rooftop(int phase,string instruction)
        {
            return new StoryCheckpoint{Floor=SteepleMilestone,Ceiling=SteepleCeiling,ScopeFields=new[]{OpeningRooftopEvidence.Field},MapByte=PhaseByte,MapValue=phase,RequiredScripts=RooftopScripts(),
                Stage=new StoryStage{Milestone=SteepleMilestone,GoalFields=new[]{OpeningRooftopEvidence.Field},Goal="Alexandria",Instruction=instruction}};
        }
        private static Dictionary<int,string> Scripts(params object[] pairs)
        {var result=new Dictionary<int,string>();for(int i=0;i+1<pairs.Length;i+=2)result[(int)pairs[i]]=(string)pairs[i+1];return result;}
        private static NavigationPoint P(float x,float z){return new NavigationPoint(x,0,z);}
    }
}
