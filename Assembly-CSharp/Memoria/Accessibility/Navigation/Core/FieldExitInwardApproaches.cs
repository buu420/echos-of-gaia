using System;
using System.Collections.Generic;

namespace Memoria.Accessibility.Navigation
{
    internal static class FieldExitInwardApproaches
    {
        // Room for the normal movement heading to turn before the first touch.
        // Two 60-unit steps plus the four-unit owned waypoint arrival radius
        // leave at least three steering updates on an approach from the stage.
        private const float TurnDistance = 124;
        private static readonly float[] StageDistances = { 180, 240, 320, 400, 520, 640 };

        public static Dictionary<NavigationPoint, NavigationPoint> Create(NavigationPoint[] region,
            NavigationPoint[] candidates, FieldGeometrySnapshot geometry, Func<bool> cancelled, NavigationTarget target = null)
        {
            var approaches = new Dictionary<NavigationPoint, NavigationPoint>();
            if (region == null || region.Length < 3) return approaches;
            if (cancelled != null && cancelled()) return approaches;
            if (target != null && target.PhysicalIdentity.Kind == TargetKind.FieldExit && target.PhysicalIdentity.NativeId == 7 &&
                target.PhysicalIdentity.MapKey == "field:762:FBG_N13_BRMC_MAP274_BU_HAL_1" &&
                geometry.Fingerprint == "8D406CAF00DF6F755B4D499493656F5DD26CD031325353EFCDF545EBE3A78662")
            {
                // Behind this doorway the straight staging line meets the room wall.
                // Approach diagonally from the west, then enter inside the native facing arc.
                var west = new NavigationPoint(-450, 781, 1655);
                var doorway = new NavigationPoint(-192.34381f, 781, 1833.0791f);
                if (!FieldRegionApproaches.CanStopInside(region, west, 0))
                    foreach (var point in candidates)
                        if (NavigationPoint.Distance(point, doorway) < 0.5f &&
                            FieldRegionApproaches.CanStopInside(region, point, FieldAnalogMovement.ArrivalRadius(geometry.Radius, 1)) &&
                            TryStage(west, point, geometry, out var stage))
                        {
                            approaches.Add(stage, point);
                            return approaches;
                        }
            }
            float arrivalRadius = FieldAnalogMovement.ArrivalRadius(geometry.Radius, 1);
            var first = region[0];
            var second = region[1];
            foreach (var entry in candidates)
            {
                if (cancelled != null && cancelled()) break;
                if (!FieldRegionApproaches.CanStopInside(region, entry, arrivalRadius)) continue;
                var projected = FieldExitDestinations.JumpPoint(entry, (int)first.X, (int)first.Z, (int)second.X, (int)second.Z);
                float length = FieldGeometrySnapshot.Horizontal(entry, projected);
                if (length < 1) continue;
                float x = (entry.X - projected.X) / length;
                float z = (entry.Z - projected.Z) / length;
                foreach (float distance in StageDistances)
                {
                    var probe = new NavigationPoint(entry.X + distance * x, entry.Y, entry.Z + distance * z);
                    if (FieldRegionApproaches.CanStopInside(region, probe, -TurnDistance)) continue;
                    NavigationPoint stage;
                    if (!TryStage(probe, entry, geometry, out stage)) continue;
                    if (!approaches.ContainsKey(stage)) approaches.Add(stage, entry);
                    break;
                }
            }
            return approaches;
        }

        private static bool TryStage(NavigationPoint probe, NavigationPoint entry, FieldGeometrySnapshot geometry,
            out NavigationPoint stage)
        {
            stage = default(NavigationPoint);
            float bestHeightDifference = Single.MaxValue;
            foreach (var triangle in geometry.Triangles)
            {
                if (!geometry.Permitted(triangle) || !triangle.Contains(probe)) continue;
                float height = triangle.Height(probe);
                float difference = Math.Abs(height - probe.Y);
                if (difference >= bestHeightDifference) continue;
                var position = new NavigationPoint(probe.X, height, probe.Z);
                if (!geometry.IsSegmentClear(position, position) || !geometry.IsSegmentClear(position, entry)) continue;
                stage = position;
                bestHeightDifference = difference;
            }
            return bestHeightDifference != Single.MaxValue;
        }
    }
}
