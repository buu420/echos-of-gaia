using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldLoopPromptTests
    {
        private const int Icon=104;
        private static byte[] Hex(string hex){return Convert.FromHexString(hex);}
        private static ScriptInstruction Expr(int ip,int next,string bytes){return new ScriptInstruction(ip,next,5){Expression=Hex(bytes)};}
        // Desert Palace 2213, entry 4, tag 1. The native wait returns to
        // offset 11 each turn. The prompt precedes the input check at 122.
        private static ScriptInstruction[] Code()
        {
            return new[]{Expr(11,34,"7a027d010020e4ff0d7d00002027c5917d000020277f"),new ScriptInstruction(34,37,2){Argument=188},
                Expr(37,93,"ca0078fa007d7dfe157d08001278fa007d7dfe157d0800121178fa027d7cff157d08001278fa027d7cff157d08001211147d1000122c7f"),
                Expr(93,116,"ca00d5181878fa017dd4fe1978fa017d2c011827277f"),new ScriptInstruction(116,119,2){Argument=106},
                new ScriptInstruction(119,122,Icon){Argument=1},Expr(122,130,"7e00000a004f7f"),new ScriptInstruction(225,228,34)};
        }
        private static bool Prompt(NavigationPoint at,bool alreadyLit=false,bool control=true,int cue=119)
        {
            var globals=new byte[1024];if(alreadyLit)globals[3583>>3]|=(byte)(1<<(3583&7));
            var map=new byte[256];map[24]=50;
            var snapshot=new ScriptSnapshot(globals,map,new byte[16]){UserControl=control};
            return new FieldLoopPrompt(Code()).Presented(11,cue,Icon,snapshot,(uid,property)=>FieldExitDestinations.PlayerProperty(9,at,uid,property));
        }
        public static void TheNativeCandlePromptCanBeProvenBeforeReadingTheConfirmButton()
        {
            TestAssert.True(Prompt(new NavigationPoint(-387,0,-132)));
            TestAssert.False(Prompt(new NavigationPoint(387,0,-132)));
        }
        public static void SameHorizontalPositionOnTheWrongFloorDoesNotOfferTheCandle()
        {
            TestAssert.False(Prompt(new NavigationPoint(-387,500,-132)));
        }
        public static void LightingTheCandleOrLosingControlRetiresItsPrompt()
        {
            var at=new NavigationPoint(-387,0,-132);
            TestAssert.False(Prompt(at,true));TestAssert.False(Prompt(at,false,false));
        }
        public static void AReviewCannotClaimThatThePromptPressedConfirm()
        {
            TestAssert.False(Prompt(new NavigationPoint(-387,0,-132),cue:122));
        }
        public static void AlexandriaGondolaRequiresTheDockStripAndTheArrivedBoat()
        {
            var program=new FieldLoopPrompt(new[]{Expr(15,38,"7a027d01002078fa027d56fa1927d5267d000020277f"),
                new ScriptInstruction(38,41,2){Argument=63},new ScriptInstruction(41,44,Icon){Argument=0}});
            var map=new byte[64];
            bool Presents(int z)=>program.Presented(15,41,Icon,new ScriptSnapshot(null,map,null){UserControl=true},
                (uid,property)=>FieldExitDestinations.PlayerProperty(3,new NavigationPoint(53,0,z),uid,property));
            TestAssert.True(Presents(-1165));TestAssert.False(Presents(-1450));TestAssert.False(Presents(-1460));
            map[38]=2;TestAssert.False(Presents(-1165));
        }
        public static void MemoriaSaveSpheresAndHiddenSavePointsKeepSeparatePresentationRules()
        {
            const string file="docs/accessibility/field-loop-prompts.json";
            string directory=AppContext.BaseDirectory;
            while(directory!=null && !File.Exists(Path.Combine(directory,file)))directory=Path.GetDirectoryName(directory);
            if(directory==null)throw new Exception(file+" not found");
            using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,file)))) {
                var rows=doc.RootElement.GetProperty("objects").EnumerateArray().ToArray();
                foreach(var (field,sid,effect) in new[]{(2901,9,644),(2913,4,646),(2925,4,988)}) {
                    var row=rows.Single(o=>o.GetProperty("field").GetInt32()==field && o.GetProperty("sid").GetInt32()==sid);
                    TestAssert.Equal("Save sphere",row.GetProperty("label").GetString());
                    TestAssert.False(row.TryGetProperty("promptOnly",out var promptOnly) && promptOnly.GetBoolean());
                    TestAssert.Equal(effect,row.GetProperty("sprite")[1].GetInt32());
                    TestAssert.Equal(sid,row.GetProperty("uid").GetInt32());
                }
                foreach(var (field,sid) in new[]{(2905,6),(2909,4),(2916,4),(2919,7)}) {
                    var row=rows.Single(o=>o.GetProperty("field").GetInt32()==field && o.GetProperty("sid").GetInt32()==sid);
                    TestAssert.Equal("Save point",row.GetProperty("label").GetString());
                    TestAssert.True(row.GetProperty("promptOnly").GetBoolean());
                    TestAssert.False(row.TryGetProperty("anchor",out _));
                }
                foreach(var (field,sid) in new[]{(2901,4),(2905,2),(2919,3)}) {
                    var row=rows.Single(o=>o.GetProperty("field").GetInt32()==field && o.GetProperty("sid").GetInt32()==sid);
                    TestAssert.Equal("Interaction point",row.GetProperty("label").GetString());
                    TestAssert.True(row.GetProperty("promptOnly").GetBoolean());
                }
            }
        }
    }
}
