using System;
using System.Collections.Generic;
using Memoria.Assets;
using Memoria.Data;
using Memoria.ScreenReader;

namespace Memoria.Accessibility
{
    /// <summary>
    /// Installs the speech-only character-name aliases on the screen reader manager. Every announcement
    /// (dialogue, menus, battle) passes through one filter fed by the native default-name table, the
    /// current language and the names the loaded save actually holds. Saves are never modified.
    /// </summary>
    internal static class CharacterNameSpeech
    {
        private static SpokenCharacterNames _names;
        private static Int32 _tableSize = -1;

        internal static void Install()
        {
            ScreenReaderManager.Instance.TextFilter = Filter;
        }

        internal static String Filter(String text)
        {
            if (String.IsNullOrEmpty(text))
                return text;
            return Names().Filter(text, Localization.CurrentSymbol, SavedNames());
        }

        // The native table is read on first use, after DataPatchers have applied CharacterDefaultName
        // entries, and rebuilt if a patch adds characters later.
        private static SpokenCharacterNames Names()
        {
            Dictionary<String, Dictionary<CharacterId, String>> table = CharacterNamesFormatter.DefaultNamesByLang;
            Int32 size = 0;
            foreach (Dictionary<CharacterId, String> language in table.Values)
                size += language.Count;
            if (_names == null || size != _tableSize)
            {
                var converted = new List<KeyValuePair<String, IDictionary<String, String>>>();
                foreach (KeyValuePair<String, Dictionary<CharacterId, String>> language in table)
                {
                    var names = new Dictionary<String, String>(StringComparer.Ordinal);
                    foreach (KeyValuePair<CharacterId, String> entry in language.Value)
                        names[entry.Key.ToString()] = entry.Value;
                    converted.Add(new KeyValuePair<String, IDictionary<String, String>>(language.Key, names));
                }
                _names = new SpokenCharacterNames(converted);
                _tableSize = size;
            }
            return _names;
        }

        // Names the loaded save holds, or null before any game state exists (title screen).
        private static ICollection<String> SavedNames()
        {
            FF9StateGlobal state = FF9StateSystem.Common?.FF9;
            if (state == null || state.player == null)
                return null;
            var names = new List<String>();
            foreach (PLAYER player in state.player.Values)
                if (player != null && !String.IsNullOrEmpty(player.Name))
                    names.Add(player.Name);
            return names;
        }
    }
}
