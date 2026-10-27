using System;
using Memoria.Accessibility.Navigation;
using XInputDotNetPure;
namespace FF9Access.Tests
{
    internal static class ControllerChordTests
    {
        private static PhysicalInputSnapshot Pad(NavigationButton buttons=NavigationButton.None,float left=0,float right=0)
        { return new PhysicalInputSnapshot { ControllerConnected=true,Buttons=buttons,LeftTrigger=left,RightTrigger=right }; }
        private static NavigationInputBroker Broker()
        { var result=new NavigationInputBroker(NavigationBindings.Parse(null).Bindings);result.Update(TestFixtures.Context(),Pad());return result; }
        private static bool Has(NavigationCommand[] commands,NavigationCommand command){return Array.IndexOf(commands,command)>=0;}
        private static void Open(NavigationInputBroker broker,bool right=false){broker.Update(TestFixtures.Context(),Pad(left:right?0:0.8f,right:right?0.8f:0));}
        public static void StandaloneStickClicksKeepNativeControls()
        {
            foreach(var click in new[]{NavigationButton.R3,NavigationButton.L3}) {
                var broker=Broker();var commands=broker.Update(TestFixtures.Context(),Pad(click));
                TestAssert.Equal(0,commands.Length);TestAssert.False(broker.CaptureTail);
                TestAssert.True(broker.FilterButton(click,true));
                commands=broker.Update(TestFixtures.Context(),Pad(NavigationButton.DpadDown));
                TestAssert.True(Has(commands,NavigationCommand.ManualMovement));
                TestAssert.False(Has(commands,NavigationCommand.NextTarget));
                commands=broker.Update(TestFixtures.Context(),Pad(NavigationButton.Confirm));
                TestAssert.True(Has(commands,NavigationCommand.Confirm));
                TestAssert.False(Has(commands,NavigationCommand.StartGuidance));
                TestAssert.True(broker.FilterButton(NavigationButton.Confirm,true));
            }
        }
        public static void BothTriggerChoicesUseTheSameDirections()
        {
            foreach(bool right in new[]{false,true})foreach(var pair in new[]{
                new[]{NavigationButton.DpadLeft,(NavigationButton)(int)NavigationCommand.PreviousCategory},
                new[]{NavigationButton.DpadRight,(NavigationButton)(int)NavigationCommand.NextCategory},
                new[]{NavigationButton.DpadUp,(NavigationButton)(int)NavigationCommand.PreviousTarget},
                new[]{NavigationButton.DpadDown,(NavigationButton)(int)NavigationCommand.NextTarget}})
            {
                var broker=Broker();Open(broker,right);
                var commands=broker.Update(TestFixtures.Context(),Pad(pair[0],right?0:0.8f,right?0.8f:0));
                TestAssert.True(Has(commands,(NavigationCommand)(int)pair[1]));
                TestAssert.False(Has(commands,NavigationCommand.ManualMovement));
            }
        }
        public static void ExplicitStartsAndStopMatchBlindSoldier()
        {
            foreach(var pair in new[]{new[]{NavigationButton.L3,(NavigationButton)(int)NavigationCommand.StartGuidance},new[]{NavigationButton.R3,(NavigationButton)(int)NavigationCommand.StartAutomatic},new[]{NavigationButton.Cancel,(NavigationButton)(int)NavigationCommand.Stop}})
            {
                var broker=Broker();Open(broker);var raw=Pad(pair[0],0.8f);
                var commands=broker.Update(TestFixtures.Context(),raw);
                TestAssert.True(Has(commands,(NavigationCommand)(int)pair[1]));
                TestAssert.False(Has(commands,NavigationCommand.Confirm));TestAssert.False(Has(commands,NavigationCommand.ManualMovement));
                TestAssert.Equal(0,broker.Update(TestFixtures.Context(),raw).Length);
                broker.Update(TestFixtures.Context(),Pad(NavigationButton.None,0.8f));
                TestAssert.False(Has(broker.Update(TestFixtures.Context(),raw),(NavigationCommand)(int)pair[1]));
            }
        }
        public static void ModifiedTriangleAndConfirmCannotReachTheGame()
        {
            var broker=Broker();Open(broker);var raw=Pad(NavigationButton.OtherFace|NavigationButton.Confirm,0.8f);raw.GameConfirmButton=NavigationButton.OtherFace;
            var commands=broker.Update(TestFixtures.Context(),raw);
            TestAssert.False(Has(commands,NavigationCommand.Confirm));TestAssert.False(Has(commands,NavigationCommand.StartGuidance));
            TestAssert.False(broker.FilterButton(NavigationButton.OtherFace,true));TestAssert.False(broker.FilterButton(NavigationButton.Confirm,true));
            broker.Update(TestFixtures.Context(),Pad());broker.Update(TestFixtures.Context(),Pad());
            TestAssert.True(broker.FilterButton(NavigationButton.OtherFace,true));TestAssert.True(broker.FilterButton(NavigationButton.Confirm,true));
        }
        public static void UnmodifiedTriangleAndConfirmRetainNativeActions()
        {
            var broker=Broker();var commands=broker.Update(TestFixtures.Context(),Pad(NavigationButton.OtherFace|NavigationButton.Confirm));
            TestAssert.True(Has(commands,NavigationCommand.Confirm));TestAssert.True(broker.FilterButton(NavigationButton.OtherFace,true));
            TestAssert.True(broker.FilterButton(NavigationButton.Confirm,true));
        }
        public static void TriggerCaptureCoversPhysicalAxesAndRawGamepadView()
        {
            var broker=Broker();Open(broker);
            var native=new GamePadState.RawState();native.dwPacketNumber=9;native.Gamepad.wButtons=0xffff;native.Gamepad.bLeftTrigger=200;native.Gamepad.bRightTrigger=180;native.Gamepad.sThumbLX=16000;native.Gamepad.sThumbRY=17000;
            var state=new GamePadState(true,native,GamePadDeadZone.None).WithCapture(broker.CapturedButtons,broker.CapturedAxes);
            TestAssert.Equal(ButtonState.Released,state.Buttons.Y);TestAssert.Equal(ButtonState.Released,state.DPad.Left);
            TestAssert.Near(0,state.Triggers.Left,0);TestAssert.Near(0,state.Triggers.Right,0);TestAssert.Near(0,state.ThumbSticks.Left.X,0);TestAssert.Near(0,state.ThumbSticks.Right.Y,0);
            TestAssert.Equal(9u,state.PacketNumber);TestAssert.False(broker.IsKeyCaptured("Return"));
        }
        public static void TriggerReleaseCannotLeakAStillHeldDirectionOrMenuButton()
        {
            var broker=Broker();Open(broker);broker.Update(TestFixtures.Context(),Pad(NavigationButton.R3|NavigationButton.DpadDown,0.8f));
            var commands=broker.Update(TestFixtures.Context(),Pad(NavigationButton.R3|NavigationButton.DpadDown));
            TestAssert.False(Has(commands,NavigationCommand.ManualMovement));
            TestAssert.False(broker.FilterButton(NavigationButton.DpadDown,true));TestAssert.False(broker.FilterButton(NavigationButton.R3,true));
            broker.Update(TestFixtures.Context(),Pad());broker.Update(TestFixtures.Context(),Pad());
            TestAssert.True(broker.FilterButton(NavigationButton.DpadDown,true));TestAssert.True(broker.FilterButton(NavigationButton.R3,true));
        }
        public static void DirectionHeldBeforeTheTriggerNeedsANewPress()
        {
            var broker=Broker();broker.Update(TestFixtures.Context(),Pad(NavigationButton.DpadDown));
            var raw=Pad(NavigationButton.DpadDown,0.8f);
            TestAssert.False(Has(broker.Update(TestFixtures.Context(),raw),NavigationCommand.NextTarget));
            TestAssert.False(Has(broker.Update(TestFixtures.Context(),raw),NavigationCommand.NextTarget));
            broker.Update(TestFixtures.Context(),Pad(NavigationButton.None,0.8f));
            TestAssert.True(Has(broker.Update(TestFixtures.Context(),raw),NavigationCommand.NextTarget));
        }
        public static void FocusReturnDoesNotReuseAHeldModifier()
        {
            var broker=Broker();Open(broker);var background=TestFixtures.Context();background.Foreground=false;
            broker.Update(background,Pad(NavigationButton.None,0.8f));
            TestAssert.False(Has(broker.Update(TestFixtures.Context(),Pad(NavigationButton.DpadDown,0.8f)),NavigationCommand.NextTarget));
            broker.Update(TestFixtures.Context(),Pad());Open(broker);
            TestAssert.True(Has(broker.Update(TestFixtures.Context(),Pad(NavigationButton.DpadDown,0.8f)),NavigationCommand.NextTarget));
        }
        public static void NewSceneAndBattleReturnRequireAReleasedModifier()
        {
            var broker=Broker();Open(broker);var next=TestFixtures.Context(generation:2);
            broker.Update(next,Pad(NavigationButton.None,0.8f));
            TestAssert.False(Has(broker.Update(next,Pad(NavigationButton.DpadDown,0.8f)),NavigationCommand.NextTarget));
            broker.Suspend();var held=Pad(NavigationButton.R3,0.8f);
            TestAssert.False(Has(broker.Update(next,held),NavigationCommand.StartAutomatic));
            TestAssert.False(Has(broker.Update(next,Pad(NavigationButton.DpadUp,0.8f)),NavigationCommand.PreviousTarget));
        }
        public static void ReconnectRequiresANewModifierPress()
        {
            var broker=Broker();Open(broker);broker.Update(TestFixtures.Context(),new PhysicalInputSnapshot());
            TestAssert.False(Has(broker.Update(TestFixtures.Context(),Pad(NavigationButton.None,0.8f)),NavigationCommand.StartAutomatic));
            TestAssert.False(Has(broker.Update(TestFixtures.Context(),Pad(NavigationButton.DpadDown,0.8f)),NavigationCommand.NextTarget));
            broker.Update(TestFixtures.Context(),Pad());Open(broker);
            TestAssert.True(Has(broker.Update(TestFixtures.Context(),Pad(NavigationButton.DpadDown,0.8f)),NavigationCommand.NextTarget));
        }
        public static void NativeMenuAndHotColdControlsRemainAvailable()
        {
            foreach(bool minigame in new[]{false,true}) {
                var broker=Broker();var context=TestFixtures.Context();context.CanControl=false;context.CanInspect=minigame;context.MinigameInspection=minigame;
                var commands=broker.Update(context,Pad(NavigationButton.DpadDown|NavigationButton.Automatic,0.8f));
                TestAssert.False(Has(commands,NavigationCommand.NextTarget));TestAssert.False(Has(commands,NavigationCommand.StartAutomatic));
                TestAssert.True(broker.FilterButton(NavigationButton.DpadDown,true));TestAssert.Near(0.8f,broker.FilterAxis(NavigationAxis.LeftTrigger,0.8f),0);
            }
        }
        public static void SmallTriggerDriftDoesNotCaptureOrOpenAnything()
        {
            var broker=Broker();var commands=broker.Update(TestFixtures.Context(),Pad(NavigationButton.None,0.1f,0.1f));
            TestAssert.Equal(0,commands.Length);TestAssert.False(broker.CaptureTail);
        }
        public static void ModifierPausesAnOldRouteAndExplicitStartSurvivesRelease()
        {
            var broker=Broker();var coordinator=new NavigationCoordinator();var context=TestFixtures.Context();
            var catalog=IntegrationTests.Catalog();
            coordinator.Tick(context,new[]{NavigationCommand.StartAutomatic},catalog,null);
            var opened=coordinator.Tick(context,broker.Update(context,Pad(left:0.8f)),catalog,null);
            TestAssert.True(opened.Session.ReleaseMovement);TestAssert.Equal(RouteMode.Paused,opened.Session.Mode);
            TestAssert.True(opened.ModifierOpened);
            var started=coordinator.Tick(context,broker.Update(context,Pad(NavigationButton.R3,0.8f)),catalog,null);
            TestAssert.Equal(RouteMode.Moving,started.Session.Mode);TestAssert.Equal(RouteOperation.StartAutomatic,started.RouteOperation);
            var held=coordinator.Tick(context,broker.Update(context,Pad(NavigationButton.R3,0.8f)),catalog,null);
            TestAssert.Equal(RouteMode.Moving,held.Session.Mode);
            var released=coordinator.Tick(context,broker.Update(context,Pad(NavigationButton.R3)),catalog,null);
            TestAssert.Equal(RouteMode.Moving,released.Session.Mode);
        }
        public static void HoldingBothTriggersCannotIssueAStartOrCategoryChange()
        {
            var broker=Broker();broker.Update(TestFixtures.Context(),Pad(left:0.8f,right:0.8f));
            var commands=broker.Update(TestFixtures.Context(),Pad(NavigationButton.R3|NavigationButton.DpadRight,0.8f,0.8f));
            TestAssert.Equal(0,commands.Length);
            TestAssert.False(broker.FilterButton(NavigationButton.R3,true));
        }
        public static void ATriggerHeldInTheNativeMenuNeedsReleaseBeforeBrowsing()
        {
            var broker=Broker();var menu=TestFixtures.Context();menu.CanControl=false;menu.CanInspect=false;menu.GateReason=GateReason.Menu;
            broker.Update(menu,Pad(left:0.8f));
            var commands=broker.Update(TestFixtures.Context(),Pad(NavigationButton.DpadDown,0.8f));
            TestAssert.False(Has(commands,NavigationCommand.NextTarget));
            broker.Update(TestFixtures.Context(),Pad());Open(broker);
            TestAssert.True(Has(broker.Update(TestFixtures.Context(),Pad(NavigationButton.DpadDown,0.8f)),NavigationCommand.NextTarget));
        }
        public static void RemappedTriggerConfirmRetainsItsOrdinaryAction()
        {
            var broker=Broker();var raw=Pad(left:0.8f);raw.GameConfirmButton=NavigationButton.None;raw.GameConfirmExtra=true;
            raw.LeftNavigationModifier=ControllerKeyBindings.TriggerAvailable("LeftTrigger",new[]{"LeftTrigger","JoystickButton1","JoystickButton3","JoystickButton2"});
            var commands=broker.Update(TestFixtures.Context(),raw);
            TestAssert.True(Has(commands,NavigationCommand.Confirm));
            TestAssert.Near(0.8f,broker.FilterAxis(NavigationAxis.LeftTrigger,0.8f),0);
            TestAssert.False(Has(commands,NavigationCommand.ModifierOpened));
        }
        public static void NativeCriticalTriggerBindingsLeaveTheOtherModifierAvailable()
        {
            foreach(int index in new[]{0,1,2,3,8,9}) {
                var bindings=new[]{"JoystickButton0","JoystickButton1","JoystickButton3","JoystickButton2","JoystickButton4","JoystickButton5","LeftTrigger","RightTrigger","JoystickButton7","JoystickButton6"};
                bindings[index]="RightTrigger";
                TestAssert.True(ControllerKeyBindings.TriggerAvailable("LeftTrigger",bindings));
                TestAssert.False(ControllerKeyBindings.TriggerAvailable("RightTrigger",bindings));
                var broker=Broker();var raw=Pad(right:0.8f);raw.RightNavigationModifier=false;
                TestAssert.False(Has(broker.Update(TestFixtures.Context(),raw),NavigationCommand.ModifierOpened));
                TestAssert.Near(0.8f,broker.FilterAxis(NavigationAxis.RightTrigger,0.8f),0);
                broker.Update(TestFixtures.Context(),Pad());Open(broker);
                TestAssert.True(Has(broker.Update(TestFixtures.Context(),Pad(NavigationButton.R3,0.8f)),NavigationCommand.StartAutomatic));
            }
        }
    }
}
