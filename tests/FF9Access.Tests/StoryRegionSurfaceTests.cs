using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class StoryRegionSurfaceTests
    {
        private static NavigationPoint Point(JsonElement a,bool flip=false){return new NavigationPoint(a[0].GetSingle(),(flip?-1:1)*a[1].GetSingle(),a[2].GetSingle());}
        private static NavigationPoint Project(JsonElement camera,NavigationPoint p)
        {
            var r=camera.GetProperty("r");var t=camera.GetProperty("t");double[] v={p.X,-p.Y,p.Z},q=new double[3];
            for(int i=0;i<3;i++){q[i]=t[i].GetDouble();for(int j=0;j<3;j++)q[i]+=r[3*i+j].GetDouble()/4096*v[j];}
            float distance=camera.GetProperty("proj").GetSingle();
            return new NavigationPoint((float)(q[0]*distance/Math.Abs(q[2])+camera.GetProperty("centerOffset")[0].GetDouble()+camera.GetProperty("w").GetDouble()/2-160),
                (float)(-q[1]*distance/Math.Abs(q[2])-camera.GetProperty("centerOffset")[1].GetDouble()-camera.GetProperty("h").GetDouble()/2+112),(float)q[2]);
        }
        public static void InstalledAlleyLadderAndForestHaveSafePresentedTriggerSurfaces()
        {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Story.RegionSurfaces.json"))using(var document=JsonDocument.Parse(stream)) {
                int regions=0;
                foreach(var field in document.RootElement.GetProperty("Fields").EnumerateArray()) {
                    var triangles=new List<FieldTriangle>();
                    foreach(var t in field.GetProperty("Geometry").GetProperty("tris").EnumerateArray()) {
                        var n=t.GetProperty("n").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
                        triangles.Add(new FieldTriangle{Id=t.GetProperty("id").GetInt32(),Floor=t.GetProperty("floor").GetInt32(),Enabled=t.GetProperty("enabled").GetBoolean(),Attributes=(byte)t.GetProperty("attr").GetInt32(),Vertices=t.GetProperty("v").EnumerateArray().Select(x=>Point(x,true)).ToArray(),Neighbors=new[]{n[1],n[2],n[0]}});
                    }
                    var geometry=new FieldGeometrySnapshot{Radius=80,AttributeMask=255,Triangles=triangles.ToArray()};
                    var native=field.GetProperty("Cameras")[0];
                    var camera=new FieldCameraPanorama{HalfWidth=160,HalfHeight=112,MinX=native.GetProperty("vrpMinX").GetInt32(),MaxX=native.GetProperty("vrpMaxX").GetInt32(),MinY=native.GetProperty("vrpMinY").GetInt32(),MaxY=native.GetProperty("vrpMaxY").GetInt32()};
                    foreach(var region in field.GetProperty("Regions").EnumerateArray()) {
                        var polygon=region.GetProperty("Polygon").EnumerateArray().Select(x=>Point(x)).ToArray();
                        var candidates=FieldRegionApproaches.Interaction(polygon,geometry);
                        bool alleyExit=field.GetProperty("Field").GetInt32()==105 && region.GetProperty("Sid").GetInt32()==11;
                        var selected=FieldStoryRegionApproaches.Read(true,true,candidates,p=>true,p=>alleyExit?FieldScenePresentation.Passage(true,true,camera,Project(native,p)):FieldPanoramaPresentation.Contains(camera,Project(native,p)),new NavigationPoint());
                        if(selected.Length==0)throw new Exception("Native trigger has no presentable safe floor: "+field.GetProperty("Field")+":"+region.GetProperty("Sid"));
                        if(alleyExit)TestAssert.False(candidates.Any(p=>FieldPanoramaPresentation.Contains(camera,Project(native,p))));
                        foreach(var point in selected){
                            bool inside=false;for(int i=0;i<polygon.Length;i++)inside|=new FieldTriangle{Vertices=new[]{polygon[i],polygon[(i+1)%polygon.Length],polygon[(i+2)%polygon.Length]}}.Contains(point);
                            TestAssert.True(inside);TestAssert.True(geometry.IsSegmentClear(point,point));
                        }
                        regions++;
                    }
                }
                TestAssert.Equal(4,regions);
            }
        }
    }
}
