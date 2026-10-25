using System;
using System.Linq;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldExitSurfaceTests
    {
        private static NavigationPoint P(float x,float z) {return new NavigationPoint(x,x*.5f+z*.25f,z);}
        public static void NarrowCornerIntersectionIsNotLostBetweenBoundingBoxSamples()
        {
            var geometry=new FieldGeometrySnapshot{Radius=0,Triangles=new[]{new FieldTriangle{Id=0,Vertices=new[]{P(0,0),P(200,0),P(0,200)}}}};
            var region=new[]{P(150,10),P(300,10),P(300,50),P(150,50)};
            var candidates=FieldRegionApproaches.Interaction(region,geometry);
            TestAssert.True(candidates.Length>0);
            foreach(var point in candidates){TestAssert.True(geometry.IsSegmentClear(point,point));TestAssert.True(point.X>=150 && point.Z>=10 && point.Z<=50);TestAssert.True(Math.Abs(point.Y-(point.X*.5f+point.Z*.25f))<.01f);}
        }
        public static void VisibleTriggerSurfaceDoesNotRequireTheRemoteOuterEdgeInView()
        {
            var near=P(170,20);var far=P(175,5);NavigationPoint selected;
            TestAssert.True(FieldDoorEvidence.TryTriggerSurface(true,true,new[]{far,near},p=>p.Z>=10,p=>true,P(160,20),out selected));
            TestAssert.Equal(near,selected);
        }
        public static void WrongNativeRegionHiddenSurfaceAndUnprovenWalkDoNotBecomeExits()
        {
            var point=P(170,20);NavigationPoint selected;
            TestAssert.False(FieldDoorEvidence.TryTriggerSurface(false,true,new[]{point},p=>true,p=>true,point,out selected));
            TestAssert.False(FieldDoorEvidence.TryTriggerSurface(true,false,new[]{point},p=>true,p=>true,point,out selected));
            TestAssert.False(FieldDoorEvidence.TryTriggerSurface(true,true,new[]{point},p=>false,p=>true,point,out selected));
            TestAssert.False(FieldDoorEvidence.TryTriggerSurface(true,true,new[]{point},p=>true,p=>false,point,out selected));
        }
        public static void DisabledOrDisjointMeshHasNoRegionApproach()
        {
            var geometry=new FieldGeometrySnapshot{Radius=0,Triangles=new[]{new FieldTriangle{Id=0,Enabled=false,Vertices=new[]{P(0,0),P(200,0),P(0,200)}}}};
            var region=new[]{P(150,10),P(300,10),P(300,50),P(150,50)};
            TestAssert.Equal(0,FieldRegionApproaches.Interaction(region,geometry).Length);
            geometry.Triangles[0].Enabled=true;
            TestAssert.Equal(0,FieldRegionApproaches.Interaction(new[]{P(400,400),P(500,400),P(400,500)},geometry).Length);
        }
        public static void ThinBoundaryIntersectionDoesNotDependOnEightEdgeSamples()
        {
            var geometry=new FieldGeometrySnapshot{Triangles=new[]{new FieldTriangle{Id=0,Vertices=new[]{P(0,0),P(200,0),P(0,200)}}}};
            var region=new[]{P(13,-10),P(17,-10),P(17,10),P(13,10)};
            var boundaries=FieldRegionApproaches.BoundarySamples(region,geometry);
            TestAssert.True(boundaries.Any(p=>p.X>=13 && p.X<=17 && Math.Abs(p.Z)<.01f));
        }
        public static void PlayerRadiusKeepsTheSmallClearCornerOfATrigger()
        {
            var geometry=new FieldGeometrySnapshot{Radius=80,Triangles=new[]{new FieldTriangle{Id=0,Vertices=new[]{new NavigationPoint(0,0,0),new NavigationPoint(1000,0,0),new NavigationPoint(0,0,1000)}}}};
            var region=new[]{new NavigationPoint(0,0,0),new NavigationPoint(82,0,0),new NavigationPoint(82,0,82),new NavigationPoint(0,0,82)};
            TestAssert.True(geometry.IsSegmentClear(new NavigationPoint(81,0,81),new NavigationPoint(81,0,81)));
            TestAssert.True(FieldRegionApproaches.Interaction(region,geometry).Length>0);
        }
        public static void VisibleTriggerCanRemainInformativeWhenPlayerCannotFit()
        {
            var geometry=new FieldGeometrySnapshot{Radius=80,Triangles=new[]{new FieldTriangle{Id=0,Vertices=new[]{new NavigationPoint(0,0,0),new NavigationPoint(1000,0,0),new NavigationPoint(0,0,1000)}}}};
            var region=new[]{new NavigationPoint(0,0,0),new NavigationPoint(40,0,0),new NavigationPoint(40,0,40),new NavigationPoint(0,0,40)};
            TestAssert.Equal(0,FieldRegionApproaches.Interaction(region,geometry).Length);
            TestAssert.True(FieldRegionApproaches.Presentation(region,geometry).Length>0);
        }
        public static void RadiusClippingUsesTheNativeThreeDimensionalDistanceOnSlopes()
        {
            var geometry=new FieldGeometrySnapshot{Radius=80,Triangles=new[]{new FieldTriangle{Id=0,Vertices=new[]{new NavigationPoint(0,0,0),new NavigationPoint(1000,1000,0),new NavigationPoint(0,0,1000)}}}};
            var region=new[]{new NavigationPoint(0,0,0),new NavigationPoint(59,0,0),new NavigationPoint(59,0,82),new NavigationPoint(0,0,82)};
            TestAssert.True(geometry.IsSegmentClear(new NavigationPoint(58,58,81),new NavigationPoint(58,58,81)));
            var candidates=FieldRegionApproaches.Interaction(region,geometry);
            TestAssert.True(candidates.Length>0);
            foreach(var point in candidates){TestAssert.True(geometry.IsSegmentClear(point,point));TestAssert.True(Math.Abs(point.Y-point.X)<.01f);}
        }
    }
}
