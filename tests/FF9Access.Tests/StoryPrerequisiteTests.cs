using System;
using System.Collections.Generic;
using System.Linq;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class StoryPrerequisiteTests
    {
        private sealed class Fixture
        {
            internal StoryPrerequisiteField Rule;internal readonly NavigationContext Context;
            internal readonly StoryPrerequisiteEvidence Evidence;internal readonly CatalogResult Catalog=new CatalogResult();
            internal readonly Dictionary<int,int> Globals=new Dictionary<int,int>{{266,0},{401,0},{404,0},{407,0}};
            internal readonly Dictionary<int,int> Map=new Dictionary<int,int>{{38,0},{39,0}};
            internal readonly Dictionary<TargetIdentity,StoryPrerequisiteObject> Objects=new Dictionary<TargetIdentity,StoryPrerequisiteObject>();
            internal Fixture(int field,int component=0)
            {
                Rule=StoryPrerequisiteData.All.Single(r=>r.Field==field);Context=TestFixtures.Context("field:"+field+":SCENE");Context.StoryProgress=Rule.Floor;
                var player=Rule.Players.First();Evidence=new StoryPrerequisiteEvidence{Field=field,PlayerSid=player.Key,PlayerHash=player.Value,DirectorHash=Rule.Director,BgiHash=Rule.Bgi,ActiveTriangle=Array.IndexOf(Rule.Components,component),
                    GlobalByte=i=>Globals.ContainsKey(i)?(int?)Globals[i]:null,MapByte=i=>Map.ContainsKey(i)?(int?)Map[i]:null,Object=id=>Objects.ContainsKey(id)?Objects[id]:null};
            }
            internal NavigationTarget Add(int sid,int uid=-1,TargetKind kind=TargetKind.FieldActor,bool interactive=true)
            {
                var target=new NavigationTarget{Id=new TargetIdentity(kind,Context.LogicalMapKey,uid<0?sid:uid),Generation=Context.Generation,Category=kind==TargetKind.FieldExit?NavigationCategory.Exits:NavigationCategory.Objects,Label="Shown target",IsInteractive=interactive,Position=TestFixtures.P(10,10),ApproachCandidates=new[]{TestFixtures.P(10,10)}};
                Objects[target.Id]=new StoryPrerequisiteObject{Sid=sid,Hash=Rule.Entries[sid],Model=Rule.Kind==StoryPrerequisiteKind.Pinnacle?102:-1};Catalog.Targets.Add(target);return target;
            }
            internal NavigationTarget Read(){TestAssert.True(new StoryPrerequisiteCatalog(StoryPrerequisiteData.All).TryAppend(Context,Catalog,Evidence));return Catalog.Targets.Last(t=>t.Category==NavigationCategory.Story);}
        }
        public static void PinnaclePrerequisitesDoNotSelectFinalRamuhBeforeFragments()
        {var f=new Fixture(1254);f.Add(8);f.Add(9);var selected=f.Read();TestAssert.Equal(9,selected.PhysicalIdentity.NativeId);TestAssert.True(selected.Label.Contains("fragment"));}
        public static void InstantiatedFragmentUidIsNotAssumedToEqualItsScriptSid()
        {var f=new Fixture(1251);f.Add(8,130);TestAssert.Equal(130,f.Read().PhysicalIdentity.NativeId);}
        public static void HiddenOrPreviouslyCollectedFragmentsNeverGainATarget()
        {var f=new Fixture(1251);TestAssert.True(f.Read().PhysicalId==null);f=new Fixture(1251);f.Globals[266]=16;f.Add(8,130);TestAssert.True(f.Read().PhysicalId==null);}
        public static void AllFiveNativeBitsReleaseTheNormalFinalStoryStep()
        {var f=new Fixture(1254);f.Globals[266]=31;TestAssert.False(new StoryPrerequisiteCatalog(StoryPrerequisiteData.All).TryAppend(f.Context,f.Catalog,f.Evidence));}
        public static void UnknownMaskAndModifiedPlayerDoNotAssumePrerequisitesComplete()
        {var f=new Fixture(1254);f.Globals.Remove(266);f.Add(8);TestAssert.True(f.Read().PhysicalId==null);f=new Fixture(1254);f.Globals[266]=31;f.Evidence.PlayerHash="modified";TestAssert.True(f.Read().PhysicalId==null);}
        public static void WrongModelAndScriptCannotBecomeRamuhFragments()
        {var f=new Fixture(1251);var target=f.Add(8,130);f.Objects[target.Id].Model=111;TestAssert.True(f.Read().PhysicalId==null);f=new Fixture(1251);target=f.Add(8,130);f.Objects[target.Id].Hash="modified";TestAssert.True(f.Read().PhysicalId==null);}
        public static void FossilInitialSwitchAndActualReversalUseNativeBits()
        {var f=new Fixture(1419,2);f.Add(5);f.Add(7,7,TargetKind.FieldExit);TestAssert.Equal(5,f.Read().PhysicalIdentity.NativeId);f=new Fixture(1419,2);f.Globals[404]=0xC0;f.Add(5);f.Add(7,7,TargetKind.FieldExit);TestAssert.Equal(5,f.Read().PhysicalIdentity.NativeId);TestAssert.True(f.Catalog.Targets.Last().Label.Contains("Restore"));}
        public static void FossilAfterSwitchOneLeavesItsCompartment()
        {var f=new Fixture(1419,2);f.Globals[404]=0x40;f.Add(5);f.Add(7,7,TargetKind.FieldExit);TestAssert.Equal(7,f.Read().PhysicalIdentity.NativeId);}
        public static void FossilComponentChangesCannotRouteAcrossDisconnectedFloor()
        {var f=new Fixture(1418,1);f.Add(16);f.Add(19,19,TargetKind.FieldExit);TestAssert.Equal(19,f.Read().PhysicalIdentity.NativeId);f=new Fixture(1418,0);f.Add(16);f.Add(19,19,TargetKind.FieldExit);TestAssert.Equal(16,f.Read().PhysicalIdentity.NativeId);}
        public static void FossilOptionalSwitchIsOnlyRestoredWhenItBlocksTheMainRoute()
        {var f=new Fixture(1419,3);f.Globals[404]=0xA0;f.Add(6);f.Add(12,12,TargetKind.FieldExit);TestAssert.Equal(6,f.Read().PhysicalIdentity.NativeId);f=new Fixture(1419,3);f.Globals[404]=0x80;f.Add(6);f.Add(12,12,TargetKind.FieldExit);TestAssert.Equal(12,f.Read().PhysicalIdentity.NativeId);}
        public static void FossilUnknownBgiAndTriangleProvideNoGuessedRoute()
        {var f=new Fixture(1419,2);f.Add(5);f.Evidence.BgiHash="modified";TestAssert.True(f.Read().PhysicalId==null);f=new Fixture(1419,2);f.Add(5);f.Evidence.ActiveTriangle=-1;TestAssert.True(f.Read().PhysicalId==null);}
        public static void FlowerAndFeedStateIsReadWithoutForcingInteraction()
        {var f=new Fixture(1405);f.Add(8,8,TargetKind.FieldActor,false);TestAssert.True(f.Read().PhysicalId==null);TestAssert.True(f.Catalog.Targets.Last().Label.Contains("flower"));f=new Fixture(1405);f.Map[38]=128;f.Add(7,7,TargetKind.FieldActor,false);TestAssert.True(f.Read().Label.Contains("Feed"));}
        public static void FossilFirstRideArrivalLeavesPlatformForHub()
        {foreach(int flower in new[]{0,128}){var f=new Fixture(1411);f.Map[38]=flower;f.Add(7);f.Add(8);f.Add(12,12,TargetKind.FieldExit);TestAssert.Equal(12,f.Read().PhysicalIdentity.NativeId);}var hub=new Fixture(1418,1);hub.Add(19,19,TargetKind.FieldExit);TestAssert.Equal(19,hub.Read().PhysicalIdentity.NativeId);}
        public static void FossilArrivalPlatformsPreferTheirShownHubExit()
        {foreach(int flags in new[]{0,64,128,192,240})foreach(int field in new[]{1411,1414,1416}){var f=new Fixture(field);f.Globals[404]=flags;int exit=field==1416?13:12;f.Add(exit,exit,TargetKind.FieldExit);TestAssert.Equal(exit,f.Read().PhysicalIdentity.NativeId);}}
        public static void FossilPlatformsFollowChangedSwitchesWhenBacktracking()
        {foreach(var example in new[]{new[]{1406,128,11},new[]{1407,0,11},new[]{1408,32,11},new[]{1409,0,12},new[]{1410,0,15},new[]{1412,192,15}}){var f=new Fixture(example[0]);f.Globals[404]=example[1];f.Add(example[2],example[2],TargetKind.FieldExit);TestAssert.Equal(example[2],f.Read().PhysicalIdentity.NativeId);}var boarding=new Fixture(1406);boarding.Globals[404]=192;TestAssert.True(boarding.Read().Label.Contains("flower"));}
        public static void FossilPlatformExitNeedsCurrentGeometryAndSwitchEvidence()
        {var f=new Fixture(1411);f.Add(12,12,TargetKind.FieldExit);f.Evidence.BgiHash="modified";TestAssert.True(f.Read().PhysicalId==null);f=new Fixture(1411);f.Add(12,12,TargetKind.FieldExit);f.Evidence.ActiveTriangle=-1;TestAssert.True(f.Read().PhysicalId==null);f=new Fixture(1411);f.Add(12,12,TargetKind.FieldExit);f.Globals.Remove(404);TestAssert.True(f.Read().PhysicalId==null);f=new Fixture(1411,1);f.Add(12,12,TargetKind.FieldExit);TestAssert.True(f.Read().PhysicalId==null);}
        public static void GulugUsesActualCompletionAndReversibleCounter()
        {var f=new Fixture(2351);f.Add(8);TestAssert.Equal(8,f.Read().PhysicalIdentity.NativeId);f=new Fixture(2351);f.Globals[401]=2;f.Add(8);TestAssert.Equal(8,f.Read().PhysicalIdentity.NativeId);f=new Fixture(2351);f.Globals[407]=128;TestAssert.False(new StoryPrerequisiteCatalog(StoryPrerequisiteData.All).TryAppend(f.Context,f.Catalog,f.Evidence));}
        public static void InvalidGulugCountDoesNotSkipToTheBoss()
        {var f=new Fixture(2351);f.Globals[401]=3;f.Add(8);TestAssert.True(f.Read().PhysicalId==null);}
        public static void CurrentGenerationAndSafeApproachRemainRequired()
        {var f=new Fixture(2351);var t=f.Add(8);t.Generation++;TestAssert.True(f.Read().PhysicalId==null);f=new Fixture(2351);t=f.Add(8);t.ApproachCandidates=new NavigationPoint[0];TestAssert.True(f.Read().PhysicalId==null);}
        public static void NativeBuffersAreNotMutatedByPrerequisiteSelection()
        {var f=new Fixture(1419,2);f.Globals[404]=0x40;f.Add(7,7,TargetKind.FieldExit);f.Read();TestAssert.Equal(0x40,f.Globals[404]);TestAssert.Equal(0,f.Map[38]);}
    }
}
