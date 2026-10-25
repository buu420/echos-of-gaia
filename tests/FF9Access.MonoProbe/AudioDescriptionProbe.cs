using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;

internal static class AudioDescriptionProbe
{
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    internal static void Run(string library)
    {
        Assembly game = Assembly.LoadFrom(library);
        Type output = game.GetType("Memoria.Accessibility.AudioDescriptions.DescriptionWaveOutput", true);
        byte[] wave = Wave();
        Check(Math.Abs((double)output.GetMethod("Duration").Invoke(null, new object[] { wave }) - 0.2) < 0.000001, "Mono WAV parser changed");
        object player = Activator.CreateInstance(output);
        try
        {
            Check((bool)output.GetMethod("TryPlay").Invoke(player, new object[] { wave }), "Mono waveOut rejected silent fixture");
            Check((bool)output.GetProperty("Playing").GetValue(player, null), "Driver completed the 200 ms fixture before first observation");
            output.GetMethod("Pause").Invoke(player, null);
            output.GetMethod("Resume").Invoke(player, null);
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while ((bool)output.GetProperty("Playing").GetValue(player, null) && DateTime.UtcNow < deadline) Thread.Sleep(10);
            Check(!(bool)output.GetProperty("Playing").GetValue(player, null), "Mono waveOut driver completion missing");
        }
        finally { ((IDisposable)player).Dispose(); }

        // Exercise the actual managed/native fallback boundary in the original
        // Mono host. Volume zero keeps this verification inaudible.
        Type speechType=game.GetType("Memoria.Accessibility.AudioDescriptions.NativeDescriptionSpeech",true);
        object speech=Activator.CreateInstance(speechType);
        try {
            Check((bool)speechType.GetMethod("TrySpeak").Invoke(speech,new object[]{"A ship sails through clouds.",0}),"Mono owned speech rejected fixture");
            Check((bool)speechType.GetProperty("Playing").GetValue(speech,null),"Mono owned speech lost pending queue");
            speechType.GetMethod("Pause").Invoke(speech,null);speechType.GetMethod("Pause").Invoke(speech,null);
            Check((bool)speechType.GetProperty("Playing").GetValue(speech,null),"Mono paused owned speech did not retain queue");
            speechType.GetMethod("Resume").Invoke(speech,null);speechType.GetMethod("Stop").Invoke(speech,null);
            Check(!(bool)speechType.GetProperty("Playing").GetValue(speech,null),"Mono owned speech did not purge its queue");
        } finally{((IDisposable)speech).Dispose();}

        Type specType = game.GetType("Memoria.Accessibility.AudioDescriptions.DescriptionAudioSpec", true);
        string hash; using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(wave)).Replace("-", "").ToLowerInvariant();
        object spec = Activator.CreateInstance(specType, new object[] { "mono-fixture", "mono-fixture.wav", hash, wave.Length, 0.2d });
        Array specs = Array.CreateInstance(specType, 1); specs.SetValue(spec, 0);
        Type bankType = game.GetType("Memoria.Accessibility.AudioDescriptions.DescriptionAudioBank", true);
        object bank = Activator.CreateInstance(bankType, new object[] { new Action<Action>(run => run()) });
        bankType.GetMethod("Prepare").Invoke(bank, new object[] { 37L, specs, new Func<bool>(() => true), new Func<string, byte[]>(name => wave) });
        Check((bool)bankType.GetMethod("SourceVerified").Invoke(bank, new object[] { 37L }), "Mono source worker did not publish");
        object[] get = { 37L, "mono-fixture", null };
        Check((bool)bankType.GetMethod("TryGet").Invoke(bank, get) && ((byte[])get[2]).Length == wave.Length, "Mono verified audio missing");
        bankType.GetMethod("Reset").Invoke(bank, null);
        Check(!(bool)bankType.GetMethod("TryGet").Invoke(bank, get), "Mono stale scene retained audio");

        Type catalog = game.GetType("Memoria.Accessibility.AudioDescriptions.NativeDescriptionCatalog", true);
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("fr-FR");
            catalog.GetMethod("Initialize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            object failure = catalog.GetField("Failure", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Check(failure == null, "Mono catalog load failed: " + failure);
            var films = (System.Collections.IDictionary)catalog.GetField("Films", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Check(films.Count == 42, "All 42 reviewed FMVs must be present: " + films.Count);
            var audio = (System.Collections.IDictionary)catalog.GetField("Audio", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Check(audio.Count > 150, "Audio catalog is incomplete: " + audio.Count);
            CheckFieldBankCatalog(catalog,hash,wave.Length);
        }
        finally { Thread.CurrentThread.CurrentCulture = previous; }
        Console.WriteLine("PASS exact Mono descriptions: emitted WAV parser, muted owned waveOut and SAPI pause/resume/stop/completion, verified bank/reset and all 42 movie timelines under fr-FR");
    }
    private static void CheckFieldBankCatalog(Type catalog,string hash,int bytes)
    {
        string json="{\"schema\":1,\"clips\":[{\"id\":\"mono-fixture\",\"text\":\"A visible gesture.\",\"file\":\"mono-fixture.wav\",\"sha256\":\""+hash+"\",\"bytes\":"+bytes.ToString(CultureInfo.InvariantCulture)+",\"duration\":0.2}],\"films\":[],\"fieldBanks\":[{\"id\":\"mono-scene\",\"fields\":[50,51],\"clips\":[\"mono-fixture\"]}]}";
        MethodInfo load=catalog.GetMethod("Load",BindingFlags.Static|BindingFlags.NonPublic);
        load.Invoke(null,new object[]{json});
        var maps=(System.Collections.IDictionary)catalog.GetField("FieldBanks",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        var cues=(System.Collections.IDictionary)catalog.GetField("FieldCueBanks",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        var generic=(Array)catalog.GetField("GenericFieldAudio",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        Check(maps.Count==2 && Object.ReferenceEquals(maps[50],maps[51]) && cues.Count==1 && generic.Length==0,"Mono typed field bank ownership changed");
        foreach(string token in new[]{"\"50\"","50.0","5e1","true","null","2147483648"}) {
            bool rejected=false;
            try{load.Invoke(null,new object[]{json.Replace("[50,51]","["+token+",51]")});}
            catch(TargetInvocationException e){if(e.InnerException is InvalidOperationException)rejected=true;else throw;}
            Check(rejected,"Mono accepted invalid field bank map token: "+token);
        }
        Console.WriteLine("PASS exact Mono field bank catalog: typed optional arrays, shared map ownership and string/float/exponent/bool/null/overflow rejection under fr-FR");
    }
    private static byte[] Wave()
    {
        var stream = new MemoryStream(); var writer = new BinaryWriter(stream);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(9636);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write((short)1); writer.Write(24000); writer.Write(48000);
        writer.Write((short)2); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(9600); writer.Write(new byte[9600]); return stream.ToArray();
    }
}
