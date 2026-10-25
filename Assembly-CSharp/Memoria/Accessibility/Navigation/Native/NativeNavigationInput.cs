using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Memoria.Prime;
using UnityEngine;
using XInputDotNetPure;
namespace Memoria.Accessibility.Navigation
{
    public static class NativeNavigationInput
    {
        private static NavigationBindings _bindings;
        private static NavigationInputBroker _broker;
        private static string _serialized, _nativeBindings;
        private static string _bindingDiagnostic,_conflictDiagnostic;
        private static int _frame = -1;
        private static bool _updating, _unavailable;
        private static bool _sceneSuspended;
        private static readonly List<NavigationCommand> _pending = new List<NavigationCommand>();
        private static long _pendingGeneration;
        public static string Diagnostic { get; private set; }
        public static PhysicalInputSnapshot Physical { get; private set; }
        public static bool MenuOpen { get { EnsureUpdated(); return _broker != null && _broker.MenuOpen; } }
        public static bool IsCapturingController { get { EnsureUpdated(); return _unavailable || (_broker != null && (_broker.CapturedButtons != 0 || _broker.CapturedAxes != 0)); } }
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        public static void EnsureUpdated()
        {
            if (_updating || _frame == Time.frameCount) return;
            _updating = true; _frame = Time.frameCount;
            try {
                int mode=FF9StateSystem.Instance.mode;
                BattlePerformanceTrace.ObserveFrame(mode);
                if(mode!=1 && mode!=3){
                    if(!_sceneSuspended){if(_broker!=null)_broker.Suspend();_pending.Clear();Physical=null;_sceneSuspended=true;NavigationLifecycleBridge.Release(mode==2?StopReason.Battle:StopReason.UnknownContext);}
                    _unavailable=false;return;
                }
                bool resumed=_sceneSuspended;
                string serialized = Configuration.Accessibility.NavigationBindings;
                var input = UnityEngine.Object.FindObjectOfType<HonoInputManager>();
                if (input == null) return;
                if (_bindings == null || serialized != _serialized) {
                    var parsed = NavigationBindings.ParseOrKeep(serialized, _bindings);
                    _bindings = parsed.Bindings;
                    _serialized = serialized; _nativeBindings = null;
                    if (_broker == null) { _broker = new NavigationInputBroker(_bindings); if(resumed)_broker.Suspend(); } else _broker.SetBindings(_bindings);
                    _bindingDiagnostic=parsed.Success?null:"Navigation bindings could not be loaded: " + parsed.Error;
                }
                var native = ReadGameBindings(input);
                _sceneSuspended=false;
                string fingerprint = string.Join(";", native.ConvertAll(binding => binding.Name + "=" + binding.Gesture).ToArray());
                if (fingerprint != _nativeBindings) {
                    _nativeBindings = fingerprint;
                    var conflicts = _bindings.FindConflicts(native);
                    _conflictDiagnostic=null;
                    if (conflicts.Count > 0) {
                        var messages = new List<string>(); foreach (var conflict in conflicts) messages.Add(conflict.ToString());
                        _conflictDiagnostic = "Navigation binding conflicts: " + string.Join("; ", messages.ToArray());
                        Log.Message("[FF9Access] {0}", _conflictDiagnostic);
                    }
                }
                _broker.MenuButton = Configuration.Accessibility.ControllerMenuButton == "L3" ? NavigationButton.L3 : NavigationButton.R3;
                Diagnostic=_bindingDiagnostic??_conflictDiagnostic;
                foreach(string key in input.JoystickKeysPrimary)if(_broker.MenuButton!=NavigationButton.None && ControllerKeyBindings.Button(key)==_broker.MenuButton){_broker.MenuButton=NavigationButton.None;Diagnostic="Controller navigation menu conflicts with a game control. Keyboard navigation remains available.";}
                Physical = ReadPhysical();
                NavigationContext context = NativeNavigationContext.Read();
                if (context.Generation != _pendingGeneration) { _pending.Clear(); _pendingGeneration = context.Generation; }
                NavigationCommand[] commands = _broker.Update(context, Physical);
                if (_pending.Count + commands.Length <= 64) _pending.AddRange(commands);
                if (!context.CanControl || !context.Foreground) NavigationLifecycleBridge.Release(!context.Foreground ? StopReason.FocusLost : StopReason.UnknownContext);
                foreach (NavigationCommand command in commands) {
                    if (command == NavigationCommand.ManualMovement) NavigationLifecycleBridge.Release(StopReason.ManualMovement);
                    else if (command == NavigationCommand.Confirm) NavigationLifecycleBridge.Release(StopReason.Confirm);
                    else if (command == NavigationCommand.MenuOpened) NavigationLifecycleBridge.Release(StopReason.Menu);
                    else if (command == NavigationCommand.Disconnected) NavigationLifecycleBridge.Release(StopReason.Disconnected);
                    else if (command == NavigationCommand.Stop || command == NavigationCommand.PreviousCategory || command == NavigationCommand.NextCategory || command == NavigationCommand.PreviousTarget || command == NavigationCommand.NextTarget) NavigationLifecycleBridge.Release(StopReason.TargetChanged);
                }
                _unavailable = false;
            } catch (Exception exception) {
                if (!_unavailable) { Diagnostic = "Navigation input unavailable. Automatic movement stopped."; Log.Message("[FF9Access] {0} {1}", Diagnostic, exception); }
                _unavailable = true; NavigationLifecycleBridge.Release(StopReason.UnknownContext);
            } finally { _updating = false; }
        }
        public static NavigationCommand[] TakeCommands() { EnsureUpdated(); var result = _pending.ToArray(); _pending.Clear(); return result; }
        // Use the game's existing connection sample while navigation polling is
        // suspended. A battle disconnect still revokes automatic resumption.
        public static void ObserveControllerConnection(bool connected)
        {
            if(_sceneSuspended && _broker!=null && _broker.ObserveControllerConnection(connected))NavigationLifecycleBridge.Release(StopReason.Disconnected);
        }
        public static PhysicalInputSnapshot ReadPhysical()
        {
            var input = UnityEngine.Object.FindObjectOfType<HonoInputManager>();
            var manager = UnityEngine.Object.FindObjectOfType<UnityXInput.XInputManager>();
            GamePadState pad = XInputDotNetPure.GamePad.GetState(manager == null ? PlayerIndex.One : manager.PlayerIndex);
            var snapshot = new PhysicalInputSnapshot { ControllerConnected = pad.IsConnected, MoveX = pad.ThumbSticks.Left.X, MoveY = pad.ThumbSticks.Left.Y, LookX = pad.ThumbSticks.Right.X, LookY = pad.ThumbSticks.Right.Y, LeftTrigger = pad.Triggers.Left, RightTrigger = pad.Triggers.Right };
            Add(ref snapshot.Buttons, pad.Buttons.A, NavigationButton.Confirm); Add(ref snapshot.Buttons, pad.Buttons.B, NavigationButton.Cancel);
            Add(ref snapshot.Buttons, pad.Buttons.X, NavigationButton.Automatic); Add(ref snapshot.Buttons, pad.Buttons.Y, NavigationButton.OtherFace);
            Add(ref snapshot.Buttons, pad.Buttons.LeftStick, NavigationButton.L3); Add(ref snapshot.Buttons, pad.Buttons.RightStick, NavigationButton.R3);
            Add(ref snapshot.Buttons, pad.Buttons.LeftShoulder, NavigationButton.L1); Add(ref snapshot.Buttons, pad.Buttons.RightShoulder, NavigationButton.R1);
            Add(ref snapshot.Buttons, pad.Buttons.Start, NavigationButton.Start); Add(ref snapshot.Buttons, pad.Buttons.Back, NavigationButton.Back);
            Add(ref snapshot.Buttons, pad.Buttons.Guide, NavigationButton.Guide);
            Add(ref snapshot.Buttons, pad.DPad.Up, NavigationButton.DpadUp); Add(ref snapshot.Buttons, pad.DPad.Down, NavigationButton.DpadDown);
            Add(ref snapshot.Buttons, pad.DPad.Left, NavigationButton.DpadLeft); Add(ref snapshot.Buttons, pad.DPad.Right, NavigationButton.DpadRight);
            snapshot.Shift = RawKey(KeyCode.LeftShift) || RawKey(KeyCode.RightShift);
            snapshot.Control = RawKey(KeyCode.LeftControl) || RawKey(KeyCode.RightControl);
            snapshot.Alt = RawKey(KeyCode.LeftAlt) || RawKey(KeyCode.RightAlt);
            var keys = new HashSet<KeyCode>();
            for (int key = (int)KeyCode.A; key <= (int)KeyCode.Z; key++) keys.Add((KeyCode)key);
            for (int key = (int)KeyCode.Alpha0; key <= (int)KeyCode.Alpha9; key++) keys.Add((KeyCode)key);
            foreach (var binding in _bindings.All) keys.Add(ParseKey(binding.Key));
            foreach (KeyCode key in HonoInputManager.MemoriaKeyBindings) keys.Add(key);
            foreach (KeyCode key in new[] {KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow}) keys.Add(key);
            if (input != null) {
                foreach (KeyCode key in input.InputKeysPrimary) keys.Add(key);
                foreach (KeyCode key in input.InputKeys2) keys.Add(key);
                foreach (KeyCode key in input.InputKeys3) keys.Add(key);
                snapshot.KeyboardConfirm = RawKey(input.InputKeysPrimary[0]) || RawKey(input.InputKeys2[0]) || RawKey(input.InputKeys3[0]);
                string confirm=input.JoystickKeysPrimary[(int)Control.Confirm];
                snapshot.GameConfirmButton=ControllerKeyBindings.Button(confirm);
                snapshot.GameConfirmExtra=ControllerKeyBindings.TriggerConfirm(confirm,snapshot.LeftTrigger,snapshot.RightTrigger);
                MarkMovementKey(snapshot,ControlKey(input.InputKeysPrimary,Control.Special));MarkMovementKey(snapshot,ControlKey(input.InputKeys2,Control.Special));MarkMovementKey(snapshot,ControlKey(input.InputKeys3,Control.Special));
            }
            foreach (KeyCode key in keys) if (key != KeyCode.None && RawKey(key)) snapshot.KeysHeld.Add(NavigationBindings.NormalizeKey(key.ToString()) ?? key.ToString());
            foreach(var key in new[]{KeyCode.UpArrow,KeyCode.DownArrow,KeyCode.LeftArrow,KeyCode.RightArrow})MarkMovementKey(snapshot,key);
            for (int i = 0; i < 4; i++) MarkMovementKey(snapshot,HonoInputManager.MemoriaKeyBindings[i]);
            return snapshot;
        }
        private static void MarkMovementKey(PhysicalInputSnapshot snapshot,KeyCode key){if(key!=KeyCode.None && RawKey(key)){snapshot.KeyboardMovement=true;snapshot.MovementKeysHeld.Add(NavigationBindings.NormalizeKey(key.ToString())??key.ToString());}}
        private static KeyCode ControlKey(KeyCode[] keys,Control control){return keys!=null && (int)control<keys.Length?keys[(int)control]:KeyCode.None;}
        public static GamePadState FilterPad(GamePadState physical)
        {
            EnsureUpdated();
            return _unavailable ? physical.WithCapture(0xffff, 0x7f) : _broker == null ? physical : physical.WithCapture(_broker.CapturedButtons, _broker.CapturedAxes);
        }
        public static bool IsKeyCaptured(KeyCode key) { EnsureUpdated(); return _unavailable || (_broker != null && _broker.IsKeyCaptured(NavigationBindings.NormalizeKey(key.ToString()) ?? key.ToString())); }
        public static float FilterAxis(string name, float value)
        {
            EnsureUpdated(); if (_unavailable) return 0;
            if (_broker == null) return value;
            NavigationAxis axis = name.Contains("Trigger") ? (name.Contains("Left") ? NavigationAxis.LeftTrigger : NavigationAxis.RightTrigger) : name.Contains("Right") ? (name.Contains("Horizontal") ? NavigationAxis.LookX : NavigationAxis.LookY) : name.Contains("Horizontal") ? NavigationAxis.MoveX : NavigationAxis.MoveY;
            return _broker.FilterAxis(axis, value);
        }
        public static bool IsControlCaptured(Control control, HonoInputManager input)
        {
            EnsureUpdated(); if (_unavailable) return true; if (_broker == null) return false;
            int index = (int)control;
            if (index >= 0 && index < input.InputKeysPrimary.Length && IsKeyCaptured(input.InputKeysPrimary[index])) return true;
            if (index < 2 && index >= 0 && (IsKeyCaptured(input.InputKeys2[index]) || IsKeyCaptured(input.InputKeys3[index]))) return true;
            if (index >= 0 && index < input.JoystickKeysPrimary.Length) {
                string name = input.JoystickKeysPrimary[index];
                if (name.Contains("Trigger")) return _broker.FilterAxis(name.Contains("Left") ? NavigationAxis.LeftTrigger : NavigationAxis.RightTrigger, 1) == 0;
                NavigationButton button = ButtonFromName(name); if (button != NavigationButton.None && !_broker.FilterButton(button, true)) return true;
            }
            if (control == Control.Up || control == Control.Down) return _broker.FilterAxis(NavigationAxis.MoveY, 1) == 0;
            if (control == Control.Left || control == Control.Right) return _broker.FilterAxis(NavigationAxis.MoveX, 1) == 0;
            return false;
        }
        private static List<GameBinding> ReadGameBindings(HonoInputManager input)
        {
            var bindings = new List<GameBinding>();
            for (int i = 0; i < input.InputKeysPrimary.Length; i++) if (input.InputKeysPrimary[i] != KeyCode.None) bindings.Add(new GameBinding(((Control)i).ToString(), input.InputKeysPrimary[i].ToString()));
            foreach(var keys in new[]{input.InputKeys2,input.InputKeys3})for(int i=0;i<keys.Length;i++)if(keys[i]!=KeyCode.None)bindings.Add(new GameBinding(((Control)i).ToString(),keys[i].ToString()));
            foreach (KeyCode key in HonoInputManager.MemoriaKeyBindings) bindings.Add(new GameBinding("Movement or game control", key.ToString()));
            foreach (string key in new[] {"UpArrow","DownArrow","LeftArrow","RightArrow","M","0","F1","F2","F3","F4","F5","F6","F7","F8","F9","F12","Escape","Return"}) bindings.Add(new GameBinding("Game or existing accessibility control", key));
            bindings.Add(new GameBinding("Memoria shortcut", "Ctrl+Shift+S", false));
            return bindings;
        }
        private static bool RawKey(KeyCode key)
        {
            if (key == KeyCode.None) return false;
            if (UnityEngine.Input.GetKey(key)) return true;
            int vk = key >= KeyCode.A && key <= KeyCode.Z ? (int)key - (int)KeyCode.A + 65 : key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9 ? (int)key - (int)KeyCode.Alpha0 + 48 : key == KeyCode.UpArrow ? 38 : key == KeyCode.DownArrow ? 40 : key == KeyCode.LeftArrow ? 37 : key == KeyCode.RightArrow ? 39 : key == KeyCode.LeftBracket ? 219 : key == KeyCode.RightBracket ? 221 : key == KeyCode.Semicolon ? 186 : 0;
            return vk != 0 && HonoInputManager.ApplicationIsActivated() && (GetAsyncKeyState(vk) & 0x8000) != 0;
        }
        private static KeyCode ParseKey(string key) { return (KeyCode)Enum.Parse(typeof(KeyCode), key.Length == 1 && char.IsDigit(key[0]) ? "Alpha" + key : key, true); }
        private static void Add(ref NavigationButton buttons, ButtonState state, NavigationButton button) { if (state == ButtonState.Pressed) buttons |= button; }
        private static NavigationButton ButtonFromName(string name)
        {
            return ControllerKeyBindings.Button(name);
        }
    }
}
