using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace Memoria.Accessibility.AudioDescriptions
{
    public static class PromptButtonNames
    {
        public static string FormatDialogButtons(string text,IList<string> buttons,bool timed)
        {
            // The eight original US play commands contain only "Press [button] !".
            // Leave instructions, other dialogue and other languages intact.
            if(timed && buttons.Count==1 && Regex.IsMatch(text??"",@"^\s*Press\s*!\s*$",RegexOptions.CultureInvariant))return buttons[0];
            return text+" "+String.Join(", ",new List<string>(buttons).ToArray());
        }
        private static readonly string[] Phonetics={"Alpha","Bravo","Charlie","Delta","Echo","Foxtrot","Golf","Hotel","India","Juliet","Kilo","Lima","Mike","November","Oscar","Papa","Quebec","Romeo","Sierra","Tango","Uniform","Victor","Whiskey","X-ray","Yankee","Zulu"};
        public static string Controller(string binding,string naming)
        {
            bool sony=String.Equals(naming,"PlayStation",StringComparison.OrdinalIgnoreCase),generic=String.Equals(naming,"Generic",StringComparison.OrdinalIgnoreCase);
            switch(binding) {
                case "JoystickButton0":return sony?"Cross":generic?"bottom face button":"A";
                case "JoystickButton1":return sony?"Circle":generic?"right face button":"B";
                case "JoystickButton2":return sony?"Square":generic?"left face button":"X";
                case "JoystickButton3":return sony?"Triangle":generic?"top face button":"Y";
                case "JoystickButton4":return sony?"L1":generic?"left bumper":"LB";
                case "JoystickButton5":return sony?"R1":generic?"right bumper":"RB";
                case "LeftTrigger":return sony?"L2":generic?"left trigger":"LT";
                case "RightTrigger":return sony?"R2":generic?"right trigger":"RT";
                case "JoystickButton6":return sony?"Select":"Back";
                case "JoystickButton7":return "Start";
                case "JoystickButton8":return "Left stick press";
                case "JoystickButton9":return "Right stick press";
                default:return "Unassigned controller button";
            }
        }
        public static string Keyboard(string key,bool concise)
        {
            if(String.IsNullOrEmpty(key) || key=="None")return "Unassigned key";
            if(concise && key.Length==1) {
                char letter=Char.ToUpperInvariant(key[0]);if(letter>='A' && letter<='Z')return Phonetics[letter-'A'];
            }
            if(key.StartsWith("Alpha",StringComparison.Ordinal) && key.Length==6 && Char.IsDigit(key[5]))return key.Substring(5);
            switch(key) {
                case "UpArrow":return "Up";case "DownArrow":return "Down";case "LeftArrow":return "Left";case "RightArrow":return "Right";
                case "Return":return "Enter";case "KeypadEnter":return "Keypad Enter";
            }
            return Regex.Replace(key,"([a-z])([A-Z])","$1 $2");
        }
    }
}
