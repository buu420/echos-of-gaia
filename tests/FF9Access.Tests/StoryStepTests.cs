using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    // Generic native Story steps (docs/accessibility/story-steps.json, tools/Export-FF9StorySteps.py).
    // The data witnesses are the gates that were previously verified by hand in the installed scripts
    // (research/2026-11-03-entity-story-audit, research/2026-11-04-play-steiner-navigation); the generic
    // exporter must find the same native trigger, guard and position wait on its own.
    internal static class StoryStepTests
    {
        private static string RepoFile(string relative)
        {
            foreach(var start in new[]{AppContext.BaseDirectory,Environment.CurrentDirectory}) {
                var dir=new DirectoryInfo(start);while(dir!=null && !Directory.Exists(Path.Combine(dir.FullName,"Assembly-CSharp")))dir=dir.Parent;
                if(dir!=null)return Path.Combine(dir.FullName,relative);
            }
            throw new Exception("Repository root not found");
        }
        private static JsonElement Steps(){return JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("FF9_TEST_STORY_STEPS")??RepoFile("docs/accessibility/story-steps.json"))).RootElement;}
        private static JsonElement Catalog(){return JsonDocument.Parse(File.ReadAllText(RepoFile("docs/accessibility/story-navigation.json"))).RootElement;}
        private static int? Int(JsonElement row,string name){JsonElement value;return row.TryGetProperty(name,out value) && value.ValueKind==JsonValueKind.Number?(int?)value.GetInt32():null;}
        private static string Str(JsonElement row,string name){JsonElement value;return row.TryGetProperty(name,out value) && value.ValueKind==JsonValueKind.String?value.GetString():null;}
        private static IEnumerable<JsonElement> Rows(int field){return Steps().GetProperty("steps").EnumerateArray().Where(s=>s.GetProperty("field").GetInt32()==field);}
        public static void LaterActionsCannotReplaceTheCurrentRequiredStage()
        {
            var context=new NavigationContext{LogicalMapKey="field:810",Generation=1,StoryProgress=4160};
            var list=new CatalogResult();
            list.Targets.Add(new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,9),Generation=1,Category=NavigationCategory.People,Label="Current",IsInteractive=true,StoryStepMilestone=4170,StoryVerb="Talk to"});
            list.Targets.Add(new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,22),Generation=1,Category=NavigationCategory.People,Label="Future",IsInteractive=true,StoryStepMilestone=4210,StoryVerb="Talk to"});
            new StoryTargetCatalog(new[]{new StoryStage{Milestone=4170,GoalFields=new[]{810}}},new StoryTransit[0]).Append(context,list,810);
            var story=list.Targets.Where(t=>t.Category==NavigationCategory.Story).ToArray();
            TestAssert.Equal(1,story.Length);TestAssert.Equal(9,story[0].PhysicalIdentity.NativeId);
        }
        public static void NativeEarlyConversationBranchesGuardTheLaterRequest()
        {
            var row=Rows(810).Single(r=>r.GetProperty("sid").GetInt32()==22 && r.GetProperty("milestone").GetInt32()==4210);
            TestAssert.True(Int(row,"floor")>=4200);TestAssert.True(row.GetProperty("guards").GetArrayLength()>0);
        }
        public static void LiteralPositionOperandsBindTheControlledInstance()
        {
            var outside=Rows(1866).Single(r=>Str(r,"kind")=="position" && Int(r,"offset")==1767);
            TestAssert.Equal(20,outside.GetProperty("playerUid").GetInt32());TestAssert.True(Int(outside,"playerSid")==null);
            var alley=Rows(1864).Single(r=>Str(r,"kind")=="position" && Int(r,"offset")==242);
            TestAssert.Equal(15,alley.GetProperty("playerUid").GetInt32());
            foreach(var row in new[]{outside,alley}) {
                var step=Step(row);step.PlayerUid=row.GetProperty("playerUid").GetInt32();var catalog=new StoryStepCatalog(new[]{step});
                Func<int,string> hashes=sid=>step.Hash;
                TestAssert.Equal(0,catalog.CurrentPositions(step.Field,step.Milestone,step.Sid,hashes,g=>1,s=>true,99).Count);
                TestAssert.Equal(1,catalog.CurrentPositions(step.Field,step.Milestone,step.PlayerUid,hashes,g=>1,s=>true,step.PlayerUid).Count);
            }
        }
        public static void AWaitCannotInheritACounterWriteFromALaterCase()
        {TestAssert.False(Rows(2114).Any(r=>r.GetProperty("milestone").GetInt32()==10030));}
        public static void TrenoTravelDoesNotUseTheScriptedCarriageReturnAsAWalkingShortcut()
        {
            var transit=Steps().GetProperty("transit").EnumerateArray().Select(t=>new StoryTransit{From=t.GetProperty("source").GetInt32(),To=t.GetProperty("destination").GetInt32(),Kind=StoryStepCatalog.ParseTransit(Str(t,"kind")),Floor=Int(t,"floor"),Ceiling=Int(t,"ceiling")}).ToArray();
            foreach(int field in new[]{903,912}) {
                var context=new NavigationContext{LogicalMapKey="field:"+field,Generation=1,StoryProgress=4450};var list=new CatalogResult();
                foreach(int next in field==903?new[]{904,915}:new[]{904,908})list.Targets.Add(new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,context.LogicalMapKey,next),Generation=1,Category=NavigationCategory.Exits,Label="Exit "+next,IsInteractive=true,NextFields=new[]{next}});
                new StoryTargetCatalog(new[]{new StoryStage{Milestone=4455,GoalFields=new[]{913}}},transit).Append(context,list,field);
                var story=list.Targets.Where(t=>t.Category==NavigationCategory.Story).ToArray();
                TestAssert.True(story.Any(t=>t.PhysicalIdentity.NativeId==(field==903?915:908)));
                TestAssert.False(story.Any(t=>t.PhysicalIdentity.NativeId==904));
            }
        }
        private static StoryStep Step(JsonElement row)
        {
            Func<JsonElement,StoryStepGuard[]> guards=array=>array.EnumerateArray().Select(g=>new StoryStepGuard{Sid=g.GetProperty("sid").GetInt32(),Hash=g.GetProperty("hash").GetString(),Expression=Convert.FromHexString(g.GetProperty("expr").GetString())}).ToArray();
            JsonElement completion;
            return new StoryStep{Field=row.GetProperty("field").GetInt32(),Sid=row.GetProperty("sid").GetInt32(),Hash=row.GetProperty("hash").GetString(),Kind=StoryStepCatalog.ParseKind(row.GetProperty("kind").GetString()),
                Source=Str(row,"source"),Milestone=row.GetProperty("milestone").GetInt32(),Floor=Int(row,"floor"),Ceiling=Int(row,"ceiling"),Minimum=Int(row,"minimum"),Maximum=Int(row,"maximum"),
                Axis=Str(row,"axis")=="Y"?OpeningGoalAxis.Y:Str(row,"axis")=="Z"?OpeningGoalAxis.Z:OpeningGoalAxis.X,PlayerSid=Int(row,"playerSid")??-1,
                Guards=guards(row.GetProperty("guards")),Completion=row.TryGetProperty("completion",out completion)?guards(completion):new StoryStepGuard[0]};
        }

        // -- Data witnesses -------------------------------------------------------------------
        public static void ExporterFindsTheOpeningAlleyTriggerBoyAndPositionWait()
        {
            var rows=Rows(105).ToArray();
            TestAssert.True(rows.Any(r=>r.GetProperty("sid").GetInt32()==12 && Str(r,"kind")=="region-walk" && r.GetProperty("milestone").GetInt32()==1151 && Int(r,"floor")==1150 && Int(r,"ceiling")==1152));
            TestAssert.True(rows.Any(r=>r.GetProperty("sid").GetInt32()==3 && Str(r,"kind")=="talk" && r.GetProperty("milestone").GetInt32()==1152));
            var boy=rows.Single(r=>Str(r,"kind")=="position");
            TestAssert.Equal("Z",Str(boy,"axis"));TestAssert.Equal(2800,Int(boy,"minimum").Value);TestAssert.Equal(2900,Int(boy,"maximum").Value);
            TestAssert.True(boy.GetProperty("waiting").GetProperty("ips").EnumerateArray().Any(p=>p.GetInt32()==594));
        }
        public static void ExporterFindsTheSteepleLadderAndCastleStairsAndRooftopWaits()
        {
            TestAssert.True(Rows(115).Any(r=>r.GetProperty("sid").GetInt32()==15 && Str(r,"kind")=="region-confirm" && r.GetProperty("milestone").GetInt32()==1155));
            var stairs=Rows(153).Single();
            TestAssert.Equal("position",Str(stairs,"kind"));TestAssert.Equal("Y",Str(stairs,"axis"));TestAssert.Equal(450,Int(stairs,"minimum").Value);TestAssert.True(Int(stairs,"maximum")==null);
            TestAssert.Equal(OpeningCastleStory.HallZidane,Str(stairs,"hash"));
            var roof=Rows(116).ToArray();
            TestAssert.True(roof.Any(r=>Str(r,"axis")=="X" && Int(r,"maximum")==900 && Int(r,"minimum")==null));
            TestAssert.True(roof.Any(r=>Str(r,"axis")=="Z" && Int(r,"minimum")==2300 && Int(r,"maximum")==null));
        }
        public static void LookoutDetoursAreNotProgressSteps()
        {
            // 153 regions 26/27 set the hall phase to a scene that returns to phase 6.
            TestAssert.False(Rows(153).Any(r=>r.GetProperty("sid").GetInt32()==26 || r.GetProperty("sid").GetInt32()==27));
        }
        public static void ExporterBindsBurmeciaConfirmAndEvilForestWalkIn()
        {
            var burmecia=Rows(757).Single(r=>r.GetProperty("sid").GetInt32()==5);
            TestAssert.Equal("region-confirm",Str(burmecia,"kind"));TestAssert.Equal(3850,burmecia.GetProperty("milestone").GetInt32());
            TestAssert.Equal(3840,Int(burmecia,"floor").Value);TestAssert.Equal(3850,Int(burmecia,"ceiling").Value);
            TestAssert.True(Rows(251).Any(r=>r.GetProperty("sid").GetInt32()==12 && Str(r,"kind")=="region-walk" && r.GetProperty("milestone").GetInt32()==2020));
        }
        public static void StepHashesAreTheInstalledEntries()
        {
            var hashes=new Dictionary<int,Dictionary<int,string>>();
            foreach(var field in Catalog().GetProperty("fields").EnumerateArray())
                hashes[field.GetProperty("field").GetInt32()]=field.GetProperty("entries").EnumerateArray().ToDictionary(e=>e.GetProperty("sid").GetInt32(),e=>e.GetProperty("hash").GetString());
            int rows=0;
            foreach(var row in Steps().GetProperty("steps").EnumerateArray()) {
                var field=hashes[row.GetProperty("field").GetInt32()];rows++;
                TestAssert.Equal(field[row.GetProperty("sid").GetInt32()],row.GetProperty("hash").GetString());
                foreach(var guard in row.GetProperty("guards").EnumerateArray())TestAssert.Equal(field[guard.GetProperty("sid").GetInt32()],guard.GetProperty("hash").GetString());
            }
            TestAssert.True(rows>100);
            TestAssert.Equal(Catalog().GetProperty("inventorySHA256").GetString(),Steps().GetProperty("inventorySHA256").GetString());
        }
        public static void TransitIsTypedByItsNativeTrigger()
        {
            var transit=Steps().GetProperty("transit").EnumerateArray().ToArray();
            Func<int,int,string[]> kinds=(from,to)=>transit.Where(t=>t.GetProperty("source").GetInt32()==from && t.GetProperty("destination").GetInt32()==to).Select(t=>Str(t,"kind")).Distinct().ToArray();
            TestAssert.True(kinds(150,153).Contains("player"));
            TestAssert.True(kinds(153,154).Contains("player"));
            TestAssert.False(kinds(153,204).Contains("player"));
            TestAssert.Equal("scripted",kinds(166,55).Single());
            TestAssert.False(kinds(112,200).Contains("player"));
        }

        // -- Runtime semantics -------------------------------------------------------------------
        private static readonly byte[] PhaseIsSix={0xd5,0x18,0x7d,0x06,0x00,0x20,0x7f}; // MAP5[24]==6
        private static StoryStep Talk(params StoryStepGuard[] guards){return new StoryStep{Field=757,Sid=5,Hash="aa",Kind=StoryStepKind.RegionConfirm,Milestone=3850,Floor=3840,Ceiling=3850,Guards=guards,Completion=new StoryStepGuard[0]};}
        private static Func<int,string> Live(params int[] sids){return sid=>sids.Contains(sid)?(sid==5?"aa":"bb"):null;}
        public static void StepIsCurrentOnlyWhileItsNativeGuardsHold()
        {
            var guard=new StoryStepGuard{Sid=7,Hash="bb",Expression=PhaseIsSix};
            var step=Talk(guard);
            TestAssert.True(StoryStepCatalog.IsCurrent(step,3845,Live(5,7),g=>1));
            TestAssert.False(StoryStepCatalog.IsCurrent(step,3845,Live(5,7),g=>0));
            TestAssert.False(StoryStepCatalog.IsCurrent(step,3845,Live(5,7),g=>null)); // unreadable state never advances
            TestAssert.False(StoryStepCatalog.IsCurrent(step,3845,Live(5),g=>1));      // guard owner absent
            TestAssert.False(StoryStepCatalog.IsCurrent(step,3845,sid=>sid==5?"changed":"bb",g=>1));
            TestAssert.False(StoryStepCatalog.IsCurrent(step,3839,Live(5,7),g=>1));
            TestAssert.False(StoryStepCatalog.IsCurrent(step,3850,Live(5,7),g=>1));
        }
        public static void CompletedStepsAreNotOffered()
        {
            var done=new StoryStepGuard{Sid=7,Hash="bb",Expression=PhaseIsSix};
            var step=Talk();step.Completion=new[]{done};
            TestAssert.False(StoryStepCatalog.IsCurrent(step,3845,Live(5,7),g=>1));
            TestAssert.True(StoryStepCatalog.IsCurrent(step,3845,Live(5,7),g=>0));
            // An unreadable completion holds the current step rather than skipping it.
            TestAssert.True(StoryStepCatalog.IsCurrent(step,3845,Live(5,7),g=>null));
        }
        public static void CounterWritersRetireOnceTheCounterPasses()
        {
            var step=new StoryStep{Field=251,Sid=12,Hash="aa",Kind=StoryStepKind.RegionWalk,Source="direct-counter",Milestone=2020,Guards=new StoryStepGuard[0],Completion=new StoryStepGuard[0]};
            TestAssert.True(StoryStepCatalog.IsCurrent(step,2010,Live(5,12).Then(12,"aa"),g=>1));
            TestAssert.False(StoryStepCatalog.IsCurrent(step,2020,Live(12).Then(12,"aa"),g=>1));
        }
        public static void CatalogReturnsOnlyTheLiveEntrysCurrentSteps()
        {
            var a=Talk();var b=Talk();b.Sid=6;b.Hash="cc";
            var catalog=new StoryStepCatalog(new[]{a,b});
            TestAssert.Equal(1,catalog.Current(757,3845,5,"aa",Live(5),g=>1).Count);
            TestAssert.Equal(0,catalog.Current(757,3845,5,"zz",Live(5),g=>1).Count);
            TestAssert.Equal(0,catalog.Current(758,3845,5,"aa",Live(5),g=>1).Count);
        }
        public static void PositionStepsBecomeNativeGoals()
        {
            var stairs=new StoryStep{Field=153,Sid=3,Hash="z3",Kind=StoryStepKind.Position,Source="position-wait",Milestone=-1,Axis=OpeningGoalAxis.Y,Minimum=450,PlayerSid=3,
                Guards=new[]{new StoryStepGuard{Sid=3,Hash="z3",Expression=PhaseIsSix}},Completion=new StoryStepGuard[0]};
            var catalog=new StoryStepCatalog(new[]{stairs});
            Func<int,string> live=sid=>sid==3?"z3":null;
            var current=catalog.CurrentPositions(153,1190,3,live,g=>1);
            TestAssert.Equal(1,current.Count);
            var goal=StoryStepCatalog.Goal(current[0],"field:153:X",0);
            TestAssert.Equal(OpeningGoalAxis.Y,goal.Axis);TestAssert.Equal(450,goal.Minimum.Value);TestAssert.True(goal.Maximum==null);TestAssert.Equal("field:153:X",goal.Map);
            TestAssert.Equal(StoryStepCatalog.PositionAction,goal.Action);
            // The player's own wait belongs to the controlled entry only.
            TestAssert.Equal(0,catalog.CurrentPositions(153,1190,32,live,g=>1).Count);
            TestAssert.Equal(0,catalog.CurrentPositions(153,1190,3,live,g=>0).Count);
        }
        public static void StepActionsNameTheNativeTriggerKind()
        {
            TestAssert.Equal("Talk to Alexandrian Soldier",StoryStepCatalog.Action(StoryStepKind.Talk,"Alexandrian Soldier"));
            TestAssert.Equal("Interact with Interaction point",StoryStepCatalog.Action(StoryStepKind.RegionConfirm,"Interaction point"));
            TestAssert.Equal("Walk to Story path",StoryStepCatalog.Action(StoryStepKind.RegionWalk,"Story path"));
            TestAssert.Equal("Approach Chest",StoryStepCatalog.Action(StoryStepKind.Touch,"Chest"));
        }

        // -- Catalog integration ------------------------------------------------------------------
        private static StoryTargetCatalog Installed(bool typedTransit)
        {
            var root=Catalog();
            Func<JsonElement,string,int[]> ints=(row,name)=>{JsonElement value;return row.TryGetProperty(name,out value)?value.EnumerateArray().Select(v=>v.GetInt32()).ToArray():new int[0];};
            var stages=root.GetProperty("stages").EnumerateArray().Select(s=>new StoryStage{Milestone=s.GetProperty("milestone").GetInt32(),GoalFields=ints(s,"fields"),DepartureFields=ints(s,"departureFields"),
                ApproachFields=ints(s,"approachFields"),Goal=s.GetProperty("goal").GetString(),Instruction=s.GetProperty("instruction").GetString(),WorldPlaces=s.GetProperty("worldPlaces").EnumerateArray().Select(v=>v.GetString()).ToArray(),WorldLocations=ints(s,"worldLocations")}).ToArray();
            StoryTransit[] transit;
            if(typedTransit)transit=Steps().GetProperty("transit").EnumerateArray().Select(t=>new StoryTransit{From=t.GetProperty("source").GetInt32(),To=t.GetProperty("destination").GetInt32(),
                Kind=StoryStepCatalog.ParseTransit(Str(t,"kind")),Floor=Int(t,"floor"),Ceiling=Int(t,"ceiling")}).ToArray();
            else transit=root.GetProperty("transit").EnumerateArray().Select(t=>new StoryTransit{From=t.GetProperty("source").GetInt32(),To=t.GetProperty("destination").GetInt32()}).ToArray();
            return new StoryTargetCatalog(stages,transit);
        }
        private static NavigationTarget Exit(int field,int uid,params int[] next)
        {return new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,"field:"+field+":SCENE",uid),Category=NavigationCategory.Exits,Generation=1,Label="Exit "+uid,NextFields=next,ApproachCandidates=new[]{TestFixtures.P(1,1)}};}
        private static NavigationTarget Story(StoryTargetCatalog catalog,int field,int progress,params NavigationTarget[] targets)
        {
            var context=TestFixtures.Context("field:"+field+":SCENE");context.StoryProgress=progress;var input=new CatalogResult();
            foreach(var target in targets)input.Targets.Add(target);
            catalog.Append(context,input,field,id=>"Area "+id);
            return input.Targets.Last(t=>t.Category==NavigationCategory.Story);
        }
        // Regression: the untyped graph routed the castle hall toward the scripted 153 -> 204 jump.
        public static void ScriptedJumpsNoLongerShortcutWalkingRoutes()
        {
            var exits=new[]{Exit(153,24,150),Exit(153,23,154),Exit(153,25,64,151)};
            var typed=Story(Installed(true),153,1190,exits);
            TestAssert.Equal(23,typed.PhysicalIdentity.NativeId);
            TestAssert.False((typed.Label+typed.Availability).Contains("Area 204"));
        }
        public static void ScriptedArrivalStillCompletesTheFinalHop()
        {
            // 166 -> 55 is Steiner's tower-top scene: the only way into the 1400 writer's field.
            var tower=Story(Installed(true),165,1190,Exit(165,3,164),Exit(165,2,166));
            TestAssert.Equal(2,tower.PhysicalIdentity.NativeId);
        }
        public static void CurrentNativeStepIsTheStoryTargetWithItsActionVerb()
        {
            var actor=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,"field:757:SCENE",5),Category=NavigationCategory.Objects,Generation=1,Label="Interaction point",IsInteractive=true,
                ApproachCandidates=new[]{TestFixtures.P(0,0)},StoryStepMilestone=3850,StoryVerb=StoryStepCatalog.Verb(StoryStepKind.RegionConfirm)};
            var story=Story(Installed(true),757,3840,actor,Exit(757,99,752));
            TestAssert.True(story.PhysicalIdentity.Equals(actor.Id));TestAssert.Equal("Interact with Interaction point",story.Label);
            // A local step without a counter write (milestone unknown) still is the current action.
            actor.StoryStepMilestone=-1;
            TestAssert.True(Story(Installed(true),757,3840,actor).PhysicalIdentity.Equals(actor.Id));
            // Non-interactive actors are still never actionable Story goals.
            actor.IsInteractive=false;actor.InteractionUid=-1;
            TestAssert.True(Story(Installed(true),757,3840,actor).PhysicalId==null);
        }
        public static void PresentedTargetsCarryTheirCurrentNativeStep()
        {
            var context=TestFixtures.Context("field:757:SCENE");
            var observation=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,5),Position=TestFixtures.P(1,1),Visible=true,HasInteraction=true,
                ApproachCandidates=new[]{TestFixtures.P(1,1)},StoryStepMilestone=3850,StoryVerb="Interact with"};
            var catalog=new FieldTargetCatalog();
            var built=catalog.Build(context,new[]{observation},null).Targets.Single();
            TestAssert.Equal(3850,built.StoryStepMilestone);TestAssert.Equal("Interact with",built.StoryVerb);
            // Out of view: the remembered target keeps only today's native step, never an old one.
            observation.Visible=false;observation.OutOfView=true;observation.StoryStepMilestone=Int32.MinValue;observation.StoryVerb=null;
            var remembered=catalog.Build(context,new[]{observation},null).Targets.Single();
            TestAssert.Equal(Int32.MinValue,remembered.StoryStepMilestone);TestAssert.True(remembered.StoryVerb==null);
        }
        public static void FallbackNeverClaimsShownInteractions()
        {
            var story=Story(Installed(true),757,3840);
            TestAssert.True(story.PhysicalId==null);
            TestAssert.False(story.Availability.Contains("shown interactions"));
            TestAssert.True(story.Availability.Contains("no verified target"));
        }
    }
    internal static class StoryStepTestExtensions
    {
        public static Func<int,string> Then(this Func<int,string> live,int sid,string hash){return s=>s==sid?hash:live(s);}
    }
}
