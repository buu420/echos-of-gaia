using System;
namespace Memoria.Accessibility.Navigation
{
    // The same physical commands as Blind Soldier, using Memoria's existing
    // controller sample. Nothing here injects movement or activates Confirm.
    public sealed class HeldTriggerNavigation
    {
        private NavigationButton _previousButtons;
        private bool _previousModifier, _previousConnected, _observed, _active, _waitForRelease;
        public bool Capturing { get; private set; }
        public void Reset()
        {
            _previousButtons=NavigationButton.None;
            _previousModifier=_previousConnected=_observed=_active=_waitForRelease=Capturing=false;
        }
        public NavigationCommand? Update(bool permitted,PhysicalInputSnapshot physical,bool resumed)
        {
            NavigationCommand? result=null;
            bool left=physical.ControllerConnected && physical.LeftNavigationModifier && physical.LeftTrigger>0.25f;
            bool right=physical.ControllerConnected && physical.RightNavigationModifier && physical.RightTrigger>0.25f;
            bool modifier=left || right;
            bool reconnect=_observed && !_previousConnected && physical.ControllerConnected;
            NavigationButton pressed=physical.Buttons & ~_previousButtons;
            Capturing=permitted && modifier;
            if(!permitted || resumed || reconnect) {
                _active=false;_waitForRelease=modifier;
            } else if(!modifier) {
                _active=false;_waitForRelease=false;
            } else if(!_previousModifier && !_waitForRelease) {
                _active=true;result=NavigationCommand.ModifierOpened;
            } else if(_active && !(left && right)) {
                NavigationCommand? command=
                    Has(pressed,NavigationButton.Cancel)?NavigationCommand.Stop:
                    Has(pressed,NavigationButton.L3)?NavigationCommand.StartGuidance:
                    Has(pressed,NavigationButton.R3)?NavigationCommand.StartAutomatic:
                    Has(pressed,NavigationButton.DpadLeft)?NavigationCommand.PreviousCategory:
                    Has(pressed,NavigationButton.DpadRight)?NavigationCommand.NextCategory:
                    Has(pressed,NavigationButton.DpadUp)?NavigationCommand.PreviousTarget:
                    Has(pressed,NavigationButton.DpadDown)?NavigationCommand.NextTarget:(NavigationCommand?)null;
                if(command.HasValue) {
                    result=command.Value;
                    if(command.Value==NavigationCommand.Stop || command.Value==NavigationCommand.StartGuidance || command.Value==NavigationCommand.StartAutomatic) {
                        _active=false;_waitForRelease=true;
                    }
                }
            }
            _previousButtons=physical.Buttons;_previousModifier=modifier;
            _previousConnected=physical.ControllerConnected;_observed=true;
            return result;
        }
        private static bool Has(NavigationButton buttons,NavigationButton value){return (buttons & value)!=0;}
    }
}
