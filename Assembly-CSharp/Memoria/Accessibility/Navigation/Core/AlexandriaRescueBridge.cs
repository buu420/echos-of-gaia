using System;

namespace Memoria.Accessibility.Navigation
{
    internal static class AlexandriaRescueBridge
    {
        // Connected components of the original stairwell walkmesh. The east
        // landing already connects to the downward exit by ordinary walking.
        private static readonly int[] EntranceTriangles = { 5, 8, 10, 11, 13, 16, 66, 75, 134, 135, 136, 137, 144, 147, 148, 149, 157, 159 };
        private static readonly int[] MiddleTriangles = { 138, 139, 140, 141, 168, 169, 170, 171, 172, 173, 174 };

        internal static int BoardingRegion(int triangle)
        {
            if (Array.BinarySearch(EntranceTriangles, triangle) >= 0) return 14;
            if (Array.BinarySearch(MiddleTriangles, triangle) >= 0) return 16;
            return 0;
        }

        // Native 1202, Zidane's tag 15. The two outer ends exchange their
        // destinations depending on the side that started the ride.
        internal static string DismountCue(int shownUid, int side, int boardAngle, int angle)
        {
            if ((shownUid != 4 && shownUid != 21) || (angle != 0 && angle != 8 && angle != 16)) return null;
            bool stairs;
            if (side == 2 && boardAngle == 8)
                stairs = angle == 16;
            else if ((side == 0 || side == 1) && (boardAngle == 0 || boardAngle == 16))
                stairs = (boardAngle == angle) == (side == 1);
            else
                return null;

            if (angle == 8) return "Middle landing. Confirm to jump, or wait for the stairs.";
            return stairs ? "Jump to the stairs. Confirm." : "Entrance landing. Wait for the stairs.";
        }
    }
}
