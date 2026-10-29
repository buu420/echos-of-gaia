using System;
using System.IO;
using System.Reflection;

internal static class SpeechRecoveryProbe
{
    private static Assembly _reader;
    private static string _faultLibrary;
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
            Delegate preferred = Factory("CreatePreferred", factoryType, backendType);
            Delegate compatibility = Factory("CreateCompatibility", factoryType, backendType);
            Func<DateTime> clock = () => DateTime.UtcNow;
            ConstructorInfo constructor = managerType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new Type[] { factoryType, factoryType, typeof(Func<DateTime>) }, null);
            object manager = constructor.Invoke(new object[] { preferred, compatibility, clock });
            try {
                managerType.GetMethod("Initialize").Invoke(manager, null);
                string name = (string)managerType.GetMethod("GetScreenReaderName").Invoke(manager, null);
                if (!name.Contains("Injected output failure")) throw new InvalidOperationException("Fault backend did not initialize");
                string text = "Final Fantasy IX compatibility speech recovery validation.";
                bool accepted = (bool)managerType.GetMethod("TrySpeak").Invoke(manager, new object[] { text, false });
                string diagnostic = (string)managerType.GetProperty("BackendDiagnostic").GetValue(manager, null);
                if (!accepted) throw new InvalidOperationException("Rejected native output did not recover: " + diagnostic);
                if ((string)managerType.GetProperty("LastAnnouncement").GetValue(manager, null) != text
                    || (long)managerType.GetProperty("OutputSerial").GetValue(manager, null) != 1
                    || (long)managerType.GetProperty("SpeechGeneration").GetValue(manager, null) != 2)
                    throw new InvalidOperationException("Recovered output acceptance or backend ownership is incorrect");
                if (!diagnostic.Contains("fallback") || !diagnostic.Contains("Tolk ") || !diagnostic.Contains("Prism error 6"))
                    throw new InvalidOperationException("Native error or compatibility backend was not reported: " + diagnostic);
                Console.WriteLine("PASS Mono speech output recovery " + architecture + ": " + diagnostic + "; reader=" + _reader.Location);
                System.Threading.Thread.Sleep(1000);
            } finally { managerType.GetMethod("Dispose").Invoke(manager, null); }
            return 0;
        } catch (Exception exception) { Console.Error.WriteLine("FAIL Mono speech recovery: " + exception); return 1; }
    }
    private static Delegate Factory(string method, Type factoryType, Type backendType)
    {
        MethodInfo generic = typeof(SpeechRecoveryProbe).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(backendType);
        return Delegate.CreateDelegate(factoryType, generic);
    }
    private static T CreatePreferred<T>()
    {
        return (T)Activator.CreateInstance(_reader.GetType("Memoria.ScreenReader.PrismSpeechBackend", true), new object[] { _faultLibrary });
    }
    private static T CreateCompatibility<T>()
    {
        return (T)Activator.CreateInstance(_reader.GetType("Memoria.ScreenReader.TolkSpeechBackend", true));
    }
}
