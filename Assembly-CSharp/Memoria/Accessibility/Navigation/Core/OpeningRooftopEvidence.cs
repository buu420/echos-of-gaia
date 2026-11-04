using System;
namespace Memoria.Accessibility.Navigation
{
    public enum OpeningGoalAxis {X,Z}
    // A reviewed native position wait. The script polls OBJ(uid).x or .z, which
    // EBin.getvobj rounds from the float position, against constant bounds.
    // Minimum and Maximum are inclusive bounds on that rounded native value.
    public sealed class OpeningPositionGoal
    {
        public int Field,Phase,Milestone;public string Map,Action;public OpeningGoalAxis Axis;public int? Minimum,Maximum;
        public bool Satisfied(NavigationPoint point)
        {
            if(!point.IsFinite)return false;
            int value=(int)Math.Round(Axis==OpeningGoalAxis.X?point.X:point.Z,MidpointRounding.ToEven);
            return (!Minimum.HasValue || value>=Minimum.Value) && (!Maximum.HasValue || value<=Maximum.Value);
        }
    }
    public static class OpeningRooftopEvidence
    {
        public const int Field=116,PlayerUid=21;
        public const string Map="field:116:FBG_N01_ALXT_MAP023_AT_ROF_0";
        public const string Director="f036b90ffc811e55b562a785a76f7632bafaa463fa11f8d8ccaccc3d3256f203";
        public const string RatKid="bd68ae60b84ded764f228b9cefc29ff13ce18cefb222242304a4b49c57239e6f";
        public const string Player="bbc376a6e9979399cf319c95c92a8517f48dd40d85c1fd55ec788b9c682f44d0";
        public const string FirstAction="Follow the boy across the rooftop";
        public const string SecondAction="Keep following the boy across the rooftop";
        // EVT_ALEX1_AT_ROOF: init sets MAP5[24]=1 (12 only when G6[2]==217); director entry 0 tag 1
        // advances one phase per scene step. Vivi (entry 21, uid 21) tag 1: phase 3 gives UCON at 662.
        // Phase4 loops BACK while OBJ(21).x >900, so progress requires x <=900 (UCOFF773).
        // Phase8 gives UCON958; phase9 loops BACK while z <2300, so progress requires z >=2300
        // (UCOFF1069). The boy (entry 2) leads and later
        // performs MAPJUMP 61. The counter stays 1155. phase is the current native MAP5[24].
        public static OpeningPositionGoal Current(NavigationContext context,int? phase,Func<int,string> scripts)
        {
            if(context==null || !context.CanControl || context.LogicalMapKey!=Map || context.PlayerIdentity!=PlayerUid ||
               context.StoryProgress<OpeningStoryCheckpoints.SteepleMilestone || context.StoryProgress>=OpeningStoryCheckpoints.SteepleCeiling ||
               !OpeningStoryCheckpoints.Verified(scripts,OpeningStoryCheckpoints.RooftopScripts()))return null;
            if(phase==3 || phase==4)return new OpeningPositionGoal{Field=Field,Map=Map,Phase=4,Milestone=OpeningStoryCheckpoints.SteepleMilestone,Action=FirstAction,Axis=OpeningGoalAxis.X,Maximum=900};
            if(phase==8 || phase==9)return new OpeningPositionGoal{Field=Field,Map=Map,Phase=9,Milestone=OpeningStoryCheckpoints.SteepleMilestone,Action=SecondAction,Axis=OpeningGoalAxis.Z,Minimum=2300};
            return null;
        }
    }
}
