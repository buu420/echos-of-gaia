using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class CountedNavigationTests
    {
        private static NavigationUpdate Route(params NavigationPoint[] points)
        {
            var context=TestFixtures.Context();
            var target=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,context.LogicalMapKey,10),Generation=1,Label="Forest exit",Category=NavigationCategory.Exits,Position=points[points.Length-1]};
            var plan=new RoutePlan{Success=true,Generation=1,Transport=context.Transport,TargetId=target.Id,Waypoints=points,ApproachPoint=target.Position};
            return new NavigationUpdate{Context=context,Selected=target,Session=new RouteDecision{Mode=RouteMode.Guiding},GuidanceEnabled=true,Movement=new MovementObservation{RouteAvailable=true,RoutePoint=points[0],PlannedRoute=plan}};
        }
        private static string Speak(NavigationAnnouncements announcements,NavigationUpdate update,double now=0,bool busy=false,string last=null)
        {
            var result=announcements.Format(update,5000,new SpeechState{NowMilliseconds=now,Busy=busy,LastAnnouncement=last});
            foreach(var announcement in result)announcements.Acknowledge(announcement,true);
            return result.Count==0?null:result[0].Text;
        }
        public static void AcceptedGuidanceOnlyAdvancesAfterBackendAcceptance()
        {
            var update=Route(TestFixtures.P(-480,0),TestFixtures.P(-480,1200));var speech=new NavigationAnnouncements();Speak(speech,update);
            update.Context.Position=TestFixtures.P(-480,0);update.Movement.RoutePoint=TestFixtures.P(-480,1200);
            var failed=speech.Format(update,5000,new SpeechState{NowMilliseconds=100});TestAssert.Equal("up 5.",failed[0].Text);speech.Acknowledge(failed[0],false);
            TestAssert.Equal("up 5.",Speak(speech,update,1200));TestAssert.Equal(null,Speak(speech,update,1300));
        }
        public static void NarrationDefersATurnEvenWhenLastFacadeSpeechWasGuidance()
        {
            var update=Route(TestFixtures.P(-480,0),TestFixtures.P(-480,1200));var speech=new NavigationAnnouncements();string last=Speak(speech,update);
            update.Context.Position=TestFixtures.P(-480,0);update.Movement.RoutePoint=TestFixtures.P(-480,1200);
            TestAssert.Equal(0,speech.Format(update,5000,new SpeechState{NowMilliseconds=100,NarrationActive=true,LastAnnouncement=last}).Count);
            TestAssert.Equal("up 5.",Speak(speech,update,200));
        }
        public static void BentRouteCountsOnlyToTheNextTurn()
        {
            var update=Route(TestFixtures.P(-480,0),TestFixtures.P(-480,1200));
            TestAssert.Equal("left 2.",Speak(new NavigationAnnouncements(),update));
        }
        public static void TurnSpeaksImmediatelyInsteadOfWaitingForTheReminder()
        {
            var update=Route(TestFixtures.P(-480,0),TestFixtures.P(-480,1200));var speech=new NavigationAnnouncements();
            Speak(speech,update);update.Context.Position=TestFixtures.P(-480,0);update.Movement.RoutePoint=TestFixtures.P(-480,1200);
            TestAssert.Equal("up 5.",Speak(speech,update,100));
        }
        public static void MovingAlongOneLegCountsDownAtTheReminder()
        {
            var update=Route(TestFixtures.P(-480,0));var speech=new NavigationAnnouncements();Speak(speech,update);
            update.Context.Position=TestFixtures.P(-240,0);
            TestAssert.Equal(null,Speak(speech,update,100));TestAssert.Equal("left 1.",Speak(speech,update,5000));
        }
        public static void CollinearWaypointsFormOneSpokenRun()
        {
            var update=Route(TestFixtures.P(-240,0),TestFixtures.P(-480,0),TestFixtures.P(-480,1200));var speech=new NavigationAnnouncements();
            TestAssert.Equal("left 2.",Speak(speech,update));update.Context.Position=TestFixtures.P(-240,0);update.Movement.RoutePoint=TestFixtures.P(-480,0);
            TestAssert.Equal(null,Speak(speech,update,100));
        }
        public static void CornerDoesNotClaimTheFinalTargetIsHere()
        {
            var update=Route(TestFixtures.P(-480,0),TestFixtures.P(-480,1200));update.Context.Position=TestFixtures.P(-480,0);
            TestAssert.Equal("up 5.",Speak(new NavigationAnnouncements(),update));
        }
        public static void TinyBendIsKeptAndDoesNotBecomeArrival()
        {
            var update=Route(TestFixtures.P(-480,0),TestFixtures.P(-480,20),TestFixtures.P(-960,20));
            update.Context.Position=TestFixtures.P(-480,0);update.Movement.RoutePoint=TestFixtures.P(-480,20);
            TestAssert.Equal("up a little.",Speak(new NavigationAnnouncements(),update));
        }
        public static void ShortFinalApproachStillHasADirection()
        {
            var update=Route(TestFixtures.P(20,0));TestAssert.Equal("right a little.",Speak(new NavigationAnnouncements(),update));
            update.Context.Position=TestFixtures.P(20,0);TestAssert.Equal("Route approach reached.",Speak(new NavigationAnnouncements(),update));
        }
        public static void SmallDiagonalLegDoesNotBecomeACardinalShortcut()
        {
            var update=Route(TestFixtures.P(120,120));TestAssert.Equal("up and right 1.",Speak(new NavigationAnnouncements(),update));
        }
        public static void FieldTwistMatchesTheDirectionThePlayerPresses()
        {
            var update=Route(TestFixtures.P(480,0));update.Context.InputOrientationDegrees=90;
            TestAssert.Equal("up 2.",Speak(new NavigationAnnouncements(),update));
        }
        public static void FlyingChocoboDoesNotGetAStrafingInstruction()
        {
            var update=Route(TestFixtures.P(8,0));update.Context.LogicalMapKey="world:0";update.Context.Transport=new TransportIdentity(TransportKind.Chocobo,7,7);update.Context.InputOrientationDegrees=180;update.Context.WorldControlType=1;
            TestAssert.Equal("east 2.",Speak(new NavigationAnnouncements(),update));
        }
        public static void MeaningfulDiagonalKeepsBothDirections()
        {
            var update=Route(TestFixtures.P(480,480));TestAssert.Equal("up and right 3.",Speak(new NavigationAnnouncements(),update));
        }
        public static void WorldCountsUseWrappedRouteDistanceAndCameraDirection()
        {
            var update=Route(TestFixtures.P(2,0));update.Context.LogicalMapKey="world:0";update.Context.Position=TestFixtures.P(1534,0);update.Context.InputOrientationDegrees=180;update.Context.WorldControlType=0;
            TestAssert.Equal("left 1.",Speak(new NavigationAnnouncements(),update));
        }
        public static void TurningVehiclesKeepCompassInstructions()
        {
            var update=Route(TestFixtures.P(8,0));update.Context.LogicalMapKey="world:0";update.Context.Transport=new TransportIdentity(TransportKind.Boat,7,8);update.Context.InputOrientationDegrees=180;
            TestAssert.Equal("east 2.",Speak(new NavigationAnnouncements(),update));
        }
        public static void MissingRouteIsClearlyABearing()
        {
            var update=Route(TestFixtures.P(-480,0));update.Movement.RouteAvailable=false;
            TestAssert.Equal("Bearing only, left 2. No walking route.",Speak(new NavigationAnnouncements(),update));
        }
        public static void PlanningDoesNotGiveAnOldOrDirectWalkingInstruction()
        {
            var update=Route(TestFixtures.P(-480,0));update.IsPlanning=true;
            TestAssert.Equal("Finding a walking route.",Speak(new NavigationAnnouncements(),update));
        }
        public static void BusyUnrelatedSpeechDefersTheCurrentTurn()
        {
            var update=Route(TestFixtures.P(-480,0),TestFixtures.P(-480,1200));var speech=new NavigationAnnouncements();Speak(speech,update);
            update.Context.Position=TestFixtures.P(-480,0);update.Movement.RoutePoint=TestFixtures.P(-480,1200);
            TestAssert.Equal(null,Speak(speech,update,100,true,"Narration is still playing."));
            TestAssert.Equal("up 5.",Speak(speech,update,200));
        }
        public static void NewTurnMayReplaceOnlyThePreviousGuidance()
        {
            var update=Route(TestFixtures.P(-480,0),TestFixtures.P(-480,1200));var speech=new NavigationAnnouncements();var last=Speak(speech,update);
            update.Context.Position=TestFixtures.P(-480,0);update.Movement.RoutePoint=TestFixtures.P(-480,1200);
            var result=speech.Format(update,5000,new SpeechState{NowMilliseconds=100,Busy=true,LastAnnouncement=last});
            TestAssert.Equal(1,result.Count);TestAssert.Equal("up 5.",result[0].Text);TestAssert.True(result[0].Interrupt);
        }
        public static void StatusUsesTheSameInstructionWithoutRepeatingItNextFrame()
        {
            var update=Route(TestFixtures.P(-480,0));var speech=new NavigationAnnouncements();update.RequestStatus=true;
            TestAssert.True(Speak(speech,update).Contains("left 2."));update.RequestStatus=false;
            TestAssert.Equal(null,Speak(speech,update,20));
        }
        public static void NewTargetDoesNotInheritThePreviousReminderTimer()
        {
            var update=Route(TestFixtures.P(-480,0));var speech=new NavigationAnnouncements();Speak(speech,update);
            update.Selected.Id=new TargetIdentity(TargetKind.FieldExit,"field:100",11);update.Movement.PlannedRoute.TargetId=update.Selected.Id;
            TestAssert.Equal("left 2.",Speak(speech,update,100));
        }
        public static void GateAndResetMakeTheNextInstructionFresh()
        {
            var update=Route(TestFixtures.P(-480,0));var speech=new NavigationAnnouncements();Speak(speech,update);
            update.Context.CanControl=false;TestAssert.Equal(null,Speak(speech,update,100));update.Context.CanControl=true;
            TestAssert.Equal("left 2.",Speak(speech,update,200));speech.ResetPending();TestAssert.Equal("left 2.",Speak(speech,update,300));
        }
        public static void ContextAndReplannedRouteEachGetAFreshInstruction()
        {
            for(int change=0;change<5;change++) {
                var update=Route(TestFixtures.P(-480,0));var speech=new NavigationAnnouncements();Speak(speech,update);
                if(change==0)update.Context.Generation++;
                if(change==1)update.Context.SaveIdentity="another save";
                if(change==2)update.Context.PlayerIdentity++;
                if(change==3)update.Context.Transport=new TransportIdentity(TransportKind.Chocobo,9,3);
                if(change==4)update.Movement.PlannedRoute=Route(TestFixtures.P(-480,0)).Movement.PlannedRoute;
                TestAssert.Equal("left 2.",Speak(speech,update,100));
            }
        }
        public static void DeferredTurnsUseCurrentProgressRatherThanAQueuedOldTurn()
        {
            var update=Route(TestFixtures.P(-480,0),TestFixtures.P(-480,1200),TestFixtures.P(0,1200));var speech=new NavigationAnnouncements();Speak(speech,update);
            update.Context.Position=TestFixtures.P(-480,0);update.Movement.RoutePoint=TestFixtures.P(-480,1200);
            TestAssert.Equal(null,Speak(speech,update,100,true,"Other speech."));
            update.Context.Position=TestFixtures.P(-480,1200);update.Movement.RoutePoint=TestFixtures.P(0,1200);
            TestAssert.Equal("right 2.",Speak(speech,update,200));
        }
        public static void WorldCameraInstructionsAgreeWithNativeSteering()
        {
            string[] expected={"left 2.","down 2.","right 2.","up 2."};
            int[] lx={-128,0,128,0},ly={0,128,0,-128};
            for(int i=0;i<4;i++) {
                var update=Route(TestFixtures.P(8,0));update.Context.LogicalMapKey="world:0";update.Context.WorldControlType=0;update.Context.InputOrientationDegrees=i*90+180;
                var command=WorldSteering.For(update.Context,TestFixtures.P(8,0),new WorldMovementState{CameraDegrees=i*90,ControlType=0});
                TestAssert.Equal(lx[i],command.LX);TestAssert.Equal(ly[i],command.LY);
                TestAssert.Equal(expected[i],Speak(new NavigationAnnouncements(),update));
            }
        }
        public static void ViewingApproachDoesNotClaimAnInteraction()
        {
            var update=Route(TestFixtures.P(20,0));update.Context.Position=TestFixtures.P(20,0);update.Movement.PlannedRoute.ViewingPoint=true;
            TestAssert.Equal("Viewing point reached.",Speak(new NavigationAnnouncements(),update));
        }
        public static void DialogueStartingAndGuidanceOffStaySilent()
        {
            var update=Route(TestFixtures.P(-480,0));var speech=new NavigationAnnouncements();
            TestAssert.Equal(0,speech.Format(update,5000,new SpeechState{DialogueStarting=true}).Count);
            update.GuidanceEnabled=false;TestAssert.Equal(null,Speak(speech,update,100));
        }
    }
}
