using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class WorldTargetsTests
    {
        private static NavigationContext World() { return TestFixtures.Context("world:1"); }
        private static WorldTargetObservation Vehicle(int id,int index=9) { return new WorldTargetObservation { Id=new TargetIdentity(TargetKind.Vehicle,"world:1",id), Label=index==8?"Boat":"Hilda Garde III", Position=TestFixtures.P(20, -20), Available=true, Visible=true, NativeIndex=index }; }
        public static void WrapUsesShortDistanceAtBothSeams() { TestAssert.Near(2,WorldCoordinates.Distance(TestFixtures.P(1535,-20),TestFixtures.P(1,-20)),0.001f); TestAssert.Near(2,WorldCoordinates.Distance(TestFixtures.P(20,-1279),TestFixtures.P(20,-1)),0.001f); }
        public static void RegionIdCannotEqualTownIdentity() { TestAssert.False(new TargetIdentity(TargetKind.RegionPoint,"world:1",2).Equals(new TargetIdentity(TargetKind.WorldLocation,"world:1",2))); }
        public static void UndiscoveredProximityDoesNotNameTown() { var town=new WorldTargetObservation { Id=new TargetIdentity(TargetKind.WorldLocation,"world:1",0), Label="Hidden town",Position=TestFixtures.P(1,0) }; TestAssert.Equal(0,new WorldTargetCatalog().Build(World(),new[]{town},null).Targets.Count); }
        public static void BoatAndAirshipBothRemainInCatalog() { TestAssert.Equal(2,new WorldTargetCatalog().Build(World(),new[]{Vehicle(11,8),Vehicle(12,9)},null).Targets.Count); }
        public static void VehiclePositionRefreshes() { var vehicle=Vehicle(12); var catalog=new WorldTargetCatalog(); var before=catalog.Build(World(),new[]{vehicle},null).Find(vehicle.Id); vehicle.Position=TestFixtures.P(300,-200); var after=catalog.Build(World(),new[]{vehicle},null).Find(vehicle.Id); TestAssert.Near(300,after.Position.X,0); TestAssert.Equal(before.Label,after.Label); }
        public static void HiddenUnmarkedVehicleIsExcluded() { var vehicle=Vehicle(12); vehicle.Visible=false; TestAssert.Equal(0,new WorldTargetCatalog().Build(World(),new[]{vehicle},null).Targets.Count); }
        public static void MarkedVehicleRemainsInformative() { var vehicle=Vehicle(12); vehicle.Visible=false;vehicle.Marked=true; TestAssert.Equal(1,new WorldTargetCatalog().Build(World(),new[]{vehicle},null).Targets.Count); }
        public static void CompassMatchesNativeProjection() { TestAssert.Equal("east",WorldCoordinates.Compass(TestFixtures.P(1,0))); TestAssert.Equal("north",WorldCoordinates.Compass(TestFixtures.P(0,1))); }
    }
}
