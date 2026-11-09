using System.Threading;
using Memoria.Accessibility.Navigation;

namespace FF9Access.Tests
{
    internal static class FieldPlanningTests
    {
        private static NavigationContext Context()
        {
            var context = TestFixtures.Context();
            context.Position = TestFixtures.P(10, 10);
            return context;
        }

        private static NavigationTarget Target()
        {
            return new NavigationTarget
            {
                Id = new TargetIdentity(TargetKind.FieldExit, "field:100", 8),
                Generation = 1,
                Position = TestFixtures.P(20, 20),
                ApproachCandidates = new[] { TestFixtures.P(20, 20) },
                UseApprovedApproaches = true
            };
        }

        private static FieldGeometrySnapshot Geometry()
        {
            return new FieldGeometrySnapshot
            {
                Generation = 1, ActiveTriangle = 0, Fingerprint = "hall-a",
                Triangles = new[] { new FieldTriangle { Vertices = new[] { TestFixtures.P(0, 0), TestFixtures.P(100, 0), TestFixtures.P(0, 100) } } }
            };
        }

        public static void FieldPlanningUsesDetachedInputsAndDoesNotWaitForTheSearch()
        {
            WaitCallback queued = null;
            var worker = new OwnedFieldPlanner(callback => queued = callback);
            var context = Context();
            var target = Target();
            var geometry = Geometry();
            TestAssert.True(worker.TryStart(context, target, geometry));
            FieldPlanResult result;
            TestAssert.False(worker.TryTake(out result));
            context.Position = TestFixtures.P(100, 100);
            target.ApproachCandidates[0] = context.Position;
            queued(null);
            TestAssert.True(worker.TryTake(out result));
            TestAssert.True(result.Route.Success);
            TestAssert.Equal(TestFixtures.P(10, 10), result.Route.Origin);
            TestAssert.Equal(TestFixtures.P(20, 20), result.Route.ApproachPoint);
            TestAssert.True(result.IsCurrent(Context(), Target(), geometry));
            TestAssert.False(result.IsCurrent(context, target, geometry));
        }

        public static void FieldCompletionRequiresTheLiveSceneStartAndApproach()
        {
            WaitCallback queued = null;
            var worker = new OwnedFieldPlanner(callback => queued = callback);
            TestAssert.True(worker.TryStart(Context(), Target(), Geometry()));
            queued(null);
            FieldPlanResult result;
            TestAssert.True(worker.TryTake(out result));
            var context = Context();
            var target = Target();
            var geometry = Geometry();
            TestAssert.True(result.IsCurrent(context, target, geometry));
            context.Generation++;
            TestAssert.False(result.IsCurrent(context, target, geometry));
            context = Context(); context.SaveIdentity = "other save";
            TestAssert.False(result.IsCurrent(context, target, geometry));
            context = Context(); context.CanControl = false;
            TestAssert.False(result.IsCurrent(context, target, geometry));
            context = Context(); context.Foreground = false;
            TestAssert.False(result.IsCurrent(context, target, geometry));
            context = Context(); context.StoryProgress++;
            TestAssert.False(result.IsCurrent(context, target, geometry));
            context = Context(); context.Position = TestFixtures.P(11, 10);
            TestAssert.False(result.IsCurrent(context, target, geometry));
            context = Context(); geometry.ActiveTriangle = 1;
            TestAssert.False(result.IsCurrent(context, target, geometry));
            geometry = Geometry(); geometry.Fingerprint = "moving floor";
            TestAssert.False(result.IsCurrent(context, target, geometry));
            geometry = Geometry(); target.ApproachCandidates[0] = TestFixtures.P(23, 23);
            TestAssert.False(result.IsCurrent(context, target, geometry));
            target = Target(); target.PhysicalId = new TargetIdentity(TargetKind.FieldExit, "field:100", 9);
            TestAssert.False(result.IsCurrent(context, target, geometry));
        }

        public static void CancellingKeepsOneJobInFlightAndDiscardsItsResult()
        {
            WaitCallback queued = null;
            var worker = new OwnedFieldPlanner(callback => queued = callback);
            TestAssert.True(worker.TryStart(Context(), Target(), Geometry()));
            worker.Cancel();
            TestAssert.False(worker.TryStart(Context(), Target(), Geometry()));
            queued(null);
            FieldPlanResult result;
            TestAssert.False(worker.TryTake(out result));
            TestAssert.True(worker.TryStart(Context(), Target(), Geometry()));
            queued(null);
            TestAssert.True(worker.TryTake(out result));
            TestAssert.True(result.Route.Success);
            TestAssert.False(worker.TryTake(out result));
        }
    }
}
