using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public enum NavigationCommand { PreviousCategory, NextCategory, PreviousTarget, NextTarget, Status, ToggleGuidance, ToggleAutomatic, Repeat, DecreaseInterval, IncreaseInterval, MenuOpened, MenuClosed, StartGuidance, StartAutomatic, Stop, ManualMovement, Confirm, Disconnected }
    [Flags]
    public enum NavigationButton : uint { None = 0, DpadUp = 1, DpadDown = 2, DpadLeft = 4, DpadRight = 8, Start = 16, Back = 32, L3 = 64, R3 = 128, L1 = 256, R1 = 512, Guide = 1024, Confirm = 4096, Cancel = 8192, Automatic = 16384, OtherFace = 32768, All = 0xffff }
    [Flags]
    public enum NavigationAxis : uint { MoveX = 1, MoveY = 2, LookX = 4, LookY = 8, LeftTrigger = 16, RightTrigger = 32, Throttle = 64, All = 127 }
    public sealed class PhysicalInputSnapshot
    {
        public NavigationButton Buttons;
        public NavigationButton GameConfirmButton=NavigationButton.Confirm;
        public bool GameConfirmExtra;
        public bool GameConfirmHeld {get{return GameConfirmExtra || (Buttons&GameConfirmButton)!=0;}}
        public readonly HashSet<string> KeysHeld = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> MovementKeysHeld = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool Shift, Control, Alt, ControllerConnected, KeyboardMovement, KeyboardConfirm;
        public float MoveX, MoveY, LookX, LookY, LeftTrigger, RightTrigger;
        public bool ConfirmPressed { get { return (Buttons & NavigationButton.Confirm) != 0; } }
        public string Gesture(string key) { return (Control ? "Ctrl+" : "") + (Alt ? "Alt+" : "") + (Shift ? "Shift+" : "") + NavigationBindings.NormalizeKey(key); }
        public uint ActiveAxes {
            get {
                uint axes = 0;
                if (Math.Abs(MoveX) > NavigationControls.StickDeadZone) axes |= (uint)NavigationAxis.MoveX;
                if (Math.Abs(MoveY) > NavigationControls.StickDeadZone) axes |= (uint)NavigationAxis.MoveY;
                if (Math.Abs(LookX) > NavigationControls.StickDeadZone) axes |= (uint)NavigationAxis.LookX;
                if (Math.Abs(LookY) > NavigationControls.StickDeadZone) axes |= (uint)NavigationAxis.LookY | (uint)NavigationAxis.Throttle;
                if (LeftTrigger > NavigationControls.TriggerDeadZone) axes |= (uint)NavigationAxis.LeftTrigger;
                if (RightTrigger > NavigationControls.TriggerDeadZone) axes |= (uint)NavigationAxis.RightTrigger;
                return axes;
            }
        }
    }
    public static class NavigationControls
    {
        // XInput has already applied its native hardware dead zone. A small secondary
        // threshold avoids treating residual legacy/Steam Input drift as manual intent.
        public const float StickDeadZone = 0.15f, TriggerDeadZone = 0.2f;
        public static bool RegionAnnouncements(string value)
        {
            value = value == null ? null : value.Trim();
            if(value == "0")return false;
            if(value == "1")return true;
            bool parsed;return !Boolean.TryParse(value,out parsed) || parsed;
        }
        public static int IntervalMilliseconds(int explicitMilliseconds, int legacySeconds)
        {
            long value = explicitMilliseconds > 0 ? explicitMilliseconds : Math.Max(1L, legacySeconds) * 1000L;
            value = Math.Min(10000L, Math.Max(500L, value));
            return (int)((value + 250L) / 500L * 500L);
        }
    }
}
