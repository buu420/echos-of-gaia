using System;
using System.Threading;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class WorldCaptureBoundaryTests
    {
        public static void CaptureFailureCancelsWaitingWorkerWithoutEscapingOrPublishing()
        {
            WaitCallback queued=null;var preparation=new WorldGeometryPreparation(callback=>queued=callback);
            TestAssert.True(preparation.TryBegin(7));
            var thread=new Thread(()=>queued(null));thread.IsBackground=true;thread.Start();
            var deadline=System.Diagnostics.Stopwatch.StartNew();
            while((thread.ThreadState&ThreadState.WaitSleepJoin)==0 && deadline.ElapsedMilliseconds<2000)Thread.Yield();
            TestAssert.True((thread.ThreadState&ThreadState.WaitSleepJoin)!=0);
            int reported=0;var boundary=new WorldCaptureBoundary(preparation,exception=>{reported++;TestAssert.Equal("bad native capture",exception.Message);});
            TestAssert.False(boundary.TryCapture(()=>{throw new InvalidOperationException("bad native capture");}));
            TestAssert.True(boundary.Unavailable);TestAssert.Equal(1,reported);TestAssert.True(thread.Join(2000));
            WorldGeometrySnapshot geometry;TestAssert.False(preparation.TryRead(7,1,"failed",new TransportRules(),null,out geometry));
            bool visited=false;TestAssert.False(boundary.TryCapture(()=>visited=true));TestAssert.False(visited);TestAssert.Equal(1,reported);
            boundary.Reset();TestAssert.True(boundary.TryCapture(()=>visited=true));TestAssert.True(visited);TestAssert.False(boundary.Unavailable);
            TestAssert.True(preparation.TryBegin(8));preparation.Invalidate();queued(null);
        }
        public static void FailingDiagnosticCannotAbortNativeLoading()
        {
            var preparation=new WorldGeometryPreparation(callback=>{});
            TestAssert.True(preparation.TryBegin(7));
            var boundary=new WorldCaptureBoundary(preparation,exception=>{throw new InvalidOperationException("diagnostic failed");});
            TestAssert.False(boundary.TryCapture(()=>{throw new OutOfMemoryException("capture failed");}));
            TestAssert.True(boundary.Unavailable);WorldGeometrySnapshot geometry;
            TestAssert.False(preparation.TryRead(7,1,"failed",new TransportRules(),null,out geometry));
        }
    }
}
