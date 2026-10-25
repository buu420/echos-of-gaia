using System;
using System.Runtime.InteropServices;
namespace Memoria.Accessibility.Navigation
{
    public interface ISpatialWaveApi
    {
        IntPtr Allocate(int bytes);void Free(IntPtr memory);uint Open(out IntPtr device);
        uint Prepare(IntPtr device,IntPtr header);uint Write(IntPtr device,IntPtr header);
        uint Reset(IntPtr device);uint Unprepare(IntPtr device,IntPtr header);uint Close(IntPtr device);
    }
    public sealed class SpatialWaveOutput : IDisposable
    {
        public const int SampleRate=24000,FrameSize=512;
        private sealed class Buffer {internal IntPtr Data,Header;internal bool Prepared,Queued;}
        private readonly ISpatialWaveApi _api;private readonly Buffer[] _buffers=new Buffer[3];private IntPtr _device;private bool _disposed;
        private static readonly int HeaderSize=Marshal.SizeOf(typeof(WaveHeader));
        public static int HeaderFlagsOffset {get{return Marshal.OffsetOf(typeof(WaveHeader),"Flags").ToInt32();}}
        [StructLayout(LayoutKind.Sequential)]private struct WaveHeader {internal IntPtr Data;internal uint Length,Recorded;internal UIntPtr User;internal uint Flags,Loops;internal IntPtr Next;internal UIntPtr Reserved;}
        public SpatialWaveOutput():this(new WinMm()){}
        public SpatialWaveOutput(ISpatialWaveApi api){if(api==null)throw new ArgumentNullException("api");_api=api;}
        public int PendingCount {get{int count=0;foreach(var b in _buffers)if(b!=null && b.Queued){if((Marshal.ReadInt32(b.Header,HeaderFlagsOffset)&1)!=0)b.Queued=false;else count++;}return count;}}
        public bool TryWrite(short[] stereo)
        {
            if(_disposed)throw new ObjectDisposedException("SpatialWaveOutput");if(stereo==null || stereo.Length!=FrameSize*2)throw new ArgumentException("Expected a 512-frame stereo PCM block");
            if(PendingCount>=3)return false;
            if(_device==IntPtr.Zero){Check(_api.Open(out _device),"open");if(_device==IntPtr.Zero)throw new InvalidOperationException("Spatial sound device unavailable");}
            int index=0;while(_buffers[index]!=null && _buffers[index].Queued)index++;
            var b=_buffers[index];
            if(b==null){b=new Buffer();_buffers[index]=b;b.Data=_api.Allocate(FrameSize*4);b.Header=_api.Allocate(HeaderSize);Marshal.StructureToPtr(new WaveHeader{Data=b.Data,Length=FrameSize*4},b.Header,false);Check(_api.Prepare(_device,b.Header),"prepare");b.Prepared=true;}
            Marshal.Copy(stereo,0,b.Data,stereo.Length);Check(_api.Write(_device,b.Header),"write");b.Queued=true;return true;
        }
        public void Stop(){if(_device==IntPtr.Zero)return;Check(_api.Reset(_device),"reset");foreach(var b in _buffers)if(b!=null)b.Queued=false;}
        public void Dispose()
        {
            if(_disposed)return;Stop();
            for(int i=0;i<_buffers.Length;i++){var b=_buffers[i];if(b==null)continue;if(b.Prepared)Check(_api.Unprepare(_device,b.Header),"unprepare");if(b.Header!=IntPtr.Zero)_api.Free(b.Header);if(b.Data!=IntPtr.Zero)_api.Free(b.Data);_buffers[i]=null;}
            if(_device!=IntPtr.Zero){Check(_api.Close(_device),"close");_device=IntPtr.Zero;}_disposed=true;
        }
        private static void Check(uint result,string operation){if(result!=0)throw new InvalidOperationException("Spatial waveOut "+operation+" failed: "+result);}
        private sealed class WinMm : ISpatialWaveApi
        {
            public IntPtr Allocate(int bytes){return Marshal.AllocHGlobal(bytes);}public void Free(IntPtr memory){Marshal.FreeHGlobal(memory);}
            public uint Open(out IntPtr device){var format=new Format{Tag=1,Channels=2,Rate=SampleRate,BytesPerSecond=SampleRate*4,Alignment=4,Bits=16};return waveOutOpen(out device,UInt32.MaxValue,ref format,UIntPtr.Zero,UIntPtr.Zero,0);}
            public uint Prepare(IntPtr device,IntPtr header){return waveOutPrepareHeader(device,header,(uint)HeaderSize);}public uint Write(IntPtr device,IntPtr header){return waveOutWrite(device,header,(uint)HeaderSize);}
            public uint Reset(IntPtr device){return waveOutReset(device);}public uint Unprepare(IntPtr device,IntPtr header){return waveOutUnprepareHeader(device,header,(uint)HeaderSize);}public uint Close(IntPtr device){return waveOutClose(device);}
            [StructLayout(LayoutKind.Sequential,Pack=2)]private struct Format {internal ushort Tag,Channels;internal uint Rate,BytesPerSecond;internal ushort Alignment,Bits,Extra;}
            [DllImport("winmm.dll")]private static extern uint waveOutOpen(out IntPtr handle,uint device,ref Format format,UIntPtr callback,UIntPtr instance,uint flags);
            [DllImport("winmm.dll")]private static extern uint waveOutPrepareHeader(IntPtr device,IntPtr header,uint size);
            [DllImport("winmm.dll")]private static extern uint waveOutWrite(IntPtr device,IntPtr header,uint size);
            [DllImport("winmm.dll")]private static extern uint waveOutReset(IntPtr device);
            [DllImport("winmm.dll")]private static extern uint waveOutUnprepareHeader(IntPtr device,IntPtr header,uint size);
            [DllImport("winmm.dll")]private static extern uint waveOutClose(IntPtr device);
        }
    }
}
