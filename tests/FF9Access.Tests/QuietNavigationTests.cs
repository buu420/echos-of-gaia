using System;
using System.Linq;
using Memoria.Accessibility.Navigation;

namespace FF9Access.Tests
{
    internal static class QuietNavigationTests
    {
        private static PhysicalInputSnapshot Pad(NavigationButton buttons=NavigationButton.None,float trigger=0)
        { return new PhysicalInputSnapshot{ControllerConnected=true,Buttons=buttons,LeftTrigger=trigger}; }
        private static NavigationInputBroker Broker()
        { var broker=new NavigationInputBroker(NavigationBindings.Parse(null).Bindings);broker.Update(TestFixtures.Context(),Pad());return broker; }
        private static void Require(bool condition,string message) { if(!condition)throw new Exception(message); }
        private static NavigationCommand Help()
        { NavigationCommand command;Require(Enum.TryParse("Help",out command),"controller help is unavailable");return command; }

        public static void EveryTriggerOpeningReadsSelectionWithoutTutorial()
        {
            var coordinator=new NavigationCoordinator();var broker=Broker();var context=TestFixtures.Context();var catalog=IntegrationTests.Catalog();
            for(int i=0;i<3;i++) {
                var update=coordinator.Tick(context,broker.Update(context,Pad(trigger:0.8f)),catalog,null);
                string spoken=String.Join(" ",update.Messages);
                Require(spoken.Contains("Exits") && spoken.Contains(update.Selected.Label),"opening lost the category or selected target");
                Require(!spoken.Contains("D-pad"),"opening repeats the full controls tutorial");
                Require(spoken.Contains("1 of"),"opening lost the target's position in the list");
                coordinator.Tick(context,broker.Update(context,Pad()),catalog,null);
            }
        }
        public static void PausedRouteDoesNotTellControllerPlayersToUseKeyboardKeys()
        {
            var coordinator=new NavigationCoordinator();var context=TestFixtures.Context();var catalog=IntegrationTests.Catalog();
            coordinator.Tick(context,new[]{NavigationCommand.StartAutomatic},catalog,null);
            context.CanControl=false;context.GateReason=GateReason.Menu;
            string spoken=String.Join(" ",coordinator.Tick(context,new NavigationCommand[0],catalog,null).Messages);
            TestAssert.True(spoken.Contains("Navigation paused"));
            Require(!spoken.Contains("I resumes") && !spoken.Contains("P resumes"),"pause advertises keyboard-only resume controls");
        }
        public static void HelpRequiresFreshTopFacePressInsideTriggerNavigation()
        {
            var broker=Broker();var context=TestFixtures.Context();var help=Help();
            TestAssert.False(broker.Update(context,Pad(NavigationButton.OtherFace)).Contains(help));
            broker.Update(context,Pad(NavigationButton.OtherFace,0.8f));
            TestAssert.False(broker.Update(context,Pad(NavigationButton.OtherFace,0.8f)).Contains(help));
            broker.Update(context,Pad(trigger:0.8f));
            var commands=broker.Update(context,Pad(NavigationButton.OtherFace,0.8f));
            Require(commands.Contains(help),"fresh Triangle/Y did not request help");
            TestAssert.False(commands.Contains(NavigationCommand.Confirm) || commands.Contains(NavigationCommand.StartAutomatic));
            TestAssert.False(broker.FilterButton(NavigationButton.OtherFace,true));
            Require(!broker.Update(context,Pad(NavigationButton.OtherFace,0.8f)).Contains(help),"held help repeats");
            broker.Update(context,Pad(NavigationButton.OtherFace));
            Require(!broker.FilterButton(NavigationButton.OtherFace,true),"released trigger leaked held Menu/Confirm");
            broker.Update(context,Pad());broker.Update(context,Pad());
            TestAssert.True(broker.FilterButton(NavigationButton.OtherFace,true));
        }
        public static void HelpDoesNotStartTravelOrChangeSelection()
        {
            var coordinator=new NavigationCoordinator();var context=TestFixtures.Context();var catalog=IntegrationTests.Catalog();
            var opened=coordinator.Tick(context,new[]{NavigationCommand.ModifierOpened},catalog,null);
            var update=coordinator.Tick(context,new[]{Help()},catalog,null);
            TestAssert.Equal(opened.Selected.Id,update.Selected.Id);
            TestAssert.Equal(RouteOperation.None,update.RouteOperation);
            TestAssert.False(update.Session.IsAutomatic);
            string spoken=String.Join(" ",update.Messages);
            Require(spoken.Contains("D-pad") && spoken.Contains("L3") && spoken.Contains("R3"),"requested help lost the control instructions");
        }
        public static void BothTriggersAndNativeMenuDoNotRequestHelp()
        {
            var help=Help();var broker=Broker();var context=TestFixtures.Context();
            var both=Pad(trigger:0.8f);both.RightTrigger=0.8f;broker.Update(context,both);
            both.Buttons=NavigationButton.OtherFace;TestAssert.False(broker.Update(context,both).Contains(help));
            broker=Broker();context.CanControl=context.CanInspect=false;context.GateReason=GateReason.Menu;
            broker.Update(context,Pad(trigger:0.8f));TestAssert.False(broker.Update(context,Pad(NavigationButton.OtherFace,0.8f)).Contains(help));
        }
        public static void ExplicitHelpUsesConfiguredControllerNames()
        {
            foreach(string naming in new[]{"PlayStation","Xbox","Generic"}) {
                var update=new NavigationCoordinator().Tick(TestFixtures.Context(),new[]{Help()},IntegrationTests.Catalog(),null,naming);
                string spoken=String.Join(" ",update.Messages);
                TestAssert.True(spoken.Contains(naming=="PlayStation"?"Circle":naming=="Xbox"?"B stop":"right face button"));
            }
        }
        public static void PreparingWorldListStillIdentifiesTheCurrentCategory()
        {
            var context=TestFixtures.Context("world:0");
            var catalog=new CatalogResult{UnavailableReason="World destinations are being checked."};
            var update=new NavigationCoordinator().Tick(context,new[]{NavigationCommand.ModifierOpened},catalog,null);
            string spoken=String.Join(" ",update.Messages);
            Require(spoken.Contains("Towns") && spoken.Contains("being checked"),"opening an unavailable list lost the current category");
            TestAssert.False(spoken.Contains("D-pad"));
        }
    }
}
