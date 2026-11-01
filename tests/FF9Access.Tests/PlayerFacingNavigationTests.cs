using System;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class PlayerFacingNavigationTests
    {
        public static void NativeStartRefusalsGiveThePlayerAnActionInsteadOfAnEnum()
        {TestAssert.True(MovementStartMessage.Read(MovementStartResult.StaleRoute).Contains("request a new route"));TestAssert.True(MovementStartMessage.Read(MovementStartResult.InvalidContext).Contains("when you can move"));TestAssert.False(MovementStartMessage.Read(MovementStartResult.Busy).Contains("Busy"));}
        public static void TechnicalCaptureDiagnosticsDoNotBecomePlayerSpeech()
        {
            var catalog=IntegrationTests.Catalog();catalog.Diagnostics.Add("An entrance candidate has unresolved presentation evidence.");
            var update=new NavigationCoordinator().Tick(TestFixtures.Context(),new NavigationCommand[0],catalog,null);
            TestAssert.Equal(0,new NavigationAnnouncements().Format(update,1000,new SpeechState()).Count);
        }
        public static void InternalFollowerEvidenceIsReplacedByReadableInterruptionReason()
        {
            var coordinator=new NavigationCoordinator();var context=TestFixtures.Context();var catalog=IntegrationTests.Catalog();
            coordinator.Tick(context,new[]{NavigationCommand.StartAutomatic},catalog,null);
            var update=coordinator.Tick(context,new NavigationCommand[0],catalog,new MovementObservation{Outcome=MovementOutcome.Interrupted,Reason=StopReason.NativeClear,Evidence="Native interaction or still-frame threshold cleared the owned path"});
            string spoken=String.Join(" ",update.Messages);
            TestAssert.True(spoken.Contains("stopped"));TestAssert.False(spoken.Contains("threshold") || spoken.Contains("owned path"));
        }
    }
}
