using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    // Native evidence (installed US EVT_ALEX1_TS_CARGO_0, field 50): Zidane entry 17 tag 1 sets the
    // scenario counter to 1000 (0x06fe/0x070c) immediately before UCON, so the whole candle step,
    // the Baku battle, the meeting (field 52) and Vivi's walk to the booth (fields 100-104) run at
    // counter 1000; only field 104 writes 1150. MAP5[24] is field 50's own phase: 1 = candle step.
    internal static class OpeningStoryTests
    {
        private static string RepoFile(string relative)
        {
            foreach(var start in new[]{AppContext.BaseDirectory,Environment.CurrentDirectory}) {
                var dir=new DirectoryInfo(start);while(dir!=null && !Directory.Exists(Path.Combine(dir.FullName,"Assembly-CSharp")))dir=dir.Parent;
                if(dir!=null)return Path.Combine(dir.FullName,relative);
            }
            throw new Exception("Repository root not found");
        }
        private static JsonElement Catalog()
        {
            return JsonDocument.Parse(File.ReadAllText(RepoFile("docs/accessibility/story-navigation.json"))).RootElement;
        }
        private static StoryTargetCatalog Installed(bool checkpoints)
        {
            var root=Catalog();
            var stages=root.GetProperty("stages").EnumerateArray().Select(s=>new StoryStage{Milestone=s.GetProperty("milestone").GetInt32(),GoalFields=s.GetProperty("fields").EnumerateArray().Select(v=>v.GetInt32()).ToArray(),
                DepartureFields=s.GetProperty("departureFields").EnumerateArray().Select(v=>v.GetInt32()).ToArray(),Goal=s.GetProperty("goal").GetString(),Instruction=s.GetProperty("instruction").GetString(),
                WorldPlaces=s.GetProperty("worldPlaces").EnumerateArray().Select(v=>v.GetString()).ToArray(),WorldLocations=s.GetProperty("worldLocations").EnumerateArray().Select(v=>v.GetInt32()).ToArray()}).ToArray();
            var transit=root.GetProperty("transit").EnumerateArray().Select(t=>new StoryTransit{From=t.GetProperty("source").GetInt32(),To=t.GetProperty("destination").GetInt32()}).ToArray();
            return checkpoints?new StoryTargetCatalog(stages,transit,OpeningStoryCheckpoints.All):new StoryTargetCatalog(stages,transit);
        }
        private static Func<int,string> LiveScripts(int field)
        {
            var root=Catalog();var entries=root.GetProperty("fields").EnumerateArray().First(f=>f.GetProperty("field").GetInt32()==field).GetProperty("entries");
            var hashes=entries.EnumerateArray().ToDictionary(e=>e.GetProperty("sid").GetInt32(),e=>e.GetProperty("hash").GetString());
            return sid=>{string hash;return hashes.TryGetValue(sid,out hash)?hash:null;};
        }
        private static NavigationTarget Story(StoryTargetCatalog catalog,int field,int progress,int? phase,Func<int,string> scripts,CatalogResult input=null)
        {
            var context=TestFixtures.Context("field:"+field+":SCENE");context.StoryProgress=progress;
            input=input??new CatalogResult();
            catalog.Append(context,input,field,id=>id==50?"Prima Vista/Cargo Room":id==52?"Prima Vista/Meeting Room":"Alexandria/Main Street",index=>index==OpeningStoryCheckpoints.PhaseByte?phase:(int?)0,scripts);
            return input.Targets.Last(t=>t.Category==NavigationCategory.Story);
        }
        public static void InstalledCatalogReproducesTheNewGameTicketPreview()
        {
            // Regression evidence for the live 2026-10-17 trace: without checkpoints the counter floor 1000
            // already passed the candle stage, so the cargo room announced Vivi's ticket booth.
            var context=TestFixtures.Context("field:50:SCENE");context.StoryProgress=1000;var input=new CatalogResult();
            Installed(false).Append(context,input,50,id=>"Alexandria");
            TestAssert.True(input.Targets.Single().Availability.Contains("ticket"));
        }
        public static void CandlePhaseAtTheCounterFloorAsksForTheCandle()
        {
            var story=Story(Installed(true),50,1000,OpeningStoryCheckpoints.CandlePhase,LiveScripts(50));
            TestAssert.True(story.Label.Contains("Prima Vista/Cargo Room"));
            TestAssert.True(story.Availability.Contains("Light the candle in the middle of the room"));
            TestAssert.False(story.Availability.Contains("ticket") || story.Availability.Contains("Vivi") || story.Label.Contains("Alexandria"));
        }
        public static void OtherCargoRoomPhasesNeverPreviewLaterScenes()
        {
            foreach(int? phase in new int?[]{0,2,7,null}) {
                var story=Story(Installed(true),50,1000,phase,LiveScripts(50));
                TestAssert.False(story.Availability.Contains("candle") || story.Availability.Contains("ticket") || story.Availability.Contains("Vivi") || story.Label.Contains("Alexandria"));
                TestAssert.True(story.Availability.Contains("Continue the current scene"));
            }
        }
        public static void MeetingRoomAtTheCounterFloorStaysOnTheShownScene()
        {
            var story=Story(Installed(true),52,1000,null,LiveScripts(52));
            TestAssert.True(story.Label.Contains("Prima Vista/Meeting Room"));
            TestAssert.False(story.Availability.Contains("ticket") || story.Availability.Contains("Vivi") || story.Availability.Contains("candle"));
        }
        public static void AlexandriaAtTheSameFloorKeepsTheReviewedTicketStage()
        {
            var story=Story(Installed(true),100,1000,null,LiveScripts(100));
            TestAssert.True(story.Availability.Contains("Show Vivi's ticket at the ticket booth"));
        }
        public static void OldFutureOrModifiedScriptsNeverUseOpeningCheckpoints()
        {
            // Before the floor (opening FMV) the existing 1000 stage is unchanged.
            TestAssert.True(Story(Installed(true),50,999,OpeningStoryCheckpoints.CandlePhase,LiveScripts(50)).Availability.Contains("Examine the candle"));
            // A later counter in the same field uses the ordinary stage order.
            TestAssert.False(Story(Installed(true),50,1150,OpeningStoryCheckpoints.CandlePhase,LiveScripts(50)).Availability.Contains("Light the candle in the middle"));
            // A different script cannot identify the candle interaction.
            Func<int,string> modded=sid=>sid==17?"0000":LiveScripts(50)(sid);
            TestAssert.False(Story(Installed(true),50,1000,OpeningStoryCheckpoints.CandlePhase,modded).Availability.Contains("Light the candle in the middle"));
            TestAssert.False(Story(Installed(true),50,1000,OpeningStoryCheckpoints.CandlePhase,sid=>null).Availability.Contains("Light the candle in the middle"));
        }
        public static void PresentedCandleHotspotBecomesTheStoryTarget()
        {
            var context=TestFixtures.Context("field:50:SCENE");
            var hotspot=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,4),Generation=1,Category=NavigationCategory.Objects,Label="Object A",IsInteractive=true,
                Position=TestFixtures.P(0,350),ApproachCandidates=new[]{TestFixtures.P(250,350)},InteractionUid=4,InteractionRegion=OpeningStoryCheckpoints.CandleArea,
                StoryMilestones=new[]{OpeningStoryCheckpoints.CandleMilestone},StoryAction=OpeningStoryCheckpoints.CandleAction};
            var input=new CatalogResult();input.Targets.Add(hotspot);
            var story=Story(Installed(true),50,1000,OpeningStoryCheckpoints.CandlePhase,LiveScripts(50),input);
            TestAssert.True(story.PhysicalIdentity.Equals(hotspot.Id));TestAssert.Equal(OpeningStoryCheckpoints.CandleAction,story.Label);
            TestAssert.Equal(4,story.InteractionUid);
        }
        public static void UnverifiedOpeningScriptsKeepCurrentSceneWithoutWrappingUnverifiedTargets()
        {
            foreach(int field in new[]{50,52}) {
                var map="field:"+field+":SCENE";
                var input=new CatalogResult();input.Targets.Add(new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,map,4),Generation=1,Category=NavigationCategory.Objects,
                    Label="Object A",IsInteractive=true,ApproachCandidates=new[]{TestFixtures.P(250,350)},StoryMilestones=new[]{1000},StoryAction=OpeningStoryCheckpoints.CandleAction});
                var story=Story(Installed(true),field,1000,1,sid=>null,input);
                TestAssert.Equal(TargetKind.Story,story.PhysicalIdentity.Kind);
                TestAssert.Equal(0,story.ApproachCandidates.Length);
                TestAssert.True(story.Availability.Contains("current scene"));
                TestAssert.False(story.Label.Contains("Alexandria") || story.Label.Contains("candle") || story.Availability.Contains("ticket") || story.Availability.Contains("candle"));
            }
        }
        public static void CandleRegionsAreTheExactNativeSettersOfTheCandleFlag()
        {
            var scripts=LiveScripts(50);
            foreach(var region in OpeningStoryCheckpoints.CandleRegions) {
                TestAssert.Equal(scripts(region.Sid),region.Hash);
                TestAssert.True(OpeningStoryCheckpoints.IsCandleRegion(50,region.Sid,region.Hash,1000,OpeningStoryCheckpoints.CandlePhase));
                TestAssert.False(OpeningStoryCheckpoints.IsCandleRegion(50,region.Sid,region.Hash,1000,2));
                TestAssert.False(OpeningStoryCheckpoints.IsCandleRegion(50,region.Sid,region.Hash,1150,OpeningStoryCheckpoints.CandlePhase));
                TestAssert.False(OpeningStoryCheckpoints.IsCandleRegion(51,region.Sid,region.Hash,1000,OpeningStoryCheckpoints.CandlePhase));
                TestAssert.False(OpeningStoryCheckpoints.IsCandleRegion(50,region.Sid,region.Hash.Replace('a','b'),1000,OpeningStoryCheckpoints.CandlePhase));
            }
            TestAssert.Equal(4,OpeningStoryCheckpoints.CandleRegions.Count);
            TestAssert.False(OpeningStoryCheckpoints.IsCandleRegion(50,8,scripts(8),1000,OpeningStoryCheckpoints.CandlePhase)); // hidden item spot
            // The four native triangles/quads tile one hexagon around the table at (0,350).
            TestAssert.Equal(6,OpeningStoryCheckpoints.CandleArea.Length);
        }
    }
}
