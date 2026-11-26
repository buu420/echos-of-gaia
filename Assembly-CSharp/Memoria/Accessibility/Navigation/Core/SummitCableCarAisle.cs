using System;

namespace Memoria.Accessibility.Navigation
{
    internal static class SummitCableCarAisle
    {
        private const string Map = "field:815:FBG_N16_STGT_MAP341_SG_TRN_2";
        private const string Geometry = "2351B521C89BE36665507D94E60CA7899DA12E29ADD9A6FDCCC862A50D919F7E";
        private static readonly NavigationPoint Approach = new NavigationPoint(1, 523, -316);
        private static readonly int[] AisleTriangles = { 148, 151, 84, 83, 42, 9 };
        private static readonly int[,] AisleWalls = { { 9, 2 }, { 42, 1 }, { 50, 1 }, { 77, 2 }, { 82, 0 }, { 85, 1 } };

        internal static RoutePlan Plan(NavigationContext context, NavigationTarget target, FieldGeometrySnapshot geometry)
        {
            if (!Matches(context, target, geometry)) return null;
            var origin = context.Position;
            int start = AisleStart(origin, geometry.ActiveTriangle);
            if (start < 0) return null;
            var reviewed = Review(geometry);
            NavigationPoint[] surface;
            if (!reviewed.TrySegmentClear(origin, Approach, out surface)) return null;

            // The original controller crosses the doorway with forward motion.
            // Keep one steering goal past the two opposing walls where it can stop.
            var corridor = new int[AisleTriangles.Length - start];
            Array.Copy(AisleTriangles, start, corridor, 0, corridor.Length);
            return new RoutePlan
            {
                Success = true, TargetId = target.Id, PhysicalId = target.PhysicalId,
                Generation = context.Generation, Transport = context.Transport,
                Origin = origin, ApproachPoint = Approach, Waypoints = surface, FieldSteeringWaypoints = new[] { Approach },
                Corridor = corridor, Length = FieldGeometrySnapshot.Horizontal(origin, Approach), GeometryFingerprint = geometry.Fingerprint
            };
        }

        internal static bool TryGuideGeometry(NavigationContext context, NavigationTarget target, FieldGeometrySnapshot geometry,
            RoutePlan route, out FieldGeometrySnapshot reviewed)
        {
            reviewed = geometry;
            if (!route.Success || !route.ApproachPoint.Equals(Approach) || !route.PhysicalIdentity.Equals(target.PhysicalIdentity) ||
                route.GeometryFingerprint != geometry.Fingerprint || !Matches(context, target, geometry) ||
                AisleStart(context.Position, geometry.ActiveTriangle) < 0) return false;
            reviewed = Review(geometry);
            return true;
        }

        private static int AisleStart(NavigationPoint position, int triangle)
        {
            if (position.X < -12 || position.X > 12 || position.Z < -514 || position.Z >= Approach.Z || Math.Abs(position.Y - Approach.Y) >= 1f)
                return -1;
            return Array.IndexOf(AisleTriangles, triangle);
        }

        private static bool Matches(NavigationContext context, NavigationTarget target, FieldGeometrySnapshot geometry)
        {
            var actor = target.PhysicalIdentity;
            return context.LogicalMapKey == Map && context.CanControl && context.StoryProgress == 4230 && context.PlayerIdentity == 9 &&
                actor.Kind == TargetKind.FieldActor && actor.MapKey == Map && actor.NativeId == 10 && target.IsInteractive && target.InteractionUid < 0 &&
                target.ActorInteractionRange.BodyClearance == 208 && target.ActorInteractionRange.ContactLimit == 406 &&
                Math.Abs(target.Position.X + 183) < 4f && Math.Abs(target.Position.Y - 584) < 1f && Math.Abs(target.Position.Z - 26) < 4f &&
                geometry.Radius == 80 && geometry.Fingerprint == Geometry;
        }

        private static FieldGeometrySnapshot Review(FieldGeometrySnapshot geometry)
        {
            // ServiceChar replays at the original radius traverse these six walls
            // using native collision forces. Only this planning snapshot is adjusted.
            var triangles = (FieldTriangle[])geometry.Triangles.Clone();
            for (int index = 0; index < AisleWalls.GetLength(0); index++)
            {
                int id = AisleWalls[index, 0];
                var triangle = triangles[id];
                triangles[id] = new FieldTriangle
                {
                    Id = id, Floor = triangle.Floor, Vertices = triangle.Vertices, RadiusVertices = triangle.RadiusVertices,
                    Neighbors = triangle.Neighbors, Enabled = triangle.Enabled, RuntimeEnabled = triangle.RuntimeEnabled,
                    Attributes = triangle.Attributes, TraversalCost = triangle.TraversalCost, Native = triangle.Native,
                    ReviewedBoundaryEdges = 1 << AisleWalls[index, 1], ReviewedBoundaryClearance = 56, ReviewedBoundaryMaximumRadius = 80
                };
            }
            return new FieldGeometrySnapshot
            {
                Generation = geometry.Generation, Triangles = triangles, Radius = geometry.Radius, ActiveTriangle = geometry.ActiveTriangle,
                AttributeMask = geometry.AttributeMask, BlockedAttributes = geometry.BlockedAttributes, Fingerprint = geometry.Fingerprint
            };
        }
    }
}
