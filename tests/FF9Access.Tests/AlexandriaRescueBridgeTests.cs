using Memoria.Accessibility.Navigation;

namespace FF9Access.Tests
{
    internal static class AlexandriaRescueBridgeTests
    {
        public static void AlexandriaDoorFacingRequiresTheReviewedRegionAndScript()
        {
            CheckFacing(1204, 8, "af678b3663d71a8553907051607df32f25dff192aff2b444301f1c342d44f140");
            CheckFacing(1213, 11, "41ddf8a923f8204d5e33e3b611d4bb29557b7b3bb8b1772c98536f3b8dd42244");
            CheckFacing(1214, 6, "723c3a9351a0bc66bffde01800361b02f561f037ba1c68d23c6f49c3e2cec1ab");
            CheckFacing(1224, 7, "2a80aeaddc48caacc590e60effdf6ea868428f553ce072b6ae96304b89f531bd");
            TestAssert.False(FieldExitDestinations.RequiresInwardApproach(1204, 9, 9,
                "63191fd64b0a338c40102a6ba8c3a5e2f025d2c9eec8e50a211f4ef9c65b234b"));
        }

        private static void CheckFacing(int field, int actor, string script)
        {
            TestAssert.True(FieldExitDestinations.ReviewedEntryFacing(field, actor, actor, script));
            TestAssert.True(FieldExitDestinations.RequiresInwardApproach(field, actor, actor, script));
            TestAssert.False(FieldExitDestinations.ReviewedEntryFacing(field, actor, actor + 1, script));
            TestAssert.False(FieldExitDestinations.ReviewedEntryFacing(field, actor, actor, script + "0"));
        }

        public static void TheBridgeApproachUsesThePlayersSupportingTriangle()
        {
            TestAssert.Equal(14, AlexandriaRescueBridge.BoardingRegion(5));
            TestAssert.Equal(14, AlexandriaRescueBridge.BoardingRegion(159));
            TestAssert.Equal(16, AlexandriaRescueBridge.BoardingRegion(169));
            TestAssert.Equal(0, AlexandriaRescueBridge.BoardingRegion(142));
            TestAssert.Equal(0, AlexandriaRescueBridge.BoardingRegion(77));
            TestAssert.Equal(0, AlexandriaRescueBridge.BoardingRegion(-1));
        }

        public static void DismountSpeechRequiresTheNativePromptAndDistinguishesTheDestination()
        {
            TestAssert.Equal(null, AlexandriaRescueBridge.DismountCue(-1, 0, 0, 16));
            TestAssert.Equal("Jump to the stairs. Confirm.", AlexandriaRescueBridge.DismountCue(4, 0, 0, 16));
            TestAssert.Equal("Jump to the stairs. Confirm.", AlexandriaRescueBridge.DismountCue(4, 0, 16, 0));
            TestAssert.Equal("Jump to the stairs. Confirm.", AlexandriaRescueBridge.DismountCue(4, 1, 16, 16));
            TestAssert.Equal("Jump to the stairs. Confirm.", AlexandriaRescueBridge.DismountCue(4, 2, 8, 16));
            TestAssert.Equal("Jump to the stairs. Confirm.", AlexandriaRescueBridge.DismountCue(21, 0, 0, 16));
            TestAssert.Equal("Entrance landing. Wait for the stairs.", AlexandriaRescueBridge.DismountCue(21, 0, 0, 0));
            TestAssert.Equal("Middle landing. Confirm to jump, or wait for the stairs.", AlexandriaRescueBridge.DismountCue(4, 0, 0, 8));
            TestAssert.Equal("Entrance landing. Wait for the stairs.", AlexandriaRescueBridge.DismountCue(4, 0, 0, 0));
            TestAssert.Equal(null, AlexandriaRescueBridge.DismountCue(4, 0, 8, 16));
            TestAssert.Equal(null, AlexandriaRescueBridge.DismountCue(4, 2, 8, 9));
        }
    }
}
