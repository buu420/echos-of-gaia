namespace Memoria.Accessibility.Navigation
{
    // Intent only: no scene objects, followers, geometry, or input handles survive here.
    public sealed class NavigationSession
    {
        private RouteMode _mode;
        private bool _automatic, _guidance, _randomSuspension, _battleResumeAllowed;
        private string _map, _save;
        private TransportIdentity _transport;
        private int _player;
        private long _generation;
        public RouteMode Mode { get { return _mode; } }
        public bool GuidanceEnabled { get { return _guidance; } }
        public void RevokeAutomaticResume() { _randomSuspension=_battleResumeAllowed=false;_mode=(_automatic || _guidance)?RouteMode.Paused:RouteMode.Idle; }
        public void Reset() { _mode = RouteMode.Idle; _automatic = _guidance = _randomSuspension = _battleResumeAllowed = false; _map = null; }
        public RouteDecision Update(NavigationContext context, NavigationSignal signal)
        {
            bool release = false, replan = false;
            StopReason reason = StopReason.None;
            if (context == null) { context = new NavigationContext(); context.GateReason = GateReason.UnknownInput; }
            bool identityChanged = _map != null && (_map != context.LogicalMapKey || _save != context.SaveIdentity || !_transport.Equals(context.Transport) || _player != context.PlayerIdentity);
            // Detached/uncertain actor frames must pause, not overwrite the stable identity
            // against which a reconstructed random-battle scene is validated.
            if (identityChanged && context.CanControl && signal != NavigationSignal.SceneDetach)
            {
                reason = _save != context.SaveIdentity ? StopReason.SaveChanged : _map != context.LogicalMapKey ? StopReason.MapChanged : StopReason.TransportChanged;
                Reset(); release = true;
            }
            if (signal == NavigationSignal.SceneDetach)
            {
                release = true; reason = StopReason.Battle;
                _randomSuspension = context.BattleTransition == BattleTransition.RandomStarted && _battleResumeAllowed && (_automatic || _guidance);
                if (context.BattleTransition == BattleTransition.RandomStarted || context.BattleTransition == BattleTransition.ScriptedStarted)
                    _mode = (_automatic || _guidance) ? RouteMode.Paused : RouteMode.Idle;
                else { Reset(); reason = StopReason.MapChanged; }
                return Decision(release, false, reason);
            }
            if (!context.Foreground || !context.CanControl || !context.Position.IsFinite || context.PlayerIdentity < 0)
            {
                release = true;
                reason = !context.Foreground ? StopReason.FocusLost : context.GateReason == GateReason.Menu ? StopReason.Menu : context.GateReason == GateReason.Dialogue ? StopReason.Dialogue : context.GateReason == GateReason.Disconnected ? StopReason.Disconnected : context.GateReason == GateReason.Battle ? StopReason.Battle : StopReason.UnknownContext;
                if (_automatic || _guidance) _mode = RouteMode.Paused;
                if (context.BattleTransition == BattleTransition.RandomStarted) _randomSuspension = _battleResumeAllowed && (_automatic || _guidance);
                else if (context.BattleTransition == BattleTransition.ScriptedStarted || reason != StopReason.Battle) { _randomSuspension = false; _battleResumeAllowed = false; }
                return Decision(release, false, reason);
            }
            if (_randomSuspension && context.BattleTransition == BattleTransition.RandomReturned && !identityChanged)
            {
                _mode = _automatic ? RouteMode.Moving : RouteMode.Guiding;
                replan = _automatic || _guidance; _randomSuspension = false;
            }
            if (signal == NavigationSignal.Stop || signal == NavigationSignal.TargetChanged)
            {
                Reset(); release = true; reason = signal == NavigationSignal.Stop ? StopReason.Explicit : StopReason.TargetChanged;
            }
            else if (signal == NavigationSignal.ManualMovement)
            {
                _automatic = false; _randomSuspension = false;
                _mode = _guidance ? RouteMode.Guiding : RouteMode.Idle;
                _battleResumeAllowed = _guidance;
                release = true; reason = StopReason.ManualMovement;
            }
            else if (signal == NavigationSignal.Confirm || signal == NavigationSignal.OpenMenu)
            {
                _mode = (_automatic || _guidance) ? RouteMode.Paused : RouteMode.Idle;
                _randomSuspension = _battleResumeAllowed = false; release = true; reason = signal == NavigationSignal.Confirm ? StopReason.Confirm : StopReason.Menu;
            }
            else if (signal == NavigationSignal.StartGuidance)
            {
                _automatic = false; _guidance = _battleResumeAllowed = true; _randomSuspension = false;
                _mode = RouteMode.Guiding; release = true;
            }
            else if (signal == NavigationSignal.ToggleGuidance)
            {
                if (_mode == RouteMode.Moving) { _guidance = !_guidance; _battleResumeAllowed = true; }
                else if (_mode == RouteMode.Guiding) { _guidance = _automatic = _battleResumeAllowed = false; _mode = RouteMode.Idle; }
                else { _automatic = false; _guidance = _battleResumeAllowed = true; _randomSuspension = false; _mode = RouteMode.Guiding; release = true; }
            }
            else if (signal == NavigationSignal.StartAutomatic)
            {
                _automatic = _guidance = _battleResumeAllowed = true; _randomSuspension = false; _mode = RouteMode.Moving; replan = true;
            }
            else if (signal == NavigationSignal.TargetLost || signal == NavigationSignal.RouteFailed)
            {
                _automatic = false; _mode = signal == NavigationSignal.RouteFailed && _guidance ? RouteMode.Guiding : RouteMode.Unavailable; _randomSuspension = _battleResumeAllowed = false;
                release = true; reason = signal == NavigationSignal.TargetLost ? StopReason.TargetLost : StopReason.PathInvalid;
            }
            else if (signal == NavigationSignal.Arrived)
            {
                _automatic = _guidance = _battleResumeAllowed = false; _mode = RouteMode.Arrived; release = true; reason = StopReason.Arrived;
            }
            if (_generation != 0 && _generation != context.Generation && (_mode == RouteMode.Moving || _mode == RouteMode.Guiding)) { replan = true; release = true; }
            _map = context.LogicalMapKey; _save = context.SaveIdentity; _transport = context.Transport; _player = context.PlayerIdentity; _generation = context.Generation;
            return Decision(release, replan, reason);
        }
        private RouteDecision Decision(bool release, bool replan, StopReason reason) { return new RouteDecision { Mode = _mode, IsAutomatic = _automatic && _mode == RouteMode.Moving, ReleaseMovement = release, RequireReplan = replan, Reason = reason }; }
    }
}
