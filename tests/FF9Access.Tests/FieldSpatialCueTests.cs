using System;
using System.Collections.Generic;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldSpatialCueTests
    {
        private static NavigationContext Context(){return new NavigationContext{LogicalMapKey="field:test",Generation=1,PlayerIdentity=7,SaveIdentity="save",Position=new NavigationPoint(),Foreground=true,CanControl=true,GateReason=GateReason.None};}
        private static NavigationTarget Target(int id,NavigationCategory category,float x=100,float z=0){return new NavigationTarget{Id=new TargetIdentity(category==NavigationCategory.Exits?TargetKind.FieldExit:TargetKind.FieldActor,"field:test",id),Generation=1,Category=category,Position=new NavigationPoint(x,0,z),ApproachCandidates=new[]{new NavigationPoint(x,0,z)},IsInteractive=true,Evidence=category==NavigationCategory.Exits?VisibilityEvidence.NativeDoorTile:VisibilityEvidence.RenderedActor};}
        private static FieldSpatialCue Find(FieldSpatialCue[] cues,FieldSpatialCueKind kind){foreach(var cue in cues)if(cue.Kind==kind)return cue;throw new Exception("Missing "+kind+" cue");}
        public static void ThreeKindsUseDistinctRecordingsAndSpacedPulseStarts()
        {
            var t=new FieldSpatialCueTracker();var c=Context();var targets=new[]{Target(1,NavigationCategory.People,80),Target(2,NavigationCategory.Objects,160),Target(3,NavigationCategory.Exits,240)};
            var cues=t.Update(c,targets,0,p=>true);TestAssert.Equal(3,cues.Length);TestAssert.True(Find(cues,FieldSpatialCueKind.Npc).PulseSerial>0);TestAssert.Equal(0L,Find(cues,FieldSpatialCueKind.Interactable).PulseSerial);
            cues=t.Update(c,targets,249,p=>true);TestAssert.Equal(0L,Find(cues,FieldSpatialCueKind.Interactable).PulseSerial);
            cues=t.Update(c,targets,250,p=>true);TestAssert.True(Find(cues,FieldSpatialCueKind.Interactable).PulseSerial>0);
            cues=t.Update(c,targets,500,p=>true);TestAssert.True(Find(cues,FieldSpatialCueKind.Exit).PulseSerial>0);
            long pulse=Find(cues,FieldSpatialCueKind.Npc).PulseSerial;
            TestAssert.Equal(pulse,Find(t.Update(c,targets,3199,p=>true),FieldSpatialCueKind.Npc).PulseSerial);
            TestAssert.True(Find(t.Update(c,targets,3200,p=>true),FieldSpatialCueKind.Npc).PulseSerial>pulse);
        }
        public static void CurrentVisibleInteractiveItemsUseTheObjectSound()
        {
            var t=new FieldSpatialCueTracker();var cues=t.Update(Context(),new[]{Target(1,NavigationCategory.Items)},0,p=>true);
            TestAssert.Equal(1,cues.Length);TestAssert.Equal(FieldSpatialCueKind.Interactable,cues[0].Kind);
        }
        public static void HiddenRememberedWrongVisitStoryAndViewingOnlyTargetsCannotSound()
        {
            for(int mode=0;mode<9;mode++){
                var target=Target(1,NavigationCategory.People);
                if(mode==0)target.Evidence=VisibilityEvidence.None;if(mode==1)target.Evidence=VisibilityEvidence.ObservedFieldLandmark;
                if(mode==2)target.Generation=2;if(mode==3)target.Id=new TargetIdentity(TargetKind.FieldActor,"field:other",1);
                if(mode==4)target.IsInteractive=false;if(mode==5)target.Category=NavigationCategory.Story;
                if(mode==6)target.UseViewingPoint=true;if(mode==7)target.ApproachCandidates=new NavigationPoint[0];
                if(mode==8)target.Position=new NavigationPoint(float.NaN,0,0);
                TestAssert.Equal(0,new FieldSpatialCueTracker().Update(Context(),new[]{target},0,p=>{throw new Exception("Ineligible target touched geometry");}).Length);
            }
        }
        public static void NearestClearTargetPerKindWinsAndSourceRetainsItsActualPosition()
        {
            var blocked=Target(1,NavigationCategory.People,50);var clear=Target(2,NavigationCategory.People,240);
            clear.Position=new NavigationPoint(280,20,0);
            var cues=new FieldSpatialCueTracker().Update(Context(),new[]{blocked,clear,clear},0,p=>p.X!=50);
            TestAssert.Equal(1,cues.Length);TestAssert.Equal(clear.Id,cues[0].Identity);TestAssert.Equal(280f,cues[0].Position.X);TestAssert.True(Math.Abs(cues[0].Gain-.5f)<.00001);
            TestAssert.Equal(0,new FieldSpatialCueTracker().Update(Context(),new[]{Target(3,NavigationCategory.Exits,400)},0,p=>{throw new Exception("Out of range");}).Length);
            TestAssert.Equal(0,new FieldSpatialCueTracker().Update(Context(),new[]{clear},0,p=>false).Length);
        }
        public static void MovingTargetsUpdateTheirDirectionWithoutRestartingThePulse()
        {
            var t=new FieldSpatialCueTracker();var c=Context();var target=Target(1,NavigationCategory.People,80);
            var cue=t.Update(c,new[]{target},0,p=>true)[0];long serial=cue.PulseSerial;TestAssert.True(cue.Direction.X>.99);
            target.Position=new NavigationPoint(-80,0,0);target.ApproachCandidates=new[]{target.Position};
            cue=t.Update(c,new[]{target},200,p=>true)[0];TestAssert.Equal(serial,cue.PulseSerial);TestAssert.True(cue.Direction.X<-.99);
            TestAssert.Equal(0,t.Update(c,new NavigationTarget[0],201,p=>true).Length);
            TestAssert.True(t.Update(c,new[]{target},202,p=>true)[0].PulseSerial>serial);
        }
        public static void RotationAgreesWithNavigationAndSteamAudioUsesNegativeForwardZ()
        {
            var c=Context();NavigationPoint direction;
            TestAssert.True(FieldSpatialCueTracker.TryDirection(c,new NavigationPoint(0,0,100),out direction));TestAssert.True(direction.Z<-.99);
            c.InputOrientationDegrees=90;TestAssert.True(FieldSpatialCueTracker.TryDirection(c,new NavigationPoint(100,0,0),out direction));TestAssert.True(direction.Z<-.99);
            TestAssert.True(FieldSpatialCueTracker.TryDirection(c,new NavigationPoint(0,0,100),out direction));TestAssert.True(direction.X<-.99);
            TestAssert.True(FieldSpatialCueTracker.TryDirection(c,c.Position,out direction));TestAssert.Equal(-1f,direction.Z);
            c.InputOrientationDegrees=float.NaN;TestAssert.False(FieldSpatialCueTracker.TryDirection(c,c.Position,out direction));
        }
        public static void UnsafeContextsAndIdentityChangesDoNotKeepOldPulses()
        {
            foreach(var gate in new[]{GateReason.Battle,GateReason.Dialogue,GateReason.Menu,GateReason.Background,GateReason.MapTransition,GateReason.NoControl,GateReason.UnknownActor,GateReason.UnknownInput,GateReason.Disconnected}){
                var c=Context();var target=Target(1,NavigationCategory.People);var t=new FieldSpatialCueTracker();long pulse=t.Update(c,new[]{target},0,p=>true)[0].PulseSerial;
                c.GateReason=gate;TestAssert.Equal(0,t.Update(c,new[]{target},1,p=>true).Length);c.GateReason=GateReason.None;
                TestAssert.True(t.Update(c,new[]{target},2,p=>true)[0].PulseSerial>pulse);
            }
            for(int mode=0;mode<5;mode++){
                var c=Context();var target=Target(1,NavigationCategory.People);var t=new FieldSpatialCueTracker();long pulse=t.Update(c,new[]{target},10,p=>true)[0].PulseSerial;
                if(mode==0)c.SaveIdentity="other";if(mode==1)c.PlayerIdentity++;if(mode==2){c.Generation++;target.Generation++;}if(mode==3)c.MinigameInspection=true;if(mode==4)c.Foreground=false;
                var result=t.Update(c,new[]{target},11,p=>true);if(mode<3)TestAssert.True(result[0].PulseSerial>pulse);else TestAssert.Equal(0,result.Length);
            }
        }
        public static void RaisedActorsAndBoundTalkingRegionsUseNativeGroundApproaches()
        {
            var c=Context();c.Position=new NavigationPoint(200,0,200);
            var geometry=new FieldGeometrySnapshot{Generation=1,Radius=10,ActiveTriangle=0,Triangles=new[]{
                new FieldTriangle{Id=0,Vertices=new[]{new NavigationPoint(0,0,0),new NavigationPoint(1000,0,0),new NavigationPoint(0,0,1000)},Neighbors=new[]{-1,1,-1}},
                new FieldTriangle{Id=1,Vertices=new[]{new NavigationPoint(1000,0,0),new NavigationPoint(1000,0,1000),new NavigationPoint(0,0,1000)},Neighbors=new[]{-1,-1,0}}}};
            for(int bound=0;bound<2;bound++){
                var actor=Target(9,NavigationCategory.People);actor.Position=new NavigationPoint(400,bound==0?200:600,200);
                actor.ApproachCandidates=FieldActorApproaches.Create(c.Position,actor.Position,76,140);
                if(bound==1){actor.InteractionUid=19;actor.InteractionRegion=new[]{new NavigationPoint(320,0,150),new NavigationPoint(420,0,150),new NavigationPoint(420,0,250),new NavigationPoint(320,0,250)};}
                var cues=new FieldSpatialCueTracker().Update(c,new[]{actor},0,()=>geometry);
                TestAssert.Equal(1,cues.Length);TestAssert.Equal(actor.Id,cues[0].Identity);TestAssert.Equal(actor.Position.Y,cues[0].Position.Y);TestAssert.True(cues[0].Direction.Y>0);
            }
        }
        public static void DistantOrUnsafeTargetsDoNotPrepareFieldGeometryForSounds()
        {
            var c=Context();var distant=Target(1,NavigationCategory.People,5000);var tracker=new FieldSpatialCueTracker();
            TestAssert.Equal(0,tracker.Update(c,new[]{distant},0,()=>{throw new Exception("Distant actor requested geometry");}).Length);
            c.GateReason=GateReason.Battle;TestAssert.Equal(0,tracker.Update(c,new[]{Target(2,NavigationCategory.People)},1,()=>{throw new Exception("Battle requested geometry");}).Length);
        }
        public static void CurrentPlainWalkOutRegionsSoundButRememberedCopiesDoNot()
        {
            var c=Context();var point=new NavigationPoint(100,0,0);
            var observation=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldExit,c.LogicalMapKey,20),Position=point,ContainsJump=true,
                Exit=new ExitPresentation{Visible=true,ApproachEnabled=true,Approach=point,ApproachCandidates=new[]{point},Evidence=VisibilityEvidence.ObservedRegion}};
            var catalog=new FieldTargetCatalog();var current=catalog.Build(c,new[]{observation},new ObservedIdentityStore());TestAssert.Equal(1,current.Targets.Count);
            TestAssert.Equal(1,new FieldSpatialCueTracker().Update(c,current.Targets,0,p=>true).Length);
            observation.Exit.Visible=false;observation.OutOfView=true;var remembered=catalog.Build(c,new[]{observation},new ObservedIdentityStore());TestAssert.Equal(1,remembered.Targets.Count);TestAssert.Equal(VisibilityEvidence.ObservedFieldLandmark,remembered.Targets[0].Evidence);
            TestAssert.Equal(0,new FieldSpatialCueTracker().Update(c,remembered.Targets,1,p=>{throw new Exception("Remembered exit touched geometry");}).Length);
            var npc=Target(2,NavigationCategory.People);npc.Evidence=VisibilityEvidence.ObservedRegion;TestAssert.Equal(0,new FieldSpatialCueTracker().Update(c,new[]{npc},0,p=>{throw new Exception("Unproven NPC evidence");}).Length);
        }
    }
}
