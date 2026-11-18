using System;
using System.Collections.Generic;

namespace Memoria.Accessibility
{
    internal static class TitleLanguageSpeech
    {
        private const string MenuPrefix = "Language";
        private const string CurrentSuffix = ", current language";

        // Mirrors LanguageName: { language name, symbol, spoken name }.
        private static readonly string[][] Languages =
        {
            new[] { "English(US)", "US", "English (United States)" },
            new[] { "English(UK)", "UK", "English (United Kingdom)" },
            new[] { "Japanese", "JP", "Japanese" },
            new[] { "German", "GR", "German" },
            new[] { "French", "FR", "French" },
            new[] { "Italian", "IT", "Italian" },
            new[] { "Spanish", "ES", "Spanish" }
        };

        /// <summary>Spoken name for a game language name ("English(US)") or symbol ("US"); null when unknown.</summary>
        internal static string SpokenName(string languageOrSymbol)
        {
            string[] language = Find(languageOrSymbol);
            return language == null ? null : language[2];
        }

        /// <summary>A focused flag: its language, marked when it is the language currently in use.</summary>
        internal static string OptionLabel(string language, string currentSymbol)
        {
            string[] option = Find(language);
            if (option == null)
                return null;
            return option == Find(currentSymbol) ? option[2] + CurrentSuffix : option[2];
        }

        /// <summary>
        /// The title menu's language button. Its label shows one short name per line (two in dual-language
        /// mode); each known line is spoken by language name, other text is read as displayed, and an empty
        /// label falls back to the current language.
        /// </summary>
        internal static string MenuButtonLabel(string displayedLabel, string currentSymbol)
        {
            var parts = new List<string>();
            if (displayedLabel != null)
            {
                foreach (string line in displayedLabel.Split('\n'))
                {
                    string shown = line.Trim();
                    if (shown.Length == 0)
                        continue;
                    parts.Add(SpokenName(shown) ?? shown);
                }
            }
            if (parts.Count == 0)
            {
                string current = SpokenName(currentSymbol);
                if (current != null)
                    parts.Add(current);
            }
            return parts.Count == 0 ? MenuPrefix : MenuPrefix + ", " + String.Join(", ", parts.ToArray());
        }

        private static string[] Find(string languageOrSymbol)
        {
            if (languageOrSymbol == null)
                return null;
            string key = languageOrSymbol.Trim();
            if (key.Length == 0)
                return null;
            foreach (string[] language in Languages)
                if (String.Equals(key, language[0], StringComparison.OrdinalIgnoreCase) || String.Equals(key, language[1], StringComparison.OrdinalIgnoreCase))
                    return language;
            return null;
        }
    }
}
