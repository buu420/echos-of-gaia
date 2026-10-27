using System;
using Memoria.Accessibility.Navigation;
using XInputDotNetPure;
namespace FF9Access.Tests
{
    internal static class InputTests
    {
        public static void InspectionDoesNotCaptureNonInspectionGameKeys()
        {
            var broker=Broker();var context=TestFixtures.Context();context.CanControl=false;context.CanInspect=true;
            var raw=Pad(NavigationButton.Automatic|NavigationButton.DpadUp);raw.KeysHeld.Add("P");raw.KeysHeld.Add("L");raw.KeysHeld.Add("R");
            var commands=broker.Update(context,raw);
            TestAssert.True(Array.IndexOf(commands,NavigationCommand.Status)>=0);
            TestAssert.True(Array.IndexOf(commands,NavigationCommand.Repeat)>=0);
            TestAssert.False(broker.IsKeyCaptured("P"));
            TestAssert.True(broker.IsKeyCaptured("L"));TestAssert.True(broker.IsKeyCaptured("R"));
            TestAssert.True(broker.FilterButton(NavigationButton.Automatic,true));TestAssert.True(broker.FilterButton(NavigationButton.DpadUp,true));

        }
        public static void MinigameStickClickReadsStatusWithoutCapturingDigOrMovement()
        {
            var broker=Broker();var context=TestFixtures.Context();context.CanControl=false;context.CanInspect=true;context.MinigameInspection=true;
            var raw=Pad(NavigationButton.R3|NavigationButton.Automatic|NavigationButton.DpadUp);raw.MoveX=1;
            var commands=broker.Update(context,raw);
            TestAssert.True(Array.IndexOf(commands,NavigationCommand.Status)>=0);
            TestAssert.False(broker.FilterButton(NavigationButton.R3,true));
            TestAssert.True(broker.FilterButton(NavigationButton.Automatic,true));TestAssert.True(broker.FilterButton(NavigationButton.DpadUp,true));
            TestAssert.Near(1,broker.FilterAxis(NavigationAxis.MoveX,1),0);
            TestAssert.False(Array.IndexOf(broker.Update(context,raw),NavigationCommand.Status)>=0);
        }
        public static void MinigameEntryReleasesNavigationCaptureAndDoesNotReplayHeldClick()
        {
            var broker=Broker();Open(broker);
            var raw=BrowsePad(NavigationButton.R3|NavigationButton.Automatic|NavigationButton.DpadUp);raw.MoveX=1;raw.KeysHeld.Add("P");
            broker.Update(TestFixtures.Context(),raw);
            var context=TestFixtures.Context();context.CanControl=false;context.CanInspect=true;context.MinigameInspection=true;
            var commands=broker.Update(context,raw);
            TestAssert.False(Array.IndexOf(commands,NavigationCommand.Status)>=0);
            TestAssert.True(broker.FilterButton(NavigationButton.Automatic,true));TestAssert.True(broker.FilterButton(NavigationButton.DpadUp,true));
            TestAssert.Near(1,broker.FilterAxis(NavigationAxis.MoveX,1),0);TestAssert.False(broker.IsKeyCaptured("P"));
        }
        public static void MinigameStatusRespectsConfiguredClickAndForeground()
        {
            var broker=Broker();broker.InspectionButton=NavigationButton.L3;
            var context=TestFixtures.Context();context.CanControl=false;context.CanInspect=true;context.MinigameInspection=true;
            var commands=broker.Update(context,Pad(NavigationButton.L3));
            TestAssert.True(Array.IndexOf(commands,NavigationCommand.Status)>=0);
            broker.Update(context,Pad());context.Foreground=false;
            TestAssert.False(Array.IndexOf(broker.Update(context,Pad(NavigationButton.R3)),NavigationCommand.Status)>=0);
            context.Foreground=true;broker.InspectionButton=NavigationButton.None;broker.Update(context,Pad());
            TestAssert.False(Array.IndexOf(broker.Update(context,Pad(NavigationButton.R3|NavigationButton.L3)),NavigationCommand.Status)>=0);
        }
        public static void MinigameInspectionNeverStartsNavigation()
        {
            var broker=Broker();var context=TestFixtures.Context();context.CanInspect=true;context.MinigameInspection=true;
            var commands=broker.Update(context,Pad(NavigationButton.R3|NavigationButton.Automatic));
            TestAssert.True(Array.IndexOf(commands,NavigationCommand.Status)>=0);
            TestAssert.False(Array.IndexOf(commands,NavigationCommand.StartAutomatic)>=0);
            TestAssert.True(broker.FilterButton(NavigationButton.Automatic,true));
        }
        public static void NativeDisconnectObservationSurvivesBattlePollingSuspension()
        {
            var broker=Broker();
            var context=TestFixtures.Context();
            broker.Update(context,new PhysicalInputSnapshot{ControllerConnected=true});
            broker.Suspend();
            TestAssert.True(broker.ObserveControllerConnection(false));
            TestAssert.False(broker.ObserveControllerConnection(false));
            TestAssert.False(broker.ObserveControllerConnection(true));
            TestAssert.True(broker.ObserveControllerConnection(false));
        }
        private static NavigationInputBroker Broker() { return new NavigationInputBroker(NavigationBindings.Parse(null).Bindings); }
        private static PhysicalInputSnapshot Pad(NavigationButton buttons = NavigationButton.None) { return new PhysicalInputSnapshot { Buttons = buttons, ControllerConnected = true }; }
        private static PhysicalInputSnapshot BrowsePad(NavigationButton buttons=NavigationButton.None) {var raw=Pad(buttons);raw.LeftTrigger=0.8f;return raw;}
        private static void Open(NavigationInputBroker broker) { broker.Update(TestFixtures.Context(), Pad()); broker.Update(TestFixtures.Context(), BrowsePad()); }
        public static void BattleSuspensionPassesControlsAndSuppressesHeldNavigationOnReturn()
        {
            var broker=Broker();Open(broker);var held=Pad(NavigationButton.R3|NavigationButton.Confirm);held.MoveX=1;held.KeysHeld.Add("P");
            broker.Update(TestFixtures.Context(),held);broker.Suspend();
            TestAssert.False(broker.CaptureTail);TestAssert.True(broker.FilterButton(NavigationButton.Confirm,true));
            TestAssert.Near(1,broker.FilterAxis(NavigationAxis.MoveX,1),0);TestAssert.False(broker.IsKeyCaptured("P"));
            var commands=broker.Update(TestFixtures.Context("world:1",2),held);
            TestAssert.False(Array.IndexOf(commands,NavigationCommand.ModifierOpened)>=0);
            TestAssert.False(Array.IndexOf(commands,NavigationCommand.StartGuidance)>=0);
            TestAssert.False(Array.IndexOf(commands,NavigationCommand.ToggleAutomatic)>=0);
            TestAssert.True(Array.IndexOf(commands,NavigationCommand.ManualMovement)>=0);
            broker.Update(TestFixtures.Context("world:1",2),Pad());held=Pad();held.KeysHeld.Add("P");
            TestAssert.True(Array.IndexOf(broker.Update(TestFixtures.Context("world:1",2),held),NavigationCommand.ToggleAutomatic)>=0);
        }
        public static void NativeConfirmFollowsCustomControllerBinding()
        {
            var broker=Broker();var raw=Pad();raw.GameConfirmButton=NavigationButton.Cancel;
            broker.Update(TestFixtures.Context(),raw);raw.Buttons=NavigationButton.Cancel;
            var commands=broker.Update(TestFixtures.Context(),raw);
            TestAssert.True(Array.IndexOf(commands,NavigationCommand.Confirm)>=0);
            TestAssert.True(broker.FilterButton(NavigationButton.Cancel,true));
            raw.Buttons=NavigationButton.None;broker.Update(TestFixtures.Context(),raw);raw.Buttons=NavigationButton.Confirm;
            TestAssert.False(Array.IndexOf(broker.Update(TestFixtures.Context(),raw),NavigationCommand.Confirm)>=0);
        }
        public static void QualifiedNativeJoystickNamesResolveActualButtons()
        {
            TestAssert.Equal(NavigationButton.Cancel,ControllerKeyBindings.Button("Joystick1Button1"));
            TestAssert.Equal(NavigationButton.R3,ControllerKeyBindings.Button("Joystick2Button9"));
            TestAssert.Equal(NavigationButton.Confirm,ControllerKeyBindings.Button("JoystickButton0"));
            TestAssert.Equal(NavigationButton.None,ControllerKeyBindings.Button("OtherButton9"));
        }
        public static void HeldModifiedAutoClickDoesNotCancelItsOwnTravel()
        {
            var broker=Broker();Open(broker);broker.Update(TestFixtures.Context(),BrowsePad(NavigationButton.R3));
            var commands=broker.Update(TestFixtures.Context(),BrowsePad(NavigationButton.R3));
            TestAssert.False(Array.IndexOf(commands,NavigationCommand.ManualMovement)>=0);
            commands=broker.Update(TestFixtures.Context(),Pad(NavigationButton.R3|NavigationButton.DpadUp));
            TestAssert.True(Array.IndexOf(commands,NavigationCommand.ManualMovement)>=0);
        }
        public static void ShiftIntervalDoesNotStartMovement()
        {
            var broker = Broker(); var raw = Pad(); raw.KeysHeld.Add("P"); raw.Shift = true;
            var commands = broker.Update(TestFixtures.Context(), raw);
            TestAssert.Equal(1, commands.Length); TestAssert.Equal(NavigationCommand.IncreaseInterval, commands[0]);
            raw.Shift = false; TestAssert.Equal(0, broker.Update(TestFixtures.Context(), raw).Length);
        }
        public static void R3AutomaticTravelRequiresTheHeldTrigger()
        {
            var broker = Broker(); var raw = Pad(); raw.LookX = 1;
            TestAssert.False(Array.IndexOf(broker.Update(TestFixtures.Context(),raw),NavigationCommand.ModifierOpened)>=0);
            TestAssert.Equal(0,broker.Update(TestFixtures.Context(),Pad(NavigationButton.R3)).Length);
            broker.Update(TestFixtures.Context(),BrowsePad());
            TestAssert.True(Array.IndexOf(broker.Update(TestFixtures.Context(),BrowsePad(NavigationButton.R3)),NavigationCommand.StartAutomatic)>=0);
        }
        public static void ModifiedClickReleaseCannotConfirm()
        {
            var broker=Broker();Open(broker);var raw=BrowsePad(NavigationButton.L3);raw.GameConfirmButton=NavigationButton.L3;
            var commands=broker.Update(TestFixtures.Context(),raw);
            TestAssert.True(Array.IndexOf(commands,NavigationCommand.StartGuidance)>=0);
            TestAssert.False(Array.IndexOf(commands,NavigationCommand.Confirm)>=0);TestAssert.False(broker.FilterButton(NavigationButton.L3,true));
            raw=Pad(NavigationButton.L3);raw.GameConfirmButton=NavigationButton.L3;
            TestAssert.False(Array.IndexOf(broker.Update(TestFixtures.Context(),raw),NavigationCommand.Confirm)>=0);
            TestAssert.False(broker.FilterButton(NavigationButton.L3,true));
            broker.Update(TestFixtures.Context(),Pad());broker.Update(TestFixtures.Context(),Pad());
            TestAssert.True(broker.FilterButton(NavigationButton.L3,true));
        }
        public static void DirectFieldDpadIsCaptured()
        {
            var broker = Broker(); Open(broker); broker.Update(TestFixtures.Context(), BrowsePad(NavigationButton.DpadUp));
            TestAssert.False(broker.FilterButton(NavigationButton.DpadUp, true));
            var raw = new GamePadState.RawState(); raw.Gamepad.wButtons = 0x0001;
            var state = new GamePadState(true, raw, GamePadDeadZone.None);
            TestAssert.Equal(ButtonState.Released, state.WithCapture(broker.CapturedButtons, broker.CapturedAxes).DPad.Up);
            TestAssert.Equal(ButtonState.Pressed, state.DPad.Up);
        }
        public static void WorldThrottleIsCaptured()
        {
            var broker = Broker(); Open(broker);
            TestAssert.Near(0, broker.FilterAxis(NavigationAxis.Throttle, 1), 0);
            TestAssert.Near(0, broker.FilterAxis(NavigationAxis.MoveX, 1), 0);
        }
        public static void StickDriftDoesNotTakeOver()
        {
            var broker = Broker(); var raw = Pad(); raw.MoveX = 0.03f;
            TestAssert.False(Array.IndexOf(broker.Update(TestFixtures.Context(), raw), NavigationCommand.ManualMovement) >= 0);
            raw.MoveX = 0.7f; TestAssert.True(Array.IndexOf(broker.Update(TestFixtures.Context(), raw), NavigationCommand.ManualMovement) >= 0);
        }
        public static void FocusLossWithAlwaysCaptureRetainsReleaseMask()
        {
            var broker = Broker(); Open(broker); var raw = BrowsePad(NavigationButton.Confirm); raw.MoveX = 1;
            broker.Update(TestFixtures.Context(),raw);
            var context = TestFixtures.Context(); context.Foreground = false;
            broker.Update(context, raw);
            TestAssert.False(broker.FilterButton(NavigationButton.Confirm, true)); TestAssert.Near(0, broker.FilterAxis(NavigationAxis.MoveX, 1), 0);
            context.Foreground = true; broker.Update(context, raw); TestAssert.False(broker.FilterButton(NavigationButton.Confirm, true));
        }
        public static void HeldTriggerDoesNotCaptureUnboundKeyboardMovement()
        {
            var broker=Broker();Open(broker);var raw=BrowsePad();raw.KeysHeld.Add("W");raw.MovementKeysHeld.Add("W");raw.KeyboardMovement=true;
            TestAssert.True(Array.IndexOf(broker.Update(TestFixtures.Context(),raw),NavigationCommand.ManualMovement)>=0);
            TestAssert.False(broker.IsKeyCaptured("W"));
            raw.LeftTrigger=0;
            TestAssert.True(Array.IndexOf(broker.Update(TestFixtures.Context(),raw),NavigationCommand.ManualMovement)>=0);
            TestAssert.False(broker.IsKeyCaptured("W"));
        }
        public static void ButtonMaskPreservesUncapturedCameraAndPacket()
        {
            var raw = new GamePadState.RawState(); raw.dwPacketNumber = 12; raw.Gamepad.wButtons = 0x1200; raw.Gamepad.sThumbRX = 16384;
            var state = new GamePadState(true, raw, GamePadDeadZone.None);
            var filtered = state.WithCapture((uint)NavigationButton.Confirm, 0);
            TestAssert.Equal(ButtonState.Released, filtered.Buttons.A); TestAssert.Equal(ButtonState.Pressed, filtered.Buttons.RightShoulder);
            TestAssert.Equal(12u, filtered.PacketNumber); TestAssert.Near(state.ThumbSticks.Right.X, filtered.ThumbSticks.Right.X, 0);
        }
        public static void KeyboardConfirmStopsBeforePassingRealAction()
        {
            var broker = Broker(); var raw = Pad(); raw.KeyboardConfirm = true;
            TestAssert.True(Array.IndexOf(broker.Update(TestFixtures.Context(), raw), NavigationCommand.Confirm) >= 0);
            TestAssert.True(broker.FilterButton(NavigationButton.Confirm, true));
        }
        public static void PartialTriggerConfirmPausesBeforeNativeAction()
        {
            var broker=Broker();var raw=Pad();raw.GameConfirmButton=NavigationButton.None;raw.LeftTrigger=0.1f;
            raw.GameConfirmExtra=ControllerKeyBindings.TriggerConfirm("LeftTrigger",raw.LeftTrigger,0);
            TestAssert.True(Array.IndexOf(broker.Update(TestFixtures.Context(),raw),NavigationCommand.Confirm)>=0);
            TestAssert.True(ControllerKeyBindings.TriggerConfirm("RightTrigger",0,0.01f));
            TestAssert.False(ControllerKeyBindings.TriggerConfirm("RightTrigger",0.1f,0));
        }
        public static void SceneGenerationClosesBrowsingAndKeepsHeldButtonCapture()
        {
            var broker = Broker(); Open(broker);
            broker.Update(TestFixtures.Context(),BrowsePad(NavigationButton.Confirm));
            var next = TestFixtures.Context(generation: 2);
            var commands = broker.Update(next, BrowsePad(NavigationButton.Confirm));
            TestAssert.False(Array.IndexOf(commands, NavigationCommand.StartGuidance) >= 0);
             TestAssert.False(broker.FilterButton(NavigationButton.Confirm, true));
        }
    }
}
