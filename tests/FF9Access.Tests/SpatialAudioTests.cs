using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class SpatialAudioTests
    {
        private sealed class Hrtf : ISpatialHrtf
        {
            public int Calls,Resets,Tails;public NavigationPoint LastDirection;
            public bool Mix(int lane,float[] mono,int offset,int count,NavigationPoint direction,float gain,float[] stereo){Calls++;LastDirection=direction;if(count==0){Tails++;return false;}for(int i=0;i<count;i++){stereo[2*i]+=mono[offset+i]*gain;stereo[2*i+1]+=mono[offset+i]*gain;}return true;}
            public void Reset(int lane){Resets++;}public void Dispose(){}
        }
        private static float[][] Clips(){var a=new float[600];for(int i=0;i<a.Length;i++)a[i]=.25f;return new[]{a,a,a};}
        private static FieldSpatialCue Cue(long pulse=1,float x=1,int id=1){return new FieldSpatialCue(FieldSpatialCueKind.Npc,new TargetIdentity(TargetKind.FieldActor,"field:test",id),new NavigationPoint(),new NavigationPoint(x,0,0),1,pulse);}
        public static void RepeatedSnapshotsUpdatePositionWithoutRestartingTheSample()
        {
            var h=new Hrtf();var m=new SpatialAudioMixer(Clips(),h);var block=new float[1024];
            m.Update(new[]{Cue()},1,0);TestAssert.True(m.Render(block,0));TestAssert.Equal(.25f,block[0]);
            m.Update(new[]{Cue(1,-1)},1,200);TestAssert.True(m.Render(block,200));TestAssert.Equal(-1f,h.LastDirection.X);TestAssert.Equal(.25f,block[174]);TestAssert.Equal(0f,block[176]);
            TestAssert.True(m.Render(block,201));TestAssert.Equal(1,h.Tails);TestAssert.False(m.Render(block,202));
            m.Update(new[]{Cue(1,-1)},1,300);TestAssert.False(m.Render(block,300));m.Update(new[]{Cue(2,-1)},1,3200);TestAssert.True(m.Render(block,3200));
        }
        public static void RemovingAVisibleTargetOrChangingEpochClearsItsQueuedAudio()
        {
            var h=new Hrtf();var m=new SpatialAudioMixer(Clips(),h);var block=new float[1024];
            m.Update(new[]{Cue()},1,0);m.Render(block,0);TestAssert.True(m.Update(new FieldSpatialCue[0],1,1));TestAssert.False(m.Render(block,1));
            m.Update(new[]{Cue()},1,2);TestAssert.True(m.Render(block,2));TestAssert.True(m.Update(new[]{Cue()},2,3));TestAssert.True(m.Render(block,3));
            TestAssert.True(m.Update(new[]{Cue(0,1,2)},2,4));TestAssert.False(m.Render(block,4));
        }
        public static void StalledGameSnapshotsExpireAndNeverRestartTheOldPulse()
        {
            var h=new Hrtf();var m=new SpatialAudioMixer(Clips(),h);var block=new float[1024];
            m.Update(new[]{Cue()},1,0);TestAssert.True(m.Render(block,749));TestAssert.False(m.Render(block,750));TestAssert.True(m.Expired);
            TestAssert.False(m.Render(block,751));m.Update(new[]{Cue()},1,800);TestAssert.False(m.Render(block,800));
            m.Update(new[]{Cue(2)},1,3200);TestAssert.True(m.Render(block,3200));
        }
        public static void InvalidOrMutedDirectionsCannotEnterTheNativeHrtf()
        {
            var h=new Hrtf();var m=new SpatialAudioMixer(Clips(),h);var block=new float[1024];
            var bad=new FieldSpatialCue(FieldSpatialCueKind.Npc,Cue().Identity,new NavigationPoint(),new NavigationPoint(float.NaN,0,0),1,1);
            m.Update(new[]{bad},1,0);TestAssert.False(m.Render(block,0));TestAssert.Equal(0,h.Calls);
            var mute=new FieldSpatialCue(FieldSpatialCueKind.Npc,Cue().Identity,new NavigationPoint(),new NavigationPoint(1,0,0),0,1);
            m.Update(new[]{mute},1,1);TestAssert.False(m.Render(block,1));TestAssert.Equal(0,h.Calls);
        }
        private sealed class WaveApi : ISpatialWaveApi
        {
            public readonly List<IntPtr> Headers=new List<IntPtr>();public int Frees,Resets,Closed;public uint ResetResult,UnprepareResult;
            public IntPtr Allocate(int size){return Marshal.AllocHGlobal(size);}public void Free(IntPtr p){Marshal.FreeHGlobal(p);Frees++;}
            public uint Open(out IntPtr device){device=new IntPtr(1);return 0;}
            public uint Prepare(IntPtr device,IntPtr header){Headers.Add(header);Marshal.WriteInt32(header,SpatialWaveOutput.HeaderFlagsOffset,2);return 0;}
            public uint Write(IntPtr device,IntPtr header){Marshal.WriteInt32(header,SpatialWaveOutput.HeaderFlagsOffset,2);return 0;}
            public uint Reset(IntPtr device){Resets++;if(ResetResult==0)foreach(var h in Headers)Marshal.WriteInt32(h,SpatialWaveOutput.HeaderFlagsOffset,3);return ResetResult;}
            public uint Unprepare(IntPtr device,IntPtr header){if(UnprepareResult==0)Marshal.WriteInt32(header,SpatialWaveOutput.HeaderFlagsOffset,0);return UnprepareResult;}
            public uint Close(IntPtr device){Closed++;return 0;}
        }
        public static void DriverQueueIsBoundedAndOnlyCompletedBuffersCanBeReused()
        {
            var api=new WaveApi();using(var output=new SpatialWaveOutput(api)){
                var block=new short[1024];TestAssert.True(output.TryWrite(block));TestAssert.True(output.TryWrite(block));TestAssert.True(output.TryWrite(block));TestAssert.False(output.TryWrite(block));TestAssert.Equal(3,output.PendingCount);
                Marshal.WriteInt32(api.Headers[0],SpatialWaveOutput.HeaderFlagsOffset,3);TestAssert.Equal(2,output.PendingCount);TestAssert.True(output.TryWrite(block));TestAssert.Equal(3,api.Headers.Count);
                output.Stop();TestAssert.Equal(0,output.PendingCount);
            }
            TestAssert.Equal(6,api.Frees);TestAssert.Equal(1,api.Closed);
        }
        public static void FailedDriverResetCannotFreeMemoryStillOwnedByTheDriver()
        {
            var api=new WaveApi();var output=new SpatialWaveOutput(api);output.TryWrite(new short[1024]);api.ResetResult=1;
            bool failed=false;try{output.Dispose();}catch(InvalidOperationException){failed=true;}
            TestAssert.True(failed);TestAssert.Equal(0,api.Frees);TestAssert.Equal(0,api.Closed);
            api.ResetResult=0;output.Dispose();TestAssert.Equal(2,api.Frees);
        }
        public static void FailedUnprepareCanBeRetriedWithoutFreeingTheUncertainBuffer()
        {
            var api=new WaveApi();var output=new SpatialWaveOutput(api);output.TryWrite(new short[1024]);api.UnprepareResult=33;
            bool failed=false;try{output.Dispose();}catch(InvalidOperationException){failed=true;}
            TestAssert.True(failed);TestAssert.Equal(0,api.Frees);TestAssert.Equal(0,api.Closed);api.UnprepareResult=0;output.Dispose();TestAssert.Equal(2,api.Frees);
        }
        public static void SlowLoadingCannotBlockTheCallerOrReplayACanceledSnapshot()
        {
            using(var entered=new ManualResetEvent(false))using(var release=new ManualResetEvent(false)){
                var api=new WaveApi();var worker=new FieldSpatialAudioWorker(()=>{entered.Set();release.WaitOne();return Clips();},()=>new Hrtf(),()=>new SpatialWaveOutput(api));
                TestAssert.True(entered.WaitOne(3000));var clock=System.Diagnostics.Stopwatch.StartNew();worker.Submit(new[]{Cue()},1);worker.Submit(new FieldSpatialCue[0],2);worker.Dispose();
                TestAssert.True(clock.ElapsedMilliseconds<250);TestAssert.False(worker.Finished);release.Set();
                var deadline=DateTime.UtcNow.AddSeconds(3);while(!worker.Finished && DateTime.UtcNow<deadline)Thread.Sleep(2);
                TestAssert.True(worker.Finished);TestAssert.Equal<string>(null,worker.Failure);TestAssert.Equal(0,api.Headers.Count);
            }
        }
        public static void WorkerLoadingFailureIsReportedWithoutThrowingOnTheGameThread()
        {
            var worker=new FieldSpatialAudioWorker(()=>{throw new InvalidOperationException("missing supplied recording");},()=>new Hrtf(),()=>new SpatialWaveOutput(new WaveApi()));
            worker.Submit(new[]{Cue()},1);var deadline=DateTime.UtcNow.AddSeconds(3);while(!worker.Finished && DateTime.UtcNow<deadline)Thread.Sleep(2);
            TestAssert.True(worker.Finished);TestAssert.True(worker.Failure.Contains("missing supplied recording"));worker.Dispose();
        }
    }
}
