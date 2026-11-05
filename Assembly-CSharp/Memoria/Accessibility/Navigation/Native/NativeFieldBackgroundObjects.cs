using System;
using System.Collections.Generic;
using UnityEngine;
namespace Memoria.Accessibility.Navigation
{
    public static partial class NativeFieldTargets
    {
        // Integration hook (NativeFieldTargets.Read, !ContainsJump(touch) branch):
        //     var storyRegion=ReadStoryRegion(...);
        //     if(storyRegion!=null){LabelReviewedRegion(context,field,quad,storyRegion);result.Add(storyRegion);}
        //     else { (exits owner's ReadIndirectExit first, if present)
        //         var background=ReadBackgroundObject(context,engine,field,player,quad,ref currentGeometry,ref geometryRead);
        //         if(background!=null)result.Add(background);
        //         else if(quadIcon)result.Add(PresentedPrompt(ObservedQuad(context,engine,quad,center)));
        //     }
        // A reviewed background object (FieldBackgroundObjects) is listed with its drawn form while its art is
        // on screen in a reviewed camera of the same scene and its single live region may be requested at
        // Confirm; approaches are walkable points where it is the first Talk region. Reads only.
        internal static FieldTargetObservation ReadBackgroundObject(NavigationContext context,EventEngine engine,FieldMap field,Actor player,Quad quad,ref FieldGeometrySnapshot geometry,ref bool geometryRead)
        {
            try {
                int map=FF9StateSystem.Common.FF9.fldMapNo;
                if(context==null || !context.CanControl || engine==null || engine.gMode!=1 || field==null || field.scene==null || field.scene.ebgBin==null || player==null || player.uid!=context.PlayerIdentity ||
                    quad==null || quad.ebData==null || quad.q==null || quad.n<3 || quad.n>quad.q.Length || !FieldBackgroundObjects.HasField(map))return null;
                string hash=Fingerprint(quad.ebData);
                FieldBackgroundObject row=null;
                foreach(var candidate in FieldBackgroundObjects.All)if(candidate.Field==map && candidate.RegionSid==quad.sid && candidate.RegionHash==hash && context.LogicalMapKey=="field:"+map+":"+candidate.Scene)row=candidate;
                if(row==null || row.GroundCue && quad.sid!=quad.uid)return null;
                var polygon=new NavigationPoint[quad.n];Vector3 center=Vector3.zero;
                for(int i=0;i<quad.n;i++){var p=quad.q[i].Vector3Val;polygon[i]=Point(p);center+=p;}center/=quad.n;
                var live=new FieldBackgroundPresence{CanControl=context.CanControl,
                    SceneMatches=Fingerprint(field.scene.ebgBin)==row.Background && CurrentBgiHash(field)==row.Walkmesh,
                    CameraMatches=Array.IndexOf(row.Cameras,field.camIdx)>=0,ArtOnScreen=row.GroundCue || StorySurfacePresented(field,Point(center)),
                    RegionLevel=quad.level,RegionTalkable=row.TouchCue?EventCollision.NavigationCanTriggerQuad(player,quad):NativeFieldTalkability.IsTalkable(quad),
                    MapTalkPermitted=row.TouchCue || FieldInteractionBindings.QuadTalkPermitted(map,quad.sid),Approaches=1};
                if(row.LandmarkSid>=0)live.LandmarkRendered=RenderedLandmark(engine,field,row);
                if(row.GroundCue)live.CurrentGroundCue=EIcon.NavigationPresentedUid==quad.uid ||
                    FieldInteractionGuard.Evaluate(Decode(engine,quad,EventEngine.tagPush),
                        new ScriptSnapshot(FF9StateSystem.EventState.gEventGlobal,engine.GetMapVar(),ScriptSnapshot.InstanceBytes(quad.buffer,quad.vofs)){UserControl=true},
                        _exitPromptOpcodes,true)==FieldInteractionState.Available;
                foreach(var obj in ActiveObjects(engine)){var other=obj as Quad;if(other!=null && other.sid==row.RegionSid && other.ebData!=null && Fingerprint(other.ebData)==hash)live.LiveRegions++;}
                // Cheap native gates first; the guard and geometry only for a still-presented row.
                if(!FieldBackgroundObjects.Present(row,live))return null;
                live.TalkGuard=row.TouchCue?FieldInteractionState.Available:NativeFieldActorInteractions.QuadTalkGuard(context,engine,quad);
                if(!FieldBackgroundObjects.Present(row,live))return null;
                if(!geometryRead){geometry=NativeFieldGeometry.Read(context);geometryRead=true;}
                if(geometry==null)return null;
                var approaches=new List<NavigationPoint>();
                foreach(var point in Surface(geometry,quad).Approaches)
                    if(point.IsFinite && (row.TouchCue?engine.NavigationTouchQuadAt(point.X,point.Z):engine.NavigationTalkQuadAt(point.X,point.Z))==quad &&
                        StorySurfacePresented(field,point))approaches.Add(point);
                live.Approaches=approaches.Count;
                if(!FieldBackgroundObjects.Present(row,live))return null;
                approaches.Sort((a,b)=>NavigationPoint.Distance(context.Position,a).CompareTo(NavigationPoint.Distance(context.Position,b)));
                var position=row.GroundCue?approaches[0]:Point(center);var support=geometry.At(position);
                if(support!=null)position=new NavigationPoint(position.X,support.Height(position),position.Z);
                var observation=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,quad.uid),
                    NativePresentationIdentity=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(quad),Position=position,Visible=true,
                    ShowsIcon=EIcon.NavigationPresentedUid==quad.uid && OnScreen(field,Vector(context.Position)),HasInteraction=true,
                    InteractionUid=quad.uid,InteractionRegion=polygon,ApproachCandidates=approaches.ToArray(),UseApprovedApproaches=true,VisibleLabel=row.Label};
                observation.LabelIdentity=ExitLabelSignature(quad);
                // Story evidence of the Confirm handler this object offers.
                Annotate(context,engine,quad,observation,row.TouchCue?EventEngine.tagPush:EventEngine.tagTalk);
                if(_cueLogs.Count<64 && _cueLogs.Add("background:"+quad.uid+":"+field.camIdx))
                    Memoria.Prime.Log.Message("[FF9Access] Background object presented: map={0}; uid={1}; sid={2}; camera={3}; label={4}; approaches={5}",context.LogicalMapKey,quad.uid,quad.sid,field.camIdx,row.Label,approaches.Count);
                return observation;
            }catch(Exception exception){Memoria.Prime.Log.Message("[FF9Access] Background object unavailable: {0}",exception.Message);return null;}
        }
        internal static FieldBackgroundObject FindBackgroundTouchCue(NavigationContext context,Quad quad)
        {
            int map=FF9StateSystem.Common.FF9.fldMapNo;
            if((map!=1817 && map!=1818) || context==null || quad==null || quad.ebData==null)return null;
            string prefix="field:"+map+":";
            if(context.LogicalMapKey==null || !context.LogicalMapKey.StartsWith(prefix))return null;
            var row=FieldBackgroundObjects.Find(map,context.LogicalMapKey.Substring(prefix.Length),quad.sid,Fingerprint(quad.ebData));
            return row!=null && row.TouchCue && quad.uid==quad.sid?row:null;
        }
        internal static InteractionReadiness ReadBackgroundTouchReadiness(NavigationContext context,EventEngine engine,Actor player,Quad quad)
        {
            var row=FindBackgroundTouchCue(context,quad);
            if(row==null || player==null || player.fieldMapActorController==null)return null;
            var field=player.fieldMapActorController.fieldMap;
            if(field==null || field.scene==null || field.scene.ebgBin==null || Fingerprint(field.scene.ebgBin)!=row.Background ||
                CurrentBgiHash(field)!=row.Walkmesh || Array.IndexOf(row.Cameras,field.camIdx)<0)return new InteractionReadiness();
            bool selected=engine.TreadQuad(player,2)==quad;
            var result=FieldInteractionRules.PushReadiness(selected,engine.NavigationContainsQuad(quad,context.Position.X,context.Position.Z),
                EventCollision.NavigationHasTouchFacing(player,quad),EIcon.NavigationPresentedUid==quad.uid);
            result.Evidence="The original lift prompt is displayed. Confirm remains player controlled.";
            return result;
        }
        private static bool RenderedLandmark(EventEngine engine,FieldMap field,FieldBackgroundObject row)
        {
            var actor=engine.GetObjUID(row.LandmarkSid) as Actor;
            if(actor==null || actor.sid!=row.LandmarkSid || actor.model!=row.LandmarkModel || actor.ebData==null || Fingerprint(actor.ebData)!=row.LandmarkHash ||
                actor.go==null || !actor.go.activeInHierarchy || !actor.isEnableRenderer || !actor.frontCamera || (actor.flags&EventEngine.flagShow)==0)return false;
            var position=actor.fieldMapActorController==null?new Vector3(actor.pos[0],actor.pos[1],actor.pos[2]):actor.fieldMapActorController.curPos;
            if(!OnScreen(field,position) && !StorySurfacePresented(field,Point(position)))return false;
            foreach(var renderer in actor.go.GetComponentsInChildren<Renderer>()) {
                if(!renderer.enabled || !renderer.gameObject.activeInHierarchy || actor.fieldMapActor!=null && renderer==actor.fieldMapActor.shadowMeshRenderer)continue;
                foreach(var material in renderer.sharedMaterials)if(material!=null && (!material.HasProperty("_Color") || material.GetColor("_Color").a>0))return true;
            }
            return false;
        }
        private static void RemoveDuplicateBackgroundLandmark(EventEngine engine,List<FieldTargetObservation> observations)
        {
            int field=FF9StateSystem.Common.FF9.fldMapNo;if(field<1405 || field>1415)return;
            foreach(var row in FieldBackgroundObjects.All)if(row.Field==field && row.LandmarkSid>=0 &&
                observations.Exists(o=>o.Id.Kind==TargetKind.FieldActor && o.Id.NativeId==row.RegionSid && o.InteractionUid==row.RegionSid && o.VisibleLabel==row.Label)) {
                var actor=engine.GetObjUID(row.LandmarkSid) as Actor;
                if(actor!=null && actor.sid==row.LandmarkSid && actor.model==row.LandmarkModel && actor.ebData!=null && Fingerprint(actor.ebData)==row.LandmarkHash)
                    observations.RemoveAll(o=>o.Id.Kind==TargetKind.FieldActor && o.Id.NativeId==actor.uid);
            }
        }
        // A Confirm region whose own bubble is presented but whose object is not reviewed (hidden pickups,
        // unidentified signs): an honest interaction point, never a guessed name or reward.
        internal static FieldTargetObservation PresentedPrompt(FieldTargetObservation observation)
        {
            if(observation!=null)observation.VisibleLabel=FieldBackgroundObjects.PromptLabel(observation.VisibleLabel);
            return observation;
        }
        // A story region whose object is a reviewed background object takes its drawn form as its label
        // (for example the Gargan Roo lever), on the reviewed scene and camera only.
        internal static void LabelReviewedRegion(NavigationContext context,FieldMap field,Quad quad,FieldTargetObservation observation)
        {
            try {
                int map=FF9StateSystem.Common.FF9.fldMapNo;
                string prefix="field:"+map+":";
                if(context==null || context.LogicalMapKey==null || !context.LogicalMapKey.StartsWith(prefix) || observation==null || field==null || field.scene==null || field.scene.ebgBin==null ||
                    quad==null || quad.ebData==null || !FieldBackgroundObjects.HasField(map))return;
                var row=FieldBackgroundObjects.Find(map,context.LogicalMapKey.Substring(prefix.Length),quad.sid,Fingerprint(quad.ebData));
                if(row==null || Array.IndexOf(row.Cameras,field.camIdx)<0 ||
                    Fingerprint(field.scene.ebgBin)!=row.Background || CurrentBgiHash(field)!=row.Walkmesh)return;
                observation.VisibleLabel=row.Label;
            }catch(Exception){}
        }
    }
}
