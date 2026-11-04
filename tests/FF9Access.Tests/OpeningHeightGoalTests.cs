using System;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class OpeningHeightGoalTests
    {
        private static NavigationPoint P(float x,float y,float z){return new NavigationPoint(x,y,z);}
        private static FieldGeometrySnapshot Ramp(){return new FieldGeometrySnapshot{Generation=1,Radius=2,ActiveTriangle=0,Triangles=new[]{new FieldTriangle{Id=0,Vertices=new[]{P(0,0,0),P(200,1000,0),P(0,0,200)}}}};}
        public static void HeightGoalUsesTheNativeRoundedVerticalCoordinate()
        {
            var goal=new OpeningPositionGoal{Axis=OpeningGoalAxis.Y,Minimum=450};
            TestAssert.True(goal.Satisfied(P(0,449.6f,0)));TestAssert.False(goal.Satisfied(P(0,449.4f,9000)));
            TestAssert.False(goal.Satisfied(P(0,0,9000)));TestAssert.True(goal.Satisfied(P(0,1440,0)));
        }
        public static void RisingFloorPointsLeaveEnoughMarginForHorizontalArrivalTolerance()
        {
            var goal=new OpeningPositionGoal{Axis=OpeningGoalAxis.Y,Minimum=450};
            var result=OpeningPositionApproaches.Read(goal,Ramp(),P(10,50,10));TestAssert.True(result.Length>0);
            foreach(var p in result){TestAssert.True(p.Y>=490-.01f);TestAssert.True(goal.Satisfied(P(p.X-4,p.Y-20,p.Z)));}
        }
        public static void DescendingFloorPointsRespectTheUpperHeightBoundary()
        {
            var goal=new OpeningPositionGoal{Axis=OpeningGoalAxis.Y,Maximum=500};
            var result=OpeningPositionApproaches.Read(goal,Ramp(),P(180,900,10));TestAssert.True(result.Length>0);
            foreach(var p in result){TestAssert.True(p.Y<=460+.01f);TestAssert.True(goal.Satisfied(P(p.X+4,p.Y+20,p.Z)));}
        }
        public static void AnUnreachableUpperFloorCannotSupplyAHeightGoal()
        {
            var g=new FieldGeometrySnapshot{Generation=1,Radius=2,ActiveTriangle=0,Triangles=new[]{
                new FieldTriangle{Id=0,Vertices=new[]{P(0,0,0),P(200,100,0),P(0,0,200)}},
                new FieldTriangle{Id=1,Vertices=new[]{P(0,1000,0),P(200,1000,0),P(0,1000,200)}}}};
            TestAssert.Equal(0,OpeningPositionApproaches.Read(new OpeningPositionGoal{Axis=OpeningGoalAxis.Y,Minimum=450},g,P(10,5,10)).Length);
        }
    }
}
