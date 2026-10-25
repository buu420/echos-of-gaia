using System;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldExitProximityTests
    {
        private static NavigationContext Context() {return new NavigationContext{Generation=1,LogicalMapKey="field:2950:test",Foreground=true,CanControl=true,GateReason=GateReason.None,PlayerIdentity=25,Position=new NavigationPoint(0,0,0)};}
        private static NavigationTarget Exit(int uid=20,float x=100) {return new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,"field:2950:test",uid),Generation=1,Category=NavigationCategory.Exits,IsInteractive=true,Position=new NavigationPoint(x,0,0),ApproachCandidates=new[]{new NavigationPoint(x,0,0)}};}
        public static void NearbyCurrentExitPulsesAtBlindSoldierIntervalAndAttenuates()
        {
            var tracker=new FieldExitProximityTracker();var c=Context();var e=Exit(20,240);
            var cue=tracker.Update(c,new[]{e},0,p=>true);TestAssert.True(cue!=null);TestAssert.True(Math.Abs(cue.Gain-.5f)<.001f);
            TestAssert.Equal<FieldExitProximityCue>(null,tracker.Update(c,new[]{e},3199,p=>true));
            TestAssert.True(tracker.Update(c,new[]{e},3200,p=>true)!=null);
            c.Position=new NavigationPoint(200,0,0);TestAssert.Equal(1f,tracker.Update(c,new[]{e},6400,p=>true).Gain);
        }
        public static void NoCueAcrossWallsDifferentFloorsOrOutsideRange()
        {
            var tracker=new FieldExitProximityTracker();var c=Context();
            TestAssert.Equal<FieldExitProximityCue>(null,tracker.Update(c,new[]{Exit()},0,p=>false));
            TestAssert.Equal<FieldExitProximityCue>(null,tracker.Update(c,new[]{Exit(20,400)},0,p=>{throw new Exception("Out-of-range exit checked geometry");}));
            var e=Exit();e.ApproachCandidates=new[]{new NavigationPoint(0,1000,0)};
            TestAssert.Equal<FieldExitProximityCue>(null,tracker.Update(c,new[]{e},0,p=>true));
        }
        public static void StoryCopiesDisabledWrongSceneAndStaleTargetsStaySilent()
        {
            var c=Context();var tracker=new FieldExitProximityTracker();
            foreach(int mode in new[]{0,1,2,3,4}) {
                var e=Exit();if(mode==0)e.Category=NavigationCategory.Story;if(mode==1)e.Generation=2;if(mode==2)e.Id=new TargetIdentity(TargetKind.FieldExit,"field:other",20);if(mode==3)e.IsInteractive=false;if(mode==4)e.ApproachCandidates=new NavigationPoint[0];
                TestAssert.Equal<FieldExitProximityCue>(null,tracker.Update(c,new[]{e},0,p=>{throw new Exception("Ineligible target checked geometry");}));
            }
        }
        public static void BattleDialogueFocusMenuAndMinigameClearTheCueState()
        {
            foreach(var gate in new[]{GateReason.Battle,GateReason.Dialogue,GateReason.Menu,GateReason.Background,GateReason.MapTransition,GateReason.NoControl}) {
                var tracker=new FieldExitProximityTracker();var c=Context();TestAssert.True(tracker.Update(c,new[]{Exit()},0,p=>true)!=null);
                c.GateReason=gate;TestAssert.Equal<FieldExitProximityCue>(null,tracker.Update(c,new[]{Exit()},1,p=>true));TestAssert.False(tracker.HasAudibleExit);
                c.GateReason=GateReason.None;TestAssert.True(tracker.Update(c,new[]{Exit()},2,p=>true)!=null);
            }
            var t=new FieldExitProximityTracker();var context=Context();context.MinigameInspection=true;TestAssert.Equal<FieldExitProximityCue>(null,t.Update(context,new[]{Exit()},0,p=>true));
        }
        public static void LeavingRangeAndReenteringOrChangingVisitDoesNotKeepAStaleCooldown()
        {
            var tracker=new FieldExitProximityTracker();var c=Context();var e=Exit();
            TestAssert.True(tracker.Update(c,new[]{e},0,p=>true)!=null);
            c.Position=new NavigationPoint(1000,0,0);TestAssert.Equal<FieldExitProximityCue>(null,tracker.Update(c,new[]{e},1,p=>true));TestAssert.False(tracker.HasAudibleExit);
            c.Position=new NavigationPoint();TestAssert.True(tracker.Update(c,new[]{e},2,p=>true)!=null);
            c.Generation=2;e.Generation=2;TestAssert.True(tracker.Update(c,new[]{e},3,p=>true)!=null);
        }
        public static void NearestClearApproachWinsWithoutRepeatingForStoryAlias()
        {
            var tracker=new FieldExitProximityTracker();var c=Context();var blocked=Exit(19,50);var clear=Exit(20,200);
            var cue=tracker.Update(c,new[]{blocked,clear,clear},0,p=>p.X!=50);
            TestAssert.Equal(clear.Id,cue.Identity);TestAssert.True(tracker.HasAudibleExit);
            TestAssert.Equal<FieldExitProximityCue>(null,tracker.Update(c,new[]{clear},1,p=>true));
        }
        public static void NativeSfxMuteAndBothVolumeControlsAreRespected()
        {
            TestAssert.Equal(0f,FieldExitProximityTracker.VolumeGain(false,100,false,100));
            TestAssert.Equal(0f,FieldExitProximityTracker.VolumeGain(true,100,true,100));
            TestAssert.Equal(0f,FieldExitProximityTracker.VolumeGain(true,0,false,100));
            TestAssert.Equal(0f,FieldExitProximityTracker.VolumeGain(true,100,false,0));
            TestAssert.Equal(.25f,FieldExitProximityTracker.VolumeGain(true,50,false,50));
            TestAssert.Equal(2f,FieldExitProximityTracker.VolumeGain(true,999,false,999));
        }
    }
}
