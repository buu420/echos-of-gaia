using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace Memoria.Accessibility.Navigation
{
    public sealed class ObservedIdentityStore
    {
        private readonly Dictionary<TargetIdentity, string> _names = new Dictionary<TargetIdentity, string>();
        private readonly string _directory;
        public string SaveIdentity { get; private set; }
        public int KnownCount { get { return _names.Count; } }
        public ObservedIdentityStore() { }
        public ObservedIdentityStore(string directory) { _directory = directory; }
        public void SwitchSave(string verifiedIdentity)
        {
            _names.Clear(); SaveIdentity = verifiedIdentity;
            string path = StoragePath(verifiedIdentity);
            if (path == null || !File.Exists(path)) return;
            // Companion files are optional evidence. Never substitute stale knowledge
            // when one is unreadable or malformed.
            try {
                foreach (string line in File.ReadAllLines(path)) {
                    string[] parts = line.Split('\t');
                    if (parts.Length != 4) continue;
                    int kind, id;
                    if (!int.TryParse(parts[0], out kind) || !int.TryParse(parts[1], out id) || kind < 0 || kind > (int)TargetKind.Vehicle) continue;
                    Record(new TargetIdentity((TargetKind)kind, Decode(parts[2]), id), Decode(parts[3]));
                }
            } catch { _names.Clear(); }
        }
        public void Record(TargetIdentity id, string displayedLabel) { if (!string.IsNullOrEmpty(displayedLabel)) _names[id] = displayedLabel; }
        public bool TryGetLabel(TargetIdentity id, out string label) { return _names.TryGetValue(id, out label); }
        public void CommitSave(string verifiedNewIdentity)
        {
            if (string.IsNullOrEmpty(verifiedNewIdentity)) { SwitchSave(null); return; }
            SaveIdentity = verifiedNewIdentity;
            string path = StoragePath(verifiedNewIdentity);
            if (path == null) return;
            Directory.CreateDirectory(_directory);
            List<string> lines = new List<string>();
            foreach (KeyValuePair<TargetIdentity, string> entry in _names) lines.Add((int)entry.Key.Kind + "\t" + entry.Key.NativeId + "\t" + Encode(entry.Key.MapKey) + "\t" + Encode(entry.Value));
            lines.Sort(StringComparer.Ordinal);
            string temporary = path + ".tmp";
            File.WriteAllLines(temporary, lines.ToArray(), Encoding.UTF8);
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
        private string StoragePath(string identity)
        {
            if (_directory == null || string.IsNullOrEmpty(identity)) return null;
            using (SHA256 hash = SHA256.Create()) return Path.Combine(_directory, BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").ToLowerInvariant() + ".names");
        }
        private static string Encode(string value) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(value)); }
        private static string Decode(string value) { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
    }
}
