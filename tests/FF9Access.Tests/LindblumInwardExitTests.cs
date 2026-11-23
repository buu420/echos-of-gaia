using System;
using System.Reflection;
using Memoria.Accessibility.Navigation;

namespace FF9Access.Tests
{
    internal static class LindblumInwardExitTests
    {
        public static void OriginalLindblumDoorScriptsUseInwardApproaches()
        {
            CheckReview(555, 20, "a3daba2e9f09ccf1cb8d3661e09046915f5aa5938d307fea212971d573f0fc63");
            CheckReview(556, 19, "abadf9abb6d5dbd5bf18fde3b3633f2c46add0ebfdc380c0e7b556ac0ad0bd50");
            CheckReview(570, 24, "d5a671438d494f979fc3b35a22bb09246fc30657f430b30ea4cbdc95f3699d97");
            CheckReview(1302, 12, "cc9171c8d4949cb580c3daad8b8cc7e8aad2d370cc6ad0866ee6580942af4c44");
            CheckReview(1305, 14, "4b1bf936225fb3dd9b21af833e9643538bba38383d00fa307474664d69b3bf60");
            CheckReview(2102, 9, "b5bc9cb45d89eeed1d2c50168644510372700fc934335b89effe3bfa2d8e7b47");
            CheckReview(2105, 16, "bb5c9c5b8e705e94c7d882223c95382a2e5547af1487a16626734865d7d9198f");
            CheckReview(2170, 7, "24c2f539b0f08c00d957cefd8fcd65509016f4dadd82c387c45a251bc49239ac");
            TestAssert.False(FieldExitDestinations.RequiresInwardApproach(619, 7, 7, "b97c53476bf1b41c6cf60b10152009a90e70ce021c3caf4250fa12105510c370"));
        }

        public static void ReenteringADoorStagesOutsideThenTurnsInward()
        {
            var target = DoorTarget();
            var geometry = Room(-600);
            var plan = new FieldRoutePlanner().Plan(Context(), target, geometry);
            TestAssert.True(plan.Success);
            var waypoints = plan.FieldSteeringWaypoints ?? plan.Waypoints;
            TestAssert.True(waypoints.Length >= 2);
            var stage = waypoints[waypoints.Length - 2];
            var entry = waypoints[waypoints.Length - 1];
            TestAssert.True(stage.X <= -224);
            TestAssert.Equal(Point(100, 0), entry);
            TestAssert.True(geometry.IsSegmentClear(stage, entry));
            TestAssert.Equal(entry, plan.ApproachPoint);
            TestAssert.True(plan.Length >= 600);
        }

        public static void InwardLegKeepsTheFullRadiusAtTheOutsideWall()
        {
            var geometry = Room(-280);
            var target = DoorTarget();
            var plan = new FieldRoutePlanner().Plan(Context(), target, geometry);
            TestAssert.False(plan.Success);
            TestAssert.Equal(RouteFailure.TargetOutsideMesh, plan.Failure);
            target.RequireInwardApproach = false;
            TestAssert.True(new FieldRoutePlanner().Plan(Context(), target, geometry).Success);
        }

        private static void CheckReview(int field, int sid, string hash)
        {
            if (!FieldExitDestinations.ReviewedEntryFacing(field, sid, sid, hash))
                throw new Exception("Original Lindblum door script " + field + "/" + sid + " is not reviewed");
            TestAssert.True(FieldExitDestinations.RequiresInwardApproach(field, sid, sid, hash));
            TestAssert.False(FieldExitDestinations.ReviewedEntryFacing(field, sid, sid + 1, hash));
            TestAssert.False(FieldExitDestinations.ReviewedEntryFacing(field, sid, sid, hash + "0"));
            TestAssert.False(FieldExitDestinations.ReviewedEntryFacing(field + 1, sid, sid, hash));
        }

        private static NavigationPoint Point(float x, float z) => new NavigationPoint(x, 0, z);

        private static FieldGeometrySnapshot Room(int left)
        {
            var a = Point(left, -350);
            var b = Point(800, -350);
            var c = Point(800, 350);
            var d = Point(left, 350);
            return new FieldGeometrySnapshot
            {
                Generation = 1, Radius = 80,
                Triangles = new[]
                {
                    new FieldTriangle { Id = 0, Vertices = new[] { a, b, c }, Neighbors = new[] { -1, -1, 1 } },
                    new FieldTriangle { Id = 1, Vertices = new[] { a, c, d }, Neighbors = new[] { 0, -1, -1 } }
                }
            };
        }

        private static NavigationTarget DoorTarget()
        {
            var target = new NavigationTarget
            {
                Generation = 1, Id = new TargetIdentity(TargetKind.FieldExit, "field:556", 19),
                UseApprovedApproaches = true, ApproachCandidates = new[] { Point(100, 0) },
                InteractionRegion = new[] { Point(200, -100), Point(200, 100), Point(-100, 100), Point(-100, -100) }
            };
            target.RequireInwardApproach = true;
            return target;
        }

        private static NavigationContext Context() =>
            new NavigationContext { Generation = 1, LogicalMapKey = "field:556", Position = Point(150, 0), CanControl = true };
    }
}
