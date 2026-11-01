using System;
namespace Memoria.Accessibility.Navigation
{
    public static class NativeFieldTouchReadiness
    {
        public static bool TryRead(NavigationContext context,NavigationTarget target,EventEngine engine,Actor player,Quad quad,out InteractionReadiness readiness)
        {
            readiness=null;
            if(context==null || !context.CanControl || FF9StateSystem.Common.FF9.fldMapNo!=2108 || target==null || target.PhysicalIdentity.MapKey!=context.LogicalMapKey ||
                engine==null || player==null || quad==null || quad.uid!=6 || quad.sid!=6 || quad.ebData==null || NativeFieldTargets.Fingerprint(quad.ebData)!=FieldTouchConfirmation.SourceHash)return false;
            var other=engine.TreadQuad(player,4);if(other!=null && other!=quad)return false;
            var callback=engine.GetObjUID(4) as Actor;if(callback==null || callback.ebData==null)return false;
            var state=new FieldTouchConfirmation{Field=2108,Uid=quad.uid,Sid=quad.sid,Hash=FieldTouchConfirmation.SourceHash,CanControl=context.CanControl,
                ShownUid=EIcon.NavigationPresentedUid,SelectedTouch=engine.TreadQuad(player,2)==quad,Contains=engine.NavigationContainsQuad(quad,context.Position.X,context.Position.Z),
                NativeFacingAllowed=EventCollision.NavigationHasTouchFacing(player,quad),OriginalBytes=!quad.isAdditionCommand && System.Object.ReferenceEquals(quad.currentByte,quad.ebData),
                Running=quad.state==EventEngine.stateRunning,Level=quad.level,Ip=quad.ip,Depth=quad.sx,Wait=quad.wait,
                CallbackSid=callback.sid,CallbackModel=callback.model,CallbackHash=NativeFieldTargets.Fingerprint(callback.ebData),CallbackRunning=callback.state==EventEngine.stateRunning,
                CallbackOriginalBytes=!callback.isAdditionCommand && System.Object.ReferenceEquals(callback.currentByte,callback.ebData),CallbackLevel=callback.level,CallbackHasTag=engine.GetIP(callback.sid,17,callback.ebData)!=engine.nil};
            if(quad.sx==2) {
                int offset=4*quad.sofs;if(quad.buffer==null || offset<0 || offset>quad.buffer.Length-8)return false;
                state.SavedIp=BitConverter.ToInt32(quad.buffer,offset);state.SavedStatus=BitConverter.ToInt32(quad.buffer,offset+4);
            }
            if(!state.Ready)return false;
            readiness=new InteractionReadiness{SelectedCollision=true,InRange=true,Talkable=true,FacingWithinArc=true,Evidence="Displayed native touch confirmation; Confirm remains player controlled"};return true;
        }
    }
}
