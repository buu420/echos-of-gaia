namespace Memoria.Accessibility.Navigation
{
    // Arrival speech for a reached approach point. Interaction is promised only by the native readiness
    // outcomes (ReadyToInteract/TurnNeeded); reaching a point is reported as a position, never a prompt.
    public static class ArrivalNotice
    {
        public static string ApproachReached(NavigationTarget target,bool viewingPoint)
        {
            string label=target==null?"the selected target":target.Label;
            if(viewingPoint)return "Reached a viewing point for "+label+". This object has no interaction.";
            if(target!=null && target.IsInteractive)return "Reached "+label+". The game is not offering an interaction there yet.";
            return "Reached "+label+".";
        }
    }
}
