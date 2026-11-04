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
        private static FieldTargetObservation ReadStoryRegion(NavigationContext context,EventEngine engine,FieldMap field,Actor player,Quad quad,ref FieldGeometrySnapshot geometry,ref bool geometryRead)
        {
            if(player==null || !NativeStoryTargets.Matches(context,quad.sid,Fingerprint(quad.ebData)) || !EventCollision.NavigationCanTriggerQuad(player,quad))return null;
            var observation=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,quad.uid),NativePresentationIdentity=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(quad),Visible=true,HasInteraction=true};
            observation.LabelIdentity=ExitLabelSignature(quad);
            Annotate(context,engine,quad,observation,EventEngine.tagPush);
            if(observation.StoryMilestones==null || observation.StoryMilestones.Length==0)return null;
            if(!geometryRead){geometry=NativeFieldGeometry.Read(context);geometryRead=true;}
            if(geometry==null)return null;
            var surface=Surface(geometry,quad);
            var safe=FieldStoryRegionApproaches.Read(true,true,surface.Approaches,p=>engine.NavigationTouchQuadAt(p.X,p.Z)==quad,p=>StorySurfacePresented(field,p),context.Position);
            if(safe.Length==0)return null;
            observation.Position=safe[0];observation.ApproachCandidates=safe;observation.UseApprovedApproaches=true;
            observation.VisibleLabel="Story path";
            bool talk=NativeFieldTalkability.IsTalkable(quad) && EventCollision.NavigationCanTalkQuad(player,quad);
            if(talk){observation.InteractionUid=quad.uid;observation.InteractionRegion=surface.Region;observation.VisibleLabel="Interaction point";}
            // The ladder is drawn in this reviewed steeple background. Its
            // current native region/phase still decides whether it is a goal.
            if(context.LogicalMapKey=="field:115:FBG_N01_ALXT_MAP026_AT_SIN_0" && quad.sid==15 &&
                Fingerprint(quad.ebData)=="442070aaf58c831db9bd8af7a1db98b766794821663efa9705eebf961303654e" && field.scene!=null && field.scene.ebgBin!=null &&
                Fingerprint(field.scene.ebgBin)=="96b4ee826f0ad72ac4f718bddbcfb5f4436e9df11831c165d11c097ceb6b864f")observation.VisibleLabel="Ladder";
            observation.ShowsIcon=EIcon.NavigationPresentedUid==quad.uid && OnScreen(field,Vector(context.Position));
            if(_cueLogs.Count<64 && _cueLogs.Add("story-region:"+quad.uid))Memoria.Prime.Log.Message("[FF9Access] Story region presented: map={0}; uid={1}; sid={2}; label={3}; approaches={4}; interactionUid={5}",context.LogicalMapKey,quad.uid,quad.sid,observation.VisibleLabel,safe.Length,observation.InteractionUid);
            return observation;
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
