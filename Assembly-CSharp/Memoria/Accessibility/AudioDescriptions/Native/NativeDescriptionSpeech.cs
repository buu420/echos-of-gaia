using System;
using System.IO;
using System.Runtime.InteropServices;
namespace Memoria.Accessibility.AudioDescriptions
{
    // Dedicated SAPI voice through a small native ABI: original Windows Mono
    // does not implement Type.GetTypeFromProgID. This never owns the Prism or
    // screen-reader queue. Load by absolute architecture-specific assembly path.
    public sealed class NativeDescriptionSpeech : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CreateVoice(out IntPtr voice);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl,CharSet=CharSet.Unicode)] private delegate int SpeakVoice(IntPtr voice,[MarshalAs(UnmanagedType.LPWStr)]string text,int volume);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int VoiceCall(IntPtr voice);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
        [DllImport("kernel32.dll",CharSet=CharSet.Ansi,ExactSpelling=true)] private static extern IntPtr GetProcAddress(IntPtr module,string name);
        [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr module);
        private IntPtr _module,_voice;
        private SpeakVoice _speak;
        private VoiceCall _pause,_resume,_stop,_playing,_free;
        private bool _disposed;
        private Delegate Bind(string name,Type type)
        {
            IntPtr address=GetProcAddress(_module,name);
            if(address==IntPtr.Zero)throw new EntryPointNotFoundException(name);
            return Marshal.GetDelegateForFunctionPointer(address,type);
        }
        private static void Check(int result){if(result<0)throw new InvalidOperationException("Description speech failed (0x"+unchecked((uint)result).ToString("x8")+")");}
        private void Initialize()
        {
            if(_disposed)throw new ObjectDisposedException("NativeDescriptionSpeech");
            if(_voice!=IntPtr.Zero)return;
            string path=Path.Combine(Path.GetDirectoryName(typeof(NativeDescriptionSpeech).Assembly.Location),"Memoria.DescriptionSpeech.dll");
            _module=LoadLibraryEx(path,IntPtr.Zero,8);
            if(_module==IntPtr.Zero)throw new DllNotFoundException("Owned description speech dependency could not load");
            try {
                var create=(CreateVoice)Bind("FF9AD_Create",typeof(CreateVoice));
                _speak=(SpeakVoice)Bind("FF9AD_Speak",typeof(SpeakVoice));
                _pause=(VoiceCall)Bind("FF9AD_Pause",typeof(VoiceCall));_resume=(VoiceCall)Bind("FF9AD_Resume",typeof(VoiceCall));
                _stop=(VoiceCall)Bind("FF9AD_Stop",typeof(VoiceCall));_playing=(VoiceCall)Bind("FF9AD_Playing",typeof(VoiceCall));_free=(VoiceCall)Bind("FF9AD_Free",typeof(VoiceCall));
                Check(create(out _voice));if(_voice==IntPtr.Zero)throw new InvalidOperationException("Owned description speech returned no voice");
            }catch{if(_voice==IntPtr.Zero){FreeLibrary(_module);_module=IntPtr.Zero;}throw;}
        }
        public bool TrySpeak(string text,int volume)
        {
            if(String.IsNullOrEmpty(text))return false;
            Initialize();Check(_speak(_voice,text,Math.Max(0,Math.Min(100,volume))));return true;
        }
        public bool Playing {get{if(_voice==IntPtr.Zero)return false;int result=_playing(_voice);Check(result);return result==1;}}
        public void Pause(){if(_voice!=IntPtr.Zero)Check(_pause(_voice));}
        public void Resume(){if(_voice!=IntPtr.Zero)Check(_resume(_voice));}
        public void Stop(){if(_voice!=IntPtr.Zero)Check(_stop(_voice));}
        public void Dispose()
        {
            if(_disposed)return;
            // If native cleanup fails, keep its module/voice rooted. Unloading
            // code while SAPI can still call it would be unsafe.
            if(_voice!=IntPtr.Zero){Check(_free(_voice));_voice=IntPtr.Zero;}
            if(_module!=IntPtr.Zero){FreeLibrary(_module);_module=IntPtr.Zero;}
            _disposed=true;
        }
    }
}
