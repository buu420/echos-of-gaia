using System;
using UnityEngine;
namespace Memoria.Accessibility.AudioDescriptions
{
    public static class NativePromptInputNames
    {
        public static string Read(Control control,bool checkFromConfig,string tag,bool concise)
        {
            if(control==Control.None)return String.Empty;
            switch(control){case Control.Up:return "Up";case Control.Down:return "Down";case Control.Left:return "Left";case Control.Right:return "Right";case Control.DPad:return "D-pad";}
            var input=PersistenSingleton<HonoInputManager>.Instance;if(input==null)return String.Empty;
            bool controller=tag==NGUIText.JoyStickButtonIcon || tag!=NGUIText.KeyboardButtonIcon &&
                (input.AccessibilityInputDevice==PromptInputDevice.Controller || input.AccessibilityInputDevice==PromptInputDevice.Unknown && UICamera.currentScheme==UICamera.ControlScheme.Controller);
            int index=(int)control;
            if(controller) {
                var keys=checkFromConfig?input.JoystickKeysPrimary:input.DefaultJoystickInputKeys;
                if(!checkFromConfig && EventInput.isJapaneseLayout && index<2)index=1-index;
                return keys!=null && index>=0 && index<keys.Length?PromptButtonNames.Controller(keys[index],Memoria.Configuration.Accessibility.ControllerNaming):"Unassigned controller button";
            }
            KeyCode key;
            if(control==Control.Pause || control==Control.Select)key=HonoInputManager.MemoriaKeyBindings[control==Control.Pause?4:5];
            else {
                if(!checkFromConfig)index=(int)input.PhysicalButtonToLogicalControl(index);
                if(input.InputKeysPrimary==null || index<0 || index>=input.InputKeysPrimary.Length)return "Unassigned key";
                key=input.InputKeysPrimary[index];
            }
            // Icon labels can be punctuation-only (for example ";"), which
            // many screen-reader voices do not speak. Name the bound key.
            return PromptButtonNames.Keyboard(key.ToString(),concise);
        }
    }
}
