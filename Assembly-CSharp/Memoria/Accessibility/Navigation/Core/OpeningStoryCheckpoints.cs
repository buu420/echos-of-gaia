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
        };}}
        // True only for the reviewed regions, in field 50, at the counter floor, during the native candle phase.
        public static bool IsCandleRegion(int field,int sid,string hash,int progress,int? phase)
        {
            if(field!=CargoField || progress<Floor || progress>=Ceiling || phase!=CandlePhase || hash==null)return false;
            foreach(var region in _regions)if(region.Sid==sid && region.Hash==hash)return true;
            return false;
        }
        private static Dictionary<int,string> Scripts(params object[] pairs)
        {var result=new Dictionary<int,string>();for(int i=0;i+1<pairs.Length;i+=2)result[(int)pairs[i]]=(string)pairs[i+1];return result;}
        private static NavigationPoint P(float x,float z){return new NavigationPoint(x,0,z);}
    }
}
