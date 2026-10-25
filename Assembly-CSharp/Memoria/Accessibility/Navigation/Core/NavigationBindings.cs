using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class NavigationBinding
    {
        public NavigationCommand Command;
        public string Gesture, Key;
        public bool IsEnabled = true;
    }
    public sealed class GameBinding
    {
        public readonly string Name, Gesture, Key;
        public readonly bool AnyModifiers;
        public GameBinding(string name, string gesture) : this(name, gesture, true) { }
        public GameBinding(string name, string gesture, bool anyModifiers) { Name = name; Gesture = NavigationBindings.NormalizeGesture(gesture); Key = NavigationBindings.BaseKey(Gesture); AnyModifiers = anyModifiers; }
    }
    public sealed class BindingConflict
    {
        public NavigationCommand Command;
        public string Gesture, GameAction;
        public override string ToString() { return Gesture + " is assigned to " + GameAction; }
    }
    public sealed class BindingParseResult
    {
        public bool Success;
        public string Error;
        public NavigationBindings Bindings;
    }
    public sealed class NavigationBindings
    {
        // Memoria's native INI reader treats semicolons as inline comments.
        // Pipes preserve all assignments; legacy semicolons remain accepted by the editor/parser.
        public const string LegacyDefaultSerialization = "PreviousCategory=U|NextCategory=O|PreviousTarget=LeftBracket|NextTarget=RightBracket|Status=K|ToggleGuidance=I|ToggleAutomatic=P|Repeat=R|DecreaseInterval=Shift+I|IncreaseInterval=Shift+P";
        public const string DefaultSerialization = "PreviousCategory=U|NextCategory=O|PreviousTarget=K|NextTarget=Semicolon|Status=L|ToggleGuidance=I|ToggleAutomatic=P|Repeat=R|DecreaseInterval=Shift+I|IncreaseInterval=Shift+P";
        private readonly Dictionary<NavigationCommand, NavigationBinding> _bindings = new Dictionary<NavigationCommand, NavigationBinding>();
        public ICollection<NavigationBinding> All { get { return _bindings.Values; } }
        public NavigationBinding Get(NavigationCommand command) { NavigationBinding result; return _bindings.TryGetValue(command, out result) ? result : null; }
        public static BindingParseResult Parse(string serialized)
        {
            // Upgrade only the exact previously shipped profile; preserve custom bindings.
            if (serialized == LegacyDefaultSerialization) serialized = DefaultSerialization;
            var bindings = new NavigationBindings();
            Apply(bindings, DefaultSerialization);
            string error = string.IsNullOrEmpty(serialized) ? null : Apply(bindings, serialized);
            var gestures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (NavigationBinding binding in bindings.All) if (!gestures.Add(binding.Gesture)) error = "Two navigation actions use " + binding.Gesture;
            return new BindingParseResult { Bindings = bindings, Success = error == null, Error = error };
        }
        private static string Apply(NavigationBindings bindings, string serialized)
        {
            // A literal semicolon is a key when it is the value, and a separator otherwise.
            serialized = serialized.Replace("=;|", "=Semicolon|").Replace("=;;", "=Semicolon;");
            if (serialized.EndsWith("=;", StringComparison.Ordinal)) serialized = serialized.Substring(0, serialized.Length - 2) + "=Semicolon";
            var commands = new HashSet<NavigationCommand>();
            foreach (string assignment in serialized.Split(new char[] { ';', '|' })) {
                if (assignment.Trim().Length == 0) continue;
                string[] parts = assignment.Split('=');
                if (parts.Length != 2) return "Use Action=Key for navigation bindings";
                NavigationCommand command;
                try { command = (NavigationCommand)Enum.Parse(typeof(NavigationCommand), parts[0].Trim(), true); }
                catch { return "Unknown navigation action: " + parts[0]; }
                if (command < NavigationCommand.PreviousCategory || command > NavigationCommand.IncreaseInterval || !commands.Add(command)) return "Invalid or duplicate navigation action: " + parts[0];
                string gesture = NormalizeGesture(parts[1]);
                if (gesture == null) return "Unknown navigation key: " + parts[1];
                bindings._bindings[command] = new NavigationBinding { Command = command, Gesture = gesture, Key = BaseKey(gesture) };
            }
            return null;
        }
        public static BindingParseResult ParseOrKeep(string serialized, NavigationBindings previous)
        {
            BindingParseResult result = Parse(serialized);
            if (!result.Success) result.Bindings = previous ?? Parse(null).Bindings;
            return result;
        }
        public IList<BindingConflict> FindConflicts(IList<GameBinding> nativeBindings)
        {
            var conflicts = new List<BindingConflict>();
            foreach (NavigationBinding binding in All) {
                binding.IsEnabled = true;
                foreach (GameBinding native in nativeBindings) {
                    if (native.Gesture != null && (native.AnyModifiers ? native.Key == binding.Key : native.Gesture == binding.Gesture)) {
                        binding.IsEnabled = false;
                        conflicts.Add(new BindingConflict { Command = binding.Command, Gesture = binding.Gesture, GameAction = native.Name });
                    }
                }
            }
            return conflicts;
        }
        public string Serialize()
        {
            var list = new List<string>();
            for (int i = 0; i <= (int)NavigationCommand.IncreaseInterval; i++) { NavigationBinding binding = Get((NavigationCommand)i); list.Add(binding.Command + "=" + binding.Gesture); }
            return string.Join("|", list.ToArray());
        }
        public static string BaseKey(string gesture) { if (gesture == null) return null; int split = gesture.LastIndexOf('+'); return gesture.Substring(split + 1); }
        public static string NormalizeGesture(string value)
        {
            if (value == null) return null;
            string[] parts = value.Trim().Split('+');
            bool shift = false, control = false, alt = false;
            for (int i = 0; i < parts.Length - 1; i++) {
                string modifier = parts[i].Trim().ToUpperInvariant();
                if (modifier == "SHIFT" && !shift) shift = true;
                else if ((modifier == "CTRL" || modifier == "CONTROL") && !control) control = true;
                else if (modifier == "ALT" && !alt) alt = true;
                else return null;
            }
            string key = NormalizeKey(parts[parts.Length - 1]);
            return key == null ? null : (control ? "Ctrl+" : "") + (alt ? "Alt+" : "") + (shift ? "Shift+" : "") + key;
        }
        public static string NormalizeKey(string value)
        {
            string key = (value ?? "").Trim().ToUpperInvariant();
            if (key.Length == 1 && key[0] >= 'A' && key[0] <= 'Z') return key;
            if (key.Length == 1 && key[0] >= '0' && key[0] <= '9') return key;
            if (key.StartsWith("ALPHA") && key.Length == 6 && char.IsDigit(key[5])) return key.Substring(5);
            if (key == "[" || key == "LEFTBRACKET") return "LeftBracket";
            if (key == "]" || key == "RIGHTBRACKET") return "RightBracket";
            if (key == ";" || key == "SEMICOLON") return "Semicolon";
            if (key == "LEFTARROW" || key == "RIGHTARROW" || key == "UPARROW" || key == "DOWNARROW") return char.ToUpperInvariant(key[0]) + key.Substring(1).ToLowerInvariant().Replace("arrow", "Arrow");
            if (key == "SPACE" || key == "TAB" || key == "ESCAPE" || key == "RETURN" || key == "BACKSPACE") return key.Substring(0, 1) + key.Substring(1).ToLowerInvariant();
            int function;
            if (key.StartsWith("F") && int.TryParse(key.Substring(1), out function) && function >= 1 && function <= 12) return "F" + function;
            return null;
        }
    }
}
