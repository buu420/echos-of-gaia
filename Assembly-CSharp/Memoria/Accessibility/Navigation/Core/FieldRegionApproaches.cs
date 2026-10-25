using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    // Prospective surface points only. Native selection and the shown interaction
    // bubble still decide whether the ordinary Confirm action is available.
    public static class FieldRegionApproaches
    {
        public static NavigationPoint[] Interaction(NavigationPoint[] region,FieldGeometrySnapshot geometry)
        {return Samples(region,geometry,true);}
        public static NavigationPoint[] Presentation(NavigationPoint[] region,FieldGeometrySnapshot geometry)
        {return Samples(region,geometry,false);}
        private static NavigationPoint[] Samples(NavigationPoint[] region,FieldGeometrySnapshot geometry,bool requireClearance)
        {
            if(region==null || region.Length<3 || region.Length>16 || geometry==null)return new NavigationPoint[0];
            float minX=float.MaxValue,maxX=float.MinValue,minZ=float.MaxValue,maxZ=float.MinValue;
            foreach(var p in region){if(!p.IsFinite)return new NavigationPoint[0];minX=Math.Min(minX,p.X);maxX=Math.Max(maxX,p.X);minZ=Math.Min(minZ,p.Z);maxZ=Math.Max(maxZ,p.Z);}
            var result=new List<NavigationPoint>();
            foreach(float x in new[]{0.5f,0.25f,0.75f})foreach(float z in new[]{0.5f,0.25f,0.75f}) {
                var point=new NavigationPoint(minX+(maxX-minX)*x,0,minZ+(maxZ-minZ)*z);
                if(!Inside(region,point))continue;
                foreach(var triangle in geometry.Triangles)if(geometry.Permitted(triangle)&&triangle.Contains(point)) {
                    var surface=new NavigationPoint(point.X,triangle.Height(point),point.Z);
                    if(!requireClearance || geometry.IsSegmentClear(surface,surface))result.Add(surface);
                }
            }
            // A fixed grid misses thin regions and regions which only clip a
            // corner of the floor. Intersect each native consecutive-triple
            // polygon with actual floor triangles; preserve the floor's height.
            foreach(var triangle in geometry.Triangles)if(geometry.Permitted(triangle)) {
                for(int i=0;i<region.Length;i++) {
                    var a=region[i];var b=region[(i+1)%region.Length];var c=region[(i+2)%region.Length];
                    float winding=FieldTriangle.Cross(a,b,c);if(Math.Abs(winding)<.001f)continue;
                    var polygon=new List<NavigationPoint>(triangle.Vertices);
                    polygon=Clip(polygon,a,b,winding);polygon=Clip(polygon,b,c,winding);polygon=Clip(polygon,c,a,winding);
                    if(polygon.Count<3)continue;
                    if(requireClearance && geometry.Radius>0) {
                        // A tiny feasible corner may survive only after the
                        // player's radius is accounted for. Fixed centroid
                        // ratios cannot find that corner. Clip against the
                        // support triangle's real outer walls at radius offset;
                        // the native capsule check remains the final authority.
                        var clear=new List<NavigationPoint>(polygon);
                        for(int edge=0;edge<3;edge++)if(geometry.IsNativeOuterBoundary(triangle,edge)) {
                            var first=triangle.Vertices[edge];var second=triangle.Vertices[(edge+1)%3];
                            clear=Clip(clear,first,second,FieldTriangle.Cross(first,second,triangle.Center),WallOffset(triangle,first,second,geometry.Radius));
                        }
                        if(clear.Count>=3) {
                            var sum=new NavigationPoint();foreach(var p in clear)sum=new NavigationPoint(sum.X+p.X,sum.Y+p.Y,sum.Z+p.Z);
                            Add(result,new NavigationPoint(sum.X/clear.Count,sum.Y/clear.Count,sum.Z/clear.Count),region,geometry,true);
                        }
                    }
                    var center=new NavigationPoint();foreach(var p in polygon)center=new NavigationPoint(center.X+p.X,center.Y+p.Y,center.Z+p.Z);
                    center=new NavigationPoint(center.X/polygon.Count,center.Y/polygon.Count,center.Z/polygon.Count);
                    Add(result,center,region,geometry,requireClearance);
                    for(int p=0;p<polygon.Count;p++) {
                        var mid=Lerp(polygon[p],polygon[(p+1)%polygon.Count],.5f);
                        Add(result,Lerp(mid,center,.05f),region,geometry,requireClearance);
                        Add(result,Lerp(polygon[p],center,.1f),region,geometry,requireClearance);
                    }
                }
            }
            return result.ToArray();
        }
        private static double WallOffset(FieldTriangle triangle,NavigationPoint a,NavigationPoint b,float radius)
        {
            // On the floor plane, cross(XZ) = distance3D * edgeLength3D *
            // abs(unitNormal.Y). A horizontal radius offset over-clips slopes.
            var v=triangle.Vertices;double ux=v[1].X-v[0].X,uy=v[1].Y-v[0].Y,uz=v[1].Z-v[0].Z;
            double vx=v[2].X-v[0].X,vy=v[2].Y-v[0].Y,vz=v[2].Z-v[0].Z;
            double nx=uy*vz-uz*vy,ny=uz*vx-ux*vz,nz=ux*vy-uy*vx;
            double normal=Math.Sqrt(nx*nx+ny*ny+nz*nz),dx=b.X-a.X,dy=b.Y-a.Y,dz=b.Z-a.Z;
            return normal==0?0:radius*Math.Abs(ny)/normal*Math.Sqrt(dx*dx+dy*dy+dz*dz);
        }
        private static List<NavigationPoint> Clip(List<NavigationPoint> polygon,NavigationPoint a,NavigationPoint b,float winding,double offset=0)
        {
            var result=new List<NavigationPoint>();if(polygon.Count==0)return result;
            var previous=polygon[polygon.Count-1];double before=Side(a,b,previous,winding)-offset;
            foreach(var current in polygon) {
                double after=Side(a,b,current,winding)-offset;
                if((before>=0)!=(after>=0))result.Add(Lerp(previous,current,(float)(before/(before-after))));
                if(after>=0)result.Add(current);previous=current;before=after;
            }
            return result;
        }
        public static NavigationPoint[] BoundarySamples(NavigationPoint[] region,FieldGeometrySnapshot geometry)
        {
            var result=new List<NavigationPoint>();if(region==null || region.Length<3 || region.Length>16 || geometry==null)return result.ToArray();
            foreach(var p in region)if(!p.IsFinite)return result.ToArray();
            foreach(var triangle in geometry.Triangles)if(geometry.Permitted(triangle))for(int edge=0;edge<3;edge++)if(geometry.IsNativeOuterBoundary(triangle,edge)) {
                for(int i=0;i<region.Length;i++) {
                    var a=region[i];var b=region[(i+1)%region.Length];var c=region[(i+2)%region.Length];float winding=FieldTriangle.Cross(a,b,c);
                    if(Math.Abs(winding)<.001f)continue;
                    var from=triangle.Vertices[edge];var to=triangle.Vertices[(edge+1)%3];double low=0,high=1;
                    bool intersects=true;var corners=new[]{a,b,c};
                    for(int k=0;k<3;k++) {
                        double before=Side(corners[k],corners[(k+1)%3],from,winding),after=Side(corners[k],corners[(k+1)%3],to,winding);
                        if(before<0 && after<0){intersects=false;break;}
                        if((before>=0)!=(after>=0)){double t=before/(before-after);if(before<0)low=Math.Max(low,t);else high=Math.Min(high,t);}
                    }
                    if(intersects && low<=high)result.Add(Lerp(from,to,(float)((low+high)*.5)));
                }
            }
            return result.ToArray();
        }
        private static double Side(NavigationPoint a,NavigationPoint b,NavigationPoint p,float winding)
        {return (((double)b.X-a.X)*(p.Z-a.Z)-((double)b.Z-a.Z)*(p.X-a.X))*(winding>0?1:-1);}
        private static NavigationPoint Lerp(NavigationPoint a,NavigationPoint b,float t)
        {return new NavigationPoint(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,a.Z+(b.Z-a.Z)*t);}
        private static void Add(List<NavigationPoint> result,NavigationPoint point,NavigationPoint[] region,FieldGeometrySnapshot geometry,bool requireClearance)
        {
            if(!point.IsFinite || !Inside(region,point))return;
            foreach(var previous in result)if(NavigationPoint.Distance(previous,point)<.1f)return;
            if(!requireClearance || geometry.IsSegmentClear(point,point))result.Add(point);
        }
        public static NavigationPoint[] Viewing(NavigationContext context,NavigationPoint observed,FieldGeometrySnapshot geometry)
        {
            var start=geometry.Find(geometry.ActiveTriangle);if(!geometry.Permitted(start)||!start.Contains(context.Position))start=geometry.At(context.Position);
            if(start==null || !observed.IsFinite)return new NavigationPoint[0];
            var reachable=new HashSet<int>{start.Id};var pending=new Queue<FieldTriangle>();pending.Enqueue(start);
            while(pending.Count>0) {
                var triangle=pending.Dequeue();
                for(int edge=0;edge<3;edge++) {
                    int id=triangle.Neighbors[edge];NavigationPoint portal;
                    if(reachable.Contains(id)||!geometry.TryPortalPoint(triangle,edge,out portal))continue;
                    reachable.Add(id);pending.Enqueue(geometry.Find(id));
                }
            }
            var candidates=new List<NavigationPoint>();
            foreach(var triangle in geometry.Triangles)if(reachable.Contains(triangle.Id)) {
                var nearest=Closest(triangle,observed);var center=triangle.Center;
                for(int step=0;step<=20;step++) {
                    float t=step/20f;var point=new NavigationPoint(nearest.X+(center.X-nearest.X)*t,nearest.Y+(center.Y-nearest.Y)*t,nearest.Z+(center.Z-nearest.Z)*t);
                    if(geometry.IsSegmentClear(point,point)){candidates.Add(point);break;}
                }
            }
            candidates.Sort((a,b)=>FieldGeometrySnapshot.Horizontal(a,observed).CompareTo(FieldGeometrySnapshot.Horizontal(b,observed)));
            if(candidates.Count>8)candidates.RemoveRange(8,candidates.Count-8);
            return candidates.ToArray();
        }
        private static NavigationPoint Closest(FieldTriangle triangle,NavigationPoint point)
        {
            if(triangle.Contains(point))return new NavigationPoint(point.X,triangle.Height(point),point.Z);
            NavigationPoint best=triangle.Center;float distance=float.MaxValue;
            for(int edge=0;edge<3;edge++) {
                var a=triangle.Vertices[edge];var b=triangle.Vertices[(edge+1)%3];float dx=b.X-a.X,dz=b.Z-a.Z,squared=dx*dx+dz*dz;
                float t=squared==0?0:Math.Max(0,Math.Min(1,((point.X-a.X)*dx+(point.Z-a.Z)*dz)/squared));
                var projected=new NavigationPoint(a.X+t*dx,a.Y+t*(b.Y-a.Y),a.Z+t*dz);float candidate=FieldGeometrySnapshot.Horizontal(point,projected);
                if(candidate<distance){best=projected;distance=candidate;}
            }
            return best;
        }
        private static bool Inside(NavigationPoint[] region,NavigationPoint point)
        {
            // EventEngine.IsInQuad tests consecutive triples, including wrapping.
            for(int i=0;i<region.Length;i++) {
                var triangle=new FieldTriangle{Vertices=new[]{region[i],region[(i+1)%region.Length],region[(i+2)%region.Length]}};
                if(Math.Abs(FieldTriangle.Cross(triangle.Vertices[0],triangle.Vertices[1],triangle.Vertices[2]))>0.001f && triangle.ContainsStrict(point))return true;
            }
            return false;
        }
    }
}
