namespace Memoria.Accessibility.Navigation
{
    internal static class BurmeciaStatuePassage
    {
        internal const string Courtyard = "field:759:FBG_N13_BRMC_MAP265_BU_CYD_0";
        private const string FallenStatueGeometry = "B58F9644A4C43B58F73802B46A8DA470C536BDD308D45BA518949AC49E52A4C2";

        internal static void ReviewGeometry(string map, FieldGeometrySnapshot geometry)
        {
            if (map != Courtyard || geometry.Radius != 80 || geometry.Triangles.Length != 268 ||
                FieldNativeGeometryContract.Fingerprint(map, geometry) != FallenStatueGeometry)
                return;

            // These two faces have a usable XZ trace and stock adjoining edges.
            // ServiceChar crosses them and follows the statue's narrow sides with the full body radius.
            geometry.Find(179).ReviewedSteepProjection = true;
            geometry.Find(180).ReviewedSteepProjection = true;
            for (int id = 177; id <= 194; id++)
            {
                var triangle = geometry.Find(id);
                for (int edge = 0; edge < 3; edge++)
                    if (geometry.IsNativeOuterBoundary(triangle, edge))
                        triangle.ReviewedBoundaryEdges |= 1 << edge;
                triangle.ReviewedBoundaryClearance = 52;
                triangle.ReviewedBoundaryMaximumRadius = 80;
            }
        }

        internal static bool IsOpen(FieldGeometrySnapshot geometry)
        {
            return geometry?.Find(179)?.ReviewedSteepProjection == true;
        }

        internal static float IntermediateArrivalRadius(string map, FieldGeometrySnapshot geometry, RoutePlan route)
        {
            if (map != Courtyard || !IsOpen(geometry) || route.FieldSteeringWaypoints == null)
                return 0;
            foreach (int id in route.Corridor)
                if (id >= 177 && id <= 194)
                    return 24;
            return 0;
        }
    }
}
