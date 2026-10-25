using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.Accessibility.Navigation;
using Memoria.ScreenReader;

namespace Memoria.Launcher
{
    public sealed class AccessibilitySettingsPanel : StackPanel
    {
        public readonly Dictionary<NavigationCommand, TextBox> BindingEditors = new Dictionary<NavigationCommand, TextBox>();
        public readonly CheckBox RegionChanges;
        public readonly CheckBox Descriptions;
        public readonly TextBox DescriptionVolume = new TextBox { MinWidth = 150, MaxLength = 3 };
        public readonly CheckBox ExitSounds;
        public readonly TextBox ExitSoundVolume = new TextBox { MinWidth = 150, MaxLength = 3 };
        public readonly CheckBox NpcSounds,InteractableSounds;
        public readonly TextBox NpcSoundVolume = new TextBox { MinWidth = 150, MaxLength = 3 },InteractableSoundVolume = new TextBox { MinWidth = 150, MaxLength = 3 };
        public readonly ComboBox Interval = new ComboBox(), ControllerNaming = new ComboBox(), MenuTrigger = new ComboBox();
        public readonly AccessibleStatus Status = new AccessibleStatus();
        private static readonly string[] Labels = { "Previous category", "Next category", "Previous target", "Next target", "Navigation status", "Toggle spoken guidance", "Toggle automatic walking", "Repeat speech", "Decrease guidance interval", "Increase guidance interval" };

        public AccessibilitySettingsPanel()
        {
            Margin = new Thickness(20, 10, 20, 10);
            KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Continue);
            for (int index = 0; index <= (int)NavigationCommand.IncreaseInterval; index++)
            {
                var editor = new TextBox { MinWidth = 150, MaxLength = 40 };
                BindingEditors.Add((NavigationCommand)index, editor);
                AddRow(Text("Binding" + index, Labels[index]), editor, ((NavigationCommand)index).ToString());
            }
            for (int value = 500; value <= 10000; value += 500)
                Interval.Items.Add(new ComboBoxItem { Content = (value / 1000.0).ToString("0.0", CultureInfo.CurrentCulture) + " seconds", Tag = value });
            AddRow(Text("Interval", "Guidance interval"), Interval, "GuidanceInterval");
            foreach (string value in new[] { "Xbox", "PlayStation", "Generic" }) ControllerNaming.Items.Add(value);
            AddRow(Text("ControllerNaming", "Controller button names"), ControllerNaming, "ControllerNaming");
            foreach (string value in new[] { "R3", "L3" }) MenuTrigger.Items.Add(value);
            AddRow(Text("MenuTrigger", "Controller navigation menu"), MenuTrigger, "ControllerMenuButton");
            RegionChanges = new CheckBox { Content = Text("Regions", "Announce region changes"), Margin = new Thickness(0, 8, 0, 8) };
            AutomationProperties.SetName(RegionChanges, RegionChanges.Content.ToString());
            AutomationProperties.SetAutomationId(RegionChanges, "AnnounceRegionChanges");
            Children.Add(RegionChanges);
            Descriptions = new CheckBox { Content = "Audio descriptions", Margin = new Thickness(0, 8, 0, 8) };
            AutomationProperties.SetName(Descriptions, "Audio descriptions");
            AutomationProperties.SetAutomationId(Descriptions, "AudioDescriptions");
            Children.Add(Descriptions);
            AddRow("Description volume (0 to 200 percent)", DescriptionVolume, "AudioDescriptionVolume");
            ExitSounds = new CheckBox { Content = "Exit proximity sounds", Margin = new Thickness(0, 8, 0, 8) };
            AutomationProperties.SetName(ExitSounds, "Exit proximity sounds");
            AutomationProperties.SetAutomationId(ExitSounds, "FieldExitSounds");
            Children.Add(ExitSounds);
            AddRow("Exit sound volume (0 to 200 percent)", ExitSoundVolume, "FieldExitSoundVolume");
            NpcSounds = AddSoundToggle("NPC proximity sounds", "FieldNpcSounds");
            AddRow("NPC sound volume (0 to 200 percent)", NpcSoundVolume, "FieldNpcSoundVolume");
            InteractableSounds = AddSoundToggle("Interactable proximity sounds", "FieldInteractableSounds");
            AddRow("Interactable sound volume (0 to 200 percent)", InteractableSoundVolume, "FieldInteractableSoundVolume");
            var save = new Button { Content = Text("Save", "Save accessibility settings"), Margin = new Thickness(0, 3, 0, 3) };
            save.Click += delegate { string result; TrySave(IniFile.MemoriaIni, out result); Status.Report(result, true); };
            Children.Add(save);
            var test = new Button { Content = Text("TestSpeech", "Test speech"), Margin = new Thickness(0, 3, 0, 3) };
            test.Click += delegate {
                try {
                    var speech = ScreenReaderManager.Instance;
                    speech.Initialize();
                    speech.Speak("Final Fantasy IX accessibility speech test.", true);
                    Status.Report(speech.BackendDiagnostic ?? "Speech backend unavailable.", true);
                } catch (Exception error) { Status.Report("Speech test failed: " + error.Message, true); }
            };
            Children.Add(test);
            Children.Add(Status);
            Load(IniFile.MemoriaIni);
            Status.Report(AccessibilityBuildPolicy.UpdateStatus, false);
        }

