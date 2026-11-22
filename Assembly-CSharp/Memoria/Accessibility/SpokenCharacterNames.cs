using System;
using System.Collections.Generic;
using System.Text;

namespace Memoria.Accessibility
{
    /// <summary>
    /// Speech-only aliases for character names. A saved name that is exactly another language's
    /// native default (for example the Japanese default ジタン while playing in English) is spoken
    /// as the current language's default for the same character. Nothing is renamed: the save and
    /// the screen keep the stored name. Names in the current language's own script are never
    /// aliased, because a Latin foreign default cannot be told apart from a custom name and is
    /// pronounceable anyway.
    /// </summary>
    internal sealed class SpokenCharacterNames
    {
        private sealed class Owner { internal string Character, Language; }

        // Foreign default name (exact) -> owning character key and the language it came from.
        private readonly Dictionary<string, List<Owner>> _foreignDefaults = new Dictionary<string, List<Owner>>(StringComparer.Ordinal);
        // Language symbol -> character key -> default name.
        private readonly Dictionary<string, IDictionary<string, string>> _defaults = new Dictionary<string, IDictionary<string, string>>(StringComparer.Ordinal);

        /// <param name="defaultsByLanguage">Native default names: language symbol, then character key, then name.</param>
        public SpokenCharacterNames(IEnumerable<KeyValuePair<string, IDictionary<string, string>>> defaultsByLanguage)
        {
            if (defaultsByLanguage == null) return;
            foreach (KeyValuePair<string, IDictionary<string, string>> language in defaultsByLanguage)
            {
                if (String.IsNullOrEmpty(language.Key) || language.Value == null) continue;
                _defaults[language.Key] = language.Value;
                foreach (KeyValuePair<string, string> entry in language.Value)
                {
                    if (String.IsNullOrEmpty(entry.Key) || String.IsNullOrEmpty(entry.Value) || !HasForeignScript(entry.Value)) continue;
                    List<Owner> owners;
                    if (!_foreignDefaults.TryGetValue(entry.Value, out owners)) _foreignDefaults[entry.Value] = owners = new List<Owner>();
                    owners.Add(new Owner { Character = entry.Key, Language = language.Key });
                }
            }
        }

        /// <summary>The current language's default to speak instead of <paramref name="name"/>, or null to speak it as displayed.</summary>
        public string Alias(string name, string currentSymbol)
        {
            if (String.IsNullOrEmpty(name) || String.IsNullOrEmpty(currentSymbol)) return null;
            IDictionary<string, string> current; List<Owner> owners;
            if (!_defaults.TryGetValue(currentSymbol, out current) || !_foreignDefaults.TryGetValue(name, out owners)) return null;
            foreach (Owner owner in owners)
            {
                string spoken;
                if (owner.Language == currentSymbol || !current.TryGetValue(owner.Character, out spoken)) continue;
                if (String.IsNullOrEmpty(spoken) || spoken == name || HasForeignScript(spoken)) continue;
                return spoken;
            }
            return null;
        }

        /// <summary>
        /// Rewrites whole-word occurrences of aliased names inside an announcement. When the saved party
        /// names are known, only names the save actually holds are aliased; null means unknown.
        /// </summary>
        public string Filter(string text, string currentSymbol, ICollection<string> savedNames)
        {
            if (String.IsNullOrEmpty(text) || String.IsNullOrEmpty(currentSymbol) || _foreignDefaults.Count == 0) return text;
            StringBuilder result = null; int copied = 0;
            for (int i = 0; i < text.Length;)
            {
                if (!Char.IsLetter(text[i])) { i++; continue; }
                int end = i + 1; while (end < text.Length && IsWordChar(text[end])) end++;
                if (i > 0 && Char.IsDigit(text[i - 1])) { i = end; continue; }
                string word = text.Substring(i, end - i);
                string spoken = savedNames != null && !savedNames.Contains(word) ? null : Alias(word, currentSymbol);
                if (spoken != null)
                {
                    if (result == null) result = new StringBuilder(text.Length + 16);
                    result.Append(text, copied, i - copied).Append(spoken); copied = end;
                }
                i = end;
            }
            if (result == null) return text;
            return result.Append(text, copied, text.Length - copied).ToString();
        }

        private static bool IsWordChar(char c) { return Char.IsLetterOrDigit(c); }

        /// <summary>True when the name contains a letter outside the Latin ranges the Western game languages use.</summary>
        internal static bool HasForeignScript(string name)
        {
            if (String.IsNullOrEmpty(name)) return false;
            foreach (char c in name) if (c > 'ɏ' && Char.IsLetter(c)) return true;
            return false;
        }
    }
}
