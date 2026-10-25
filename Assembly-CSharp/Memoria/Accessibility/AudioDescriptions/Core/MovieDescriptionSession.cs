using System;
namespace Memoria.Accessibility.AudioDescriptions
{
    public sealed class DescriptionCue
    {
        public readonly string Id,Text;
        public readonly double Start,End,Duration;
        public DescriptionCue(string id,double start,double end,double duration,string text)
        {
            if(String.IsNullOrEmpty(id) || String.IsNullOrEmpty(text) || !Finite(start) || !Finite(end) || !Finite(duration) || start<0 || duration<=0 || end-start<duration)
                throw new ArgumentException("Invalid audio-description window");
            Id=id;Start=start;End=end;Duration=duration;Text=text;
        }
        internal static bool Finite(double value){return !Double.IsNaN(value) && !Double.IsInfinity(value);}
    }
    public sealed class DescriptionMovie
    {
        public readonly string Key;
        private readonly DescriptionCue[] _cues;
        public int Count {get{return _cues.Length;}}
        public DescriptionCue this[int index] {get{return _cues[index];}}
        public DescriptionMovie(string key,DescriptionCue[] cues)
        {
            if(String.IsNullOrEmpty(key) || cues==null)throw new ArgumentException("Missing audio-description movie");
            Key=key;_cues=(DescriptionCue[])cues.Clone();double end=0;
            foreach(var cue in _cues){if(cue==null || cue.Start<end)throw new ArgumentException("Overlapping or unordered audio-description windows");end=cue.End;}
        }
    }
    public interface IDescriptionPlayback
    {
        bool Playing {get;}
        // False means temporarily unavailable or rejected, never accepted speech.
        bool TryStart(DescriptionCue cue);
        void Pause();
        void Resume();
        void Stop();
    }
    // Uses the movie's presented time only. No free-running timer or game input.
    public sealed class MovieDescriptionSession
    {
        private readonly IDescriptionPlayback _output;
        private long _owner;
        private DescriptionMovie _movie;
        private DescriptionCue _active;
        private int _next;
        private double _time;
        private bool _paused;
        public MovieDescriptionSession(IDescriptionPlayback output){if(output==null)throw new ArgumentNullException("output");_output=output;}
        public void Observe(long owner,DescriptionMovie movie,double presentedSeconds,bool visible,bool advancing,bool foreground,bool enabled)
        {
            if(movie!=_movie || owner!=_owner){Clear();_movie=movie;_owner=owner;}
            if(movie==null || !DescriptionCue.Finite(presentedSeconds) || presentedSeconds<0 || !visible || !foreground || !enabled){Silence();return;}
            if(presentedSeconds<_time-.25){Silence();_next=0;}
            _time=presentedSeconds;
            if(_active!=null && (presentedSeconds>=_active.End || !_output.Playing))Silence();
            if(!advancing){if(_output.Playing && !_paused){_output.Pause();_paused=true;}return;}
            if(_paused){_output.Resume();_paused=false;}
            if(_output.Playing)return;
            while(_next<movie.Count) {
                var cue=movie[_next];
                if(presentedSeconds<cue.Start)return;
                // A seek, late load or slow frame must not spill an old line
                // into the next shot. Rejected current output can still retry.
                if(presentedSeconds+cue.Duration>cue.End+.001){_next++;continue;}
                if(_output.TryStart(cue)){_active=cue;_next++;}
                return;
            }
        }
        public void Stop(long owner){if(_movie!=null && owner==_owner)Clear();}
        public void Pause(long owner){if(_movie!=null && owner==_owner && _output.Playing && !_paused){_output.Pause();_paused=true;}}
        public void Reset(){Clear();}
        private void Silence(){_output.Stop();_active=null;_paused=false;}
        private void Clear(){Silence();_movie=null;_owner=0;_next=0;_time=0;}
    }
}
