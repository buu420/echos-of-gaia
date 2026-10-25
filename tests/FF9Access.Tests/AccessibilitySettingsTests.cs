using Memoria.Accessibility.Navigation;
using Memoria.Prime.Ini;
namespace FF9Access.Tests
{
    internal static class AccessibilitySettingsTests
    {
        public static void RegionAnnouncementsHonorNativeAndLegacySavedChoices()
        {
            foreach(string text in new[]{"0","False","false"," False "}) {
                var setting=IniValue.String("AnnounceRegionChanges");setting.ParseValue(text);
                TestAssert.False(NavigationControls.RegionAnnouncements(setting));
            }
            foreach(string text in new[]{"1","True","true"," True "}) {
                var setting=IniValue.String("AnnounceRegionChanges");setting.ParseValue(text);
                TestAssert.True(NavigationControls.RegionAnnouncements(setting));
            }
        }
        public static void UnconfiguredRegionAnnouncementsRemainEnabled()
        {
            TestAssert.True(NavigationControls.RegionAnnouncements(null));
            TestAssert.True(NavigationControls.RegionAnnouncements(""));
            TestAssert.True(NavigationControls.RegionAnnouncements("invalid"));
        }
    }
}
