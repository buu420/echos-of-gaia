using System;
using System.IO;
using System.Runtime.InteropServices;
namespace Memoria.Accessibility.Navigation
{
    // Valve Steam Audio 4.6.1 C ABI. The native worker exclusively owns this
    // instance. No Unity audio callbacks, global DLL search path or game mixer.
    public sealed class SteamAudioHrtf : ISpatialHrtf
    {
        private IntPtr _module,_context,_hrtf;private readonly IntPtr[] _effects=new IntPtr[3];private AudioBuffer _input,_output;
        private readonly float[] _mono=new float[512],_left=new float[512],_right=new float[512];
        private ContextCreate _contextCreate;private Release _contextRelease,_hrtfRelease,_effectRelease;
        private HrtfCreate _hrtfCreate;private EffectCreate _effectCreate;private EffectReset _reset;private EffectApply _apply;private EffectTail _tail;private BufferAllocate _allocate;private BufferFree _free;
        public SteamAudioHrtf(string library)
        {
            if(String.IsNullOrEmpty(library) || !Path.IsPathRooted(library))throw new ArgumentException("Absolute Steam Audio library path required");
            try {
                _module=LoadLibraryExW(library,IntPtr.Zero,8);if(_module==IntPtr.Zero)throw new InvalidOperationException("Steam Audio load failed: "+Marshal.GetLastWin32Error());
                _contextCreate=(ContextCreate)Bind(typeof(ContextCreate),"iplContextCreate",8);_contextRelease=(Release)Bind(typeof(Release),"iplContextRelease",4);
                _hrtfCreate=(HrtfCreate)Bind(typeof(HrtfCreate),"iplHRTFCreate",16);_hrtfRelease=(Release)Bind(typeof(Release),"iplHRTFRelease",4);
                _effectCreate=(EffectCreate)Bind(typeof(EffectCreate),"iplBinauralEffectCreate",16);_effectRelease=(Release)Bind(typeof(Release),"iplBinauralEffectRelease",4);
                _reset=(EffectReset)Bind(typeof(EffectReset),"iplBinauralEffectReset",4);_apply=(EffectApply)Bind(typeof(EffectApply),"iplBinauralEffectApply",16);_tail=(EffectTail)Bind(typeof(EffectTail),"iplBinauralEffectGetTail",8);
                _allocate=(BufferAllocate)Bind(typeof(BufferAllocate),"iplAudioBufferAllocate",16);_free=(BufferFree)Bind(typeof(BufferFree),"iplAudioBufferFree",8);
                var context=new ContextSettings{Version=0x040601};Check(_contextCreate(ref context,out _context),"context");
                var audio=new AudioSettings{SamplingRate=SpatialWaveOutput.SampleRate,FrameSize=SpatialWaveOutput.FrameSize};var hrtf=new HrtfSettings{Volume=1};
                Check(_hrtfCreate(_context,ref audio,ref hrtf,out _hrtf),"HRTF");var effect=new EffectSettings{Hrtf=_hrtf};
                for(int i=0;i<3;i++)Check(_effectCreate(_context,ref audio,ref effect,out _effects[i]),"binaural effect");
                Check(_allocate(_context,1,512,ref _input),"input buffer");Check(_allocate(_context,2,512,ref _output),"output buffer");
            }catch{Dispose();throw;}
        }
        private Delegate Bind(Type type,string name,int stackBytes)
        {
            string export=IntPtr.Size==4?"_"+name+"@"+stackBytes:name;IntPtr pointer=GetProcAddress(_module,export);
            if(pointer==IntPtr.Zero)throw new EntryPointNotFoundException("Steam Audio export missing: "+export);
            return Marshal.GetDelegateForFunctionPointer(pointer,type);
        }
        public void Reset(int lane){Validate(lane);_reset(_effects[lane]);}
        public bool Mix(int lane,float[] mono,int offset,int count,NavigationPoint direction,float gain,float[] stereo)
        {
            Validate(lane);float length=direction.X*direction.X+direction.Y*direction.Y+direction.Z*direction.Z;
            if(mono==null || offset<0 || count<0 || count>512 || offset>mono.Length-count || stereo==null || stereo.Length!=1024 || !direction.IsFinite || length<.99f || length>1.01f || Single.IsNaN(gain) || Single.IsInfinity(gain) || gain<0 || gain>2)throw new ArgumentException("Invalid HRTF frame");
            int state;
            if(count==0)state=_tail(_effects[lane],ref _output);
            else {
                Array.Clear(_mono,0,512);Array.Copy(mono,offset,_mono,0,count);Marshal.Copy(_mono,0,Marshal.ReadIntPtr(_input.Data),512);
                var parameters=new EffectParams{Direction=direction,Interpolation=1,SpatialBlend=1,Hrtf=_hrtf};state=_apply(_effects[lane],ref parameters,ref _input,ref _output);
            }
            if(state!=0 && state!=1)throw new InvalidOperationException("Invalid Steam Audio tail state");
            Marshal.Copy(Marshal.ReadIntPtr(_output.Data),_left,0,512);Marshal.Copy(Marshal.ReadIntPtr(_output.Data,IntPtr.Size),_right,0,512);
            for(int i=0;i<512;i++){stereo[2*i]+=_left[i]*gain;stereo[2*i+1]+=_right[i]*gain;}
            return state==0;
        }
        private void Validate(int lane){if(_module==IntPtr.Zero || _hrtf==IntPtr.Zero)throw new ObjectDisposedException("SteamAudioHrtf");if(lane<0 || lane>=3)throw new ArgumentOutOfRangeException("lane");}
        public void Dispose()
        {
            if(_input.Data!=IntPtr.Zero)_free(_context,ref _input);if(_output.Data!=IntPtr.Zero)_free(_context,ref _output);
            for(int i=0;i<3;i++)if(_effects[i]!=IntPtr.Zero)_effectRelease(ref _effects[i]);
            if(_hrtf!=IntPtr.Zero)_hrtfRelease(ref _hrtf);if(_context!=IntPtr.Zero)_contextRelease(ref _context);
            if(_module!=IntPtr.Zero){FreeLibrary(_module);_module=IntPtr.Zero;}
        }
        private static void Check(int error,string operation){if(error!=0)throw new InvalidOperationException("Steam Audio "+operation+" failed: "+error);}
        [StructLayout(LayoutKind.Sequential)]private struct ContextSettings {internal uint Version;internal IntPtr Log,Allocate,Free;internal int Simd,Flags;}
        [StructLayout(LayoutKind.Sequential)]private struct AudioSettings {internal int SamplingRate,FrameSize;}
        [StructLayout(LayoutKind.Sequential)]private struct HrtfSettings {internal int Type;internal IntPtr SofaFile,SofaData;internal int SofaSize;internal float Volume;internal int NormType;}
        [StructLayout(LayoutKind.Sequential)]private struct EffectSettings {internal IntPtr Hrtf;}
        [StructLayout(LayoutKind.Sequential)]private struct EffectParams {internal NavigationPoint Direction;internal int Interpolation;internal float SpatialBlend;internal IntPtr Hrtf,PeakDelays;}
        [StructLayout(LayoutKind.Sequential)]private struct AudioBuffer {internal int NumChannels,NumSamples;internal IntPtr Data;}
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int ContextCreate(ref ContextSettings settings,out IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate void Release(ref IntPtr handle);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int HrtfCreate(IntPtr context,ref AudioSettings audio,ref HrtfSettings settings,out IntPtr hrtf);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int EffectCreate(IntPtr context,ref AudioSettings audio,ref EffectSettings settings,out IntPtr effect);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate void EffectReset(IntPtr effect);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int EffectApply(IntPtr effect,ref EffectParams settings,ref AudioBuffer input,ref AudioBuffer output);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int EffectTail(IntPtr effect,ref AudioBuffer output);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int BufferAllocate(IntPtr context,int channels,int samples,ref AudioBuffer buffer);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate void BufferFree(IntPtr context,ref AudioBuffer buffer);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true,ExactSpelling=true)]private static extern IntPtr LoadLibraryExW(string path,IntPtr file,uint flags);
        [DllImport("kernel32.dll",CharSet=CharSet.Ansi,ExactSpelling=true)]private static extern IntPtr GetProcAddress(IntPtr module,string name);
        [DllImport("kernel32.dll",ExactSpelling=true)]private static extern bool FreeLibrary(IntPtr module);
    }
}
