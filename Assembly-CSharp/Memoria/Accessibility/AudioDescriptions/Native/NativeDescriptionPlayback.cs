using System;
using Memoria.ScreenReader;
namespace Memoria.Accessibility.AudioDescriptions
{
    internal sealed class NativeDescriptionPlayback : IDescriptionPlayback,IDisposable
    {
        private DescriptionWaveOutput _wave=new DescriptionWaveOutput();
        private readonly NativeDescriptionSpeech _speech=new NativeDescriptionSpeech();
        private readonly Action<string> _report;
        internal DescriptionAudioBank Bank;
        internal long Owner;
        internal int Volume=100;
        internal bool MovieFallback;
        private long _speechSerial;
        private string _fallback;
        private DescriptionCue _recorded;
        private bool _failed,_speechFailed,_recoverCaptionPending,_speechRecoveryPending;
        private int _failures;
        internal NativeDescriptionPlayback(Action<string> report){_report=report;}
        public bool Playing {get{return !_failed && _wave.Playing || SpeechPlaying || _recoverCaptionPending || _fallback!=null && ScreenReaderManager.Instance.OutputSerial==_speechSerial;}}
        private bool SpeechPlaying {
            get {
                if(_speechFailed)return _speechRecoveryPending;
                try{return _speech.Playing;}
                catch(Exception exception){
                    FailSpeech(exception);_speechRecoveryPending=_recorded!=null;
                    try{_speech.Stop();}catch(Exception cleanup){_report("Owned speech quarantine failed: "+cleanup.Message);}
                    // An unread caption remains pending; a failed status query
                    // does not establish that the owned utterance completed.
                    return _speechRecoveryPending;
                }
            }
        }
        internal bool NeedsSpeechRecovery {get{return _speechRecoveryPending;}}
        internal void RecoverMovieSpeech(double position)
        {
            if(!_speechRecoveryPending && !_recoverCaptionPending)return;
            // Recovery restarts a whole utterance and obeys the same window
            // fit and rewind boundary as a first start in MovieDescriptionSession.
            if(_recorded==null || position<_recorded.Start || position+_recorded.Duration>_recorded.End+.001){_speechRecoveryPending=_recoverCaptionPending=false;return;}
            if(TryFallback(_recorded))_speechRecoveryPending=_recoverCaptionPending=false;
        }
        public bool TryStart(DescriptionCue cue)
        {
            if(Bank==null || !Bank.SourceVerified(Owner))return false;
            byte[] wave;bool rejected=false;
            if(!_failed && Bank.TryGet(Owner,cue.Id,out wave)) {
                try{if(!_wave.TryPlay(Scale(wave,Volume)))return false;_failures=0;_fallback=null;_recorded=cue;_recoverCaptionPending=false;return true;}
                catch(Exception exception){rejected=true;RecoverRecordedOutput("Recorded description output failed: "+exception.Message);}
            }
            if(MovieFallback && (rejected || _failed || Bank.Failure(Owner)!=null))return TryFallback(cue);
            return false;
        }
        public void Pause()
        {
            try{if(!_failed)_wave.Pause();}
            catch(Exception exception){RecoverRecordedOutput("Description pause failed: "+exception.Message);_recoverCaptionPending=MovieFallback && _recorded!=null;}
            try{if(!_speechFailed)_speech.Pause();}catch(Exception exception){FailSpeech(exception);_recoverCaptionPending=MovieFallback && _recorded!=null;}
        }
        public void Resume()
        {
            try{if(!_failed)_wave.Resume();}
            catch(Exception exception){RecoverRecordedOutput("Description resume failed: "+exception.Message);_recoverCaptionPending=MovieFallback && _recorded!=null;}
            try{if(!_speechFailed)_speech.Resume();}catch(Exception exception){FailSpeech(exception);_recoverCaptionPending=MovieFallback && _recorded!=null;}
        }
        public void Stop()
        {
            try{if(!_failed)_wave.Stop();}catch(Exception exception){RecoverRecordedOutput("Description stop failed: "+exception.Message);}
            try{_speech.Stop();}catch(Exception exception){FailSpeech(exception);}
            // Prism cannot cancel one queued utterance. Globally silencing here
            // would also cut native dialogue queued before our fallback caption.
            _fallback=null;_recorded=null;_recoverCaptionPending=_speechRecoveryPending=false;
        }
        public void Dispose(){Stop();try{_wave.Dispose();}catch(Exception exception){_report("Description device disposal failed: "+exception.Message);}try{_speech.Dispose();}catch(Exception exception){FailSpeech(exception);}}
        private void FailSpeech(Exception exception){_speechFailed=true;_report("Owned description speech unavailable: "+exception.Message);}
        internal bool TryOwnedFallback(DescriptionCue cue)
        {
            if(cue==null || _speechFailed)return false;
            try{if(!_speech.TrySpeak(cue.Text,Volume))return false;_recorded=cue;_fallback=null;_recoverCaptionPending=false;return true;}
            catch(Exception exception){FailSpeech(exception);return false;}
        }
        private bool TryFallback(DescriptionCue cue)
        {
            if(TryOwnedFallback(cue))return true;
            if(cue==null || !ScreenReaderManager.Instance.TrySpeak(cue.Text,false))return false;
            _fallback=cue.Text;_speechSerial=ScreenReaderManager.Instance.OutputSerial;_recoverCaptionPending=false;return true;
        }
        internal void RecoverRecordedOutput(string reason)
        {
            _report(reason);_failures++;
            try{
                _wave.Stop();_wave.Dispose();
                // A single late completion/device error may recover. Repeated
                // failures use speech for this session; uncertain native buffers
                // remain retained by the old output if reset/cleanup failed.
                if(_failures<2){_wave=new DescriptionWaveOutput();_failed=false;}else _failed=true;
            }catch(Exception exception){_failed=true;_report("Description device cleanup failed: "+exception.Message);}
        }
        // Per-recording software gain never changes the original soundtrack,
        // the system mixer, or another audio device's volume.
        private static byte[] Scale(byte[] wave,int volume)
        {
            if(volume==100)return wave;volume=Math.Max(0,Math.Min(200,volume));var result=(byte[])wave.Clone();
            for(int i=44;i+1<result.Length;i+=2){int sample=unchecked((short)(wave[i]|wave[i+1]<<8));int value=Math.Max(Int16.MinValue,Math.Min(Int16.MaxValue,sample*volume/100));result[i]=unchecked((byte)value);result[i+1]=unchecked((byte)(value>>8));}
            return result;
        }
    }
}
