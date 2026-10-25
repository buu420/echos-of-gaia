using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class CapturedDoorScriptsTests
    {
        private static ScriptInstruction[] Instructions(JsonElement values)
        {
            return values.EnumerateArray().Select(v=>{var item=new ScriptInstruction(v.GetProperty("Offset").GetInt32(),v.GetProperty("NextOffset").GetInt32(),v.GetProperty("Opcode").GetInt32()) {
                Arguments=v.GetProperty("Arguments").EnumerateArray().Select(a=>a.ValueKind==JsonValueKind.Null?(int?)null:a.GetInt32()).ToArray(),
                ArgumentExpressions=v.GetProperty("ArgumentExpressions").EnumerateArray().Select(a=>a.ValueKind==JsonValueKind.Null?null:a.EnumerateArray().Select(b=>b.GetByte()).ToArray()).ToArray()
            };item.Argument=item.Constant(0);item.Expression=item.ArgumentExpression(0);return item;}).ToArray();
        }
        public static void NativeHildaTalkCannotAdvertiseItsFuturePlotShortcut()
        {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Field2850.StoryGuard.json"))using(var json=JsonDocument.Parse(stream)) {
                var root=json.RootElement;byte[] global=new byte[4096];global[0]=0xA0;global[1]=0x28; // 10400
                var evidence=ScriptStoryAnalyzer.Read(new ScriptStoryFunction{Identity=2*256+3,Code=Instructions(root.GetProperty("Talk")),Snapshot=new ScriptSnapshot(global,new byte[256],new byte[64])},(uid,tag,caller,snapshot)=>{throw new Exception("A future guarded request was followed");},root.GetProperty("MapJump").GetInt32(),root.GetProperty("WorldJump").GetInt32(),root.GetProperty("Requests").EnumerateArray().Select(v=>v.GetInt32()).ToArray());
                TestAssert.Equal(0,evidence.Destinations.Count);TestAssert.Equal(0,evidence.Milestones.Count);TestAssert.Equal((byte)0xa0,global[0]);
            }
        }
        public static void NativeForestWorldExitRemainsRecognizedAcrossItsSwitchTables()
        {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Field2950.ExitScript.json"))using(var json=JsonDocument.Parse(stream)) {
                var root=json.RootElement;var c=root.GetProperty("Opcodes");
                var codes=new FieldDoorOpcodes{MapJump=c.GetProperty("MapJump").GetInt32(),WorldJump=c.GetProperty("WorldJump").GetInt32(),JumpPosition=c.GetProperty("JumpPosition").GetInt32(),MoveQuad=c.GetProperty("MoveQuad").GetInt32(),Move=c.GetProperty("Move").GetInt32(),Requests=c.GetProperty("Requests").EnumerateArray().Select(v=>v.GetInt32()).ToArray(),Animations=c.GetProperty("Animations").EnumerateArray().Select(v=>v.GetInt32()).ToArray(),InteractionBarriers=c.GetProperty("InteractionBarriers").EnumerateArray().Select(v=>v.GetInt32()).ToArray()};
                var player=new Dictionary<int,IList<ScriptInstruction>>();foreach(var function in root.GetProperty("Player").EnumerateObject())player[int.Parse(function.Name)]=Instructions(function.Value);
                TestAssert.True(FieldDoorEvidence.HasPlainWalkOut(Instructions(root.GetProperty("Touch")),tag=>player.ContainsKey(tag)?player[tag]:new ScriptInstruction[0],codes));
            }
        }
        public static void NativeGuardRoomExitResolvesItsOwnRequestedWalkAndAnimation()
        {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Field150.DoorScript.json"))using(var json=JsonDocument.Parse(stream)) {
                var root=json.RootElement;var c=root.GetProperty("Opcodes");
                var codes=new FieldDoorOpcodes{MapJump=c.GetProperty("MapJump").GetInt32(),WorldJump=c.GetProperty("WorldJump").GetInt32(),JumpPosition=c.GetProperty("JumpPosition").GetInt32(),MoveQuad=c.GetProperty("MoveQuad").GetInt32(),Move=c.GetProperty("Move").GetInt32(),Requests=c.GetProperty("Requests").EnumerateArray().Select(v=>v.GetInt32()).ToArray(),Animations=c.GetProperty("Animations").EnumerateArray().Select(v=>v.GetInt32()).ToArray(),InteractionBarriers=c.GetProperty("InteractionBarriers").EnumerateArray().Select(v=>v.GetInt32()).ToArray()};
                var player=new Dictionary<int,IList<ScriptInstruction>>();foreach(var function in root.GetProperty("Player").EnumerateObject())player[int.Parse(function.Name)]=Instructions(function.Value);
                var touch=Instructions(root.GetProperty("Touch"));
                TestAssert.True(FieldDoorEvidence.HasWalkOut(touch,tag=>player.ContainsKey(tag)?player[tag]:new ScriptInstruction[0],codes));
                var animations=FieldDoorEvidence.Animations(touch,tag=>player.ContainsKey(tag)?player[tag]:new ScriptInstruction[0],codes);
                TestAssert.Equal(1,animations.Length);TestAssert.Equal(0,animations[0]);
                TestAssert.Equal(0,FieldDoorEvidence.Animations(touch,tag=>new ScriptInstruction[0],codes).Length);
            }
        }
    }
}
