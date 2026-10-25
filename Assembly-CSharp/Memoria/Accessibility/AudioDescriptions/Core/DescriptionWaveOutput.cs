using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace Memoria.Accessibility.AudioDescriptions
{
    public interface IDescriptionWaveApi
    {
        IntPtr Allocate(int bytes);void Free(IntPtr memory);
        uint Open(out IntPtr device);
        uint Prepare(IntPtr device,IntPtr header);uint Write(IntPtr device,IntPtr header);
        uint Reset(IntPtr device);uint Unprepare(IntPtr device,IntPtr header);uint Close(IntPtr device);
        uint Pause(IntPtr device);uint Resume(IntPtr device);
    }
    // A dedicated device and driver-owned buffers. No PlaySound, engine audio,
    // process-wide volume changes or timers that assume a clip has completed.
    public sealed class DescriptionWaveOutput : IDisposable
    {
        private sealed class Buffer {internal IntPtr Data,Header;}
        private readonly IDescriptionWaveApi _api;
        private IntPtr _device;
        private Buffer _current;
        private readonly List<Buffer> _retained=new List<Buffer>();
        private bool _disposed;
        private static readonly int HeaderSize=Marshal.SizeOf(typeof(WaveHeader));
        public static int HeaderFlagsOffset {get{return Marshal.OffsetOf(typeof(WaveHeader),"Flags").ToInt32();}}
        public DescriptionWaveOutput():this(new WinMm()){}
        public DescriptionWaveOutput(IDescriptionWaveApi api){if(api==null)throw new ArgumentNullException("api");_api=api;}
        public bool Playing {get{return _current!=null && (Flags(_current)&1)==0;}}
        public bool TryPlay(byte[] wave)
        {
            if(_disposed)throw new ObjectDisposedException("DescriptionWaveOutput");
            Validate(wave);if(Playing)return false;Reclaim();
            if(_device==IntPtr.Zero){IntPtr opened;Check(_api.Open(out opened),"open");if(opened==IntPtr.Zero)throw new InvalidOperationException("Audio description device is unavailable");_device=opened;}
            var buffer=new Buffer{Data=_api.Allocate(wave.Length-44)};
            try{buffer.Header=_api.Allocate(HeaderSize);}catch{_api.Free(buffer.Data);throw;}
            bool prepared=false;
            try {
                Marshal.Copy(wave,44,buffer.Data,wave.Length-44);
                Marshal.StructureToPtr(new WaveHeader{Data=buffer.Data,Length=(uint)(wave.Length-44)},buffer.Header,false);
                Check(_api.Prepare(_device,buffer.Header),"prepare");prepared=true;
                Check(_api.Write(_device,buffer.Header),"write");
                _current=buffer;return true;
            } catch {
                if(!prepared)Release(buffer);
                else {bool released=false;try{if(_api.Unprepare(_device,buffer.Header)==0){Release(buffer);released=true;}}catch(Exception){}
                    if(!released)_retained.Add(buffer);}
                throw;
            }
        }
        public void Pause(){if(_device!=IntPtr.Zero && Playing)Check(_api.Pause(_device),"pause");}
        public void Resume(){if(_device!=IntPtr.Zero && Playing)Check(_api.Resume(_device),"resume");}
        public void Stop()
        {
            if(_device==IntPtr.Zero || _current==null && _retained.Count==0)return;
            Check(_api.Reset(_device),"reset");Reclaim();ReclaimRetained();
        }
        public void Dispose()
        {
            if(_disposed)return;_disposed=true;if(_device==IntPtr.Zero)return;
            // Failed reset/unprepare leaves every native buffer and the device
            // alive. Freeing uncertain memory could crash the game later.
            Check(_api.Reset(_device),"cleanup reset");Reclaim();ReclaimRetained();
            Check(_api.Close(_device),"close");_device=IntPtr.Zero;
        }
        private static uint Flags(Buffer buffer){return unchecked((uint)Marshal.ReadInt32(buffer.Header,HeaderFlagsOffset));}
        private void Reclaim()
        {
            var buffer=_current;if(buffer==null)return;
            if((Flags(buffer)&2)!=0)Check(_api.Unprepare(_device,buffer.Header),"unprepare");
            _current=null;Release(buffer);
        }
        private void ReclaimRetained()
        {
            for(int i=_retained.Count-1;i>=0;i--){var buffer=_retained[i];Check(_api.Unprepare(_device,buffer.Header),"retained unprepare");_retained.RemoveAt(i);Release(buffer);}
        }
        private void Release(Buffer buffer){_api.Free(buffer.Header);_api.Free(buffer.Data);}
        private static void Check(uint result,string operation){if(result!=0)throw new InvalidOperationException("Audio description waveOut "+operation+" failed: "+result);}
        public static double Duration(byte[] wave){Validate(wave);return (wave.Length-44)/48000d;}
        private static bool Label(byte[] bytes,int offset,string label){for(int i=0;i<label.Length;i++)if(bytes[offset+i]!=label[i])return false;return true;}
        private static void Validate(byte[] wave)
        {
            if(wave==null || wave.Length<46 || (wave.Length&1)!=0 || !BitConverter.IsLittleEndian || !Label(wave,0,"RIFF") || BitConverter.ToInt32(wave,4)!=wave.Length-8 ||
                !Label(wave,8,"WAVEfmt ") || BitConverter.ToInt32(wave,16)!=16 || BitConverter.ToInt16(wave,20)!=1 || BitConverter.ToInt16(wave,22)!=1 ||
                BitConverter.ToInt32(wave,24)!=24000 || BitConverter.ToInt32(wave,28)!=48000 || BitConverter.ToInt16(wave,32)!=2 || BitConverter.ToInt16(wave,34)!=16 ||
                !Label(wave,36,"data") || BitConverter.ToInt32(wave,40)!=wave.Length-44)throw new ArgumentException("Description audio must be canonical 24 kHz mono PCM16 WAV");
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHeader {internal IntPtr Data;internal uint Length,Recorded;internal UIntPtr User;internal uint Flags,Loops;internal IntPtr Next;internal UIntPtr Reserved;}
        private sealed class WinMm : IDescriptionWaveApi
        {
            public IntPtr Allocate(int bytes){return Marshal.AllocHGlobal(bytes);}public void Free(IntPtr memory){Marshal.FreeHGlobal(memory);}
            public uint Open(out IntPtr device){var format=new Format{Tag=1,Channels=1,Rate=24000,BytesPerSecond=48000,Alignment=2,Bits=16};return waveOutOpen(out device,UInt32.MaxValue,ref format,UIntPtr.Zero,UIntPtr.Zero,0);}
            public uint Prepare(IntPtr device,IntPtr header){return waveOutPrepareHeader(device,header,(uint)HeaderSize);}
            public uint Write(IntPtr device,IntPtr header){return waveOutWrite(device,header,(uint)HeaderSize);}
            public uint Reset(IntPtr device){return waveOutReset(device);}public uint Unprepare(IntPtr device,IntPtr header){return waveOutUnprepareHeader(device,header,(uint)HeaderSize);}
            public uint Close(IntPtr device){return waveOutClose(device);}public uint Pause(IntPtr device){return waveOutPause(device);}public uint Resume(IntPtr device){return waveOutRestart(device);}
            [StructLayout(LayoutKind.Sequential,Pack=2)]private struct Format {internal ushort Tag,Channels;internal uint Rate,BytesPerSecond;internal ushort Alignment,Bits,Extra;}
            [DllImport("winmm.dll")]private static extern uint waveOutOpen(out IntPtr handle,uint device,ref Format format,UIntPtr callback,UIntPtr instance,uint flags);
            [DllImport("winmm.dll")]private static extern uint waveOutPrepareHeader(IntPtr device,IntPtr header,uint size);
            [DllImport("winmm.dll")]private static extern uint waveOutWrite(IntPtr device,IntPtr header,uint size);
            [DllImport("winmm.dll")]private static extern uint waveOutReset(IntPtr device);
            [DllImport("winmm.dll")]private static extern uint waveOutUnprepareHeader(IntPtr device,IntPtr header,uint size);
            [DllImport("winmm.dll")]private static extern uint waveOutClose(IntPtr device);
            [DllImport("winmm.dll")]private static extern uint waveOutPause(IntPtr device);
            [DllImport("winmm.dll")]private static extern uint waveOutRestart(IntPtr device);
        }
    }
}
