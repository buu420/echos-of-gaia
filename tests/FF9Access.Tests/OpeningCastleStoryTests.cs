using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    // Native evidence (installed US scripts, 2026-11-04 audit; research/2026-11-04-play-steiner-navigation/claude):
    // 1155: fields 61-64 are the play and the sword fight; 150 (G6[2]=325 -> phase 0) is the cutscene in which
    // Blank writes 1190. 1190: EVT_ALEX1_AC_H2F (153) phase 6 is Zidane's free state; his tag-1 phase-6 case
    // waits until OBJ(255).1 <= -450 (Zidane up the staircase Blank pointed at) before the hooded-girl scene.
    // Regions 26/27 are jump-less "Look out!" detours (phases 7-16) on the doorways that later are Steiner's
    // exits. After the queen's scene Steiner (model 5489) searches the castle until field 55 writes 1400.
    internal static class OpeningCastleStoryTests
    {
        private static string RepoFile(string relative)
        {
            foreach(var start in new[]{AppContext.BaseDirectory,Environment.CurrentDirectory}) {
                var dir=new DirectoryInfo(start);while(dir!=null && !Directory.Exists(Path.Combine(dir.FullName,"Assembly-CSharp")))dir=dir.Parent;
                if(dir!=null)return Path.Combine(dir.FullName,relative);
            }
            throw new Exception("Repository root not found");
        }
        private static JsonElement Catalog(){return JsonDocument.Parse(File.ReadAllText(RepoFile("docs/accessibility/story-navigation.json"))).RootElement;}
        private static int[] Ints(JsonElement row,string name){JsonElement value;return row.TryGetProperty(name,out value)?value.EnumerateArray().Select(v=>v.GetInt32()).ToArray():new int[0];}
        private static StoryTargetCatalog Installed()
        {
            var root=Catalog();
            var stages=root.GetProperty("stages").EnumerateArray().Select(s=>new StoryStage{Milestone=s.GetProperty("milestone").GetInt32(),GoalFields=Ints(s,"fields"),
                DepartureFields=Ints(s,"departureFields"),ApproachFields=Ints(s,"approachFields"),Goal=s.GetProperty("goal").GetString(),Instruction=s.GetProperty("instruction").GetString(),
                WorldPlaces=s.GetProperty("worldPlaces").EnumerateArray().Select(v=>v.GetString()).ToArray(),WorldLocations=Ints(s,"worldLocations")}).ToArray();
            var transit=root.GetProperty("transit").EnumerateArray().Select(t=>new StoryTransit{From=t.GetProperty("source").GetInt32(),To=t.GetProperty("destination").GetInt32()}).ToArray();
            return new StoryTargetCatalog(stages,transit,OpeningStoryCheckpoints.All);
        }
        private static Dictionary<int,string> Hashes(int field)
        {
            var entries=Catalog().GetProperty("fields").EnumerateArray().First(f=>f.GetProperty("field").GetInt32()==field).GetProperty("entries");
            return entries.EnumerateArray().ToDictionary(e=>e.GetProperty("sid").GetInt32(),e=>e.GetProperty("hash").GetString());
        }
        // Live entries of one native state: only these script ids exist in the field.
        private static Func<int,string> State(int field,params int[] present)
        {var hashes=Hashes(field);return sid=>present.Contains(sid) && hashes.ContainsKey(sid)?hashes[sid]:null;}
        private static Func<int,string> All(int field){var hashes=Hashes(field);return sid=>{string hash;return hashes.TryGetValue(sid,out hash)?hash:null;};}
        private static string Location(int id){return "Area "+id;}
        private static NavigationTarget Story(int field,int progress,int? phase,Func<int,string> scripts,params NavigationTarget[] targets)
        {
            var context=TestFixtures.Context("field:"+field+":SCENE");context.StoryProgress=progress;var input=new CatalogResult();
            KeyValuePair<int,string> steiner;
            if(OpeningCastleStory.SteinerEntries.TryGetValue(field,out steiner) && scripts(steiner.Key)==steiner.Value)context.PlayerIdentity=steiner.Key;
            else if(field==153 && scripts(3)==OpeningCastleStory.HallZidane)context.PlayerIdentity=3;
            else if(field==150 && scripts(2)==OpeningCastleStory.GuardZidane)context.PlayerIdentity=2;
            foreach(var target in targets)input.Targets.Add(target);
            Installed().Append(context,input,field,Location,index=>index==OpeningCastleStory.PhaseByte?phase:(int?)0,scripts);
            return input.Targets.Last(t=>t.Category==NavigationCategory.Story);
        }
        private static NavigationTarget Exit(int field,int uid,params int[] next)
        {return new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,"field:"+field+":SCENE",uid),Category=NavigationCategory.Exits,Generation=1,Label="Exit "+uid,NextFields=next,ApproachCandidates=new[]{TestFixtures.P(1,1)}};}
        private static string Text(NavigationTarget story){return (story.Label??"")+" "+(story.Availability??"");}
        private static void Informational(NavigationTarget story)
        {
            TestAssert.True(story.PhysicalId==null);TestAssert.Equal(0,story.ApproachCandidates.Length);
            TestAssert.True(story.Availability.Contains("Continue the current scene"));
            TestAssert.False(Text(story).Contains("Steiner") || Text(story).Contains("Search the castle") || Text(story).Contains("stairs"));
        }
        private static NavigationContext Hall(){var context=TestFixtures.Context(OpeningCastleStory.HallMap);context.StoryProgress=1190;context.CanControl=true;context.PlayerIdentity=OpeningCastleStory.HallZidaneUid;return context;}

        // Regression (tester log 2026-11-04): during the play (1155) Story announced "Continue Steiner's search".
        public static void PlayAndItsEndingCutsceneAreInformationOnly()
        {
            foreach(int field in new[]{61,62,63,64,150})foreach(int? phase in new int?[]{0,1,10,null})
                Informational(Story(field,1155,phase,All(field),Exit(field,11,153),Exit(field,18,153)));
        }
        // Regression: twenty minutes in 153 with "Next area" pointing at the trapped doorway.
        public static void ZidaneHallPhaseSixAsksForTheStairsWithoutARoute()
        {
            var story=Story(153,1190,6,State(153,0,1,2,3,7,9,11,22,28,31),Exit(153,28,150));
            TestAssert.True(story.PhysicalId==null);
            TestAssert.True(story.Label.Contains("Climb the stairs to the royal box"));
            TestAssert.False(story.Availability.Contains("Next area") || Text(story).Contains("Steiner"));
        }
        public static void ZidaneStairsGoalIsTheNativeHeightWait()
        {
            var goal=OpeningCastleStory.CurrentGoal(Hall(),6,State(153,0,1,2,3,7));
            TestAssert.True(goal!=null);TestAssert.Equal(OpeningGoalAxis.Y,goal.Axis);TestAssert.Equal(450,goal.Minimum.Value);TestAssert.True(goal.Maximum==null);
            TestAssert.Equal(153,goal.Field);TestAssert.Equal(OpeningCastleStory.HallMap,goal.Map);TestAssert.Equal(6,goal.Phase);TestAssert.Equal(1190,goal.Milestone);
            TestAssert.Equal(OpeningCastleStory.StairsAction,goal.Action);
        }
        public static void StairsGoalRequiresTheCurrentNativeZidaneState()
        {
            var scripts=State(153,0,1,2,3,7);
            foreach(int? phase in new int?[]{0,1,5,7,14,16,17,40,null})TestAssert.True(OpeningCastleStory.CurrentGoal(Hall(),phase,scripts)==null);
            foreach(int sid in new[]{0,2,3,7})TestAssert.True(OpeningCastleStory.CurrentGoal(Hall(),6,s=>s==sid?"0000":scripts(s))==null);
            TestAssert.True(OpeningCastleStory.CurrentGoal(Hall(),6,s=>null)==null);
            var context=Hall();context.CanControl=false;TestAssert.True(OpeningCastleStory.CurrentGoal(context,6,scripts)==null);
            context=Hall();context.StoryProgress=1189;TestAssert.True(OpeningCastleStory.CurrentGoal(context,6,scripts)==null);
            context=Hall();context.StoryProgress=1400;TestAssert.True(OpeningCastleStory.CurrentGoal(context,6,scripts)==null);
            context=Hall();context.PlayerIdentity=32;TestAssert.True(OpeningCastleStory.CurrentGoal(context,6,scripts)==null);
            context=Hall();context.LogicalMapKey="field:153:OTHER";TestAssert.True(OpeningCastleStory.CurrentGoal(context,6,scripts)==null);
        }
        public static void LookoutDetoursAndCastleCutscenesStayInformational()
        {
            var zidane=State(153,0,1,2,3,7,9,11,22,28,31);
            foreach(int phase in new[]{1,5,7,8,13,14,16,17,30})Informational(Story(153,1190,phase,zidane,Exit(153,28,150)));
            Informational(Story(153,1190,100,State(153,0,1,2,18,20),Exit(153,28,150)));
            Informational(Story(154,1190,1,State(154,0,1,2,4),Exit(154,8,158)));
            Informational(Story(151,1190,1,State(151,0,1,2,3,4,5,12,17),Exit(151,8,153)));
            foreach(int phase in new[]{40,44,48})Informational(Story(153,1190,phase,State(153,0,1,2,13,14,16,17,21,23,24,25,32),Exit(153,23,154)));
            Informational(Story(150,1190,0,State(150,0,1,2,3,4,5,6,9,17,18),Exit(150,18,153)));
        }
        public static void GuardRoomSeparatesZidaneBacktrackFromSteinersSearch()
        {
            var zidane=Story(150,1190,11,State(150,0,1,2,5,6,9,18),Exit(150,18,153));
            TestAssert.Equal(18,zidane.PhysicalIdentity.NativeId);TestAssert.True(zidane.Label.Contains("Return to the stairs to the royal box"));TestAssert.False(Text(zidane).Contains("Steiner"));
            var steiner=Story(150,1190,11,State(150,0,1,7,8,10,15,16,19,23),Exit(150,19,153));
            TestAssert.Equal(19,steiner.PhysicalIdentity.NativeId);TestAssert.True(steiner.Label.Contains("Search the castle for Princess Garnet"));TestAssert.False(Text(steiner).Contains("stairs"));
        }
        public static void SteinerSearchFollowsOnlyLiveExitsTowardTheTower()
        {
            var hall=Story(153,1190,0,State(153,0,1,2,13,14,21,23,24,25,32),Exit(153,24,150),Exit(153,23,154),Exit(153,25,64,151));
            TestAssert.Equal(23,hall.PhysicalIdentity.NativeId);TestAssert.True(hall.Label.Contains("Search the castle for Princess Garnet"));
            var entrance=Story(154,1190,0,State(154,0,1,5,6,7,8,9,10,11,15),Exit(154,9,155),Exit(154,8,158),Exit(154,10,167));
            TestAssert.Equal(8,entrance.PhysicalIdentity.NativeId);
            var tower=Story(165,1190,0,State(165,0,2,3,7),Exit(165,3,164),Exit(165,2,166));
            TestAssert.Equal(2,tower.PhysicalIdentity.NativeId);
            // A position-dependent exit without a proven destination is never a walking route.
            var unproven=Story(153,1190,0,State(153,0,1,2,13,14,21,23,24,25,32),Exit(153,23),Exit(153,24,150));
            TestAssert.True(unproven.PhysicalId==null);TestAssert.Equal(0,unproven.ApproachCandidates.Length);TestAssert.True(unproven.Label.Contains("Search the castle for Princess Garnet"));
        }
        public static void ChangedOrUnavailableCastleScriptsNeverRouteOrLeakSteiner()
        {
            foreach(int field in new[]{150,153,154,158,165}) {
                Informational(Story(field,1190,0,sid=>null,Exit(field,23,154),Exit(field,8,158)));
                var changed=All(field);Informational(Story(field,1190,6,sid=>changed(sid)==null?null:"0000",Exit(field,23,154),Exit(field,8,158)));
            }
        }
        public static void CastleHashesAreTheInstalledEntries()
        {
            var hall=Hashes(153);
            TestAssert.Equal(hall[0],OpeningCastleStory.HallDirector);TestAssert.Equal(hall[2],OpeningCastleStory.HallPhases);
            TestAssert.Equal(hall[3],OpeningCastleStory.HallZidane);TestAssert.Equal(hall[7],OpeningCastleStory.HallBlank);
            var guard=Hashes(150);TestAssert.Equal(guard[0],OpeningCastleStory.GuardDirector);TestAssert.Equal(guard[2],OpeningCastleStory.GuardZidane);
            foreach(var steiner in OpeningCastleStory.SteinerEntries)TestAssert.Equal(Hashes(steiner.Key)[steiner.Value.Key],steiner.Value.Value);
            TestAssert.Equal(18,OpeningCastleStory.SteinerEntries.Count);
        }
    }
}
