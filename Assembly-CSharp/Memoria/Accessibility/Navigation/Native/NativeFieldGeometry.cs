using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
namespace Memoria.Accessibility.Navigation
{
    public static class NativeFieldGeometry
    {
        private static long _captureGeneration;
        private static readonly HashSet<string> _captures=new HashSet<string>();
        private static long _interruptionGeneration;
        private static int _interruptionCount;
        private static int _queuedCaptures;
        private static FieldGeometrySnapshot _geometry;
        private static string _geometryMap;
        private static int _geometryNativeMap;

        public static void CaptureInterruption(NavigationContext context,NavigationTarget target,RoutePlan route,FieldMovementCapture movement)
        {
            if(context==null || target==null || route==null || movement==null || route.Generation!=context.Generation)return;
            if(_interruptionGeneration!=context.Generation){_interruptionGeneration=context.Generation;_interruptionCount=0;}
            if(_interruptionCount>=8)return;
            _interruptionCount++;
            // This is an event-only capture after movement release, not frame work.
            var geometry=Read(context);
            if(geometry==null)return;
            QueueCapture(context, target, geometry, route, movement, "-interrupted-" + movement.Owner);
        }
        // Keep the existing call boundary; capture successful plans as paired evidence.
        // Raw arrays are detached only here, never in the per-frame Read.
        public static void CaptureFailure(NavigationContext context,NavigationTarget target,FieldGeometrySnapshot geometry,RoutePlan route)
        {
            if(context==null || target==null || route==null || geometry==null)return;
            if(_captureGeneration!=context.Generation){_captureGeneration=context.Generation;_captures.Clear();}
            string key=target.Id+":"+geometry.Fingerprint+":"+route.Failure;
            if(_captures.Count>=16 || !_captures.Add(key))return;
            QueueCapture(context, target, geometry, route, null, "-" + target.Id.NativeId);
        }

