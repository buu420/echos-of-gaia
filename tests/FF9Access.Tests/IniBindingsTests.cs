using System;
using System.IO;
using Memoria.Prime.Ini;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    public static class IniBindingsTests
    {
        public sealed class Settings : Ini
        {
            public Section Accessibility;
            public Settings() { BindingSection(out Accessibility, value => Accessibility = value); }
        }
        public sealed class Section : IniSection
        {
            public readonly IniValue<string> Bindings;
            public readonly IniValue<int> Interval;
            public Section() : base("Accessibility", true) {
                Bindings=BindString("NavigationBindings", "");
                Interval=BindInt32("NavigationIntervalMilliseconds", 1000);
            }
        }
        public static void EverySavedAssignmentSurvivesTheActualGameIniReader()
        {
            var changed=NavigationBindings.Parse("ToggleAutomatic=T;Repeat=Q").Bindings;
            string file=Path.Combine(Path.GetTempPath(), "ff9-ini-"+Guid.NewGuid().ToString("N")+".ini");
            try {
                File.WriteAllText(file,"[Accessibility]\nNavigationBindings = "+changed.Serialize()+"\nNavigationIntervalMilliseconds = 2500 ; ordinary comment\n");
                var settings=new Settings();new IniReader(file).Read(settings);
                var parsed=NavigationBindings.Parse(settings.Accessibility.Bindings.Value);
                TestAssert.True(parsed.Success);
                TestAssert.Equal("T",parsed.Bindings.Get(NavigationCommand.ToggleAutomatic).Gesture);
                TestAssert.Equal("Q",parsed.Bindings.Get(NavigationCommand.Repeat).Gesture);
                TestAssert.Equal(2500,settings.Accessibility.Interval.Value);
            } finally {File.Delete(file);}
        }
    }
}
