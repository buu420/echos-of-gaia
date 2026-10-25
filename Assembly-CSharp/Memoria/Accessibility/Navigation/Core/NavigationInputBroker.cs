using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class NavigationInputBroker
    {
        private NavigationBindings _bindings;
        private NavigationButton _previousButtons;
        private long _generation;
        private bool _previousConnected, _captureAllKeys, _previousKeyboardConfirm,_previousGameConfirm;
        private bool _suspended, _previousMinigameInspection;
        private uint _latchedButtons, _latchedAxes;
        private HashSet<string> _previousKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _latchedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _capturedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool MenuOpen { get; private set; }
        public uint CapturedButtons { get; private set; }
        public uint CapturedAxes { get; private set; }
        public NavigationButton MenuButton = NavigationButton.R3;
        public bool CaptureTail { get { return CapturedButtons != 0 || CapturedAxes != 0 || _capturedKeys.Count != 0; } }
        public NavigationInputBroker(NavigationBindings bindings) { _bindings = bindings; }
        public void SetBindings(NavigationBindings bindings) { _bindings = bindings; }
        public bool ObserveControllerConnection(bool connected)
        {
            bool disconnected=_previousConnected && !connected;_previousConnected=connected;return disconnected;
        }
        public void Suspend()
        {
            _suspended=true;MenuOpen=false;_captureAllKeys=false;CapturedButtons=CapturedAxes=_latchedButtons=_latchedAxes=0;
            _latchedKeys.Clear();_capturedKeys.Clear();_previousKeys.Clear();_generation=0;
        }
        public NavigationCommand[] Update(NavigationContext context, PhysicalInputSnapshot physical)
        {
            var commands = new List<NavigationCommand>();
            if(_suspended){
                _suspended=false;_previousButtons=physical.Buttons;_previousConnected=physical.ControllerConnected;
                _previousKeyboardConfirm=physical.KeyboardConfirm;_previousGameConfirm=physical.GameConfirmHeld;
                _previousKeys=new HashSet<string>(physical.KeysHeld,StringComparer.OrdinalIgnoreCase);
            }
            bool permitted = context != null && context.CanControl && context.Foreground && !context.MinigameInspection;
            if (_generation != 0 && context != null && context.Generation != _generation) permitted = false;
            bool wasOpen = MenuOpen;
            bool minigameInspection=context!=null && context.MinigameInspection;
            if(minigameInspection && !_previousMinigameInspection) {
                // A previous navigation menu must not hold the minigame's dig or movement controls.
                wasOpen=false;_latchedButtons=_latchedAxes=0;_latchedKeys.Clear();_capturedKeys.Clear();
            }
            bool inspectionClick=minigameInspection && context.CanInspect && context.Foreground && physical.ControllerConnected;
            NavigationButton pressed = physical.Buttons & ~_previousButtons;
            if (_previousConnected && !physical.ControllerConnected) commands.Add(NavigationCommand.Disconnected);
            if (!permitted || (_previousConnected && !physical.ControllerConnected)) {
                if (MenuOpen) commands.Add(NavigationCommand.MenuClosed);
                MenuOpen = false;
                if(context!=null && context.CanInspect && context.Foreground)foreach(NavigationBinding binding in _bindings.All) {
                    if((binding.Command==NavigationCommand.Status || binding.Command==NavigationCommand.Repeat) && binding.IsEnabled && physical.KeysHeld.Contains(binding.Key) && !_previousKeys.Contains(binding.Key) && physical.Gesture(binding.Key)==binding.Gesture)commands.Add(binding.Command);
                }
                if(inspectionClick && (pressed & MenuButton)!=0)commands.Add(NavigationCommand.Status);
            } else {
                if ((pressed & MenuButton) != 0) {
                    MenuOpen = !MenuOpen;
                    commands.Add(MenuOpen ? NavigationCommand.MenuOpened : NavigationCommand.MenuClosed);
                }
                if (MenuOpen && wasOpen) {
                    AddButton(commands, pressed, NavigationButton.L1, NavigationCommand.PreviousCategory);
                    AddButton(commands, pressed, NavigationButton.R1, NavigationCommand.NextCategory);
                    AddButton(commands, pressed, NavigationButton.DpadUp, NavigationCommand.PreviousTarget);
                    AddButton(commands, pressed, NavigationButton.DpadDown, NavigationCommand.NextTarget);
                    AddButton(commands, pressed, NavigationButton.DpadLeft, NavigationCommand.Status);
                    AddButton(commands, pressed, NavigationButton.DpadRight, NavigationCommand.Repeat);
                    if ((pressed & NavigationButton.Cancel) != 0) { commands.Add(NavigationCommand.Stop); MenuOpen = false; }
                    else if ((pressed & NavigationButton.Automatic) != 0) { commands.Add(NavigationCommand.StartAutomatic); MenuOpen = false; }
                    else if ((pressed & NavigationButton.Confirm) != 0) { commands.Add(NavigationCommand.StartGuidance); MenuOpen = false; }
                }
                if (!MenuOpen && !wasOpen) {
                    foreach (NavigationBinding binding in _bindings.All) {
                        if (binding.IsEnabled && physical.KeysHeld.Contains(binding.Key) && !_previousKeys.Contains(binding.Key) && physical.Gesture(binding.Key) == binding.Gesture) commands.Add(binding.Command);
                    }
                    bool keyboardMovement=physical.KeyboardMovement && (physical.MovementKeysHeld.Count>0?HasUnlatched(physical.MovementKeysHeld):physical.KeysHeld.Count==0 || HasUnlatched(physical.KeysHeld));
                    bool movement = keyboardMovement || (physical.ActiveAxes & ~_latchedAxes)!=0 || ((uint)physical.Buttons & ~_latchedButtons & (uint)(NavigationButton.DpadUp | NavigationButton.DpadDown | NavigationButton.DpadLeft | NavigationButton.DpadRight | NavigationButton.L1 | NavigationButton.R1 | NavigationButton.Automatic)) != 0;
                    if (movement) commands.Add(NavigationCommand.ManualMovement);
                    if ((physical.GameConfirmHeld && !_previousGameConfirm) || (physical.KeyboardConfirm && !_previousKeyboardConfirm)) commands.Add(NavigationCommand.Confirm);
                }
            }
            _captureAllKeys = MenuOpen || wasOpen;
            _capturedKeys = new HashSet<string>(_latchedKeys, StringComparer.OrdinalIgnoreCase);
            if (_captureAllKeys) {
                CapturedButtons = (uint)NavigationButton.All; CapturedAxes = (uint)NavigationAxis.All;
                _latchedButtons = (uint)physical.Buttons; _latchedAxes = physical.ActiveAxes;
                _latchedKeys = new HashSet<string>(physical.KeysHeld, StringComparer.OrdinalIgnoreCase);
            } else {
                CapturedButtons = _latchedButtons; CapturedAxes = _latchedAxes;
                _latchedButtons &= (uint)physical.Buttons; _latchedAxes &= physical.ActiveAxes;
                _latchedKeys.IntersectWith(physical.KeysHeld);
                if(inspectionClick && (physical.Buttons & MenuButton)!=0) {
                    CapturedButtons|=(uint)MenuButton;_latchedButtons|=(uint)MenuButton;
                }
                if (permitted || context!=null && context.CanInspect && context.Foreground) foreach (NavigationBinding binding in _bindings.All) {
                    bool inspectionCommand=binding.Command==NavigationCommand.Status || binding.Command==NavigationCommand.Repeat;
                    if ((permitted || inspectionCommand) && binding.IsEnabled && physical.KeysHeld.Contains(binding.Key) && physical.Gesture(binding.Key) == binding.Gesture) { _capturedKeys.Add(binding.Key); _latchedKeys.Add(binding.Key); }
                }
            }
            _previousButtons = physical.Buttons; _previousConnected = physical.ControllerConnected;
            _previousKeyboardConfirm = physical.KeyboardConfirm;
            _previousGameConfirm=physical.GameConfirmHeld;
            _previousKeys = new HashSet<string>(physical.KeysHeld, StringComparer.OrdinalIgnoreCase);
            _previousMinigameInspection=minigameInspection;
            if (context != null) _generation = context.Generation;
            return commands.ToArray();
        }
        public bool FilterButton(NavigationButton button, bool pressed) { return (CapturedButtons & (uint)button) == 0 && pressed; }
        public float FilterAxis(NavigationAxis axis, float value) { return (CapturedAxes & (uint)axis) != 0 ? 0 : value; }
        public bool IsKeyCaptured(string key) { return _captureAllKeys || _capturedKeys.Contains(NavigationBindings.NormalizeKey(key) ?? key); }
        private bool HasUnlatched(IEnumerable<string> keys){foreach(string key in keys)if(!_latchedKeys.Contains(key))return true;return false;}
        private static void AddButton(List<NavigationCommand> commands, NavigationButton pressed, NavigationButton button, NavigationCommand command) { if ((pressed & button) != 0) commands.Add(command); }
    }
}