        private static string Text(string key, string fallback)
        {
            object value = Lang.Res["Accessibility." + key];
            return value as string ?? fallback;
        }

        private CheckBox AddSoundToggle(string name,string id)
        {
            var toggle=new CheckBox{Content=name,Margin=new Thickness(0,8,0,8)};
            AutomationProperties.SetName(toggle,name);AutomationProperties.SetAutomationId(toggle,id);Children.Add(toggle);return toggle;
        }

        private void AddRow(string name, Control control, string id)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            var label = new Label { Content = name, Target = control };
            AutomationProperties.SetName(control, name);
            AutomationProperties.SetAutomationId(control, id);
            AutomationProperties.SetLabeledBy(control, label);
            Grid.SetColumn(control, 1);
            row.Children.Add(label); row.Children.Add(control); Children.Add(row);
        }

        public void Load(IniFile ini)
        {
            var parsed = NavigationBindings.Parse(ini.GetSetting("Accessibility", "NavigationBindings"));
            var bindings = parsed.Success ? parsed.Bindings : NavigationBindings.Parse(null).Bindings;
            foreach (var editor in BindingEditors) editor.Value.Text = bindings.Get(editor.Key).Gesture;
            int milliseconds, legacy;
            Int32.TryParse(ini.GetSetting("Accessibility", "NavigationIntervalMilliseconds"), out milliseconds);
            Int32.TryParse(ini.GetSetting("Accessibility", "NavigationUpdateInterval", "1"), out legacy);
            Interval.SelectedIndex = NavigationControls.IntervalMilliseconds(milliseconds, legacy) / 500 - 1;
            RegionChanges.IsChecked = NavigationControls.RegionAnnouncements(ini.GetSetting("Accessibility", "AnnounceRegionChanges", "1"));
            Descriptions.IsChecked = NavigationControls.RegionAnnouncements(ini.GetSetting("Accessibility", "AudioDescriptions", "1"));
            DescriptionVolume.Text = ini.GetSetting("Accessibility", "AudioDescriptionVolume", "100");
            ExitSounds.IsChecked = NavigationControls.RegionAnnouncements(ini.GetSetting("Accessibility", "FieldExitSounds", "1"));
            ExitSoundVolume.Text = ini.GetSetting("Accessibility", "FieldExitSoundVolume", "100");
            NpcSounds.IsChecked = NavigationControls.RegionAnnouncements(ini.GetSetting("Accessibility", "FieldNpcSounds", "1"));
            NpcSoundVolume.Text = ini.GetSetting("Accessibility", "FieldNpcSoundVolume", "100");
            InteractableSounds.IsChecked = NavigationControls.RegionAnnouncements(ini.GetSetting("Accessibility", "FieldInteractableSounds", "1"));
            InteractableSoundVolume.Text = ini.GetSetting("Accessibility", "FieldInteractableSoundVolume", "100");
            ControllerNaming.SelectedItem = ini.GetSetting("Accessibility", "ControllerNaming", "Xbox");
            if (ControllerNaming.SelectedIndex < 0) ControllerNaming.SelectedIndex = 0;
            MenuTrigger.SelectedItem = ini.GetSetting("Accessibility", "ControllerMenuButton", "R3");
            if (MenuTrigger.SelectedIndex < 0) MenuTrigger.SelectedIndex = 0;
            if (!parsed.Success) Status.Report(parsed.Error + ". The saved bindings were preserved; defaults are displayed for editing.", true);
        }

        public bool TrySave(IniFile ini, out string validationMessage)
        {
            string serialized = String.Join("|", BindingEditors.Select(pair => pair.Key + "=" + pair.Value.Text).ToArray());
            var parsed = NavigationBindings.Parse(serialized);
            if (!parsed.Success) { validationMessage = parsed.Error; return false; }
            var conflicts = parsed.Bindings.FindConflicts(DefaultGameBindings());
            if (conflicts.Count > 0) { validationMessage = conflicts[0].ToString() + ". Choose a different key."; return false; }
            if (Interval.SelectedIndex < 0 || ControllerNaming.SelectedIndex < 0 || MenuTrigger.SelectedIndex < 0)
            { validationMessage = "Choose a guidance interval and controller options."; return false; }
            int descriptionVolume;
            if (!Int32.TryParse(DescriptionVolume.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out descriptionVolume) || descriptionVolume < 0 || descriptionVolume > 200)
            { validationMessage = "Enter a description volume from 0 to 200 percent."; return false; }
            int exitVolume;
            if (!Int32.TryParse(ExitSoundVolume.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out exitVolume) || exitVolume < 0 || exitVolume > 200)
            { validationMessage = "Enter an exit sound volume from 0 to 200 percent."; return false; }
            int npcVolume,interactableVolume;
            if (!Int32.TryParse(NpcSoundVolume.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out npcVolume) || npcVolume < 0 || npcVolume > 200)
            { validationMessage = "Enter an NPC sound volume from 0 to 200 percent."; return false; }
            if (!Int32.TryParse(InteractableSoundVolume.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out interactableVolume) || interactableVolume < 0 || interactableVolume > 200)
            { validationMessage = "Enter an interactable sound volume from 0 to 200 percent."; return false; }
            try {
                var values = new Dictionary<string, string> {
                    { "NavigationBindings", parsed.Bindings.Serialize() },
                    { "NavigationIntervalMilliseconds", ((int)((ComboBoxItem)Interval.SelectedItem).Tag).ToString(CultureInfo.InvariantCulture) },
                    { "AnnounceRegionChanges", RegionChanges.IsChecked == true ? "1" : "0" },
                    { "AudioDescriptions", Descriptions.IsChecked == true ? "1" : "0" },
                    { "AudioDescriptionVolume", descriptionVolume.ToString(CultureInfo.InvariantCulture) },
                    { "FieldExitSounds", ExitSounds.IsChecked == true ? "1" : "0" },
                    { "FieldExitSoundVolume", exitVolume.ToString(CultureInfo.InvariantCulture) },
                    { "FieldNpcSounds", NpcSounds.IsChecked == true ? "1" : "0" },
                    { "FieldNpcSoundVolume", npcVolume.ToString(CultureInfo.InvariantCulture) },
                    { "FieldInteractableSounds", InteractableSounds.IsChecked == true ? "1" : "0" },
                    { "FieldInteractableSoundVolume", interactableVolume.ToString(CultureInfo.InvariantCulture) },
                    { "ControllerNaming", (string)ControllerNaming.SelectedItem },
                    { "ControllerMenuButton", (string)MenuTrigger.SelectedItem }
                };
                ini.SaveSection("Accessibility", values);
                validationMessage = "Accessibility settings saved. Restart the game to apply changed bindings. Custom game controls are checked again in the game.";
                return true;
            } catch (Exception error) { validationMessage = "Settings could not be saved: " + error.Message; return false; }
        }

        private static IList<GameBinding> DefaultGameBindings()
        {
            var bindings = new List<GameBinding>();
            foreach (string key in new[] { "X", "C", "V", "B", "G", "H", "F", "J", "Return", "Escape", "Space", "UpArrow", "DownArrow", "LeftArrow", "RightArrow", "M", "0" })
                bindings.Add(new GameBinding("game control", key));
            for (int index = 1; index <= 12; index++) bindings.Add(new GameBinding("game shortcut", "F" + index));
            bindings.Add(new GameBinding("mod manager shortcut", "Ctrl+Shift+S", false));
            return bindings;
        }
    }
}
