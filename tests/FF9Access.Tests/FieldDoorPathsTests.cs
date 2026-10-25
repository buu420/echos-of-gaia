using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldDoorPathsTests
    {
        private static readonly FieldDoorOpcodes Ops=new FieldDoorOpcodes{MapJump=43,WorldJump=182,JumpPosition=164,MoveQuad=158,Move=35,Requests=new[]{16,18,20},Animations=new[]{95,96,97,99,100,101,202,231},InteractionBarriers=new[]{31,32,149,150,208,84,124},PlainPassive=new[]{45,171,34,39,253,169,236,197},DisableControl=45,PreloadHint=253};
        private static ScriptInstruction[] Code()
        {
            var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir!=null && !Directory.Exists(Path.Combine(dir.FullName,"Assembly-CSharp")))dir=dir.Parent;
            using(var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.FullName,"tests/FF9Access.Tests/Fixtures/field-exit-mixed-path-619.json")))) {
                TestAssert.Equal("b97c53476bf1b41c6cf60b10152009a90e70ce021c3caf4250fa12105510c370",json.RootElement.GetProperty("entrySha256").GetString());
                return json.RootElement.GetProperty("Push").EnumerateArray().Select(v=>{var i=new ScriptInstruction(v.GetProperty("Offset").GetInt32(),v.GetProperty("NextOffset").GetInt32(),v.GetProperty("Opcode").GetInt32()) {
                    Arguments=v.GetProperty("Arguments").EnumerateArray().Select(a=>a.ValueKind==JsonValueKind.Null?(int?)null:a.GetInt32()).ToArray(),
                    ArgumentExpressions=v.GetProperty("ArgumentExpressions").EnumerateArray().Select(a=>a.ValueKind==JsonValueKind.Null?null:a.EnumerateArray().Select(b=>b.GetByte()).ToArray()).ToArray()
                };i.Argument=i.Constant(0);i.Expression=i.ArgumentExpression(0);return i;}).ToArray();
            }
        }
        private static ScriptSnapshot State(int mode)
        {
            var map=new byte[1024];map[31]=(byte)mode;map[17]=255;map[159>>3]|=(byte)(1<<(159&7));map[167>>3]|=(byte)(1<<(167&7));
            return new ScriptSnapshot(new byte[2048],map,new byte[64]){UserControl=true};
        }
        public static void ActualPlainBranchSurvivesAnUnrelatedAnimatedBranch()
        {
            var code=Code();TestAssert.True(FieldDoorEvidence.HasWalkOut(code,null,Ops));TestAssert.False(FieldDoorEvidence.HasPlainWalkOut(code,null,Ops));TestAssert.Equal(0,FieldDoorEvidence.Animations(code,null,Ops).Length);
            TestAssert.True(FieldDoorEvidence.CurrentPlainWalkOut(code,State(1),Ops,(uid,type)=>uid==250 && type==1?(int?)-1301:null));
        }
        public static void AnimatedModeWrongHeightAndUnknownObjectStateStayUnresolved()
        {
            TestAssert.False(FieldDoorEvidence.CurrentPlainWalkOut(Code(),State(0),Ops,(uid,type)=>uid==250 && type==1?(int?)-1301:null));
            TestAssert.False(FieldDoorEvidence.CurrentPlainWalkOut(Code(),State(1),Ops,(uid,type)=>uid==250 && type==1?(int?)-1200:null));
            TestAssert.False(FieldDoorEvidence.CurrentPlainWalkOut(Code(),State(1),Ops,null));
        }
    }
}
