using System.Linq;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class OpeningPositionApproachTests
    {
        private static NavigationPoint P(float x,float z){return new NavigationPoint(x,0,z);}
        private static FieldGeometrySnapshot Floor()
        {return new FieldGeometrySnapshot{Generation=1,Radius=10,ActiveTriangle=0,Triangles=new[]{new FieldTriangle{Id=0,Vertices=new[]{P(0,0),P(2000,0),P(0,4000)}}}};}
        public static void RoofGoalPointsLeaveRoomForTheNativeArrivalTolerance()
        {
            var goal=new OpeningPositionGoal{Axis=OpeningGoalAxis.X,Maximum=900};
            var points=OpeningPositionApproaches.Read(goal,Floor(),P(1500,100));
            TestAssert.True(points.Length>0);
            foreach(var point in points){TestAssert.True(goal.Satisfied(point));TestAssert.True(point.X<=892);TestAssert.True(Floor().IsSegmentClear(point,point));}
        }
        public static void GoalPointsStayNearTheCrossingRatherThanTouringTheWholeRoof()
        {
            var goal=new OpeningPositionGoal{Axis=OpeningGoalAxis.X,Maximum=900};
            var points=OpeningPositionApproaches.Read(goal,Floor(),P(1500,100));
            TestAssert.True(points.Length>0);foreach(var point in points)TestAssert.True(point.X>=828);
        }
        public static void BoundedAlleyGoalRemainsInsideTheActualTriggerWindow()
        {
            var goal=new OpeningPositionGoal{Axis=OpeningGoalAxis.Z,Minimum=2800,Maximum=2900};
            var points=OpeningPositionApproaches.Read(goal,Floor(),P(100,100));
            TestAssert.True(points.Length>0);foreach(var point in points)TestAssert.True(point.Z>=2808 && point.Z<=2892 && goal.Satisfied(point));
        }
        public static void DisconnectedOrDisabledFloorCannotProvideAWalkingGoal()
        {
            var f=Floor();f.Triangles=new[]{new FieldTriangle{Id=0,Vertices=new[]{P(0,0),P(100,0),P(0,100)}},new FieldTriangle{Id=1,Vertices=new[]{P(1000,0),P(2000,0),P(1000,1000)}}};
            var goal=new OpeningPositionGoal{Axis=OpeningGoalAxis.X,Minimum=901};
            TestAssert.Equal(0,OpeningPositionApproaches.Read(goal,f,P(10,10)).Length);
            f=Floor();f.Triangles[0].Enabled=false;TestAssert.Equal(0,OpeningPositionApproaches.Read(goal,f,P(10,10)).Length);
        }
        public static void InvalidOrTooNarrowGoalsDoNotInventAnApproach()
        {
            TestAssert.Equal(0,OpeningPositionApproaches.Read(null,Floor(),P(100,100)).Length);
            TestAssert.Equal(0,OpeningPositionApproaches.Read(new OpeningPositionGoal{Axis=OpeningGoalAxis.Z,Minimum=2800,Maximum=2805},Floor(),P(100,100)).Length);
        }
    }
}
