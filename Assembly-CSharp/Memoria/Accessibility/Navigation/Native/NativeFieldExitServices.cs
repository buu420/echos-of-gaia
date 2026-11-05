using System;
using System.Collections.Generic;
using UnityEngine;
namespace Memoria.Accessibility.Navigation
{
    public static partial class NativeFieldTargets
    {
        private static readonly FieldInteractionOpcodes _exitPromptOpcodes=new FieldInteractionOpcodes{IconOpcode=(int)EBin.event_code_binary.FICON};
        // Indirect and reviewed Confirm exits need the actual current trigger and
        // the first selected region on a visible, walkable approach. Story scenes
        // remain with ReadStoryRegion. The player operates the native prompt.
        internal static FieldTargetObservation ReadIndirectExit(NavigationContext context,EventEngine engine,FieldMap field,Actor player,Quad quad,ref FieldGeometrySnapshot geometry,ref bool geometryRead,int trigger=EventEngine.tagPush)
        {
            try {
                if(context==null || engine==null || field==null || player==null || quad==null || quad.ebData==null)return null;
                string hash=Fingerprint(quad.ebData);int map=FF9StateSystem.Common.FF9.fldMapNo;
                bool confirm=trigger==EventEngine.tagTalk;
                // Cheap native prefilter before any surface or script replay.
                var possible=FieldExitFunctions.Possible(map,quad.sid,hash,confirm);
                if(possible.Length==0)return null;
                if(confirm?(!NativeFieldTalkability.IsTalkable(quad) || !EventCollision.NavigationCanApproachTalkQuad(player,quad)):
                    !EventCollision.NavigationCanTriggerQuad(player,quad))return null;
                if(!geometryRead){geometry=NativeFieldGeometry.Read(context);geometryRead=true;}
                if(geometry==null)return null;
                string function=FieldExitFunctions.Function(map,quad.sid,hash,confirm);
                var prove=ExitProof(context,engine,quad,hash,trigger);var admitted=new List<NavigationPoint>();int proofs=0;
                var surface=Surface(geometry,quad);
                // An invariant World Map departure may open a party/menu/sound
                // sequence that the detached exit reader cannot replay. Offer
                // the native prompt, but do not give Story an unproven map edge.
                bool prompt=confirm && function==null && WorldOnly(possible) &&
                    (EIcon.NavigationPresentedUid==quad.uid ||
                     FieldInteractionGuard.Evaluate(Decode(engine,quad,EventEngine.tagPush),
                        new ScriptSnapshot(FF9StateSystem.EventState.gEventGlobal,engine.GetMapVar(),ScriptSnapshot.InstanceBytes(quad.buffer,quad.vofs)){UserControl=true},
                        _exitPromptOpcodes,true)==FieldInteractionState.Available);
                foreach(var point in surface.Approaches) {
                    if(!point.IsFinite || (confirm?engine.NavigationTalkQuadAt(point.X,point.Z):engine.NavigationTouchQuadAt(point.X,point.Z))!=quad || !StorySurfacePresented(field,point))continue;
                    if(proofs++>=ExitRoutePoints)break;
                    var evidence=prove(point);
                    if(FieldExitAdmissions.Admit(function,evidence,confirm)!=FieldExitAdmission.None ||
                        prompt && evidence.Kind==FieldExitPathKind.Unknown)admitted.Add(point);
                }
                if(admitted.Count==0)return null;
                admitted.Sort((a,b)=>NavigationPoint.Distance(context.Position,a).CompareTo(NavigationPoint.Distance(context.Position,b)));
                var presentation=new ExitPresentation{Visible=true,ApproachEnabled=true,Approach=admitted[0],ApproachCandidates=admitted.ToArray(),Evidence=VisibilityEvidence.ObservedRegion};
                int identity=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(quad)^hash.GetHashCode();
                var observation=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldExit,context.LogicalMapKey,quad.uid),NativePresentationIdentity=identity,Position=admitted[0],Exit=presentation,ContainsJump=true};
                observation.LabelIdentity=ExitLabelSignature(quad);
                // The helper already proved each point using its own current
                // request and choice path. The old direct-jump filter cannot
                // model these inputs and would discard those same approaches.
                Annotate(context,engine,quad,observation,trigger,null,false);
                if(confirm){observation.InteractionUid=quad.uid;observation.InteractionRegion=surface.Region;observation.UseApprovedApproaches=true;}
                var camera=field.GetMainCamera();
                observation.VisibleLabel=camera==null?"Exit":FieldExitLabel.Read(Point(camera.WorldToViewportPoint(Project(field,Vector(presentation.Approach)))));
                NativeObservedFieldExits.Presented(context,quad,observation.LabelIdentity);
                AnnotateExitRoute(context,engine,quad,observation,trigger);
                if(_cueLogs.Count<64 && _cueLogs.Add("indirect-exit:"+context.LogicalMapKey+":"+quad.uid))
                    Memoria.Prime.Log.Message("[FF9Access] Indirect exit presented: map={0}; uid={1}; sid={2}; function={3}; approaches={4}",context.LogicalMapKey,quad.uid,quad.sid,function??"walk",admitted.Count);
                return observation;
            }catch(Exception exception){if(_cueLogs.Add("indirect-exit-error:"+exception.GetType().Name))Memoria.Prime.Log.Message("[FF9Access] Indirect exit unavailable: {0}",exception.Message);return null;}
        }
        private static bool WorldOnly(int[] destinations)
        {foreach(int field in destinations)if(field<9000)return false;return destinations.Length>0;}
    }
}
