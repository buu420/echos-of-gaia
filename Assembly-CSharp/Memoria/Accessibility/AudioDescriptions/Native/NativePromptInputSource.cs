using System;
using UnityEngine;
using XInputDotNetPure;
namespace Memoria.Accessibility.AudioDescriptions
{
    public static class NativePromptInputSource
    {
        public static void Observe(SourceControl[] sources,PromptInputDeviceTracker tracker,HonoInputManager input,bool disabledPrimary)
        {
            if(tracker==null || input==null || !HonoInputManager.ApplicationIsActivated())return;
            try {
            bool keyboard=false,controller=false;
            if(sources!=null)foreach(SourceControl source in sources){keyboard|=source==SourceControl.KeyBoard;controller|=source==SourceControl.Joystick;}
            // Windows gameplay reads the cached UnityXInput snapshot, including
            // JoyShock pads. GetButtonDown/GetKeyDown are read-only queries.
            if(input.JoystickKeysPrimary!=null)foreach(string binding in input.JoystickKeysPrimary)
                if(!String.IsNullOrEmpty(binding))controller|=UnityXInput.Input.GetButtonDown(binding);
            if(!disabledPrimary && input.InputKeysPrimary!=null)foreach(KeyCode key in input.InputKeysPrimary)
                keyboard|=UnityXInput.Input.GetKeyDown(key);
            int secondary=disabledPrimary?3:2;
            for(int i=0;i<secondary;i++) {
                if(input.InputKeys2!=null && i<input.InputKeys2.Length)keyboard|=UnityXInput.Input.GetKeyDown(input.InputKeys2[i]);
                if(input.InputKeys3!=null && i<input.InputKeys3.Length)keyboard|=UnityXInput.Input.GetKeyDown(input.InputKeys3[i]);
            }
            foreach(KeyCode key in HonoInputManager.MemoriaKeyBindings)keyboard|=UnityXInput.Input.GetKeyDown(key);
            keyboard|=UnityXInput.Input.GetKeyDown(KeyCode.UpArrow) || UnityXInput.Input.GetKeyDown(KeyCode.DownArrow)
                || UnityXInput.Input.GetKeyDown(KeyCode.LeftArrow) || UnityXInput.Input.GetKeyDown(KeyCode.RightArrow);
            var manager=PersistenSingleton<UnityXInput.XInputManager>.Instance;
            var current=manager.CurrentState;var previous=manager.PreviousState;
            if(current.IsConnected) {
                controller|=AxisEdge(current.ThumbSticks.Left.X,previous.IsConnected?previous.ThumbSticks.Left.X:0)
                    || AxisEdge(current.ThumbSticks.Left.Y,previous.IsConnected?previous.ThumbSticks.Left.Y:0)
                    || Pressed(current.DPad.Up,previous.IsConnected && previous.DPad.Up==ButtonState.Pressed)
                    || Pressed(current.DPad.Down,previous.IsConnected && previous.DPad.Down==ButtonState.Pressed)
                    || Pressed(current.DPad.Left,previous.IsConnected && previous.DPad.Left==ButtonState.Pressed)
                    || Pressed(current.DPad.Right,previous.IsConnected && previous.DPad.Right==ButtonState.Pressed);
            }
            tracker.Observe(keyboard,controller,true);
            }catch(Exception){ /* A read-only accessibility observer must not break native input. */ }
        }
        private static bool Pressed(ButtonState current,bool wasPressed){return current==ButtonState.Pressed && !wasPressed;}
        private static int Direction(float value){float threshold=HonoInputManager.AnalogThreadhold;return value>threshold?1:value< -threshold?-1:0;}
        private static bool AxisEdge(float current,float previous){int direction=Direction(current);return direction!=0 && direction!=Direction(previous);}
    }
}
