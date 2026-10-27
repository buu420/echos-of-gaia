using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class WorldReachabilityTests
    {
        private static TransportRules Foot(){return new TransportRules{LimitLow=1,Step=0.5f};}
        private static WorldGeometrySnapshot Strip(int barrier=57*4,bool complete=true,TransportRules rules=null)
        {
            var surfaces=new List<WorldSurface>();
            for(int x=0;x<3;x++) {
                var a=TestFixtures.P(x*16,0);var b=TestFixtures.P((x+1)*16,0);var c=TestFixtures.P(x*16,-16);var d=TestFixtures.P((x+1)*16,-16);
                int terrain=x==1?barrier:0;
                surfaces.Add(new WorldSurface{Id=x*2,BlockId=0,NormalY=1,TerrainId=terrain,Vertices=new[]{a,b,c}});
                surfaces.Add(new WorldSurface{Id=x*2+1,BlockId=0,NormalY=1,TerrainId=terrain,Vertices=new[]{b,d,c}});
            }
            return new WorldGeometrySnapshot(1,"strip",surfaces.ToArray(),rules??Foot(),complete);
        }
        private static NavigationContext Context(float x=4){var context=TestFixtures.Context("world:1");context.Position=TestFixtures.P(x,-4);return context;}
        private static NavigationTarget Target(int id,float x,TargetKind kind=TargetKind.WorldLocation)
        {
            return new NavigationTarget{Id=new TargetIdentity(kind,"world:1",id),Generation=1,Category=kind==TargetKind.RegionPoint?NavigationCategory.Regions:kind==TargetKind.Vehicle?NavigationCategory.Vehicles:NavigationCategory.Towns,Label="Target "+id,Position=TestFixtures.P(x,-4),Evidence=VisibilityEvidence.NativeMarker};
        }
        private static CatalogResult Catalog(params NavigationTarget[] targets){var catalog=new CatalogResult();foreach(var target in targets)catalog.Targets.Add(target);return catalog;}
        private static CatalogResult Filter(NavigationContext context,WorldGeometrySnapshot geometry,params NavigationTarget[] targets)
        {
            return WorldTargetCatalog.Filter(context,Catalog(targets),geometry,WorldReachabilityIndex.Build(geometry));
        }
        public static void FootCatalogExcludesOppositeLandAcrossWater()
        {
            var result=Filter(Context(),Strip(),Target(1,4),Target(2,36));
            TestAssert.Equal(1,result.Targets.Count);TestAssert.Equal(1,result.Targets[0].Id.NativeId);
        }
        public static void AreaExitSelectsTheOtherComponentWithoutRebuildingGeometry()
        {
            var geometry=Strip();var index=WorldReachabilityIndex.Build(geometry);var catalog=Catalog(Target(1,4),Target(2,36));
            var result=WorldTargetCatalog.Filter(Context(36),catalog,geometry,index);
            TestAssert.Equal(1,result.Targets.Count);TestAssert.Equal(2,result.Targets[0].Id.NativeId);
        }
        public static void MountainBarrierSplitsTheSameNamedRegion()
        {
            TestAssert.Equal(1,Filter(Context(),Strip(49*4),Target(1,4),Target(2,36)).Targets.Count);
        }
        public static void FlyingIncludesAvailableDestinationsAcrossSeparatedLand()
        {
            var geometry=Strip(rules:new TransportRules{Flying=true,LimitHigh=uint.MaxValue,LimitLow=uint.MaxValue});
            TestAssert.Equal(2,Filter(Context(),geometry,Target(1,4),Target(2,36)).Targets.Count);
        }
        public static void GroundWaterBoundaryStillAppliesWhenBothTerrainsAreInMask()
        {
            var rules=new TransportRules{LimitHigh=uint.MaxValue,LimitLow=uint.MaxValue,PreventGroundWaterCrossing=true};
            TestAssert.Equal(1,Filter(Context(),Strip(rules:rules),Target(1,4),Target(2,36)).Targets.Count);
            rules.PreventGroundWaterCrossing=false;
            TestAssert.Equal(2,Filter(Context(),Strip(rules:rules),Target(1,4),Target(2,36)).Targets.Count);
        }
        public static void ShoreVehicleUsesReachableBoardingApproach()
        {
            var boat=Target(9,20,TargetKind.Vehicle);boat.ApproachCandidates=new[]{boat.Position,TestFixtures.P(15,-4)};
            var result=Filter(Context(),Strip(),boat);
            TestAssert.Equal(1,result.Targets.Count);TestAssert.Near(20,result.Targets[0].Position.X,0);
            TestAssert.Equal(1,result.Targets[0].ApproachCandidates.Length);TestAssert.Near(15,result.Targets[0].ApproachCandidates[0].X,0);
        }
        public static void RegionChoosesReachablePointInsteadOfNearestDisconnectedPoint()
        {
            var region=Target(3,33,TargetKind.RegionPoint);region.ApproachCandidates=new[]{TestFixtures.P(33,-1),TestFixtures.P(1,-15)};
            var context=Context(15);context.Position=TestFixtures.P(15,-1);
            TestAssert.True(WorldCoordinates.Distance(context.Position,region.ApproachCandidates[0])<WorldCoordinates.Distance(context.Position,region.ApproachCandidates[1]));
            var result=Filter(context,Strip(),region);
            TestAssert.Equal(1,result.Targets.Count);TestAssert.Near(1,result.Targets[0].Position.X,0);TestAssert.Equal(1,result.Targets[0].ApproachCandidates.Length);
        }
        public static void IncompleteGeometryAndPendingIndexHaveAnAccessibleReason()
        {
            var result=WorldTargetCatalog.Filter(Context(),Catalog(Target(1,4)),Strip(complete:false),null);
            TestAssert.Equal(0,result.Targets.Count);TestAssert.True(!string.IsNullOrEmpty(result.UnavailableReason));
            var update=new NavigationCoordinator().Tick(Context(),new[]{NavigationCommand.ModifierOpened},result,null);
            TestAssert.True(update.Messages[0].Contains(result.UnavailableReason));
        }
        public static void OldVehicleRulesCannotBeAppliedToNewTransport()
        {
            var foot=Strip();var index=WorldReachabilityIndex.Build(foot);
            var sailing=foot.ForContext(1,"ship",new TransportRules{LimitHigh=1u<<25,LimitLow=0},null);
            TestAssert.False(index.Matches(sailing));TestAssert.Equal(0,WorldTargetCatalog.Filter(Context(),Catalog(Target(1,4)),sailing,index).Targets.Count);
        }
        public static void CacheQueuesOnceAndReusesAfterBattleGenerationChanges()
        {
            var jobs=new Queue<WaitCallback>();var cache=new WorldReachabilityCache(callback=>jobs.Enqueue(callback));var geometry=Strip();WorldReachabilityIndex index;
            for(int i=0;i<1000;i++)TestAssert.False(cache.TryRead(geometry,true,out index));
            TestAssert.Equal(1,jobs.Count);jobs.Dequeue()(null);TestAssert.True(cache.TryRead(geometry,true,out index));
            cache.Cancel();var returned=geometry.ForContext(9,"returned",Foot(),null);
            TestAssert.True(cache.TryRead(returned,true,out index));TestAssert.Equal(0,jobs.Count);
        }
        public static void CancelledWorkerCannotPublishForNewGeometry()
        {
            var jobs=new Queue<WaitCallback>();var cache=new WorldReachabilityCache(callback=>jobs.Enqueue(callback));WorldReachabilityIndex index;
            TestAssert.False(cache.TryRead(Strip(),true,out index));cache.Cancel();jobs.Dequeue()(null);
            var fresh=Strip(0);TestAssert.False(cache.TryRead(fresh,true,out index));jobs.Dequeue()(null);
            TestAssert.True(cache.TryRead(fresh,true,out index));TestAssert.Equal(2,WorldTargetCatalog.Filter(Context(),Catalog(Target(1,4),Target(2,36)),fresh,index).Targets.Count);
        }
        public static void NoWorkerStartsWhileWorldControlIsUnavailable()
        {
            int queued=0;var cache=new WorldReachabilityCache(callback=>queued++);WorldReachabilityIndex index;
            TestAssert.False(cache.TryRead(Strip(),false,out index));TestAssert.Equal(0,queued);
        }
        public static void LargeReachabilityIndexHasSmallAllocationAndCachedReads()
        {
            var geometry=new WorldGeometrySnapshot(1,"large",WorldGeometryPerformanceTests.DenseSurfaces(256),Foot(),true);
            long before=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();var index=WorldReachabilityIndex.Build(geometry);timer.Stop();long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            TestAssert.True(index!=null);Console.WriteLine("World reachability: 131072 faces; build="+timer.ElapsedMilliseconds+" ms; allocated="+allocated+" bytes");
            TestAssert.True(allocated<8*1024*1024);TestAssert.True(timer.ElapsedMilliseconds<1500);
        }
        public static void PlaceIsAnEndpointButCannotJoinTheOppositeGround()
        {
            var result=Filter(Context(),Strip(0xc000),Target(1,20),Target(2,36));
            TestAssert.Equal(1,result.Targets.Count);TestAssert.Equal(1,result.Targets[0].Id.NativeId);
        }
        public static void StartingInsidePlaceCanExitIntoConnectedGround()
        {
            TestAssert.Equal(2,Filter(Context(20),Strip(0xc000),Target(1,4),Target(2,36)).Targets.Count);
        }
        public static void BoatListUsesItsWaterAreaAndCannotOfferInlandTowns()
        {
            var geometry=Strip(rules:new TransportRules{LimitHigh=1u<<25,LimitLow=0});
            var result=Filter(Context(20),geometry,Target(1,4),Target(2,36),Target(3,20,TargetKind.Vehicle));
            TestAssert.Equal(1,result.Targets.Count);TestAssert.Equal(3,result.Targets[0].Id.NativeId);
        }
        public static void MenuReopenedInAnotherAreaSelectsAnEligibleDestination()
        {
            var coordinator=new NavigationCoordinator();var geometry=Strip();
            coordinator.Tick(Context(),new[]{NavigationCommand.ModifierOpened},Filter(Context(),geometry,Target(1,4),Target(2,36)),null);
            var update=coordinator.Tick(Context(36),new[]{NavigationCommand.ModifierOpened},Filter(Context(36),geometry,Target(1,4),Target(2,36)),null);
            TestAssert.True(update.Selected!=null);TestAssert.Equal(2,update.Selected.Id.NativeId);
        }
        public static void TownMarkerOnExcludedBuildingKeepsItsGroundApproach()
        {
            var surfaces=new List<WorldSurface>(Strip(4078).Surfaces);
            // Native Chocobo's Forest marker is on object terrain 4078, which
            // WMBlock raycasting excludes. Its entrance is on surrounding ground.
            surfaces.Insert(0,new WorldSurface{Id=9,BlockId=0,MeshIndex=1,NormalY=1,TerrainId=4078,Vertices=new[]{new NavigationPoint(18,15,0),new NavigationPoint(30,15,0),new NavigationPoint(18,15,-12)}});
            var geometry=new WorldGeometrySnapshot(1,"building marker",surfaces.ToArray(),Foot(),true);
            var town=Target(19,20);var result=Filter(Context(),geometry,town);
            TestAssert.Equal(1,result.Targets.Count);TestAssert.True(result.Targets[0].ApproachCandidates.Length>0);
            TestAssert.Equal(WorldGeometrySnapshot.Cell(town.Position),WorldGeometrySnapshot.Cell(result.Targets[0].ApproachCandidates[0]));
        }
    }
}
