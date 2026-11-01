using System;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldTalkScriptTests
    {
        private static bool Read(byte[] script)
        {
            return FieldTalkScript.IsTalkable(script,4);
        }
        private static byte[] Script(byte first=4,byte second=4)
        { var data=new byte[15];data[1]=1;data[2]=3;data[4]=4;data[13]=first;data[14]=second;return data; }
        public static void OriginalTalkHandlerRemainsReadableDuringAdditionalCommand()
        { var original=Script(5);var unrelatedAdditionalCommand=new byte[]{1,4};TestAssert.True(Read(original));TestAssert.False(Read(unrelatedAdditionalCommand)); }
        public static void TwoReturnsAreNotTalkable() { TestAssert.False(Read(Script())); }
        public static void EitherNativeNonreturnByteEnablesTalk() { TestAssert.True(Read(Script(5)));TestAssert.True(Read(Script(4,5))); }
        public static void EmptyAndTruncatedScriptsDoNotThrow()
        { TestAssert.False(Read(null));for(int length=0;length<14;length++){var full=Script();var data=new byte[length];Array.Copy(full,data,length);TestAssert.False(Read(data));} }
        public static void InvalidHeaderOffsetsDoNotBecomeInteractions()
        { var data=Script(5);data[4]=255;data[5]=255;TestAssert.False(Read(data));data=Script(5);data[4]=0;TestAssert.False(Read(data));data=Script(5);data[1]=255;TestAssert.False(Read(data)); }
        public static void AbsentTalkHandlerIsNotTalkable() { var data=Script(5);data[2]=2;TestAssert.False(Read(data)); }
    }
}
