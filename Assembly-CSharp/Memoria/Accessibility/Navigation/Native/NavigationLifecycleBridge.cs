using System;
using Memoria.Prime;
namespace Memoria.Accessibility.Navigation
{
    public static class NavigationLifecycleBridge
    {
        public static readonly NavigationSession Session = new NavigationSession();
        public static long Generation { get; private set; }
        public static BattleTransition ReturnTransition { get; private set; }
        public static event Action<StopReason> Releasing;
        private static BattleTransition _suspended;
        private static bool _attached, _world;
        public static void AttachScene(bool world)
        {
            Generation++; _attached = true; _world = world;
            ReturnTransition = _suspended == BattleTransition.RandomStarted ? BattleTransition.RandomReturned : _suspended == BattleTransition.ScriptedStarted ? BattleTransition.UncertainReturn : BattleTransition.None;
            _suspended = BattleTransition.None;
        }
        public static void DetachScene()
        {
            NativeWorldFootsteps.Suspend();
            NativeFieldExitCues.Suspend();
            NativeWorldGeometry.Detach();
            if (!_attached) return;
            var context = NativeNavigationContext.Read();
            bool nextBattle = FF9StateSystem.Instance.mode == 2 || (_world ? FF9StateSystem.World.FF9World.map.nextMode == 2 : FF9StateSystem.Field.FF9Field.loc.map.nextMode == 2);
            _suspended = nextBattle ? (FF9StateSystem.Battle.isRandomEncounter ? BattleTransition.RandomStarted : BattleTransition.ScriptedStarted) : BattleTransition.None;
            context.BattleTransition = _suspended;
            Session.Update(context, NavigationSignal.SceneDetach);
            Release(nextBattle ? StopReason.Battle : StopReason.MapChanged);
            Generation++; _attached = false; ReturnTransition = BattleTransition.None;
        }
        public static void Acknowledge(NavigationContext context) { if (context != null && context.CanControl) ReturnTransition = BattleTransition.None; }
        public static void ReplaceSave()
        {
            NativeFieldExitCues.Suspend();
            Release(StopReason.SaveChanged); Session.Reset(); Generation++; _suspended = ReturnTransition = BattleTransition.None;
        }
        public static void Release(StopReason reason)
        {
            if(reason==StopReason.FocusLost || reason==StopReason.Disconnected)Session.RevokeAutomaticResume();
            var release = Releasing;
            if (release == null) return;
            foreach (Action<StopReason> handler in release.GetInvocationList()) {
                try { handler(reason); }
                catch (Exception exception) { Log.Message("[FF9Access] Movement release failed: {0}", exception); }
            }
        }
    }
}
