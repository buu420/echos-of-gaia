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
                foreach(int milestone in new[]{10030,10050}) {
                    var meeting=root.GetProperty("stages").EnumerateArray().Single(s=>s.GetProperty("milestone").GetInt32()==milestone);
                    TestAssert.Equal(2169,meeting.GetProperty("fields")[0].GetInt32());
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
