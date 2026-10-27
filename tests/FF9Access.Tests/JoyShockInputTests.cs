using XInputDotNetPure;

namespace FF9Access.Tests
{
    internal static class JoyShockInputTests
    {
        public static void LeftStickClickUsesJoyShockBitSix()
        {
            GamePadState state = Merge(new JSL.State { buttons = 0x40 });
            TestAssert.Equal(ButtonState.Pressed, state.Buttons.LeftStick);
            TestAssert.Equal(ButtonState.Released, state.Buttons.RightStick);
        }

        public static void RightStickClickUsesJoyShockBitSeven()
        {
            GamePadState state = Merge(new JSL.State { buttons = 0x80 });
            TestAssert.Equal(ButtonState.Released, state.Buttons.LeftStick);
            TestAssert.Equal(ButtonState.Pressed, state.Buttons.RightStick);
        }

        public static void TriggerButtonsDoNotSynthesizeStickClicks()
        {
            foreach (int buttons in new[] { 0x400, 0x800, 0xC00 })
            {
                GamePadState state = Merge(new JSL.State { buttons = buttons });
                TestAssert.Equal(ButtonState.Released, state.Buttons.LeftStick);
                TestAssert.Equal(ButtonState.Released, state.Buttons.RightStick);
            }
        }

        public static void TriggerPressureAndRealClickStayIndependent()
        {
            GamePadState state = Merge(new JSL.State { buttons = 0xC80, lTrigger = 0.25f, rTrigger = 0.75f });
            TestAssert.Equal(ButtonState.Released, state.Buttons.LeftStick);
            TestAssert.Equal(ButtonState.Pressed, state.Buttons.RightStick);
            TestAssert.Near(63f / 255f, state.Triggers.Left, 0.000001f);
            TestAssert.Near(191f / 255f, state.Triggers.Right, 0.000001f);
        }

        public static void AnalogPressureDoesNotRequireDigitalTriggerBits()
        {
            GamePadState state = Merge(new JSL.State { lTrigger = 0.5f, rTrigger = 1f });
            TestAssert.Near(127f / 255f, state.Triggers.Left, 0.000001f);
            TestAssert.Near(1f, state.Triggers.Right, 0f);
            TestAssert.Equal(ButtonState.Released, state.Buttons.LeftStick);
            TestAssert.Equal(ButtonState.Released, state.Buttons.RightStick);
        }

        public static void ExistingJoyShockButtonsKeepTheirXInputMapping()
        {
            // Literal native masks come from the JoyShockLibrary header; expected
            // masks are the XInput contract, independent of the mapping code.
            int[] input = { 0x1, 0x2, 0x4, 0x8, 0x10, 0x20, 0x100, 0x200, 0x1000, 0x2000, 0x4000, 0x8000, 0x10000, 0x20000 };
            ushort[] expected = { 0x1, 0x2, 0x4, 0x8, 0x10, 0x20, 0x100, 0x200, 0x1000, 0x2000, 0x4000, 0x8000, 0x400, 0x20 };
            for (int i = 0; i < input.Length; i++)
            {
                var raw = new GamePadState.RawState();
                GamePad.MergeJoyShockState(ref raw, new JSL.State { buttons = input[i] });
                TestAssert.Equal(expected[i], raw.Gamepad.wButtons);
            }
        }

        public static void MergePreservesXInputPacketAndAlreadyHeldButtons()
        {
            var raw = new GamePadState.RawState { dwPacketNumber = 12345 };
            raw.Gamepad.wButtons = 0xA1C5;
            GamePad.MergeJoyShockState(ref raw, new JSL.State { buttons = 0x10 });
            TestAssert.Equal(12345u, raw.dwPacketNumber);
            TestAssert.Equal((ushort)0xA1D5, raw.Gamepad.wButtons);
        }

        public static void MergeKeepsAdditiveAxesAndTriggerPressure()
        {
            var raw = new GamePadState.RawState();
            raw.Gamepad.sThumbLX = 1000;
            raw.Gamepad.sThumbLY = 2000;
            raw.Gamepad.sThumbRX = -1000;
            raw.Gamepad.sThumbRY = -2000;
            raw.Gamepad.bLeftTrigger = 10;
            raw.Gamepad.bRightTrigger = 20;
            GamePad.MergeJoyShockState(ref raw, new JSL.State
            {
                stickLX = 0.25f, stickLY = 0.5f, stickRX = -0.25f, stickRY = -0.5f,
                lTrigger = 0.25f, rTrigger = 0.5f
            });
            TestAssert.Equal((short)9191, raw.Gamepad.sThumbLX);
            TestAssert.Equal((short)18383, raw.Gamepad.sThumbLY);
            TestAssert.Equal((short)-9191, raw.Gamepad.sThumbRX);
            TestAssert.Equal((short)-18383, raw.Gamepad.sThumbRY);
            TestAssert.Equal((byte)73, raw.Gamepad.bLeftTrigger);
            TestAssert.Equal((byte)147, raw.Gamepad.bRightTrigger);
        }

        public static void MergeKeepsSaturatedAxesAndTriggersWithinNativeRanges()
        {
            var raw = new GamePadState.RawState();
            raw.Gamepad.sThumbLX = 30000;
            raw.Gamepad.sThumbLY = -30000;
            raw.Gamepad.sThumbRX = -30000;
            raw.Gamepad.sThumbRY = 30000;
            raw.Gamepad.bLeftTrigger = 200;
            raw.Gamepad.bRightTrigger = 200;
            GamePad.MergeJoyShockState(ref raw, new JSL.State
            {
                stickLX = 1f, stickLY = -1f, stickRX = -1f, stickRY = 1f,
                lTrigger = 1f, rTrigger = 1f
            });
            TestAssert.Equal(short.MaxValue, raw.Gamepad.sThumbLX);
            TestAssert.Equal(short.MinValue, raw.Gamepad.sThumbLY);
            TestAssert.Equal(short.MinValue, raw.Gamepad.sThumbRX);
            TestAssert.Equal(short.MaxValue, raw.Gamepad.sThumbRY);
            TestAssert.Equal(byte.MaxValue, raw.Gamepad.bLeftTrigger);
            TestAssert.Equal(byte.MaxValue, raw.Gamepad.bRightTrigger);
        }

        private static GamePadState Merge(JSL.State joyShock)
        {
            var raw = new GamePadState.RawState();
            GamePad.MergeJoyShockState(ref raw, joyShock);
            return new GamePadState(true, raw, GamePadDeadZone.None);
        }
    }
}
