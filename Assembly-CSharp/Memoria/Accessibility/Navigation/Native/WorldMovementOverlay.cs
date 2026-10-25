namespace Memoria.Accessibility.Navigation
{
    // This value is consumed only by the movement-local ff9 readers. There is
    // intentionally no button/event-mask representation in the command.
    public static class WorldMovementOverlay
    {
        private static readonly MovementOverlayState _state=new MovementOverlayState();
        public static void Set(NavigationContext context,WorldSteeringCommand command,long ownershipId){_state.Set(context,command,ownershipId);}
        public static void Clear(long ownershipId){_state.Clear(ownershipId);}
        public static bool TryRead(out WorldSteeringCommand command)
        {
            NativeNavigationInput.EnsureUpdated();
            return _state.TryRead(NativeNavigationContext.Read(),out command);
        }
        public static bool TryLX(out int value){WorldSteeringCommand c;bool available=TryRead(out c);value=c.LX;return available;}
        public static bool TryLY(out int value){WorldSteeringCommand c;bool available=TryRead(out c);value=c.LY;return available;}
        public static bool TryThrottle(out int value){WorldSteeringCommand c;bool available=TryRead(out c);value=c.Throttle;return available;}
        public static void ApplyFlightY(ref int value){WorldSteeringCommand c;if(TryRead(out c))value=c.LY;}
    }
}
