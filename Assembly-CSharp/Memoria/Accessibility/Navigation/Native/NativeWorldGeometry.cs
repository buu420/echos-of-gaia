using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using UnityEngine;
using Memoria.Prime;
namespace Memoria.Accessibility.Navigation
{
    public static class NativeWorldGeometry
    {
        // Native references are inspected only on the Unity thread. The worker
        // receives detached scalar surfaces and the opaque revision number.
        private sealed class MeshIdentity
        {
            private readonly WMMesh _mesh;private readonly bool _relative;private readonly Matrix4x4 _matrix;
            internal MeshIdentity(WMMesh mesh,Transform block)
            {
                _mesh=mesh;_relative=!System.Object.ReferenceEquals(mesh.Transform,block);
                if(_relative && block!=null && mesh.Transform!=null)_matrix=block.worldToLocalMatrix*mesh.Transform.localToWorldMatrix;
            }
            internal bool Matches(WMMesh mesh,Transform block)
            {
                if(!System.Object.ReferenceEquals(_mesh.Transform,mesh.Transform) || !System.Object.ReferenceEquals(_mesh.Vertices,mesh.Vertices) || !System.Object.ReferenceEquals(_mesh.Triangles,mesh.Triangles) || !System.Object.ReferenceEquals(_mesh.Tangents,mesh.Tangents) || !System.Object.ReferenceEquals(_mesh.TriangleNormals,mesh.TriangleNormals))return false;
                // Ordinary world wrapping translates the block and its native
                // mesh together. Only a separate mesh transform needs checking.
                if(_relative) {
                    if(block==null || mesh.Transform==null)return false;
                    var matrix=block.worldToLocalMatrix*mesh.Transform.localToWorldMatrix;
                    for(int i=0;i<16;i++)if(_matrix[i]!=matrix[i])return false;
                }
                return true;
            }
        }
        private sealed class BlockIdentity
        {
            private readonly WMBlock _block;private readonly int _form;private readonly bool _ready;
            private readonly Transform _transform;private readonly List<WMMesh> _list;private readonly MeshIdentity[] _meshes;
            internal readonly bool Complete;
            internal BlockIdentity(WMBlock block)
            {
                _block=block;if(block==null)return;
                _form=block.Form;_ready=block.IsReady;_transform=block.transform;_list=block.ActiveWalkMeshes;
                Complete=_ready && _list!=null;if(_list==null)return;
                _meshes=new MeshIdentity[_list.Count];for(int i=0;i<_meshes.Length;i++)_meshes[i]=new MeshIdentity(_list[i],_transform);
            }
            internal bool Matches(WMBlock block)
            {
                if(!System.Object.ReferenceEquals(_block,block))return false;if(block==null)return true;
                var list=block.ActiveWalkMeshes;
                if(_form!=block.Form || _ready!=block.IsReady || !System.Object.ReferenceEquals(_list,list))return false;
                if(list==null)return true;if(list.Count!=_meshes.Length)return false;
                for(int i=0;i<_meshes.Length;i++)if(!_meshes[i].Matches(list[i],_transform))return false;return true;
            }
        }
        private sealed class WorldIdentity
        {
            private readonly WMWorld _world;private readonly WMBlock[,] _blocks;private readonly int _disc;private readonly bool _ready;
            private readonly BlockIdentity[] _blockIdentities=new BlockIdentity[480];
            internal readonly bool Complete;
            internal WorldIdentity(WMWorld world)
            {
                _world=world;_blocks=world.InitialBlocks;_disc=ff9.w_frameDisc;_ready=ff9.w_blockReady;Complete=_ready;
                for(int i=0;i<480;i++){_blockIdentities[i]=new BlockIdentity(_blocks[i%24,i/24]);Complete &= _blockIdentities[i].Complete;}
            }
            internal bool Matches(WMWorld world)
            {
                if(!System.Object.ReferenceEquals(_world,world) || !System.Object.ReferenceEquals(_blocks,world.InitialBlocks) || _disc!=ff9.w_frameDisc || _ready!=ff9.w_blockReady)return false;
                for(int i=0;i<480;i++)if(!_blockIdentities[i].Matches(_blocks[i%24,i/24]))return false;return true;
            }
        }
        private struct FaceKey : IEquatable<FaceKey>
        {
            private readonly int _block,_mesh,_triangle;
            internal FaceKey(int block,int mesh,int triangle){_block=block;_mesh=mesh;_triangle=triangle;}
            public bool Equals(FaceKey other){return _block==other._block && _mesh==other._mesh && _triangle==other._triangle;}
            public override bool Equals(object value){return value is FaceKey && Equals((FaceKey)value);}
            public override int GetHashCode(){return (_block*397^_mesh)*397^_triangle;}
        }
        private static readonly WorldGeometryPreparation _preparation=new WorldGeometryPreparation();
        private static readonly WorldCaptureBoundary _captureBoundary=new WorldCaptureBoundary(_preparation,LoadingFailed);
        private static readonly WorldCaptureBudget _budget=new WorldCaptureBudget();
        private static readonly List<WorldSurface> _surfaces=new List<WorldSurface>();
        private static readonly Dictionary<FaceKey,int> _indices=new Dictionary<FaceKey,int>();
        private static WorldIdentity _identity;private static long _revision,_copyTicks;
        private static int _block,_capturedCount;private static bool _valid,_submitted,_readyLogged,_failureLogged;
        private static WorldSurface[] _captured;private static Stopwatch _captureTimer;private static long _captureMilliseconds;
        private static WMWorld _loadingWorld;private static bool _streaming,_loadingComplete,_reusing;
        private static readonly WorldSurface[][] _loadBatches=new WorldSurface[480][];
        private static readonly BlockIdentity[] _loadIdentities=new BlockIdentity[480];
        private static WorldGeometryContent _retained;private static int _retainedDisc;
        public static bool IsPreparing{get{return _identity!=null && _identity.Complete && _valid && !_readyLogged && _preparation.Failure(_revision)==null;}}
        private static void RetainCompleted()
        {
            var ready=_preparation.RetainReady();
            if(ready!=null && (_retained==null || !System.Object.ReferenceEquals(ready,_retained.Geometry))){_retained=new WorldGeometryContent(ready);_retainedDisc=ff9.w_frameDisc;}
        }
        public static void Detach()
        {
            try{_captureBoundary.TryCapture(RetainCompleted);}catch(Exception exception){_captureBoundary.Fail(exception);}
            _preparation.Invalidate();_identity=null;_loadingWorld=null;_streaming=false;
            _surfaces.Clear();_captured=null;Array.Clear(_loadBatches,0,480);Array.Clear(_loadIdentities,0,480);
        }
        public static void BeginLoad(WMWorld world)
        {
            try{_captureBoundary.Reset();_captureBoundary.TryCapture(()=>BeginLoadCore(world));}catch(Exception exception){_captureBoundary.Fail(exception);}
        }
        private static void BeginLoadCore(WMWorld world)
        {
            if(world==null || world.InitialBlocks==null || world.InitialBlocks.GetLength(0)!=24 || world.InitialBlocks.GetLength(1)!=20)return;
            RetainCompleted();_preparation.Invalidate();_identity=null;_loadingWorld=world;_revision++;
            _streaming=true;_loadingComplete=false;_reusing=_retained!=null && _retainedDisc==ff9.w_frameDisc;
            _valid=true;_submitted=false;_readyLogged=false;_failureLogged=false;_block=0;_capturedCount=0;_copyTicks=0;
            _surfaces.Clear();_captured=null;_indices.Clear();Array.Clear(_loadBatches,0,480);Array.Clear(_loadIdentities,0,480);
            _captureTimer=Stopwatch.StartNew();_captureMilliseconds=0;FlushLoading();
            NavigationRuntime.Trace("World loading preparation begun; revision="+_revision+"; retained="+_reusing);
        }
        // A bounded set of native loader checkpoints survives an interrupted
        // return from battle. Never called from the normal movement loop.
        public static void TraceLoadCheckpoint(WMWorld world,string stage,int completedRows)
        {
            try {
                if(_loadingComplete || _captureTimer==null || !System.Object.ReferenceEquals(world,_loadingWorld))return;
                NavigationRuntime.Trace("World native load checkpoint; revision="+_revision+"; stage="+stage+"; rows="+completedRows+"; elapsed_ms="+_captureTimer.ElapsedMilliseconds);
            }catch(Exception){ }
        }
        public static void CaptureLoadedBlock(WMWorld world,WMBlock block)
        {
            try{_captureBoundary.TryCapture(()=>CaptureLoadedBlockCore(world,block));}catch(Exception exception){_captureBoundary.Fail(exception);}
        }
        private static void CaptureLoadedBlockCore(WMWorld world,WMBlock block)
        {
            if(!_streaming || !System.Object.ReferenceEquals(world,_loadingWorld) || block==null || !_valid)return;
            int id=block.InitialY*24+block.InitialX;
            if(id<0 || id>=480 || !System.Object.ReferenceEquals(world.InitialBlocks[id%24,id/24],block) || _loadIdentities[id]!=null){_valid=false;return;}
            var identity=new BlockIdentity(block);var timer=Stopwatch.StartNew();
            var batch=CaptureBlock(block,id,_capturedCount);timer.Stop();_copyTicks+=timer.ElapsedTicks;
            if(batch==null || !identity.Complete || !identity.Matches(block)){_valid=false;return;}
            _loadIdentities[id]=identity;_loadBatches[id]=batch;_capturedCount+=batch.Length;
            if(_reusing && !_retained.MatchesBlock(id,batch))_reusing=false;
            FlushLoading();
        }
        public static void CompleteLoad(WMWorld world)
        {
            try{_captureBoundary.TryCapture(()=>CompleteLoadCore(world));}catch(Exception exception){_captureBoundary.Fail(exception);}
        }
        private static void CompleteLoadCore(WMWorld world)
        {
            if(!_streaming || !System.Object.ReferenceEquals(world,_loadingWorld))return;
            var identity=new WorldIdentity(world);_valid &= identity.Complete;
            for(int i=0;i<480;i++)if(_loadIdentities[i]==null || !_loadIdentities[i].Matches(world.InitialBlocks[i%24,i/24]))_valid=false;
            _identity=identity;_block=480;_loadingComplete=true;_captureTimer.Stop();_captureMilliseconds=_captureTimer.ElapsedMilliseconds;
            _loadingWorld=null;Array.Clear(_loadIdentities,0,480);
            if(!_valid)_preparation.Invalidate();else FlushLoading();
            NavigationRuntime.Trace("World native loading complete; revision="+_revision+"; valid="+_valid+"; reused="+_reusing+"; capture_ms="+_captureMilliseconds+"; copy_ms="+(_copyTicks*1000/Stopwatch.Frequency));
        }
        private static void LoadingFailed(Exception exception)
        {
            _valid=false;_identity=null;_loadingWorld=null;_streaming=false;
            _surfaces.Clear();_captured=null;_indices.Clear();Array.Clear(_loadBatches,0,480);Array.Clear(_loadIdentities,0,480);
            _failureLogged=true;
            NavigationRuntime.Trace("World preparation unavailable; revision="+_revision+"; native loading continues; error="+exception.Message);
            Log.Message("[FF9Access] World preparation unavailable; native loading continues: {0}",exception);
        }
        private static void FlushLoading()
        {
            if(!_streaming || !_valid)return;
            if(_reusing){
                if(!_loadingComplete)return;
                if(!_preparation.TryBindReady(_revision,_retained.Geometry))return;
                _indices.Clear();foreach(var surface in _retained.Geometry.Surfaces)_indices[new FaceKey(surface.BlockId,surface.MeshIndex,surface.TriangleIndex)]=surface.Id;
                Array.Clear(_loadBatches,0,480);_submitted=true;_streaming=false;return;
            }
            if(!_submitted){if(!_preparation.TryBegin(_revision))return;_submitted=true;}
            for(int i=0;i<480;i++)if(_loadBatches[i]!=null){
                var batch=_loadBatches[i];if(!_preparation.TryAppendOwned(_revision,batch))return;
                foreach(var surface in batch)_indices[new FaceKey(surface.BlockId,surface.MeshIndex,surface.TriangleIndex)]=surface.Id;
                _loadBatches[i]=null;
            }
            if(_loadingComplete){_preparation.Complete(_revision);_streaming=false;}
        }
        private static WorldSurface[] CaptureBlock(WMBlock block,int blockId,int firstId)
        {
            if(block==null || !block.IsReady || block.ActiveWalkMeshes==null)return null;
            var surfaces=new List<WorldSurface>();var meshes=block.ActiveWalkMeshes;var blockTransform=block.transform;
            for(int meshIndex=0;meshIndex<meshes.Count;meshIndex++){
                var mesh=meshes[meshIndex];
                if(mesh.Transform==null || mesh.Vertices==null || mesh.Triangles==null || mesh.Tangents==null || mesh.TriangleNormals==null || mesh.Triangles.Length%3!=0)return null;
                var transform=System.Object.ReferenceEquals(mesh.Transform,blockTransform)?Matrix4x4.identity:blockTransform.worldToLocalMatrix*mesh.Transform.localToWorldMatrix;
                for(int triangle=0;triangle<mesh.Triangles.Length/3;triangle++){
                    int offset=triangle*3,index=mesh.Triangles[offset];
                    if(index<0 || index>=mesh.Tangents.Length || triangle>=mesh.TriangleNormals.Length)return null;
                    var points=new NavigationPoint[3];
                    for(int vertex=0;vertex<3;vertex++){
                        int nativeIndex=mesh.Triangles[offset+vertex];if(nativeIndex<0 || nativeIndex>=mesh.Vertices.Length)return null;
                        var point=transform.MultiplyPoint3x4(mesh.Vertices[nativeIndex]);
                        points[vertex]=new NavigationPoint(point.x+block.InitialX*64,point.y,point.z-block.InitialY*64);if(!points[vertex].IsFinite)return null;
                    }
                    surfaces.Add(new WorldSurface{Id=firstId+surfaces.Count,BlockId=blockId,MeshIndex=meshIndex,TriangleIndex=triangle,TerrainId=(int)mesh.Tangents[index].x,NormalY=mesh.TriangleNormals[triangle].y,Vertices=points});
                }
            }
            return surfaces.ToArray();
        }
        private static bool EnsureIdentity(WMWorld world)
        {
            if(_captureBoundary.Unavailable)return false;
            if(world==null || world.InitialBlocks==null || world.InitialBlocks.GetLength(0)!=24 || world.InitialBlocks.GetLength(1)!=20)return false;
            if(System.Object.ReferenceEquals(world,_loadingWorld))return false;
            if(_identity==null || !_identity.Matches(world)) {
                RetainCompleted();_preparation.Invalidate();_identity=new WorldIdentity(world);_revision++;
                _streaming=false;_loadingWorld=null;Array.Clear(_loadBatches,0,480);Array.Clear(_loadIdentities,0,480);
                _surfaces.Clear();_indices.Clear();_captured=null;_block=0;_capturedCount=0;_copyTicks=0;
                _valid=_identity.Complete;_submitted=false;_readyLogged=false;_failureLogged=false;
                _captureTimer=Stopwatch.StartNew();_captureMilliseconds=0;
            }
            return _identity.Complete;
        }
        private static string Fingerprint(TransportRules rules)
        {
            // Context generation, the live cache and the changing terrain ID
            // belong to request state. Traversal constraints invalidate routes.
            return _revision+":"+ff9.w_frameDisc+":"+rules.ControlMode+":"+rules.ControlType+":"+rules.LimitHigh+":"+rules.LimitLow+":"+rules.Flying+":"+rules.PreventGroundWaterCrossing+":"+rules.Step.ToString("R",CultureInfo.InvariantCulture);
        }
        public static string CurrentFingerprint()
        {
            var world=UnityEngine.Object.FindObjectOfType<WMWorld>();var rules=ReadRules();
            return rules==null || !EnsureIdentity(world)?null:Fingerprint(rules);
        }
        public static void Warm(NavigationContext context)
        {
            if(context==null || !context.Foreground)return;
            var world=UnityEngine.Object.FindObjectOfType<WMWorld>();if(EnsureIdentity(world))AdvanceCapture(world);
        }
        public static WorldGeometrySnapshot Read(NavigationContext context,NavigationTarget target)
        {
            var world=UnityEngine.Object.FindObjectOfType<WMWorld>();var rules=ReadRules();
            if(context==null || rules==null || !EnsureIdentity(world))return null;
            AdvanceCapture(world);string fingerprint=Fingerprint(rules);WorldGeometrySnapshot geometry;
            return _preparation.TryRead(_revision,context.Generation,fingerprint,rules,ReadCache(),out geometry)?geometry:new WorldGeometrySnapshot(context.Generation,fingerprint,new WorldSurface[0],rules,false);
        }
        private static void AdvanceCapture(WMWorld world)
        {
            if(_streaming)FlushLoading();
            if(_block<480 && _valid) {
                int end=_block+_budget.Take(Time.frameCount,24,480-_block);
                if(end!=_block) {
                    string before=CaptureNativeState();var timer=Stopwatch.StartNew();
                    for(;_block<end;_block++) {
                        int x=_block%24,z=_block/24;var block=world.InitialBlocks[x,z];
                        if(block==null || !block.IsReady || block.ActiveWalkMeshes==null){_valid=false;continue;}
                        var local=block.transform.worldToLocalMatrix;var meshes=block.ActiveWalkMeshes;
                        for(int meshIndex=0;meshIndex<meshes.Count;meshIndex++) {
                            var mesh=meshes[meshIndex];
                            if(mesh.Transform==null || mesh.Vertices==null || mesh.Triangles==null || mesh.Tangents==null || mesh.TriangleNormals==null || mesh.Triangles.Length%3!=0){_valid=false;continue;}
                            Matrix4x4 transform=local*mesh.Transform.localToWorldMatrix;
                            for(int triangle=0;triangle<mesh.Triangles.Length/3;triangle++) {
                                int offset=triangle*3,index=mesh.Triangles[offset];
                                if(index<0 || index>=mesh.Tangents.Length || triangle>=mesh.TriangleNormals.Length){_valid=false;continue;}
                                var points=new NavigationPoint[3];bool valid=true;
                                for(int vertex=0;vertex<3;vertex++) {
                                    int nativeIndex=mesh.Triangles[offset+vertex];if(nativeIndex<0 || nativeIndex>=mesh.Vertices.Length){valid=false;break;}
                                    Vector3 point=transform.MultiplyPoint3x4(mesh.Vertices[nativeIndex]);
                                    points[vertex]=new NavigationPoint(point.x+x*64,point.y,point.z-z*64);valid &= points[vertex].IsFinite;
                                }
                                if(!valid){_valid=false;continue;}
                                int id=_surfaces.Count;_indices[new FaceKey(_block,meshIndex,triangle)]=id;
                                _surfaces.Add(new WorldSurface{Id=id,BlockId=_block,MeshIndex=meshIndex,TriangleIndex=triangle,TerrainId=(int)mesh.Tangents[index].x,NormalY=mesh.TriangleNormals[triangle].y,Vertices=points});
                            }
                        }
                    }
                    timer.Stop();_copyTicks+=timer.ElapsedTicks;
                    if(before!=CaptureNativeState()){_valid=false;Log.Message("[FF9Access] World snapshot native-state sentinel changed; automatic route rejected");}
                }
                if(_block==480) {
                    _captureTimer.Stop();_captureMilliseconds=_captureTimer.ElapsedMilliseconds;_capturedCount=_surfaces.Count;
                    if(_valid)_captured=_surfaces.ToArray();_surfaces.Clear();
                }
            }
            if(!_submitted && _captured!=null && _preparation.TryStartOwned(_revision,_captured)) {
                _submitted=true;_captured=null;
                NavigationRuntime.Trace("World topology preparation queued; revision="+_revision+"; triangles="+_capturedCount+"; capture_ms="+_captureMilliseconds+"; copy_ms="+(_copyTicks*1000/Stopwatch.Frequency));
            }
            if(!_readyLogged && _preparation.IsReady(_revision)) {
                _readyLogged=true;RetainCompleted();NavigationRuntime.Trace("World topology ready; revision="+_revision+"; triangles="+_capturedCount+"; build_ms="+_preparation.ElapsedMilliseconds(_revision));
            }
            string failure=_valid?_preparation.Failure(_revision):"Native world geometry incomplete";
            if(failure!=null && !_failureLogged){_failureLogged=true;Log.Message("[FF9Access] World preparation unavailable: {0}",failure);NavigationRuntime.Trace("World preparation unavailable; revision="+_revision+"; error="+failure);}
        }
        private static TransportRules ReadRules()
        {
            var actor=ff9.w_moveActorPtr;int mode=ff9.w_moveCHRControl_No;
            if(actor==null || actor.originalActor==null || ff9.w_moveCHRControl==null || mode<0 || mode>=ff9.w_moveCHRControl.Length || ff9.w_moveCHRStatus==null || actor.originalActor.index<0 || actor.originalActor.index>=ff9.w_moveCHRStatus.Length)return null;
            var native=ff9.w_moveCHRControl[mode];var status=ff9.w_moveCHRStatus[actor.originalActor.index];
            if(native==null || native.limit==null || native.limit.Length!=2 || status==null)return null;
            return new TransportRules{ControlMode=mode,ControlType=native.type,Flying=native.flg_fly,PreventGroundWaterCrossing=native.flg_gake!=0,LimitHigh=native.limit[0],LimitLow=native.limit[1],Step=Math.Max(1f/256,Math.Abs(ff9.S(native.speed_move))),CurrentTerrainId=status.id};
        }
        private static int[] ReadCache()
        {
            var result=new int[10];for(int i=0;i<10;i++)result[i]=-1;
            var actor=ff9.w_moveActorPtr;if(actor==null || actor.originalActor==null || ff9.w_moveCHRCache==null)return result;
            int cacheIndex=ff9.w_moveCHRStatus[actor.originalActor.index].cache;
            if(cacheIndex<0 || cacheIndex>=ff9.w_moveCHRCache.Length)return result;
            var native=ff9.w_moveCHRCache[cacheIndex];if(native==null)return result;
            for(int i=0;i<10;i++) {int entry=(native.Number+i)%10;var block=native.Blocks[entry];int id;
                if(block!=null && _indices.TryGetValue(new FaceKey(block.InitialY*24+block.InitialX,native.WalkMeshIndices[entry],native.TriangleIndices[entry]),out id))result[i]=id;
            }return result;
        }
        public static string CaptureNativeState()
        {
            var text=new StringBuilder();text.Append(WMPhysics.IgnoreExceptions).Append(WMPhysics.CastRayFromSky).Append(WMPhysics.UseInfiniteRaycast);
            foreach(var cache in ff9.w_moveCHRCache) {if(cache==null)continue;text.Append('|').Append(cache.Number);for(int i=0;i<10;i++)text.Append(':').Append(cache.Blocks[i]==null?-1:cache.Blocks[i].Number).Append(',').Append(cache.TriangleIndices[i]).Append(',').Append(cache.WalkMeshIndices[i]).Append(',').Append(cache.OnObject[i]);}
            var actor=ff9.w_moveActorPtr;if(actor!=null)text.Append('|').Append(actor.pos[0]).Append(',').Append(actor.pos[1]).Append(',').Append(actor.pos[2]);
            var world=UnityEngine.Object.FindObjectOfType<WMWorld>();if(world!=null && world.InitialBlocks!=null)for(int z=0;z<20;z++)for(int x=0;x<24;x++){var b=world.InitialBlocks[x,z];text.Append('|').Append(b==null?-1:b.Form).Append(b!=null && b.IsReady?'r':'n');}
            return text.ToString();
        }
    }
}
