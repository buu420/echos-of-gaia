using System;
using System.IO;
using System.Reflection;

internal static class SpeechRecoveryProbe
{
    private static Assembly _reader;
    private static string _faultLibrary;
    private static int _opens;
    private static int Main(string[] args)
    {
        try {
            if (args.Length != 2) throw new ArgumentException("Speech assembly and expected architecture required");
            string architecture = IntPtr.Size == 8 ? "x64" : "x86";
            if (architecture != args[1]) throw new InvalidOperationException("Mono architecture mismatch");
            // No compile-time reader reference: original Mono would resolve it from the
            // installed directory before Main, silently exercising the old installation.
            _reader = Assembly.LoadFrom(args[0]);
            if (!String.Equals(Path.GetFullPath(_reader.Location), Path.GetFullPath(args[0]), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Recovery probe loaded a different speech assembly: " + _reader.Location);
            _faultLibrary = Path.Combine(Path.GetDirectoryName(_reader.Location), "PrismOutputFailure.dll");
            Type managerType = _reader.GetType("Memoria.ScreenReader.ScreenReaderManager", true);
            Type backendType = _reader.GetType("Memoria.ScreenReader.ISpeechBackend", true);
            Type factoryType = typeof(Func<>).MakeGenericType(backendType);
            MethodInfo generic = typeof(SpeechRecoveryProbe).GetMethod("CreatePrism", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(backendType);
            Delegate factory = Delegate.CreateDelegate(factoryType, generic);
            Func<DateTime> clock = () => DateTime.UtcNow;
            ConstructorInfo constructor = managerType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new Type[] { factoryType, typeof(Func<DateTime>) }, null);
            object manager = constructor.Invoke(new object[] { factory, clock });
            try {
                managerType.GetMethod("Initialize").Invoke(manager, null);
                string name = (string)managerType.GetMethod("GetScreenReaderName").Invoke(manager, null);
                if (!name.Contains("Injected output failure")) throw new InvalidOperationException("Fault backend did not initialize");
                MethodInfo speak = managerType.GetMethod("TrySpeak");
                if (!(bool)speak.Invoke(manager, new object[] { "Current focus", true }) || _opens != 1)
                    throw new InvalidOperationException("A braille error repeated speech Prism already accepted");
                if ((bool)speak.Invoke(manager, new object[] { "Rejected text", true }) || _opens != 1
                    || (string)managerType.GetProperty("LastAnnouncement").GetValue(manager, null) != "Current focus")
                    throw new InvalidOperationException("A text rejection replaced the current Prism backend or announcement");
                string text = "\0Final Fantasy IX Prism\0speech check. \uD800";
                string expected = "Final Fantasy IX Prism speech check. \uFFFD";
                bool accepted = (bool)speak.Invoke(manager, new object[] { text, false });
                string diagnostic = (string)managerType.GetProperty("BackendDiagnostic").GetValue(manager, null);
                if (!accepted) throw new InvalidOperationException("Rejected native output did not recover: " + diagnostic);
                if ((string)managerType.GetProperty("LastAnnouncement").GetValue(manager, null) != expected
                    || (long)managerType.GetProperty("OutputSerial").GetValue(manager, null) != 2
                    || (long)managerType.GetProperty("SpeechGeneration").GetValue(manager, null) != 2 || _opens != 2)
                    throw new InvalidOperationException("Recovered output acceptance or backend ownership is incorrect");
                if (!diagnostic.StartsWith("Prism: ", StringComparison.Ordinal) || !diagnostic.Contains("(reconnected)"))
                    throw new InvalidOperationException("Prism reconnect was not reported: " + diagnostic);
                Console.WriteLine("PASS Mono speech output recovery " + architecture + ": " + diagnostic + "; reader=" + _reader.Location);
                System.Threading.Thread.Sleep(1000);
            } finally { managerType.GetMethod("Dispose").Invoke(manager, null); }
            return 0;
        } catch (Exception exception) { Console.Error.WriteLine("FAIL Mono speech recovery: " + exception); return 1; }
    }
    private static T CreatePrism<T>()
    {
        Type prism = _reader.GetType("Memoria.ScreenReader.PrismSpeechBackend", true);
        return (T)(++_opens == 1 ? Activator.CreateInstance(prism, new object[] { _faultLibrary }) : Activator.CreateInstance(prism));
    }
}
