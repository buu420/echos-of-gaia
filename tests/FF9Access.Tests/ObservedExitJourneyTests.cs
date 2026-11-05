using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class ObservedExitJourneyTests
    {
        private static TargetIdentity Source {get{return new TargetIdentity(TargetKind.FieldExit,"field:102:street",7);}}
        private static NavigationContext Arrival(){return new NavigationContext{Generation=12,LogicalMapKey="field:108:shop",SaveIdentity="save",CanControl=true,CurrentLocation="Alexandria Item Shop"};}
        private static ObservedExitJourney Depart(){var journey=new ObservedExitJourney();journey.Capture(Source,"sid7:script:polygon",10,"save");journey.Executed(108);journey.Detach(false);journey.Attach(false);return journey;}
        private static bool Complete(ObservedExitJourney journey,NavigationContext context,int map=108)
        {TargetIdentity id;string scope,label;return journey.TryComplete(context,map,out id,out scope,out label);}
        public static void LearnsOnlyExecutedCompletedForwardTraversal()
        {var journey=Depart();TargetIdentity id;string scope,label;TestAssert.True(journey.TryComplete(Arrival(),108,out id,out scope,out label));TestAssert.True(id.Equals(Source));TestAssert.Equal("sid7:script:polygon",scope);TestAssert.Equal("Last led to Alexandria Item Shop",label);TestAssert.False(Complete(journey,Arrival()));}
        public static void TriggerAndExecutionWithoutRealArrivalAreNotKnowledge()
        {var j=new ObservedExitJourney();j.Capture(Source,"scope",10,"save");TestAssert.False(Complete(j,Arrival()));j.Executed(108);TestAssert.False(Complete(j,Arrival()));j.Detach(false);TestAssert.False(Complete(j,Arrival()));}
        public static void NoControlAndMissingNativeLocationWaitForRealReadiness()
        {var j=Depart();var c=Arrival();c.CanControl=false;TestAssert.False(Complete(j,c));c.CanControl=true;c.CurrentLocation=null;TestAssert.False(Complete(j,c));c.CurrentLocation="Item Shop";TestAssert.True(Complete(j,c));}
        public static void BattleWorldWrongMapAndSaveReplacementDiscardTheJourney()
        {var j=Depart();var c=Arrival();c.GateReason=GateReason.Battle;TestAssert.False(Complete(j,c));TestAssert.False(Complete(j,Arrival()));j=Depart();c=Arrival();c.SaveIdentity="other save";TestAssert.False(Complete(j,c));j=Depart();TestAssert.False(Complete(j,Arrival(),109));j=Depart();j.Attach(true);TestAssert.False(Complete(j,Arrival()));j=Depart();j.Detach(true);TestAssert.False(Complete(j,Arrival()));}
        public static void ResetAndSecondSceneDepartureCannotLeakOldExitKnowledge()
        {var j=Depart();j.Reset();TestAssert.False(Complete(j,Arrival()));j=Depart();j.Detach(false);j.Attach(false);TestAssert.False(Complete(j,Arrival()));}
        public static void ActualViewportProvidesReadableExitPosition()
        {TestAssert.Equal("Upper-left exit",FieldExitLabel.Read(new NavigationPoint(.1f,.9f,1)));TestAssert.Equal("Right exit",FieldExitLabel.Read(new NavigationPoint(.9f,.5f,1)));TestAssert.Equal("Lower exit",FieldExitLabel.Read(new NavigationPoint(.5f,.1f,1)));TestAssert.Equal("Exit",FieldExitLabel.Read(new NavigationPoint(2,0,1)));}
        public static void CameraDescriptionChangesKeepStableDuplicateSuffix()
        {var catalog=new FieldTargetCatalog();var c=Arrival();var o=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldExit,c.LogicalMapKey,7),Position=new NavigationPoint(),VisibleLabel="Left exit",Exit=new ExitPresentation{Visible=true}};var a=catalog.Build(c,new[]{o},null).Targets[0].Label;o.VisibleLabel="Upper-left exit";var b=catalog.Build(c,new[]{o},null).Targets[0].Label;TestAssert.Equal("Left exit",a);TestAssert.Equal("Upper-left exit",b);}
        public static void ReviewedVisualTableHasSpecificChildrenAndNoUnknownGuess()
        {TestAssert.Equal("Child with pigtails",FieldVisibleLabels.Read(5492));TestAssert.Equal("Man in a cap",FieldVisibleLabels.Read(111));TestAssert.Equal<string>(null,FieldVisibleLabels.Read(395));TestAssert.Equal<string>(null,FieldVisibleLabels.Read(-1));}
        public static void VisibleUnvisitedExitAndResidentsAppearWithoutStoredKnowledge()
        {var c=Arrival();var exit=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldExit,c.LogicalMapKey,7),LabelIdentity="scope",VisibleLabel="Upper exit",Exit=new ExitPresentation{Visible=true},Position=new NavigationPoint()};var actor=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,c.LogicalMapKey,8),IsPerson=true,Visible=true,VisibleLabel="Child with pigtails",Position=new NavigationPoint()};var catalog=new FieldTargetCatalog().Build(c,new[]{exit,actor},new ObservedIdentityStore());TestAssert.Equal(2,catalog.Targets.Count);TestAssert.Equal("Upper exit",catalog.Targets[0].Label);TestAssert.Equal("Child with pigtails",catalog.Targets[1].Label);}
        public static void ExitChangedOutsideViewCannotRetainAnOldDestinationOrPosition()
        {var c=Arrival();var names=new ObservedIdentityStore();var o=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldExit,c.LogicalMapKey,7),LabelIdentity="original polygon",NativePresentationIdentity=7,VisibleLabel="Left exit",Exit=new ExitPresentation{Visible=true},Position=new NavigationPoint()};names.Record(o.Id,"Last led to Item Shop",o.LabelIdentity);var catalog=new FieldTargetCatalog();TestAssert.Equal("Last led to Item Shop",catalog.Build(c,new[]{o},names).Targets[0].Label);o.OutOfView=true;o.Exit.Visible=false;o.LabelIdentity="moved polygon";TestAssert.Equal(0,catalog.Build(c,new[]{o},names).Targets.Count);}
    }
}
