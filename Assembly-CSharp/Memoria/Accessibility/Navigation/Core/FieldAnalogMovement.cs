using System;
namespace Memoria.Accessibility.Navigation
{
    public static class FieldAnalogMovement
    {
        // Shared with the actual MovePC consumer, so tests exercise the speed delivered
        // to its normalized direction instead of an unrelated follower simulation.
        public static float Speed(float minimum, float maximum, float directionMagnitude, float physicalMagnitude, float threshold, long owner, bool hasTarget)
        {
            if (physicalMagnitude <= threshold && (owner == 0 || !hasTarget)) return 0;
            float scale = Math.Max(0, Math.Min(1, directionMagnitude));
            return minimum + (maximum - minimum) * scale;
        }
        public static float ArrivalRadius(float bodyRadius,long owner) { return owner==0?bodyRadius:Math.Min(bodyRadius,4); }
        public static float StepMagnitude(float proposed,float remaining,long owner,bool hasTarget) { return owner==0 || !hasTarget?proposed:Math.Min(proposed,Math.Max(0,remaining)); }
    }
}
