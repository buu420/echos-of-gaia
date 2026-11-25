namespace Memoria.Accessibility.Navigation
{
    internal static class PinnacleRocksWalkways
    {
        private const float NativePlayerRadius = 80;
        private static readonly int[] MontyRampWalls = { 5, 6, 7, 12, 13, 15, 17, 18 };
        private static readonly int[] BranchBridgeWalls = { 46, 47, 48, 49 };

        internal static void Review(int field, string walkmesh, FieldGeometrySnapshot geometry)
        {
            if (geometry.Radius != NativePlayerRadius) return;
            // Each group names the walls beside a narrow, continuous native walkway.
            // The clearance applies to the navigation plan. The original controller
            // still owns the player's radius, support and collision response.
            if (field == 1252 && walkmesh == "96d2e3006185ce7cdf6f3ed5b7ea8e2d9f6cb8e7af98cfa6cb4f393dbe37df1b")
                ReviewWalls(geometry, MontyRampWalls, 64);
            if (field == 1254 && walkmesh == "c38f0bd29ea4279b9cf3d27101a699872d147e84de5487198ed3ae78c2dc57d7")
                ReviewWalls(geometry, BranchBridgeWalls, 72);
        }

        internal static float IntermediateArrivalRadius(string scene, FieldGeometrySnapshot geometry, RoutePlan route)
        {
            if (route.FieldSteeringWaypoints == null || route.Corridor == null ||
                (scene != "field:1252:FBG_N26_PNCL_MAP456_PR_PW1_0" && scene != "field:1254:FBG_N26_PNCL_MAP458_PR_PW3_0"))
                return 0;
            foreach (int id in route.Corridor)
            {
                var triangle = geometry.Find(id);
                if (triangle != null && geometry.UsesReviewedBoundaryClearance(triangle)) return 24;
            }
            return 0;
        }

        private static void ReviewWalls(FieldGeometrySnapshot geometry, int[] walls, float clearance)
        {
            foreach (int id in walls)
            {
                var triangle = geometry.Find(id);
                if (triangle == null) continue;
                for (int edge = 0; edge < 3; edge++)
                    if (geometry.IsNativeOuterBoundary(triangle, edge))
                        triangle.ReviewedBoundaryEdges |= 1 << edge;
                triangle.ReviewedBoundaryClearance = clearance;
                triangle.ReviewedBoundaryMaximumRadius = NativePlayerRadius;
            }
        }
    }
}
