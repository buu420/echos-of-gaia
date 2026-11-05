using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    // Region-mediated people: the game's own QuadTalkableData (EventEngineUtils) or a single direct
    // request proves which actor a Confirm region talks to. Presence follows that proof and live native
    // state; the player's current facing only decides readiness ("turn to face"), never presence.
    internal static class ObjectInteractionTests
    {
        private const string ClerkHash="d94cab6b54986da3e58750315c3c8e9b0ddb37de51e2bf083eb9251b86ceaeef",CounterHash="661a7278d98db4ef3d9ce50086f872b801d6ca43d1cdff69b0fedd0ed97ed04c";
        private static readonly NavigationPoint[] Counter={new NavigationPoint(0,0,0),new NavigationPoint(200,0,0),new NavigationPoint(200,0,100),new NavigationPoint(0,0,100)};
        private static FieldInteractionPresence Live(NavigationPoint actor)
        {return new FieldInteractionPresence{ActorRendered=true,ActorObserved=true,ActorLevel=7,LiveRegions=1,RegionLevel=7,RegionTalkable=true,MapTalkPermitted=true,RegionGuard=FieldInteractionState.Unknown,ActorPosition=actor,Region=Counter};}
        public static void TrenoStadiumClerkUsesTheNativeCounterRegion()
        {
            var clerk=FieldInteractionBindings.FindActor(903,6,ClerkHash,11,6);
            TestAssert.True(clerk!=null);
            TestAssert.Equal(12,clerk.RegionSid);TestAssert.Equal(CounterHash,clerk.RegionHash);
            TestAssert.Equal(16,clerk.RequestedTag);TestAssert.Equal(FieldInteractionEvidence.Native,clerk.Evidence);
            TestAssert.True(FieldInteractionBindings.FindRegion(903,12,CounterHash)==clerk);
        }
        public static void LazyCarpenterOnTheScaffoldUsesItsSingleRequestingRegion()
        {
            var carpenter=FieldInteractionBindings.FindActor(2102,2,"19f8c18e0a42d76c64cd0bce423959a2500af37aa6097cb6b1bc4aedd036c89e",103,2);
            TestAssert.True(carpenter!=null);TestAssert.Equal(7,carpenter.RegionSid);TestAssert.Equal(19,carpenter.RequestedTag);
            TestAssert.Equal(FieldInteractionEvidence.Script,carpenter.Evidence);TestAssert.Equal(4,carpenter.RequestedLevel);
        }
        public static void InnCounterReachedThroughTheDirectorNamesTheInnkeeper()
        {
            // 2103 region 8 checks the player's own direction (QuadTalkableData names the controlled
            // party member); its director function 11 requests the innkeeper's function 12.
            var innkeeper=FieldInteractionBindings.FindActor(2103,5,"17d008ffeb21e681aa0447aae0357f39f600a711ba2dbc14031b6a307e9d758b",111,5);
            TestAssert.True(innkeeper!=null);TestAssert.Equal(8,innkeeper.RegionSid);TestAssert.Equal(FieldInteractionEvidence.NativeDirection,innkeeper.Evidence);
        }
        public static void NativeCounterProofIsNotLimitedByTheReviewDistance()
        {
            var clerk=FieldInteractionBindings.FindActor(903,6,ClerkHash,11,6);
            var farBehindCounter=new NavigationPoint(100,0,1344); // 1244 units beyond the polygon
            TestAssert.True(FieldInteractionBindings.WithinReach(clerk,farBehindCounter,Counter));
            TestAssert.True(FieldInteractionBindings.Present(clerk,Live(farBehindCounter)));
            var scripted=FieldInteractionBindings.All.Single(b=>b.Field==2102 && b.RegionSid==7);
            TestAssert.True(FieldInteractionBindings.WithinReach(scripted,new NavigationPoint(100,0,700),Counter));
            TestAssert.False(FieldInteractionBindings.WithinReach(scripted,new NavigationPoint(100,0,900),Counter));
            TestAssert.False(FieldInteractionBindings.WithinReach(clerk,new NavigationPoint(float.NaN,0,0),Counter));
            TestAssert.False(FieldInteractionBindings.WithinReach(clerk,farBehindCounter,new NavigationPoint[2]));
        }
        public static void PresenceFailsClosedOnEveryNativeGate()
        {
            var clerk=FieldInteractionBindings.FindActor(903,6,ClerkHash,11,6);
            var inside=new NavigationPoint(100,0,50);
            TestAssert.True(FieldInteractionBindings.Present(clerk,Live(inside)));
            Action<Action<FieldInteractionPresence>> refused=change=>{var live=Live(inside);change(live);TestAssert.False(FieldInteractionBindings.Present(clerk,live));};
            refused(p=>p.ActorRendered=false);
            refused(p=>p.ActorObserved=false);
            refused(p=>p.ActorLevel=1);          // Request needs the requested level (1) below the actor's level
            refused(p=>p.LiveRegions=0);
            refused(p=>p.LiveRegions=2);         // a second live instance is not the reviewed layout
            refused(p=>p.RegionLevel=1);
            refused(p=>p.RegionTalkable=false);  // talk handler that only returns
            refused(p=>p.MapTalkPermitted=false);
            refused(p=>p.RegionGuard=FieldInteractionState.Unavailable);
            refused(p=>p.ActorPosition=new NavigationPoint(float.PositiveInfinity,0,0));
            TestAssert.False(FieldInteractionBindings.Present(null,Live(inside)));
            TestAssert.False(FieldInteractionBindings.Present(clerk,null));
        }
        public static void QuadTalkMapRuleMirrorsCheckQuadTalk()
        {
            // EventCollision.CheckQuadTalk: field 2108 region sid 7 and field 2504 region sid 9 never talk.
            TestAssert.False(FieldInteractionBindings.QuadTalkPermitted(2108,7));
            TestAssert.False(FieldInteractionBindings.QuadTalkPermitted(2504,9));
            TestAssert.True(FieldInteractionBindings.QuadTalkPermitted(2108,6));
            TestAssert.True(FieldInteractionBindings.QuadTalkPermitted(2504,8));
            TestAssert.True(FieldInteractionBindings.QuadTalkPermitted(903,12));
        }
        public static void FacingAwayFromACounterAsksToTurnInsteadOfDroppingThePerson()
        {
            var turn=FieldInteractionBindings.Readiness(true,true,true,false,false);
            TestAssert.True(turn.SelectedCollision && turn.InRange && turn.Talkable);TestAssert.False(turn.FacingWithinArc);
            var ready=FieldInteractionBindings.Readiness(true,true,true,true,true);
            TestAssert.True(ready.SelectedCollision && ready.InRange && ready.Talkable && ready.FacingWithinArc);
            // Facing allowed but no presented bubble: nothing is offered yet, and no turn is requested.
            TestAssert.False(FieldInteractionBindings.Readiness(true,true,true,true,false).Talkable);
            // Another region owns Confirm here, or the player stands outside: not in range.
            TestAssert.False(FieldInteractionBindings.Readiness(false,true,true,true,true).InRange);
            TestAssert.False(FieldInteractionBindings.Readiness(true,false,true,true,true).InRange);
            TestAssert.False(FieldInteractionBindings.Readiness(true,true,false,false,false).Talkable);
        }
        public static void ContactPropIsReadyOnlyWhileItsOwnBubbleIsPresented()
        {
            // A chest's push handler shows FICON for itself and waits for Confirm while the player touches it.
            var ready=FieldInteractionRules.PushReadiness(true,true,true,true);
            TestAssert.True(ready.SelectedCollision && ready.InRange && ready.Talkable && ready.FacingWithinArc);
            // Touching but no bubble (opened chest, guard returned): nothing is offered, and no turn is requested.
            var quiet=FieldInteractionRules.PushReadiness(true,true,true,false);
            TestAssert.True(quiet.SelectedCollision && quiet.InRange);TestAssert.False(quiet.Talkable || quiet.FacingWithinArc);
            TestAssert.False(FieldInteractionRules.PushReadiness(false,true,true,true).InRange);
            TestAssert.False(FieldInteractionRules.PushReadiness(true,false,true,true).InRange);
            TestAssert.False(FieldInteractionRules.PushReadiness(true,true,false,true).Talkable);
        }
        public static void PublishedBindingsCarryRequestedFunctionAndEvidence()
        {
            using(var json=JsonDocument.Parse(File.ReadAllText(Repo("docs/accessibility/field-interaction-evidence.json")))) {
                TestAssert.Equal(3,json.RootElement.GetProperty("schema").GetInt32());
                foreach(var item in json.RootElement.GetProperty("bindings").EnumerateArray()) {
                    var compiled=FieldInteractionBindings.All.Single(b=>b.Field==item.GetProperty("field").GetInt32() && b.RegionSid==item.GetProperty("region").GetInt32());
                    TestAssert.Equal(item.GetProperty("requestedTag").GetInt32(),compiled.RequestedTag);
                    TestAssert.Equal(item.GetProperty("evidence").GetString(),compiled.Evidence==FieldInteractionEvidence.Native?"native":compiled.Evidence==FieldInteractionEvidence.NativeDirection?"native-direction":"script");
                }
            }
        }
        public static void BoundRegionStoryJoinsThePersonWithoutReplacingItsOwn()
        {
            var person=new FieldTargetObservation{StoryMilestones=new[]{3110},NextFields=new int[0],StoryAction="own action"};
            var region=new FieldTargetObservation{StoryMilestones=new[]{7600,3110},NextFields=new[]{554},StoryAction="region action",StoryStepMilestone=7600,StoryVerb="Talk"};
            FieldBoundStory.Merge(region,person);
            TestAssert.Equal("3110,7600",string.Join(",",person.StoryMilestones.OrderBy(m=>m)));
            TestAssert.Equal("554",string.Join(",",person.NextFields));
            TestAssert.Equal("own action",person.StoryAction);
            TestAssert.Equal(7600,person.StoryStepMilestone);TestAssert.Equal("Talk",person.StoryVerb);
            var stepped=new FieldTargetObservation{StoryStepMilestone=1420,StoryVerb="Examine"};
            FieldBoundStory.Merge(region,stepped);
            TestAssert.Equal(1420,stepped.StoryStepMilestone);TestAssert.Equal("Examine",stepped.StoryVerb);
            TestAssert.Equal("region action",stepped.StoryAction);
            var untouched=new FieldTargetObservation{StoryMilestones=new[]{5}};
            FieldBoundStory.Merge(null,untouched);
            TestAssert.Equal("5",string.Join(",",untouched.StoryMilestones));
            var empty=new FieldTargetObservation();
            FieldBoundStory.Merge(new FieldTargetObservation(),empty);
            TestAssert.True(empty.StoryMilestones==null && empty.NextFields==null && empty.StoryStepMilestone==Int32.MinValue);
        }
        private static string Repo(string relative)
        {
            var dir=new DirectoryInfo(AppContext.BaseDirectory);
            while(dir!=null && !Directory.Exists(Path.Combine(dir.FullName,"Assembly-CSharp")))dir=dir.Parent;
            if(dir==null){dir=new DirectoryInfo(Environment.CurrentDirectory);while(dir!=null && !Directory.Exists(Path.Combine(dir.FullName,"Assembly-CSharp")))dir=dir.Parent;}
            if(dir==null)throw new Exception("Repository root not found");
            return Path.Combine(dir.FullName,relative);
        }
    }
}
