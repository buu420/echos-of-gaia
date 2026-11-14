using Memoria.Accessibility.AudioDescriptions;

namespace FF9Access.Tests
{
    internal static class GulugWellRopeCueTests
    {
        public static void OnlyTheHeldDirectionLoopHasRopeControls()
        {
            TestAssert.False(GulugWellRopeCue.AcceptsDirection(567,13053));
            TestAssert.True(GulugWellRopeCue.AcceptsDirection(571,13053));
            TestAssert.True(GulugWellRopeCue.AcceptsDirection(842,13073));
            TestAssert.True(GulugWellRopeCue.AcceptsDirection(1063,13055));
            TestAssert.False(GulugWellRopeCue.AcceptsDirection(1066,13059));
            TestAssert.False(GulugWellRopeCue.AcceptsDirection(1098,13055));
            TestAssert.False(GulugWellRopeCue.AcceptsDirection(1306,200));
        }

        public static void HintRetriesUnacceptedSpeechAndReadsOncePerClimb()
        {
            var cue=new GulugWellRopeCue();
            int attempts=0;
            TestAssert.Equal("Hold Down or Left to descend. Hold Up or Right to climb.",GulugWellRopeCue.Controls);
            cue.Observe(true,text=>++attempts==2);
            cue.Observe(true,text=>++attempts==2);
            cue.Observe(true,text=>++attempts==2);
            TestAssert.Equal(2,attempts);
            cue.Observe(false,text=>{attempts++;return true;});
            cue.Observe(true,text=>{attempts++;return true;});
            TestAssert.Equal(3,attempts);
        }
    }
}
