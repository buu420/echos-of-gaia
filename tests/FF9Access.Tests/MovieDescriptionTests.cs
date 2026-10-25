using System;
using System.Collections.Generic;
using Memoria.Accessibility.AudioDescriptions;
namespace FF9Access.Tests
{
    internal static class MovieDescriptionTests
    {
        private sealed class Output : IDescriptionPlayback
        {
            public readonly List<string> Events=new List<string>();
            public bool Playing {get;set;}
            public bool Accept=true;
            public bool TryStart(DescriptionCue cue){Events.Add("start:"+cue.Id);if(!Accept)return false;Playing=true;return true;}
            public void Pause(){Events.Add("pause");}
            public void Resume(){Events.Add("resume");}
            public void Stop(){Events.Add("stop");Playing=false;}
        }
        private static DescriptionMovie Movie(){return new DescriptionMovie("FMV001",new[]{new DescriptionCue("one",1,5,2,"An airship crosses the sky."),new DescriptionCue("two",7,11,2,"The young woman turns away.")});}
        private static void Tick(MovieDescriptionSession session,long owner,DescriptionMovie movie,double time,bool visible=true,bool advancing=true,bool foreground=true,bool enabled=true){session.Observe(owner,movie,time,visible,advancing,foreground,enabled);}
        public static void UsesPresentedMovieTimeAndDoesNotRepeatAnAcceptedCue()
        {
            var output=new Output();var session=new MovieDescriptionSession(output);var movie=Movie();
            Tick(session,1,movie,.9);TestAssert.False(output.Playing);Tick(session,1,movie,1);TestAssert.True(output.Playing);
            output.Playing=false;Tick(session,1,movie,2);TestAssert.Equal(1,output.Events.FindAll(e=>e=="start:one").Count);
        }
        public static void PausedMoviePausesOwnedAudioAndDoesNotStartAnotherCue()
        {
            var output=new Output();var session=new MovieDescriptionSession(output);var movie=Movie();
            Tick(session,1,movie,1);Tick(session,1,movie,2,advancing:false);TestAssert.Equal("pause",output.Events[output.Events.Count-1]);
            Tick(session,1,movie,2,advancing:false);TestAssert.Equal(1,output.Events.FindAll(e=>e=="pause").Count);
            Tick(session,1,movie,2.1);TestAssert.Equal("resume",output.Events[output.Events.Count-1]);
        }
        public static void SkipAndReplacementCannotReleaseExpiredOrStaleDescriptions()
        {
            var output=new Output();var session=new MovieDescriptionSession(output);var movie=Movie();
            Tick(session,1,movie,1);Tick(session,1,movie,8);TestAssert.True(output.Events.Contains("start:two"));
            session.Stop(1);int count=output.Events.Count;session.Stop(99);TestAssert.Equal(count,output.Events.Count);
            Tick(session,2,movie,20);TestAssert.False(output.Playing);TestAssert.Equal(2,output.Events.FindAll(e=>e.StartsWith("start:")).Count);
        }
        public static void LateCueIsNotStartedWhenItCannotFitItsRemainingWindow()
        {
            var output=new Output();var session=new MovieDescriptionSession(output);Tick(session,1,Movie(),4);
            TestAssert.False(output.Playing);TestAssert.False(output.Events.Contains("start:one"));
        }
        public static void HiddenOrBackgroundMovieNeverStartsAndCancelsOwnedPlayback()
        {
            var output=new Output();var session=new MovieDescriptionSession(output);var movie=Movie();
            Tick(session,1,movie,1,visible:false);TestAssert.False(output.Playing);Tick(session,1,movie,1);TestAssert.True(output.Playing);
            Tick(session,1,movie,2,foreground:false);TestAssert.False(output.Playing);Tick(session,1,movie,8,foreground:false);TestAssert.False(output.Events.Contains("start:two"));
            Tick(session,1,movie,12);TestAssert.False(output.Playing);
        }
        public static void RejectedOutputCanRetryWhileTheCurrentWindowStillFits()
        {
            var output=new Output{Accept=false};var session=new MovieDescriptionSession(output);var movie=Movie();
            Tick(session,1,movie,1);TestAssert.False(output.Playing);output.Accept=true;Tick(session,1,movie,1.1);TestAssert.True(output.Playing);
        }
        public static void StaleOwnerStopDoesNotStopTheReplacementMovie()
        {
            var output=new Output();var session=new MovieDescriptionSession(output);var movie=Movie();
            Tick(session,1,movie,1);Tick(session,2,movie,1);session.Stop(1);TestAssert.True(output.Playing);
            session.Stop(2);TestAssert.False(output.Playing);
        }
        public static void NativePauseIsImmediateEvenBeforeAnotherRenderedFrame()
        {
            var output=new Output();var session=new MovieDescriptionSession(output);var movie=Movie();Tick(session,1,movie,1);
            session.Pause(99);TestAssert.False(output.Events.Contains("pause"));session.Pause(1);TestAssert.Equal("pause",output.Events[output.Events.Count-1]);
            Tick(session,1,movie,1.1);TestAssert.Equal("resume",output.Events[output.Events.Count-1]);
        }
        public static void SeekBackAndReplayUsesTheNewNativePosition()
        {
            var output=new Output();var session=new MovieDescriptionSession(output);var movie=Movie();
            Tick(session,1,movie,8);Tick(session,1,movie,1);TestAssert.Equal("start:one",output.Events[output.Events.Count-1]);
        }
        public static void DisabledAndInvalidFramesCannotStartAudio()
        {
            var output=new Output();var session=new MovieDescriptionSession(output);var movie=Movie();
            Tick(session,1,movie,1,enabled:false);Tick(session,1,movie,double.NaN);Tick(session,1,movie,double.PositiveInfinity);Tick(session,1,movie,-1);
            TestAssert.False(output.Playing);TestAssert.False(output.Events.Exists(e=>e.StartsWith("start:")));
        }
        public static void InvalidCueWindowsAndOverlapsAreRejected()
        {
            bool invalid=false;try{new DescriptionMovie("FMV001",new[]{new DescriptionCue("bad",1,2,3,"Too long.")});}catch(ArgumentException){invalid=true;}TestAssert.True(invalid);
            invalid=false;try{new DescriptionMovie("FMV001",new[]{new DescriptionCue("a",1,4,1,"One."),new DescriptionCue("b",3,5,1,"Two.")});}catch(ArgumentException){invalid=true;}TestAssert.True(invalid);
        }
    }
}
