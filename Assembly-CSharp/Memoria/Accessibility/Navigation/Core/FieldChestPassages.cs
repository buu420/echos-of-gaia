using System;

namespace Memoria.Accessibility.Navigation
{
    internal static class FieldChestPassages
    {
        private const string EngineRoom = "field:57:FBG_N00_TSHP_MAP012_TH_EGR_0";
        private const string CleyraEntrance = "field:1003:FBG_N18_GTRE_MAP363_GT_DUN_0";
        private const string IifaTree = "field:1752:FBG_N32_IFUG_MAP565A_IU_EUG_1";
        // Normal control in the Engine Room follows the director disabling triangle 50.
        private const string EngineRoomGeometry = "DE0CA6D0A17915CF4346DCE6B86F4DD0DF07C414FB3C9F3A03177DC6A2CABF32";
        private const string CleyraEntranceGeometry = "518B8091A6D4ECE84BEDEF2EFE3544AE22954F1903292861C3AA5590DD97FAFC";

        // The Iifa Tree director disables triangles 4, 5, 145 and 146 in this phase.
        private const string IifaTreeGeometry = "C4F81ECD185F0D1A0156E49869A8E3A6540495D1807E37EAB7C2F50DB9A196F8";

        private static readonly int[,] EngineRoomWalls = { {38, 1}, {39, 2}, {40, 0}, {41, 1} };
        private static readonly int[,] CleyraEntranceWalls =
        {
            {12, 1}, {13, 1}, {14, 0}, {15, 2}, {17, 1}, {20, 2}, {26, 1}, {59, 1}, {61, 1}
        };

        private static readonly int[,] IifaTreeWalls = { {50, 0}, {86, 2}, {101, 1}, {121, 2}, {123, 0}, {124, 1}, {125, 2} };

        internal static bool TryReview(NavigationContext context, NavigationTarget target, FieldGeometrySnapshot geometry,
            out FieldGeometrySnapshot reviewed, out float intermediateArrival)
        {
            reviewed = geometry;
            intermediateArrival = 0;
            if (!target.IsInteractive || target.UseApprovedApproaches || target.UseViewingPoint || target.RequireInwardApproach ||
                target.InteractionUid >= 0 || geometry.Radius != 80 ||
                target.ActorInteractionRange.BodyClearance != 256 || target.ActorInteractionRange.ContactLimit != 346)
                return false;

            var actor = target.PhysicalIdentity;
            if (actor.Kind != TargetKind.FieldActor || actor.MapKey != context.LogicalMapKey)
                return false;

            int[,] walls;
            string fingerprint;
            float arrival;
            if (actor.MapKey == EngineRoom && actor.NativeId == 6 && At(target.Position, 1055, 0, 967))
            {
                // The same chest begins suspended and reaches the floor only after its native drop.
                walls = EngineRoomWalls;
                fingerprint = EngineRoomGeometry;
                arrival = 16;
            }
            else if (actor.MapKey == CleyraEntrance && actor.NativeId == 3 && At(target.Position, 870, 321.853f, 2932))
            {
                walls = CleyraEntranceWalls;
                fingerprint = CleyraEntranceGeometry;
                arrival = 28;
            }
            else if (actor.MapKey == IifaTree && actor.NativeId == 6 && At(target.Position, -140, -413, -988))
            {
                walls = IifaTreeWalls;
                fingerprint = IifaTreeGeometry;
                arrival = 24;
            }
            else
                return false;

            if (FieldNativeGeometryContract.Fingerprint(actor.MapKey, geometry) != fingerprint)
                return false;

            // These exact walls receive planning clearance. The native controller
            // keeps its body radius and moves through them using its collision forces.
            var triangles = (FieldTriangle[])geometry.Triangles.Clone();
            for (int i = 0; i < walls.GetLength(0); i++)
            {
                int id = walls[i, 0], edge = walls[i, 1];
                var triangle = triangles[id];
                triangles[id] = new FieldTriangle
                {
                    Id = triangle.Id, Floor = triangle.Floor, Vertices = triangle.Vertices,
                    RadiusVertices = triangle.RadiusVertices, Neighbors = triangle.Neighbors,
                    Enabled = triangle.Enabled, RuntimeEnabled = triangle.RuntimeEnabled,
                    Attributes = triangle.Attributes, TraversalCost = triangle.TraversalCost, Native = triangle.Native,
                    ReviewedBoundaryEdges = 1 << edge, ReviewedBoundaryClearance = 56,
                    ReviewedBoundaryMaximumRadius = 80
                };
            }
            reviewed = new FieldGeometrySnapshot
            {
                Generation = geometry.Generation, Triangles = triangles, Radius = geometry.Radius,
                ActiveTriangle = geometry.ActiveTriangle, AttributeMask = geometry.AttributeMask,
                BlockedAttributes = geometry.BlockedAttributes, Fingerprint = geometry.Fingerprint
            };
            intermediateArrival = arrival;
            return true;
        }

        internal static NavigationPoint[] FixedApproaches(NavigationTarget target, FieldGeometrySnapshot geometry)
        {
            if (target.PhysicalIdentity.MapKey != IifaTree)
                return null;

            // The native controller settles here after its wall forces. A more
            // distant approach on the same corridor can stall at the last wall.
            var support = geometry.Find(124);
            var point = new NavigationPoint(-45.1f, 0, -658.6f);
            if (!geometry.Permitted(support) || !support.Contains(point))
                return new NavigationPoint[0];

            point = new NavigationPoint(point.X, support.Height(point), point.Z);
            return geometry.IsSegmentClear(point, point) ? new[] { point } : new NavigationPoint[0];
        }

        private static bool At(NavigationPoint position, float x, float y, float z)
        {
            return Math.Abs(position.X - x) < 1f && Math.Abs(position.Y - y) < 1f && Math.Abs(position.Z - z) < 1f;
        }
    }
}
