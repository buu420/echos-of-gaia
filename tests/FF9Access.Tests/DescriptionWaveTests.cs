using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Memoria.Accessibility.AudioDescriptions;
namespace FF9Access.Tests
{
    internal static class DescriptionWaveTests
    {
        private sealed class Driver : IDescriptionWaveApi
        {
            public readonly HashSet<IntPtr> Allocated=new HashSet<IntPtr>();
            public IntPtr Header;public int Writes,Closes,Pauses,Resumes;public uint ResetError,UnprepareError,WriteError;
            public IntPtr Allocate(int bytes){var p=Marshal.AllocHGlobal(bytes);Allocated.Add(p);return p;}
            public void Free(IntPtr p){TestAssert.True(Allocated.Remove(p));if(p==Header)Header=IntPtr.Zero;Marshal.FreeHGlobal(p);}
            public uint Open(out IntPtr device){device=new IntPtr(123);return 0;}
            public uint Prepare(IntPtr device,IntPtr header){Header=header;Marshal.WriteInt32(header,DescriptionWaveOutput.HeaderFlagsOffset,2);return 0;}
            public uint Write(IntPtr device,IntPtr header){Writes++;return WriteError;}
            public uint Reset(IntPtr device){if(ResetError==0 && Header!=IntPtr.Zero)Marshal.WriteInt32(Header,DescriptionWaveOutput.HeaderFlagsOffset,3);return ResetError;}
            public uint Unprepare(IntPtr device,IntPtr header){return UnprepareError;}
            public uint Close(IntPtr device){Closes++;return 0;}
            public uint Pause(IntPtr device){Pauses++;return 0;}
            public uint Resume(IntPtr device){Resumes++;return 0;}
            public void Complete(){Marshal.WriteInt32(Header,DescriptionWaveOutput.HeaderFlagsOffset,3);}
            public void ReleaseRetainedTestMemory(){foreach(var p in Allocated)Marshal.FreeHGlobal(p);Allocated.Clear();}
        }
        private static byte[] Wave()
        {
            var b=new byte[48044];Action<int,int> integer=(o,v)=>Buffer.BlockCopy(BitConverter.GetBytes(v),0,b,o,4);Action<int,short> small=(o,v)=>Buffer.BlockCopy(BitConverter.GetBytes(v),0,b,o,2);
            foreach(var s in new[]{Tuple.Create(0,"RIFF"),Tuple.Create(8,"WAVEfmt "),Tuple.Create(36,"data")})Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(s.Item2),0,b,s.Item1,s.Item2.Length);
            integer(4,b.Length-8);integer(16,16);small(20,1);small(22,1);integer(24,24000);integer(28,48000);small(32,2);small(34,16);integer(40,b.Length-44);return b;
        }
        public static void DriverCompletionOwnsBufferLifetimeAndPermitsTheNextClip()
        {
            var driver=new Driver();using(var output=new DescriptionWaveOutput(driver)){
                TestAssert.True(output.TryPlay(Wave()));TestAssert.True(output.Playing);TestAssert.False(output.TryPlay(Wave()));TestAssert.Equal(1,driver.Writes);
                driver.Complete();TestAssert.False(output.Playing);TestAssert.True(output.TryPlay(Wave()));TestAssert.Equal(2,driver.Allocated.Count);
                output.Stop();TestAssert.Equal(0,driver.Allocated.Count);
            }TestAssert.Equal(1,driver.Closes);
        }
        public static void PauseAndResumeOnlyTouchTheOwnedDevice()
        {
            var driver=new Driver();using(var output=new DescriptionWaveOutput(driver)){
                output.TryPlay(Wave());output.Pause();TestAssert.True(output.Playing);output.Resume();TestAssert.Equal(1,driver.Pauses);TestAssert.Equal(1,driver.Resumes);
            }TestAssert.Equal(0,driver.Allocated.Count);
        }
        public static void ResetFailureRetainsEveryDriverOwnedByte()
        {
            var driver=new Driver();var output=new DescriptionWaveOutput(driver);output.TryPlay(Wave());driver.ResetError=1;
            bool failed=false;try{output.Dispose();}catch(InvalidOperationException){failed=true;}TestAssert.True(failed);TestAssert.Equal(2,driver.Allocated.Count);TestAssert.Equal(0,driver.Closes);
            driver.ReleaseRetainedTestMemory();
        }
        public static void UnprepareFailureDoesNotFreeMemoryOrLoseItForRetry()
        {
            var driver=new Driver();using(var output=new DescriptionWaveOutput(driver)){
                output.TryPlay(Wave());driver.Complete();driver.UnprepareError=33;
                bool failed=false;try{output.TryPlay(Wave());}catch(InvalidOperationException){failed=true;}TestAssert.True(failed);TestAssert.Equal(2,driver.Allocated.Count);
                driver.UnprepareError=0;output.Stop();TestAssert.Equal(0,driver.Allocated.Count);
            }
        }
        public static void RejectedWriteDoesNotLeakAnUnqueuedBuffer()
        {
            var driver=new Driver{WriteError=1};using(var output=new DescriptionWaveOutput(driver)){
                bool failed=false;try{output.TryPlay(Wave());}catch(InvalidOperationException){failed=true;}TestAssert.True(failed);TestAssert.Equal(0,driver.Allocated.Count);
            }
        }
        public static void InvalidPcmIsRejectedBeforeOpeningOrAllocating()
        {
            var driver=new Driver();using(var output=new DescriptionWaveOutput(driver)){
                var wave=Wave();wave[22]=2;bool failed=false;try{output.TryPlay(wave);}catch(ArgumentException){failed=true;}TestAssert.True(failed);TestAssert.Equal(0,driver.Allocated.Count);TestAssert.Equal(0,driver.Writes);
            }
        }
    }
}
