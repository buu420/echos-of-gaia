using UnityEngine;
namespace Memoria.Accessibility.Navigation
{
    // Reads the same selection and angle as CheckNPCInput, without KeyOn, Request,
    // listener writes, sSysAngle writes, or the mutable EBin expression stack.
    public static class NativeInteractionProbe
    {
        public static InteractionReadiness Read(NavigationContext context, NavigationTarget target)
        {
            var result = new InteractionReadiness();
            if (context == null || !context.CanControl || target == null || target.PhysicalIdentity.Kind != TargetKind.FieldActor || target.Generation != context.Generation) return result;
            var engine = Object.FindObjectOfType<EventEngine>();
            if (engine == null || engine.gMode != 1) return result;
            var player = engine.GetControlChar() as Actor;
            if(player==null || player.uid!=context.PlayerIdentity || player.fieldMapActorController==null || player.fieldMapActorController.walkMesh==null)return result;
            InteractionReadiness candle;if(NativeFieldTargets.TryOpeningCandleReadiness(context,target,engine,player,out candle))return candle;
            var controller=player.fieldMapActorController;float distance;
            var selected=controller.walkMesh.Collision(controller,4,out distance);
            var presentedQuad=engine.GetObjUID(target.PhysicalIdentity.NativeId) as Quad;
            if(presentedQuad!=null && target.InteractionUid==presentedQuad.uid) {
                if(NpcConsumesConfirm(engine,player,selected as Actor,distance))return result;
                result.SelectedCollision=engine.TreadQuad(player,4)==presentedQuad;
                result.InRange=result.SelectedCollision && engine.NavigationContainsQuad(presentedQuad,controller.curPos.x,controller.curPos.z);
                result.Talkable=engine.IsActuallyTalkable(presentedQuad) && EventCollision.NavigationCanTalkQuad(player,presentedQuad) && EIcon.NavigationPresentedUid==presentedQuad.uid;
                result.FacingWithinArc=result.Talkable;result.Evidence="Selected native interaction region and presented bubble; Confirm remains player controlled";return result;
            }
            var npc = engine.GetObjUID(target.PhysicalIdentity.NativeId) as Actor;
            if(npc==null)return result;
            if(target.InteractionUid>=0) {
                var region=engine.GetObjUID(target.InteractionUid) as Quad;
                bool forest=NativeFieldTargets.IsForestInteraction(context,npc,region);
                if(!forest && !NativeFieldActorInteractions.IsBoundPair(context,engine,player,npc,region))return result;
                // CheckNPCInput runs before CheckQuadInput. A competing NPC must
                // not turn the Forest region into a false ready announcement.
                if(NpcConsumesConfirm(engine,player,selected as Actor,distance))return result;
                result.SelectedCollision=engine.TreadQuad(player,4)==region;
                result.InRange=result.SelectedCollision && engine.NavigationContainsQuad(region,controller.curPos.x,controller.curPos.z);
                result.Talkable=engine.IsActuallyTalkable(region) && EventCollision.NavigationCanTalkQuad(player,region) && EIcon.NavigationPresentedUid==region.uid;
                result.FacingWithinArc=result.Talkable;
                result.Evidence=(forest?"Native Forest talking region":"Reviewed native talking region of this speaker")+", selected quad, request level and presented interaction bubble; Confirm remains player controlled";
                return result;
            }
            result.SelectedCollision = selected == npc;
            result.InRange = result.SelectedCollision && distance < 0;
            result.Talkable = npc.level > 1 && (npc.flags & EventEngine.flagTalkInh) == 0 && (engine.IsActuallyTalkable(npc) || NativeFieldTargets.IsReviewedListener(context,engine,npc)) && NativeTalkGuard(engine, npc);
            if (result.SelectedCollision) {
                result.FacingError = EventCollision.CollisionAngle(player, npc);
                result.FacingWithinArc = result.FacingError > -1024 && result.FacingError < 1024;
            }
            result.Evidence = "Native talk collision selection, request level, live talk guard and strict facing arc; Confirm remains player controlled";
            return result;
        }
        private static bool NpcConsumesConfirm(EventEngine engine,Actor player,Actor npc,float distance)
        {
            if(npc==null || distance>=0 || npc.level<=1 || (npc.flags&EventEngine.flagTalkInh)!=0 || engine.GetIP(npc.sid,EventEngine.tagTalk,npc.ebData)==engine.nil || !NativeTalkGuard(engine,npc))return false;
            int angle=EventCollision.CollisionAngle(player,npc);return angle> -1024 && angle<1024;
        }
        // Side-effect-free mirror of EventCollision.IsNPCTalkable (no EBin getVarManually scratch use).
        internal static bool NativeTalkGuard(EventEngine engine, Actor npc)
        {
            int map = FF9StateSystem.Common.FF9.fldMapNo;
            // Most fields have no special native talk rule. Avoid copying event
            // variables or looking up the bubble for every ordinary NPC.
            if(!(map>=656 && map<=659) && map!=350 && map!=507 && map!=566 && map!=1856 && map!=611 && map!=1603 && map!=1608 && map!=2950)return true;
            if(map==566 && npc.uid==10 || map==1856 && npc.uid==4) {
                var bubble=Object.FindObjectOfType<BubbleUI>();
                return bubble==null || !bubble.IsActive || EIcon.SFIconType!=BubbleUI.IconType.Exclamation;
            }
            var snapshot = new ScriptSnapshot(FF9StateSystem.EventState.gEventGlobal, engine.GetMapVar(), null);
            int? story = Read(snapshot, EBin.SC_COUNTER_SVR), index = Read(snapshot, EBin.MAP_INDEX_SVR);
            if (map >= 656 && map <= 659 && (npc.model == 174 || npc.model == 175 || npc.model == 176 || npc.model == EMinigame.GoldenFrogModelId))
                return Read(snapshot, 157157) > 0 || map == 657 && npc.sid == 4;
            if (map == 350 && npc.sid == 34) return story.HasValue && index.HasValue && !(story == 2600 && index == 2);
            if (map == 507 && npc.sid == 15) return story.HasValue && index.HasValue && !(story == 2915 && index == 3 && engine.GetObjUID(10) != null);
            if (map == 611 && npc.sid == 7) return story.HasValue && index.HasValue && !(story == 3140 && index == 40);
            if (map == 1603 && npc.uid == 133) return story.HasValue && story != 6810;
            if (map == 1608 && npc.sid == 15) return story >= 6850;
            if (map == 2950 && npc.sid == 9) return index.HasValue && index != 2 && Read(snapshot, 8401) == 1;
            return true;
        }
        private static int? Read(ScriptSnapshot snapshot, int operation)
        {
            int index = (operation & 0xff00) >> 8;
            if ((operation & 0x20) != 0) index |= (operation >> 8) & 0xff00;
            return snapshot.Read(operation & 3, (operation & 0x1c) >> 2, index);
        }
    }
}
