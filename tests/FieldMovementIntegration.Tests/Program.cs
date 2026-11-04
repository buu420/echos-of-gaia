using System;
using System.Reflection;
using Memoria.Accessibility.Navigation;
using UnityEngine;
internal static class Program
{
    private static FieldMapActorController controller;
    private static NavigationContext context;
    private static NavigationTarget target;
    private static long nextOwner;
    private static void Require(bool condition,string text){if(!condition)throw new Exception(text);}
    private static void Reset()
    {
        var actor=new Actor();controller=new FieldMapActorController{originalActor=actor};actor.fieldMapActorController=controller;
        UnityEngine.Object.Engine=new EventEngine{Player=actor};
        context=new NavigationContext{Generation=1,PlayerIdentity=7,CanControl=true,Foreground=true,LogicalMapKey="field:100",Transport=new TransportIdentity(TransportKind.Foot,7,0)};
        NativeNavigationContext.Context=context;
        target=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldExit,"field:100",15),Generation=1};
        NativeFieldGeometry.Interruptions=0;NativeFieldGeometry.State=null;NativeFieldGeometry.FailCapture=false;
        Memoria.Prime.Log.FailWrite=false;
    }
    private static void Start()
    {
        var route=new RoutePlan{Success=true,Generation=1,TargetId=target.Id,Transport=context.Transport,GeometryFingerprint="mesh",Waypoints=new[]{new NavigationPoint(25,0,1500),new NavigationPoint(50,0,6500)}};
        Require(NativeFieldMovement.TryStart(context,target,route,++nextOwner)==MovementStartResult.Started,"owned movement did not start");
        controller.hasTarget=true;controller.moveTarget=controller.movePaths[controller.movePaths.Count-1];controller.movePaths.RemoveAt(controller.movePaths.Count-1);
        controller.curPos=new Vector3(20,0,1490);controller.lastPos=controller.curPos;controller.stillCount=31;
    }
    private static void ClearCapturesOwnedPathBeforeItIsDestroyed()
    {
        Start();NativeFieldMovement.OnNativeClear(controller);
        Require(NativeFieldGeometry.Interruptions==1,"native interruption did not capture the failing waypoint");
        Require(NativeFieldGeometry.ReleasedWhenCaptured,"optional geometry/file work ran before movement was released");
        var state=NativeFieldGeometry.State;
        Require(state!=null && (bool)state.GetType().GetField("HasTarget").GetValue(state),"pre-clear hasTarget was lost");
        Require((int)state.GetType().GetField("RemainingWaypoints").GetValue(state)==1,"pre-clear pending path was lost");
        Require((int)state.GetType().GetField("StillCount").GetValue(state)==31,"still-frame counter was lost");
        var waypoint=(NavigationPoint)state.GetType().GetField("Waypoint").GetValue(state);
        Require(waypoint.Z==1500,"next waypoint was lost");
        Require(controller.AccessibilityPathOwner==0 && !controller.hasTarget && controller.movePaths.Count==0,"native stop left owned movement active");
    }
    private static void UnownedNativePathDoesNotCaptureOrChange()
    {
        controller.hasTarget=true;controller.moveTarget=new Vector3(10,0,20);NativeFieldMovement.OnNativeClear(controller);
        Require(NativeFieldGeometry.Interruptions==0 && controller.hasTarget && controller.moveTarget.z==20,"diagnostics changed an unowned native path");
    }
    private static void CaptureFailureCannotPreventCancellation()
    {
        Start();NativeFieldGeometry.FailCapture=true;NativeFieldMovement.OnNativeClear(controller);
        Require(controller.AccessibilityPathOwner==0 && controller.movePaths.Count==0 && !controller.hasTarget,"capture failure prevented cancellation");
        Require(NativeFieldMovement.ReadOutcome(context,target).Outcome==FieldOutcome.Interrupted,"capture failure lost the terminal outcome");
    }
    private static void RepeatedClearDoesNotCaptureEveryFrame()
    {
        Start();for(int i=0;i<60;i++)NativeFieldMovement.OnNativeClear(controller);
        Require(NativeFieldGeometry.Interruptions==1,"one native clear produced repeated frame captures");
    }
    private static void NativeCausesRemainDistinctAndStillFramesReportStalled()
    {
        foreach(var cause in new[]{FieldPathClearCause.StillFrames,FieldPathClearCause.PhysicalMovement,FieldPathClearCause.Interaction,FieldPathClearCause.ScriptedControl}) {
            Start();NativeFieldMovement.OnNativeClear(controller,cause);
            Require(((FieldMovementCapture)NativeFieldGeometry.State).Cause==cause,"actual native clear cause was lost");
            var outcome=NativeFieldMovement.ReadOutcome(context,target);
            Require(outcome.Outcome==FieldOutcome.Interrupted && outcome.Reason==(cause==FieldPathClearCause.StillFrames?StopReason.Stalled:StopReason.NativeClear),"native cause was misreported");
            Require(controller.AccessibilityPathOwner==0,"native cause restarted movement");
        }
    }
    private static void ProbeAndLogFailuresCannotPreventTheNativeStop()
    {
        Start();controller.walkMesh=new WalkMesh{FailProbe=true};Memoria.Prime.Log.FailWrite=true;
        NativeFieldMovement.OnNativeClear(controller,FieldPathClearCause.StillFrames);
        Require(controller.AccessibilityPathOwner==0 && !controller.hasTarget && controller.movePaths.Count==0,"diagnostic failure kept walking active");
        Require(NativeFieldMovement.ReadOutcome(context,target).Reason==StopReason.Stalled,"diagnostic failure lost the stop reason");
        Require(NativeFieldGeometry.State is FieldMovementCapture && !((FieldMovementCapture)NativeFieldGeometry.State).CollisionProbeAvailable,"failed optional collision observation erased the waypoint");
    }
    private static void PreviousCollisionIsNeverAssumedToBeCurrent()
    {
        Start();controller.originalActor.coll=new PosObj{uid=8,collRad=14,pos=new[]{100f,0f,1400f}};
        controller.walkMesh=new WalkMesh{Contact=new PosObj{uid=9,collRad=20,pos=new[]{30f,0f,1500f}}};
        NativeFieldMovement.OnNativeClear(controller,FieldPathClearCause.StillFrames);
        var state=(FieldMovementCapture)NativeFieldGeometry.State;
        Require(state.PreviousCollisionUid==8 && state.CollisionUid==9,"stale contact was confused with current native collision");
        Require(state.CollisionProbeAvailable && state.CollisionRadius==80 && state.PreviousCollisionRadius==56 && state.CollisionDistance==-2,"native radius/distance evidence was lost");
    }
    private static int Main()
    {
        var tests=new Action[]{ClearCapturesOwnedPathBeforeItIsDestroyed,UnownedNativePathDoesNotCaptureOrChange,CaptureFailureCannotPreventCancellation,RepeatedClearDoesNotCaptureEveryFrame,NativeCausesRemainDistinctAndStillFramesReportStalled,ProbeAndLogFailuresCannotPreventTheNativeStop,PreviousCollisionIsNeverAssumedToBeCurrent};
        int failures=0;
        foreach(var test in tests)try{Reset();test();Console.WriteLine("PASS "+test.Method.Name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+test.Method.Name+": "+e.Message);}
        Console.WriteLine("FieldMovementIntegration: "+(tests.Length-failures)+" passed, "+failures+" failed");return failures==0?0:1;
    }
}
