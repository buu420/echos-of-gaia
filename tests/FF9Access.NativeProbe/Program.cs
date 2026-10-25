using System;
using System.IO;
using Memoria.ScreenReader;
internal static class Program
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)] private static extern IntPtr LoadLibrary(string path);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr module);
    private static int Main(string[] args)
    {
        try {
            string architecture = null, library = null, mode = null;
            for (int i = 0; i + 1 < args.Length; i += 2) { if (args[i] == "--architecture") architecture = args[i + 1]; else if (args[i] == "--library") library = args[i + 1]; else if (args[i] == "--mode") mode = args[i + 1]; else throw new ArgumentException("Unknown argument " + args[i]); }
            if (architecture != (IntPtr.Size == 8 ? "x64" : "x86")) throw new InvalidOperationException("Probe architecture mismatch");
            if (string.IsNullOrEmpty(library) || !Path.IsPathRooted(library)) throw new ArgumentException("Absolute library required");
            if (mode == "tolk") {
                Directory.SetCurrentDirectory(Path.GetDirectoryName(library));
                IntPtr module=LoadLibrary(library);
                if(module==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                try { using(var fallback=new TolkSpeechBackend()) {
                    if(!fallback.Initialize() || !fallback.Output("Final Fantasy IX fallback speech test. Entrée.",true))throw new InvalidOperationException("Tolk output failed");
                    Console.WriteLine("PASS Tolk fallback output accepted "+architecture+": "+fallback.Name+"; audible speech requires listener verification");
                    System.Threading.Thread.Sleep(2000);
                } } finally {FreeLibrary(module);}
                return 0;
            }
            using (var native = new PrismNative(library)) {
                if (mode == "abi") Console.WriteLine("PASS Prism ABI " + architecture + ": exports bound, null-config context created; output not tested");
                else if (mode == "output") {
                    if (!native.SelectBackend()) throw new InvalidOperationException("No Prism backend available");
                    if (!native.Output("Final Fantasy IX accessibility speech test. Alexandrie, entrée.", true)) throw new InvalidOperationException("Prism output error " + native.LastError);
                    Console.WriteLine("PASS Prism output accepted " + architecture + ": " + native.Name + "; audible speech requires listener verification");
                    System.Threading.Thread.Sleep(2000);
                } else throw new ArgumentException("Mode must be abi or output");
            }
            return 0;
        } catch (Exception exception) { Console.Error.WriteLine("FAIL: " + exception); return 1; }
    }
}
