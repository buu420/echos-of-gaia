using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
namespace Memoria.Accessibility.Navigation
{
    public sealed class WorldGeometryPreparation
    {
        private sealed class Job
        {
            internal long Identity;internal WorldSurface[] Surfaces;internal volatile bool Cancelled;internal bool Complete;
            internal readonly Queue<WorldSurface[]> Batches=new Queue<WorldSurface[]>();
        }
        private readonly object _sync=new object();
        private readonly Action<WaitCallback> _schedule;
        private readonly Func<WorldSurface[],Func<bool>,WorldGeometrySnapshot> _build;
        private Job _job;private long _identity,_elapsed;private WorldGeometrySnapshot _ready;private string _error;
        public WorldGeometryPreparation():this(callback=>{if(!ThreadPool.QueueUserWorkItem(callback))throw new InvalidOperationException("World preparation worker unavailable");}) { }
        internal WorldGeometryPreparation(Action<WaitCallback> schedule) {_schedule=schedule;}
        internal WorldGeometryPreparation(Action<WaitCallback> schedule,Func<WorldSurface[],Func<bool>,WorldGeometrySnapshot> build) {_schedule=schedule;_build=build;}
        public bool TryBegin(long identity){return Start(identity,null,false);}
        public bool TryAppendOwned(long identity,WorldSurface[] surfaces)
        {
            if(surfaces==null)return false;
            lock(_sync){if(_job==null || _job.Cancelled || _job.Complete || _job.Identity!=identity)return false;_job.Batches.Enqueue(surfaces);Monitor.PulseAll(_sync);return true;}
        }
        public bool Complete(long identity)
        {
            lock(_sync){if(_job==null || _job.Cancelled || _job.Identity!=identity)return false;_job.Complete=true;Monitor.PulseAll(_sync);return true;}
        }
        public bool TryBindReady(long identity,WorldGeometrySnapshot geometry)
        {
            if(identity==0 || geometry==null || !geometry.Complete)return false;
            lock(_sync){if(_job!=null)return false;_identity=identity;_ready=geometry;_error=null;_elapsed=0;return true;}
        }
        public WorldGeometrySnapshot RetainReady(){lock(_sync){return _ready;}}
        // On success the caller transfers these detached surfaces to this job.
        // In particular, Unity arrays/objects must never be passed to the worker.
        public bool TryStartOwned(long identity,WorldSurface[] surfaces)
        {
            return surfaces!=null && Start(identity,surfaces,true);
        }
        private bool Start(long identity,WorldSurface[] surfaces,bool complete)
        {
            if(identity==0)return false;Job job;
            lock(_sync) {
                if(_identity==identity && _ready!=null)return true;
                if(_job!=null || _identity==identity && _error!=null)return false;
                _identity=identity;_ready=null;_error=null;_elapsed=0;
                job=new Job{Identity=identity,Surfaces=surfaces,Complete=complete};_job=job;
            }
            try{_schedule(delegate(object unused){Run(job);});}
            catch(Exception exception){lock(_sync){if(_job==job){_error=exception.Message;_job=null;}}}
            return true;
        }
        private WorldSurface[] NextBatch(Job job)
        {
            lock(_sync){
                while(!job.Cancelled){
                    if(job.Surfaces!=null){var surfaces=job.Surfaces;job.Surfaces=null;return surfaces;}
                    if(job.Batches.Count!=0)return job.Batches.Dequeue();
                    if(job.Complete)return null;Monitor.Wait(_sync);
                }
                throw new OperationCanceledException();
            }
        }
        private void Run(Job job)
        {
            WorldGeometrySnapshot ready=null;string error=null;var timer=Stopwatch.StartNew();
            try{if(!job.Cancelled){
                if(_build==null)ready=new WorldGeometrySnapshot(()=>NextBatch(job),()=>job.Cancelled);
                else {var surfaces=new List<WorldSurface>();WorldSurface[] batch;while((batch=NextBatch(job))!=null)surfaces.AddRange(batch);ready=_build(surfaces.ToArray(),()=>job.Cancelled);}
                if(ready!=null && !ready.Complete){ready=null;error="World preparation incomplete";}
            }}
            catch(OperationCanceledException){if(!job.Cancelled)error="World preparation cancelled";}
            catch(Exception exception){error=exception.Message;}
            timer.Stop();
            lock(_sync) {
                if(!job.Cancelled && _identity==job.Identity && _job==job){_ready=ready;_error=error??(ready==null?"World preparation unavailable":null);_elapsed=timer.ElapsedMilliseconds;}
                if(_job==job)_job=null;job.Surfaces=null;job.Batches.Clear();
            }
        }
        public bool TryRead(long identity,long generation,string fingerprint,TransportRules rules,int[] nativeCache,out WorldGeometrySnapshot geometry)
        {
            lock(_sync){geometry=null;if(_identity!=identity || _ready==null)return false;geometry=_ready.ForContext(generation,fingerprint,rules,nativeCache);return true;}
        }
        public void Invalidate()
        {
            lock(_sync){_identity=0;_ready=null;_error=null;_elapsed=0;if(_job!=null){_job.Cancelled=true;_job.Surfaces=null;_job.Batches.Clear();}Monitor.PulseAll(_sync);}
        }
        public bool IsReady(long identity){lock(_sync){return _identity==identity && _ready!=null;}}
        public string Failure(long identity){lock(_sync){return _identity==identity?_error:null;}}
        public long ElapsedMilliseconds(long identity){lock(_sync){return _identity==identity?_elapsed:0;}}
    }
}
