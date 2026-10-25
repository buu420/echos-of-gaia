using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using Memoria.Prime;
using Code = EventEngineUtils.BinaryScript.Entry.Function.Code;
namespace Memoria.Accessibility.Navigation
{
    public static partial class NativeFieldTargets
    {
        private static long _generation;
        private static readonly Dictionary<byte[],string> _hashes = new Dictionary<byte[],string>();
        private static readonly Dictionary<byte[],Dictionary<int,List<ScriptInstruction>>> _decoded=new Dictionary<byte[],Dictionary<int,List<ScriptInstruction>>>();
        private static readonly Dictionary<byte[],DoorBinding> _doors=new Dictionary<byte[],DoorBinding>();
        private sealed class RegionSurface {public string Geometry,Polygon;public NavigationPoint[] Region,Approaches,Boundaries,Presentation;}
        private static readonly Dictionary<Quad,RegionSurface> _surfaces=new Dictionary<Quad,RegionSurface>();
        private static BGSCENE_DEF _tileScene;private static int _tileFrame=-1;
        private static readonly Dictionary<int,DoorTileObservation> _tiles=new Dictionary<int,DoorTileObservation>();
        private static int _doorPlayer=-1;private static byte[] _doorPlayerScript;
        private static readonly HashSet<string> _cueLogs=new HashSet<string>();
        private static readonly HashSet<int> _seenExits=new HashSet<int>();
        private static string _walkmeshHash;
        private sealed class DoorBinding {public bool Walk,Plain;public int[] Animations;}
        private static readonly FieldDoorOpcodes _opcodes=new FieldDoorOpcodes {
            MapJump=(int)EBin.event_code_binary.MAPJUMP,WorldJump=(int)EBin.event_code_binary.WMAPJUMP,JumpPosition=(int)EBin.event_code_binary.MJPOS,
            MoveQuad=(int)EBin.event_code_binary.MOVQ,Move=(int)EBin.event_code_binary.MOVE,
            Requests=new[]{(int)EBin.event_code_binary.REQ,(int)EBin.event_code_binary.REQSW,(int)EBin.event_code_binary.REQEW},
            Animations=new[]{(int)EBin.event_code_binary.BGAANIME,(int)EBin.event_code_binary.BGAACTIVE,(int)EBin.event_code_binary.BGARATE,(int)EBin.event_code_binary.BGAWAIT,(int)EBin.event_code_binary.BGAFLAG,(int)EBin.event_code_binary.BGARANGE,(int)EBin.event_code_binary.BGAVISIBLE,(int)EBin.event_code_binary.BGAFRAME},
            InteractionBarriers=new[]{(int)EBin.event_code_binary.MES,(int)EBin.event_code_binary.MESN,(int)EBin.event_code_binary.MESA,(int)EBin.event_code_binary.MESAN,(int)EBin.event_code_binary.MESB,(int)EBin.event_code_binary.WAITMES,(int)EBin.event_code_binary.CHOOSEPARAM},
            PlainPassive=new[]{(int)EBin.event_code_binary.UCOFF,(int)EBin.event_code_binary.UCON,(int)EBin.event_code_binary.MENUOFF,(int)EBin.event_code_binary.WAIT,(int)EBin.event_code_binary.BGIMASK,(int)EBin.event_code_binary.HINT,(int)EBin.event_code_binary.GETSCREEN,(int)EBin.event_code_binary.WIPERGB,(int)EBin.event_code_binary.FLDSND0},DisableControl=(int)EBin.event_code_binary.UCOFF,EnableControl=(int)EBin.event_code_binary.UCON,PreloadHint=(int)EBin.event_code_binary.HINT
        };
        public static IList<FieldTargetObservation> Read(NavigationContext context)
        {
            var result = new List<FieldTargetObservation>();
            if (context == null || context.LogicalMapKey==null || !context.LogicalMapKey.StartsWith("field:")) return result;
            // Battle/loading contexts retain the previous field ID. They are
            // not observations of that field and must not prepare its surfaces.
            if(context.GateReason==GateReason.Battle || context.GateReason==GateReason.MapTransition)return result;
            if (_generation != context.Generation) { _hashes.Clear();_decoded.Clear();_doors.Clear();_surfaces.Clear();_cueLogs.Clear();_seenExits.Clear();_walkmeshHash=null; _generation = context.Generation; }
            var engine = UnityEngine.Object.FindObjectOfType<EventEngine>();
            var field = UnityEngine.Object.FindObjectOfType<FieldMap>();
            if (engine == null || field == null) return result;
            var player=engine.GetControlChar() as Actor;
            int doorPlayer=player==null?-1:player.uid;byte[] doorScript=player==null?null:player.ebData;
            if(_doorPlayer!=doorPlayer || !System.Object.ReferenceEquals(_doorPlayerScript,doorScript)) {_doors.Clear();_doorPlayer=doorPlayer;_doorPlayerScript=doorScript;}
            FieldGeometrySnapshot currentGeometry=null;bool geometryRead=false;
            var openingCandle=ReadOpeningCandle(context,engine,field,player);
            if(openingCandle!=null)result.Add(openingCandle);
            foreach (Obj obj in ActiveObjects(engine)) {
                if (obj.uid == context.PlayerIdentity) continue;
                var actor = obj as Actor;
                if (actor != null) {
                    Vector3 position = actor.fieldMapActorController == null ? new Vector3(actor.pos[0], actor.pos[1], actor.pos[2]) : actor.fieldMapActorController.curPos;
                    bool rendered = (obj.flags & EventEngine.flagShow) != 0 && obj.isEnableRenderer && obj.go != null && obj.go.activeInHierarchy;
                    bool mesh = false; if (rendered) foreach (Renderer renderer in obj.go.GetComponentsInChildren<Renderer>()) mesh |= renderer.enabled;
                    bool onScreen=rendered && mesh && OnScreen(field,position);
                    bool icon=EIcon.NavigationPresentedUid==obj.uid && OnScreen(field,Vector(context.Position));
                    float collision=player==null?0:Math.Max(player.fieldMapActorController==null?0:player.fieldMapActorController.radius,4*player.collRad)+4*actor.collRad;
                    float talkRadius=player==null?0:4*(player.talkRad+actor.talkRad)+actor.speed+60;
                    var observation=new FieldTargetObservation { Id = new TargetIdentity(TargetKind.FieldActor, context.LogicalMapKey, obj.uid), NativePresentationIdentity=obj.go==null?0:obj.go.GetInstanceID(),Position = Point(position), Visible = onScreen,OutOfView=rendered && mesh && !onScreen,ShowsIcon=icon, IsPerson = engine.GetIP(obj.sid, EventEngine.tagTalk, obj.ebData) != engine.nil, HasInteraction = engine.IsActuallyTalkable(obj) || engine.GetIP(obj.sid, EventEngine.tagPush, obj.ebData) != engine.nil,ApproachCandidates=FieldActorApproaches.Create(context.Position,Point(position),collision,talkRadius) };
                    string modelName;if(FF9BattleDB.GEO.TryGetValue(actor.model,out modelName)) {
                        if(modelName.StartsWith("GEO_ACC_"))observation.IsPerson=false;
                        else if(modelName.StartsWith("GEO_NPC_") || modelName.StartsWith("GEO_MAIN_") || modelName.StartsWith("GEO_SUB_"))observation.IsPerson=true;
                    }
                    if(actor.ebData!=null)Annotate(context,engine,actor,observation);
                    if(context.LogicalMapKey==ForestNavigationEvidence.Map && obj.ebData!=null) {
                        var region=engine.GetObjUID(19) as Quad;
                        if(IsForestInteraction(context,actor,region)) {
                            observation.InteractionUid=region.uid;observation.InteractionRegion=new NavigationPoint[region.n];
                            for(int i=0;i<region.n;i++)observation.InteractionRegion[i]=Point(region.q[i].Vector3Val);
                            observation.HasInteraction=true;
                            observation.ShowsIcon|=observation.Visible && EIcon.NavigationPresentedUid==region.uid && OnScreen(field,Vector(context.Position));
                        }
                        if(actor.sid==11 && actor.model==241)observation.UseViewingPoint=ForestNavigationEvidence.NoninteractiveObject(context.LogicalMapKey,actor.sid,actor.model,Fingerprint(actor.ebData),observation.HasInteraction);
                        if((onScreen || observation.ShowsIcon) && (actor.sid==9 || actor.sid==11) && _cueLogs.Add("forest-binding:"+actor.uid+":"+observation.InteractionUid+":"+observation.UseViewingPoint))
                            Log.Message("[FF9Access] Forest target binding: uid={0}; model={1}; interactionUid={2}; viewingPoint={3}; actorScript={4}; regionScript={5}",actor.uid,actor.model,observation.InteractionUid,observation.UseViewingPoint,Fingerprint(actor.ebData),region==null || region.ebData==null?"unavailable":Fingerprint(region.ebData));
                    }
                    NativeFieldActorInteractions.Annotate(context,engine,player,actor,field,observation);
                    result.Add(observation);
                    continue;
                }
                var quad = obj as Quad; if (quad == null || quad.n < 3 || quad.q == null || quad.n>quad.q.Length || quad.ebData == null) continue;
                if(openingCandle!=null && quad.sid>=4 && quad.sid<=7)continue;
                // The proven Forest talking region belongs to the observed person;
                // its bubble must not create a second generic object target.
                if(quad.uid==19 && IsForestInteraction(context,engine.GetObjUID(9) as Actor,quad))continue;
                if(NativeFieldActorInteractions.IsBoundRegion(context,engine,quad))continue;
                var touch = Decode(engine,quad,EventEngine.tagPush); var talk = Decode(engine,quad,EventEngine.tagTalk);
                Vector3 center = Vector3.zero; for(int i=0;i<quad.n && i<quad.q.Length;i++) center+=quad.q[i].Vector3Val; center/=quad.n;
                bool quadIcon=EIcon.NavigationPresentedUid==quad.uid && OnScreen(field,Vector(context.Position));
                if (!ContainsJump(touch)) {
                    if(quadIcon)result.Add(ObservedQuad(context,engine,quad,center));
                    continue;
                }
                string fingerprint = Fingerprint(quad.ebData);
                DoorBinding binding;
                if(!_doors.TryGetValue(quad.ebData,out binding)) {
                    Func<int,IList<ScriptInstruction>> requested=tag=>player==null?new List<ScriptInstruction>():Decode(engine,player,tag);
                    binding=new DoorBinding{Walk=FieldDoorEvidence.HasWalkOut(touch,requested,_opcodes),Plain=FieldDoorEvidence.HasPlainWalkOut(touch,requested,_opcodes),Animations=FieldDoorEvidence.Animations(touch,requested,_opcodes)};_doors[quad.ebData]=binding;
                }
                bool reviewed=FieldExitEvidence.HasSurfaceReview(context.LogicalMapKey,quad.sid) || FieldExitEvidence.HasBoundaryReview(context.LogicalMapKey,quad.sid);
                if(!geometryRead && (binding.Walk || reviewed || quadIcon)){currentGeometry=NativeFieldGeometry.Read(context);geometryRead=true;}
                ExitPresentation presentation=VisibleBoundary(context,currentGeometry,field,engine,player,quad,binding,reviewed,fingerprint);
                if(presentation==null && quadIcon) {
                    // A talk/Confirm outcome is an interaction object until a walk-exit cue is established.
                    result.Add(ObservedQuad(context,engine,quad,center));
                    continue;
                }
                if (presentation != null) {
                    byte[] instance=ScriptSnapshot.InstanceBytes(quad.buffer,quad.vofs);
                    // Movement remains gated by context.CanControl. A temporary
                    // dialogue must not replace a remembered entrance with a
                    // disabled geometry snapshot solely because UC is false.
                    var snapshot = new ScriptSnapshot(FF9StateSystem.EventState.gEventGlobal,engine.GetMapVar(),instance) { UserControl = true };
                    var outcome = ExitScriptAnalyzer.Evaluate(touch,snapshot,(int)EBin.event_code_binary.MAPJUMP,(int)EBin.event_code_binary.WMAPJUMP,(int)EBin.event_code_binary.MOVQ,(int)EBin.event_code_binary.MJPOS);
                    if(!presentation.ApproachEnabled)presentation.Availability="safe approach unavailable";
                    else if(!EventCollision.NavigationCanTriggerQuad(player,quad)){presentation.ApproachEnabled=false;presentation.Availability="current entrance trigger unavailable";}
                    else if(outcome==ScriptExitOutcome.NoJumpNow){presentation.ApproachEnabled=false;presentation.Availability="current entrance trigger unavailable";}
                    else presentation.Availability = outcome == ScriptExitOutcome.JumpNow ? null : "availability requires normal game interaction";
                }
                // Retain only an exit already observed in this scene. A live
                // disabled/changed region must not reuse its old approach.
                int exitIdentity=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(quad)^fingerprint.GetHashCode();
                if(presentation!=null && presentation.Visible)_seenExits.Add(exitIdentity);
                bool offscreen=presentation==null && _seenExits.Contains(exitIdentity) && EventCollision.NavigationCanTriggerQuad(player,quad);
                if(offscreen) {
                    byte[] instance=ScriptSnapshot.InstanceBytes(quad.buffer,quad.vofs);
                    offscreen=ExitScriptAnalyzer.Evaluate(touch,new ScriptSnapshot(FF9StateSystem.EventState.gEventGlobal,engine.GetMapVar(),instance){UserControl=true},(int)EBin.event_code_binary.MAPJUMP,(int)EBin.event_code_binary.WMAPJUMP,(int)EBin.event_code_binary.MOVQ,(int)EBin.event_code_binary.MJPOS)!=ScriptExitOutcome.NoJumpNow;
                }
                if(offscreen) {
                    var safe=new List<NavigationPoint>();
                    if(currentGeometry!=null && (binding.Walk || reviewed))foreach(var point in Surface(currentGeometry,quad).Approaches)if(engine.NavigationTouchQuadAt(point.X,point.Z)==quad)safe.Add(point);
                    presentation=new ExitPresentation{Visible=false,ApproachEnabled=safe.Count>0,ApproachCandidates=safe.ToArray(),Availability=safe.Count>0?"availability requires normal game interaction":"safe approach unavailable"};
                }
                var exitObservation=new FieldTargetObservation { Id = new TargetIdentity(TargetKind.FieldExit, context.LogicalMapKey,quad.uid),NativePresentationIdentity=exitIdentity,OutOfView=offscreen, Position = presentation == null ? Point(center) : presentation.Approach, Exit = presentation, ContainsJump = true };
                Annotate(context,engine,quad,exitObservation);result.Add(exitObservation);
            }
            return result;
        }
        private static FieldTargetObservation ObservedQuad(NavigationContext context,EventEngine engine,Quad quad,Vector3 center)
        {
            var region=new NavigationPoint[quad.n];for(int i=0;i<region.Length;i++)region[i]=Point(quad.q[i].Vector3Val);
            var observation=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,quad.uid),NativePresentationIdentity=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(quad),Position=Point(center),ShowsIcon=true,HasInteraction=true,InteractionUid=quad.uid,InteractionRegion=region,ApproachCandidates=new[]{engine.NavigationContainsQuad(quad,context.Position.X,context.Position.Z)?context.Position:Point(center)}};
            Annotate(context,engine,quad,observation);return observation;
        }
        private static void Annotate(NavigationContext context,EventEngine engine,Obj obj,FieldTargetObservation observation)
        {
            NativeStoryTargets.Annotate(context,obj.sid,Fingerprint(obj.ebData),observation,delegate {
                var combined=new ScriptStoryEvidence();
                foreach(int tag in new[]{EventEngine.tagPush,EventEngine.tagTalk}) {
                    var snapshot=new ScriptSnapshot(FF9StateSystem.EventState.gEventGlobal,engine.GetMapVar(),ScriptSnapshot.InstanceBytes(obj.buffer,obj.vofs)){UserControl=true};
                    Func<int,int,int,ScriptSnapshot,ScriptStoryFunction> requested=(uid,calledTag,callerIdentity,caller)=> {
                        var called=uid==255?engine.GetObjUID(callerIdentity/256):uid==250?engine.GetControlChar():engine.GetObjUID(uid);
                        if(called==null || called.ebData==null || !NativeStoryTargets.Matches(context,called.sid,Fingerprint(called.ebData)))return null;
                        return new ScriptStoryFunction{Identity=called.uid*256+calledTag,Code=Decode(engine,called,calledTag),Snapshot=caller.WithInstance(ScriptSnapshot.InstanceBytes(called.buffer,called.vofs))};
                    };
                    var evidence=ScriptStoryAnalyzer.Read(new ScriptStoryFunction{Identity=obj.uid*256+tag,Code=Decode(engine,obj,tag),Snapshot=snapshot},requested,_opcodes.MapJump,_opcodes.WorldJump,_opcodes.Requests);
                    combined.Milestones.UnionWith(evidence.Milestones);combined.Destinations.UnionWith(evidence.Destinations);
                }
                return combined;
            },(sid,hash)=>{var controlled=engine.GetControlChar();return controlled!=null && controlled.uid==context.PlayerIdentity && controlled.sid==sid && controlled.ebData!=null && Fingerprint(controlled.ebData)==hash;});
        }
        public static bool IsReviewedListener(NavigationContext context,EventEngine engine,Obj obj)
        {
            if(context==null || engine==null || obj==null || obj.ebData==null || engine.GetIP(obj.sid,EventEngine.tagTalk,obj.ebData)==engine.nil)return false;
            return NativeStoryTargets.ReviewedListener(context,obj.sid,Fingerprint(obj.ebData),(sid,hash)=>{var controlled=engine.GetControlChar();return controlled!=null && controlled.uid==context.PlayerIdentity && controlled.sid==sid && controlled.ebData!=null && Fingerprint(controlled.ebData)==hash;});
        }
        private static IEnumerable<Obj> ActiveObjects(EventEngine engine) { for(var list=engine.GetActiveObjList();list!=null;list=list.next) if(list.obj!=null)yield return list.obj; }
        public static bool IsForestInteraction(NavigationContext context,Actor actor,Quad region)
        {
            if(context==null || context.LogicalMapKey!=ForestNavigationEvidence.Map || actor==null || actor.sid!=9 || actor.model!=220 || actor.ebData==null || region==null || region.uid!=19 || region.sid!=19 || region.ebData==null || region.q==null || region.n<3 || region.n>16 || region.n>region.q.Length)return false;
            var position=actor.fieldMapActorController==null?new NavigationPoint(actor.pos[0],actor.pos[1],actor.pos[2]):Point(actor.fieldMapActorController.curPos);
            return ForestNavigationEvidence.MeneRegion(context.LogicalMapKey,actor.sid,actor.model,Fingerprint(actor.ebData),region.sid,Fingerprint(region.ebData),position);
        }
        private static List<ScriptInstruction> Decode(EventEngine engine,Obj obj,int tag)
        {
            var result=new List<ScriptInstruction>(); int start=engine.GetIP(obj.sid,tag,obj.ebData); if(start==engine.nil || start<0)return result;
            Dictionary<int,List<ScriptInstruction>> functions;List<ScriptInstruction> cached;
            if(!_decoded.TryGetValue(obj.ebData,out functions)){functions=new Dictionary<int,List<ScriptInstruction>>();_decoded[obj.ebData]=functions;}
            if(functions.TryGetValue(start,out cached))return cached;
            int end=obj.ebData.Length; int count=obj.ebData.Length>1?obj.ebData[1]:0;
            for(int i=0;i<count;i++) { int p=2+4*i; if(p+3>=obj.ebData.Length)break; int offset=2+obj.ebData[p+2]+(obj.ebData[p+3]<<8); if(offset>start && offset<end)end=offset; }
            try { uint cursor=(uint)start; while(cursor<end && result.Count<1024) {
                int offset=(int)cursor; var code=new Code(obj.ebData,ref cursor); var item=new ScriptInstruction(offset,(int)cursor,code.opcode);
                if(cursor>end) return new List<ScriptInstruction>();
                item.Arguments=new int?[code.arguments.Count];item.ArgumentExpressions=new byte[code.arguments.Count][];
                for(int argument=0;argument<code.arguments.Count;argument++) {
                    var constant=code.arguments[argument] as Code.ArgumentConstant; if(constant!=null)item.Arguments[argument]=(int)constant.value;
                    var expression=code.arguments[argument] as Code.ArgumentExpression;
                    if(expression!=null) { var bytes=new List<byte>(); foreach(var operation in expression.operations) { bytes.Add(operation.operation); if(operation.arguments!=null)bytes.AddRange(operation.arguments); } item.ArgumentExpressions[argument]=bytes.ToArray(); }
                }
                item.Argument=item.Constant(0);item.Expression=item.ArgumentExpression(0);
                result.Add(item);
            } } catch(Exception exception) { Log.Message("[FF9Access] Entrance decoding unresolved: {0}",exception.Message); result.Clear(); }
            functions[start]=result;return result;
        }
        private static bool ContainsJump(IList<ScriptInstruction> code) { foreach(var item in code)if(item.Opcode==(int)EBin.event_code_binary.MAPJUMP || item.Opcode==(int)EBin.event_code_binary.WMAPJUMP)return true; return false; }
        private static RegionSurface Surface(FieldGeometrySnapshot geometry,Quad quad)
        {
            var region=new NavigationPoint[quad.n];string key="";
            for(int i=0;i<quad.n;i++){region[i]=Point(quad.q[i].Vector3Val);key+=region[i].X+","+region[i].Z+";";}
            RegionSurface surface;
            if(!_surfaces.TryGetValue(quad,out surface) || surface.Geometry!=geometry.Fingerprint || surface.Polygon!=key) {
                surface=new RegionSurface{Geometry=geometry.Fingerprint,Polygon=key,Region=region,Approaches=FieldRegionApproaches.Interaction(region,geometry),Boundaries=FieldRegionApproaches.BoundarySamples(region,geometry)};_surfaces[quad]=surface;
            }
            return surface;
        }
        private static ExitPresentation VisibleBoundary(NavigationContext context,FieldGeometrySnapshot geometry,FieldMap field,EventEngine engine,Actor player,Quad quad,DoorBinding binding,bool reviewed,string hash)
        {
            if((!binding.Walk && !reviewed) || geometry==null || field.walkMesh==null || field.bgi==null)return null;
            var surface=Surface(geometry,quad);FieldExitSurfaceReview surfaceReview=null;string boundaryCue=null;
            bool plain=binding.Plain;
            if(!plain && binding.Walk && binding.Animations.Length==0 && player!=null && !Single.IsNaN(player.pos[1]) && !Single.IsInfinity(player.pos[1])) {
                int height=-EventEngineUtils.CastFloatToIntWithChecking(player.pos[1]);
                var snapshot=new ScriptSnapshot(FF9StateSystem.EventState.gEventGlobal,engine.GetMapVar(),ScriptSnapshot.InstanceBytes(quad.buffer,quad.vofs)){UserControl=true};
                plain=FieldDoorEvidence.CurrentPlainWalkOut(Decode(engine,quad,EventEngine.tagPush),snapshot,_opcodes,(uid,property)=>uid==250 && property==1?(int?)height:null);
            }
            if(reviewed && field.scene!=null && field.scene.ebgBin!=null) {
                if(_walkmeshHash==null) {
                    try {var bytes=AssetManager.LoadBytes(FieldMap.GetMapResourcePath(field.mapName)+field.mapName+".bgi");_walkmeshHash=bytes==null?"":Fingerprint(bytes);}
                    catch(Exception exception){_walkmeshHash="";Log.Message("[FF9Access] Entrance asset evidence unavailable: {0}",exception.Message);}
                }
                string backgroundHash=Fingerprint(field.scene.ebgBin);
                FieldExitEvidence.TrySurface(context.LogicalMapKey,quad.sid,field.camIdx,hash,backgroundHash,_walkmeshHash,surface.Region,out surfaceReview);
                FieldExitEvidence.TryBoundary(context.LogicalMapKey,quad.sid,field.camIdx,hash,backgroundHash,_walkmeshHash,out boundaryCue);
            }
            var animationFrames=new Dictionary<int,IList<DoorTileObservation>>();
            if(field.scene!=null)foreach(int animation in binding.Animations)if(animation>=0 && animation<field.scene.animList.Count)animationFrames[animation]=ReadFrames(field,animation);
            Func<NavigationPoint,bool> presented=p=> {
                if(!EntranceOnScreen(field,Vector(p)))return false;
                if(plain && RenderedBackgroundAt(field,Vector(p)))return true;
                if(surfaceReview!=null && RenderedBackgroundAt(field,Vector(p),surfaceReview.OverlayIds))return true;
                int overlay;foreach(var animation in animationFrames)if(FieldDoorEvidence.TryBind(field.scene.animList[animation.Key].camNdx,field.camIdx,animation.Value,Point(Project(field,Vector(p))),true,out overlay))return true;
                return false;
            };
            Func<NavigationPoint,bool> nativeFirst=p=>engine.NavigationTouchQuadAt(p.X,p.Z)==quad;
            NavigationPoint approach;
            if(FieldDoorEvidence.TryTriggerSurface(binding.Walk || surfaceReview!=null,true,surface.Approaches,presented,nativeFirst,context.Position,out approach)) {
                var safe=new List<NavigationPoint>();foreach(var point in surface.Approaches)if(nativeFirst(point))safe.Add(point);
                string cue=surfaceReview!=null?surfaceReview.Cue:plain?"Rendered native walk-out surface":"Current rendered door tile at native trigger surface";
                if(_cueLogs.Count<64 && _cueLogs.Add("surface:"+quad.uid+":"+field.camIdx))Log.Message("[FF9Access] Entrance trigger surface: map={0}; uid={1}; sid={2}; camera={3}; candidates={4}; approach={5}; cue={6}",context.LogicalMapKey,quad.uid,quad.sid,field.camIdx,safe.Count,Vector(approach),cue);
                return new ExitPresentation{Visible=true,ApproachEnabled=true,Approach=approach,ApproachCandidates=safe.ToArray(),Evidence=surfaceReview!=null?VisibilityEvidence.ReviewedBackground:plain?VisibilityEvidence.ObservedRegion:VisibilityEvidence.NativeDoorTile,Cue=cue,SourceFingerprint=hash};
            }
            // A visible opening can still be listed when the player cannot fit.
            // These floor/boundary samples never become movement or sound points.
            if(surface.Presentation==null)surface.Presentation=FieldRegionApproaches.Presentation(surface.Region,geometry);
            if(FieldDoorEvidence.TryTriggerSurface(binding.Walk || surfaceReview!=null,true,surface.Presentation,presented,nativeFirst,context.Position,out approach))
                return new ExitPresentation{Visible=true,ApproachEnabled=false,Approach=approach,ApproachCandidates=new NavigationPoint[0],Evidence=surfaceReview!=null?VisibilityEvidence.ReviewedBackground:plain?VisibilityEvidence.ObservedRegion:VisibilityEvidence.NativeDoorTile,Cue=surfaceReview==null?"Rendered native entrance; safe approach unavailable":surfaceReview.Cue,SourceFingerprint=hash};
            // Exact clipped edge intersections also cover a trigger which only
            // touches the floor boundary, without depending on fixed samples.
            if(FieldDoorEvidence.TryTriggerSurface(binding.Walk || surfaceReview!=null || boundaryCue!=null,true,surface.Boundaries,p=>presented(p) || boundaryCue!=null && EntranceOnScreen(field,Vector(p)) && RenderedBackgroundAt(field,Vector(p)),nativeFirst,context.Position,out approach))
                return new ExitPresentation{Visible=true,ApproachEnabled=false,Approach=approach,ApproachCandidates=new NavigationPoint[0],Evidence=VisibilityEvidence.ReviewedBackground,Cue=boundaryCue,SourceFingerprint=hash};
            return null;
        }
        private static IList<DoorTileObservation> ReadFrames(FieldMap field,int animation)
        {
            var frames=new List<DoorTileObservation>();var ids=new HashSet<int>();
            foreach(var frame in field.scene.animList[animation].frameList)if(ids.Add(frame.target) && frame.target>=0 && frame.target<field.scene.overlayList.Count)frames.Add(ReadTile(field,frame.target));
            return frames;
        }
        private static DoorTileObservation ReadTile(FieldMap field,int id)
        {
            // Share live renderer observations across every exit and candidate
            // in this frame. Refresh on the next frame or scene replacement.
            if(!ReferenceEquals(_tileScene,field.scene) || _tileFrame!=Time.frameCount){_tiles.Clear();_tileScene=field.scene;_tileFrame=Time.frameCount;}
            DoorTileObservation tile;if(!_tiles.TryGetValue(id,out tile)){tile=ReadTile(field.scene.overlayList[id],id);_tiles[id]=tile;}return tile;
        }
        private static DoorTileObservation ReadTile(BGOVERLAY_DEF overlay,int id)
        {
            var tile=new DoorTileObservation{Overlay=id,Camera=overlay.camNdx,Flags=(int)overlay.flags,HierarchyActive=overlay.transform!=null && overlay.transform.gameObject.activeInHierarchy};
            if(!tile.HierarchyActive || (tile.Flags&2)==0 || (tile.Flags&(1|8|128))!=0 || overlay.viewportNdx!=0)return tile;
            foreach(var renderer in overlay.transform.GetComponentsInChildren<Renderer>())if(renderer.enabled && renderer.gameObject.activeInHierarchy) {
                bool drawn=false;foreach(var material in renderer.sharedMaterials)if(material!=null && (!material.HasProperty("_Color") || material.GetColor("_Color").a>0)){drawn=true;break;}
                if(!drawn)continue;var bounds=renderer.bounds;if(bounds.size.x<=0 || bounds.size.y<=0)continue;
                if(!tile.HasRenderer){tile.MinX=bounds.min.x;tile.MinY=bounds.min.y;tile.MaxX=bounds.max.x;tile.MaxY=bounds.max.y;tile.HasRenderer=true;}
                else {tile.MinX=Math.Min(tile.MinX,bounds.min.x);tile.MinY=Math.Min(tile.MinY,bounds.min.y);tile.MaxX=Math.Max(tile.MaxX,bounds.max.x);tile.MaxY=Math.Max(tile.MaxY,bounds.max.y);}
            }return tile;
        }
        private static bool RenderedBackgroundAt(FieldMap field,Vector3 point,int[] overlayIds=null)
        {
            if(field.scene==null)return false;var projected=Project(field,point);
            int count=overlayIds==null || overlayIds.Length==0?field.scene.overlayList.Count:overlayIds.Length;
            for(int index=0;index<count;index++) {
                int i=overlayIds==null || overlayIds.Length==0?index:overlayIds[index];if(i<0 || i>=field.scene.overlayList.Count)continue;
                var tile=ReadTile(field,i);int overlay;
                if(FieldDoorEvidence.TryBind(tile.Camera,field.camIdx,new[]{tile},Point(projected),true,out overlay))return true;
            }return false;
        }
        private static Vector3 Project(FieldMap field,Vector3 point)
        {
            var bg=field.GetCurrentBgCamera();return PSX.CalculateGTE_RTPT(point,Matrix4x4.identity,bg.GetMatrixRT(),bg.GetViewDistance(),field.GetProjectionOffset());
        }
        private static bool EntranceOnScreen(FieldMap field,Vector3 point)
        {
            var camera=field.GetMainCamera();if(camera==null || field.GetCurrentBgCamera()==null)return false;
            return FieldDoorEvidence.InViewport(Point(camera.WorldToViewportPoint(Project(field,point))),1,1);
        }
        private static bool OnScreen(FieldMap field,Vector3 point)
        {
            var camera=field.GetMainCamera(); var bg=field.GetCurrentBgCamera(); if(camera==null || bg==null)return false;
            Vector3 projected=Project(field,point); projected=camera.WorldToScreenPoint(projected);
            return projected.x>=-24 && projected.x<=Screen.width+24 && projected.y>=-24 && projected.y<=Screen.height+24;
        }
        private static string Fingerprint(byte[] bytes) { string hash; if(!_hashes.TryGetValue(bytes,out hash)) { using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(); _hashes[bytes]=hash; } return hash; }
        private static NavigationPoint Point(Vector3 value) { return new NavigationPoint(value.x,value.y,value.z); }
        private static Vector3 Vector(NavigationPoint value) { return new Vector3(value.X,value.Y,value.Z); }
    }
}
