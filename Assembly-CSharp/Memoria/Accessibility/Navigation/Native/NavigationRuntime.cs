using System;
using System.IO;
using System.Collections.Generic;
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
        private static readonly OwnedFieldPlanner _fieldPlanner = new OwnedFieldPlanner();
        private static long _worldRequest,_nextWorldRequest;
        private static System.Diagnostics.Stopwatch _planTimer;
        private static RouteOperation _planning;private static MovementObservation _pending;private static bool _quietGuidancePlan;
        private static RouteFailure _priorGuidanceFailure;
        private static bool _exitReached,_minigameInspection;private static string _traceContext,_traceSpeech,_error;
        private static long _diagnosticGeneration;private static readonly HashSet<string> _diagnostics=new HashSet<string>();
        static NavigationRuntime(){NavigationLifecycleBridge.Releasing+=DetachScene;NativeFieldMovement.ExitReached+=delegate{_exitReached=true;};}
        public static void Tick()
        {
            if(_frame==Time.frameCount)return;_frame=Time.frameCount;
            NavigationContext context=null;
            try {
                NativeNavigationInput.EnsureUpdated();context=NativeNavigationContext.Read();
                NativeNumericDialogs.Tick(context.Foreground && !context.MenuOpen);
                NativeObservedFieldExits.Arrive(context);
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
                if(context.MenuOpen) {
                    // Native menu input and speech must not pay for field/world navigation work.
                    // Loading is a separate state: its existing geometry preparation stays enabled.
                    var paused=_coordinator.Tick(context,NativeNavigationInput.TakeCommands(),null,null,Configuration.Accessibility.ControllerNaming);
                    if(paused.Session.ReleaseMovement)DetachScene(paused.Session.Reason);
                    _announcer.ResetPending();NavigationLifecycleBridge.Acknowledge(context);return;
                }
                NativeChocoboHotCold.DrainResults(context);
                bool world=context.LogicalMapKey!=null && context.LogicalMapKey.StartsWith("world:");
                if(world && context.Foreground)NativeWorldGeometry.Warm(context);
                ReadPresentation(context,world);
                CatalogResult catalog=world?WorldMapAccessibilityManager.Instance.ReadTargets(context):AccessibleNavigationManager.Instance.ReadTargets(context);
                if(_diagnosticGeneration!=context.Generation){_diagnostics.Clear();_diagnosticGeneration=context.Generation;}
                foreach(string diagnostic in catalog.Diagnostics)if(_diagnostics.Count<64 && _diagnostics.Add(diagnostic))Trace("Target diagnostic; map="+context.LogicalMapKey+"; "+diagnostic);
                NativeFieldExitCues.Tick(context,catalog);
                if(!NativeOpeningPositionTargets.TryAppend(context,catalog))NativeStoryTargets.Append(context,catalog);
                var movement=ReadMovement(context,catalog);
                if(movement!=null && movement.Outcome==MovementOutcome.Interrupted)Trace("Movement interrupted; reason="+movement.Reason+"; evidence="+movement.Evidence);
                var commands=NativeNavigationInput.TakeCommands();var update=_coordinator.Tick(context,commands,catalog,movement,Configuration.Accessibility.ControllerNaming);
                if(_pending==movement && update.Session.Mode!=RouteMode.Moving)_pending=null;
                foreach(var command in commands) {
                    Trace("Command "+command+"; map="+context.LogicalMapKey+"; generation="+context.Generation+"; mode="+update.Session.Mode);
                    if(command==NavigationCommand.StartAutomatic || command==NavigationCommand.ToggleAutomatic || command==NavigationCommand.StartGuidance || command==NavigationCommand.ToggleGuidance || command==NavigationCommand.Status)TraceSelection(update,catalog);
                    if(command==NavigationCommand.DecreaseInterval || command==NavigationCommand.IncreaseInterval) {
                        Configuration.Accessibility.NavigationIntervalMilliseconds+=command==NavigationCommand.IncreaseInterval?500:-500;
                        Configuration.Accessibility.SaveValues();update.Messages.Add("Guidance interval "+(Configuration.Accessibility.NavigationIntervalMilliseconds/1000f).ToString("0.0")+" seconds.");
                    }
                }
                if(update.Session.ReleaseMovement)DetachScene(update.Session.Reason);
                if(!String.IsNullOrEmpty(NativeNavigationInput.Diagnostic))update.Diagnostics.Add(NativeNavigationInput.Diagnostic);
                if(_exitReached && context.CanControl){update.Messages.Add("Selected exit reached. "+context.CurrentLocation+".");_exitReached=false;}
                if(update.RouteOperation!=RouteOperation.None && update.Selected!=null && context.CanControl)BeginPlan(update.RouteOperation,update.Selected,update.RefreshGuidance);
                if (!world && _planning != RouteOperation.None && update.Session.Mode != RouteMode.Guiding && update.Session.Mode != RouteMode.Moving)
                {
                    _fieldPlanner.Cancel();
                    _planning = RouteOperation.None;
                }
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
                string message=exception.GetType().FullName+": "+exception.Message;
                if(_error!=message){_error=message;Trace(message);Log.Message("[FF9Access] Navigation exception: {0}",exception);if(context!=null && context.CanInspect)ScreenReaderManager.Instance.SpeakInterrupt("Navigation is temporarily unavailable. Try again when the scene is ready.");}
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
                bool analog=Configuration.AnalogControl.Enabled;
                bool stick=NativeNavigationInput.Physical!=null && (Math.Abs(NativeNavigationInput.Physical.MoveX)>NavigationControls.StickDeadZone || Math.Abs(NativeNavigationInput.Physical.MoveY)>NavigationControls.StickDeadZone);
                context.InputOrientationDegrees=FieldInputOrientation.Resolve(context.FieldInputBasis,stick,analog,Configuration.AnalogControl.UseAbsoluteOrientationStick,Configuration.AnalogControl.UseAbsoluteOrientationKeys,FF9StateSystem.Field.twist.x,FF9StateSystem.Field.twist.y);
            }
        }
        private static MovementObservation ReadMovement(NavigationContext context,CatalogResult catalog)
        {
            var result=_pending;
            // Keep a one-shot terminal result when a movement command defers it.
            // Status/repeat can consume it immediately; I may leave movement active.
            if(result==null || !result.IsTerminal || _coordinator.Session.Mode!=RouteMode.Moving)_pending=null;
            if(_planning!=RouteOperation.None)return new MovementObservation{IsPlanning=true,QuietPlanning=_quietGuidancePlan,Notice=_quietGuidancePlan || result==null?null:result.Notice};
            if(result!=null)return result;
            if(_route==null || _route.Generation!=context.Generation || !_route.Transport.Equals(context.Transport))return null;
            result=new MovementObservation{RouteAvailable=_route.Success,PlannedRoute=_route};
            if(_coordinator.Session.Mode==RouteMode.Guiding && context.CanControl && _routeTarget!=null) {
                var fresh=catalog.Find(_routeTarget.Id);
                if(fresh==null)return result;
                if(context.LogicalMapKey.StartsWith("field:")) {
                    var geometry=NativeFieldGeometry.Read(context);
                    if(_route.Success && _guideProgress!=null && geometry!=null) {
                        result.ManualStep=_guideProgress.Read(context,_route,p=>CanGuideFieldStep(context,geometry,p));result.RoutePoint=result.ManualStep.Aim;
                    }
                    bool segmentClear=!_route.Success || result.ManualStep!=null && !result.ManualStep.NeedReplan;
                    result.Outcome=GuidanceRouteValidation.Check(_route,_routeTarget,fresh,geometry==null?null:geometry.Fingerprint,geometry==null?1:geometry.Radius,false,segmentClear);
                }else {
                    var geometry=NativeWorldGeometry.Read(context,fresh);
                    if(_route.Success && _guideProgress!=null && geometry!=null) {
                        result.ManualStep=_guideProgress.Read(context,_route,p=>CanGuideWorldStep(context,geometry,p,fresh,_route.ApproachPoint));result.RoutePoint=result.ManualStep.Aim;
                    }
                    bool segmentClear=!_route.Success || result.ManualStep!=null && !result.ManualStep.NeedReplan;
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
                if(result.ManualStep!=null)return result;
                var tracked=_guideProgress==null?_route.Waypoints[0]:_guideProgress.Next(context.Position);
                var native=context.LogicalMapKey.StartsWith("world:")?NativeWorldMovement.CurrentWaypoint:NativeFieldMovement.CurrentWaypoint();
                if(native.HasValue && _guideProgress!=null)_guideProgress.ObserveWaypoint(native.Value);
                result.RoutePoint=native??tracked;
            }
            return result;
        }
        private static bool CanGuideFieldStep(NavigationContext context,FieldGeometrySnapshot geometry,NavigationPoint point){return geometry!=null && geometry.IsSegmentClear(context.Position,point);}
        private static bool CanGuideWorldStep(NavigationContext context,WorldGeometrySnapshot geometry,NavigationPoint point,NavigationTarget target,NavigationPoint approach){return GuidanceRouteValidation.WorldLeg(geometry,context,point,target,approach);}
        private static void CancelWorldPlan(){_worldPlanner.Cancel();_worldRequest=0;}
        private static void BeginPlan(RouteOperation operation,NavigationTarget target,bool quiet=false)
        {
            if(!quiet || _planning==RouteOperation.None)
                _priorGuidanceFailure=quiet && _route!=null && _route.PhysicalIdentity.Equals(target.PhysicalIdentity)?_route.Failure:RouteFailure.None;
            CancelWorldPlan();_fieldPlanner.Cancel();_planTimer=System.Diagnostics.Stopwatch.StartNew();_planning=operation;_quietGuidancePlan=quiet;
            _routeTarget=target;_planningAttempts=0;_route=null;_pending=null;
        }
        private static void ApplyPlan(NavigationContext context,NavigationTarget target,bool world,bool automatic)
        {
            if(target.Category==NavigationCategory.Story && !target.PhysicalId.HasValue) {
                CancelWorldPlan();_fieldPlanner.Cancel();_planning=RouteOperation.None;_route=null;
                _pending=new MovementObservation{Outcome=MovementOutcome.Unavailable,Evidence=target.Availability,Notice=target.Availability};return;
            }
            RoutePlan route;
            FieldGeometrySnapshot field = null;
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
                        Trace("World route completion rejected as stale; request="+finished);BeginPlan(_planning,target,_quietGuidancePlan);return;
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
                field = NativeFieldGeometry.Read(context);
                if (field == null)
                {
                    _fieldPlanner.Cancel();
                    route = new RoutePlan
                    {
                        TargetId = target.Id, PhysicalId = target.PhysicalId, Generation = context.Generation,
                        Transport = context.Transport, Failure = RouteFailure.GeometryUnavailable
                    };
                }
                else
                {
                    FieldPlanResult completed;
                    if (!_fieldPlanner.TryTake(out completed))
                    {
                        if (_fieldPlanner.TryStart(context, target, field)) ReleaseOwned(StopReason.Explicit);
                        return;
                    }
                    if (!completed.IsCurrent(context, target, field))
                    {
                        if (_planningAttempts++ == 0) Trace("Field route completion rejected after live route inputs changed.");
                        return;
                    }
                    route = completed.Route;
                    Trace("Field route search completed; search_ms=" + completed.ElapsedMilliseconds + "; request_ms=" + _planTimer.ElapsedMilliseconds);
                    if (completed.Error != null) Trace("Field route worker unavailable; error=" + completed.Error);
                    NativeFieldGeometry.CaptureFailure(completed.Origin, completed.Target, completed.Geometry, route);
                }
            }
            _planning=RouteOperation.None;
            _route=route;_routeTarget=target;
            if(route.Success)_guideProgress=new RouteGuidanceProgress(route.Origin,route.Waypoints,world,world?0.5f:field==null?1:field.Radius);
            Trace("Route "+target.Id+"; success="+route.Success+"; failure="+route.Failure+"; points="+route.Waypoints.Length+"; fingerprint="+route.GeometryFingerprint);
            if(!route.Success) {
                string reason=RouteReason(route.Failure);
                bool repeat=_quietGuidancePlan && _priorGuidanceFailure==route.Failure;
                _pending=new MovementObservation{Outcome=MovementOutcome.Unavailable,RouteFailure=route.Failure,PlannedRoute=route,
                    Evidence=reason,Notice=requestedMovement || repeat?null:"Route unavailable: "+reason+"."};
                return;
            }
            if(!requestedMovement || !automatic)return;
            _owner=++_nextOwner;var result=world?NativeWorldMovement.TryStart(context,target,route,_owner):NativeFieldMovement.TryStart(context,target,route,_owner);
            if(result!=MovementStartResult.Started)Trace("Native movement start refused: "+result);
            _pending=result==MovementStartResult.Started?new MovementObservation{Outcome=MovementOutcome.Moving,RouteAvailable=true,PlannedRoute=route,RoutePoint=route.Waypoints[0],Notice="Automatic movement started toward "+target.Label+"."}:new MovementObservation{Outcome=MovementOutcome.Unavailable,Evidence=MovementStartMessage.Read(result)};
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
            if (reason != StopReason.ManualMovement) _fieldPlanner.Cancel();
            ReleaseOwned(reason);
            if(reason==StopReason.MapChanged || reason==StopReason.SaveChanged || reason==StopReason.Battle || reason==StopReason.TransportChanged || reason==StopReason.TargetChanged || reason==StopReason.TargetLost){_route=null;_routeTarget=null;_guideProgress=null;_planning=RouteOperation.None;_pending=null;_announcer.ResetPending();}
            else if(reason!=StopReason.ManualMovement)_planning=RouteOperation.None;
        }
        internal static void Trace(string message)
        {
            try{string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FF9Access");Directory.CreateDirectory(path);File.AppendAllText(Path.Combine(path,"navigation.log"),DateTime.UtcNow.ToString("o")+" "+message+Environment.NewLine);}catch(Exception exception){Log.Message("[FF9Access] Navigation trace unavailable: {0}",exception.Message);}
        }
        private static void TraceSelection(NavigationUpdate update,CatalogResult catalog)
        {
            var target=update.Selected;
            Trace("Selection; target="+(target==null?"none":target.Id.ToString())+"; physical="+(target==null?"none":target.PhysicalIdentity.ToString())+"; availability="+(target==null?"":target.Availability));
            int count=0;
            foreach(var exit in catalog.Targets)if(exit.Category==NavigationCategory.Exits && count++<8) {
                var fields=new List<string>();foreach(int field in exit.NextFields??new int[0])fields.Add(field.ToString());
                Trace("Local exit; target="+exit.Id+"; approaches="+(exit.ApproachCandidates==null?-1:exit.ApproachCandidates.Length)+"; next="+String.Join(",",fields.ToArray())+"; evidence="+exit.Evidence+"; availability="+exit.Availability);
            }
        }
    }
}
