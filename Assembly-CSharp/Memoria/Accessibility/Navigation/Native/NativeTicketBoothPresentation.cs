using System.Collections.Generic;
using UnityEngine;
namespace Memoria.Accessibility.Navigation
{
    public static partial class NativeFieldTargets
    {
        private static FieldTargetObservation ReadOpeningTicketBooth(NavigationContext context,EventEngine engine,FieldMap field,Actor player,ref FieldGeometrySnapshot geometry,ref bool geometryRead)
        {
            // Prepare nothing in unrelated maps, menus, battles or transitions.
            if(context==null || !context.Foreground || !context.CanControl || context.LogicalMapKey!=OpeningTicketBoothEvidence.Map || context.StoryProgress<1000 || context.StoryProgress>=1190 ||
                engine==null || engine.gMode!=1 || field==null || field.camIdx!=0 || field.scene==null || field.scene.ebgBin==null || player==null || player.uid!=context.PlayerIdentity ||
                player.sid!=30 || player.model!=8 || player.ebData==null || Fingerprint(player.ebData)!=OpeningTicketBoothEvidence.Player)return null;
            var region=engine.GetObjUID(28) as Quad;var director=engine.GetObjUID(0);
            if(region==null || region.sid!=28 || region.ebData==null || region.q==null || region.n!=4 || region.q.Length<4 ||
                director==null || director.sid!=0 || director.ebData==null || Fingerprint(director.ebData)!=OpeningTicketBoothEvidence.Director || Fingerprint(region.ebData)!=OpeningTicketBoothEvidence.Script)return null;
            string background=Fingerprint(field.scene.ebgBin);if(background!=OpeningTicketBoothEvidence.Background)return null;
            var polygon=new NavigationPoint[region.n];Vector3 center=Vector3.zero;for(int i=0;i<polygon.Length;i++){var p=region.q[i].Vector3Val;polygon[i]=Point(p);center+=p;}center/=polygon.Length;
            bool onScreen=OnScreen(field,center),drawn=onScreen && BoothArtworkIsPresented(field);
            if(!drawn)return null;
            string walkmesh=CurrentBgiHash(field);if(walkmesh!=OpeningTicketBoothEvidence.Walkmesh)return null;
            if(!geometryRead){geometry=NativeFieldGeometry.Read(context);geometryRead=true;}
            var approaches=new List<NavigationPoint>();var position=Point(center);
            if(geometry!=null) {
                var support=geometry.At(position);if(support!=null)position=new NavigationPoint(position.X,support.Height(position),position.Z);
                foreach(var p in Surface(geometry,region).Approaches)if(engine.NavigationTouchQuadAt(p.X,p.Z)==region)approaches.Add(p);
            }
            return NativeOpeningTicketBooth.Read(context,engine,player,region,new OpeningTicketBoothPresentation{Generation=context.Generation,Camera=field.camIdx,Background=background,Walkmesh=walkmesh,
                ArtPresented=drawn,RegionOnScreen=onScreen,ShowsIcon=EIcon.NavigationPresentedUid==region.uid,Height=position.Y,Region=polygon,Approaches=approaches.ToArray()});
        }
        private static bool BoothArtworkIsPresented(FieldMap field)
        {
            int id=OpeningTicketBoothEvidence.Overlay;if(id<0 || id>=field.scene.overlayList.Count)return false;
            var tile=ReadTile(field,id);int bound;
            var center=new NavigationPoint((tile.MinX+tile.MaxX)*.5f,(tile.MinY+tile.MaxY)*.5f,0);
            if(!FieldDoorEvidence.TryBind(field.camIdx,field.camIdx,new[]{tile},center,true,out bound))return false;
            var overlay=field.scene.overlayList[id];if(overlay.transform==null)return false;
            foreach(var renderer in overlay.transform.GetComponentsInChildren<Renderer>())if(renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.isVisible) {
                var materials=renderer.sharedMaterials;if(materials==null)continue;
                foreach(var material in materials)if(material!=null && (!material.HasProperty("_Color") || material.GetColor("_Color").a>0))return true;
            }
            return false;
        }
    }
}
