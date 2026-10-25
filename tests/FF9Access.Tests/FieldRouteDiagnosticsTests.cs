using System;
using System.Globalization;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class FieldRouteDiagnosticsTests
    {
        public static void LegacyNameOnlyReflectionLookupStillInvokesFourArgumentSerializer()
        {
            // Original Mono WorldPlanningProbe resolves this entry by name alone.
            var method=typeof(FieldRouteDiagnostics).GetMethod("Serialize");
            TestAssert.True(method!=null);
            var context=TestFixtures.Context();
            var json=(string)method.Invoke(null,new object[]{context,new NavigationTarget(),FieldRoutesTests.Grid(false),new RoutePlan{Success=true}});
            using(var document=JsonDocument.Parse(json)) {
                TestAssert.Equal(1,document.RootElement.GetProperty("Schema").GetInt32());
                TestAssert.True(document.RootElement.GetProperty("Route").GetProperty("Success").GetBoolean());
            }
        }
        public static void RawNativePayloadPreservesDifferentCoordinateAndNeighborContracts()
        {
            var context=TestFixtures.Context();
            var raw=new FieldNativeCapture{Map=611,Camera=2,Frame=42,ActiveTriangle=0,LastTriangle=7,ActiveFloor=1,LastFloor=0,
                CurrentPosition=new NavigationPoint(1,-13,3),LastPosition=new NavigationPoint(1,-20,3),
                BgiOriginalPosition=new NavigationPoint(10,20,30),BgiCurrentPosition=new NavigationPoint(11,22,33),
                Floors=new[]{new FieldNativeCaptureFloor{Index=1,OriginalPosition=new NavigationPoint(0,0,0),CurrentPosition=new NavigationPoint(0,7,0)}},
                Triangles=new[]{new FieldNativeCaptureTriangle{Index=0,OriginalVertices=new[]{new NavigationPoint(1,-20,3)},RawVertices=new[]{new NavigationPoint(1,20,3)},RuntimeNeighbors=new[]{1,2,3},NativeNeighbors=new[]{4,5,6},VertexIndices=new[]{10,11,12},EdgeIndices=new[]{20,21,22},EdgeClones=new[]{0,-1,2},Normal=new NavigationPoint(0,65536,0),Plane=-15}}};
            using(var document=JsonDocument.Parse(FieldRouteDiagnostics.SerializeWithNativeCapture(context,new NavigationTarget(),FieldRoutesTests.Grid(false),new RoutePlan{Success=true},raw))) {
                var native=document.RootElement.GetProperty("Native");
                TestAssert.Equal(1,native.GetProperty("Schema").GetInt32());TestAssert.Equal(42,native.GetProperty("Frame").GetInt32());
                TestAssert.Equal(-13f,native.GetProperty("CurrentPosition").GetProperty("Y").GetSingle());
                TestAssert.Equal(7f,native.GetProperty("Floors")[0].GetProperty("CurrentPosition").GetProperty("Y").GetSingle());
                var triangle=native.GetProperty("Triangles")[0];
                TestAssert.Equal(-20f,triangle.GetProperty("OriginalVertices")[0].GetProperty("Y").GetSingle());
                TestAssert.Equal(20f,triangle.GetProperty("RawVertices")[0].GetProperty("Y").GetSingle());
                TestAssert.Equal(1,triangle.GetProperty("RuntimeNeighbors")[0].GetInt32());TestAssert.Equal(4,triangle.GetProperty("NativeNeighbors")[0].GetInt32());
                TestAssert.Equal(-1,triangle.GetProperty("EdgeClones")[1].GetInt32());TestAssert.True(document.RootElement.GetProperty("Route").GetProperty("Success").GetBoolean());
            }
        }
        public static void DetachedSnapshotRetainsExactTopologyAndValues()
        {
            var context=TestFixtures.Context();context.CurrentLocation="Room \"one\"\nsecond line";
            var target=new NavigationTarget{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,16),Generation=1,Position=new NavigationPoint(1.25f,2.5f,-3.75f)};
            var geometry=FieldRoutesTests.Grid(false);geometry.ActiveTriangle=1;geometry.Fingerprint="fixture";
            var previous=CultureInfo.CurrentCulture;
            try {
                CultureInfo.CurrentCulture=new CultureInfo("fr-FR");
                using(var json=JsonDocument.Parse(FieldRouteDiagnostics.Serialize(context,target,geometry,new RoutePlan{Failure=RouteFailure.Disconnected}))) {
                    var root=json.RootElement;TestAssert.Equal(1,root.GetProperty("Schema").GetInt32());
                    TestAssert.Equal(context.CurrentLocation,root.GetProperty("Context").GetProperty("CurrentLocation").GetString());
                    TestAssert.Equal(1.25f,root.GetProperty("Target").GetProperty("Position").GetProperty("X").GetSingle());
                    var copy=root.GetProperty("Geometry");TestAssert.Equal(1,copy.GetProperty("ActiveTriangle").GetInt32());
                    TestAssert.Equal(geometry.Triangles.Length,copy.GetProperty("Triangles").GetArrayLength());
                    TestAssert.Equal(geometry.Triangles[1].Neighbors[0],copy.GetProperty("Triangles")[1].GetProperty("Neighbors")[0].GetInt32());
                    TestAssert.Equal((int)RouteFailure.Disconnected,root.GetProperty("Route").GetProperty("Failure").GetInt32());
                }
            }finally {CultureInfo.CurrentCulture=previous;}
        }
    }
}
