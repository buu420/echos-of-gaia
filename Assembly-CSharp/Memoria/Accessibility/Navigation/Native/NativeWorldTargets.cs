using System;
using System.Collections.Generic;
using UnityEngine;
using Assets.Sources.Scripts.UI.Common;
namespace Memoria.Accessibility.Navigation
{
    public static class NativeWorldTargets
    {
        public static IList<WorldTargetObservation> Read(NavigationContext context)
        {
            var result=new List<WorldTargetObservation>();if(context==null || !context.LogicalMapKey.StartsWith("world:"))return result;
            int map=WMUIData.ActiveMapNo;var positions=ff9.w_naviLocationPos;var text=FF9TextTool.GetTableText(0u);
            if(positions!=null && map>=0 && map<positions.GetLength(0))for(int id=0;id<positions.GetLength(1) && id<64;id++) {
                if(!WMUIData.LocationAvailable(id))continue;var position=positions[map,id];if(position.tx==0 && position.ty==0)continue;
                int textId=id==63?49:id;string label=text!=null && textId+1<text.Length?text[textId+1]:null;if(string.IsNullOrEmpty(label))continue;
                result.Add(new WorldTargetObservation {Id=new TargetIdentity(TargetKind.WorldLocation,context.LogicalMapKey,id),Label=label,Position=new NavigationPoint(ff9.S(position.tx),0,ff9.S(position.ty)),Available=true,Marked=true,NativeLocationId=id});
            }
            var engine=UnityEngine.Object.FindObjectOfType<EventEngine>();if(engine==null)return result;
            for(var list=engine.GetActiveObjList();list!=null;list=list.next) {
                var actor=list.obj as Actor;if(actor==null || actor.index<3 || actor.index>10 || actor.uid==context.PlayerIdentity || actor.wmActor==null)continue;
                var native=actor.wmActor;bool marked=(native==ff9.w_movePlanePtr && WMUIData.PlaneIsAvailable) || (native==ff9.w_moveChocoboPtr && WMUIData.ChocoboIsAvailable);
                bool visible=engine.objIsVisible(actor) && actor.isEnableRenderer && actor.go!=null && actor.go.activeInHierarchy && OnScreen(actor.wmActor);
                var position=native.RealPosition;string label=actor.index==8?"Blue Narciss":actor.index==9?"Hilda Garde III":actor.index==10?"Invincible":"Chocobo";
                var player=engine.GetControlChar() as Actor;
                float radius=player==null?0:(4*actor.talkRad+4*player.talkRad+actor.speed+60)/256f*0.75f;
                var approaches=new List<NavigationPoint>();approaches.Add(new NavigationPoint(position.x,position.y,position.z));
                for(int i=0;i<8 && radius>0;i++){double angle=i*Math.PI/4;approaches.Add(WorldCoordinates.Canonical(new NavigationPoint(position.x+(float)Math.Sin(angle)*radius,position.y,position.z+(float)Math.Cos(angle)*radius)));}
                result.Add(new WorldTargetObservation {Id=new TargetIdentity(TargetKind.Vehicle,context.LogicalMapKey,actor.uid),Label=label,Position=new NavigationPoint(position.x,position.y,position.z),ApproachCandidates=approaches.ToArray(),Available=true,Visible=visible,Marked=marked,NativeIndex=actor.index});
            }
            return result;
        }
        private static bool OnScreen(WMActor actor)
        {
            var world=UnityEngine.Object.FindObjectOfType<WMWorld>();var camera=world==null?null:world.MainCamera;if(camera==null)return false;var p=camera.WorldToViewportPoint(actor.transform.position);
            return p.z>0 && p.x>=-0.02f && p.x<=1.02f && p.y>=-0.02f && p.y<=1.02f;
        }
    }
}
