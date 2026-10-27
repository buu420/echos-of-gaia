using System;
using System.IO;
using UnityEngine;
using Assets.Sources.Scripts.UI.Common;
using Memoria.ScreenReader;
using Memoria.Prime;
namespace Memoria.Accessibility.Navigation
{
    public static class NavigationRuntime
    {
        private static readonly NavigationCoordinator _coordinator=new NavigationCoordinator(NavigationLifecycleBridge.Session);
        private static readonly NavigationAnnouncements _announcer=new NavigationAnnouncements();
        private static int _frame=-1,_planningAttempts;private static long _owner,_nextOwner;
        private static RoutePlan _route;private static NavigationTarget _routeTarget;
        private static RouteGuidanceProgress _guideProgress;
        private static readonly OwnedWorldPlanner _worldPlanner=new OwnedWorldPlanner();
        private static long _worldRequest,_nextWorldRequest;
        private static System.Diagnostics.Stopwatch _planTimer;
        private static RouteOperation _planning;private static MovementObservation _pending;
        private static bool _exitReached,_minigameInspection;private static string _traceContext,_traceSpeech,_error;
        static NavigationRuntime(){NavigationLifecycleBridge.Releasing+=DetachScene;NativeFieldMovement.ExitReached+=delegate{_exitReached=true;};}
        public static void Tick()
        {
            if(_frame==Time.frameCount)return;_frame=Time.frameCount;
            NavigationContext context=null;
            try {
                NativeNavigationInput.EnsureUpdated();context=NativeNavigationContext.Read();
                NativeWorldFootsteps.UpdateGates(context);
                NativeFieldExitCues.UpdateGates(context);
                if(context.MinigameInspection) {
                    if(!_minigameInspection) {
                        _minigameInspection=true;DetachScene(StopReason.Menu);_announcer.ResetPending();
                        Trace("Hot and Cold inspection; map="+context.LogicalMapKey+"; generation="+context.Generation);
                    }
                    NavigationLifecycleBridge.Session.Update(context,NavigationSignal.None);
                    NativeChocoboHotCold.Tick(context,NativeNavigationInput.TakeCommands());
                    NavigationLifecycleBridge.Acknowledge(context);return;
                }
                if(_minigameInspection){_minigameInspection=false;_announcer.ResetPending();}
                NativeChocoboHotCold.DrainResults(context);
                bool world=context.LogicalMapKey!=null && context.LogicalMapKey.StartsWith("world:");
                if(world && context.Foreground)NativeWorldGeometry.Warm(context);
                ReadPresentation(context,world);
                CatalogResult catalog=world?WorldMapAccessibilityManager.Instance.ReadTargets(context):AccessibleNavigationManager.Instance.ReadTargets(context);
                NativeFieldExitCues.Tick(context,catalog);
                NativeStoryTargets.Append(context,catalog);
                var movement=ReadMovement(context,catalog);
                var commands=NativeNavigationInput.TakeCommands();var update=_coordinator.Tick(context,commands,catalog,movement);
                if(_pending==movement && update.Session.Mode!=RouteMode.Moving)_pending=null;
                update.ControllerNaming=Configuration.Accessibility.ControllerNaming;
                foreach(var command in commands) {
                    Trace("Command "+command+"; map="+context.LogicalMapKey+"; generation="+context.Generation+"; mode="+update.Session.Mode);
                    if(command==NavigationCommand.DecreaseInterval || command==NavigationCommand.IncreaseInterval) {
                        Configuration.Accessibility.NavigationIntervalMilliseconds+=command==NavigationCommand.IncreaseInterval?500:-500;
                        Configuration.Accessibility.SaveValues();update.Messages.Add("Guidance interval "+(Configuration.Accessibility.NavigationIntervalMilliseconds/1000f).ToString("0.0")+" seconds.");
                    }
                }
                if(update.ModifierOpened) {update.Messages.Clear();update.Messages.Add("Navigation. "+update.Category+". "+(update.Selected==null?(catalog.UnavailableReason??"No target currently presented."):NavigationCoordinator.SelectionLabel(update.Selected)+".")+" "+NavigationCoordinator.ModifierHelp(update.ControllerNaming));}
                if(update.Session.ReleaseMovement)DetachScene(update.Session.Reason);
                if(!String.IsNullOrEmpty(NativeNavigationInput.Diagnostic))update.Diagnostics.Add(NativeNavigationInput.Diagnostic);
                if(_exitReached && context.CanControl){update.Messages.Add("Selected exit reached. "+context.CurrentLocation+".");_exitReached=false;}
                if(update.RouteOperation!=RouteOperation.None && update.Selected!=null && context.CanControl)BeginPlan(update.RouteOperation,update.Selected);
                if(_planning!=RouteOperation.None && context.CanControl && update.Selected!=null && _routeTarget.Id.Equals(update.Selected.Id))ApplyPlan(context,update.Selected,world,update.Session.IsAutomatic);
                NavigationLifecycleBridge.Acknowledge(context);
                string traceKey=context.Generation+":"+context.LogicalMapKey+":"+context.CanControl+":"+context.GateReason;
                if(_traceContext!=traceKey){_traceContext=traceKey;Trace("Context "+traceKey+"; player="+context.PlayerIdentity+"; transport="+context.Transport.Kind+"; location="+context.CurrentLocation+"; targets="+catalog.Targets.Count);}
                var facade=ScreenReaderManager.Instance;
                string speechState=facade.Enabled+":"+facade.IsInitialized+":"+facade.SpeechGeneration+":"+facade.BackendDiagnostic+"; last="+facade.LastAnnouncement+"; log path="+facade.DiagnosticLogPath+"; log failure="+facade.DiagnosticWriteFailure;
                if(_traceSpeech!=speechState){_traceSpeech=speechState;Trace("Speech "+speechState+"; bits="+(IntPtr.Size*8)+"; base="+AppDomain.CurrentDomain.BaseDirectory+"; assembly="+typeof(ScreenReaderManager).Assembly.Location);}
                foreach(var announcement in _announcer.Format(update,Configuration.Accessibility.NavigationIntervalMilliseconds,new SpeechState{NowMilliseconds=Time.realtimeSinceStartup*1000,DialogueActive=!context.CanInspect && !context.CanControl,Busy=facade.IsSpeaking(),NarrationActive=Memoria.Accessibility.AudioDescriptions.NativeAudioDescriptions.NarrationPlaying,LastAnnouncement=facade.LastAnnouncement})) {
                    _announcer.Acknowledge(announcement,facade.TrySpeak(announcement.Text,announcement.Interrupt));
                }
                _error=null;
            }catch(Exception exception) {
                NavigationLifecycleBridge.Release(StopReason.UnknownContext);
                string message="Navigation unavailable: "+exception.Message;
                if(_error!=message){_error=message;Trace(message);Log.Message("[FF9Access] Navigation exception: {0}",exception);if(context!=null && context.CanInspect)ScreenReaderManager.Instance.SpeakInterrupt(message);}
            }
        }
        private static void ReadPresentation(NavigationContext context,bool world)
        {
            if(world) {
                // Type-0 ground controls face opposite world +Z at camera zero.
                // Inverting the camera rotation gives the same directions as
                // WorldSteering.For; turning craft continue to use the compass.
                context.InputOrientationDegrees=ff9.w_cameraSysDataCamera.rotation+180;
                context.WorldControlType=ff9.w_moveCHRControlPtr==null?-1:ff9.w_moveCHRControlPtr.type;
                int id;string label;if(NativeRegionReader.TryRead(out id,out label)){context.CurrentLocation=FF9TextTool.RemoveOpCode(label);if(Configuration.Accessibility.AnnounceRegionChanges)context.CurrentRegion=context.CurrentLocation;}
            }
            else if(context.LogicalMapKey!=null && context.LogicalMapKey.StartsWith("field:")) {
                context.CurrentLocation=FF9TextTool.RemoveOpCode(FF9TextTool.LocationName(FF9StateSystem.Common.FF9.fldMapNo));
                bool analog=Configuration.AnalogControl.Enabled;
                bool stick=NativeNavigationInput.Physical!=null && (Math.Abs(NativeNavigationInput.Physical.MoveX)>NavigationControls.StickDeadZone || Math.Abs(NativeNavigationInput.Physical.MoveY)>NavigationControls.StickDeadZone);
                context.InputOrientationDegrees=analog && (stick?Configuration.AnalogControl.UseAbsoluteOrientationStick:Configuration.AnalogControl.UseAbsoluteOrientationKeys)?FF9StateSystem.Field.twist.x:FF9StateSystem.Field.twist.y;
            }
        }
        private static MovementObservation ReadMovement(NavigationContext context,CatalogResult catalog)
        {
            var result=_pending;
            // Keep a one-shot terminal result when a movement command defers it.
            // Status/repeat can consume it immediately; I may leave movement active.
            if(result==null || !result.IsTerminal || _coordinator.Session.Mode!=RouteMode.Moving)_pending=null;
            if(_planning!=RouteOperation.None)return new MovementObservation{IsPlanning=true,Notice=result==null?null:result.Notice};
            if(result!=null)return result;
            if(_route==null || _route.Generation!=context.Generation || !_route.Transport.Equals(context.Transport))return null;
            result=new MovementObservation{RouteAvailable=_route.Success,PlannedRoute=_route};
            if(_coordinator.Session.Mode==RouteMode.Guiding && context.CanControl && _routeTarget!=null) {
                var fresh=catalog.Find(_routeTarget.Id);
                if(fresh==null)return result;
                if(context.LogicalMapKey.StartsWith("field:")) {
                    var geometry=NativeFieldGeometry.Read(context);
                    bool segmentClear=!_route.Success || _guideProgress==null || geometry==null || geometry.IsSegmentClear(context.Position,_guideProgress.Next(context.Position));
                    result.Outcome=GuidanceRouteValidation.Check(_route,_routeTarget,fresh,geometry==null?null:geometry.Fingerprint,geometry==null?1:geometry.Radius,false,segmentClear);
                }else {
                    var geometry=NativeWorldGeometry.Read(context,fresh);
                    bool segmentClear=!_route.Success || _guideProgress==null || GuidanceRouteValidation.WorldLeg(geometry,context,_guideProgress.Next(context.Position),fresh,_route.ApproachPoint);
                    result.Outcome=GuidanceRouteValidation.Check(_route,_routeTarget,fresh,NativeWorldGeometry.CurrentFingerprint(),0.5f,true,segmentClear);
                }
                if(result.Outcome!=MovementOutcome.None){result.RouteAvailable=false;return result;}
            }
            if(_routeTarget!=null && _coordinator.Session.Mode==RouteMode.Moving) {
                var fresh=catalog.Find(_routeTarget.Id);
                if(fresh!=null && fresh.PhysicalIdentity.Kind==TargetKind.FieldActor) {
                    var geometry=NativeFieldGeometry.Read(context);
                    if(GuidanceRouteValidation.FieldApproachChanged(_routeTarget,fresh) || geometry!=null && NavigationPoint.Distance(fresh.Position,_routeTarget.Position)>geometry.Radius){ReleaseOwned(StopReason.TargetChanged);result.Outcome=MovementOutcome.TargetMoved;return result;}
                }
                if(context.LogicalMapKey.StartsWith("world:")) {
                    var outcome=NativeWorldMovement.ReadOutcome(context,fresh);result.Reason=outcome.Reason;result.Evidence=outcome.Evidence;
                    result.Outcome=outcome.Outcome==WorldOutcome.Moving?MovementOutcome.Moving:outcome.Outcome==WorldOutcome.TargetMoved?MovementOutcome.TargetMoved:outcome.Outcome==WorldOutcome.ApproachReached || outcome.Outcome==WorldOutcome.Arrived?MovementOutcome.ApproachReached:outcome.Outcome==WorldOutcome.Interrupted?MovementOutcome.Interrupted:MovementOutcome.Unavailable;
                }else {
                    var outcome=NativeFieldMovement.ReadOutcome(context,fresh);result.Reason=outcome.Reason;result.Evidence=outcome.Evidence;
                    result.Outcome=outcome.Outcome==FieldOutcome.Moving?MovementOutcome.Moving:outcome.Outcome==FieldOutcome.ReadyToInteract?MovementOutcome.ReadyToInteract:outcome.Outcome==FieldOutcome.TurnNeeded?MovementOutcome.TurnNeeded:outcome.Outcome==FieldOutcome.ArrivedExit?MovementOutcome.ArrivedExit:outcome.Outcome==FieldOutcome.ApproachReached?MovementOutcome.ApproachReached:outcome.Outcome==FieldOutcome.Interrupted?MovementOutcome.Interrupted:MovementOutcome.Unavailable;
                }
            }
            if(_route.Success && _route.Waypoints.Length>0) {
                var native=context.LogicalMapKey.StartsWith("world:")?NativeWorldMovement.CurrentWaypoint:NativeFieldMovement.CurrentWaypoint();
                result.RoutePoint=native??(_guideProgress==null?_route.Waypoints[0]:_guideProgress.Next(context.Position));
            }
            return result;
        }
        private static void CancelWorldPlan(){_worldPlanner.Cancel();_worldRequest=0;}
        private static void BeginPlan(RouteOperation operation,NavigationTarget target){CancelWorldPlan();_planTimer=System.Diagnostics.Stopwatch.StartNew();_planning=operation;_routeTarget=target;_planningAttempts=0;_route=null;_pending=null;}
        private static void ApplyPlan(NavigationContext context,NavigationTarget target,bool world,bool automatic)
        {
            if(target.Category==NavigationCategory.Story && !target.PhysicalId.HasValue) {
                CancelWorldPlan();_planning=RouteOperation.None;_route=null;
                _pending=new MovementObservation{Outcome=MovementOutcome.Unavailable,Evidence=target.Availability,Notice=target.Availability};return;
            }
            RoutePlan route;
            bool requestedMovement=_planning==RouteOperation.StartAutomatic || _planning==RouteOperation.ReplanAutomatic;
            if(world && target.PhysicalIdentity.Kind==TargetKind.Vehicle && EIcon.NavigationPresentedUid==target.PhysicalIdentity.NativeId) {
                CancelWorldPlan();_planning=RouteOperation.None;_route=null;
                _pending=new MovementObservation{Outcome=MovementOutcome.ApproachReached,Notice=requestedMovement?null:"Selected vehicle interaction is presented. Use the ordinary game action to board."};
                Trace("Vehicle interaction presented; target="+target.Id+"; player="+context.PlayerIdentity);return;
            }
            if(world) {
                if(_worldRequest!=0) {
                    WorldPlanResult completed;if(!_worldPlanner.TryTake(_worldRequest,out completed))return;
                    long finished=_worldRequest;_worldRequest=0;
                    if(!completed.IsCurrent(context,target,NativeWorldGeometry.CurrentFingerprint())) {
                        Trace("World route completion rejected as stale; request="+finished);BeginPlan(_planning,target);return;
                    }
                    route=completed.Route;
                    Trace("World route search completed; request="+finished+"; search_ms="+completed.ElapsedMilliseconds+"; request_ms="+_planTimer.ElapsedMilliseconds);
                    if(route==null)route=new RoutePlan{Generation=context.Generation,TargetId=target.Id,Transport=context.Transport,Failure=RouteFailure.GeometryUnavailable,GeometryFingerprint=NativeWorldGeometry.CurrentFingerprint()};
                    if(completed.Error!=null)Trace("World route worker unavailable; request="+finished+"; error="+completed.Error);
                }else {
                    var geometry=NativeWorldGeometry.Read(context,target);
                    if(geometry!=null && !geometry.Complete && NativeWorldGeometry.IsPreparing && _planTimer.ElapsedMilliseconds<15000){if(++_planningAttempts==1)_pending=new MovementObservation{Notice="World route data is loading. Movement waiting."};return;}
                    if(geometry!=null && geometry.Complete) {
                        long request=++_nextWorldRequest;
                        if(_worldPlanner.TryStart(request,context,target,geometry)) {_worldRequest=request;Trace("World route search queued; request="+request+"; target="+target.Id+"; wait_ms="+_planTimer.ElapsedMilliseconds);}
                        return;
                    }
                    route=new RoutePlan{Generation=context.Generation,TargetId=target.Id,Transport=context.Transport,Failure=RouteFailure.TerrainUnavailable,GeometryFingerprint=NativeWorldGeometry.CurrentFingerprint()};
                }
            }else {
                var geometry=NativeFieldGeometry.Read(context);route=new FieldRoutePlanner().Plan(context,target,geometry);
                NativeFieldGeometry.CaptureFailure(context,target,geometry,route);
            }
            _planning=RouteOperation.None;
            _route=route;_routeTarget=target;
            if(route.Success){var field=world?null:NativeFieldGeometry.Read(context);_guideProgress=new RouteGuidanceProgress(context.Position,route.Waypoints,world,world?0.5f:field==null?1:field.Radius);}
            Trace("Route "+target.Id+"; success="+route.Success+"; failure="+route.Failure+"; points="+route.Waypoints.Length+"; fingerprint="+route.GeometryFingerprint);
            if(!route.Success) {_pending=new MovementObservation{Outcome=MovementOutcome.Unavailable,RouteFailure=route.Failure,Evidence=RouteReason(route.Failure),Notice=requestedMovement?null:"Route unavailable: "+RouteReason(route.Failure)+". Direct guidance available."};return;}
            if(!requestedMovement || !automatic)return;
            _owner=++_nextOwner;var result=world?NativeWorldMovement.TryStart(context,target,route,_owner):NativeFieldMovement.TryStart(context,target,route,_owner);
            _pending=result==MovementStartResult.Started?new MovementObservation{Outcome=MovementOutcome.Moving,RouteAvailable=true,PlannedRoute=route,RoutePoint=route.Waypoints[0],Notice="Automatic movement started toward "+target.Label+"."}:new MovementObservation{Outcome=MovementOutcome.Unavailable,Evidence="native follower "+result};
            Trace("Movement start "+result+"; owner="+_owner+"; request_ms="+_planTimer.ElapsedMilliseconds);
        }
        public static string RouteReason(RouteFailure failure)
        {
            switch(failure){case RouteFailure.TerrainUnavailable:return "current terrain data is unavailable";case RouteFailure.Disconnected:return "no connected route";case RouteFailure.NarrowPassage:return "passage too narrow for the player";case RouteFailure.TargetOutsideMesh:return "no reachable target approach";case RouteFailure.StartOutsideMesh:return "player is outside the current movement mesh";case RouteFailure.StaleContext:return "scene changed while planning";case RouteFailure.UnsupportedTransport:return "current transport is unsupported";case RouteFailure.GeometryUnavailable:return "current movement geometry is unavailable";default:return "native movement constraints rejected the route";}
        }
        private static void ReleaseOwned(StopReason reason){if(_owner!=0){NativeFieldMovement.Release(_owner,reason);NativeWorldMovement.Release(_owner,reason);Trace("Movement release "+reason+"; owner="+_owner);_owner=0;}}
        public static void DetachScene(StopReason reason)
        {
            if(reason==StopReason.MapChanged || reason==StopReason.SaveChanged || reason==StopReason.Battle || reason==StopReason.NativeClear) {
                _minigameInspection=false;NativeChocoboHotCold.Reset();
            }
            CancelWorldPlan();
            ReleaseOwned(reason);
            if(reason==StopReason.MapChanged || reason==StopReason.SaveChanged || reason==StopReason.Battle || reason==StopReason.TransportChanged || reason==StopReason.TargetChanged || reason==StopReason.TargetLost){_route=null;_routeTarget=null;_guideProgress=null;_planning=RouteOperation.None;_pending=null;_announcer.ResetPending();}
            else if(reason!=StopReason.ManualMovement)_planning=RouteOperation.None;
        }
        internal static void Trace(string message)
        {
            try{string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FF9Access");Directory.CreateDirectory(path);File.AppendAllText(Path.Combine(path,"navigation.log"),DateTime.UtcNow.ToString("o")+" "+message+Environment.NewLine);}catch(Exception exception){Log.Message("[FF9Access] Navigation trace unavailable: {0}",exception.Message);}
        }
    }
}
