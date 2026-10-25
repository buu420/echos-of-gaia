using System;
using System.Threading;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    public static class WorldGeometryPreparationTests
    {
        private static WorldSurface[] Surfaces()
        {
            return new[]{new WorldSurface{Id=17,BlockId=0,MeshIndex=0,TriangleIndex=0,TerrainId=4,NormalY=1,Vertices=new[]{new NavigationPoint(0,0,0),new NavigationPoint(10,0,0),new NavigationPoint(0,0,-10)}}};
        }
        private static TransportRules Rules(){return new TransportRules{LimitHigh=0x1234,LimitLow=0x5678,ControlMode=6,ControlType=1,CurrentTerrainId=4,PreventGroundWaterCrossing=true,Flying=true,Step=0.125f};}
        public static void IncrementalBatchesStayUnavailableUntilLoadingCompletes()
        {
            WaitCallback queued=null;var preparation=new WorldGeometryPreparation(callback=>queued=callback);WorldGeometrySnapshot geometry;
            TestAssert.True(preparation.TryBegin(7));TestAssert.True(preparation.TryAppendOwned(7,Surfaces()));
            TestAssert.False(preparation.IsReady(7));TestAssert.False(preparation.TryRead(7,1,"loading",Rules(),null,out geometry));
            var next=Surfaces();next[0].Id=18;next[0].MeshIndex=1;next[0].TerrainId=8;
            TestAssert.True(preparation.TryAppendOwned(7,next));TestAssert.True(preparation.Complete(7));queued(null);
            TestAssert.True(preparation.TryRead(7,2,"loaded",Rules(),null,out geometry));TestAssert.Equal(2,geometry.Surfaces.Length);
            TestAssert.Equal(17,geometry.Surfaces[0].Id);TestAssert.Equal(18,geometry.Surfaces[1].Id);
            TestAssert.Equal(17,geometry.Probe(new NavigationPoint(1,0,-1),0,null).Id);
            TestAssert.False(preparation.TryAppendOwned(7,Surfaces()));TestAssert.False(preparation.Complete(8));
        }
        public static void CancellationWakesAnIncrementalWorkerWaitingForBlocks()
        {
            WaitCallback queued=null;var preparation=new WorldGeometryPreparation(callback=>queued=callback);
            TestAssert.True(preparation.TryBegin(7));
            var entered=new ManualResetEvent(false);var finished=new ManualResetEvent(false);
            var thread=new Thread(()=>{entered.Set();queued(null);finished.Set();});thread.IsBackground=true;thread.Start();
            TestAssert.True(entered.WaitOne(2000));preparation.Invalidate();
            TestAssert.True(finished.WaitOne(2000));TestAssert.False(preparation.IsReady(7));
            TestAssert.True(preparation.TryBegin(8));TestAssert.True(preparation.TryAppendOwned(8,Surfaces()));TestAssert.True(preparation.Complete(8));queued(null);
            TestAssert.True(preparation.IsReady(8));entered.Close();finished.Close();
        }
        public static void OnlyCompleteRetainedTopologyCanBeReboundWithFreshPrivateState()
        {
            WaitCallback queued=null;var preparation=new WorldGeometryPreparation(callback=>queued=callback);WorldGeometrySnapshot geometry;
            TestAssert.True(preparation.TryStartOwned(7,Surfaces()));
            TestAssert.False(preparation.TryBindReady(8,new WorldGeometrySnapshot(0,null,Surfaces(),null,true)));
            queued(null);var retained=preparation.RetainReady();TestAssert.True(retained!=null);
            preparation.Invalidate();TestAssert.False(preparation.TryBindReady(8,new WorldGeometrySnapshot(0,null,Surfaces(),null,false)));
            TestAssert.True(preparation.TryBindReady(8,retained));
            TestAssert.False(preparation.TryRead(7,1,"old",Rules(),null,out geometry));
            TestAssert.True(preparation.TryRead(8,200,"new",Rules(),new[]{17},out geometry));
            TestAssert.Equal(200L,geometry.Generation);TestAssert.True(Object.ReferenceEquals(retained.Surfaces,geometry.Surfaces));
            geometry.NativeCache[0]=99;geometry.Rules.LimitLow=0;
            TestAssert.True(preparation.TryRead(8,201,"fresh",Rules(),new[]{17},out geometry));
            TestAssert.Equal(17,geometry.NativeCache[0]);TestAssert.Equal(0x5678u,geometry.Rules.LimitLow);
        }
        public static void QueuedTopologySuppliesFreshPrivateRequestState()
        {
            WaitCallback queued=null;var preparation=new WorldGeometryPreparation(callback=>queued=callback);WorldGeometrySnapshot geometry;
            TestAssert.True(preparation.TryStartOwned(7,Surfaces()));
            TestAssert.False(preparation.TryRead(7,100,"first",Rules(),new[]{17},out geometry));
            TestAssert.True(queued!=null);queued(null);
            var rules=Rules();var cache=new[]{17};
            TestAssert.True(preparation.TryRead(7,100,"first",rules,cache,out geometry));
            TestAssert.True(geometry.Complete);TestAssert.Equal(100L,geometry.Generation);TestAssert.Equal("first",geometry.Fingerprint);
            TestAssert.Equal(17,geometry.Find(17).Id);TestAssert.Equal(6,geometry.Rules.ControlMode);TestAssert.True(geometry.Rules.Flying);
            rules.LimitHigh=0;rules.ControlType=0;cache[0]=-1;
            TestAssert.Equal(0x1234u,geometry.Rules.LimitHigh);TestAssert.Equal(1,geometry.Rules.ControlType);TestAssert.Equal(17,geometry.NativeCache[0]);
            geometry.Rules.LimitLow=0;geometry.NativeCache[0]=123;
            rules=new TransportRules{LimitHigh=9,LimitLow=8,ControlMode=2,ControlType=0,CurrentTerrainId=12,Step=0.5f};
            WorldGeometrySnapshot fresh;
            TestAssert.True(preparation.TryRead(7,101,"second",rules,new[]{-1},out fresh));
            TestAssert.Equal(101L,fresh.Generation);TestAssert.Equal("second",fresh.Fingerprint);
            TestAssert.Equal(9u,fresh.Rules.LimitHigh);TestAssert.Equal(8u,fresh.Rules.LimitLow);TestAssert.Equal(2,fresh.Rules.ControlMode);TestAssert.Equal(0,fresh.Rules.ControlType);TestAssert.Equal(12,fresh.Rules.CurrentTerrainId);
            TestAssert.False(fresh.Rules.Flying);TestAssert.False(fresh.Rules.PreventGroundWaterCrossing);TestAssert.Near(0.5f,fresh.Rules.Step,0);
            TestAssert.Equal(-1,fresh.NativeCache[0]);TestAssert.True(Object.ReferenceEquals(geometry.Surfaces,fresh.Surfaces));
            TestAssert.False(preparation.TryRead(8,101,"second",rules,new[]{-1},out fresh));
        }
        public static void ReplacementWaitsForCancelledWorkerToExit()
        {
            WaitCallback queued=null;var preparation=new WorldGeometryPreparation(callback=>queued=callback);WorldGeometrySnapshot geometry;
            TestAssert.True(preparation.TryStartOwned(7,Surfaces()));TestAssert.False(preparation.TryStartOwned(8,Surfaces()));
            preparation.Invalidate();TestAssert.False(preparation.TryStartOwned(8,Surfaces()));
            queued(null);TestAssert.False(preparation.TryRead(7,1,"old",Rules(),null,out geometry));
            TestAssert.True(preparation.TryStartOwned(8,Surfaces()));queued(null);
            TestAssert.False(preparation.TryRead(7,1,"old",Rules(),null,out geometry));TestAssert.True(preparation.TryRead(8,2,"new",Rules(),null,out geometry));
        }
        public static void InvalidationDuringBuildNeverPublishesObsoleteTopology()
        {
            WaitCallback queued=null;WorldGeometryPreparation preparation=null;
            preparation=new WorldGeometryPreparation(callback=>queued=callback,(surfaces,cancelled)=>{
                preparation.Invalidate();TestAssert.True(cancelled());TestAssert.False(preparation.TryStartOwned(8,Surfaces()));
                return new WorldGeometrySnapshot(1,"obsolete",surfaces,Rules(),true);
            });
            WorldGeometrySnapshot geometry;TestAssert.True(preparation.TryStartOwned(7,Surfaces()));queued(null);
            TestAssert.False(preparation.TryRead(7,1,"old",Rules(),null,out geometry));TestAssert.True(preparation.TryStartOwned(8,Surfaces()));
        }
        public static void FailureIsReportedWithoutRepeatedScheduling()
        {
            WaitCallback queued=null;int scheduled=0;var preparation=new WorldGeometryPreparation(callback=>{queued=callback;scheduled++;},(surfaces,cancelled)=>{throw new InvalidOperationException("bad mesh");});
            TestAssert.True(preparation.TryStartOwned(7,Surfaces()));queued(null);WorldGeometrySnapshot geometry;
            TestAssert.Equal("bad mesh",preparation.Failure(7));TestAssert.False(preparation.TryRead(7,1,"old",Rules(),null,out geometry));
            TestAssert.False(preparation.TryStartOwned(7,Surfaces()));TestAssert.Equal(1,scheduled);
            preparation.Invalidate();TestAssert.True(preparation.TryStartOwned(8,Surfaces()));TestAssert.Equal(2,scheduled);
        }
        public static void TopologyConstructionHonorsCancellation()
        {
            bool rejected=false;
            try{new WorldGeometrySnapshot(1,"cancel",Surfaces(),Rules(),true,null,()=>true);}
            catch(OperationCanceledException){rejected=true;}
            TestAssert.True(rejected);
        }
        public static void DefaultPreparationRunsAwayFromRequestingThread()
        {
            var preparation=new WorldGeometryPreparation();WorldGeometrySnapshot geometry=null;
            TestAssert.True(preparation.TryStartOwned(7,Surfaces()));
            var timeout=System.Diagnostics.Stopwatch.StartNew();
            while(!preparation.TryRead(7,1,"thread",Rules(),null,out geometry) && timeout.ElapsedMilliseconds<5000)Thread.Sleep(1);
            TestAssert.True(geometry!=null);TestAssert.Equal(17,geometry.Find(17).Id);
        }
    }
}
