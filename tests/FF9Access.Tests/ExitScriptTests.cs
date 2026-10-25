using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class ExitScriptTests
    {
        public static void ObjectInstanceOffsetIsMeasuredInNativeWords()
        {
            byte[] objectBuffer={99,99,99,99,99,99,99,99,1,0,77};
            var instance=ScriptSnapshot.InstanceBytes(objectBuffer,2);var snapshot=new ScriptSnapshot(null,null,instance);
            TestAssert.Equal((int?)1,snapshot.Read(2,7,0));TestAssert.Equal((int?)77,snapshot.Read(2,5,2));
            instance[0]=3;TestAssert.Equal((byte)1,objectBuffer[8]);TestAssert.Equal(0,ScriptSnapshot.InstanceBytes(objectBuffer,3).Length);
        }
        public static void NativeSwitchVariantsChooseOnlyTheCurrentExit()
        {
            foreach(int opcode in new[]{6,11,13}) {
                var expression=new ScriptInstruction(0,10,5,new byte[]{0x7d,83,0,0x7f});
                var table=new ScriptInstruction(10,25,opcode){Arguments=opcode==6?new int?[]{36,83,16}:opcode==11?new int?[]{83,39,19}:new int?[]{83,38,18}};
                var code=new[]{expression,table,new ScriptInstruction(30,31,99),new ScriptInstruction(50,51,4)};
                TestAssert.Equal(ScriptExitOutcome.JumpNow,ExitScriptAnalyzer.Evaluate(code,new ScriptSnapshot(null,null,null),99,100,101,102));
                expression.Expression=new byte[]{0x7d,82,0,0x7f};
                TestAssert.Equal(ScriptExitOutcome.NoJumpNow,ExitScriptAnalyzer.Evaluate(code,new ScriptSnapshot(null,null,null),99,100,101,102));
            }
        }
        public static void NativeVariableTypesUseEngineWidthsAndSignedness()
        {
            byte[] bytes = {0xFF,0xFE,0x80,0x55};
            var snapshot = new ScriptSnapshot(bytes,bytes,bytes);
            foreach(int source in new[]{0,1,2}) {
                TestAssert.Equal((int?)1,snapshot.Read(source,0,7));
                TestAssert.Equal((int?)1,snapshot.Read(source,1,7));
                TestAssert.Equal((int?)-8323329,snapshot.Read(source,2,0));
                TestAssert.Equal((int?)-8323329,snapshot.Read(source,3,0));
                TestAssert.Equal((int?)-1,snapshot.Read(source,4,0));
                TestAssert.Equal((int?)255,snapshot.Read(source,5,0));
                TestAssert.Equal((int?)-257,snapshot.Read(source,6,0));
                TestAssert.Equal((int?)65279,snapshot.Read(source,7,0));
            }
            TestAssert.Equal((int?)null,snapshot.Read(3,7,0));
            TestAssert.Equal((int?)null,snapshot.Read(0,8,0));
        }
        public static void NativeStoryCounterDoesNotConsumeMapIndex()
        {
            byte[] global = {0xB8,0x24,0x7F,0x55}; // counter 9400, adjacent map index
            var code=new[]{new ScriptInstruction(0,8,5,new byte[]{0xdc,0,0x7d,0xb8,0x24,0x20,0x7f}),new ScriptInstruction(8,11,2,1),new ScriptInstruction(11,12,99),new ScriptInstruction(12,13,4)};
            TestAssert.Equal(ScriptExitOutcome.JumpNow,ExitScriptAnalyzer.Evaluate(code,new ScriptSnapshot(global,null,null),99,100,101,102));
            TestAssert.Equal((byte)0x7f,global[2]);
        }
        public static void ShadowWritesPreserveNeighboringNativeVariables()
        {
            byte[] bytes={0xAA,0xBB,0xCC,0xDD};
            foreach(int type in new[]{2,3,4,5,6,7}) {
                var snapshot=new ScriptSnapshot(bytes,null,null);
                TestAssert.True(snapshot.Write(0,type,0,0x123456));
                int width=type==2 || type==3?3:type==4 || type==5?1:2;
                TestAssert.Equal((int?)bytes[width],snapshot.Read(0,5,width));
            }
            TestAssert.Equal((byte)0xaa,bytes[0]);
            TestAssert.False(new ScriptSnapshot(new byte[2],null,null).Write(0,2,0,1));
            TestAssert.False(new ScriptSnapshot(bytes,null,null).Write(0,8,0,1));
        }
        public static void CurrentBranchChoosesExitWithoutChangingState()
        {
            byte[] map = {1}; var snapshot = new ScriptSnapshot(new byte[8], map, new byte[8]);
            var code = new[] { new ScriptInstruction(0, 2, 5, new byte[] {0xC5,0,0x7f}), new ScriptInstruction(2, 5, 2, 3), new ScriptInstruction(5, 6, 99), new ScriptInstruction(8,9,4) };
            TestAssert.Equal(ScriptExitOutcome.JumpNow, ExitScriptAnalyzer.Evaluate(code, snapshot, 99, 100, 101, 102)); TestAssert.Equal((byte)1,map[0]);
            map[0]=0; TestAssert.Equal(ScriptExitOutcome.NoJumpNow, ExitScriptAnalyzer.Evaluate(code,new ScriptSnapshot(new byte[8],map,new byte[8]),99,100,101,102));
        }
        public static void AssignmentWritesOnlyTheShadowStore()
        {
            byte[] map={0}; var snapshot=new ScriptSnapshot(new byte[8],map,new byte[8]);
            var code=new[] {new ScriptInstruction(0,5,5,new byte[]{0xc5,0,0x7d,1,0,0x2c,0x7f}),new ScriptInstruction(5,6,99)};
            TestAssert.Equal(ScriptExitOutcome.JumpNow,ExitScriptAnalyzer.Evaluate(code,snapshot,99,100,101,102)); TestAssert.Equal((byte)0,map[0]);
        }
        public static void RandomOrUnknownConditionDoesNotExposeJump()
        {
            var code=new[] {new ScriptInstruction(0,3,5,new byte[]{0x7a,0,0x7f}),new ScriptInstruction(3,6,3,1),new ScriptInstruction(6,7,99)};
            TestAssert.Equal(ScriptExitOutcome.Unknown,ExitScriptAnalyzer.Evaluate(code,new ScriptSnapshot(null,null,null),99,100,101,102));
        }
        public static void LoopIsBounded()
        {
            var code=new[]{new ScriptInstruction(0,3,1,65533)};
            TestAssert.Equal(ScriptExitOutcome.Unknown,ExitScriptAnalyzer.Evaluate(code,new ScriptSnapshot(null,null,null),99,100,101,102));
        }
    }
}
