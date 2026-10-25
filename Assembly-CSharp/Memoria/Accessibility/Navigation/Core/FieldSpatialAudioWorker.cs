using System;
using System.Diagnostics;
using System.Threading;
namespace Memoria.Accessibility.Navigation
{
    public sealed class FieldSpatialAudioWorker : IDisposable
    {
        private sealed class Snapshot {internal FieldSpatialCue[] Cues;internal long Epoch;internal double Time;}
        private readonly object _sync=new object();private readonly AutoResetEvent _wake=new AutoResetEvent(false);private readonly Stopwatch _clock=Stopwatch.StartNew();
        private readonly Func<float[][]> _load;private readonly Func<ISpatialHrtf> _hrtf;private readonly Func<SpatialWaveOutput> _output;
        private Snapshot _latest;private bool _stopping,_finished,_ready;private string _failure;
        public FieldSpatialAudioWorker(string directory,string library):this(()=>SpatialCueAssets.Load(directory,library),()=>new SteamAudioHrtf(library),()=>new SpatialWaveOutput()){}
        public FieldSpatialAudioWorker(Func<float[][]> load,Func<ISpatialHrtf> hrtf,Func<SpatialWaveOutput> output)
        {
            if(load==null || hrtf==null || output==null)throw new ArgumentNullException("spatial worker input");_load=load;_hrtf=hrtf;_output=output;
            var worker=new Thread(Run){IsBackground=true,Name="FFIX positional cues"};worker.Start();
        }
        public void Submit(FieldSpatialCue[] cues,long epoch)
        {
            if(cues==null || cues.Length>3)throw new ArgumentException("Invalid spatial snapshot");
            var snapshot=new Snapshot{Cues=(FieldSpatialCue[])cues.Clone(),Epoch=epoch,Time=_clock.Elapsed.TotalMilliseconds};
            lock(_sync){if(_stopping)return;_latest=snapshot;_wake.Set();}
        }
        public bool Finished {get{lock(_sync)return _finished;}}
        public string Failure {get{lock(_sync)return _failure;}}
        public bool Ready {get{lock(_sync)return _ready;}}
        public void Dispose(){lock(_sync){if(_stopping)return;_stopping=true;_wake.Set();}}
        private bool Stopping {get{lock(_sync)return _stopping;}}
        private void Run()
        {
            ISpatialHrtf hrtf=null;SpatialWaveOutput output=null;
            try {
                // All disk, DLL and native initialization belongs to this worker.
                float[][] clips=_load();if(Stopping)return;hrtf=_hrtf();if(Stopping)return;output=_output();
                var mixer=new SpatialAudioMixer(clips,hrtf);var stereo=new float[1024];var pcm=new short[1024];Snapshot seen=null;bool expired=false;
                lock(_sync)_ready=true;
                while(!Stopping) {
                    Snapshot current;lock(_sync)current=_latest;
                    if(current==null){_wake.WaitOne(200);continue;}
                    if(!System.Object.ReferenceEquals(current,seen)) {
                        if(mixer.Update(current.Cues,current.Epoch,current.Time))output.Stop();seen=current;expired=false;
                    }
                    double now=_clock.Elapsed.TotalMilliseconds;
                    if(now-current.Time>=750){if(!expired){mixer.Render(stereo,now);output.Stop();expired=true;}_wake.WaitOne(200);continue;}
                    bool rendered=false;
                    while(output.PendingCount<3 && !Stopping) {
                        if(!mixer.Render(stereo,_clock.Elapsed.TotalMilliseconds))break;
                        for(int i=0;i<pcm.Length;i++){
                            float value=stereo[i];if(Single.IsNaN(value) || Single.IsInfinity(value))throw new InvalidOperationException("HRTF produced invalid PCM");
                            pcm[i]=(short)Math.Round(Math.Max(-1,Math.Min(1,value))*32767);
                        }
                        if(!output.TryWrite(pcm))break;rendered=true;
                    }
                    _wake.WaitOne(rendered || output.PendingCount>0?5:200);
                }
            }catch(Exception error){Fail(error);}
            finally {
                lock(_sync)_stopping=true;
                // Failed driver reset/unprepare intentionally retains its memory.
                // It is never freed merely because the scene or worker ended.
                try{if(output!=null)output.Dispose();}catch(Exception error){Fail(error);}
                try{if(hrtf!=null)hrtf.Dispose();}catch(Exception error){Fail(error);}
                lock(_sync){_ready=false;_finished=true;_wake.Close();}
            }
        }
        private void Fail(Exception error){lock(_sync){if(_failure==null)_failure=error.Message;}}
    }
}
