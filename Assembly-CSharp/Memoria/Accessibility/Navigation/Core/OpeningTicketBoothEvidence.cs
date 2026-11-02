namespace Memoria.Accessibility.Navigation
{
    public sealed class OpeningTicketBoothState
    {
        public NavigationContext Context;public int Camera,PlayerUid,PlayerSid,PlayerModel,RegionSid;
        public string PlayerHash,DirectorHash,RegionHash,BackgroundHash,WalkmeshHash;
        public bool ArtPresented,RegionOnScreen,NativeInteractionAllowed;
        public NavigationPoint[] Region;
    }
    // The sign says TICKET on the currently rendered square backdrop. Region28
    // is its native Confirm spot; player30 owns the choice and room transition.
    public static class OpeningTicketBoothEvidence
    {
        public const string Map="field:103:FBG_N01_ALXT_MAP019N_AT_CNT_N";
        public const string Player="0e72508bc17c135bed7a794f53ea81941c31cadc590c6832cae3a3e85e87f45d";
        public const string Director="f9abc49cafca1b9355a8c0171f1acb9c85e104febcbee6b10d4831bd2e998946";
        public const string Script="6a8882a5ac1d5752691727fccd62d42ca691675f3c1464048f33a2f1d71c4a66";
        public const string Background="822130022cd06b30b4f85f74c21f2649fb84fe5f07817a97191b9ad6f1f2d8ea";
        public const string Walkmesh="a23d1a8220a5dc3cdb681d267d6bacbb5e103475af1df87d6edd0b4fa4327ddf";
        public const int Overlay=20;
        public const string Label="Ticket booth";
        public static NavigationPoint[] Region{get{return new[]{new NavigationPoint(-500,0,-111),new NavigationPoint(550,0,-111),new NavigationPoint(250,0,-1000),new NavigationPoint(-200,0,-1000)};}}
        public static bool Available(OpeningTicketBoothState s)
        {
            if(s==null || s.Context==null)return false;
            var context=s.Context;
            if(!context.Foreground || !context.CanControl || context.LogicalMapKey!=Map || context.StoryProgress<1000 || context.StoryProgress>=1190)return false;
            if(s.PlayerUid!=context.PlayerIdentity || s.PlayerSid!=30 || s.PlayerModel!=8 || s.PlayerHash!=Player || s.DirectorHash!=Director)return false;
            return s.Camera==0 && s.RegionSid==28 && s.RegionHash==Script && s.BackgroundHash==Background && s.WalkmeshHash==Walkmesh &&
                s.ArtPresented && s.RegionOnScreen && s.NativeInteractionAllowed && Matches(s.Region);
        }
        private static bool Matches(NavigationPoint[] points)
        {var expected=Region;if(points==null || points.Length!=expected.Length)return false;for(int i=0;i<points.Length;i++)if(!points[i].IsFinite || points[i].X!=expected[i].X || points[i].Z!=expected[i].Z)return false;return true;}
        public static void Annotate(NavigationContext context,FieldTargetObservation observation)
        {
            observation.VisibleLabel=Label;
            if(context.StoryProgress>=1000 && context.StoryProgress<1150){observation.StoryMilestones=new[]{1150};observation.StoryAction="Approach the ticket booth";}
        }
    }
}
