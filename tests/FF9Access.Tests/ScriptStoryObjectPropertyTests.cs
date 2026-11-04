using System;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    // Exact touch function (tag 2) of EVT_ALEX1_AC_ENT_2F (154) region 8,
    // sha256 42fd082594fcb46f3ce55a6cad15248b00e194956ef8ca130dde3e0d42cf3ff6 (installed US script).
    // 38: EXPR {OBJ(250).1 -100 LT}; 47: JMP_IFN +160 -> 210. The upper branch reaches MAPJUMP 153 at 203,
    // the lower branch MAPJUMP 158 at 363. OBJ(250).1 is the native -round(pos.y) of the controlled player.
    internal static class ScriptStoryObjectPropertyTests
    {
        private const int MapJump=43,WorldJump=182;
        private static ScriptInstruction E(int offset,int next,params byte[] bytes){return new ScriptInstruction(offset,next,5,bytes);}
        private static ScriptInstruction I(int offset,int next,int opcode,int? argument=null){return new ScriptInstruction(offset,next,opcode,argument);}
        private static ScriptInstruction[] Region8()
        {
            var code=new System.Collections.Generic.List<ScriptInstruction>{
                E(30,34,0x7a,0x02,0x7f),I(34,37,3,1),I(37,38,4),
                E(38,47,0x78,0xfa,0x01,0x7d,0x9c,0xff,0x18,0x7f),I(47,50,2,160)};
            foreach(int start in new[]{50,210}) {
                int d=start-50;
                code.AddRange(new[]{I(50+d,51+d,164),I(51+d,52+d,158),E(52+d,60+d,0xc5,0x9e,0x7d,0x00,0x00,0x2c,0x7f),E(60+d,68+d,0xc5,0x9f,0x7d,0x01,0x00,0x20,0x7f),I(68+d,71+d,2,19),
                    I(71+d,72+d,45),E(72+d,80+d,0xc5,0x90,0x7d,0x00,0x00,0x20,0x7f),I(80+d,83+d,2,4),I(83+d,84+d,171),I(84+d,87+d,1,3),I(87+d,90+d,34),I(90+d,93+d,39),I(93+d,98+d,253),
                    E(98+d,106+d,0xd9,0x15,0x7d,(byte)(start==50?0x99:0x9e),0x00,0x2c,0x7f),E(106+d,114+d,0xd5,0x11,0x7d,0xff,0x00,0x20,0x7f),I(114+d,117+d,2,15),
                    E(117+d,132+d,0x7a,0x0c,0x7d,0xa0,0x00,0x15,0x7a,0x0d,0x7d,0x70,0x00,0x15,0x66,0x7f),I(132+d,135+d,169),I(135+d,145+d,236),I(145+d,148+d,34),
                    E(148+d,156+d,0xc5,0xa7,0x7d,0x01,0x00,0x20,0x7f),I(156+d,159+d,2,14),I(159+d,165+d,197),E(165+d,173+d,0xc5,0xa7,0x7d,0x00,0x00,0x2c,0x7f),
                    E(173+d,181+d,0xc5,0xa2,0x7d,0x00,0x00,0x20,0x7f),I(181+d,184+d,2,0),E(184+d,192+d,0xc5,0xa3,0x7d,0x00,0x00,0x20,0x7f),I(192+d,195+d,2,0),
                    E(195+d,203+d,0xd8,0x02,0x7d,(byte)(start==50?0x2d:0x2c),0x01,0x2c,0x7f),I(203+d,207+d,MapJump,start==50?153:158)});
                if(start==50)code.Add(I(207,210,1,157));
            }
            code.Add(I(367,368,4));
            return code.ToArray();
        }
        private static ScriptStoryEvidence Read(Func<int,int,int?> objectProperty)
        {
            var snapshot=new ScriptSnapshot(new byte[16],new byte[64],null);
            var start=new ScriptStoryFunction{Identity=8*256+2,Code=Region8(),Snapshot=snapshot};
            return objectProperty==null?ScriptStoryAnalyzer.Read(start,null,MapJump,WorldJump,new[]{16,18,20}):ScriptStoryAnalyzer.Read(start,null,MapJump,WorldJump,new[]{16,18,20},objectProperty);
        }
        public static void UnknownPlayerPositionProvesNoDestination()
        {
            TestAssert.Equal(0,Read(null).Destinations.Count);
            TestAssert.Equal(0,Read((uid,property)=>null).Destinations.Count);
        }
        public static void ProspectivePlayerHeightSelectsTheNativeBranch()
        {
            // Lower floor (pos.y ~ 0): OBJ(250).1 = 0, not < -100 -> MAPJUMP 158.
            var lower=Read((uid,property)=>uid==250 && property==1?(int?)0:null);
            TestAssert.Equal(1,lower.Destinations.Count);TestAssert.True(lower.Destinations.Contains(158));
            // Upper floor (pos.y ~ 1000): OBJ(250).1 = -1000 -> MAPJUMP 153.
            var upper=Read((uid,property)=>uid==250 && property==1?(int?)-1000:null);
            TestAssert.Equal(1,upper.Destinations.Count);TestAssert.True(upper.Destinations.Contains(153));
        }
        public static void OtherObjectsAndPropertiesStayUnknown()
        {
            int calls=0;
            var evidence=Read((uid,property)=>{calls++;return uid==250 && property==0?(int?)0:null;});
            TestAssert.Equal(0,evidence.Destinations.Count);TestAssert.True(calls>0);
        }
    }
}
