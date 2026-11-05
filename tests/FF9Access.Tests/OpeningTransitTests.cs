using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class OpeningNativeFixture
    {
        internal static JsonDocument Scripts(){return JsonDocument.Parse(Assembly.GetExecutingAssembly().GetManifestResourceStream("Opening.StreetScripts.json"));}
        internal static JsonDocument Story(){return JsonDocument.Parse(Assembly.GetExecutingAssembly().GetManifestResourceStream("StoryNavigation.json"));}
        internal static ScriptInstruction[] Code(JsonElement row)
        {return row.GetProperty("Code").EnumerateArray().Select(v=>{var i=new ScriptInstruction(v.GetProperty("Offset").GetInt32(),v.GetProperty("NextOffset").GetInt32(),v.GetProperty("Opcode").GetInt32()){
            Arguments=v.GetProperty("Arguments").EnumerateArray().Select(a=>a.ValueKind==JsonValueKind.Null?(int?)null:a.GetInt32()).ToArray(),
            ArgumentExpressions=v.GetProperty("ArgumentExpressions").EnumerateArray().Select(a=>a.ValueKind==JsonValueKind.Null?null:a.EnumerateArray().Select(b=>b.GetByte()).ToArray()).ToArray()};i.Argument=i.Constant(0);i.Expression=i.ArgumentExpression(0);return i;}).ToArray();}
        internal static ScriptStoryEvidence Read(JsonElement root,JsonElement row,int wipe=0,int progress=1000)
        {
            byte[] global=new byte[4096],map=new byte[256];global[0]=(byte)progress;global[1]=(byte)(progress>>8);map[17]=(byte)wipe;map[19]=128;
            var before=(byte[])map.Clone();var g=(byte[])global.Clone();
            var result=ScriptStoryAnalyzer.Read(new ScriptStoryFunction{Identity=row.GetProperty("Sid").GetInt32()*256+row.GetProperty("Tag").GetInt32(),Code=Code(row),Snapshot=new ScriptSnapshot(global,map,new byte[64]){UserControl=true}},
                null,root.GetProperty("MapJump").GetInt32(),root.GetProperty("WorldJump").GetInt32(),root.GetProperty("Requests").EnumerateArray().Select(v=>v.GetInt32()).ToArray());
            TestAssert.True(map.SequenceEqual(before) && global.SequenceEqual(g));return result;
        }
        internal static StoryTargetCatalog Catalog(JsonElement root)
        {
            var stages=root.GetProperty("stages").EnumerateArray().Select(s=>new StoryStage{Milestone=s.GetProperty("milestone").GetInt32(),Goal=s.GetProperty("goal").GetString(),Instruction=s.GetProperty("instruction").GetString(),GoalFields=Ints(s,"fields"),ApproachFields=Ints(s,"approachFields"),DepartureFields=Ints(s,"departureFields"),WorldLocations=Ints(s,"worldLocations"),WorldPlaces=s.GetProperty("worldPlaces").EnumerateArray().Select(v=>v.GetString()).ToArray()}).ToArray();
            return new StoryTargetCatalog(stages,root.GetProperty("transit").EnumerateArray().Select(t=>new StoryTransit{From=t.GetProperty("source").GetInt32(),To=t.GetProperty("destination").GetInt32()}).ToArray(),OpeningStoryCheckpoints.All);
        }
        internal static int[] Ints(JsonElement root,string property){JsonElement values;return root.TryGetProperty(property,out values)?values.EnumerateArray().Select(v=>v.GetInt32()).ToArray():new int[0];}
        internal static string Location(int field){return field==103?"Alexandria/Square":field==104?"Alexandria/Ticket Booth":field==105?"Alexandria/Alley":field==108?"Alexandria/Item Shop":"Alexandria/Main Street";}
        internal static CatalogResult Presented(JsonElement root,NavigationContext context,int field)
        {
            var result=new CatalogResult();
            foreach(var row in root.GetProperty("Functions").EnumerateArray().Where(r=>r.GetProperty("Field").GetInt32()==field && r.GetProperty("Tag").GetInt32()==2)) {
                int sid=row.GetProperty("Sid").GetInt32();var evidence=Read(root,row,0,context.StoryProgress);
                result.Targets.Add(new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,context.LogicalMapKey,sid),Generation=context.Generation,Category=NavigationCategory.Exits,Label="Shown exit "+sid,IsInteractive=true,Position=TestFixtures.P(1,1),ApproachCandidates=new[]{TestFixtures.P(1,1)},NextFields=evidence.Destinations.ToArray()});
            }return result;
        }
    }
    internal static class OpeningTransitTests
    {
        private static void Require(bool value,string reason){if(!value)throw new Exception(reason);}
        public static void ActualStreetScriptsRetainTransitThroughTheVisualWipe()
        {
            using(var json=OpeningNativeFixture.Scripts()) {
                var root=json.RootElement;
                foreach(var test in new[]{new[]{100,15,101},new[]{101,16,102},new[]{102,8,103},new[]{102,9,101},new[]{102,10,108},new[]{108,4,102}})
                foreach(int wipe in new[]{0,255}) {
                    var row=root.GetProperty("Functions").EnumerateArray().Single(r=>r.GetProperty("Field").GetInt32()==test[0] && r.GetProperty("Sid").GetInt32()==test[1]);
                    Require(OpeningNativeFixture.Read(root,row,wipe).Destinations.Contains(test[2]),"Actual field"+test[0]+"/sid"+test[1]+" lost its destination while wipe="+wipe);
                }
            }
        }
        public static void TicketObjectiveUsesTheActualLocalStreetExitAcrossZonesAndShopDetours()
        {
            using(var scripts=OpeningNativeFixture.Scripts())using(var story=OpeningNativeFixture.Story()) {
                var catalog=OpeningNativeFixture.Catalog(story.RootElement);
                foreach(var leg in new[]{new[]{100,15},new[]{101,16},new[]{102,8},new[]{108,4},new[]{102,8}}) {
                    var c=TestFixtures.Context("field:"+leg[0]+":SCENE");c.StoryProgress=1000;
                    var input=OpeningNativeFixture.Presented(scripts.RootElement,c,leg[0]);catalog.Append(c,input,leg[0],OpeningNativeFixture.Location);
                    var selected=input.Targets.Last(t=>t.Category==NavigationCategory.Story);
                    Require(selected.PhysicalId.HasValue,"Story did not select a local exit in field"+leg[0]);
                    TestAssert.Equal(leg[1],selected.PhysicalIdentity.NativeId);TestAssert.Equal(c.LogicalMapKey,selected.PhysicalIdentity.MapKey);
                    if(leg[0]==102)Require(selected.Label.Contains("Alexandria/Square"),"Travel leg did not identify the next area");
                }
            }
        }
        public static void MissingPhysicalExitStillIdentifiesTheNextRequiredArea()
        {
            using(var story=OpeningNativeFixture.Story()) {
                var c=TestFixtures.Context("field:102:SCENE");c.StoryProgress=1000;var input=new CatalogResult();
                OpeningNativeFixture.Catalog(story.RootElement).Append(c,input,102,OpeningNativeFixture.Location);
                var selected=input.Targets.Single();TestAssert.True(selected.PhysicalId==null && selected.ApproachCandidates.Length==0);
                Require((selected.Label+selected.Availability).Contains("Alexandria/Square"),"No-route information omitted the next area");
            }
        }
        public static void RealSquareExitIdsNeverInventADirectWalkingExitIntoTheBooth()
        {
            using(var scripts=OpeningNativeFixture.Scripts())using(var story=OpeningNativeFixture.Story()) {
                var c=TestFixtures.Context("field:103:SCENE");c.StoryProgress=1150;
                var input=OpeningNativeFixture.Presented(scripts.RootElement,c,103);
                TestAssert.False(input.Targets.Any(t=>(t.NextFields??new int[0]).Contains(104)));
                OpeningNativeFixture.Catalog(story.RootElement).Append(c,input,103,OpeningNativeFixture.Location);
                var selected=input.Targets.Last(t=>t.Category==NavigationCategory.Story);Require(selected.PhysicalId.HasValue,"Post-ticket alley exit missing");
                TestAssert.Equal(22,selected.PhysicalIdentity.NativeId);TestAssert.True(selected.Label.Contains("Alley"));
            }
        }
        public static void InformationalStoryCommandsReadTheNextAreaWithoutStartingARoute()
        {
            using(var story=OpeningNativeFixture.Story()) {
                var c=TestFixtures.Context("field:102:SCENE");c.StoryProgress=1000;var input=new CatalogResult();OpeningNativeFixture.Catalog(story.RootElement).Append(c,input,102,OpeningNativeFixture.Location);
                var coordinator=new NavigationCoordinator();coordinator.Tick(c,new[]{NavigationCommand.PreviousCategory},input,null);
                var update=coordinator.Tick(c,new[]{NavigationCommand.ToggleAutomatic},input,null);
                TestAssert.Equal(RouteOperation.None,update.RouteOperation);TestAssert.False(update.Session.Mode==RouteMode.Moving);TestAssert.True(update.Messages.Any(m=>m.Contains("Alexandria/Square")));
                update=coordinator.Tick(c,new[]{NavigationCommand.StartGuidance},input,null);TestAssert.Equal(RouteOperation.None,update.RouteOperation);TestAssert.True(update.Messages.Any(m=>m.Contains("Alexandria/Square")));
                update=coordinator.Tick(c,new[]{NavigationCommand.ToggleGuidance},input,null);TestAssert.False(update.GuidanceEnabled);TestAssert.True(update.Messages.Contains("Guidance off."));
            }
        }
        public static void StoryRetainsItsLocalLegAcrossNativeStreetMapChanges()
        {
            using(var scripts=OpeningNativeFixture.Scripts())using(var story=OpeningNativeFixture.Story()) {
                var catalog=OpeningNativeFixture.Catalog(story.RootElement);var coordinator=new NavigationCoordinator();long generation=0;
                foreach(var leg in new[]{new[]{100,15},new[]{101,16},new[]{102,8},new[]{108,4},new[]{102,8}}) {
                    var c=TestFixtures.Context("field:"+leg[0]+":SCENE");c.Generation=++generation;c.StoryProgress=1000;
                    var input=OpeningNativeFixture.Presented(scripts.RootElement,c,leg[0]);catalog.Append(c,input,leg[0],OpeningNativeFixture.Location);
                    var update=coordinator.Tick(c,generation==1?new[]{NavigationCommand.PreviousCategory}:new NavigationCommand[0],input,null);
                    TestAssert.Equal(NavigationCategory.Story,update.Category);TestAssert.Equal(c.LogicalMapKey,update.Selected.PhysicalIdentity.MapKey);TestAssert.Equal(leg[1],update.Selected.PhysicalIdentity.NativeId);TestAssert.Equal(RouteOperation.None,update.RouteOperation);
                }
            }
        }
        public static void AMapChangeToAnInformationalStoryStepDoesNotScheduleAGeometryPlan()
        {
            using(var scripts=OpeningNativeFixture.Scripts())using(var story=OpeningNativeFixture.Story()) {
                var c=TestFixtures.Context("field:102:SCENE");c.StoryProgress=1000;var catalog=OpeningNativeFixture.Catalog(story.RootElement);
                var input=OpeningNativeFixture.Presented(scripts.RootElement,c,102);catalog.Append(c,input,102,OpeningNativeFixture.Location);
                var coordinator=new NavigationCoordinator();coordinator.Tick(c,new[]{NavigationCommand.PreviousCategory},input,null);coordinator.Tick(c,new[]{NavigationCommand.StartGuidance},input,null);
                c.Generation++;c.LogicalMapKey="field:103:SCENE";input=new CatalogResult();catalog.Append(c,input,103,OpeningNativeFixture.Location);
                var update=coordinator.Tick(c,new NavigationCommand[0],input,null);TestAssert.True(update.Selected.PhysicalId==null);TestAssert.Equal(RouteOperation.None,update.RouteOperation);
            }
        }
    }
    internal static class OpeningRouteMetadataTests
    {
        public static void TicketNavigationApproachesTheSquareBeforeTheDialogueRoom()
        {
            using(var story=OpeningNativeFixture.Story()) {
                var stage=story.RootElement.GetProperty("stages").EnumerateArray().Single(s=>s.GetProperty("milestone").GetInt32()==1150);
                TestAssert.True(OpeningNativeFixture.Ints(stage,"approachFields").SequenceEqual(new[]{103}));
                TestAssert.True(OpeningNativeFixture.Ints(stage,"fields").SequenceEqual(new[]{104}));
            }
        }
        public static void UnpresentedBoothInSquareKeepsTheObjectiveLocal()
        {
            using(var story=OpeningNativeFixture.Story()) {
                var c=TestFixtures.Context("field:103:SCENE");c.StoryProgress=1000;var input=new CatalogResult();
                OpeningNativeFixture.Catalog(story.RootElement).Append(c,input,103,OpeningNativeFixture.Location);
                TestAssert.True(input.Targets.Single().Availability.Contains("next story step is in this area"));
                TestAssert.True(input.Targets.Single().PhysicalId==null);
            }
        }
    }
}
