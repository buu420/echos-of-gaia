using System;

namespace Memoria.Accessibility.Navigation
{
    internal static class WorldBoatLanding
    {
        internal const int VillageShore = 68;
        internal const int PalaceShore = 69;
        internal const int BoatTerrain = 53;
        private const float ArrivalArea = 2f;

        internal static int Destination(int progress)
        {
            if (progress >= 9400 && progress < 9450)
                return VillageShore;
            if (progress >= 9450 && progress < 9510)
                return PalaceShore;
            return -1;
        }

        internal static bool IsLanding(int id)
        {
            return id == VillageShore || id == PalaceShore;
        }

        internal static NavigationPoint LandingPoint(int id)
        {
            // Points on the original sailing side of a shared coast. The native
            // get-off probe places the player a few units inland on the walking side.
            return id == VillageShore ? new NavigationPoint(955.054932f, 0, -307.120911f) :
                new NavigationPoint(1288.304810f, 0, -309.229248f);
        }

        internal static bool IsNearBeach(NavigationPoint current, int id)
        {
            return WorldCoordinates.Distance(current, LandingPoint(id)) <= ArrivalArea;
        }

        internal static bool IsNearPalaceShore(NavigationPoint position)
        {
            // Keep the embarked objective from sending a player back to the
            // ship immediately after landing, before the pits come into view.
            return WorldCoordinates.Distance(position, LandingPoint(PalaceShore)) <= 130f;
        }

        internal static bool TryGetOff(WorldGeometrySnapshot geometry, NavigationPoint position, int terrain,
            float heading, int boatRadius, uint walkingHigh, uint walkingLow, uint groundHigh, uint groundLow)
        {
            return TryGetOff(geometry, position, terrain, heading, boatRadius, walkingHigh, walkingLow, groundHigh, groundLow, out _);
        }

        internal static bool TryGetOff(WorldGeometrySnapshot geometry, NavigationPoint position, int terrain,
            float heading, int boatRadius, uint walkingHigh, uint walkingLow, uint groundHigh, uint groundLow, out NavigationPoint ashore)
        {
            ashore = default;
            if (geometry == null || !geometry.Complete || geometry.Rules == null || WorldTerrainRules.Topography(terrain) != BoatTerrain)
                return false;

            // World03/09 Confirm reads WPRM 28. Its native search sweeps both
            // directions from starboard, checking eight foot positions per angle.
            var cache = new PrivateWorldCache(geometry.NativeCache);
            for (int sweep = 0; sweep < 8; sweep++)
            {
                int choices = sweep == 0 ? 1 : 2;
                for (int side = 0; side < choices; side++)
                {
                    double angle = (heading + 90 + sweep * (side == 0 ? 22.5 : -22.5)) * Math.PI / 180;
                    float directionX = (float)Math.Sin(angle), directionZ = (float)Math.Cos(angle);
                    WorldSurface hit = null;
                    NavigationPoint point = default;
                    for (int step = 1; step <= 8; step++)
                    {
                        float distance = (boatRadius * step / 4) / 256f;
                        point = WorldCoordinates.Canonical(new NavigationPoint(position.X + directionX * distance,
                            position.Y, position.Z + directionZ * distance));
                        hit = geometry.ProbeFromSky(point, terrain, cache);
                        if (hit == null || !WorldTerrainRules.InMask(walkingHigh, walkingLow, hit.TerrainId))
                        {
                            hit = null;
                            break;
                        }
                    }
                    if (hit != null && !WorldTerrainRules.InMask(groundHigh, groundLow, hit.TerrainId) && hit.TryHeight(point, out var height))
                    {
                        ashore = new NavigationPoint(point.X, height, point.Z);
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
