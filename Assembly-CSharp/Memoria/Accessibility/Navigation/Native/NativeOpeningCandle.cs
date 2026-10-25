using System;
using System.Collections.Generic;
using UnityEngine;
namespace Memoria.Accessibility.Navigation
{
    public static partial class NativeFieldTargets
    {
        private static long _candleGeneration;
        private static BGSCENE_DEF _candleScene;
        private static string _candleWalkmeshHash;
        private static readonly OpeningCandleApproachCache _candleApproaches=new OpeningCandleApproachCache();

        private static bool OpeningCandleState(NavigationContext context,EventEngine engine,FieldMap field,Actor player,out List<Quad> regions)
        {
            regions=null;
            // This path never prepares geometry or hashes during battles or in
            // unrelated fields. Confirm is read only by the game's own script.
            if(context==null || !context.CanControl || context.LogicalMapKey!=OpeningCandleEvidence.Map || engine==null || engine.gMode!=1 ||
               field==null || field.scene==null || field.scene.ebgBin==null || player==null || player.uid!=context.PlayerIdentity || player.sid!=17 || player.ebData==null)return false;
            var phase=engine.GetMapVar();if(phase==null || phase.Length<=OpeningStoryCheckpoints.PhaseByte)return false;
            var director=engine.GetObjUID(3);if(director==null || director.sid!=3 || director.ebData==null)return false;
            if(_candleGeneration!=context.Generation || !System.Object.ReferenceEquals(_candleScene,field.scene)){
                _candleGeneration=context.Generation;_candleScene=field.scene;_candleWalkmeshHash="";
                try{var bytes=AssetManager.LoadBytes(FieldMap.GetMapResourcePath(field.mapName)+field.mapName+".bgi");if(bytes!=null)_candleWalkmeshHash=Fingerprint(bytes);}
                catch(Exception){return false;}
            }
            if(!OpeningCandleEvidence.Available(context,phase[OpeningStoryCheckpoints.PhaseByte],player.sid,Fingerprint(player.ebData),Fingerprint(director.ebData),Fingerprint(field.scene.ebgBin),_candleWalkmeshHash))return false;
            var current=new List<Quad>();
            foreach(var reviewed in OpeningStoryCheckpoints.CandleRegions){
                var region=engine.GetObjUID(reviewed.Sid) as Quad;
                if(region==null || region.sid!=reviewed.Sid || region.ebData==null || region.q==null || region.n<3 || region.n>region.q.Length || !EventCollision.NavigationCanTriggerQuad(player,region))return false;
                var polygon=new NavigationPoint[region.n];for(int i=0;i<polygon.Length;i++)polygon[i]=Point(region.q[i].Vector3Val);
                if(!OpeningCandleEvidence.MatchesRegion(region.sid,Fingerprint(region.ebData),polygon))return false;
                current.Add(region);
            }
            regions=current;return true;
        }
        private static FieldTargetObservation ReadOpeningCandle(NavigationContext context,EventEngine engine,FieldMap field,Actor player)
        {
            List<Quad> regions;if(!OpeningCandleState(context,engine,field,player,out regions))return null;
            var geometry=NativeFieldGeometry.Read(context);if(geometry==null)return null;
            var area=OpeningCandleEvidence.ApproachArea;
            var candidates=_candleApproaches.Read(geometry);
            var permitted=new List<NavigationPoint>();
            foreach(var point in candidates){
                var selected=engine.NavigationTouchQuadAt(point.X,point.Z);
                foreach(var region in regions)if(selected==region){permitted.Add(point);break;}
            }
            var center=new NavigationPoint(0,0,350);var support=geometry.At(center);
            if(support!=null)center=new NavigationPoint(center.X,support.Height(center),center.Z);
            else if(permitted.Count>0)center=new NavigationPoint(center.X,permitted[0].Y,center.Z);
            bool visible=OnScreen(field,Vector(center)) && RenderedBackgroundAt(field,Vector(center));
            int shown=EIcon.NavigationPresentedUid;bool icon=false;foreach(var region in regions)if(shown==region.uid)icon=true;
            if(!visible && !icon)return null;
            var observation=new FieldTargetObservation{
                Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,regions[0].uid),
                NativePresentationIdentity=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(regions[0]),
                Position=center,Visible=visible,ShowsIcon=icon,HasInteraction=true,IsPerson=false,
                InteractionUid=regions[0].uid,InteractionRegion=area,ApproachCandidates=permitted.ToArray()
            };
            Annotate(context,engine,regions[0],observation);return observation;
        }
        internal static bool TryOpeningCandleReadiness(NavigationContext context,NavigationTarget target,EventEngine engine,Actor player,out InteractionReadiness result)
        {
            result=null;
            if(context==null || context.LogicalMapKey!=OpeningCandleEvidence.Map || target==null || target.PhysicalIdentity.NativeId!=4)return false;
            result=new InteractionReadiness();var field=player==null || player.fieldMapActorController==null?null:player.fieldMapActorController.fieldMap;
            List<Quad> regions;if(!OpeningCandleState(context,engine,field,player,out regions) || player.go==null)return true;
            var position=player.go.transform.position;int shown=EIcon.NavigationPresentedUid;
            foreach(var region in regions)if(shown==region.uid && engine.NavigationContainsQuad(region,position.x,position.z)){
                result.SelectedCollision=result.InRange=result.Talkable=result.FacingWithinArc=true;
                result.Evidence="Native candle phase, current question-mark bubble and containing touch region; use Confirm and choose Light the candle manually";
                break;
            }
            return true;
        }
    }
}
