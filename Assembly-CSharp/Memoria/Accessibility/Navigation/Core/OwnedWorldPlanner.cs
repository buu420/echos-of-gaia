using System;
using System.Diagnostics;
using System.Threading;
namespace Memoria.Accessibility.Navigation
{
    public sealed class WorldPlanResult
    {
        public RoutePlan Route { get; internal set; }
        public string Error { get; internal set; }
        public long ElapsedMilliseconds { get; internal set; }
        internal NavigationContext Origin;internal NavigationTarget Target;internal string Fingerprint;
        public bool IsCurrent(NavigationContext context,NavigationTarget target,string fingerprint)
        {
            if(context==null || target==null || !context.CanControl || !context.Foreground || !context.Position.IsFinite || !target.Position.IsFinite || Origin.Generation!=context.Generation || Origin.LogicalMapKey!=context.LogicalMapKey || Origin.SaveIdentity!=context.SaveIdentity || Origin.PlayerIdentity!=context.PlayerIdentity || !Origin.Transport.Equals(context.Transport) || Target.Generation!=target.Generation || !Target.Id.Equals(target.Id) || !Target.PhysicalIdentity.Equals(target.PhysicalIdentity) || Target.NativeLocationId!=target.NativeLocationId || Fingerprint!=fingerprint || WorldCoordinates.Distance(Origin.Position,context.Position)>0.5f || Math.Abs(Origin.Position.Y-context.Position.Y)>0.5f || WorldCoordinates.Distance(Target.Position,target.Position)>0.5f || Math.Abs(Target.Position.Y-target.Position.Y)>0.5f)return false;
            var before=Target.ApproachCandidates;var after=target.ApproachCandidates;
            if(before==null || after==null)return before==after;
            if(before.Length!=after.Length)return false;
            for(int i=0;i<before.Length;i++)if(!after[i].IsFinite || WorldCoordinates.Distance(before[i],after[i])>0.02f || Math.Abs(before[i].Y-after[i].Y)>0.02f)return false;
            return true;
        }
    }
    public sealed class OwnedWorldPlanner
    {
        private sealed class Job
        {
            internal long Request;internal volatile bool Cancelled;internal bool Completed;
            internal WorldGeometrySnapshot Geometry;internal WorldPlanResult Result;
        }
        private readonly object _sync=new object();private readonly Action<WaitCallback> _schedule;private readonly Func<NavigationContext,NavigationTarget,WorldGeometrySnapshot,Func<bool>,RoutePlan> _plan;private Job _job;
        public OwnedWorldPlanner():this(callback=>{if(!ThreadPool.QueueUserWorkItem(callback))throw new InvalidOperationException("World route worker unavailable");}) { }
        internal OwnedWorldPlanner(Action<WaitCallback> schedule):this(schedule,(context,target,geometry,cancelled)=>new WorldRoutePlanner().Plan(context,target,geometry,cancelled)) { }
        internal OwnedWorldPlanner(Action<WaitCallback> schedule,Func<NavigationContext,NavigationTarget,WorldGeometrySnapshot,Func<bool>,RoutePlan> plan) { _schedule=schedule;_plan=plan; }
        public bool TryStart(long request,NavigationContext context,NavigationTarget target,WorldGeometrySnapshot geometry)
        {
            if(request==0 || context==null || target==null || geometry==null || !geometry.Complete)return false;
            Job job;
            lock(_sync) {
                if(_job!=null)return false;
                var origin=new NavigationContext{Generation=context.Generation,LogicalMapKey=context.LogicalMapKey,SaveIdentity=context.SaveIdentity,PlayerIdentity=context.PlayerIdentity,Position=context.Position,Transport=context.Transport,CanControl=context.CanControl,Foreground=context.Foreground};
                var copy=new NavigationTarget{Id=target.Id,PhysicalId=target.PhysicalId,Generation=target.Generation,Position=target.Position,NativeLocationId=target.NativeLocationId,ApproachCandidates=target.ApproachCandidates==null?null:(NavigationPoint[])target.ApproachCandidates.Clone()};
                job=new Job{Request=request,Geometry=geometry.ForPlanning(),Result=new WorldPlanResult{Origin=origin,Target=copy,Fingerprint=geometry.Fingerprint}};_job=job;
            }
            try {_schedule(delegate(object unused){Run(job);});}
            catch {lock(_sync){job.Cancelled=true;if(_job==job)_job=null;}throw;}
            return true;
        }
        private void Run(Job job)
        {
            var timer=Stopwatch.StartNew();
            try {if(!job.Cancelled)job.Result.Route=_plan(job.Result.Origin,job.Result.Target,job.Geometry,()=>job.Cancelled);}
            catch(Exception exception) {job.Result.Error=exception.Message;}
            if(!job.Cancelled && job.Result.Route==null)job.Result.Route=new RoutePlan{Generation=job.Result.Origin.Generation,TargetId=job.Result.Target.Id,PhysicalId=job.Result.Target.PhysicalId,Transport=job.Result.Origin.Transport,Failure=RouteFailure.GeometryUnavailable,GeometryFingerprint=job.Result.Fingerprint};
            timer.Stop();job.Result.ElapsedMilliseconds=timer.ElapsedMilliseconds;
            lock(_sync) {job.Completed=true;if(job.Cancelled && _job==job)_job=null;}
        }
        public bool TryTake(long request,out WorldPlanResult result)
        {
            lock(_sync) {
                result=null;if(_job==null || _job.Request!=request || !_job.Completed || _job.Cancelled)return false;
                result=_job.Result;_job=null;return true;
            }
        }
        public void Cancel()
        {
            lock(_sync) {if(_job==null)return;_job.Cancelled=true;if(_job.Completed)_job=null;}
        }
    }
}
