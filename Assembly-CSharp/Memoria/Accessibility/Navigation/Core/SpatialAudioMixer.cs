using System;
namespace Memoria.Accessibility.Navigation
{
    public interface ISpatialHrtf : IDisposable
    {
        void Reset(int lane);
        bool Mix(int lane,float[] mono,int offset,int count,NavigationPoint direction,float gain,float[] stereo);
    }
    public sealed class SpatialAudioMixer
    {
        private sealed class Voice {internal FieldSpatialCue Cue;internal long Serial;internal int Offset,Tails;internal bool Active;}
        private readonly float[][] _clips;private readonly ISpatialHrtf _hrtf;
        private readonly Voice[] _voices={new Voice(),new Voice(),new Voice()};
        private long _epoch=Int64.MinValue;private double _updated;
        public SpatialAudioMixer(float[][] clips,ISpatialHrtf hrtf)
        {
            if(clips==null || clips.Length!=3 || hrtf==null)throw new ArgumentException("Three proximity recordings and HRTF are required");
            foreach(var clip in clips){if(clip==null || clip.Length==0 || clip.Length>24000*4)throw new ArgumentException("Invalid proximity recording length");foreach(float sample in clip)if(Single.IsNaN(sample) || Single.IsInfinity(sample) || Math.Abs(sample)>1)throw new ArgumentException("Invalid proximity PCM");}
            _clips=clips;_hrtf=hrtf;
        }
        public bool Expired {get;private set;}
        public bool Update(FieldSpatialCue[] cues,long epoch,double milliseconds)
        {
            if(cues==null || Double.IsNaN(milliseconds) || Double.IsInfinity(milliseconds))throw new ArgumentException("Invalid spatial snapshot");
            bool changed=_epoch!=epoch;if(changed){Clear();_epoch=epoch;}
            _updated=milliseconds;Expired=false;var present=new FieldSpatialCue[3];
            foreach(var cue in cues)if(cue!=null) {
                int lane=(int)cue.Kind;
                if(lane<0 || lane>=3 || present[lane]!=null || !cue.Position.IsFinite || !cue.Direction.IsFinite || Single.IsNaN(cue.Gain) || Single.IsInfinity(cue.Gain) || cue.Gain<=0 || cue.Gain>2 || cue.PulseSerial<0)continue;
                float length=cue.Direction.X*cue.Direction.X+cue.Direction.Y*cue.Direction.Y+cue.Direction.Z*cue.Direction.Z;if(length<.99f || length>1.01f)continue;
                present[lane]=cue;
            }
            for(int lane=0;lane<3;lane++) {
                var voice=_voices[lane];var cue=present[lane];
                bool different=voice.Cue==null?cue!=null:cue==null || !voice.Cue.Identity.Equals(cue.Identity);
                if(different){Stop(lane);voice.Serial=0;changed=true;}
                voice.Cue=cue;
                if(cue!=null && cue.PulseSerial>0 && cue.PulseSerial!=voice.Serial){_hrtf.Reset(lane);voice.Serial=cue.PulseSerial;voice.Offset=voice.Tails=0;voice.Active=true;}
            }
            return changed;
        }
        public bool Render(float[] stereo,double milliseconds)
        {
            if(stereo==null || stereo.Length!=SpatialWaveOutput.FrameSize*2)throw new ArgumentException("Invalid stereo block");Array.Clear(stereo,0,stereo.Length);
            if(Double.IsNaN(milliseconds) || Double.IsInfinity(milliseconds) || milliseconds<_updated || milliseconds-_updated>=750) {
                if(!Expired){for(int lane=0;lane<3;lane++)Stop(lane);Expired=true;}return false;
            }
            bool rendered=false;
            for(int lane=0;lane<3;lane++) {
                var voice=_voices[lane];if(!voice.Active || voice.Cue==null)continue;
                int count=Math.Min(SpatialWaveOutput.FrameSize,_clips[lane].Length-voice.Offset);if(count==0 && ++voice.Tails>128)throw new InvalidOperationException("HRTF tail exceeded its bounded lifetime");
                bool tail=_hrtf.Mix(lane,_clips[lane],voice.Offset,count,voice.Cue.Direction,voice.Cue.Gain,stereo);voice.Offset+=count;rendered=true;
                if(voice.Offset>=_clips[lane].Length && !tail)voice.Active=false;
            }
            return rendered;
        }
        private void Stop(int lane){var voice=_voices[lane];if(voice.Active || voice.Cue!=null)_hrtf.Reset(lane);voice.Active=false;voice.Offset=voice.Tails=0;}
        public void Clear(){for(int lane=0;lane<3;lane++){Stop(lane);_voices[lane].Cue=null;_voices[lane].Serial=0;}Expired=false;}
    }
}
