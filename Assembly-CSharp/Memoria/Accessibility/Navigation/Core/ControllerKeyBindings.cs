using System;
namespace Memoria.Accessibility.Navigation
{
    public static class ControllerKeyBindings
    {
        public static bool TriggerConfirm(string name,float left,float right) { return name=="LeftTrigger"?left!=0:name=="RightTrigger"?right!=0:false; }
        public static NavigationButton Button(string name)
        {
            if(name==null)return NavigationButton.None;
            int separator=name.LastIndexOf("Button",StringComparison.OrdinalIgnoreCase);
            if(separator<8 || !name.StartsWith("Joystick",StringComparison.OrdinalIgnoreCase))return NavigationButton.None;
            for(int i=8;i<separator;i++)if(name[i]<'0' || name[i]>'9')return NavigationButton.None;
            name="JoystickButton"+name.Substring(separator+6);
            switch(name) {
                case "JoystickButton0":return NavigationButton.Confirm;case "JoystickButton1":return NavigationButton.Cancel;
                case "JoystickButton2":return NavigationButton.Automatic;case "JoystickButton3":return NavigationButton.OtherFace;
                case "JoystickButton4":return NavigationButton.L1;case "JoystickButton5":return NavigationButton.R1;
                case "JoystickButton6":return NavigationButton.Back;case "JoystickButton7":return NavigationButton.Start;
                case "JoystickButton8":return NavigationButton.L3;case "JoystickButton9":return NavigationButton.R3;
                default:return NavigationButton.None;
            }
        }
    }
}
