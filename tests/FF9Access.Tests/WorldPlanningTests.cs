using System;
using System.Threading;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class WorldPlanningTests
    {
        private static NavigationContext Context(){var c=TestFixtures.Context("world:0");c.Position=TestFixtures.P(1,-1);return c;}
        private static NavigationTarget Target(){return new NavigationTarget{Id=new TargetIdentity(TargetKind.RegionPoint,"world:0",1),Generation=1,Position=TestFixtures.P(2,-2),ApproachCandidates=new[]{TestFixtures.P(2,-2)}};}
        private static WorldGeometrySnapshot Geometry()
        {
            return new WorldGeometrySnapshot(1,"forms-a",new[]{new WorldSurface{Id=0,TerrainId=0,NormalY=1,Vertices=new[]{TestFixtures.P(0,0),TestFixtures.P(10,0),TestFixtures.P(0,-10)}}},new TransportRules{LimitLow=1,Step=0.05f},true,new[]{0});
        }
        public static void SearchDoesNotRunOnTheRequestingThreadAndSnapshotsMutableInputs()
        {
            WaitCallback pending=null;var worker=new OwnedWorldPlanner(callback=>pending=callback);var c=Context();var t=Target();var g=Geometry();
            TestAssert.True(worker.TryStart(1,c,t,g));WorldPlanResult result;TestAssert.False(worker.TryTake(1,out result));
            c.Position=TestFixtures.P(100,-100);t.Position=TestFixtures.P(90,-90);t.ApproachCandidates[0]=t.Position;g.Rules.LimitLow=0;g.NativeCache[0]=12345;
            pending(null);TestAssert.True(worker.TryTake(1,out result));TestAssert.True(result.Route.Success);TestAssert.Equal(TestFixtures.P(2,-2),result.Route.ApproachPoint);
            TestAssert.Equal(0u,g.Rules.LimitLow);TestAssert.Equal(12345,g.NativeCache[0]);
        }
        public static void StoryWorkerPreservesPhysicalIdentityAndRejectsRebinding()
        {
            WaitCallback pending=null;var worker=new OwnedWorldPlanner(callback=>pending=callback);var c=Context();
            var physical=Target();physical.Id=new TargetIdentity(TargetKind.WorldLocation,c.LogicalMapKey,7);physical.NativeLocationId=7;
            var story=StoryTargetCatalog.Wrap(c,10500,physical,"Travel to the castle");WorldPlanResult result;
            TestAssert.True(worker.TryStart(3,c,story,Geometry()));pending(null);TestAssert.True(worker.TryTake(3,out result));
            TestAssert.True(result.Route.Success);TestAssert.True(result.Route.PhysicalIdentity.Equals(physical.Id));
            TestAssert.True(result.IsCurrent(c,story,"forms-a"));TestAssert.True(GuidanceRouteValidation.Matches(result.Route,c,story));
            story.PhysicalId=new TargetIdentity(TargetKind.WorldLocation,c.LogicalMapKey,8);
            TestAssert.False(result.IsCurrent(c,story,"forms-a"));
        }
        public static void CancellationBoundsWorkAndRejectsSupersededCompletion()
        {
            WaitCallback pending=null;int queued=0;var worker=new OwnedWorldPlanner(callback=>{pending=callback;queued++;});WorldPlanResult result;
            TestAssert.True(worker.TryStart(1,Context(),Target(),Geometry()));worker.Cancel();
            TestAssert.False(worker.TryStart(2,Context(),Target(),Geometry()));TestAssert.Equal(1,queued);
            pending(null);TestAssert.False(worker.TryTake(1,out result));
            TestAssert.True(worker.TryStart(2,Context(),Target(),Geometry()));pending(null);
            TestAssert.False(worker.TryTake(1,out result));TestAssert.True(worker.TryTake(2,out result));TestAssert.True(result.Route.Success);
            TestAssert.False(worker.TryTake(2,out result));
        }
        public static void CompletionRejectsChangedNativeIdentityOriginTargetAndTerrain()
        {
            WaitCallback pending=null;var worker=new OwnedWorldPlanner(callback=>pending=callback);WorldPlanResult result;
            TestAssert.True(worker.TryStart(1,Context(),Target(),Geometry()));pending(null);TestAssert.True(worker.TryTake(1,out result));
            TestAssert.True(result.IsCurrent(Context(),Target(),"forms-a"));
            var c=Context();c.Generation++;TestAssert.False(result.IsCurrent(c,Target(),"forms-a"));
            c=Context();c.SaveIdentity="save-b";TestAssert.False(result.IsCurrent(c,Target(),"forms-a"));
            c=Context();c.LogicalMapKey="world:1";TestAssert.False(result.IsCurrent(c,Target(),"forms-a"));
            c=Context();c.PlayerIdentity++;TestAssert.False(result.IsCurrent(c,Target(),"forms-a"));
            c=Context();c.Transport=new TransportIdentity(TransportKind.Boat,6,8);TestAssert.False(result.IsCurrent(c,Target(),"forms-a"));
            c=Context();c.Position=TestFixtures.P(4,-1);TestAssert.False(result.IsCurrent(c,Target(),"forms-a"));
            c=Context();c.Foreground=false;TestAssert.False(result.IsCurrent(c,Target(),"forms-a"));
            c=Context();c.CanControl=false;TestAssert.False(result.IsCurrent(c,Target(),"forms-a"));
            c=Context();c.Position=new NavigationPoint(float.NaN,0,0);TestAssert.False(result.IsCurrent(c,Target(),"forms-a"));
            var t=Target();t.Id=new TargetIdentity(TargetKind.RegionPoint,"world:0",2);TestAssert.False(result.IsCurrent(Context(),t,"forms-a"));
            t=Target();t.Position=TestFixtures.P(5,-2);TestAssert.False(result.IsCurrent(Context(),t,"forms-a"));
            t=Target();t.ApproachCandidates[0]=TestFixtures.P(6,-2);TestAssert.False(result.IsCurrent(Context(),t,"forms-a"));
            TestAssert.False(result.IsCurrent(Context(),Target(),"forms-b"));
        }
        public static void LongNativeStepSamplingChecksCancellationInsideTheLeg()
        {
            var g=Geometry();var c=Context();var cache=new PrivateWorldCache(g.NativeCache);var start=g.Probe(c.Position,0,cache);WorldSurface end;int checks=0;
            TestAssert.False(g.Segment(c.Position,TestFixtures.P(8,-1),start,cache,WorldGeometrySnapshot.Cell(c.Position),-1,out end,()=>++checks>=4));
            TestAssert.Equal(4,checks);
        }
        public static void CompletionRejectsChangesInOriginTargetAndApproachHeight()
        {
            WaitCallback pending=null;var worker=new OwnedWorldPlanner(callback=>pending=callback);WorldPlanResult result;
            TestAssert.True(worker.TryStart(1,Context(),Target(),Geometry()));pending(null);TestAssert.True(worker.TryTake(1,out result));
            var c=Context();c.Position=new NavigationPoint(c.Position.X,c.Position.Y+1,c.Position.Z);
            TestAssert.False(result.IsCurrent(c,Target(),"forms-a"));
            var t=Target();t.Position=new NavigationPoint(t.Position.X,t.Position.Y+1,t.Position.Z);
            TestAssert.False(result.IsCurrent(Context(),t,"forms-a"));
            t=Target();var point=t.ApproachCandidates[0];t.ApproachCandidates[0]=new NavigationPoint(point.X,point.Y+0.1f,point.Z);
            TestAssert.False(result.IsCurrent(Context(),t,"forms-a"));
        }
        public static void WorkerExceptionPublishesAStableFailedRouteWithoutRepeatedSearches()
        {
            WaitCallback pending=null;var worker=new OwnedWorldPlanner(callback=>pending=callback,(context,target,geometry,cancelled)=>{throw new InvalidOperationException("route worker failed");});WorldPlanResult result;
            var c=Context();var t=Target();t.Category=NavigationCategory.Regions;t.Label="Visited point";var catalog=new CatalogResult();catalog.Targets.Add(t);
            TestAssert.True(worker.TryStart(1,c,t,Geometry()));pending(null);TestAssert.True(worker.TryTake(1,out result));
            TestAssert.Equal("route worker failed",result.Error);TestAssert.True(result.Route!=null);TestAssert.False(result.Route.Success);
            TestAssert.Equal(RouteFailure.GeometryUnavailable,result.Route.Failure);TestAssert.Equal("forms-a",result.Route.GeometryFingerprint);
            foreach(bool automatic in new[]{false,true}) {
                var coordinator=new NavigationCoordinator();coordinator.Tick(c,new[]{NavigationCommand.NextCategory},catalog,null);
                coordinator.Tick(c,new[]{automatic?NavigationCommand.StartAutomatic:NavigationCommand.StartGuidance},catalog,null);
                coordinator.Tick(c,new NavigationCommand[0],catalog,new MovementObservation{Outcome=MovementOutcome.Unavailable});
                for(int frame=0;frame<60;frame++) {
                    var outcome=GuidanceRouteValidation.Check(result.Route,t,t,"forms-a",0.5f,true,true);
                    var update=coordinator.Tick(c,new NavigationCommand[0],catalog,new MovementObservation{Outcome=outcome});
                    TestAssert.Equal(RouteMode.Guiding,update.Session.Mode);TestAssert.Equal(RouteOperation.None,update.RouteOperation);TestAssert.Equal(0,update.Messages.Count);
                }
            }
        }
        public static void DefaultWorkerPublishesCompletionAcrossTheActualThreadPool()
        {
            var worker=new OwnedWorldPlanner();TestAssert.True(worker.TryStart(1,Context(),Target(),Geometry()));WorldPlanResult result=null;
            var deadline=DateTime.UtcNow.AddSeconds(5);
            while(!worker.TryTake(1,out result) && DateTime.UtcNow<deadline)Thread.Sleep(1);
            TestAssert.True(result!=null);TestAssert.True(result.Route.Success);TestAssert.True(result.IsCurrent(Context(),Target(),"forms-a"));
        }
        public static void CrossThreadCancellationRetainsTheSingleWorkerBound()
        {
            using(var entered=new ManualResetEvent(false))using(var release=new ManualResetEvent(false))using(var finished=new ManualResetEvent(false)) {
                var worker=new OwnedWorldPlanner(callback=>ThreadPool.QueueUserWorkItem(delegate(object unused){entered.Set();if(release.WaitOne(5000))callback(null);finished.Set();}));
                TestAssert.True(worker.TryStart(1,Context(),Target(),Geometry()));TestAssert.True(entered.WaitOne(5000));
                worker.Cancel();TestAssert.False(worker.TryStart(2,Context(),Target(),Geometry()));release.Set();TestAssert.True(finished.WaitOne(5000));
                WorldPlanResult result;TestAssert.False(worker.TryTake(1,out result));
            }
        }
    }
}
