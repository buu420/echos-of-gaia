using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Memoria.Accessibility;
namespace FF9Access.Tests
{
    internal static class TitleLanguageSpeechTests
    {
        // LanguageGroupPanel child order in TitleUI.Awake: the seven flag-only buttons of the native picker.
        private static readonly string[] FlagSymbols = { "JP", "US", "FR", "ES", "GR", "IT", "UK" };

        private static string RepoFile(string relative)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assembly-CSharp"))) dir = dir.Parent;
            if (dir == null) throw new Exception("Repository root not found");
            return Path.Combine(dir.FullName, relative);
        }

        public static void EveryFlagOnlyOptionHasADistinctSpokenName()
        {
            var seen = new HashSet<string>();
            foreach (string symbol in FlagSymbols)
            {
                string spoken = TitleLanguageSpeech.SpokenName(symbol);
                TestAssert.True(!String.IsNullOrEmpty(spoken));
                TestAssert.True(seen.Add(spoken));
            }
            TestAssert.Equal("Japanese", TitleLanguageSpeech.SpokenName("JP"));
            TestAssert.Equal("English (United States)", TitleLanguageSpeech.SpokenName("US"));
            TestAssert.Equal("English (United Kingdom)", TitleLanguageSpeech.SpokenName("UK"));
            TestAssert.Equal("French", TitleLanguageSpeech.SpokenName("FR"));
            TestAssert.Equal("Spanish", TitleLanguageSpeech.SpokenName("ES"));
            TestAssert.Equal("German", TitleLanguageSpeech.SpokenName("GR"));
            TestAssert.Equal("Italian", TitleLanguageSpeech.SpokenName("IT"));
        }

        public static void SpokenTableMirrorsEveryGameLanguageConstant()
        {
            // TitleUI maps flags through LanguageName constants ("English(US)") while the menu button shows
            // the NameShort symbols ("US"); both forms must reach the same spoken name for every game language.
            string source = File.ReadAllText(RepoFile("Assembly-CSharp/Global/Language/LanguageName.cs"));
            var constants = new Dictionary<string, string>();
            foreach (Match match in Regex.Matches(source, @"public const String (\w+) = ""([^""]+)"";"))
                constants[match.Groups[1].Value] = match.Groups[2].Value;
            int languages = 0;
            foreach (KeyValuePair<string, string> constant in constants)
            {
                if (constant.Key.StartsWith("Symbol", StringComparison.Ordinal)) continue;
                string symbol = constants["Symbol" + constant.Key];
                string spoken = TitleLanguageSpeech.SpokenName(constant.Value);
                TestAssert.True(!String.IsNullOrEmpty(spoken));
                TestAssert.Equal(spoken, TitleLanguageSpeech.SpokenName(symbol));
                languages++;
            }
            TestAssert.Equal(7, languages);
        }

        public static void FocusedFlagMarksOnlyTheCurrentLanguage()
        {
            TestAssert.Equal("Japanese, current language", TitleLanguageSpeech.OptionLabel("Japanese", "JP"));
            TestAssert.Equal("English (United States)", TitleLanguageSpeech.OptionLabel("English(US)", "JP"));
            TestAssert.Equal("English (United Kingdom)", TitleLanguageSpeech.OptionLabel("English(UK)", "US"));
            TestAssert.Equal("English (United States), current language", TitleLanguageSpeech.OptionLabel("English(US)", "US"));
            TestAssert.Equal(null, TitleLanguageSpeech.OptionLabel("", "US"));
            TestAssert.Equal(null, TitleLanguageSpeech.OptionLabel(null, "US"));
        }

        public static void JapaneseMenuButtonReadsTheLanguageNotTheTwoLetterCode()
        {
            TestAssert.Equal("Language, Japanese", TitleLanguageSpeech.MenuButtonLabel("JP", "JP"));
            TestAssert.Equal("Language, English (United States)", TitleLanguageSpeech.MenuButtonLabel("US", "US"));
            // Dual-language mode shows the primary and secondary short names on two lines.
            TestAssert.Equal("Language, Japanese, English (United States)", TitleLanguageSpeech.MenuButtonLabel("JP\nUS", "JP"));
            TestAssert.Equal("Language, Japanese, English (United States)", TitleLanguageSpeech.MenuButtonLabel("JP\r\nUS", "JP"));
        }

        public static void MenuButtonFallsBackToTheCurrentLanguageUntilItsLabelIsFilled()
        {
            TestAssert.Equal("Language, French", TitleLanguageSpeech.MenuButtonLabel("", "FR"));
            TestAssert.Equal("Language, French", TitleLanguageSpeech.MenuButtonLabel(null, "FR"));
            TestAssert.Equal("Language", TitleLanguageSpeech.MenuButtonLabel(null, null));
        }

        public static void ModdedShortNamesAreReadAsDisplayed()
        {
            TestAssert.Equal("Language, 日本語", TitleLanguageSpeech.MenuButtonLabel("日本語", "JP"));
            TestAssert.Equal("Language, Deutsch, English (United States)", TitleLanguageSpeech.MenuButtonLabel("Deutsch\nUS", "GR"));
        }

        public static void UnknownLanguagesAreNotInvented()
        {
            TestAssert.Equal(null, TitleLanguageSpeech.SpokenName("XX"));
            TestAssert.Equal(null, TitleLanguageSpeech.SpokenName(""));
            TestAssert.Equal(null, TitleLanguageSpeech.SpokenName(null));
            TestAssert.Equal("Japanese", TitleLanguageSpeech.SpokenName(" jp "));
        }

    }
}
