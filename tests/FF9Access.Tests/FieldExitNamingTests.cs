using System;
using System.Collections.Generic;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    // Exit names come only from the current native destination proof of each approach, named with the
    // destination's native location text and a stable physical area number. No ids, no Exit letters.
    internal static class FieldExitNamingTests
    {
        private static FieldTargetObservation Exit(int uid,string side,params NavigationPoint[] approaches)
        {
            return new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldExit,"field:100",uid),NativePresentationIdentity=1000+uid,Position=approaches.Length==0?TestFixtures.P(0,0):approaches[0],VisibleLabel=side,ContainsJump=true,
                Exit=new ExitPresentation{Visible=true,ApproachEnabled=approaches.Length!=0,Approach=approaches.Length==0?TestFixtures.P(0,0):approaches[0],ApproachCandidates=approaches}};
        }
        private static FieldExitRoute Route(params object[] pairs)
        {
            var route=new FieldExitRoute();
            for(int i=0;i<pairs.Length;i+=3){int field=(int)pairs[i];route.Approaches[field]=(NavigationPoint[])pairs[i+1];route.Names[field]=(string)pairs[i+2];}
            return route;
        }
        private static CatalogResult Build(FieldTargetCatalog catalog,Dictionary<FieldTargetObservation,FieldExitRoute> routes,params FieldTargetObservation[] exits)
        {
            var context=TestFixtures.Context();var published=new Dictionary<TargetIdentity,FieldExitRoute>();
            foreach(var pair in routes){pair.Value.Presentation=pair.Key.NativePresentationIdentity;published[pair.Key.Id]=pair.Value;}
            FieldExitRoutes.Publish(context.LogicalMapKey,context.Generation,published);
            return catalog.Build(context,exits,new ObservedIdentityStore(null));
        }
        private static Dictionary<FieldTargetObservation,FieldExitRoute> Routes(params object[] pairs)
        {var result=new Dictionary<FieldTargetObservation,FieldExitRoute>();for(int i=0;i<pairs.Length;i+=2)result[(FieldTargetObservation)pairs[i]]=(FieldExitRoute)pairs[i+1];return result;}
        private static NavigationTarget Only(CatalogResult result){TestAssert.Equal(1,result.Targets.Count);return result.Targets[0];}
        public static void ProvenExitIsNamedForItsDestinationWithoutLetters()
        {
            var a=TestFixtures.P(10,0);var exit=Exit(7,"Upper-left exit",a);
            var target=Only(Build(new FieldTargetCatalog(),Routes(exit,Route(220,new[]{a},"Evil Forest, Trail, Area 1")),exit));
            TestAssert.Equal("Exit to Evil Forest, Trail, Area 1",target.Label);TestAssert.Equal(exit.Id,target.Id);
        }
        public static void UnknownOrUnavailableDestinationIsNeverInvented()
        {
            var exit=Exit(7,"Upper-left exit",TestFixtures.P(10,0));
            TestAssert.Equal("Upper-left exit",Only(Build(new FieldTargetCatalog(),Routes(),exit)).Label);
            // A proof without a readable native name, or a disabled approach, keeps the presented side only.
            var unnamed=Exit(8,"Left exit",TestFixtures.P(1,0));
            TestAssert.Equal("Left exit",Only(Build(new FieldTargetCatalog(),Routes(unnamed,Route(300,new[]{TestFixtures.P(1,0)},null)),unnamed)).Label);
            var closed=Exit(9,"Right exit");closed.Exit.Availability="current entrance trigger unavailable";
            var target=Only(Build(new FieldTargetCatalog(),Routes(closed,Route(300,new NavigationPoint[0],"Dali, Inn")),closed));
            TestAssert.Equal("Right exit",target.Label);TestAssert.True(target.Availability.Contains("unavailable"));
            // A route recorded for another presentation of the same uid is stale.
            var stale=Exit(10,"Exit",TestFixtures.P(2,0));var route=Route(300,new[]{TestFixtures.P(2,0)},"Dali, Inn");
            var context=TestFixtures.Context();route.Presentation=-5;FieldExitRoutes.Publish(context.LogicalMapKey,context.Generation,new Dictionary<TargetIdentity,FieldExitRoute>{{stale.Id,route}});
            TestAssert.Equal("Exit",Only(new FieldTargetCatalog().Build(context,new[]{stale},null)).Label);
        }
        public static void PositionDependentExitSplitsIntoDestinationBoundTargets()
        {
            var upper=TestFixtures.P(0,50);var lower=TestFixtures.P(0,-50);var unknown=TestFixtures.P(30,0);
            var exit=Exit(9,"Exit",upper,lower,unknown);exit.NextFields=new[]{154,155};
            exit.DestinationApproaches=new Dictionary<int,NavigationPoint[]>{{154,new[]{upper}},{155,new[]{lower}}};
            var result=Build(new FieldTargetCatalog(),Routes(exit,Route(154,new[]{upper},"A. Castle, Library, Area 1",155,new[]{lower},"A. Castle, Guest Room")),exit);
            TestAssert.Equal(2,result.Targets.Count);
            NavigationTarget library=null,guest=null;foreach(var t in result.Targets){if(t.Label=="Exit to A. Castle, Library, Area 1")library=t;if(t.Label=="Exit to A. Castle, Guest Room")guest=t;}
            TestAssert.True(library!=null && guest!=null);
            TestAssert.Equal(1,library.ApproachCandidates.Length);TestAssert.Equal(upper,library.ApproachCandidates[0]);TestAssert.Equal(upper,library.Position);
            TestAssert.Equal(1,guest.ApproachCandidates.Length);TestAssert.Equal(lower,guest.ApproachCandidates[0]);
            // Both remain the same native region for triggers, exit observation and Story wrapping.
            TestAssert.Equal(exit.Id,library.PhysicalIdentity);TestAssert.Equal(exit.Id,guest.PhysicalIdentity);TestAssert.False(library.Id.Equals(guest.Id));
            TestAssert.Equal(exit.Id.NativeId,library.Id.NativeId);TestAssert.Equal(155,library.Id.Variant);TestAssert.Equal(156,guest.Id.Variant);
            TestAssert.Equal(1,library.NextFields.Length);TestAssert.Equal(154,library.NextFields[0]);TestAssert.Equal(155,guest.NextFields[0]);
            TestAssert.Equal(1,library.DestinationApproaches.Count);TestAssert.True(library.DestinationApproaches.ContainsKey(154));
            // Split identities are stable from frame to frame.
            var again=Build(new FieldTargetCatalog(),Routes(exit,Route(154,new[]{upper},"A. Castle, Library, Area 1",155,new[]{lower},"A. Castle, Guest Room")),exit);
            foreach(var t in again.Targets)TestAssert.True(t.Id.Equals(library.Id) || t.Id.Equals(guest.Id));
        }
        public static void SingleProvenDestinationKeepsOnlyItsProvenApproaches()
        {
            var good=TestFixtures.P(0,10);var unproven=TestFixtures.P(0,90);var exit=Exit(4,"Upper exit",good,unproven);
            var target=Only(Build(new FieldTargetCatalog(),Routes(exit,Route(105,new[]{good},"Alexandria, Alley, Area 1")),exit));
            TestAssert.Equal("Exit to Alexandria, Alley, Area 1",target.Label);TestAssert.Equal(1,target.ApproachCandidates.Length);TestAssert.Equal(good,target.ApproachCandidates[0]);
            // Two native fields of one physical area (same name) stay one target.
            var other=TestFixtures.P(5,10);var alias=Exit(5,"Upper exit",good,other);
            TestAssert.Equal("Exit to Alexandria, Alley, Area 1",Only(Build(new FieldTargetCatalog(),Routes(alias,Route(105,new[]{good},"Alexandria, Alley, Area 1",1800,new[]{other},"Alexandria, Alley, Area 1")),alias)).Label);
        }
        public static void DuplicateDoorwaysToOneRoomAreDistinguishedByPositionThenDoorway()
        {
            var a=TestFixtures.P(-50,0);var b=TestFixtures.P(50,0);var c=TestFixtures.P(60,0);
            var left=Exit(3,"Left exit",a);var right=Exit(6,"Right exit",b);var alsoRight=Exit(8,"Right exit",c);
            var result=Build(new FieldTargetCatalog(),Routes(left,Route(200,new[]{a},"Dali, Inn"),right,Route(200,new[]{b},"Dali, Inn"),alsoRight,Route(200,new[]{c},"Dali, Inn")),left,right,alsoRight);
            TestAssert.Equal("Exit to Dali, Inn, left",result.Find(left.Id).Label);
            TestAssert.Equal("Exit to Dali, Inn, right, doorway 1",result.Find(right.Id).Label);
            TestAssert.Equal("Exit to Dali, Inn, right, doorway 2",result.Find(alsoRight.Id).Label);
            // Unknown destinations at one side are told apart the same way, never with letters.
            var u1=Exit(11,"Exit",TestFixtures.P(1,1));var u2=Exit(12,"Exit",TestFixtures.P(2,2));
            var unknown=Build(new FieldTargetCatalog(),Routes(),u1,u2);
            TestAssert.Equal("Exit, doorway 1",unknown.Find(u1.Id).Label);TestAssert.Equal("Exit, doorway 2",unknown.Find(u2.Id).Label);
        }
        public static void LanguageRefreshRenamesTheSameExit()
        {
            var a=TestFixtures.P(10,0);var exit=Exit(7,"Exit",a);var catalog=new FieldTargetCatalog();
            TestAssert.Equal("Exit to Evil Forest, Trail, Area 2",Only(Build(catalog,Routes(exit,Route(221,new[]{a},"Evil Forest, Trail, Area 2")),exit)).Label);
            TestAssert.Equal("Exit to Forêt Maudite, Sentier, Area 2",Only(Build(catalog,Routes(exit,Route(221,new[]{a},"Forêt Maudite, Sentier, Area 2")),exit)).Label);
        }
        public static void ProvenNameOutranksAnOlderObservedJourneyLabel()
        {
            var a=TestFixtures.P(10,0);var exit=Exit(7,"Exit",a);exit.LabelIdentity="scope";
            var identities=new ObservedIdentityStore(null);identities.Record(exit.Id,"Last led to Dali, Inn","scope");
            var context=TestFixtures.Context();var route=Route(250,new[]{a},"Dali, Windmill");route.Presentation=exit.NativePresentationIdentity;
            FieldExitRoutes.Publish(context.LogicalMapKey,context.Generation,new Dictionary<TargetIdentity,FieldExitRoute>{{exit.Id,route}});
            TestAssert.Equal("Exit to Dali, Windmill",Only(new FieldTargetCatalog().Build(context,new[]{exit},identities)).Label);
            FieldExitRoutes.Publish(context.LogicalMapKey,context.Generation,new Dictionary<TargetIdentity,FieldExitRoute>());
            TestAssert.Equal("Last led to Dali, Inn",Only(new FieldTargetCatalog().Build(context,new[]{exit},identities)).Label);
        }
        public static void AnExitBackIntoTheCurrentAreaSaysSo()
        {
            var a=TestFixtures.P(10,0);var exit=Exit(7,"Left exit",a);var context=TestFixtures.Context();context.CurrentLocation="Iifa Tree, Tree Roots, Area 1";
            var route=Route(1651,new[]{a},"Iifa Tree, Tree Roots, Area 1");route.Presentation=exit.NativePresentationIdentity;
            FieldExitRoutes.Publish(context.LogicalMapKey,context.Generation,new Dictionary<TargetIdentity,FieldExitRoute>{{exit.Id,route}});
            TestAssert.Equal("Exit to Iifa Tree, Tree Roots, Area 1, this same area",Only(new FieldTargetCatalog().Build(context,new[]{exit},null)).Label);
        }
        public static void APromptedDestinationSaysItAsksFirst()
        {
            var a=TestFixtures.P(10,0);var exit=Exit(7,"Exit",a);var route=Route(2715,new[]{a},"Pand., Event");route.Prompted.Add(2715);
            TestAssert.Equal("Exit to Pand., Event, asks first",Only(Build(new FieldTargetCatalog(),Routes(exit,route),exit)).Label);
        }
        public static void AMenuExitIsNamedForItsServiceNeverForAHiddenChoice()
        {
            // An air cab stop: the shown menu decides the district; no district is named before it is offered.
            var a=TestFixtures.P(10,0);var exit=Exit(13,"Left exit",a);var route=new FieldExitRoute{Function="Air cab"};route.Menu=new[]{a};
            var target=Only(Build(new FieldTargetCatalog(),Routes(exit,route),exit));
            TestAssert.Equal("Air cab",target.Label);TestAssert.True(target.Availability.Contains("menu"));TestAssert.Equal(1,target.ApproachCandidates.Length);
            // Without a reviewed function the menu is still described, not a screen side.
            var plain=Exit(14,"Left exit",a);var unreviewed=new FieldExitRoute();unreviewed.Menu=new[]{a};
            TestAssert.Equal("Exit with a destination menu",Only(Build(new FieldTargetCatalog(),Routes(plain,unreviewed),plain)).Label);
        }
        public static void ACurrentServiceMenuCarriesItsOfferedPlacesForStory()
        {
            // Lindblum B.D. Station air cab: the menu offers the Theater and Industrial District stations now.
            var a=TestFixtures.P(10,0);var exit=Exit(13,"Left exit",a);var route=new FieldExitRoute{Function="Air cab"};route.Menu=new[]{a};
            route.MenuApproaches[563]=new List<NavigationPoint>{a};route.MenuApproaches[572]=new List<NavigationPoint>{a};route.Names[563]="Lindblum, T.D. Station";route.Names[572]="Lindblum, I.D. Station";
            var target=Only(Build(new FieldTargetCatalog(),Routes(exit,route),exit));
            TestAssert.Equal("Air cab",target.Label);
            TestAssert.False(target.Availability.Contains("Station"));TestAssert.Equal(10000,target.Id.Variant);
            route.MenuButtons=0x0a0000;TestAssert.True(Only(Build(new FieldTargetCatalog(),Routes(exit,route),exit)).Availability.StartsWith("press Confirm there; the destination is chosen"));
            TestAssert.Equal(2,target.NextFields.Length);TestAssert.True(Array.IndexOf(target.NextFields,563)>=0 && Array.IndexOf(target.NextFields,572)>=0);
            TestAssert.Equal(1,target.DestinationApproaches[572].Length);TestAssert.Equal(a,target.DestinationApproaches[572][0]);
            TestAssert.False(Array.IndexOf(target.NextFields,9999)>=0);
            // A Story route through the cab keeps the region and its stable menu identity.
            var other=TestFixtures.P(40,0);var both=Exit(14,"Left exit",a,other);var mixed=new FieldExitRoute{Function="Air cab"};mixed.Menu=new[]{a};mixed.MenuApproaches[572]=new List<NavigationPoint>{a};mixed.Names[572]="Lindblum, I.D. Station";
            mixed.Approaches[551]=new[]{other};mixed.Names[551]="Lindblum, B.D. Station";
            var result=Build(new FieldTargetCatalog(),Routes(both,mixed),both);NavigationTarget cab=null;foreach(var t in result.Targets)if(t.Label=="Air cab")cab=t;
            TestAssert.True(cab!=null);TestAssert.Equal(both.Id,cab.PhysicalIdentity);TestAssert.Equal(10000,cab.Id.Variant);TestAssert.Equal(572,cab.NextFields[0]);
            var context=TestFixtures.Context();context.StoryProgress=1;
            var story=new StoryTargetCatalog(new[]{new StoryStage{Milestone=2,Goal="Lindblum",GoalFields=new[]{572}}},new StoryTransit[0]);
            story.Append(context,result,100,f=>f==572?"Lindblum, I.D. Station":null);
            var next=result.Targets[result.Targets.Count-1];
            TestAssert.Equal(NavigationCategory.Story,next.Category);TestAssert.Equal(cab.PhysicalIdentity,next.PhysicalIdentity);
            TestAssert.True(next.SelectDestinationInMenu);TestAssert.False(next.Label.Contains("Station"));
        }
        public static void AButtonGatedLedgeSaysWhichButton()
        {
            // Burmecia houses: touching the ledge with the "!" icon and pressing Confirm (or Special) jumps down.
            var a=TestFixtures.P(10,0);var exit=Exit(5,"Lower exit",a);var route=Route(752,new[]{a},"Burmecia, Residence");route.Buttons[752]=0x0a0000;
            TestAssert.Equal("Exit to Burmecia, Residence, press Confirm",Only(Build(new FieldTargetCatalog(),Routes(exit,route),exit)).Label);
            route.Prompted.Add(752);TestAssert.Equal("Exit to Burmecia, Residence, press Confirm, then it asks",Only(Build(new FieldTargetCatalog(),Routes(exit,route),exit)).Label);
            TestAssert.Equal("Cancel",FieldExitNames.Button(0x10000));TestAssert.Equal(null,FieldExitNames.Button(0));
        }
        public static void AnUnprovenExitWithOnePossiblePlaceNamesItHonestly()
        {
            var a=TestFixtures.P(10,0);var exit=Exit(7,"Upper exit",a);var route=new FieldExitRoute();route.PossibleNames.Add("Fossil Roo, Passage, Area 2");
            var target=Only(Build(new FieldTargetCatalog(),Routes(exit,route),exit));
            TestAssert.Equal("Exit to Fossil Roo, Passage, Area 2",target.Label);TestAssert.True(target.Availability.Contains("decides"));
            // Two possible places with no current proof are not narrowed to either.
            var two=Exit(8,"Upper exit",a);var both=new FieldExitRoute();both.PossibleNames.Add("A");both.PossibleNames.Add("B");
            TestAssert.Equal("Upper exit",Only(Build(new FieldTargetCatalog(),Routes(two,both),two)).Label);
            // A currently disabled exit keeps the one place it can lead to, with its availability.
            var closed=Exit(10,"Right exit");closed.Exit.Availability="current entrance trigger unavailable";var only=new FieldExitRoute();only.PossibleNames.Add("Dali, Inn");
            var shut=Only(Build(new FieldTargetCatalog(),Routes(closed,only),closed));
            TestAssert.Equal("Exit to Dali, Inn",shut.Label);TestAssert.True(shut.Availability.Contains("unavailable"));TestAssert.Equal(0,shut.ApproachCandidates.Length);
            // A reviewed function still names an unproven choice exit.
            var lift=Exit(9,"Upper exit",a);var reviewed=new FieldExitRoute{Function="Castle lift"};
            TestAssert.Equal("Castle lift",Only(Build(new FieldTargetCatalog(),Routes(lift,reviewed),lift)).Label);
        }
        public static void ReviewedExitFunctionsComeFromTheGeneratedNativeTable()
        {
            // Field 551 sid 13 (Lindblum B.D. Station): the native prompt is the Lindblum Castle Air Cab menu.
            string hash=null;foreach(var key in FieldExitFunctions.Keys())if(key.StartsWith("551:13:"))hash=key.Substring(7);
            TestAssert.True(hash!=null);TestAssert.Equal("Air cab",FieldExitFunctions.Function(551,13,hash));
            TestAssert.Equal(null,FieldExitFunctions.Function(551,13,"other-script"));
            TestAssert.True(FieldExitFunctions.Possible(551,13,hash).Length>=1);
        }
        public static void MenuChoicesKeepThePointsAtWhichTheyAreActuallyAvailable()
        {
            var a=TestFixtures.P(10,0);var b=TestFixtures.P(40,0);var exit=Exit(15,"Left exit",a,b);
            var route=new FieldExitRoute{Function="Castle lift",Menu=new[]{a,b}};
            route.Names[603]="Upper landing";route.Names[610]="Lower landing";
            route.MenuApproaches[603]=new List<NavigationPoint>{a};route.MenuApproaches[610]=new List<NavigationPoint>{b};
            var target=Only(Build(new FieldTargetCatalog(),Routes(exit,route),exit));
            TestAssert.Equal(1,target.DestinationApproaches[603].Length);TestAssert.Equal(a,target.DestinationApproaches[603][0]);
            TestAssert.Equal(1,target.DestinationApproaches[610].Length);TestAssert.Equal(b,target.DestinationApproaches[610][0]);
            TestAssert.False(target.Availability.Contains("Upper landing"));
        }
        public static void AProvenHelperExitCanGuideStoryWithoutALegacyDirectJump()
        {
            var at=TestFixtures.P(10,0);var exit=Exit(5,"Lower exit",at);
            var target=Only(Build(new FieldTargetCatalog(),Routes(exit,Route(752,new[]{at},"Burmecia, Residence")),exit));
            TestAssert.Equal(1,target.NextFields.Length);TestAssert.Equal(752,target.NextFields[0]);
            TestAssert.Equal(at,target.DestinationApproaches[752][0]);
        }
        public static void WorldMapDestinationsHaveOneOrdinaryName()
        {
            TestAssert.Equal("World Map",FieldExitNames.Destination(9003,field=>"unused"));
            TestAssert.Equal("Dali, Inn",FieldExitNames.Destination(200,field=>field==200?"Dali, Inn":null));
            TestAssert.Equal(null,FieldExitNames.Destination(201,field=>null));
        }
        public static void AVisibleWorldDepartureCanNameItsPromptWithoutInventingAScriptedEdge()
        {
            const string hash="7972d7bde40662db4e20a00e2935f2d9ffe3a15e415b8972df3d5d52073e122e";
            TestAssert.Equal(0,FieldExitFunctions.Possible(2750,1,hash).Length);
            var possible=FieldExitFunctions.Possible(2750,1,hash,true);
            TestAssert.Equal(13,possible.Length);foreach(var map in possible)TestAssert.Equal("World Map",FieldExitNames.Destination(map,null));
            var p=TestFixtures.P(10,0);var exit=Exit(1,"Upper exit",p);
            var route=new FieldExitRoute{ConfirmPrompt=true};route.PossibleNames.Add("World Map");
            var target=Only(Build(new FieldTargetCatalog(),Routes(exit,route),exit));
            TestAssert.Equal("Exit to World Map, press Confirm",target.Label);
            TestAssert.Equal(0,target.NextFields.Length);TestAssert.Equal(p,target.ApproachCandidates[0]);
        }
    }
    internal static class FieldPhysicalAreaTests
    {
        private static FieldAreaName Entry(int field,string scene,string name){return new FieldAreaName{Field=field,Scene=scene,Name=name};}
        public static void SameNameDifferentPhysicalAreasGetStableNumbers()
        {
            // Ice Cavern rooms share one native name; each physical room has its own number in native order.
            var names=FieldAreaNames.Build(new[]{Entry(301,"FBG_N05_ICCV_MAP087_IC_TER_0","Ice Cavern/Ice Path"),Entry(300,"FBG_N05_ICCV_MAP086_IC_STP_0","Ice Cavern/Ice Path"),Entry(305,"FBG_N05_ICCV_MAP092_IC_STA_0","Ice Cavern/Ice Path")});
            TestAssert.Equal("Ice Cavern, Ice Path, Area 1",names[300]);TestAssert.Equal("Ice Cavern, Ice Path, Area 2",names[301]);TestAssert.Equal("Ice Cavern, Ice Path, Area 3",names[305]);
        }
        public static void SceneAliasesOfOnePhysicalAreaShareItsNumber()
        {
            // Main Street at night (MAP820) and in disc 3 (MAP830) is the same street as MAP016: one installed camera pose.
            TestAssert.Equal("FBG_N01_ALXT_MAP016_AT_MSA_0",FieldPhysicalAreas.Of("FBG_N01_ALXT_MAP820_AT_MSA_A"));
            TestAssert.Equal("FBG_N01_ALXT_MAP016_AT_MSA_0",FieldPhysicalAreas.Of("FBG_N01_ALXT_MAP830_AT_MSA_W"));
            TestAssert.Equal("FBG_N01_ALXT_MAP017_AT_MSB_0",FieldPhysicalAreas.Of("FBG_N01_ALXT_MAP017_AT_MSB_0"));
            // Cleyra's trunk rooms differ only by suffix but are separate places; 376X repeats 376.
            TestAssert.False(FieldPhysicalAreas.Of("FBG_N18_GTRE_MAP364_GT_DUN_1")==FieldPhysicalAreas.Of("FBG_N18_GTRE_MAP363_GT_DUN_0"));
            TestAssert.Equal("FBG_N18_GTRE_MAP376_GT_DUN_E",FieldPhysicalAreas.Of("FBG_N18_GTRE_MAP376X_GT_DUN_F"));
            var names=FieldAreaNames.Build(new[]{Entry(100,"FBG_N01_ALXT_MAP016_AT_MSA_0","Alexandria/Main Street"),Entry(101,"FBG_N01_ALXT_MAP017_AT_MSB_0","Alexandria/Main Street"),
                Entry(2800,"FBG_N01_ALXT_MAP820_AT_MSA_A","Alexandria/Main Street"),Entry(2950,"FBG_N01_ALXT_MAP830_AT_MSA_W","Alexandria/Main Street")});
            TestAssert.Equal("Alexandria, Main Street, Area 1",names[100]);TestAssert.Equal(names[100],names[2800]);TestAssert.Equal(names[100],names[2950]);
            TestAssert.Equal("Alexandria, Main Street, Area 2",names[101]);
        }
        public static void ASharedCameraPoseAloneNeverMergesDistinctRooms()
        {
            // SG_TOP 342 and 343 reuse one installed camera pose but their floors overlap only ~10%;
            // Cleyra trunk DUN_1 and DUN_2 share a pose with ~54% floor overlap. All are separate places.
            TestAssert.False(FieldPhysicalAreas.Of("FBG_N16_STGT_MAP343_SG_TOP_1")==FieldPhysicalAreas.Of("FBG_N16_STGT_MAP342_SG_TOP_0"));
            TestAssert.False(FieldPhysicalAreas.Of("FBG_N18_GTRE_MAP365_GT_DUN_2")==FieldPhysicalAreas.Of("FBG_N18_GTRE_MAP364_GT_DUN_1"));
            // Different room tokens never merge, even with one pose: the Red Rose and Hilda Garde III bridges.
            TestAssert.False(FieldPhysicalAreas.Of("FBG_N53_HLG3_MAPV50B_G3_BRG_2")==FieldPhysicalAreas.Of("FBG_N48_RDRS_MAPV00A_RR_BRG_1"));
            // Same pose and the same floor (the X repaint of 342) is one place.
            TestAssert.Equal(FieldPhysicalAreas.Of("FBG_N16_STGT_MAP342_SG_TOP_0"),FieldPhysicalAreas.Of("FBG_N16_STGT_MAP342X_SG_TOP_3"));
            var names=FieldAreaNames.Build(new[]{Entry(363,"FBG_N18_GTRE_MAP364_GT_DUN_1","Cleyra/Tree Trunk"),Entry(364,"FBG_N18_GTRE_MAP365_GT_DUN_2","Cleyra/Tree Trunk")});
            TestAssert.False(names[363]==names[364]);
        }
        public static void ASceneWithoutInstalledAssetsStaysItsOwnPlace()
        {
            // Field 1805 (EVT_ALEX3_AC_SEAT_N) names a scene with no installed .bgi/.bgs; it is not merged.
            TestAssert.Equal("FBG_N02_ALXC_MAP038_AC_AST_0",FieldPhysicalAreas.Of("FBG_N02_ALXC_MAP038_AC_AST_0"));
        }
        public static void UnknownScenesStayTheirOwnPlace()
        {
            TestAssert.Equal("FBG_UNKNOWN",FieldPhysicalAreas.Of("FBG_UNKNOWN"));TestAssert.Equal(null,FieldPhysicalAreas.Of(null));
        }
    }
}
