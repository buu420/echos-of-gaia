using System;
namespace Memoria.Accessibility.Navigation
{
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
