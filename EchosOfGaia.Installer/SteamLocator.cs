using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace EchosOfGaia.Installer
{
    public static class SteamLocator
    {
        public static IEnumerable<string> GetSteamRoots()
        {
            var roots = new List<string>();
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    if (key != null && key.GetValue("SteamPath") is string path) roots.Add(path);
            }
            catch (SecurityException) { }
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            return roots;
        }

        public static string FindGameFolder(IEnumerable<string> steamRoots)
        {
            var libraries = new Queue<string>(steamRoots);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (libraries.Count > 0)
            {
                string library = libraries.Dequeue();
                if (string.IsNullOrWhiteSpace(library)) continue;
                try
                {
                    library = Path.GetFullPath(library);
                    if (!seen.Add(library)) continue;
                    string apps = Path.Combine(library, "steamapps");
                    string manifest = ReadSmallFile(Path.Combine(apps, "appmanifest_377840.acf"));
                    string install = ReadValue(manifest, "installdir");
                    if (ReadValue(manifest, "appid") == "377840" && !string.IsNullOrEmpty(install) &&
                        install != "." && install != ".." && install == Path.GetFileName(install) && !Path.IsPathRooted(install))
                    {
                        string game = Path.Combine(apps, "common", install);
                        if (File.Exists(Path.Combine(game, "x86", "FF9.exe")) &&
                            File.Exists(Path.Combine(game, "x64", "FF9.exe"))) return game;
                    }
                    foreach (Match path in Regex.Matches(ReadSmallFile(Path.Combine(apps, "libraryfolders.vdf")),
                        "\"path\"\\s+\"((?:\\\\.|[^\"\\\\])*)\"", RegexOptions.IgnoreCase))
                        libraries.Enqueue(Unescape(path.Groups[1].Value));
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
                    error is ArgumentException || error is NotSupportedException || error is SecurityException) { }
            }
            return null;
        }

        private static string ReadSmallFile(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 2 * 1024 * 1024) return "";
            return File.ReadAllText(path);
        }
        private static string ReadValue(string text, string key)
        {
            Match match = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s+\"((?:\\\\.|[^\"\\\\])*)\"", RegexOptions.IgnoreCase);
            return match.Success ? Unescape(match.Groups[1].Value) : null;
        }
        private static string Unescape(string value) { return value.Replace("\\\\", "\\").Replace("\\\"", "\""); }
    }
}
