using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        public static void IceCavernUsesTheReachableEntranceForTownsAndStory()
        {
            // Both native pins are on unwalkable terrain, beside separate
            // event surfaces reached from opposite sides of the cavern.
            var lower = Target(3, 0);
            lower.NativeLocationId = 3;
            lower.Label = "Ice Cavern";
            lower.Position = new NavigationPoint(306501f / 256, 0, -204700f / 256);
            lower.WorldEntranceTags = new[] { 0x9895 };
            var upper = Target(11, 0);
            upper.NativeLocationId = 11;
            upper.Label = lower.Label;
            upper.Position = new NavigationPoint(297056f / 256, 0, -215824f / 256);
            upper.WorldEntranceTags = new[] { 0x9a91 };
            WorldGeometrySnapshot geometry = IceCavernGeometry(0x4000);
            var story = new StoryTargetCatalog(new[] { new StoryStage {
                Milestone = 2500, Goal = "Ice Cavern", GoalFields = new[] { 300 }, WorldLocations = new[] { 3, 11 }
            } }, null);
            var context = Context();
            context.Transport = new TransportIdentity(TransportKind.Foot, context.PlayerIdentity, 0);
            context.StoryProgress = 2411;
            var observations = new[] { lower, upper }.Select(target => new WorldTargetObservation
            {
                Id = target.Id, NativeLocationId = target.NativeLocationId, Label = target.Label,
                Position = target.Position, WorldEntranceTags = target.WorldEntranceTags, Available = true
            }).ToArray();
            foreach (var reachable in new[] { lower, upper })
            {
                bool lowerSide = reachable == lower;
                context.Position = lowerSide ? new NavigationPoint(1198, 3, -790) : new NavigationPoint(1158, 28, -836);
                var unvisited = new WorldTargetCatalog().Build(context, observations, null);
                var result = WorldTargetCatalog.Filter(context, unvisited, geometry, WorldReachabilityIndex.Build(geometry));
                TestAssert.Equal(1, result.Targets.Count);
                var town = result.Targets[0];
                TestAssert.Equal(reachable.Id, town.Id);
                story.Append(context, result, -1);
                var objective = result.Targets[1];
                TestAssert.Equal(town.Id, objective.PhysicalIdentity);
                TestAssert.Equal(1, objective.ApproachCandidates.Length);
                foreach (var target in new[] { town, objective })
                {
                    var route = new WorldRoutePlanner().Plan(context, target, geometry);
                    TestAssert.True(route.Success);
                    var arrival = geometry.Probe(route.ApproachPoint, 0, null);
                    TestAssert.Equal(lowerSide ? 0x4000 : 0x4228, arrival.TerrainId);
                    TestAssert.Equal(WorldGeometrySnapshot.Cell(reachable.Position), WorldGeometrySnapshot.Cell(route.ApproachPoint));
                }
            }

            // A different terrain or event at the known approach must not be
            // presented as the Ice Cavern entrance.
            context.Position = new NavigationPoint(1198, 3, -790);
            TestAssert.Equal(0, Filter(context, IceCavernGeometry(0x8000), lower, upper).Targets.Count);
        }

        public static void ReachableMapPinDoesNotOverrideTheNativeEntrance()
        {
            var geometry = Strip(0x4000);
            var town = Target(13, 4);
            town.WorldEntranceTags = new[] { 0x8001 };
            // The pin is on reachable ground. An eligible native event still
            // takes precedence when its panel surrounds the stopping point.
            var result = Filter(Context(), geometry, town);
            TestAssert.Equal(1, result.Targets.Count);
            var actual = result.Targets[0];
            TestAssert.True(actual.UseApprovedApproaches);
            var route = new WorldRoutePlanner().Plan(Context(), actual, geometry);
            TestAssert.True(route.Success);
            var arrived = geometry.Probe(route.ApproachPoint, 0, null);
            TestAssert.Equal(0x4000, arrived.TerrainId);
            // Both sides can reach this panel. It still cannot connect them.
            TestAssert.Equal(1, Filter(Context(36), geometry, town).Targets.Count);
            town.WorldEntranceTags = Array.Empty<int>();
            TestAssert.Equal(0, Filter(Context(), geometry, town).Targets.Count);
        }

        public static void MarkedCliffPinKeepsReachableGroundWhenEntranceDataIsUnavailable()
        {
            var town = Target(3, 0);
            town.Position = new NavigationPoint(306501f / 256, 0, -204700f / 256);
            var context = Context();
            context.Position = new NavigationPoint(1198, 3, -790);
            var geometry = IceCavernGeometry(0x4000);
            var result = Filter(context, geometry, town);
            TestAssert.Equal(1, result.Targets.Count);
            var target = result.Targets[0];
            TestAssert.True(target.ApproachCandidates.Length > 0);
            TestAssert.True(new WorldRoutePlanner().Plan(context, target, geometry).Success);
            foreach (var point in target.ApproachCandidates)
                TestAssert.Equal(WorldGeometrySnapshot.Cell(town.Position), WorldGeometrySnapshot.Cell(point));

            // Known closed events still take precedence over a marked pin.
            town.WorldEntranceTags = Array.Empty<int>();
            TestAssert.Equal(0, Filter(context, geometry, town).Targets.Count);
            town.WorldEntranceTags = null;
            town.Id = new TargetIdentity(TargetKind.Vehicle, town.Id.MapKey, town.Id.NativeId);
            TestAssert.Equal(0, Filter(context, geometry, town).Targets.Count);
        }


        public static void SameMeshForestHeightCanBeCrossedInBothDirections()
        {
            var low = new WorldSurface
            {
                Id = 0, BlockId = 0, NormalY = 1, TerrainId = 0,
                Vertices = new[] { new NavigationPoint(2, 0, -8), new NavigationPoint(12, 0, -2), new NavigationPoint(12, 0, -14) }
            };
            const float delta = 500f / 256;
            var forest = new WorldSurface
            {
                Id = 1, BlockId = 0, NormalY = 1, TerrainId = 38 << 2,
                Vertices = new[] { new NavigationPoint(22, delta, -8), new NavigationPoint(12, delta, -14), new NavigationPoint(12, delta, -2) }
            };
            var rules = new TransportRules { LimitLow = 1, LimitHigh = 1 << 6 };
            var geometry = new WorldGeometrySnapshot(1, "forest", new[] { low, forest }, rules, true);
            foreach (var from in new[] { low, forest })
            {
                var context = Context();
                context.Position = from.Center;
                var target = Target(8, 4);
                target.Position = (from.Id == low.Id ? forest : low).Center;
                TestAssert.Equal(1, Filter(context, geometry, target).Targets.Count);
                TestAssert.True(new WorldRoutePlanner().Plan(context, target, geometry).Success);
            }

            // Shared X/Z alone never connects separate registered native meshes.
            forest.MeshIndex = 2;
            geometry = new WorldGeometrySnapshot(1, "overlapping meshes", new[] { low, forest }, rules, true);
            var origin = Context();
            origin.Position = low.Center;
            var other = Target(8, 4);
            other.Position = forest.Center;
            TestAssert.Equal(0, Filter(origin, geometry, other).Targets.Count);
        }

        public static void WorldEventPlacesFollowBranchAndLeaveNativeMapBytesAlone()
        {
            var no = new ScriptInstruction(30, 31, 4);
            var branch = new ScriptInstruction(8, 11, 2, 19);
            var test = new ScriptInstruction(0, 8, 5, new byte[] { 0xdc, 0, 0x7d, 0xea, 3, 27, 0x7f });
            var assign = new ScriptInstruction(11, 19, 5, new byte[] { 0xd5, 39, 0x7d, 14, 0, 44, 0x7f });
            var request = new ScriptInstruction(19, 30, 16) { Arguments = new int?[] { 6, 1, 11 } };
            var program = new[] { test, branch, assign, request, no };
            var global = new byte[] { 0xe9, 3 };
            var map = new byte[80];
            TestAssert.Equal(0, WorldEntranceScripts.PresentedPlace(program, new ScriptSnapshot(global, map, null), 16));
            global[0]++;
            TestAssert.Equal(14, WorldEntranceScripts.PresentedPlace(program, new ScriptSnapshot(global, map, null), 16));
            TestAssert.Equal((byte)0, map[39]);
            request.Arguments[2] = 12;
            TestAssert.False(WorldEntranceScripts.PresentedPlace(program, new ScriptSnapshot(global, map, null), 16).HasValue);
        }

        private static WorldGeometrySnapshot IceCavernGeometry(int lowerEvent)
        {
            var faces = new List<WorldSurface>();
            void Strip(float x, float y, float from, float to, int block, int terrain)
            {
                var a = new NavigationPoint(x, y, from);
                var b = new NavigationPoint(x + 4, y, from);
                var c = new NavigationPoint(x, y, to);
                var d = new NavigationPoint(x + 4, y, to);
                faces.Add(new WorldSurface { Id = faces.Count, BlockId = block, NormalY = 1, TerrainId = terrain, Vertices = new[] { a, b, c } });
                faces.Add(new WorldSurface { Id = faces.Count, BlockId = block, NormalY = 1, TerrainId = terrain, Vertices = new[] { b, d, c } });
            }
            Strip(1196, 3, -786, -795, 306, 0);
            Strip(1196, 3, -795, -798.5f, 306, lowerEvent);
            Strip(1196, 3, -798.5f, -800, 306, 236);
            Strip(1156.5f, 28, -833, -841, 330, 0);
            Strip(1156.5f, 28, -841, -846.5f, 330, 0x4228);
            // The pin is on a cliff beside the apron, at the same height.
            Strip(1160.3f, 28, -841, -846.5f, 330, 748);
            var rules = new TransportRules { LimitHigh = 0x0010667F, LimitLow = 0xD8FF3CFF, Step = 112f / 256 };
            return new WorldGeometrySnapshot(1, "Ice Cavern entrances", faces.ToArray(), rules, true);
        }
    }
}
