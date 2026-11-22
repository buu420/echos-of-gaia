using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
namespace FF9Access.Tests
{
    internal static partial class NativeReaderVerification
    {
        // Shop rows and quantities must interrupt for every input device. NVDA cancels speech on its own
        // for keyboard keys, so a non-interrupting Speak only shows up as queued rows on a controller.
        public static void VerifyShopSpeech(string library)
        {
            using (var stream = File.OpenRead(library)) using (var pe = new PEReader(stream)) {
                var metadata = pe.GetMetadataReader(); var interrupting = new Dictionary<string, int>();
                foreach (var typeHandle in metadata.TypeDefinitions) {
                    var type = metadata.GetTypeDefinition(typeHandle); if (metadata.GetString(type.Name) != "ShopUI") continue;
                    foreach (var methodHandle in type.GetMethods()) {
                        var method = metadata.GetMethodDefinition(methodHandle); string name = metadata.GetString(method.Name);
                        if ((name != "AnnounceButtonForScreenReader" && name != "AnnounceQuantityChange") || method.RelativeVirtualAddress == 0) continue;
                        var calls = new List<string>(Calls(metadata, pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()));
                        for (int i = 0; i < calls.Count; i++) {
                            if (calls[i] == "ScreenReaderManager.SpeakInterrupt") { Count(interrupting, name); continue; }
                            if (calls[i] != "ScreenReaderManager.Speak" && calls[i] != "ScreenReaderManager.TrySpeak") continue;
                            // The interrupt argument is the constant pushed right before the call.
                            string flag = i == 0 ? "(none)" : calls[i - 1];
                            if (flag != "IL.ldc.i4.1") throw new Exception("ShopUI." + name + " speaks without interrupting (" + flag + " before " + calls[i] + ") in " + library);
                            Count(interrupting, name);
                        }
                    }
                }
                if (!interrupting.ContainsKey("AnnounceButtonForScreenReader") || !interrupting.ContainsKey("AnnounceQuantityChange")) throw new Exception("ShopUI speech calls were not found in " + library);
                Console.WriteLine("PASS emitted shop speech: " + interrupting["AnnounceButtonForScreenReader"] + " row and " + interrupting["AnnounceQuantityChange"] + " quantity announcements interrupt in " + library);
            }
        }
        private static void Count(Dictionary<string, int> counts, string key) { int value; counts.TryGetValue(key, out value); counts[key] = value + 1; }

        // Every announcement passes through one speech-only name filter fed by the native default-name
        // table, the current language and the saved party, so dialogue, menus and battle all agree.
        public static void VerifyNameSpeech(string library)
        {
            var required = new HashSet<string> {
                "UIKeyTrigger.Start|CharacterNameSpeech.Install",
                "CharacterNameSpeech.Install|ScreenReaderManager.set_TextFilter",
                "CharacterNameSpeech.Filter|SpokenCharacterNames.Filter",
                "CharacterNameSpeech.Filter|Localization.get_CurrentSymbol",
                "CharacterNameSpeech.Filter|CharacterNameSpeech.Names",
                "CharacterNameSpeech.Filter|CharacterNameSpeech.SavedNames",
                "CharacterNameSpeech.Names|FieldRead.CharacterNamesFormatter.DefaultNamesByLang",
                "CharacterNameSpeech.SavedNames|FieldRead.FF9StateGlobal.player",
                "CharacterNameSpeech.SavedNames|PLAYER.get_Name"
            };
            using (var stream = File.OpenRead(library)) using (var pe = new PEReader(stream)) {
                var metadata = pe.GetMetadataReader();
                foreach (var typeHandle in metadata.TypeDefinitions) {
                    var type = metadata.GetTypeDefinition(typeHandle); string owner = metadata.GetString(type.Name);
                    if (owner != "UIKeyTrigger" && owner != "CharacterNameSpeech") continue;
                    foreach (var methodHandle in type.GetMethods()) {
                        var method = metadata.GetMethodDefinition(methodHandle); if (method.RelativeVirtualAddress == 0) continue;
                        string caller = owner + "." + metadata.GetString(method.Name);
                        foreach (string call in Calls(metadata, pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes())) required.Remove(caller + "|" + call);
                    }
                }
                if (required.Count > 0) throw new Exception("Name speech wiring is missing: " + String.Join(", ", required) + " in " + library);
                Console.WriteLine("PASS emitted name speech: native default-name filter installed once and fed by language and saved party in " + library);
            }
        }
    }
}
