using UnityEngine;
namespace Memoria.Accessibility.Navigation
{
    public static class NativeNavigationContext
    {
        public static NavigationContext Read()
        {
            var context = new NavigationContext { Generation = NavigationLifecycleBridge.Generation, SaveIdentity = NavigationSaveIdentity.Current, GateReason = GateReason.UnknownActor, BattleTransition = NavigationLifecycleBridge.ReturnTransition };
            context.Foreground = HonoInputManager.ApplicationIsActivated();
            var state = Object.FindObjectOfType<FF9StateSystem>();
            var engine = Object.FindObjectOfType<EventEngine>();
            var ui = Object.FindObjectOfType<UIManager>();
            if (state == null || engine == null || ui == null) return context;
            bool world = state.mode == 3;
            bool hud = ui.State == (world ? UIManager.UIState.WorldHUD : UIManager.UIState.FieldHUD);
            var dialogs = Object.FindObjectOfType<DialogManager>();
            bool nativeControl = (state.mode == 1 || world) && ui.IsPlayerControlEnable && engine.GetUserControl() && EventInput.IsMovementControl;
            // Visible includes persistent instructions shown while ordinary walking is
            // enabled. Keep choices modal and respect every native movement gate.
            bool blockingDialog = dialogs != null && dialogs.Visible && (!nativeControl || dialogs.GetChoiceDialog() != null);
            context.LogicalMapKey = world ? "world:" + WMUIData.ActiveMapNo : "field:" + FF9StateSystem.Common.FF9.fldMapNo + ":" + FF9StateSystem.Field.SceneName;
            bool pendingBattle = state.mode==1 ? FF9StateSystem.Field.FF9Field.loc.map.nextMode==2 : world && FF9StateSystem.World.FF9World.map.nextMode==2;
            context.MinigameInspection=NativeChocoboHotCold.ObserveContext(context,ui,dialogs,state.mode,pendingBattle);
            context.CanInspect = context.Foreground && (state.mode == 1 || world) && hud && !ui.IsPause && !ui.IsLoading &&
                (context.MinigameInspection?NativeChocoboHotCold.CanInspect:EventHUD.CurrentHUD == MinigameHUD.None && !blockingDialog);
            if (state.mode == 2) { context.GateReason = GateReason.Battle; return context; }
            if (state.mode != 1 && !world) { context.GateReason = GateReason.MapTransition; return context; }
            if (pendingBattle) {
                context.GateReason = GateReason.Battle;
                context.BattleTransition = FF9StateSystem.Battle.isRandomEncounter ? BattleTransition.RandomStarted : BattleTransition.ScriptedStarted;
                return context;
            }
            var actor = engine.GetControlChar() as Actor;
            if (actor == null || actor.uid != engine.GetControlUID() || actor.go == null) return context;
            context.PlayerIdentity = actor.uid;
            var story=FF9StateSystem.EventState.gEventGlobal;if(story!=null && story.Length>=2)context.StoryProgress=story[0]|(story[1]<<8);
            if (world) {
                WMActor controlled = actor.wmActor;
                if (controlled == null || controlled != ff9.w_moveActorPtr || controlled.originalActor != actor) return context;
                Vector3 position = controlled.RealPosition;
                context.Position = new NavigationPoint(position.x, position.y, position.z);
                int status = actor.index;
                TransportKind kind = status == 8 ? TransportKind.Boat : status == 9 ? TransportKind.HildaGarde : status == 10 ? TransportKind.Invincible : status >= 3 && status <= 7 ? TransportKind.Chocobo : TransportKind.Foot;
                context.Transport = new TransportIdentity(kind, actor.uid, status);
            } else {
                if (actor.fieldMapActorController == null || !actor.fieldMapActorController.isPlayer) return context;
                Vector3 position = actor.fieldMapActorController.curPos;
                context.Position = new NavigationPoint(position.x, position.y, position.z);
                context.Transport = new TransportIdentity(TransportKind.Foot, actor.uid, 0);
            }
            if (!context.Foreground) context.GateReason = GateReason.Background;
            else if (!hud || ui.IsPause || ui.IsLoading || context.MinigameInspection || EventHUD.CurrentHUD != MinigameHUD.None) context.GateReason = GateReason.Menu;
            else if (blockingDialog) context.GateReason = GateReason.Dialogue;
            else if (!nativeControl) context.GateReason = GateReason.NoControl;
            else context.GateReason = context.Position.IsFinite ? GateReason.None : GateReason.UnknownActor;
            context.CanControl = context.GateReason == GateReason.None;
            return context;
        }
    }
}
