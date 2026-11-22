using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Memoria.Accessibility;
namespace FF9Access.Tests
{
    internal static class SpokenCharacterNamesTests
    {
        private static string RepoFile(string relative)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assembly-CSharp"))) dir = dir.Parent;
            if (dir == null) throw new Exception("Repository root not found");
            return Path.Combine(dir.FullName, relative);
        }

        // The native default-name table, read from the game source so these tests never hold a guessed copy.
        private static Dictionary<string, IDictionary<string, string>> NativeDefaults()
        {
            var table = new Dictionary<string, IDictionary<string, string>>(StringComparer.Ordinal);
            string symbol = null;
            foreach (string line in File.ReadAllLines(RepoFile("Assembly-CSharp/Memoria/Assets/Text/CharacterNamesFormatter.cs")))
            {
                if (line.Contains("private static") && line.Contains("_scriptNames")) break;
                Match language = Regex.Match(line, "^\\s*\\{\\s*\"([A-Z]{2})\",\\s*new Dictionary<CharacterId, String>");
                if (language.Success) { symbol = language.Groups[1].Value; table[symbol] = new Dictionary<string, string>(StringComparer.Ordinal); continue; }
                Match entry = Regex.Match(line, "^\\s*\\{\\s*CharacterId\\.(\\w+),\\s*\"([^\"]+)\"\\s*\\}");
                if (entry.Success && symbol != null) table[symbol][entry.Groups[1].Value] = entry.Groups[2].Value;
            }
            if (table.Count < 7 || !table.ContainsKey("JP") || table["JP"]["Zidane"] != "ジタン") throw new Exception("Native default-name table was not read");
            return table;
        }

        private static SpokenCharacterNames Names() { return new SpokenCharacterNames(NativeDefaults()); }

        public static void JapaneseDefaultZidaneIsSpokenAsTheEnglishDefault()
        {
            TestAssert.Equal("Zidane", Names().Alias("ジタン", "US"));
        }

        public static void EachJapaneseDefaultFollowsItsOwnCharacterInTheCurrentLanguage()
        {
            var names = Names();
            TestAssert.Equal("Dagger", names.Alias("ダガー", "US"));
            TestAssert.Equal("Steiner", names.Alias("スタイナー", "FR"));
            TestAssert.Equal("Mahagon", names.Alias("サラマンダー", "GR"));
            TestAssert.Equal("Bibi", names.Alias("ビビ", "FR"));
        }

        public static void JapanesePlayKeepsEveryNameAsDisplayed()
        {
            var names = Names();
            TestAssert.Equal<string>(null, names.Alias("ジタン", "JP"));
            TestAssert.Equal<string>(null, names.Alias("Zidane", "JP"));
        }

        public static void CurrentLanguageDefaultsAreNotAliased()
        {
            var names = Names();
            TestAssert.Equal<string>(null, names.Alias("Zidane", "US"));
            TestAssert.Equal<string>(null, names.Alias("Dagger", "US"));
            TestAssert.Equal<string>(null, names.Alias("Steiner", "FR"));
        }

        public static void LatinForeignDefaultsCannotBeToldFromCustomNamesAndStay()
        {
            var names = Names();
            TestAssert.Equal<string>(null, names.Alias("Gidan", "US"));
            TestAssert.Equal<string>(null, names.Alias("Djidane", "US"));
            TestAssert.Equal<string>(null, names.Alias("Zidane", "FR"));
        }

        public static void CustomKatakanaNamesAndNearMissesStayUnchanged()
        {
            var names = Names();
            TestAssert.Equal<string>(null, names.Alias("ゾロ", "US"));
            TestAssert.Equal<string>(null, names.Alias("ジタンZ", "US"));
            TestAssert.Equal<string>(null, names.Alias("ジタ", "US"));
            TestAssert.Equal<string>(null, names.Alias("", "US"));
            TestAssert.Equal<string>(null, names.Alias(null, "US"));
            TestAssert.Equal<string>(null, names.Alias("ジタン", null));
            TestAssert.Equal<string>(null, names.Alias("ジタン", "XX"));
        }

        public static void FilterReplacesTheNameOnlyAsAWholeToken()
        {
            var names = Names();
            TestAssert.Equal("Zidane: “Hey, Vivi!”", names.Filter("ジタン: “Hey, Vivi!”", "US", null));
            TestAssert.Equal("Zidane, Attack", names.Filter("ジタン, Attack", "US", null));
            TestAssert.Equal("Zidane, Level 1, HP 105 of 105", names.Filter("ジタン, Level 1, HP 105 of 105", "US", null));
            TestAssert.Equal("Zidane", names.Filter("ジタン", "US", null));
            TestAssert.Equal("Dagger: “Zidane!”", names.Filter("ダガー: “ジタン!”", "US", null));
            // A custom name that merely contains a default keeps all of its letters.
            TestAssert.Equal("ビビジタン: “...”", names.Filter("ビビジタン: “...”", "US", null));
            TestAssert.Equal("ジタンX: “...”", names.Filter("ジタンX: “...”", "US", null));
        }

        public static void FilterFollowsTheSavedPartyIdentityWhenKnown()
        {
            var names = Names();
            // The save does not hold that name, so the word is left as displayed.
            TestAssert.Equal("ジタン: “Hey!”", names.Filter("ジタン: “Hey!”", "US", new[] { "Zidane", "Vivi" }));
            TestAssert.Equal("Zidane: “Hey!”", names.Filter("ジタン: “Hey!”", "US", new[] { "ジタン", "Vivi" }));
            // A known but empty party holds no names at all, so nothing is aliased.
            TestAssert.Equal("ジタン: “Hey!”", names.Filter("ジタン: “Hey!”", "US", new string[0]));
        }

        public static void FilterKeepsJapanesePlayAndEmptyTextUnchanged()
        {
            var names = Names();
            TestAssert.Equal("ジタン: “Hey!”", names.Filter("ジタン: “Hey!”", "JP", null));
            TestAssert.Equal("Zidane: “Hey!”", names.Filter("Zidane: “Hey!”", "JP", null));
            TestAssert.Equal<string>(null, names.Filter(null, "US", null));
            TestAssert.Equal("", names.Filter("", "US", null));
        }
    }
}
