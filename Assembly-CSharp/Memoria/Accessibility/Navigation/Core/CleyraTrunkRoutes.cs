using System;

namespace Memoria.Accessibility.Navigation
{
    internal static class CleyraTrunkRoutes
    {
        internal const string MapKey = "field:1009:FBG_N18_GTRE_MAP369_GT_DUN_6";
        // Captured from the original Area 7 BGI before applying any planning clearance.
        private const string OriginalGeometry = "4E75ADCFE4736358A32B51D4798E8106331E12B4EB05B7B021BE83E1D2DBDA9C";
        private static readonly int[,] BridgeWalls = { {35, 1}, {49, 2}, {51, 0}, {109, 1} };
        private static readonly int[,] ChestPathWalls = {
            {26, 0}, {27, 1}, {41, 2}, {50, 0}, {59, 0}, {68, 2}, {82, 0},
            {83, 1}, {84, 2}, {87, 1}, {99, 1}, {107, 1}, {119, 0}, {120, 1},
            {123, 0}, {129, 1}, {132, 1}, {133, 0}, {134, 1}, {135, 2}, {136, 0}
        };

        internal static void ReviewGeometry(string mapKey, FieldGeometrySnapshot geometry)
        {
            if (mapKey != MapKey || geometry.Radius != 80 || geometry.Triangles.Length != 137 ||
                FieldNativeGeometryContract.Fingerprint(mapKey, geometry) != OriginalGeometry)
                return;

            // Opposing walls push Zidane towards the center during native movement.
            // Only these original walls receive planning clearance. All support,
            // adjacency, native collision flags and the controller's body stay intact.
            ReviewWalls(geometry, BridgeWalls);
            ReviewWalls(geometry, ChestPathWalls);
            geometry.HasReviewedCleyraTrunk = true;
        }

        private static void ReviewWalls(FieldGeometrySnapshot geometry, int[,] walls)
        {
            for (int i = 0; i < walls.GetLength(0); i++)
            {
                var triangle = geometry.Find(walls[i, 0]);
                triangle.ReviewedBoundaryEdges = 1 << walls[i, 1];
                triangle.ReviewedBoundaryClearance = 48;
                triangle.ReviewedBoundaryMaximumRadius = 80;
            }
        }

        internal static bool TryChestApproach(NavigationTarget target, FieldGeometrySnapshot geometry, out NavigationPoint point)
        {
            point = default(NavigationPoint);
            if (!geometry.HasReviewedCleyraTrunk || !IsChest(target))
                return false;

            // The native push range reaches the middle of the ledge between
            // the normal angular samples, where those samples fall by the walls.
            var support = geometry.Find(87);
            var center = new NavigationPoint(426, 0, 4322);
            point = new NavigationPoint(center.X, support.Height(center), center.Z);
            return Math.Abs(point.Y - target.Position.Y) < 396 && geometry.IsSegmentClear(point, point);
        }

        internal static float IntermediateRadius(NavigationTarget target, FieldGeometrySnapshot geometry, RoutePlan route)
        {
            if (!geometry.HasReviewedCleyraTrunk || route.FieldSteeringWaypoints == null)
                return 0;
            if (IsChest(target))
                return 40;

            for (int i = 0; i < ChestPathWalls.GetLength(0); i++)
                if (Array.IndexOf(route.Corridor, ChestPathWalls[i, 0]) >= 0)
                    return 24;
            return 0;
        }

        private static bool IsChest(NavigationTarget target)
        {
            return target.IsInteractive && target.InteractionUid < 0 && target.PhysicalIdentity.Kind == TargetKind.FieldActor &&
                target.PhysicalIdentity.NativeId == 2 && Math.Abs(target.Position.X - 717) < 1 &&
                Math.Abs(target.Position.Z - 4415) < 1;
        }
    }
}