        private static void QueueCapture(NavigationContext context, NavigationTarget target, FieldGeometrySnapshot geometry,
            RoutePlan route, FieldMovementCapture movement, string suffix)
        {
            // Bound detached snapshots even if writing is slow or routes are requested in quick succession.
            if (Interlocked.Increment(ref _queuedCaptures) > 4)
            {
                Interlocked.Decrement(ref _queuedCaptures);
                return;
            }
            bool scheduled = false;
            try
            {
                var raw = CaptureRaw(context);
                if (raw == null) return;
                var origin = context.Copy();
                var selected = FieldTargetCatalog.Copy(target);
                string name = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + suffix + ".json";
                scheduled = ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        string payload = FieldRouteDiagnostics.SerializeWithMovementCapture(origin, selected, geometry, route, raw, movement);
                        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FF9Access/field-routes");
                        Directory.CreateDirectory(directory);
                        string path = Path.Combine(directory, name);
                        File.WriteAllText(path, payload);
                        if (movement == null) Memoria.Prime.Log.Message("[FF9Access] Field route snapshot ({0}): {1}", route.Success ? "success" : "failure", path);
                        else Memoria.Prime.Log.Message("[FF9Access] Field interruption snapshot ({0}): {1}", movement.Cause, path);
                    }
                    catch (Exception exception) { Memoria.Prime.Log.Message("[FF9Access] Field route snapshot unavailable: {0}", exception.Message); }
                    finally { Interlocked.Decrement(ref _queuedCaptures); }
                });
            }
            catch (Exception exception) { Memoria.Prime.Log.Message("[FF9Access] Field route snapshot unavailable: {0}", exception.Message); }
            finally { if (!scheduled) Interlocked.Decrement(ref _queuedCaptures); }
        }
        public static FieldGeometrySnapshot Read(NavigationContext context)
        {
            // Scene teardown or malformed native arrays produce no planner input;
            // never return a partial mesh after skipping malformed triangles.
            try
            {
                var snapshot = ReadAvailable(context);
                if (snapshot == null) _geometry = null;
                return snapshot;
            }
            catch (Exception)
            {
                _geometry = null;
                return null;
            }
        }
        private static FieldGeometrySnapshot ReadAvailable(NavigationContext context)
        {
            EventEngine engine;Actor actor;var controller=Controller(context,out engine,out actor);if(controller==null)return null;
            var field=controller.fieldMap;var bgi=field.bgi;var runtimeTriangles=field.walkMesh.tris;
            if(runtimeTriangles==null || bgi.triList==null || bgi.floorList==null || bgi.vertexList==null || bgi.edgeList==null || bgi.normalList==null)return null;
            int map=FF9StateSystem.Common.FF9.fldMapNo;
            byte blocked=(byte)(actor.sid==engine.GetControlUID()?0x80:0x40);
            float radius = controller.radius;
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0) return null;

            var result = new FieldGeometrySnapshot
            {
                Generation = context.Generation, Radius = radius, ActiveTriangle = controller.activeTri,
                AttributeMask = bgi.attributeMask, BlockedAttributes = blocked
            };
            // Player support changes while walking even when the geometry is stable.
            // Return a fresh view, with the previously detached triangles and route identity.
            if (_geometry != null && _geometryNativeMap == map && _geometryMap == context.LogicalMapKey && _geometry.Generation == result.Generation &&
                _geometry.Radius == result.Radius && _geometry.AttributeMask == result.AttributeMask &&
                _geometry.BlockedAttributes == result.BlockedAttributes && MatchesTriangles(bgi, runtimeTriangles, _geometry.Triangles))
            {
                result.Triangles = _geometry.Triangles;
                result.Fingerprint = _geometry.Fingerprint;
                return result;
            }

            var triangles=new FieldTriangle[runtimeTriangles.Count];
            for(int index=0;index<runtimeTriangles.Count;index++) {
                var runtime=runtimeTriangles[index];
                if(runtime==null || runtime.triIdx!=index || index>=bgi.triList.Count || runtime.originalVertices==null || runtime.originalVertices.Length!=3 || !Three(runtime.neighborIdx))return null;
                var native=bgi.triList[index];
                if(native==null || native.triIdx!=index || native.floorNdx<0 || native.floorNdx>=bgi.floorList.Count || runtime.floorIdx!=native.floorNdx ||
                    !Three(native.vertexNdx)||!Three(native.neighborNdx)||!Three(native.edgeNdx))return null;
                var floor=bgi.floorList[runtime.floorIdx];if(floor==null)return null;
                var originals=new NavigationPoint[3];var crossing=new NavigationPoint[3];var clones=new int[3];
                var offset=Point(bgi.curPos.ToVector3()+floor.curPos.ToVector3());
                for(int i=0;i<3;i++) {
                    int vertex=native.vertexNdx[i],edge=native.edgeNdx[i];
                    if(vertex<0||vertex>=bgi.vertexList.Count||edge<0||edge>=bgi.edgeList.Count ||
                        !Neighbor(runtime.neighborIdx[i],runtimeTriangles.Count)||!Neighbor(native.neighborNdx[i],runtimeTriangles.Count))return null;
                    originals[i]=Point(runtime.originalVertices[i]);
                    var raw=bgi.vertexList[vertex].ToVector3();crossing[i]=new NavigationPoint(raw.x+offset.X,raw.y+offset.Y,raw.z+offset.Z);
                    // pointInPoly stores sums as Int16; crossing uses float sums.
                    // Overflow would make those two native contracts disagree.
                    if(crossing[i].X<short.MinValue||crossing[i].X>short.MaxValue||crossing[i].Y<short.MinValue||crossing[i].Y>short.MaxValue||crossing[i].Z<short.MinValue||crossing[i].Z>short.MaxValue)return null;
                    clones[i]=bgi.edgeList[edge].edgeClone;if(clones[i]<-1||clones[i]>2)return null;
                }
                NavigationPoint normal=new NavigationPoint();int inverseY=0;
                if(native.normalNdx>=0) {
                    if(native.normalNdx>=bgi.normalList.Count)return null;var value=bgi.normalList[native.normalNdx];
                    if(value==null||!Three(value.coord))return null;normal=new NavigationPoint(value.coord[0],value.coord[1],value.coord[2]);inverseY=value.oneOverY;
                }
                var data=new FieldNativeTriangleData{OriginalVertices=originals,CrossingVertices=crossing,
                    FloorDelta=Point(floor.curPos.ToVector3()-floor.orgPos.ToVector3()),RuntimeNeighbors=Normalize(runtime.neighborIdx),NativeNeighbors=Normalize(Copy(native.neighborNdx)),EdgeClones=Normalize(clones),
                    RuntimeFlags=runtime.triFlags,NativeFlags=native.triFlags,NormalIndex=native.normalNdx,Normal=normal,NormalInverseY=inverseY,
                    Plane=native.d,ThetaX=native.thetaX,ThetaZ=native.thetaZ,Projection=FieldNativeGeometryContract.Projection(native.normalNdx,normal)};
                triangles[index]=FieldNativeGeometryContract.CreateTriangle(index,runtime.floorIdx,map,data);if(triangles[index]==null)return null;
            }
            result.Triangles = triangles;
            // Projection camera is presentation state, not movement geometry.
            result.Fingerprint = FieldNativeGeometryContract.Fingerprint(context.LogicalMapKey, result);
            _geometryMap = context.LogicalMapKey;
            _geometryNativeMap = map;
            _geometry = result;
            return result;
        }

        private static bool MatchesTriangles(BGI_DEF bgi, List<WalkMeshTriangle> runtimeTriangles, FieldTriangle[] captured)
        {
            if (runtimeTriangles.Count != captured.Length || bgi.triList.Count < captured.Length)
                return false;
            Vector3 mapOffset = bgi.curPos.ToVector3();
            for (int i = 0; i < captured.Length; i++)
            {
                var runtime = runtimeTriangles[i];
                var native = bgi.triList[i];
                var old = captured[i].Native;
                if (runtime == null || native == null || runtime.triIdx != i || native.triIdx != i ||
                    runtime.floorIdx != captured[i].Floor || native.floorNdx != runtime.floorIdx ||
                    runtime.originalVertices == null || runtime.originalVertices.Length != 3 || !Three(runtime.neighborIdx) ||
                    !Three(native.vertexNdx) || !Three(native.neighborNdx) || !Three(native.edgeNdx))
                    return false;

                // The low 0x80 bit is native traversal scratch, also excluded by the fingerprint.
                if (((runtime.triFlags ^ old.RuntimeFlags) & ~0x80) != 0 || ((native.triFlags ^ old.NativeFlags) & ~0x80) != 0 ||
                    native.normalNdx != old.NormalIndex || native.d != old.Plane || native.thetaX != old.ThetaX || native.thetaZ != old.ThetaZ)
                    return false;
                if (native.normalNdx >= 0)
                {
                    var normal = bgi.normalList[native.normalNdx];
                    if (normal == null || !Three(normal.coord) || normal.oneOverY != old.NormalInverseY ||
                        normal.coord[0] != old.Normal.X || normal.coord[1] != old.Normal.Y || normal.coord[2] != old.Normal.Z)
                        return false;
                }

                var floor = bgi.floorList[runtime.floorIdx];
                if (floor == null) return false;
                Vector3 floorOffset = floor.curPos.ToVector3();
                if (!SamePoint(old.FloorDelta, floorOffset - floor.orgPos.ToVector3())) return false;
                Vector3 offset = mapOffset + floorOffset;
                for (int edge = 0; edge < 3; edge++)
                {
                    int normalized = (edge + 2) % 3;
                    if (runtime.neighborIdx[edge] != old.RuntimeNeighbors[normalized] ||
                        native.neighborNdx[edge] != old.NativeNeighbors[normalized] ||
                        bgi.edgeList[native.edgeNdx[edge]].edgeClone != old.EdgeClones[normalized] ||
                        !SamePoint(old.OriginalVertices[edge], runtime.originalVertices[edge]) ||
                        !SamePoint(old.CrossingVertices[edge], bgi.vertexList[native.vertexNdx[edge]].ToVector3() + offset))
                        return false;
                }
            }
            return true;
        }

        private static bool SamePoint(NavigationPoint captured, Vector3 current)
        {
            return captured.X == current.x && captured.Y == current.y && captured.Z == current.z;
        }

        private static FieldMapActorController Controller(NavigationContext context,out EventEngine engine,out Actor actor)
        {
            engine=UnityEngine.Object.FindObjectOfType<EventEngine>();actor=engine==null?null:engine.GetControlChar() as Actor;
            var controller=actor==null?null:actor.fieldMapActorController;
            if(context==null||controller==null||!controller.isPlayer||actor.uid!=context.PlayerIdentity)return null;
            var field=controller.fieldMap;return field==null||field.bgi==null||field.walkMesh==null?null:controller;
        }
        private static FieldNativeCapture CaptureRaw(NavigationContext context)
        {
            EventEngine engine;Actor actor;var controller=Controller(context,out engine,out actor);if(controller==null)return null;
            var field=controller.fieldMap;var bgi=field.bgi;
            var raw=new FieldNativeCapture{Map=FF9StateSystem.Common.FF9.fldMapNo,Camera=field.camIdx,Frame=Time.frameCount,
                CurrentPosition=Point(controller.curPos),LastPosition=Point(controller.lastPos),ActiveTriangle=controller.activeTri,LastTriangle=controller.lastTri,ActiveFloor=controller.activeFloor,LastFloor=controller.lastFloor,
                ControlUid=engine.GetControlUID(),ActorUid=actor.uid,ActorSid=actor.sid,AttributeMask=bgi.attributeMask,BlockedAttributes=actor.sid==engine.GetControlUID()?0x80:0x40,
                BgiOriginalPosition=Point(bgi.orgPos.ToVector3()),BgiCurrentPosition=Point(bgi.curPos.ToVector3()),
                Floors=new FieldNativeCaptureFloor[bgi.floorList.Count],Triangles=new FieldNativeCaptureTriangle[field.walkMesh.tris.Count]};
            for(int i=0;i<raw.Floors.Length;i++) {
                var floor=bgi.floorList[i];raw.Floors[i]=new FieldNativeCaptureFloor{Index=i,Flags=floor.floorFlags,OriginalPosition=Point(floor.orgPos.ToVector3()),CurrentPosition=Point(floor.curPos.ToVector3())};
            }
            for(int i=0;i<raw.Triangles.Length;i++) {
                var runtime=field.walkMesh.tris[i];var native=bgi.triList[runtime.triIdx];
                var triangle=new FieldNativeCaptureTriangle{Index=runtime.triIdx,RuntimeFloor=runtime.floorIdx,NativeFloor=native.floorNdx,RuntimeFlags=runtime.triFlags,NativeFlags=native.triFlags,
                    OriginalVertices=new NavigationPoint[3],RawVertices=new NavigationPoint[3],RuntimeNeighbors=(int[])runtime.neighborIdx.Clone(),NativeNeighbors=Copy(native.neighborNdx),VertexIndices=Copy(native.vertexNdx),EdgeIndices=Copy(native.edgeNdx),EdgeClones=new int[3],EdgeFlags=new int[3],
                    NormalIndex=native.normalNdx,Plane=native.d,ThetaX=native.thetaX,ThetaZ=native.thetaZ,NativeCenter=Point(native.center.ToVector3())};
                if(native.normalNdx>=0){var normal=bgi.normalList[native.normalNdx];triangle.Normal=new NavigationPoint(normal.coord[0],normal.coord[1],normal.coord[2]);triangle.NormalInverseY=normal.oneOverY;}
                for(int j=0;j<3;j++) {
                    triangle.OriginalVertices[j]=Point(runtime.originalVertices[j]);triangle.RawVertices[j]=Point(bgi.vertexList[native.vertexNdx[j]].ToVector3());
                    var edge=bgi.edgeList[native.edgeNdx[j]];triangle.EdgeClones[j]=edge.edgeClone;triangle.EdgeFlags[j]=edge.edgeFlags;
                }
                raw.Triangles[i]=triangle;
            }return raw;
        }
        private static NavigationPoint Point(Vector3 value){return new NavigationPoint(value.x,value.y,value.z);}
        private static bool Three(Array source){return source!=null&&source.Length==3;}
        private static bool Neighbor(int value,int count){return value>=-1&&value<count;}
        private static int[] Copy(short[] source){var result=new int[source.Length];for(int i=0;i<source.Length;i++)result[i]=source[i];return result;}
        private static int[] Normalize(int[] source){return new[]{source[1],source[2],source[0]};}
    }
}
