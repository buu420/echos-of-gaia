using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    // Reviewed background objects: a Confirm region whose object is drawn in the field's own artwork
    // (installed scene render reviewed per camera). Listed with its visible form only while that artwork,
    // the region's native Confirm permission and a walkable first-Talk approach are all current.
    internal static class BackgroundObjectTests
    {
        private static FieldBackgroundObject Lever(){return FieldBackgroundObjects.All.Single(o=>o.Field==951 && o.RegionSid==7);}
        private static FieldBackgroundPresence Live()
        {return new FieldBackgroundPresence{CanControl=true,SceneMatches=true,CameraMatches=true,ArtOnScreen=true,LiveRegions=1,RegionLevel=7,RegionTalkable=true,MapTalkPermitted=true,TalkGuard=FieldInteractionState.Unknown,Approaches=3};}
        public static void GarganRooLeverIsAReviewedBackgroundObject()
        {
            var lever=Lever();TestAssert.Equal("Lever",lever.Label);
            TestAssert.True(lever.Cameras.Contains(1));
            TestAssert.True(FieldBackgroundObjects.Find(951,"FBG_N21_GRGR_MAP421_GR_LEF_0",7,lever.RegionHash)==lever);
            TestAssert.True(FieldBackgroundObjects.Find(951,"FBG_N21_GRGR_MAP421_GR_LEF_0",7,"changed")==null);
            TestAssert.True(FieldBackgroundObjects.Find(951,"another scene",7,lever.RegionHash)==null);
        }
        public static void PresenceFailsClosedOnEveryGate()
        {
            var lever=Lever();TestAssert.True(FieldBackgroundObjects.Present(lever,Live()));
            Action<Action<FieldBackgroundPresence>> refused=change=>{var live=Live();change(live);TestAssert.False(FieldBackgroundObjects.Present(lever,live));};
            refused(p=>p.CanControl=false);
            refused(p=>p.SceneMatches=false);        // another background or walkmesh than the reviewed render
            refused(p=>p.CameraMatches=false);       // camera whose render was not reviewed
            refused(p=>p.ArtOnScreen=false);         // the drawn object is outside the current view
            refused(p=>p.LiveRegions=0);
            refused(p=>p.LiveRegions=2);
            refused(p=>p.RegionLevel=1);             // Request(quad,1,3) needs 1 < level
            refused(p=>p.RegionTalkable=false);      // Confirm handler that only returns
            refused(p=>p.MapTalkPermitted=false);    // CheckQuadTalk
            refused(p=>p.TalkGuard=FieldInteractionState.Unavailable);
            refused(p=>p.Approaches=0);              // no walkable point where this region is the first Talk region
            TestAssert.False(FieldBackgroundObjects.Present(null,Live()));TestAssert.False(FieldBackgroundObjects.Present(lever,null));
        }
        public static void AnUnreviewedPresentedPromptIsAnHonestInteractionPoint()
        {
            TestAssert.Equal("Interaction point",FieldBackgroundObjects.PromptLabel(null));
            TestAssert.Equal("Interaction point",FieldBackgroundObjects.PromptLabel(""));
            TestAssert.Equal("Ladder",FieldBackgroundObjects.PromptLabel("Ladder"));
        }
        public static void TheFossilRooFlowerAndBoardingSpotNeedTheirOwnCurrentCues()
        {
            var flower=FieldBackgroundObjects.All.Single(o=>o.Field==1405 && o.RegionSid==8);
            var boarding=FieldBackgroundObjects.All.Single(o=>o.Field==1405 && o.RegionSid==7);
            TestAssert.Equal(537,flower.LandmarkModel);
            var live=Live();live.CurrentGroundCue=true;
            TestAssert.True(FieldBackgroundObjects.Present(boarding,live));
            TestAssert.False(FieldBackgroundObjects.Present(flower,live));
            live.LandmarkRendered=true;TestAssert.True(FieldBackgroundObjects.Present(flower,live));
            live.CurrentGroundCue=false;
            TestAssert.False(FieldBackgroundObjects.Present(flower,live));TestAssert.False(FieldBackgroundObjects.Present(boarding,live));
        }
        public static void PublishedReviewMatchesTheCompiledRowsAndItsProof()
        {
            using(var json=JsonDocument.Parse(File.ReadAllText(Repo("docs/accessibility/reviewed-background-objects.json")))) {
                var root=json.RootElement;TestAssert.Equal(1,root.GetProperty("schema").GetInt32());
                var objects=root.GetProperty("objects").EnumerateArray().ToList();
                TestAssert.Equal(objects.Count,FieldBackgroundObjects.All.Count);
                foreach(var item in objects) {
                    var row=FieldBackgroundObjects.Find(item.GetProperty("field").GetInt32(),item.GetProperty("scene").GetString(),item.GetProperty("sid").GetInt32(),item.GetProperty("hash").GetString());
                    TestAssert.True(row!=null);
                    TestAssert.Equal(item.GetProperty("label").GetString(),row.Label);
                    TestAssert.Equal(item.GetProperty("background").GetString(),row.Background);TestAssert.Equal(item.GetProperty("walkmesh").GetString(),row.Walkmesh);
                    TestAssert.Equal(item.TryGetProperty("groundCue",out var cue) && cue.GetBoolean(),row.GroundCue);
                    TestAssert.Equal(item.TryGetProperty("touchCue",out var touchCue) && touchCue.GetBoolean(),row.TouchCue);
                    if(item.TryGetProperty("landmark",out var landmark)) {
                        TestAssert.Equal(landmark.GetProperty("sid").GetInt32(),row.LandmarkSid);
                        TestAssert.Equal(landmark.GetProperty("model").GetInt32(),row.LandmarkModel);
                        TestAssert.Equal(landmark.GetProperty("hash").GetString(),row.LandmarkHash);
                    }
                    TestAssert.Equal(string.Join(",",item.GetProperty("cameras").EnumerateArray().Select(c=>c.GetInt32())),string.Join(",",row.Cameras));
                    var proof=item.GetProperty("proof");
                    TestAssert.True(proof.GetProperty("image").GetString().EndsWith(".png"));TestAssert.Equal(64,proof.GetProperty("sha256").GetString().Length);
                }
            }
        }
        public static void LabelsDescribeVisibleFormOnly()
        {
            var forbidden=new[]{"GEO_","FBG_","EVT_","Gil","Potion","Ether","Card","Elixir","Zidane","Vivi","Dagger","Garnet","Steiner","Freya","Quina","Eiko","Amarant","Kuja"};
            foreach(var row in FieldBackgroundObjects.All) {
                TestAssert.False(string.IsNullOrEmpty(row.Label));
                foreach(var word in forbidden)TestAssert.False(row.Label.Contains(word));
                TestAssert.False(row.Label.Any(char.IsDigit));
            }
        }
        public static void EveryStudiedCaseHasExactlyOneEvidenceClass()
        {
            using(var json=JsonDocument.Parse(File.ReadAllText(Repo("docs/accessibility/reviewed-background-objects.json")))) {
                var cases=json.RootElement.GetProperty("cases").EnumerateArray().ToList();
                var keys=new HashSet<string>();
                foreach(var item in cases) {
                    string key=item.GetProperty("set").GetString()+":"+item.GetProperty("field").GetInt32()+":"+item.GetProperty("sid").GetInt32();
                    TestAssert.True(keys.Add(key));
                    TestAssert.False(string.IsNullOrEmpty(item.GetProperty("class").GetString()));
                    TestAssert.False(string.IsNullOrEmpty(item.GetProperty("evidence").GetString()));
                }
                // 78 background hotspots, 64 region speech cases, 46 generic actors and 16 model-less actors.
                TestAssert.Equal(78,cases.Count(c=>c.GetProperty("set").GetString()=="hotspot"));
                TestAssert.Equal(64,cases.Count(c=>c.GetProperty("set").GetString()=="speech"));
                TestAssert.Equal(62,cases.Count(c=>c.GetProperty("set").GetString()=="actor"));
                // Hidden pickups never carry a name or outcome.
                foreach(var item in cases.Where(c=>c.GetProperty("class").GetString()=="hidden-pickup"))TestAssert.Equal("Interaction point",item.GetProperty("label").GetString());
            }
        }
        public static void LoopWitnessesForRootKeepHiddenPickupsPromptOnly()
        {
            using(var json=JsonDocument.Parse(File.ReadAllText(Repo("docs/accessibility/reviewed-background-objects.json")))) {
                var loops=json.RootElement.GetProperty("loopWitnesses").EnumerateArray().ToList();
                TestAssert.Equal(244,loops.Count);
                var pickups=loops.Where(l=>l.GetProperty("class").GetString()=="hidden-pickup-loop").ToList();
                TestAssert.Equal(210,pickups.Count);
                foreach(var item in pickups) {
                    TestAssert.Equal("Interaction point",item.GetProperty("label").GetString());
                    TestAssert.True(item.GetProperty("promptOnly").GetBoolean());
                }
                // The generated runtime data imports these native witnesses.
                // Hidden pickups can never acquire an unobserved position.
                using(var reviewed=JsonDocument.Parse(File.ReadAllText(Repo("docs/accessibility/field-loop-prompts.json")))) {
                    var known=reviewed.RootElement.GetProperty("objects").EnumerateArray().ToDictionary(o=>o.GetProperty("field").GetInt32()+":"+o.GetProperty("sid").GetInt32());
                    foreach(var item in loops)TestAssert.True(known.ContainsKey(item.GetProperty("field").GetInt32()+":"+item.GetProperty("sid").GetInt32()));
                    foreach(var item in pickups) {
                        var row=known[item.GetProperty("field").GetInt32()+":"+item.GetProperty("sid").GetInt32()];
                        TestAssert.True(row.GetProperty("promptOnly").GetBoolean());TestAssert.False(row.TryGetProperty("anchor",out _));
                        TestAssert.Equal(-1,row.GetProperty("uid").GetInt32());TestAssert.Equal("Interaction point",row.GetProperty("label").GetString());
                        TestAssert.Equal(item.GetProperty("hash").GetString(),row.GetProperty("hash").GetString());
                    }
                }
            }
        }
        private static string Repo(string relative)
        {
            var dir=new DirectoryInfo(AppContext.BaseDirectory);
            while(dir!=null && !Directory.Exists(Path.Combine(dir.FullName,"Assembly-CSharp")))dir=dir.Parent;
            if(dir==null)throw new Exception("Repository root not found");
            return Path.Combine(dir.FullName,relative);
        }
    }
}
