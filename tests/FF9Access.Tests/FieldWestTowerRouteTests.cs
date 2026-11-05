using System;
using Memoria.Accessibility.Navigation;

namespace FF9Access.Tests
{
    internal static class FieldWestTowerRouteTests
    {
        public static void TheNativeSpiralStairHasAConnectedRouteInBothDirections()
        {
            var geometry = Stair(164, 80);
            var lower = LowerPoint(geometry);
            var upper = geometry.Find(39).Center;
            AssertRoute(geometry, lower, upper);
            AssertRoute(geometry, upper, lower);
        }

        public static void ReviewedStairClearanceDoesNotApplyToOtherMeshesOrLargerActors()
        {
            var geometry = Stair(163, 80);
            TestAssert.False(Plan(geometry, LowerPoint(geometry), geometry.Find(39).Center).Success);
            geometry = Stair(164, 90);
            TestAssert.False(Plan(geometry, LowerPoint(geometry), geometry.Find(39).Center).Success);
            geometry = Stair(164, 80, 20);
            TestAssert.False(Plan(geometry, LowerPoint(geometry, 20), geometry.Find(39).Center).Success);
        }

        private static NavigationPoint LowerPoint(FieldGeometrySnapshot geometry, float offset = 0)
        {
            var point = new NavigationPoint(1460 + offset, 10531, 4410);
            return new NavigationPoint(point.X, geometry.Find(58).Height(point), point.Z);
        }

        private static void AssertRoute(FieldGeometrySnapshot geometry, NavigationPoint start, NavigationPoint end)
        {
            var route = Plan(geometry, start, end);
            TestAssert.Equal(RouteFailure.None, route.Failure);
            TestAssert.True(route.Success);
            TestAssert.True(route.FieldSteeringWaypoints != null && route.FieldSteeringWaypoints.Length < route.Waypoints.Length);
            foreach (var point in route.Waypoints)
                TestAssert.True(geometry.At(point) != null);
            var from = start;
            foreach (var point in route.FieldSteeringWaypoints)
            {
                TestAssert.True(Array.IndexOf(route.Waypoints, point) >= 0);
                TestAssert.True(geometry.IsSegmentClear(from, point));
                from = point;
            }
            TestAssert.Equal(end.X, route.ApproachPoint.X);
            TestAssert.Equal(end.Z, route.ApproachPoint.Z);
        }

        private static RoutePlan Plan(FieldGeometrySnapshot geometry, NavigationPoint start, NavigationPoint end)
        {
            var context = TestFixtures.Context("field:164");
            context.Position = start;
            geometry.ActiveTriangle = geometry.At(start).Id;
            var target = new NavigationTarget
            {
                Id = new TargetIdentity(TargetKind.FieldExit, context.LogicalMapKey, 2),
                Generation = context.Generation,
                Position = end
            };
            return new FieldRoutePlanner().Plan(context, target, geometry);
        }

        // Original West Tower BGI, the spiral between native triangles 39 and 58.
        // This is a cropped fixture, with a wall at either cut end.
        private static FieldGeometrySnapshot Stair(int map, float radius, float offset = 0)
        {
            return new FieldGeometrySnapshot
            {
                Generation = 1,
                Radius = radius,
                Triangles = new[]
                {
                    Triangle(map, offset, 39, new[] { -1, -1, 40 }, new[] { -1, -1, 1 }, 650,11028,4523, 626,11035,4792, 874,10901,4771),
                    Triangle(map, offset, 40, new[] { 39, 41, -1 }, new[] { 0, 1, -1 }, 650,11028,4523, 874,10901,4771, 859,10891,4524),
                    Triangle(map, offset, 41, new[] { 40, -1, 42 }, new[] { 2, -1, 1 }, 859,10891,4524, 874,10901,4771, 992,10831,4655),
                    Triangle(map, offset, 42, new[] { 41, 54, -1 }, new[] { 0, 1, -1 }, 859,10891,4524, 992,10831,4655, 966,10822,4508),
                    Triangle(map, offset, 54, new[] { 42, -1, 55 }, new[] { 2, -1, 1 }, 966,10822,4508, 992,10831,4655, 1246,10695,4585),
                    Triangle(map, offset, 55, new[] { 54, 56, -1 }, new[] { 0, 1, -1 }, 966,10822,4508, 1246,10695,4585, 1178,10685,4460),
                    Triangle(map, offset, 56, new[] { 55, 57, -1 }, new[] { 2, 1, -1 }, 1178,10685,4460, 1246,10695,4585, 1270,10616,4417),
                    Triangle(map, offset, 57, new[] { 56, -1, 80 }, new[] { 2, -1, 1 }, 1270,10616,4417, 1246,10695,4585, 1366,10627,4583),
                    Triangle(map, offset, 80, new[] { 57, 81, -1 }, new[] { 0, 1, -1 }, 1270,10616,4417, 1366,10627,4583, 1375,10547,4337),
                    Triangle(map, offset, 81, new[] { 80, -1, 58 }, new[] { 2, -1, 1 }, 1375,10547,4337, 1366,10627,4583, 1491,10558,4533),
                    Triangle(map, offset, 58, new[] { 81, -1, 59 }, new[] { 0, -1, 1 }, 1375,10547,4337, 1491,10558,4533, 1698,10420,4414),
                    Triangle(map, offset, 59, new[] { 58, -1, -1 }, new[] { 0, -1, -1 }, 1375,10547,4337, 1698,10420,4414, 1544,10411,4227)
                }
            };
        }

        private static FieldTriangle Triangle(int map, float offset, int id, int[] neighbors, int[] clones, params int[] coordinates)
        {
            var support = new NavigationPoint[3];
            for (int i = 0; i < support.Length; i++)
                support[i] = new NavigationPoint(coordinates[3 * i] + offset, coordinates[3 * i + 1], coordinates[3 * i + 2]);
            return FieldNativeGeometryContract.CreateTriangle(id, 0, map, new FieldNativeTriangleData
            {
                OriginalVertices = support,
                CrossingVertices = Array.ConvertAll(support, p => new NavigationPoint(p.X, -p.Y, p.Z)),
                RuntimeNeighbors = neighbors,
                NativeNeighbors = neighbors,
                EdgeClones = clones,
                RuntimeFlags = 8193,
                NativeFlags = 8193
            });
        }
    }
}
