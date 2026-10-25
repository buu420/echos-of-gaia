using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public sealed class FieldTriangle
    {
        public int Id,Floor;public NavigationPoint[] Vertices;public int[] Neighbors={-1,-1,-1};public bool Enabled=true,RuntimeEnabled=true;public byte Attributes;public float TraversalCost=1;
        public NavigationPoint[] RadiusVertices;public FieldNativeTriangleData Native;
        public NavigationPoint Center { get { return new NavigationPoint((Vertices[0].X+Vertices[1].X+Vertices[2].X)/3,(Vertices[0].Y+Vertices[1].Y+Vertices[2].Y)/3,(Vertices[0].Z+Vertices[1].Z+Vertices[2].Z)/3); } }
        public bool Contains(NavigationPoint point)
        {
            return Contains(point,0.01f);
        }
        internal bool ContainsStrict(NavigationPoint point) {return Contains(point,0);}
        private bool Contains(NavigationPoint point,float tolerance)
        {
            float a=Cross(Vertices[0],Vertices[1],point),b=Cross(Vertices[1],Vertices[2],point),c=Cross(Vertices[2],Vertices[0],point);
            // Cross products have area units. Scale the tolerance by edge length to
            // retain a 0.01-unit distance bound after ordinary field-coordinate rounding.
            float ta=FieldGeometrySnapshot.Horizontal(Vertices[0],Vertices[1])*tolerance,tb=FieldGeometrySnapshot.Horizontal(Vertices[1],Vertices[2])*tolerance,tc=FieldGeometrySnapshot.Horizontal(Vertices[2],Vertices[0])*tolerance;
            return !(a< -ta || b< -tb || c< -tc) || !(a>ta || b>tb || c>tc);
        }
        public float Height(NavigationPoint point)
        {
            var a=Vertices[0];var b=Vertices[1];var c=Vertices[2];float determinant=Cross(a,b,c);
            if(Math.Abs(determinant)<0.0001f)return Center.Y;
            float u=Cross(point,b,c)/determinant,v=Cross(a,point,c)/determinant,w=1-u-v;return u*a.Y+v*b.Y+w*c.Y;
        }
        internal static float Cross(NavigationPoint a,NavigationPoint b,NavigationPoint c) { return (b.X-a.X)*(c.Z-a.Z)-(b.Z-a.Z)*(c.X-a.X); }
    }
    public sealed class FieldGeometrySnapshot
    {
        public long Generation;public FieldTriangle[] Triangles=new FieldTriangle[0];public float Radius;public int ActiveTriangle=-1;public byte AttributeMask;public byte BlockedAttributes=0x80;public string Fingerprint;
        public FieldTriangle Find(int id) { foreach(var tri in Triangles)if(tri.Id==id)return tri;return null; }
        public bool Permitted(FieldTriangle tri) { return tri!=null && tri.Enabled && tri.RuntimeEnabled && (tri.Attributes&AttributeMask&BlockedAttributes)==0 && tri.Vertices!=null && tri.Vertices.Length==3; }
        // BGI_findAccessibleTriangle gates the referenced BGI flags, never the
        // cached runtime enabled bit or the existence of a support/crossing portal.
        private bool RadiusPermitted(FieldTriangle tri) {return tri!=null&&tri.Enabled&&(tri.Attributes&AttributeMask&BlockedAttributes)==0;}
        public bool IsNativeOuterBoundary(FieldTriangle tri,int edge)
        {
            return tri!=null&&edge>=0&&edge<3&&tri.Neighbors!=null&&tri.Neighbors.Length==3&&tri.Neighbors[edge]<0&&
                (tri.Native==null || (tri.Native.NativeNeighbors!=null&&tri.Native.NativeNeighbors.Length==3&&tri.Native.NativeNeighbors[edge]<0));
        }
        public FieldTriangle At(NavigationPoint point)
        {
            FieldTriangle best=null;float height=float.MaxValue;bool strict=false;
            foreach(var tri in Triangles)if(Permitted(tri)&&tri.Contains(point)) {
                float difference=Math.Abs(tri.Height(point)-point.Y);bool candidateStrict=tri.ContainsStrict(point);
                if(difference<height-0.01f || (Math.Abs(difference-height)<=0.01f&&candidateStrict&&!strict)){height=difference;best=tri;strict=candidateStrict;}
            }
            return height<=128?best:null;
        }
        public bool IsSegmentClear(NavigationPoint from,NavigationPoint to)
        {
            NavigationPoint[] surface;return TrySegmentClear(from,to,out surface);
        }
        public bool TrySegmentClear(NavigationPoint from,NavigationPoint to,out NavigationPoint[] surface)
        {
            surface=null;if(!from.IsFinite || !to.IsFinite || float.IsNaN(Radius)||float.IsInfinity(Radius))return false;
            var start=At(from);var end=At(to);var active=Find(ActiveTriangle);
            if(Permitted(active)&&active.Contains(from)&&Math.Abs(active.Height(from)-from.Y)<0.02f)start=active;
            if(start==null || end==null)return false;
            var queue=new Queue<int>();var entries=new Dictionary<int,float>{{start.Id,0}};
            var parents=new Dictionary<int,int>();queue.Enqueue(start.Id);
            // The centerline may cross only actual native neighbors through their shared 3D portal.
            // A triangle's convex interval on a straight line is visited at its earliest entry.
            while(queue.Count>0) {
                int id=queue.Dequeue();var tri=Find(id);
                if(id==end.Id)break;
                for(int edge=0;edge<3;edge++) {
                    var next=Find(tri.Neighbors[edge]);if(!ContinuousPortal(tri,edge,next))continue;
                    float low,high;if(!PortalInterval(from,to,tri.Vertices[edge],tri.Vertices[(edge+1)%3],out low,out high))continue;
                    float entry=Math.Max(entries[id],low),previous;
                    if(entry>high+0.000001f || !tri.Contains(Lerp(from,to,entry)) || !next.Contains(Lerp(from,to,entry)))continue;
                    if(entries.TryGetValue(next.Id,out previous)&&previous<=entry+0.000001f)continue;
                    entries[next.Id]=entry;parents[next.Id]=id;queue.Enqueue(next.Id);
                }
            }
            if(!entries.ContainsKey(end.Id))return false;
            var path=new List<int>{end.Id};while(parents.ContainsKey(path[path.Count-1])){path.Add(parents[path[path.Count-1]]);if(path.Count>Triangles.Length)return false;}path.Reverse();
            var pieces=new List<NavigationPoint>();var times=new List<float>();
            for(int i=0;i<path.Count;i++) {
                var tri=Find(path[i]);float low=entries[path[i]],high=i+1<path.Count?entries[path[i+1]]:1;
                var a=OnSurface(tri,Lerp(from,to,low));var b=OnSurface(tri,Lerp(from,to,high));
                if(!tri.Contains(a)||!tri.Contains(b)||!ClearLocalCapsule(tri,a,b,i==0))return false;
                pieces.Add(b);times.Add(high);
            }
            var result=new List<NavigationPoint>();var first=OnSurface(start,from);var last=OnSurface(end,to);
            for(int i=0;i+1<pieces.Count;i++)if(Math.Abs(pieces[i].Y-Lerp(first,last,times[i]).Y)>0.02f) {
                if(result.Count==0 || NavigationPoint.Distance(result[result.Count-1],pieces[i])>0.01f)result.Add(pieces[i]);
            }
            result.Add(last);surface=result.ToArray();return true;
        }
        private bool ContinuousPortal(FieldTriangle tri,int edge,FieldTriangle next)
        {
            if(!Permitted(next))return false;
            var a=tri.Vertices[edge];var b=tri.Vertices[(edge+1)%3];
            if(tri.Native!=null || next.Native!=null) {
                if(tri.Native==null || next.Native==null || tri.Native.NativeNeighbors[edge]!=tri.Neighbors[edge])return false;
                int clone=tri.Native.EdgeClones[edge];if(clone<0||clone>2)return false;
                int otherEdge=(clone+2)%3;
                if(!SameEdge(a,b,next.Vertices[otherEdge],next.Vertices[(otherEdge+1)%3]))return false;
                // The planner works in XZ. Unsupported YZ/XY projection and moving
                // floor crossings that disagree with controller support fail closed.
                if(tri.Native.Projection!=0 || next.Native.Projection!=0)return false;
                var c=tri.Native.CrossingVertices[edge];var d=tri.Native.CrossingVertices[(edge+1)%3];
                if(!SameEdge(c,d,next.Native.CrossingVertices[otherEdge],next.Native.CrossingVertices[(otherEdge+1)%3]))return false;
                // Native traversal flips the crossing point's Y before committing.
                return SameEdge(a,b,new NavigationPoint(c.X,-c.Y,c.Z),new NavigationPoint(d.X,-d.Y,d.Z));
            }
            for(int i=0;i<3;i++) {
                var c=next.Vertices[i];var d=next.Vertices[(i+1)%3];
                if(SameEdge(a,b,c,d))return true;
            }return false;
        }
        private static bool SameEdge(NavigationPoint a,NavigationPoint b,NavigationPoint c,NavigationPoint d)
        {
            return (NavigationPoint.Distance(a,c)<0.01f&&NavigationPoint.Distance(b,d)<0.01f)||(NavigationPoint.Distance(a,d)<0.01f&&NavigationPoint.Distance(b,c)<0.01f);
        }
        private bool ClearLocalCapsule(FieldTriangle support,NavigationPoint from,NavigationPoint to,bool departure=false)
        {
            var blocked=new List<CapsuleSpan>();if(!ScanLocalCapsule(support,from,to,blocked))return false;
            if(blocked.Count==0)return true;if(!departure)return false;
            foreach(var contact in blocked) {
                // An existing overlap may only recede from the starting point.
                // Every contacted boundary must regain full clearance before this
                // support leg ends; contacts first encountered later still block.
                if(contact.Low>0.000001f || contact.High>=1)return false;
                var edge=Subtract(contact.B,contact.A);float squared=Dot(edge,edge);
                float t=squared<0.000001f?0:Clamp(Dot(Subtract(from,contact.A),edge)/squared);
                var normal=Subtract(from,Lerp(contact.A,contact.B,t));
                if(Dot(Subtract(to,from),normal)<0)return false;
            }
            return true;
        }
        public bool TryPortalPoint(FieldTriangle support,int edge,out NavigationPoint point)
        {
            point=new NavigationPoint();if(!ContinuousPortal(support,edge,Find(support.Neighbors[edge])))return false;
            var a=support.Vertices[edge];var b=support.Vertices[(edge+1)%3];var blocked=new List<CapsuleSpan>();
            if(!ScanLocalCapsule(support,a,b,blocked))return false;
            blocked.Sort(delegate(CapsuleSpan x,CapsuleSpan y){return x.Low.CompareTo(y.Low);});
            float cursor=0,best=-1,bestDistance=float.MaxValue;
            // Choose from the exact complement of boundary collision intervals on the portal.
            for(int i=0;i<=blocked.Count;i++) {
                float end=i<blocked.Count?blocked[i].Low:1;
                if(end-cursor>0.00001f) {
                    // Use the middle of the clear interval. Clamping the portal midpoint
                    // to a collision root leaves no clearance for the next native turn.
                    float t=(cursor+end)*0.5f,distance=Math.Abs(t-0.5f);
                    if(distance<bestDistance){best=t;bestDistance=distance;}
                }
                if(i<blocked.Count)cursor=Math.Max(cursor,blocked[i].High);
            }
            if(best<0)return false;point=Lerp(a,b,best);return ClearLocalCapsule(support,point,point);
        }
        private bool ScanLocalCapsule(FieldTriangle support,NavigationPoint from,NavigationPoint to,List<CapsuleSpan> blocked)
        {
            float radius=Math.Max(0,Radius-0.01f);if(radius==0)return true;
            var queue=new Queue<CapsuleSpan>();var visited=new Dictionary<int,List<CapsuleSpan>>();
            EnqueueSpan(queue,visited,new CapsuleSpan{Triangle=support,Low=0,High=1});int work=0;
            while(queue.Count>0) {
                if(++work>Triangles.Length*64)return false;
                var span=queue.Dequeue();var tri=span.Triangle;
                var first=Lerp(from,to,span.Low);var last=Lerp(from,to,span.High);
                for(int edge=0;edge<3;edge++) {
                    var radiusVertices=tri.RadiusVertices??tri.Vertices;
                    var a=radiusVertices[edge];var b=radiusVertices[(edge+1)%3];
                    float low,high;if(!NearEdgeInterval(first,last,a,b,radius,out low,out high))continue;
                    var next=Find(tri.Neighbors[edge]);
                    if(!RadiusPermitted(next)) {
                        blocked.Add(new CapsuleSpan{Low=span.Low+(span.High-span.Low)*low,High=span.Low+(span.High-span.Low)*high,A=a,B=b});continue;
                    }
                    // Reachability of a neighboring floor belongs only to this part of the swept leg.
                    EnqueueSpan(queue,visited,new CapsuleSpan{Triangle=next,Low=span.Low+(span.High-span.Low)*low,High=span.Low+(span.High-span.Low)*high});
                }
            }return true;
        }
        private struct CapsuleSpan {public FieldTriangle Triangle;public float Low,High;public NavigationPoint A,B;}
        private static void EnqueueSpan(Queue<CapsuleSpan> queue,Dictionary<int,List<CapsuleSpan>> visited,CapsuleSpan span)
        {
            List<CapsuleSpan> ranges;
            if(!visited.TryGetValue(span.Triangle.Id,out ranges)){ranges=new List<CapsuleSpan>();visited[span.Triangle.Id]=ranges;}
            foreach(var previous in ranges)if(previous.Low<=span.Low+0.000001f&&previous.High>=span.High-0.000001f)return;
            ranges.Add(span);queue.Enqueue(span);
        }
        private static bool NearEdgeInterval(NavigationPoint from,NavigationPoint to,NavigationPoint a,NavigationPoint b,float radius,out float low,out float high)
        {
            float nearest;low=0;high=1;if(SegmentDistance(from,to,a,b,out nearest)>=radius)return false;
            // Point-to-edge distance along a straight leg is convex. Keep the outside side of
            // each bounded root so round-off can only enlarge the checked collision interval.
            if(PointDistance(from,a,b)>=radius) {
                float outside=0,inside=nearest;for(int i=0;i<24;i++){float middle=(outside+inside)*0.5f;if(PointDistance(Lerp(from,to,middle),a,b)<radius)inside=middle;else outside=middle;}low=outside;
            }
            if(PointDistance(to,a,b)>=radius) {
                float inside=nearest,outside=1;for(int i=0;i<24;i++){float middle=(outside+inside)*0.5f;if(PointDistance(Lerp(from,to,middle),a,b)<radius)inside=middle;else outside=middle;}high=outside;
            }return true;
        }
        private static NavigationPoint OnSurface(FieldTriangle tri,NavigationPoint point) {return new NavigationPoint(point.X,tri.Height(point),point.Z);}
        private static bool PortalInterval(NavigationPoint from,NavigationPoint to,NavigationPoint a,NavigationPoint b,out float low,out float high)
        {
            low=high=0;float x=to.X-from.X,z=to.Z-from.Z,ex=b.X-a.X,ez=b.Z-a.Z,qx=a.X-from.X,qz=a.Z-from.Z;
            float determinant=x*ez-z*ex;
            if(Math.Abs(determinant)>0.000001f) {
                float t=(qx*ez-qz*ex)/determinant,u=(qx*z-qz*x)/determinant;
                if(t< -0.000001f||t>1.000001f||u< -0.000001f||u>1.000001f)return false;
                low=high=Clamp(t);return true;
            }
            float squared=x*x+z*z;
            if(squared<0.000001f) {low=0;high=1;return PointDistance(from,a,b)<0.02f&&PointDistance(to,a,b)<0.02f;}
            if(Math.Abs(qx*z-qz*x)>0.001f)return false;
            float first=(qx*x+qz*z)/squared,second=((b.X-from.X)*x+(b.Z-from.Z)*z)/squared;
            low=Math.Max(0,Math.Min(first,second));high=Math.Min(1,Math.Max(first,second));return low<=high;
        }
        internal static float Horizontal(NavigationPoint a,NavigationPoint b) { float x=a.X-b.X,z=a.Z-b.Z;return (float)Math.Sqrt(x*x+z*z); }
        internal static NavigationPoint Lerp(NavigationPoint a,NavigationPoint b,float t) {return new NavigationPoint(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,a.Z+(b.Z-a.Z)*t);}
        private static float Clamp(float value) {return Math.Max(0,Math.Min(1,value));}
        private static NavigationPoint Subtract(NavigationPoint a,NavigationPoint b) {return new NavigationPoint(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
        private static float Dot(NavigationPoint a,NavigationPoint b) {return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
        private static float PointDistance(NavigationPoint point,NavigationPoint a,NavigationPoint b)
        {
            var edge=Subtract(b,a);float squared=Dot(edge,edge),t=squared<0.000001f?0:Clamp(Dot(Subtract(point,a),edge)/squared);return NavigationPoint.Distance(point,Lerp(a,b,t));
        }
        private static float SegmentDistance(NavigationPoint a,NavigationPoint b,NavigationPoint c,NavigationPoint d,out float nearest)
        {
            var u=Subtract(b,a);var v=Subtract(d,c);var w=Subtract(a,c);
            float uu=Dot(u,u),vv=Dot(v,v),uv=Dot(u,v),uw=Dot(u,w),vw=Dot(v,w),s,t;
            if(uu<0.000001f){s=0;t=vv<0.000001f?0:Clamp(vw/vv);}
            else if(vv<0.000001f){t=0;s=Clamp(-uw/uu);}
            else {
                float determinant=uu*vv-uv*uv;s=determinant>0.000001f?Clamp((uv*vw-uw*vv)/determinant):0;t=(uv*s+vw)/vv;
                if(t<0){t=0;s=Clamp(-uw/uu);}else if(t>1){t=1;s=Clamp((uv-uw)/uu);}
            }nearest=s;return NavigationPoint.Distance(Lerp(a,b,s),Lerp(c,d,t));
        }
    }
}
