using System;
namespace Memoria.Accessibility.Navigation
{
    internal static class NativePinnacleRocks
    {
        // The context fixture has no rendered Pinnacle walkmesh. It exercises
        // the prerequisite reader with the visible targets in its own catalog.
        internal static NavigationTarget NextStoryStep(NavigationContext context, CatalogResult catalog,
            Actor player, int map, int flags) => null;
    }

    public static partial class NativeStoryTargets
    {
        public static OpeningPositionGoal PositionGoal;
        public static int PositionGoalReads;
        public static OpeningPositionGoal CurrentPositionGoal(NavigationContext context){PositionGoalReads++;return PositionGoal;}
    }
    public static partial class NativeFieldTargets
    {
        public static FieldGeometrySnapshot PositionGeometry;
        public static int PositionGeometryReads;
        public static Func<NavigationPoint,bool> PositionPresented;
        public static FieldGeometrySnapshot ReadGeometryForStory(NavigationContext context){PositionGeometryReads++;return PositionGeometry;}
        public static bool StorySurfacePresented(FieldMap field,NavigationPoint point){return PositionPresented!=null && PositionPresented(point);}
    }
}
