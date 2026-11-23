using System;

namespace Memoria.Accessibility.Navigation
{
    internal static class LindblumCastlePassage
    {
        private const string MapKey = "field:612:FBG_N11_LDBM_MAP193_LB_MHW_0";
        private const string OriginalGeometry = "86CB40A678E351C45091E71B20A3CB2D44D582D120E0D078D080DA0AB5152263";
        private static readonly int[,] PassageWalls = { { 19, 0 }, { 20, 0 }, { 149, 2 }, { 150, 2 }, { 189, 1 } };

        internal const float IntermediateArrivalRadius = 12;

        internal static bool TryReview(NavigationContext context, NavigationTarget target, FieldGeometrySnapshot geometry,
            out FieldGeometrySnapshot reviewed)
        {
            reviewed = geometry;
            var destination = target.PhysicalIdentity;
            if (context.LogicalMapKey != MapKey || context.StoryProgress != 3140 || context.PlayerIdentity != 3 ||
                geometry.Radius != 128 || destination.Kind != TargetKind.FieldExit ||
                (destination.NativeId != 6 && destination.NativeId != 7) ||
                FieldNativeGeometryContract.Fingerprint(MapKey, geometry) != OriginalGeometry)
                return false;

            // Steiner's original collision forces carry his full body through this
            // passage. Planning adjusts only the five original outer walls in a
            // private snapshot, keeping the native movement fingerprint intact.
            var triangles = (FieldTriangle[])geometry.Triangles.Clone();
            for (int index = 0; index < PassageWalls.GetLength(0); index++)
            {
                int id = PassageWalls[index, 0];
                var triangle = triangles[id];
                triangles[id] = new FieldTriangle
                {
                    Id = triangle.Id, Floor = triangle.Floor, Vertices = triangle.Vertices,
                    RadiusVertices = triangle.RadiusVertices, Neighbors = triangle.Neighbors,
                    Enabled = triangle.Enabled, RuntimeEnabled = triangle.RuntimeEnabled,
                    Attributes = triangle.Attributes, TraversalCost = triangle.TraversalCost, Native = triangle.Native,
                    ReviewedBoundaryEdges = 1 << PassageWalls[index, 1],
                    ReviewedBoundaryClearance = 116, ReviewedBoundaryMaximumRadius = 128
                };
            }

            reviewed = new FieldGeometrySnapshot
            {
                Generation = geometry.Generation, Triangles = triangles, Radius = geometry.Radius,
                ActiveTriangle = geometry.ActiveTriangle, AttributeMask = geometry.AttributeMask,
                BlockedAttributes = geometry.BlockedAttributes, Fingerprint = geometry.Fingerprint
            };
            return true;
        }
    }
}
