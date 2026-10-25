using System;
namespace Memoria.Accessibility.Navigation
{
    // Adapted from Blind Soldier's WorldMapFootstepTracker: accepted movement,
    // wrapped coordinates, 300 ms walking / 500 ms grounded Chocobo cadence.
    // FFIX motion and control gates come from the native navigation snapshot.
    public sealed class WorldFootstepCadence
    {
        private bool _primed;
        private NavigationPoint _position;
        private long _generation;
        private int _player;
        private string _map, _save;
        private TransportIdentity _transport;
        private double _previousTime, _lastStepTime;
        public bool IsMoving { get; private set; }

        public bool Observe(NavigationContext context, bool grounded, double nowMilliseconds)
        {
            if (context == null || !context.Foreground || !context.CanControl || context.GateReason != GateReason.None ||
                context.LogicalMapKey == null || !context.LogicalMapKey.StartsWith("world:", StringComparison.Ordinal) ||
                !grounded || (context.Transport.Kind != TransportKind.Foot && context.Transport.Kind != TransportKind.Chocobo) ||
                !context.Position.IsFinite || double.IsNaN(nowMilliseconds) || double.IsInfinity(nowMilliseconds))
            {
                Reset(); return false;
            }
            bool changed = !_primed || _generation != context.Generation || _player != context.PlayerIdentity ||
                _map != context.LogicalMapKey || _save != context.SaveIdentity || !_transport.Equals(context.Transport) ||
                nowMilliseconds < _previousTime || nowMilliseconds - _previousTime > 1000;
            NavigationPoint previous = _position;
            _position = context.Position; _previousTime = nowMilliseconds;
            _generation = context.Generation; _player = context.PlayerIdentity;
            _map = context.LogicalMapKey; _save = context.SaveIdentity; _transport = context.Transport;
            if (changed) { _primed = true; _lastStepTime = nowMilliseconds; IsMoving = false; return false; }
            float distance = WorldCoordinates.Distance(previous, context.Position);
            // Half a native world block rejects warps. Height-only ground settling
            // and sub-fixed-point jitter do not represent walking.
            if (distance > 32) { _lastStepTime = nowMilliseconds; IsMoving = false; return false; }
            IsMoving = distance > 1f / 256;
            if (!IsMoving) return false;
            int interval = context.Transport.Kind == TransportKind.Chocobo ? 500 : 300;
            if (nowMilliseconds - _lastStepTime < interval) return false;
            _lastStepTime = nowMilliseconds;
            return true;
        }
        public void Reset() { _primed = false; IsMoving = false; _map = _save = null; }
    }
}
