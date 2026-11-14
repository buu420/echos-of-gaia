using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class StoryDataTests
    {
        private static JsonDocument Data(){return JsonDocument.Parse(Assembly.GetExecutingAssembly().GetManifestResourceStream("StoryNavigation.json"));}
        public static void IndependentMainStoryCheckpointsSpanAllFourDiscs()
        {
            // Independently selected chapter checkpoints; not generated from
            // the catalog's own stage list or algorithm.
            int[] progress={1000,2400,3700,3900,5690,5990,6130,6690,9400,9450,9600,9910,10400,10700,10890,10925,11200,11770};
            string[] goals={"Alexandria","Ice Cavern","Gizamaluke's Grotto","South Gate","Fossil Roo","Conde Petie","Black Mage Village","Iifa Tree","Black Mage Village","Desert Palace","Oeilvert","Esto Gaza","Ipsen's Castle","Terra","Bran Bal","Pandemonium","Memoria","Memoria"};
            using(var data=Data()) {
                var rows=data.RootElement.GetProperty("stages").EnumerateArray().ToArray();
                var stages=rows.Select(row=>new StoryStage{Milestone=row.GetProperty("milestone").GetInt32(),GoalFields=row.GetProperty("fields").EnumerateArray().Select(v=>v.GetInt32()).ToArray(),Goal=row.GetProperty("goal").GetString(),Instruction=row.GetProperty("instruction").GetString()}).ToArray();
                var catalog=new StoryTargetCatalog(stages,null);
                for(int i=0;i<progress.Length;i++) {
                    var context=TestFixtures.Context("world:1");context.StoryProgress=progress[i];var choices=new CatalogResult();catalog.Append(context,choices,-1);
                    if(!choices.Targets[0].Label.Contains(goals[i]))throw new InvalidOperationException("Wrong required chapter at counter "+progress[i]+": "+choices.Targets[0].Label);
                }
            }
        }
        public static void OptionalScenesAreExcludedAndAllDependencyHashesArePresent()
        {
            using(var data=Data()) {
                var root=data.RootElement;
                foreach(var stage in root.GetProperty("stages").EnumerateArray()) {
                    TestAssert.False(stage.GetProperty("fields").EnumerateArray().Any(v=>v.GetInt32()==2800 || v.GetInt32()==3100));
                    TestAssert.False(stage.GetProperty("milestone").GetInt32()==3180 || stage.GetProperty("milestone").GetInt32()==9250);
                }
                var fields=root.GetProperty("fields").EnumerateArray().ToDictionary(v=>v.GetProperty("field").GetInt32());
                TestAssert.True(fields.ContainsKey(3050));TestAssert.True(fields.ContainsKey(3100));TestAssert.False(fields.ContainsKey(3000));
                TestAssert.Equal(804,fields.Count);TestAssert.Equal(10470,fields.Values.Sum(f=>f.GetProperty("entries").GetArrayLength()));
                // Ordinary helper entry 0 is needed even without a pickup or
                // direct target annotation; callers cannot bypass its hash.
                TestAssert.True(fields[2850].GetProperty("entries").EnumerateArray().Any(e=>e.GetProperty("sid").GetInt32()==0 && e.GetProperty("hash").GetString().Length==64));
                var booth=root.GetProperty("listeners")[0];TestAssert.Equal(104,booth.GetProperty("field").GetInt32());TestAssert.Equal(4,booth.GetProperty("sid").GetInt32());TestAssert.Equal(7,booth.GetProperty("playerSid").GetInt32());
                TestAssert.True(booth.GetProperty("action").GetString().Contains("ticket booth"));
                var departure=root.GetProperty("stages").EnumerateArray().Single(s=>s.GetProperty("milestone").GetInt32()==10595);
                TestAssert.Equal(2500,departure.GetProperty("fields")[0].GetInt32());TestAssert.True(departure.GetProperty("instruction").GetString().Contains("leave Ipsen"));
                foreach(var evt in new[]{(Milestone:10030,Field:2114,Name:"Team"),(Milestone:10050,Field:2157,Name:"Recovery")}) {
                    var stage=root.GetProperty("stages").EnumerateArray().Single(s=>s.GetProperty("milestone").GetInt32()==evt.Milestone);
                    // The ATE scene writes its own counter, but Story keeps the
                    // player in the castle where the game offers the prompt.
                    TestAssert.Equal(evt.Field,stage.GetProperty("fields")[0].GetInt32());
                    TestAssert.True(stage.GetProperty("approachFields").EnumerateArray().Any(f=>f.GetInt32()==2161));
                    TestAssert.False(stage.GetProperty("approachFields").EnumerateArray().Any(f=>f.GetInt32()==evt.Field));
                    TestAssert.True(stage.GetProperty("instruction").GetString().Contains(evt.Name));
                }
            }
        }
        public static void ShrineWorldGoalsFollowTheRequiredDropoffOrder()
        {
            using(var data=Data()) {
                var stages=data.RootElement.GetProperty("stages").EnumerateArray().Select(row=>new StoryStage{Milestone=row.GetProperty("milestone").GetInt32(),GoalFields=row.GetProperty("fields").EnumerateArray().Select(v=>v.GetInt32()).ToArray(),Goal=row.GetProperty("goal").GetString(),WorldPlaces=row.GetProperty("worldPlaces").EnumerateArray().Select(v=>v.GetString()).ToArray(),WorldLocations=row.GetProperty("worldLocations").EnumerateArray().Select(v=>v.GetInt32()).ToArray()}).ToArray();
                var catalog=new StoryTargetCatalog(stages,null);string[] shrines={"Water Shrine","Fire Shrine","Wind Shrine","Earth Shrine"};int[] counters={10600,10620,10640,10660};int[] nativeIds={43,48,38,26};
                for(int phase=0;phase<4;phase++) {
                    var context=TestFixtures.Context("world:1");context.StoryProgress=counters[phase];context.Transport=new TransportIdentity(TransportKind.HildaGarde,9,9);var input=new CatalogResult();
                    for(int i=0;i<4;i++)input.Targets.Add(new NavigationTarget{Id=new TargetIdentity(TargetKind.WorldLocation,context.LogicalMapKey,nativeIds[i]),NativeLocationId=nativeIds[i],Generation=context.Generation,Category=NavigationCategory.Towns,Label=shrines[i]});
                    catalog.Append(context,input,-1);TestAssert.Equal(5,input.Targets.Count);TestAssert.Equal(nativeIds[phase],input.Targets[4].PhysicalIdentity.NativeId);TestAssert.True(input.Targets[4].Label.Contains(shrines[phase]));
                }
            }
        }
        public static void DiscTwoIifaAndMadainSariStagesCrossMountainPathInTheCorrectDirection()
        {
            using(var data=Data()) {
                var rows=data.RootElement.GetProperty("stages").EnumerateArray().ToArray();
                var journeys=new[]{
                    (Milestone:6600,Origin:-1,Entry:35,Destination:33,Departure:1557),
                    (Milestone:6700,Origin:1600,Entry:34,Destination:36,Departure:1555),
                    (Milestone:6800,Origin:1757,Entry:35,Destination:33,Departure:1557),
                    (Milestone:6900,Origin:1600,Entry:34,Destination:36,Departure:1555)};
                foreach(var journey in journeys) {
                    var row=rows.Single(s=>s.GetProperty("milestone").GetInt32()==journey.Milestone);
                    TestAssert.Equal(journey.Destination,row.GetProperty("worldLocations")[0].GetInt32());
                    TestAssert.True(row.TryGetProperty("worldWaypoints",out var waypoints));
                    TestAssert.Equal(journey.Entry,waypoints.EnumerateArray().Single().GetInt32());
                    var gates=row.GetProperty("worldDepartureFields").EnumerateArray().Select(f=>f.GetInt32()).ToArray();
                    TestAssert.Equal(journey.Origin<0?1:2,gates.Length);
                    TestAssert.True(gates.Contains(journey.Departure));
                    if(journey.Origin>=0)TestAssert.True(gates.Contains(journey.Origin));

                    var stage=new StoryStage{Milestone=journey.Milestone,Goal=row.GetProperty("goal").GetString(),
                        GoalFields=row.GetProperty("fields").EnumerateArray().Select(v=>v.GetInt32()).ToArray(),WorldDepartureFields=gates,
                        WorldLocations=new[]{journey.Destination},WorldWaypoints=new[]{journey.Entry}};
                    int entryField=journey.Entry==34?1557:1555;
                    var transit=new[]{new StoryTransit{From=1555,To=1556},new StoryTransit{From=1556,To=1557},
                        new StoryTransit{From=1557,To=1556},new StoryTransit{From=1556,To=1555}};
                    var catalog=new StoryTargetCatalog(new[]{stage},transit);
                    var world=TestFixtures.Context("world:1");world.StoryProgress=journey.Milestone-1;
                    var entry=WorldTown(world,journey.Entry,"Conde Petie Mountain Path",journey.Entry);
                    var destination=WorldTown(world,journey.Destination,stage.Goal,journey.Destination);
                    var choices=new CatalogResult();choices.Targets.Add(entry);
                    catalog.Append(world,choices,-1);TestAssert.Equal(entry.Id,choices.Targets.Last().PhysicalIdentity);
                    choices=new CatalogResult();choices.Targets.Add(entry);choices.Targets.Add(destination);
                    catalog.Append(world,choices,-1);TestAssert.Equal(3,choices.Targets.Count);TestAssert.Equal(destination.Id,choices.Targets.Last().PhysicalIdentity);

                    AssertStoryExit(entryField,9003,1556);
                    AssertStoryExit(1556,entryField,journey.Departure);
                    AssertStoryExit(journey.Departure,1556,9003);
                    if(journey.Origin>=0)AssertStoryExit(journey.Origin,1556,9003);

                    void AssertStoryExit(int field,int back,int forward)
                    {
                        var context=TestFixtures.Context("field:"+field);context.StoryProgress=world.StoryProgress;
                        var backward=FieldExit(context,1,back);
                        var recommended=FieldExit(context,2,forward);
                        var input=new CatalogResult();input.Targets.Add(backward);input.Targets.Add(recommended);
                        catalog.Append(context,input,field);TestAssert.Equal(3,input.Targets.Count);
                        TestAssert.Equal(recommended.Id,input.Targets.Last().PhysicalIdentity);
                    }
                }
            }
        }
        public static void MemoriaStoryUsesThePortalBeforeAndAfterNovaDragon()
        {
            using(var data=Data()) {
                var stages=data.RootElement.GetProperty("stages").EnumerateArray().Where(row=>row.GetProperty("milestone").GetInt32()>=11200)
                    .Select(row=>new StoryStage{Milestone=row.GetProperty("milestone").GetInt32(),Goal=row.GetProperty("goal").GetString(),
                        Instruction=row.GetProperty("instruction").GetString(),WorldPlaces=row.GetProperty("worldPlaces").EnumerateArray().Select(v=>v.GetString()).ToArray(),
                        WorldLocations=row.GetProperty("worldLocations").EnumerateArray().Select(v=>v.GetInt32()).ToArray(),RequiredVehicle=row.GetProperty("vehicle").GetString(),
                        WorldSearchHint=row.TryGetProperty("worldSearchHint",out var hint)?hint.GetString():null}).ToArray();
                var catalog=new StoryTargetCatalog(stages,null);
                var context=TestFixtures.Context("world:1");
                context.Transport=new TransportIdentity(TransportKind.Invincible,10,9);
                var ground=WorldTown(context,36,"Iifa Tree",36);
                var portal=WorldTown(context,53,"Memoria",-1);
                foreach(int progress in new[]{11100,11200,11615}) {
                    context.StoryProgress=progress;
                    var input=new CatalogResult();input.Targets.Add(ground);
                    catalog.Append(context,input,-1);
                    TestAssert.Equal(2,input.Targets.Count);
                    TestAssert.False(input.Targets[1].PhysicalId.HasValue);
                    TestAssert.True(input.Targets[1].Label.Contains("in the sky above the Iifa Tree"));
                    input=new CatalogResult();input.Targets.Add(ground);input.Targets.Add(portal);
                    catalog.Append(context,input,-1);
                    TestAssert.Equal(3,input.Targets.Count);
                    TestAssert.Equal(portal.Id,input.Targets[2].PhysicalIdentity);
                    TestAssert.Equal(-1,input.Targets[2].NativeLocationId);
                }
            }
        }
        public static void BlueNarcissStagesBindTheCorrectLandingAndKeepSandPitsNeutral()
        {
            using(var data=Data()) {
                var rows=data.RootElement.GetProperty("stages").EnumerateArray().ToDictionary(r=>r.GetProperty("milestone").GetInt32());
                foreach(int milestone in new[]{9410,9420,9430,9450}) {
                    TestAssert.Equal(WorldBoatLanding.VillageShore,rows[milestone].GetProperty("boatLanding").GetInt32());
                    TestAssert.Equal(30,rows[milestone].GetProperty("worldLocations")[0].GetInt32());
                }
                TestAssert.Equal(WorldBoatLanding.PalaceShore,rows[9510].GetProperty("boatLanding").GetInt32());
                TestAssert.True(rows[9510].GetProperty("worldLocations").EnumerateArray().Select(r=>r.GetInt32()).SequenceEqual(new[]{64,65,66,67}));
                TestAssert.Equal(1450,rows[9510].GetProperty("worldDepartureFields")[0].GetInt32());
                TestAssert.False(rows[9515].TryGetProperty("boatLanding",out _));
                TestAssert.Equal(27,rows[9515].GetProperty("worldLocations")[0].GetInt32());
                foreach(int progress in new[]{9400,9410,9420,9430,9449})TestAssert.Equal(WorldBoatLanding.VillageShore,WorldBoatLanding.Destination(progress));
                TestAssert.Equal(WorldBoatLanding.PalaceShore,WorldBoatLanding.Destination(9450));
                TestAssert.Equal(WorldBoatLanding.PalaceShore,WorldBoatLanding.Destination(9509));
                TestAssert.Equal(-1,WorldBoatLanding.Destination(9510));
            }
        }
        private static NavigationTarget WorldTown(NavigationContext context,int id,string label,int nativeLocation)
        {
            return new NavigationTarget{Id=new TargetIdentity(TargetKind.WorldLocation,context.LogicalMapKey,id),NativeLocationId=nativeLocation,
                Generation=context.Generation,Category=NavigationCategory.Towns,Label=label,ApproachCandidates=new[]{TestFixtures.P(8,8)}};
        }
        private static NavigationTarget FieldExit(NavigationContext context,int id,int next)
        {
            return new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,context.LogicalMapKey,id),Generation=context.Generation,
                Category=NavigationCategory.Exits,Label="Exit "+id,NextFields=new[]{next},ApproachCandidates=new[]{TestFixtures.P(7,7)}};
        }
        public static void ApostrophesDoNotDropNativeWorldBindings()
        {
            using(var data=Data()) {
                var rows=data.RootElement.GetProperty("stages").EnumerateArray().ToArray();
                foreach(int milestone in new[]{3710,3720,3730,3740})
                    TestAssert.True(rows.Single(r=>r.GetProperty("milestone").GetInt32()==milestone).GetProperty("worldLocations").EnumerateArray().Select(v=>v.GetInt32()).SequenceEqual(new[]{16,20,59}));
                foreach(int milestone in new[]{10500,10595})
                    TestAssert.Equal(44,rows.Single(r=>r.GetProperty("milestone").GetInt32()==milestone).GetProperty("worldLocations")[0].GetInt32());
                TestAssert.Equal(21,rows.Single(r=>r.GetProperty("milestone").GetInt32()==5900).GetProperty("worldLocations")[0].GetInt32());
                var ipsen=rows.Single(r=>r.GetProperty("milestone").GetInt32()==10500);
                var stage=new StoryStage{Milestone=10500,Goal=ipsen.GetProperty("goal").GetString(),WorldLocations=ipsen.GetProperty("worldLocations").EnumerateArray().Select(v=>v.GetInt32()).ToArray()};
                var context=TestFixtures.Context("world:1");context.StoryProgress=10400;context.Transport=new TransportIdentity(TransportKind.HildaGarde,9,9);
                var input=new CatalogResult();input.Targets.Add(new NavigationTarget{Id=new TargetIdentity(TargetKind.WorldLocation,context.LogicalMapKey,44),NativeLocationId=44,Generation=context.Generation,Category=NavigationCategory.Towns,Label="Château d'Ipsen"});
                new StoryTargetCatalog(new[]{stage},null).Append(context,input,-1);
                TestAssert.Equal(2,input.Targets.Count);TestAssert.True(input.Targets[1].PhysicalId.HasValue);TestAssert.Equal(44,input.Targets[1].PhysicalIdentity.NativeId);
            }
        }
    }
}
