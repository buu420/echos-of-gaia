namespace Memoria.Accessibility.Navigation
{
    public static class MovementStartMessage
    {
        public static string Read(MovementStartResult result)
        {
            switch(result) {
                case MovementStartResult.Started:return null;
                case MovementStartResult.InvalidContext:return "game control is not available; try again when you can move";
                case MovementStartResult.StaleRoute:return "the route changed; request a new route";
                case MovementStartResult.Busy:return "another game action is already moving the character";
                default:return "the game could not begin walking";
            }
        }
    }
}
