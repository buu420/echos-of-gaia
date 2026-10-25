using System;
using System.IO;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class RegionsTests
    {
        public static void WaterAreaZeroUsesNativeDisplayRule() { TestAssert.False(RegionObservations.ShouldDisplay(0,4)); TestAssert.True(RegionObservations.ShouldDisplay(0,0)); TestAssert.True(RegionObservations.ShouldDisplay(0,37)); TestAssert.True(RegionObservations.ShouldDisplay(1,4)); }
        public static void RegionPointsAreBounded() { var regions=new RegionObservations(null); regions.SwitchSave("save-a"); for(int i=0;i<20;i++)regions.Observe("save-a","world:1",3,"Melda Plains",TestFixtures.P(i*40,-20),new TransportIdentity(TransportKind.Foot,7,0)); TestAssert.Equal(16,regions.BuildTargets(TestFixtures.Context("world:1"))[0].ApproachCandidates.Length); }
        public static void UnknownSaveDoesNotImportRegionKnowledge() { var regions=new RegionObservations(null);regions.SwitchSave("save-a");regions.Observe("save-a","world:1",3,"Melda Plains",TestFixtures.P(40,-20),new TransportIdentity());regions.SwitchSave(null);TestAssert.Equal(0,regions.BuildTargets(TestFixtures.Context("world:1")).Count); }
        public static void RepeatedNearbySamplesDoNotInventCenters() { var regions=new RegionObservations(null);regions.SwitchSave("save-a"); for(int i=0;i<8;i++)regions.Observe("save-a","world:1",3,"Melda Plains",TestFixtures.P(40+i,-20),new TransportIdentity()); TestAssert.Equal(1,regions.BuildTargets(TestFixtures.Context("world:1"))[0].ApproachCandidates.Length); }
        public static void UnsavedRegionDoesNotLeakIntoOldSave()
        {
            string directory=Path.Combine(Path.GetTempPath(),"ff9-regions-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try { var regions=new RegionObservations(directory);regions.SwitchSave("save-a");regions.CommitSave("save-a");regions.Observe("save-a","world:1",3,"Melda Plains",TestFixtures.P(40,-20),new TransportIdentity());regions.CommitSave("save-b");regions.SwitchSave("save-a");TestAssert.Equal(0,regions.BuildTargets(TestFixtures.Context("world:1")).Count);regions.SwitchSave("save-b");TestAssert.Equal(1,regions.BuildTargets(TestFixtures.Context("world:1")).Count); } finally { Directory.Delete(directory,true); }
        }
    }
}
