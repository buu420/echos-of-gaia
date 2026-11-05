using System.Collections.Generic;
using Memoria.Accessibility.AudioDescriptions;

namespace FF9Access.Tests
{
    internal static class TimedTurtleCueTests
    {
        public static void AFailedSubmissionNeverReplaysAStaleTurn()
        {
            var reader=new TimedTurtleCue();var spoken=new List<string>();
            reader.Observe(0,text=>false);
            reader.Observe(1,Accept);
            reader.Observe(1,Accept);
            TestAssert.Equal(1,spoken.Count);TestAssert.Equal("Watching",spoken[0]);
            reader.Observe(0,Accept);
            TestAssert.Equal("Away",spoken[1]);
            bool Accept(string text){spoken.Add(text);return true;}
        }

        public static void NoCueWhileTheNativeGameIsHiddenAndTheNextAttemptReadsCurrentState()
        {
            var reader=new TimedTurtleCue();int announcements=0;
            reader.Observe(0,Accept);reader.Observe(null,Accept);reader.Observe(255,Accept);
            TestAssert.Equal(1,announcements);
            reader.Observe(0,Accept);TestAssert.Equal(2,announcements);
            bool Accept(string text){announcements++;return true;}
        }
    }
}
