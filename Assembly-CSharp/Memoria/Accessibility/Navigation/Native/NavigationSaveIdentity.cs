using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Memoria.Prime;
using SimpleJSON;
using UnityEngine;
namespace Memoria.Accessibility.Navigation
{
    public static class NavigationSaveIdentity
    {
        public static readonly ObservedIdentityStore Identities = new ObservedIdentityStore(Path.Combine(Application.persistentDataPath, "FF9Access/observations"));
        public static string Current { get { return Identities.SaveIdentity; } }
        public static event Action<string> Loaded, Saved;
        public static string Fingerprint(JSONClass root)
        {
            if (root == null) return null;
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(root.ToString()))).Replace("-", "").ToLowerInvariant();
        }
        public static void PrepareLoad()
        {
            NavigationLifecycleBridge.ReplaceSave(); Identities.SwitchSave(null);
            if (Loaded != null) Loaded(null);
        }
        public static void CompleteLoad(JSONClass root)
        {
            NavigationLifecycleBridge.ReplaceSave(); string identity = Fingerprint(root); Identities.SwitchSave(identity);
            if (Loaded != null) Loaded(identity);
            Log.Message("[FF9Access] Navigation observations switched to loaded save content.");
        }
        public static void CompleteSave(string verifiedIdentity)
        {
            try { Identities.CommitSave(verifiedIdentity); if (Saved != null) Saved(verifiedIdentity); }
            catch (Exception exception) { Log.Message("[FF9Access] Could not persist navigation observations: {0}", exception); }
        }
        public static void NewGame() { PrepareLoad(); }
    }
}
