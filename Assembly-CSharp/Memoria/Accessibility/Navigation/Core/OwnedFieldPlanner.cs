using System;
using System.Diagnostics;
using System.Threading;

namespace Memoria.Accessibility.Navigation
{
    internal sealed class FieldPlanResult
    {
        internal NavigationContext Origin;
        internal NavigationTarget Target;
        internal FieldGeometrySnapshot Geometry;
        internal RoutePlan Route;
        internal long ElapsedMilliseconds;
        internal string Error;

        internal bool IsCurrent(NavigationContext context, NavigationTarget target, FieldGeometrySnapshot geometry)
        {
            if (context == null || target == null || geometry == null || !context.CanControl || !context.Foreground ||
                !context.Position.IsFinite || !target.Position.IsFinite ||
                Origin.Generation != context.Generation || Origin.LogicalMapKey != context.LogicalMapKey ||
                Origin.SaveIdentity != context.SaveIdentity || Origin.PlayerIdentity != context.PlayerIdentity ||
                Origin.StoryProgress != context.StoryProgress || !Origin.Transport.Equals(context.Transport) ||
                NavigationPoint.Distance(Origin.Position, context.Position) > 0.01f ||
                Geometry.Generation != geometry.Generation || Geometry.Fingerprint != geometry.Fingerprint ||
                Geometry.ActiveTriangle != geometry.ActiveTriangle ||
                Target.Generation != target.Generation || !Target.Id.Equals(target.Id) || Target.IsInteractive != target.IsInteractive ||
                GuidanceRouteValidation.FieldApproachChanged(Target, target))
                return false;

            if (target.UseApprovedApproaches) return true;
            float tolerance = target.PhysicalIdentity.Kind == TargetKind.FieldActor ? Math.Max(0.01f, geometry.Radius) : 0.01f;
            if (NavigationPoint.Distance(Target.Position, target.Position) > tolerance) return false;
            var before = Target.ApproachCandidates;
            var after = target.ApproachCandidates;
            if (before == null || after == null) return before == after;
            if (before.Length != after.Length) return false;
            for (int i = 0; i < before.Length; i++)
                if (!after[i].IsFinite || NavigationPoint.Distance(before[i], after[i]) > tolerance) return false;
            return true;
        }
    }

    // The field adapter owns native reads and movement. The single worker only
    // sees detached values and never installs a path or speaks.
    internal sealed class OwnedFieldPlanner
    {
        private sealed class Job
        {
            internal FieldPlanResult Result;
            internal volatile bool Cancelled;
            internal bool Completed;
        }

        private readonly object _sync = new object();
        private readonly Action<WaitCallback> _schedule;
        private Job _job;

        internal OwnedFieldPlanner() : this(callback =>
        {
            if (!ThreadPool.QueueUserWorkItem(callback)) throw new InvalidOperationException("Field route worker unavailable");
        }) { }

        internal OwnedFieldPlanner(Action<WaitCallback> schedule) { _schedule = schedule; }

        internal bool TryStart(NavigationContext context, NavigationTarget target, FieldGeometrySnapshot geometry)
        {
            Job job;
            lock (_sync)
            {
                if (_job != null) return false;
                // NativeFieldGeometry publishes immutable detached triangles and a fresh active-triangle view.
                job = new Job { Result = new FieldPlanResult { Origin = context.Copy(), Target = FieldTargetCatalog.Copy(target), Geometry = geometry } };
                _job = job;
            }
            try { _schedule(delegate { Run(job); }); }
            catch
            {
                lock (_sync) { if (_job == job) _job = null; }
                throw;
            }
            return true;
        }

        private void Run(Job job)
        {
            var timer = Stopwatch.StartNew();
            var result = job.Result;
            try
            {
                if (!job.Cancelled)
                    result.Route = new FieldRoutePlanner().Plan(result.Origin, result.Target, result.Geometry, () => job.Cancelled);
            }
            catch (Exception exception)
            {
                result.Error = exception.Message;
                result.Route = new RoutePlan
                {
                    TargetId = result.Target.Id, PhysicalId = result.Target.PhysicalId, Generation = result.Origin.Generation,
                    Transport = result.Origin.Transport, GeometryFingerprint = result.Geometry.Fingerprint, Failure = RouteFailure.GeometryUnavailable
                };
            }
            result.ElapsedMilliseconds = timer.ElapsedMilliseconds;
            lock (_sync)
            {
                job.Completed = true;
                if (job.Cancelled && _job == job) _job = null;
            }
        }

        internal bool TryTake(out FieldPlanResult result)
        {
            lock (_sync)
            {
                result = null;
                if (_job == null || !_job.Completed || _job.Cancelled) return false;
                result = _job.Result;
                _job = null;
                return true;
            }
        }

        internal void Cancel()
        {
            lock (_sync)
            {
                if (_job == null) return;
                _job.Cancelled = true;
                if (_job.Completed) _job = null;
            }
        }
    }
}
