using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
namespace Memoria.ScreenReader
{
    // Prism v0.17.3: cdecl, C bool=1 byte, strings=UTF-8. prism_init accepts NULL config.
    public sealed class PrismNative : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr InitCall(IntPtr config);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void FreeCall(IntPtr handle);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr PointerCall(IntPtr handle);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int OutputCall(IntPtr backend, IntPtr utf8, [MarshalAs(UnmanagedType.I1)] bool interrupt);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int StatusCall(IntPtr backend, [MarshalAs(UnmanagedType.I1)] out bool speaking);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int StopCall(IntPtr backend);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate ulong FeaturesCall(IntPtr backend);
        private IntPtr _library, _context, _backend;
        private InitCall _init; private FreeCall _shutdown, _freeBackend; private PointerCall _createBest, _name;
        private OutputCall _output; private StatusCall _status; private StopCall _stop; private FeaturesCall _features;
        public string Name { get; private set; }
        public int LastError { get; private set; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr LoadLibraryEx(string path, IntPtr reserved, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)] private static extern IntPtr GetProcAddress(IntPtr library, string name);
        [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FreeLibrary(IntPtr library);
        public PrismNative(string absolutePath)
        {
            if (!Path.IsPathRooted(absolutePath)) throw new ArgumentException("Prism requires an absolute library path");
            _library = LoadLibraryEx(absolutePath, IntPtr.Zero, 0x100 | 0x1000);
            if (_library == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Prism library could not load: " + absolutePath);
            try {
                _init = Bind<InitCall>("prism_init"); _shutdown = Bind<FreeCall>("prism_shutdown");
                _createBest = Bind<PointerCall>("prism_registry_create_best"); _freeBackend = Bind<FreeCall>("prism_backend_free");
                _name = Bind<PointerCall>("prism_backend_name"); _output = Bind<OutputCall>("prism_backend_output");
                _status = Bind<StatusCall>("prism_backend_is_speaking"); _stop = Bind<StopCall>("prism_backend_stop"); _features = Bind<FeaturesCall>("prism_backend_get_features");
                _context = _init(IntPtr.Zero); if (_context == IntPtr.Zero) throw new InvalidOperationException("Prism context initialization failed");
            } catch { Dispose(); throw; }
        }
        private T Bind<T>(string name) where T : class
        {
            IntPtr address = GetProcAddress(_library, name); if (address == IntPtr.Zero) throw new EntryPointNotFoundException(name);
            return Marshal.GetDelegateForFunctionPointer(address, typeof(T)) as T;
        }
        public bool SelectBackend()
        {
            if (_backend != IntPtr.Zero) return true;
            _backend = _createBest(_context); if (_backend == IntPtr.Zero) return false;
            Name = ReadUtf8(_name(_backend)); return true;
        }
        public bool Output(string text, bool interrupt)
        {
            if (_backend == IntPtr.Zero) return false;
            byte[] bytes = Encoding.UTF8.GetBytes(text + "\0"); IntPtr pointer = Marshal.AllocHGlobal(bytes.Length);
            try { Marshal.Copy(bytes, 0, pointer, bytes.Length); LastError = _output(_backend, pointer, interrupt); return LastError == 0; } finally { Marshal.FreeHGlobal(pointer); }
        }
        public bool IsSpeaking()
        {
            if (_backend == IntPtr.Zero || (_features(_backend) & (1UL << 6)) == 0) return false;
            bool speaking; LastError = _status(_backend, out speaking); return LastError == 0 && speaking;
        }
        public void Stop() { if (_backend != IntPtr.Zero && (_features(_backend) & (1UL << 7)) != 0) LastError = _stop(_backend); }
        private static string ReadUtf8(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return null; int length = 0;
            while (length < 65536 && Marshal.ReadByte(pointer, length) != 0) length++;
            byte[] bytes = new byte[length]; Marshal.Copy(pointer, bytes, 0, length); return Encoding.UTF8.GetString(bytes);
        }
        public void Dispose()
        {
            if (_backend != IntPtr.Zero) { _freeBackend(_backend); _backend = IntPtr.Zero; }
            if (_context != IntPtr.Zero) { _shutdown(_context); _context = IntPtr.Zero; }
            if (_library != IntPtr.Zero) { FreeLibrary(_library); _library = IntPtr.Zero; }
        }
    }
}
