using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    // Native evidence (installed US scripts, 2026-11-03 audit):
    // EVT_ALEX1_AT_BACK_STR (105): region 12 touch (1150 <= counter < 1152, phase MAP5[24]==1) starts the
    // Rat Kid scene that writes 1151; on re-entry at 1151 (phase 15) the boy appears once
    // 2800 <= OBJ(250).z <= 2900 and his talk leads to Vivi's 1152 write.
    // EVT_ALEX1_AT_SENTOU (115): the ladder region 15 shows "!" (FICON 1); at 1154 phase 2 Confirm sets
    // MAP5[49]=1 and starts the scene that writes 1155; at 1155 phase 10 Confirm requests Vivi tag 15
    // (climb on held Up) and Vivi's phase-10 block advances to the MAPJUMP 116 scene once she is at the top.
    // EVT_ALEX1_AT_ROOF (116): Vivi (uid 21) is given control twice and the scene waits for
    // x <=900 (phase4) then z >=2300 (phase9): the backward loops continue while the inverse
    // comparisons hold. The counter stays1155 throughout.
    internal static class OpeningSteepleStoryTests
    {
        public static void RooftopGoalsEndTheNativeBackwardWaits()
        {
            // Original entry21 tag1: expression742 is OBJ(21).x >900;
            // JMP_IF at751 branches to WAIT739 while true. Progress requires <=900.
            // Expression1038 is OBJ(21).z <2300; JMP_IF1047 branches to WAIT1035.
            var expressions=new[]{new byte[]{0x78,21,0,0x7d,0x84,0x03,0x19,0x7f},new byte[]{0x78,21,2,0x7d,0xfc,0x08,0x18,0x7f}};
            var first=OpeningRooftopEvidence.Current(Roof(),4,LiveScripts(116));
            var second=OpeningRooftopEvidence.Current(Roof(),9,LiveScripts(116));
            foreach(var point in new[]{new NavigationPoint(2718,0,-137),new NavigationPoint(900,0,2300),new NavigationPoint(899,0,2400),new NavigationPoint(1000,0,271)}) {
                for(int step=0;step<2;step++) {
                    int? waiting;TestAssert.True(ExitScriptAnalyzer.TryExpression(expressions[step],new ScriptSnapshot(null,null,null),out waiting,(uid,property)=>uid==21?(int?)Math.Round(property==0?point.X:point.Z):null));
                    TestAssert.Equal(waiting==0,(step==0?first:second).Satisfied(point));
                }
            }
            TestAssert.Equal(739,754+unchecked((short)65521));TestAssert.Equal(1035,1050+unchecked((short)65521));
        }
        public static void DisabledActorCannotReuseAnOldInteractionRegionForStory()
        {
            var input=new CatalogResult();
            input.Targets.Add(new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,"field:105:SCENE",3),Category=NavigationCategory.People,Generation=1,Label="Boy",IsInteractive=false,InteractionUid=19,StoryMilestones=new[]{1152},ApproachCandidates=new[]{TestFixtures.P(1,1)}});
            TestAssert.True(Story(105,1151,15,LiveScripts(105),input).PhysicalId==null);
        }
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
        private static Func<int,string> LiveScripts(int field)
        {
            var entries=Catalog().GetProperty("fields").EnumerateArray().First(f=>f.GetProperty("field").GetInt32()==field).GetProperty("entries");
            var hashes=entries.EnumerateArray().ToDictionary(e=>e.GetProperty("sid").GetInt32(),e=>e.GetProperty("hash").GetString());
            return sid=>{string hash;return hashes.TryGetValue(sid,out hash)?hash:null;};
        }
        private static string Location(int id)
        {return id==105?"Alexandria, Alley":id==106?"Alexandria, By the Steeple":id==115?"Alexandria, Steeple":id==116?"Alexandria, Rooftop":id==150?"Alexandria Castle, Guardhouse":id==251?"Evil Forest, Trail, Area B":id==257?"Evil Forest, Nest":"Elsewhere";}
        private static NavigationTarget Story(int field,int progress,int? phase,Func<int,string> scripts,CatalogResult input=null)
        {
            var context=TestFixtures.Context("field:"+field+":SCENE");context.StoryProgress=progress;input=input??new CatalogResult();
            Installed().Append(context,input,field,Location,index=>index==OpeningStoryCheckpoints.PhaseByte?phase:(int?)0,scripts);
            return input.Targets.Last(t=>t.Category==NavigationCategory.Story);
        }
        private static NavigationTarget Exit(int field,int uid,int next)
        {return new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,"field:"+field+":SCENE",uid),Category=NavigationCategory.Exits,Generation=1,Label="Exit "+uid,NextFields=new[]{next},ApproachCandidates=new[]{TestFixtures.P(1,1)}};}
        private static NavigationTarget Annotated(int field,int uid,OpeningStoryAnnotation annotation,NavigationCategory category=NavigationCategory.Objects)
        {
            return new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,"field:"+field+":SCENE",uid),Category=category,Generation=1,Label="Ladder",IsInteractive=true,InteractionUid=uid,
                ApproachCandidates=new[]{TestFixtures.P(0,60)},StoryMilestones=new[]{annotation.Milestone},StoryAction=annotation.Action};
        }
        private static NavigationContext Roof(){var context=TestFixtures.Context(OpeningRooftopEvidence.Map);context.StoryProgress=1155;context.CanControl=true;context.PlayerIdentity=OpeningRooftopEvidence.PlayerUid;return context;}

        // Regression: the tester log (2026-11-03) sat at counter 1155 in the Steeple for over an hour;
        // the catalog selected the next stage (1190, Steiner in field 150) and wrapped the Steeple exit.
        public static void SteepleLadderPhaseNeverRoutesToTheCastle()
        {
            var input=new CatalogResult();input.Targets.Add(Exit(115,13,106));
            var story=Story(115,1155,OpeningStoryCheckpoints.SteepleClimbPhase,LiveScripts(115),input);
            TestAssert.True(story.PhysicalId==null);
            TestAssert.True(story.Label.Contains("ladder") && story.Availability.Contains("hold Up"));
            TestAssert.False(story.Label.Contains("Steiner") || story.Availability.Contains("Steiner") || story.Availability.Contains("castle"));
        }
        public static void PresentedLadderRegionIsTheStoryTargetWhileClimbingIsOffered()
        {
            var annotation=OpeningStoryCheckpoints.Annotation(115,OpeningStoryCheckpoints.SteepleLadderSid,OpeningStoryCheckpoints.SteepleLadder,1155,OpeningStoryCheckpoints.SteepleClimbPhase,LiveScripts(115));
            TestAssert.True(annotation!=null);TestAssert.Equal(OpeningStoryCheckpoints.SteepleMilestone,annotation.Milestone);
            var input=new CatalogResult();input.Targets.Add(Exit(115,13,106));var ladder=Annotated(115,15,annotation);input.Targets.Add(ladder);
            var story=Story(115,1155,OpeningStoryCheckpoints.SteepleClimbPhase,LiveScripts(115),input);
            TestAssert.True(story.PhysicalIdentity.Equals(ladder.Id));TestAssert.Equal(15,story.InteractionUid);
            TestAssert.True(story.Label.Contains("Climb the ladder") && story.Label.Contains("hold Up"));
        }
        public static void FirstLadderExamineAt1154AsksForConfirmOnly()
        {
            var annotation=OpeningStoryCheckpoints.Annotation(115,15,OpeningStoryCheckpoints.SteepleLadder,1154,OpeningStoryCheckpoints.SteepleExaminePhase,LiveScripts(115));
            TestAssert.True(annotation!=null);TestAssert.Equal(1155,annotation.Milestone);
            TestAssert.True(annotation.Action.Contains("Examine the ladder"));TestAssert.False(annotation.Action.Contains("hold Up"));
            var input=new CatalogResult();var ladder=Annotated(115,15,annotation);input.Targets.Add(ladder);input.Targets.Add(Exit(115,13,106));
            var story=Story(115,1154,OpeningStoryCheckpoints.SteepleExaminePhase,LiveScripts(115),input);
            TestAssert.True(story.PhysicalIdentity.Equals(ladder.Id));
        }
        public static void SteepleCutscenePhasesStayOnTheShownScene()
        {
            foreach(int? phase in new int?[]{3,9,11,12,13,null}) {
                var input=new CatalogResult();input.Targets.Add(Exit(115,13,106));
                var story=Story(115,phase>=10 || phase==null?1155:1154,phase,LiveScripts(115),input);
                TestAssert.True(story.PhysicalId==null);TestAssert.True(story.Availability.Contains("Continue the current scene"));
                TestAssert.False(story.Availability.Contains("Steiner") || story.Label.Contains("Castle"));
            }
            TestAssert.True(OpeningStoryCheckpoints.Annotation(115,15,OpeningStoryCheckpoints.SteepleLadder,1155,11,LiveScripts(115))==null);
            TestAssert.True(OpeningStoryCheckpoints.Annotation(115,15,OpeningStoryCheckpoints.SteepleLadder,1154,10,LiveScripts(115))==null);
        }
        public static void ChangedSteepleScriptsNeitherClaimTheLadderNorRouteToTheCastle()
        {
            Func<int,string> modded=sid=>sid==17?"0000":LiveScripts(115)(sid);
            TestAssert.True(OpeningStoryCheckpoints.Annotation(115,15,OpeningStoryCheckpoints.SteepleLadder,1155,10,modded)==null);
            TestAssert.True(OpeningStoryCheckpoints.Annotation(115,15,"changed",1155,10,LiveScripts(115))==null);
            TestAssert.True(OpeningStoryCheckpoints.Annotation(116,15,OpeningStoryCheckpoints.SteepleLadder,1155,10,LiveScripts(115))==null);
            foreach(var scripts in new Func<int,string>[]{modded,sid=>null}) {
                var input=new CatalogResult();input.Targets.Add(Exit(115,13,106));
                var story=Story(115,1155,10,scripts,input);
                TestAssert.True(story.PhysicalId==null);TestAssert.False(story.Availability.Contains("Steiner") || story.Availability.Contains("ladder"));
            }
        }
        public static void SteepleCheckpointHashesAreTheInstalledEntries()
        {
            var scripts=LiveScripts(115);
            TestAssert.Equal(scripts(0),OpeningStoryCheckpoints.SteepleDirector);TestAssert.Equal(scripts(1),OpeningStoryCheckpoints.SteepleRatKid);
            TestAssert.Equal(scripts(15),OpeningStoryCheckpoints.SteepleLadder);TestAssert.Equal(scripts(17),OpeningStoryCheckpoints.SteepleVivi);
            scripts=LiveScripts(105);
            TestAssert.Equal(scripts(0),OpeningStoryCheckpoints.AlleyDirector);TestAssert.Equal(scripts(1),OpeningStoryCheckpoints.AlleyPhases);
            TestAssert.Equal(scripts(3),OpeningStoryCheckpoints.AlleyRatKid);TestAssert.Equal(scripts(12),OpeningStoryCheckpoints.AlleyTrigger);TestAssert.Equal(scripts(14),OpeningStoryCheckpoints.AlleyVivi);
            scripts=LiveScripts(116);
            TestAssert.Equal(scripts(0),OpeningRooftopEvidence.Director);TestAssert.Equal(scripts(2),OpeningRooftopEvidence.RatKid);TestAssert.Equal(scripts(21),OpeningRooftopEvidence.Player);
        }

        public static void RooftopControlWindowsExposeTheNativeWalkGoals()
        {
            var scripts=LiveScripts(116);
            var first=OpeningRooftopEvidence.Current(Roof(),4,scripts);
            TestAssert.True(first!=null);TestAssert.Equal(OpeningGoalAxis.X,first.Axis);TestAssert.Equal(900,first.Maximum.Value);TestAssert.True(first.Minimum==null);
            TestAssert.Equal(OpeningStoryCheckpoints.SteepleMilestone,first.Milestone);
            TestAssert.True(first.Satisfied(new NavigationPoint(900,0,0)));TestAssert.False(first.Satisfied(new NavigationPoint(901,0,5000)));
            // Native OBJ(uid).x rounds the float position (EBin.CastFloatToIntWithChecking).
            TestAssert.True(first.Satisfied(new NavigationPoint(900.4f,0,0)));TestAssert.False(first.Satisfied(new NavigationPoint(900.6f,0,0)));
            TestAssert.True(OpeningRooftopEvidence.Current(Roof(),3,scripts).Axis==OpeningGoalAxis.X);
            var second=OpeningRooftopEvidence.Current(Roof(),9,scripts);
            TestAssert.Equal(OpeningGoalAxis.Z,second.Axis);TestAssert.Equal(2300,second.Minimum.Value);TestAssert.True(second.Maximum==null);
            TestAssert.True(second.Satisfied(new NavigationPoint(0,0,2300)));TestAssert.False(second.Satisfied(new NavigationPoint(0,0,2299)));
            TestAssert.True(OpeningRooftopEvidence.Current(Roof(),8,scripts).Axis==OpeningGoalAxis.Z);
        }
        public static void RooftopGoalsRequireCurrentControlPhaseAndScripts()
        {
            var scripts=LiveScripts(116);
            foreach(int? phase in new int?[]{1,2,5,6,7,10,12,13,null})TestAssert.True(OpeningRooftopEvidence.Current(Roof(),phase,scripts)==null);
            var context=Roof();context.CanControl=false;TestAssert.True(OpeningRooftopEvidence.Current(context,4,scripts)==null);
            context=Roof();context.StoryProgress=1154;TestAssert.True(OpeningRooftopEvidence.Current(context,4,scripts)==null);
            context=Roof();context.StoryProgress=1190;TestAssert.True(OpeningRooftopEvidence.Current(context,4,scripts)==null);
            context=Roof();context.PlayerIdentity=2;TestAssert.True(OpeningRooftopEvidence.Current(context,4,scripts)==null);
            context=Roof();context.LogicalMapKey="field:116:OTHER";TestAssert.True(OpeningRooftopEvidence.Current(context,4,scripts)==null);
            TestAssert.True(OpeningRooftopEvidence.Current(Roof(),4,sid=>sid==21?"0000":scripts(sid))==null);
            TestAssert.True(OpeningRooftopEvidence.Current(Roof(),4,sid=>null)==null);
        }
        public static void RooftopStoryUsesTheWalkGoalAndNeverTheCastleStage()
        {
            var goal=OpeningRooftopEvidence.Current(Roof(),4,LiveScripts(116));
            var bare=Story(116,1155,4,LiveScripts(116));
            TestAssert.True(bare.PhysicalId==null);TestAssert.True(bare.Label.Contains("Follow the boy across the rooftop"));TestAssert.False(bare.Availability.Contains("Steiner"));
            // Root position waypoints are direct Story targets (NativeOpeningPositionTargets), not catalog-selected actors.
            TestAssert.Equal(OpeningRooftopEvidence.FirstAction,goal.Action);
            foreach(int? phase in new int?[]{1,5,12,null})TestAssert.True(Story(116,1155,phase,LiveScripts(116)).Availability.Contains("Continue the current scene"));
            TestAssert.False(Story(116,1155,4,sid=>null).Availability.Contains("Steiner"));
        }

        public static void AlleyWalkInIsTheReviewedOpeningTrigger()
        {
            var scripts=LiveScripts(105);
            var annotation=OpeningStoryCheckpoints.Annotation(105,12,OpeningStoryCheckpoints.AlleyTrigger,1150,1,scripts);
            TestAssert.True(annotation!=null);TestAssert.Equal(1151,annotation.Milestone);
            foreach(var wrong in new[]{OpeningStoryCheckpoints.Annotation(105,12,OpeningStoryCheckpoints.AlleyTrigger,1151,1,scripts),OpeningStoryCheckpoints.Annotation(105,12,OpeningStoryCheckpoints.AlleyTrigger,1150,15,scripts),
                OpeningStoryCheckpoints.Annotation(105,12,"changed",1150,1,scripts),OpeningStoryCheckpoints.Annotation(105,11,OpeningStoryCheckpoints.AlleyTrigger,1150,1,scripts),
                OpeningStoryCheckpoints.Annotation(105,12,OpeningStoryCheckpoints.AlleyTrigger,1150,1,sid=>sid==3?"0000":scripts(sid))})TestAssert.True(wrong==null);
            var input=new CatalogResult();input.Targets.Add(Exit(105,10,103));var region=Annotated(105,12,annotation);region.InteractionUid=-1;input.Targets.Add(region);
            var story=Story(105,1150,1,scripts,input);
            TestAssert.True(story.PhysicalIdentity.Equals(region.Id));TestAssert.Equal(annotation.Action,story.Label);
        }
        public static void AlleyBoyIsTheTargetOnlyAfterHeIsShownAtTheReentryPhase()
        {
            var scripts=LiveScripts(105);
            var boy=OpeningStoryCheckpoints.Annotation(105,3,OpeningStoryCheckpoints.AlleyRatKid,1151,OpeningStoryCheckpoints.AlleyBoyPhase,scripts);
            TestAssert.True(boy!=null);TestAssert.Equal(1152,boy.Milestone);TestAssert.True(boy.Action.Contains("Talk to the boy"));
            TestAssert.True(OpeningStoryCheckpoints.Annotation(105,3,OpeningStoryCheckpoints.AlleyRatKid,1150,15,scripts)==null);
            TestAssert.True(OpeningStoryCheckpoints.Annotation(105,3,OpeningStoryCheckpoints.AlleyRatKid,1151,5,scripts)==null);
            var context=TestFixtures.Context(OpeningStoryCheckpoints.AlleyMap);context.StoryProgress=1151;context.CanControl=true;context.PlayerIdentity=OpeningStoryCheckpoints.AlleyPlayerUid;
            var goal=OpeningStoryCheckpoints.AlleyBoyGoal(context,15,scripts,false);
            TestAssert.True(goal!=null);TestAssert.Equal(OpeningGoalAxis.Z,goal.Axis);TestAssert.Equal(2800,goal.Minimum.Value);TestAssert.Equal(2900,goal.Maximum.Value);TestAssert.Equal(1152,goal.Milestone);
            TestAssert.True(goal.Satisfied(new NavigationPoint(-500,0,2850)));TestAssert.False(goal.Satisfied(new NavigationPoint(-500,0,2950)));TestAssert.False(goal.Satisfied(new NavigationPoint(-500,0,2700)));
            TestAssert.True(OpeningStoryCheckpoints.AlleyBoyGoal(context,15,scripts,true)==null);
            TestAssert.True(OpeningStoryCheckpoints.AlleyBoyGoal(context,1,scripts,false)==null);
            context.CanControl=false;TestAssert.True(OpeningStoryCheckpoints.AlleyBoyGoal(context,15,scripts,false)==null);
        }

        // A return visit at 1151: region 12 deleted itself (DELETE 255) after setting MAP5[30], so its
        // live entry is absent. The boy's proof uses only the active director, phase controller, boy and Vivi.
        public static void AlleyBoyAtTheReturnVisitDoesNotRequireTheDeletedTrigger()
        {
            var live=LiveScripts(105);Func<int,string> deleted=sid=>sid==12?null:live(sid);
            TestAssert.True(OpeningStoryCheckpoints.Annotation(105,3,OpeningStoryCheckpoints.AlleyRatKid,1151,15,deleted)!=null);
            var context=TestFixtures.Context(OpeningStoryCheckpoints.AlleyMap);context.StoryProgress=1151;context.CanControl=true;context.PlayerIdentity=OpeningStoryCheckpoints.AlleyPlayerUid;
            TestAssert.True(OpeningStoryCheckpoints.AlleyBoyGoal(context,15,deleted,false)!=null);
            foreach(int critical in new[]{0,1,3,14}) {
                Func<int,string> changed=sid=>sid==critical?"0000":deleted(sid);
                TestAssert.True(OpeningStoryCheckpoints.Annotation(105,3,OpeningStoryCheckpoints.AlleyRatKid,1151,15,changed)==null);
                TestAssert.True(OpeningStoryCheckpoints.AlleyBoyGoal(context,15,changed,false)==null);
            }
            // The walk-in itself still needs its own live entry.
            TestAssert.True(OpeningStoryCheckpoints.Annotation(105,12,OpeningStoryCheckpoints.AlleyTrigger,1150,1,deleted)==null);
        }
        // Regression (tester log): at 1155 the player repeatedly walked back through 106/105/103 and the
        // catalog chose the 1190 castle stage there. The streets route back to the ladder via live exits.
        public static void OpeningStreetsAt1155RouteBackToTheSteepleLadder()
        {
            foreach(var row in new[]{new[]{103,22,105},new[]{105,11,106},new[]{106,14,115}}) {
                int field=row[0];var input=new CatalogResult();
                input.Targets.Add(Exit(field,row[1],row[2]));
                foreach(var other in field==103?new[]{new[]{23,102},new[]{25,110}}:field==105?new[]{new[]{10,103}}:new[]{new[]{12,105},new[]{13,113}})input.Targets.Add(Exit(field,other[0],other[1]));
                var story=Story(field,1155,0,LiveScripts(field),input);
                TestAssert.Equal(row[1],story.PhysicalIdentity.NativeId);
                TestAssert.True(story.Label.Contains("ladder"));TestAssert.False(story.Label.Contains("Steiner") || (story.Availability??"").Contains("Steiner"));
            }
            // Unverified local exits (no live destination) never become a walking route.
            var unverified=new CatalogResult();var exit=Exit(105,11,106);exit.NextFields=new int[0];unverified.Targets.Add(exit);
            var fallback=Story(105,1155,0,LiveScripts(105),unverified);
            TestAssert.True(fallback.PhysicalId==null);TestAssert.Equal(0,fallback.ApproachCandidates.Length);TestAssert.False(fallback.Availability.Contains("Steiner"));
            // The castle stage still follows once the counter reaches 1190.
            TestAssert.False(Story(105,1190,0,LiveScripts(105)).Label.Contains("ladder"));
        }
        // An actor annotated before the final native interaction gates must not be a Story goal unless interactive.
        public static void NoninteractiveActorMilestoneIsNotAnActionableStoryGoal()
        {
            var actor=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,"field:105:SCENE",3),Category=NavigationCategory.People,Generation=1,Label="Boy",IsInteractive=false,
                ApproachCandidates=new[]{TestFixtures.P(0,2850)},StoryMilestones=new[]{1152},StoryAction=OpeningStoryCheckpoints.AlleyBoyAction};
            var input=new CatalogResult();input.Targets.Add(actor);
            var story=Story(105,1151,15,LiveScripts(105),input);
            TestAssert.True(story.PhysicalId==null);TestAssert.Equal(0,story.ApproachCandidates.Length);
            actor.IsInteractive=true;input=new CatalogResult();input.Targets.Add(actor);
            TestAssert.True(Story(105,1151,15,LiveScripts(105),input).PhysicalIdentity.Equals(actor.Id));
        }
        // Evil Forest (EVT_EFOREST1_EF_FR1_0, 251): region 12 has no jump of its own; its touch requests
        // director tag 26, which writes 2020 then 2030 around both Prison Cage battles. The installed
        // binding already carries [2020,2030], so a presented region annotated from its live path wins.
        public static void EvilForestWalkInIsSelectedOnceObserved()
        {
            var bare=Story(251,2010,null,LiveScripts(251));
            TestAssert.True(bare.PhysicalId==null);TestAssert.True(bare.Availability.Contains("next story step is in this area"));
            var input=new CatalogResult();input.Targets.Add(Exit(251,11,252));input.Targets.Add(Exit(251,10,250));
            var region=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,"field:251:SCENE",12),Category=NavigationCategory.Objects,Generation=1,Label="Story path",IsInteractive=true,ApproachCandidates=new[]{TestFixtures.P(-100,-1300)},StoryMilestones=new[]{2020,2030}};
            input.Targets.Add(region);
            var story=Story(251,2010,null,LiveScripts(251),input);
            TestAssert.True(story.PhysicalIdentity.Equals(region.Id));
        }
        public static void PlantBrainApproachFollowsTheForestExitsAfterTheShip()
        {
            // 257 init at counter 2280 sets MAP5[34]=1 and its director runs the Plant Brain scene (ENCOUNT 303) on arrival.
            var input=new CatalogResult();input.Targets.Add(Exit(256,1,254));input.Targets.Add(Exit(256,2,257));
            var story=Story(256,2280,null,LiveScripts(256),input);
            TestAssert.Equal(2,story.PhysicalIdentity.NativeId);
        }
    }
}
