using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldInteractionTests
    {
        // EBin opcode values (EBin.event_code_binary): FICON 0x68, MES 0x1F, REQ 0x10.
        private static readonly FieldInteractionOpcodes Ops=new FieldInteractionOpcodes{IconOpcode=0x68};
        private static string RepoFile(string relative)
        {
            var dir=new DirectoryInfo(AppContext.BaseDirectory);
            while(dir!=null && !Directory.Exists(Path.Combine(dir.FullName,"Assembly-CSharp")))dir=dir.Parent;
            // Isolated builds may live outside the repository; then the working directory must be inside it.
            if(dir==null){dir=new DirectoryInfo(Environment.CurrentDirectory);while(dir!=null && !Directory.Exists(Path.Combine(dir.FullName,"Assembly-CSharp")))dir=dir.Parent;}
            if(dir==null)throw new Exception("Repository root not found");
            return Path.Combine(dir.FullName,relative);
        }
        private static ScriptInstruction[] Instructions(JsonElement values)
        {
            return values.EnumerateArray().Select(v=>{var item=new ScriptInstruction(v.GetProperty("Offset").GetInt32(),v.GetProperty("NextOffset").GetInt32(),v.GetProperty("Opcode").GetInt32()) {
                Arguments=v.GetProperty("Arguments").EnumerateArray().Select(a=>a.ValueKind==JsonValueKind.Null?(int?)null:a.GetInt32()).ToArray(),
                ArgumentExpressions=v.GetProperty("ArgumentExpressions").EnumerateArray().Select(a=>a.ValueKind==JsonValueKind.Null?null:a.EnumerateArray().Select(b=>b.GetByte()).ToArray()).ToArray()
            };item.Argument=item.Constant(0);item.Expression=item.ArgumentExpression(0);return item;}).ToArray();
        }
        private static ScriptInstruction[] ChestPush()
        {
            using(var json=JsonDocument.Parse(File.ReadAllText(RepoFile("tests/FF9Access.Tests/Fixtures/field-interaction-guards.json")))) {
                var chest=json.RootElement.GetProperty("chest");
                TestAssert.Equal("9b143afa3d201c6a33d1257a84a2b3cc4d310cde46282d3c3e802cc08a0c23e5",chest.GetProperty("entrySha256").GetString());
                return Instructions(chest.GetProperty("Push"));
            }
        }
        private static ScriptSnapshot ChestState(bool opened,bool ready)
        {
            var global=new byte[2048];if(opened)global[7172>>3]|=(byte)(1<<(7172&7));
            var instance=new byte[64];instance[0]=(byte)(ready?1:0);
            return new ScriptSnapshot(global,new byte[1024],instance){UserControl=true};
        }
        public static void OpenedChestPushHandlerReturnsBeforeAnyInteraction()
        {
            TestAssert.Equal(FieldInteractionState.Unavailable,FieldInteractionGuard.Evaluate(ChestPush(),ChestState(true,true),Ops));
        }
        public static void UnopenedChestPushHandlerReachesTheNativeIcon()
        {
            TestAssert.Equal(FieldInteractionState.Available,FieldInteractionGuard.Evaluate(ChestPush(),ChestState(false,true),Ops));
        }
        public static void UnreadableGuardStateStaysManualInteraction()
        {
            // An empty global buffer makes the opened flag unreadable: never a false "no interaction".
            var snapshot=new ScriptSnapshot(new byte[0],new byte[0],new byte[64]){UserControl=true};
            TestAssert.Equal(FieldInteractionState.Unknown,FieldInteractionGuard.Evaluate(ChestPush(),snapshot,Ops));
        }
        public static void GuardSnapshotIsNotMutatedByEvaluation()
        {
            var global=new byte[2048];var snapshot=new ScriptSnapshot(global,new byte[1024],new byte[64]){UserControl=true};
            // MAP var assignment followed by return: a side effect may drive another script, so it is not proven unavailable.
            var code=new[]{Expr(0,new byte[]{0xC5,10,0x7D,1,0,44,0x7F}),new ScriptInstruction(9,10,4)};
            TestAssert.Equal(FieldInteractionState.Unknown,FieldInteractionGuard.Evaluate(code,snapshot,Ops));
            TestAssert.Equal((byte)0,global[10]);
        }
        public static void QuestionAndExclamationBubblesAreInteractions()
        {
            var off=new ScriptInstruction(0,2,0x68){Arguments=new int?[]{0},ArgumentExpressions=new byte[1][]};off.Argument=0;
            var on=new ScriptInstruction(0,2,0x68){Arguments=new int?[]{1},ArgumentExpressions=new byte[1][]};on.Argument=1;
            var snapshot=new ScriptSnapshot(new byte[16],new byte[16],new byte[16]){UserControl=true};
            // Native FICON(0) is the visible question-mark bubble, not an icon clear.
            TestAssert.Equal(FieldInteractionState.Available,FieldInteractionGuard.Evaluate(new[]{off,new ScriptInstruction(2,3,4)},snapshot,Ops));
            TestAssert.Equal(FieldInteractionState.Available,FieldInteractionGuard.Evaluate(new[]{on,new ScriptInstruction(2,3,4)},snapshot,Ops));
            TestAssert.Equal(FieldInteractionState.Unknown,FieldInteractionGuard.Evaluate(new ScriptInstruction[0],snapshot,Ops));
        }
        private static ScriptInstruction Expr(int offset,byte[] expression)
        {
            var item=new ScriptInstruction(offset,offset+1+expression.Length,5){Arguments=new int?[]{null},ArgumentExpressions=new[]{expression}};item.Expression=expression;return item;
        }
        public static void NativeTalkGatesMatchEventCollision()
        {
            var open=new FieldInteractionGates{HasTalkHandler=true,TalkBytesActive=true,MapTalkAllowed=true,Level=7};
            TestAssert.Equal(FieldInteractionState.Available,FieldInteractionRules.Talk(open,FieldInteractionState.Available));
            TestAssert.Equal(FieldInteractionState.Unknown,FieldInteractionRules.Talk(open,FieldInteractionState.Unknown));
            var inhibited=Copy(open);inhibited.Flags=FieldInteractionFlags.TalkInhibit;
            TestAssert.Equal(FieldInteractionState.Unavailable,FieldInteractionRules.Talk(inhibited,FieldInteractionState.Available));
            var busy=Copy(open);busy.Level=1;
            TestAssert.Equal(FieldInteractionState.Unavailable,FieldInteractionRules.Talk(busy,FieldInteractionState.Available));
            var map=Copy(open);map.MapTalkAllowed=false;
            TestAssert.Equal(FieldInteractionState.Unavailable,FieldInteractionRules.Talk(map,FieldInteractionState.Available));
            var noop=Copy(open);noop.TalkBytesActive=false;
            TestAssert.Equal(FieldInteractionState.Unavailable,FieldInteractionRules.Talk(noop,FieldInteractionState.Available));
            noop.ReviewedListener=true;
            TestAssert.Equal(FieldInteractionState.Available,FieldInteractionRules.Talk(noop,FieldInteractionState.Available));
            TestAssert.Equal(FieldInteractionState.None,FieldInteractionRules.Talk(new FieldInteractionGates{Level=7,MapTalkAllowed=true},FieldInteractionState.Available));
        }
        public static void NativePushGatesMatchWalkMeshCollision()
        {
            var push=new FieldInteractionGates{HasPushHandler=true,MapPushAllowed=true,Level=7};
            TestAssert.Equal(FieldInteractionState.Available,FieldInteractionRules.Push(push,FieldInteractionState.Available));
            // WalkMesh.Collision push mode skips only when the player carries 0x4 AND the actor carries 0x2.
            var actorOnly=Copy(push);actorOnly.Flags=FieldInteractionFlags.CollisionInhibitControl;
            TestAssert.Equal(FieldInteractionState.Available,FieldInteractionRules.Push(actorOnly,FieldInteractionState.Available));
            actorOnly.PlayerFlags=FieldInteractionFlags.CollisionInhibitOther;
            TestAssert.Equal(FieldInteractionState.Unavailable,FieldInteractionRules.Push(actorOnly,FieldInteractionState.Available));
            var map=Copy(push);map.MapPushAllowed=false;
            TestAssert.Equal(FieldInteractionState.Unavailable,FieldInteractionRules.Push(map,FieldInteractionState.Available));
            TestAssert.Equal(FieldInteractionState.Unavailable,FieldInteractionRules.Push(push,FieldInteractionState.Unavailable));
        }
        public static void ReviewedPlayerOwnedConversationSurvivesAnEmptyActorHandler()
        {
            var gates=new FieldInteractionGates{HasTalkHandler=true,ReviewedListener=true,MapTalkAllowed=true,Level=7};
            ScriptInstruction[] handler;
            using(var json=JsonDocument.Parse(File.ReadAllText(RepoFile("tests/FF9Access.Tests/Fixtures/field-interaction-guards.json")))) {
                var listener=json.RootElement.GetProperty("ticketListener");
                TestAssert.Equal("cd021ee4b7483b7b783e7477c5c9ffac67b67e5de29476232ff3f0bab7cf3c9c",listener.GetProperty("entrySha256").GetString());
                handler=Instructions(listener.GetProperty("Talk"));
            }
            var empty=FieldInteractionGuard.Evaluate(handler,new ScriptSnapshot(new byte[16],new byte[16],new byte[16]){UserControl=true},Ops);
            TestAssert.Equal(FieldInteractionState.Unavailable,empty);
            TestAssert.Equal(FieldInteractionState.Unknown,FieldInteractionRules.Talk(gates,empty));
            gates.Flags=FieldInteractionFlags.TalkInhibit;
            TestAssert.Equal(FieldInteractionState.Unavailable,FieldInteractionRules.Talk(gates,empty));
            gates.Flags=0;gates.Level=1;
            TestAssert.Equal(FieldInteractionState.Unavailable,FieldInteractionRules.Talk(gates,empty));
        }
        public static void CombinedInteractionNeverTurnsUnknownIntoNoInteraction()
        {
            TestAssert.True(FieldInteractionRules.Interactive(FieldInteractionState.Unknown,FieldInteractionState.Unavailable).Value);
            TestAssert.True(FieldInteractionRules.Interactive(FieldInteractionState.Unavailable,FieldInteractionState.Available).Value);
            TestAssert.False(FieldInteractionRules.Interactive(FieldInteractionState.Unavailable,FieldInteractionState.Unavailable).Value);
            TestAssert.False(FieldInteractionRules.Interactive(FieldInteractionState.Unavailable,FieldInteractionState.None).Value);
            // No native channel at all leaves the existing observation untouched.
            TestAssert.False(FieldInteractionRules.Interactive(FieldInteractionState.None,FieldInteractionState.None).HasValue);
        }
        private static FieldInteractionGates Copy(FieldInteractionGates g)
        {return new FieldInteractionGates{HasTalkHandler=g.HasTalkHandler,TalkBytesActive=g.TalkBytesActive,ReviewedListener=g.ReviewedListener,MapTalkAllowed=g.MapTalkAllowed,HasPushHandler=g.HasPushHandler,MapPushAllowed=g.MapPushAllowed,Level=g.Level,Flags=g.Flags,PlayerFlags=g.PlayerFlags};}
        public static void BindingTableMatchesPublishedEvidenceExactly()
        {
            using(var json=JsonDocument.Parse(File.ReadAllText(RepoFile("docs/accessibility/field-interaction-evidence.json")))) {
                var published=json.RootElement.GetProperty("bindings").EnumerateArray().Select(b=>b.GetProperty("field").GetInt32()+":"+b.GetProperty("actor").GetInt32()+":"+b.GetProperty("actorSha256").GetString()+":"+b.GetProperty("model").GetInt32()+":"+b.GetProperty("actorUid").GetInt32()+":"+b.GetProperty("region").GetInt32()+":"+b.GetProperty("regionSha256").GetString()).OrderBy(s=>s).ToList();
                var compiled=FieldInteractionBindings.All.Select(b=>b.Field+":"+b.ActorSid+":"+b.ActorHash+":"+b.Model+":"+b.ActorUid+":"+b.RegionSid+":"+b.RegionHash).OrderBy(s=>s).ToList();
                TestAssert.Equal(string.Join("\n",published),string.Join("\n",compiled));
                TestAssert.Equal(16,compiled.Count);
                TestAssert.Equal(700f,(float)json.RootElement.GetProperty("maxActorDistance").GetInt32());
                foreach(var excluded in json.RootElement.GetProperty("excluded").EnumerateArray())
                    TestAssert.True(FieldInteractionBindings.FindRegion(excluded.GetProperty("field").GetInt32(),excluded.GetProperty("region").GetInt32(),excluded.GetProperty("regionSha256").GetString())==null ||
                        FieldInteractionBindings.FindRegion(excluded.GetProperty("field").GetInt32(),excluded.GetProperty("region").GetInt32(),excluded.GetProperty("regionSha256").GetString()).ActorSid!=excluded.GetProperty("actor").GetInt32());
            }
            TestAssert.Equal(FieldInteractionBindings.All.Count,FieldInteractionBindings.All.Select(b=>b.Field+":"+b.RegionSid).Distinct().Count());
        }
        public static void BindingRequiresExactFieldEntryHashModelAndUid()
        {
            const string actor="ad233fce7a9a270bd2bad900bf8e7e9fa1519244aeea35e904e09ef484ccaa98",region="9eb30b5a3d9f8150c6d59d121484ea34921b0847ddc6cbfbb32db53fdacb9a2b";
            var found=FieldInteractionBindings.FindActor(562,4,actor,197,4);
            TestAssert.True(found!=null && found.RegionSid==9 && found.RegionHash==region);
            TestAssert.True(FieldInteractionBindings.FindActor(562,4,actor,198,4)==null);
            TestAssert.True(FieldInteractionBindings.FindActor(562,4,actor.Replace('a','b'),197,4)==null);
            TestAssert.True(FieldInteractionBindings.FindActor(562,4,actor,197,5)==null);
            TestAssert.True(FieldInteractionBindings.FindActor(2162,4,actor,197,4)==null);
            TestAssert.True(FieldInteractionBindings.FindRegion(562,9,region.Replace('a','c'))==null);
        }
        public static void BindingFailsClosedWhenTheActorLeavesTheTalkSpot()
        {
            var counter=new[]{new NavigationPoint(436,0,452),new NavigationPoint(436,0,32),new NavigationPoint(316,0,32),new NavigationPoint(316,0,452)};
            TestAssert.True(FieldInteractionBindings.WithinReach(new NavigationPoint(380,0,200),counter));
            TestAssert.True(FieldInteractionBindings.WithinReach(new NavigationPoint(1000,0,300),counter));   // 564 units behind the counter
            TestAssert.False(FieldInteractionBindings.WithinReach(new NavigationPoint(1200,0,300),counter));  // 764 units
            TestAssert.False(FieldInteractionBindings.WithinReach(new NavigationPoint(float.NaN,0,0),counter));
            TestAssert.False(FieldInteractionBindings.WithinReach(new NavigationPoint(380,0,200),new NavigationPoint[2]));
        }
        public static void BusyRequestedSpeakerDoesNotClaimInteractionReadiness()
        {
            var binding=FieldInteractionBindings.All.Single(b=>b.Field==806 && b.RegionSid==6);
            TestAssert.Equal(6,binding.RequestedLevel);
            TestAssert.False(FieldInteractionBindings.CanRequest(binding,6));
            TestAssert.False(FieldInteractionBindings.CanRequest(binding,1));
            TestAssert.True(FieldInteractionBindings.CanRequest(binding,7));
            using(var json=JsonDocument.Parse(File.ReadAllText(RepoFile("docs/accessibility/field-interaction-evidence.json"))))
                foreach(var item in json.RootElement.GetProperty("bindings").EnumerateArray()) {
                    var record=FieldInteractionBindings.All.Single(b=>b.Field==item.GetProperty("field").GetInt32() && b.RegionSid==item.GetProperty("region").GetInt32());
                    TestAssert.Equal(item.GetProperty("requestedLevel").GetInt32(),record.RequestedLevel);
                }
        }
    }
}
