namespace Memoria.Accessibility.AudioDescriptions
{
    public enum PromptInputDevice {Unknown,Keyboard,Controller}
    public sealed class PromptInputDeviceTracker
    {
        public PromptInputDevice Device {get;private set;}
        public void Observe(bool keyboardPressed,bool controllerPressed,bool foreground)
        {
            if(!foreground)return;
            if(keyboardPressed)Device=PromptInputDevice.Keyboard;
            else if(controllerPressed)Device=PromptInputDevice.Controller;
        }
    }
}
