using System;
using System.IO;
using System.Security.Cryptography;
namespace Memoria.Accessibility.Navigation
{
    // Detached native inputs. Adjacency arrays use geometry edge order [1,2,0];
    // clone VALUES retain native edge numbering, as BGI_traverseTriangles does.
    public sealed class FieldNativeTriangleData
    {
        public NavigationPoint[] OriginalVertices,CrossingVertices;
        public NavigationPoint FloorDelta,Normal;
        public int[] RuntimeNeighbors,NativeNeighbors,EdgeClones;
        public int RuntimeFlags,NativeFlags,NormalIndex=-1,NormalInverseY,Plane,ThetaX,ThetaZ;
        public int Projection; // 0 XZ, 1 YZ, 2 XY. Steep crossings remain unsupported.
    }
    public static class FieldNativeGeometryContract
    {
        public static FieldTriangle CreateTriangle(int id,int floor,int map,FieldNativeTriangleData native)
        {
            if(native==null || !Points(native.OriginalVertices) || !Points(native.CrossingVertices) ||
                !Three(native.RuntimeNeighbors) || !Three(native.NativeNeighbors) || !Three(native.EdgeClones) ||
                !native.FloorDelta.IsFinite || !native.Normal.IsFinite || native.Projection<0 || native.Projection>2)return null;
            var support=new NavigationPoint[3];var radius=new NavigationPoint[3];
            for(int i=0;i<3;i++) {
                var original=native.OriginalVertices[i];var delta=native.FloorDelta;
                support[i]=new NavigationPoint(original.X,original.Y+delta.Y,original.Z);
                radius[i]=map==2952?new NavigationPoint(original.X+delta.X,original.Y+delta.Y,original.Z+delta.Z):original;
            }
            return new FieldTriangle{Id=id,Floor=floor,Vertices=support,RadiusVertices=radius,Neighbors=native.RuntimeNeighbors,
                Enabled=(native.NativeFlags&1)!=0,RuntimeEnabled=(native.RuntimeFlags&1)!=0,Attributes=(byte)(native.NativeFlags>>8),Native=native};
        }
        public static int Projection(int normalIndex,NavigationPoint normal)
        {
            if(normalIndex<0 || normal.Y>20724 || normal.Y< -20724)return 0;
            return Math.Abs(normal.X)>Math.Abs(normal.Z)?1:2;
        }
        public static string Fingerprint(string map,FieldGeometrySnapshot geometry)
        {
            // Typed binary encoding avoids reflection and large JSON allocations on
            // every owned frame. Player position/active triangle are not geometry.
            using(var stream=new MemoryStream(64+geometry.Triangles.Length*384))
            using(var writer=new BinaryWriter(stream)) {
                writer.Write(map??"");writer.Write(geometry.Radius);writer.Write(geometry.AttributeMask);writer.Write(geometry.BlockedAttributes);writer.Write(geometry.Triangles.Length);
                foreach(var tri in geometry.Triangles) {
                    writer.Write(tri.Id);writer.Write(tri.Floor);writer.Write(tri.Enabled);writer.Write(tri.RuntimeEnabled);writer.Write(tri.Attributes);writer.Write(tri.TraversalCost);
                    Write(writer,tri.Vertices);Write(writer,tri.RadiusVertices);Write(writer,tri.Neighbors);
                    var native=tri.Native;writer.Write(native!=null);if(native==null)continue;
                    Write(writer,native.OriginalVertices);Write(writer,native.CrossingVertices);Write(writer,native.FloorDelta);Write(writer,native.Normal);
                    Write(writer,native.RuntimeNeighbors);Write(writer,native.NativeNeighbors);Write(writer,native.EdgeClones);
                    // Low bit 0x80 is native radius/traversal visitation scratch,
                    // not the high-byte 0x80 collision attribute. RadiusValid
                    // sets it while pushing away from a boundary.
                    writer.Write(native.RuntimeFlags&~0x80);writer.Write(native.NativeFlags&~0x80);writer.Write(native.NormalIndex);writer.Write(native.NormalInverseY);
                    writer.Write(native.Plane);writer.Write(native.ThetaX);writer.Write(native.ThetaZ);writer.Write(native.Projection);
                }
                writer.Flush();stream.Position=0;
                using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
            }
        }
        private static void Write(BinaryWriter writer,NavigationPoint point){writer.Write(point.X);writer.Write(point.Y);writer.Write(point.Z);}
        private static void Write(BinaryWriter writer,NavigationPoint[] points){writer.Write(points==null?-1:points.Length);if(points!=null)foreach(var point in points)Write(writer,point);}
        private static void Write(BinaryWriter writer,int[] values){writer.Write(values==null?-1:values.Length);if(values!=null)foreach(int value in values)writer.Write(value);}
        private static bool Three(int[] values){return values!=null&&values.Length==3;}
        private static bool Points(NavigationPoint[] values){return values!=null&&values.Length==3&&values[0].IsFinite&&values[1].IsFinite&&values[2].IsFinite;}
    }
}
