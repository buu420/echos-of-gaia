using System;
using System.Collections.Generic;
using UnityEngine;
namespace Memoria.Accessibility.Navigation
{
    public static partial class NativeFieldTargets
    {
        private static NavigationContext _storyGeometryContext;
        private static int _storyGeometryFrame=-1;
        private static FieldGeometrySnapshot _storyGeometry;
        private static void CacheStoryGeometry(NavigationContext context,FieldGeometrySnapshot geometry)
        {_storyGeometryContext=context;_storyGeometryFrame=Time.frameCount;_storyGeometry=geometry;}
        internal static FieldGeometrySnapshot ReadGeometryForStory(NavigationContext context)
        {
            if(!ReferenceEquals(context,_storyGeometryContext) || _storyGeometryFrame!=Time.frameCount)return NativeFieldGeometry.Read(context);
            if(_storyGeometry==null)_storyGeometry=NativeFieldGeometry.Read(context);
            return _storyGeometry;
        }
        private static void RemoveLadderDuplicate(NavigationContext context,EventEngine engine,List<FieldTargetObservation> observations)
        {
            if(context.LogicalMapKey!="field:115:FBG_N01_ALXT_MAP026_AT_SIN_0")return;
            bool bound=false;foreach(var item in observations)if(item.Id.NativeId==15 && item.VisibleLabel=="Ladder" && item.InteractionUid==15)bound=true;
            var accessory=engine.GetObjUID(8) as Actor;
            if(!bound || accessory==null || accessory.sid!=8 || accessory.model!=22 || accessory.ebData==null || Fingerprint(accessory.ebData)!="0c2b0e6895ba450010d3c015800e4f7538e3340eb1d3c85008c4a13929874224")return;
            observations.RemoveAll(item=>item.Id.Kind==TargetKind.FieldActor && item.Id.NativeId==8);
        }
        // A current story walk-in may request a director rather than jump in
        // its own handler. Read its current outcome before asking for geometry;
        // a script entry alone never supplies a target or a hidden pickup.
        // Each native trigger path is proven on its own: a Confirm step needs the native Talk gate and
        // the first Talk region (TreadQuad mode 4) at every approach; a walk-in needs the Push gate and
        // the first Push region (mode 2). A failed Talk gate never falls back to Push for a Confirm step.
        private static FieldTargetObservation ReadStoryRegion(NavigationContext context,EventEngine engine,FieldMap field,Actor player,Quad quad,ref FieldGeometrySnapshot geometry,ref bool geometryRead)
        {
            if(player==null || !NativeStoryTargets.Matches(context,quad.sid,Fingerprint(quad.ebData)))return null;
            bool confirmStep=NativeStoryTargets.HasCurrentStep(context,engine,quad.sid,Fingerprint(quad.ebData),StoryStepKind.RegionConfirm,quad.uid);
            bool walkStep=NativeStoryTargets.HasCurrentStep(context,engine,quad.sid,Fingerprint(quad.ebData),StoryStepKind.RegionWalk,quad.uid);
            bool talkGate=NativeFieldTalkability.IsTalkable(quad) && EventCollision.NavigationCanApproachTalkQuad(player,quad);
            var paths=FieldStoryRegionApproaches.Triggers(confirmStep,talkGate,EventCollision.NavigationCanTriggerQuad(player,quad));
            foreach(var path in paths) {
                int tag=FieldStoryRegionApproaches.Tag(path);
                var observation=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,quad.uid),NativePresentationIdentity=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(quad),Visible=true,HasInteraction=true};
                observation.LabelIdentity=ExitLabelSignature(quad);
                Annotate(context,engine,quad,observation,tag);
                var current=Proof(observation);
                // A handler that branches on the player's own position is proven per prospective
                // approach point (where the player stands when it fires), never at the current position.
                bool positional=ReadsPlayerPosition(Decode(engine,quad,tag));
                bool candidate=path==StoryRegionTrigger.Confirm?confirmStep:walkStep;
                if(!current.Proves && !candidate && !(positional && NativeStoryTargets.HasMilestoneBinding(context,quad.sid,Fingerprint(quad.ebData))))continue;
                int? facing=confirmStep && path==StoryRegionTrigger.Confirm?NativeStoryTargets.ReviewedApproachFacing(context,engine,quad.sid,Fingerprint(quad.ebData),quad.uid):null;
                if(!geometryRead){geometry=NativeFieldGeometry.Read(context);geometryRead=true;}
                if(geometry==null)return null;
                var surface=Surface(geometry,quad);
                var safe=FieldStoryRegionApproaches.Read(true,true,surface.Approaches,p=>FieldStoryRegionApproaches.Selects(path,engine.NavigationTalkQuadAt(p.X,p.Z),engine.NavigationTouchQuadAt(p.X,p.Z),quad),p=>StorySurfacePresented(field,p),context.Position);
                // Only points whose own detached trigger path proves the offered current step remain.
                var prove=_storyProofs.Begin(quad.uid+":"+path,context.Generation+":"+context.LogicalMapKey+":"+context.SaveIdentity+":"+context.PlayerIdentity+":"+context.StoryProgress+":"+Fingerprint(quad.ebData),surface,
                    StoryProofMemory(context,engine),Time.frameCount,point=> {
                        var prospective=new FieldTargetObservation{Id=observation.Id,NativePresentationIdentity=observation.NativePresentationIdentity,Visible=true,HasInteraction=true,LabelIdentity=observation.LabelIdentity};
                        Annotate(context,engine,quad,prospective,tag,point,true,facing);return prospective;
                    });
                var proofs=new List<StoryRegionProof>();var annotated=new List<FieldTargetObservation>();
                for(int i=0;i<safe.Length && i<StoryProofPoints;i++){var proven=prove(safe[i]);proofs.Add(Proof(proven));annotated.Add(proven);}
                var kept=FieldStoryRegionApproaches.Proven(proofs,positional?null:current,16);
                if(kept.Length==0)continue;
                var approaches=new NavigationPoint[kept.Length];for(int i=0;i<kept.Length;i++)approaches[i]=safe[kept[i]];
                CopyStory(annotated[kept[0]],observation);
                observation.Position=approaches[0];observation.ApproachCandidates=approaches;observation.UseApprovedApproaches=true;
                observation.VisibleLabel=annotated[kept[0]].VisibleLabel??"Story path";
                if(path==StoryRegionTrigger.Confirm || talkGate){observation.InteractionUid=quad.uid;observation.InteractionRegion=surface.Region;observation.VisibleLabel=annotated[kept[0]].VisibleLabel??"Interaction point";}
                // The ladder is drawn in this reviewed steeple background. Its
                // current native region/phase still decides whether it is a goal.
                if(context.LogicalMapKey=="field:115:FBG_N01_ALXT_MAP026_AT_SIN_0" && quad.sid==15 &&
                    Fingerprint(quad.ebData)=="442070aaf58c831db9bd8af7a1db98b766794821663efa9705eebf961303654e" && field.scene!=null && field.scene.ebgBin!=null &&
                    Fingerprint(field.scene.ebgBin)=="96b4ee826f0ad72ac4f718bddbcfb5f4436e9df11831c165d11c097ceb6b864f")observation.VisibleLabel="Ladder";
                observation.ShowsIcon=EIcon.NavigationPresentedUid==quad.uid && OnScreen(field,Vector(context.Position));
                if(_cueLogs.Count<64 && _cueLogs.Add("story-region:"+quad.uid+":"+path))Memoria.Prime.Log.Message("[FF9Access] Story region presented: map={0}; uid={1}; sid={2}; trigger={3}; label={4}; approaches={5}; interactionUid={6}",context.LogicalMapKey,quad.uid,quad.sid,path,observation.VisibleLabel,approaches.Length,observation.InteractionUid);
                return observation;
            }
            return null;
        }
        // Prospective proof is bounded per region path (nearest approaches first) and reused while the
        // detached memory it reads is unchanged, revalidating at least every 30 frames.
        private const int StoryProofPoints=32;
        private static readonly StoryRegionProofCache<FieldTargetObservation> _storyProofs=new StoryRegionProofCache<FieldTargetObservation>(30);
        private static byte[][] _storyProofMemory;
        // One roster snapshot per native catalog read, including every requested/guard/wait
        // owner. A phase can change via another actor without changing this region's bytes.
        private static byte[][] StoryProofMemory(NavigationContext context,EventEngine engine)
        {
            if(_storyProofMemory!=null)return _storyProofMemory;
            var memory=new List<byte[]>{FF9StateSystem.EventState.gEventGlobal,engine.GetMapVar()};
            using(var stream=new System.IO.MemoryStream())using(var writer=new System.IO.BinaryWriter(stream)) {
                writer.Write(context.Position.X);writer.Write(context.Position.Y);writer.Write(context.Position.Z);
                writer.Write(CurrentPlayerFacing(engine,context.PlayerIdentity)??-1);
                foreach(var obj in ActiveObjects(engine)) {
                    writer.Write(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj));
                    writer.Write((int)obj.uid);writer.Write((int)obj.sid);writer.Write(obj.ip);writer.Write((int)obj.level);writer.Write((int)obj.flags);writer.Write((int)obj.state);
                    writer.Write(obj.ebData==null?0:System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.ebData));
                    memory.Add(ScriptSnapshot.InstanceBytes(obj.buffer,obj.vofs));
                }
                writer.Flush();memory.Add(stream.ToArray());
            }
            return _storyProofMemory=memory.ToArray();
        }
        private static StoryRegionProof Proof(FieldTargetObservation observation)
        {return new StoryRegionProof(observation.StoryStepMilestone,observation.StoryMilestones);}
        private static void CopyStory(FieldTargetObservation from,FieldTargetObservation to)
        {
            to.StoryMilestones=from.StoryMilestones;to.NextFields=from.NextFields;to.StoryAction=from.StoryAction;
            to.StoryStepMilestone=from.StoryStepMilestone;to.StoryVerb=from.StoryVerb;to.IsItem=from.IsItem;to.HasInteraction|=from.HasInteraction;
        }
        private static bool ReadsPlayerPosition(IList<ScriptInstruction> code)
        {
            foreach(var instruction in code) {
                if(instruction.Expression!=null && Array.IndexOf(instruction.Expression,(byte)0x78)>=0)return true;
                if(instruction.ArgumentExpressions!=null)foreach(var expression in instruction.ArgumentExpressions)if(expression!=null && Array.IndexOf(expression,(byte)0x78)>=0)return true;
            }
            return false;
        }
        internal static bool StorySurfacePresented(FieldMap field,NavigationPoint point)
        {
            var bg=field.GetCurrentBgCamera();if(bg==null)return false;
            var projected=PSX.CalculateGTE_RTPT_POS(Vector(point),Matrix4x4.identity,bg.GetMatrixRT(),bg.GetViewDistance(),field.GetProjectionOffset());
            bool inArea=EntranceOnScreen(field,Vector(point)) || FieldPanoramaPresentation.Contains(ReadAreaPanorama(field),Point(projected));
            return inArea && RenderedBackgroundAt(field,Vector(point),null,true);
        }
    }
}
