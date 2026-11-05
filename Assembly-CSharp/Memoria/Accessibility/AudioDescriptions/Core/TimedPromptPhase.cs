namespace Memoria.Accessibility.AudioDescriptions
{
    public static class TimedPromptPhase
    {
        // Exact condition used by EventHUD.CheckUIMiniGameForMobile. Reading
        // the phase protects a command emitted before that frame's HUD update.
        public static bool IsChanbara(int field,int mode,int entrance,int phase)
        {return field==64 && mode==1 && phase==3 && entrance!=327 && entrance!=315 && entrance!=316;}
    }
}
