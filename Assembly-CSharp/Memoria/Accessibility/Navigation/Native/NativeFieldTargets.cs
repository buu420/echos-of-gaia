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
        private sealed class ActorLabelIdentity {public int Model,Sid;public byte[] Script;public string Signature;}
        private static readonly Dictionary<Actor,ActorLabelIdentity> _labelIdentities=new Dictionary<Actor,ActorLabelIdentity>();
        private static readonly Dictionary<byte[],Dictionary<int,List<ScriptInstruction>>> _decoded=new Dictionary<byte[],Dictionary<int,List<ScriptInstruction>>>();
        private static readonly Dictionary<byte[],DoorBinding> _doors=new Dictionary<byte[],DoorBinding>();
        private sealed class RegionSurface {public string Geometry,Polygon;public NavigationPoint[] Region,Approaches,Boundaries,Presentation;}
        private static readonly Dictionary<Quad,RegionSurface> _surfaces=new Dictionary<Quad,RegionSurface>();
        private static BGSCENE_DEF _tileScene;private static int _tileFrame=-1;
        private static readonly Dictionary<int,DoorTileObservation> _tiles=new Dictionary<int,DoorTileObservation>();
        private static int _doorPlayer=-1;private static byte[] _doorPlayerScript;
        private static readonly HashSet<string> _cueLogs=new HashSet<string>();
        private static int _rosterLogs;
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
            if (_generation != context.Generation) { _hashes.Clear();_labelIdentities.Clear();_decoded.Clear();_doors.Clear();_surfaces.Clear();_cueLogs.Clear();_seenExits.Clear();_walkmeshHash=null;_rosterLogs=0; _generation = context.Generation; }
            var engine = UnityEngine.Object.FindObjectOfType<EventEngine>();
            var field = UnityEngine.Object.FindObjectOfType<FieldMap>();
            if (engine == null || field == null) return result;
            var player=engine.GetControlChar() as Actor;
            int doorPlayer=player==null?-1:player.uid;byte[] doorScript=player==null?null:player.ebData;
            if(_doorPlayer!=doorPlayer || !System.Object.ReferenceEquals(_doorPlayerScript,doorScript)) {_doors.Clear();_doorPlayer=doorPlayer;_doorPlayerScript=doorScript;}
            FieldGeometrySnapshot currentGeometry=null;bool geometryRead=false;
            var openingCandle=ReadOpeningCandle(context,engine,field,player);
            if(openingCandle!=null)result.Add(openingCandle);
            var openingBooth=ReadOpeningTicketBooth(context,engine,field,player,ref currentGeometry,ref geometryRead);
            if(openingBooth!=null)result.Add(openingBooth);
            foreach (Obj obj in ActiveObjects(engine)) {
                if (obj.uid == context.PlayerIdentity) continue;
                var actor = obj as Actor;
                if (actor != null) {
                    Vector3 position = actor.fieldMapActorController == null ? new Vector3(actor.pos[0], actor.pos[1], actor.pos[2]) : actor.fieldMapActorController.curPos;
                    bool rendered = (obj.flags & EventEngine.flagShow) != 0 && obj.isEnableRenderer && actor.frontCamera && obj.go != null && obj.go.activeInHierarchy;
                    bool mesh = false; if (rendered) foreach (Renderer renderer in obj.go.GetComponentsInChildren<Renderer>()) {
                        if(!renderer.enabled || !renderer.gameObject.activeInHierarchy || actor.fieldMapActor!=null && renderer==actor.fieldMapActor.shadowMeshRenderer)continue;
                        foreach(var material in renderer.sharedMaterials)if(material!=null && (!material.HasProperty("_Color") || material.GetColor("_Color").a>0)){mesh=true;break;}
                    }
                    bool onScreen=rendered && mesh && OnScreen(field,position);
                    var bg=field.GetCurrentBgCamera();
                    var projected=bg==null?new NavigationPoint():Point(PSX.CalculateGTE_RTPT_POS(position,Matrix4x4.identity,bg.GetMatrixRT(),bg.GetViewDistance(),field.GetProjectionOffset()));
                    bool inArea=FieldScenePresentation.Actor(rendered,mesh,onScreen,ReadAreaPanorama(field),projected);
                    bool icon=EIcon.NavigationPresentedUid==obj.uid && OnScreen(field,Vector(context.Position));
                    float collision=player==null?0:Math.Max(player.fieldMapActorController==null?0:player.fieldMapActorController.radius,4*player.collRad)+4*actor.collRad;
                    float talkRadius=player==null?0:4*(player.talkRad+actor.talkRad)+actor.speed+60;
                    var observation=new FieldTargetObservation { Id = new TargetIdentity(TargetKind.FieldActor, context.LogicalMapKey, obj.uid), NativePresentationIdentity=obj.go==null?0:obj.go.GetInstanceID(),Position = Point(position), Visible = inArea,OutOfView=rendered && mesh && !inArea,ShowsIcon=icon, IsPerson = engine.GetIP(obj.sid, EventEngine.tagTalk, obj.ebData) != engine.nil, HasInteraction = NativeFieldTalkability.IsTalkable(obj) || engine.GetIP(obj.sid, EventEngine.tagPush, obj.ebData) != engine.nil,ApproachCandidates=FieldActorApproaches.Create(context.Position,Point(position),collision,talkRadius) };
                    observation.LabelIdentity=LabelSignature(actor);observation.VisibleLabel=FieldVisibleLabels.Read(actor.model,VisibleBody(actor));
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
                if(openingBooth!=null && quad.uid==openingBooth.Id.NativeId)continue;
                // The proven Forest talking region belongs to the observed person;
                // its bubble must not create a second generic object target.
                if(quad.uid==19 && IsForestInteraction(context,engine.GetObjUID(9) as Actor,quad))continue;
                if(NativeFieldActorInteractions.IsBoundRegion(context,engine,quad))continue;
                var touch = Decode(engine,quad,EventEngine.tagPush); var talk = Decode(engine,quad,EventEngine.tagTalk);
                Vector3 center = Vector3.zero; for(int i=0;i<quad.n && i<quad.q.Length;i++) center+=quad.q[i].Vector3Val; center/=quad.n;
                bool quadIcon=EIcon.NavigationPresentedUid==quad.uid && OnScreen(field,Vector(context.Position));
                if (!ContainsJump(touch)) {
                    var storyRegion=ReadStoryRegion(context,engine,field,player,quad,ref currentGeometry,ref geometryRead);
                    if(storyRegion!=null)result.Add(storyRegion);
                    else if(quadIcon)result.Add(ObservedQuad(context,engine,quad,center));
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
                exitObservation.LabelIdentity=ExitLabelSignature(quad);
                Annotate(context,engine,quad,exitObservation);
                if(presentation!=null && presentation.Visible) {
                    var camera=field.GetMainCamera();
                    exitObservation.VisibleLabel=camera==null?"Exit":FieldExitLabel.Read(Point(camera.WorldToViewportPoint(Project(field,Vector(presentation.Approach)))));
                    NativeObservedFieldExits.Presented(context,quad,exitObservation.LabelIdentity);
                }
                result.Add(exitObservation);
            }
            RemoveLadderDuplicate(context,engine,result);
            CacheStoryGeometry(context,currentGeometry);
            LogPresentedRoster(context,result);
            return result;
        }
        private static void LogPresentedRoster(NavigationContext context,List<FieldTargetObservation> observations)
        {
            if(!context.CanControl || _rosterLogs>=8)return;
            var people=new List<string>();var exits=new List<string>();var objects=new List<string>();
            foreach(var item in observations) {
                if(item.Id.Kind==TargetKind.FieldExit){if(item.Exit!=null && item.Exit.Visible)exits.Add(item.Id.NativeId.ToString());}
                else if(item.Visible || item.ShowsIcon)(item.IsPerson?people:objects).Add(item.Id.NativeId.ToString());
            }
            string summary="people="+String.Join(",",people.ToArray())+"; objects="+String.Join(",",objects.ToArray())+"; exits="+String.Join(",",exits.ToArray());
            if(!_cueLogs.Add("roster:"+summary))return;
            _rosterLogs++;Log.Message("[FF9Access] Presented field entities: map={0}; {1}",context.LogicalMapKey,summary);
        }
        private static FieldTargetObservation ObservedQuad(NavigationContext context,EventEngine engine,Quad quad,Vector3 center)
        {
            var region=new NavigationPoint[quad.n];for(int i=0;i<region.Length;i++)region[i]=Point(quad.q[i].Vector3Val);
            var observation=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,quad.uid),NativePresentationIdentity=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(quad),Position=Point(center),ShowsIcon=true,HasInteraction=true,InteractionUid=quad.uid,InteractionRegion=region,ApproachCandidates=new[]{engine.NavigationContainsQuad(quad,context.Position.X,context.Position.Z)?context.Position:Point(center)}};
            observation.LabelIdentity=ExitLabelSignature(quad);
            Annotate(context,engine,quad,observation);return observation;
        }
        private static bool VisibleBody(Actor actor)
        {
            if(actor.model!=102 || actor.go==null)return false;
            var body=actor.go.transform.Find("mesh1");if(body==null || !body.gameObject.activeInHierarchy)return false;
            foreach(var renderer in body.GetComponentsInChildren<Renderer>())if(renderer.enabled && renderer.gameObject.activeInHierarchy)return true;
            return false;
        }
        private static void Annotate(NavigationContext context,EventEngine engine,Obj obj,FieldTargetObservation observation)
        {Annotate(context,engine,obj,observation,-1);}
        private static void Annotate(NavigationContext context,EventEngine engine,Obj obj,FieldTargetObservation observation,int onlyTag)
        {
            NativeStoryTargets.Annotate(context,obj.sid,Fingerprint(obj.ebData),observation,()=>ReadStoryEvidence(context,engine,obj,onlyTag,null),
                (sid,hash)=>{var controlled=engine.GetControlChar();return controlled!=null && controlled.uid==context.PlayerIdentity && controlled.sid==sid && controlled.ebData!=null && Fingerprint(controlled.ebData)==hash;});
            AnnotateExitSurfaces(context,engine,obj,observation);
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
                CurrentBgiHash(field);
                string backgroundHash=Fingerprint(field.scene.ebgBin);
                FieldExitEvidence.TrySurface(context.LogicalMapKey,quad.sid,field.camIdx,hash,backgroundHash,_walkmeshHash,surface.Region,out surfaceReview);
                FieldExitEvidence.TryBoundary(context.LogicalMapKey,quad.sid,field.camIdx,hash,backgroundHash,_walkmeshHash,out boundaryCue);
            }
            var animationFrames=new Dictionary<int,IList<DoorTileObservation>>();
            if(field.scene!=null)foreach(int animation in binding.Animations)if(animation>=0 && animation<field.scene.animList.Count)animationFrames[animation]=ReadFrames(field,animation);
            var panorama=plain?ReadPassagePanorama(context,field,quad,hash,surface.Region):null;
            Func<NavigationPoint,bool> presented=p=> {
                bool onScreen=EntranceOnScreen(field,Vector(p));
                // A plain walk-off belongs to the whole current camera panorama.
                // Native trigger priority, floor clearance and the active opaque
                // backdrop are still required before it becomes a target.
                if(plain && (onScreen?RenderedBackgroundAt(field,Vector(p)):PlainPassageOnPanorama(field,Vector(p),panorama)))return true;
                if(!onScreen)return false;
                if(surfaceReview!=null && RenderedBackgroundAt(field,Vector(p),surfaceReview.OverlayIds))return true;
                int overlay;foreach(var animation in animationFrames)if(FieldDoorEvidence.TryBind(field.scene.animList[animation.Key].camNdx,field.camIdx,animation.Value,Point(Project(field,Vector(p))),true,out overlay))return true;
                return false;
            };
            Func<NavigationPoint,bool> nativeFirst=p=>engine.NavigationTouchQuadAt(p.X,p.Z)==quad;
            NavigationPoint approach;
            if(FieldDoorEvidence.TryTriggerSurface(binding.Walk || surfaceReview!=null,true,surface.Approaches,presented,nativeFirst,context.Position,out approach)) {
                var safe=new List<NavigationPoint>();foreach(var point in surface.Approaches)if(nativeFirst(point))safe.Add(point);
                string cue=surfaceReview!=null?surfaceReview.Cue:plain?(EntranceOnScreen(field,Vector(approach))?"Rendered native walk-out surface":"Rendered native passage on the scrolling field panorama"):"Current rendered door tile at native trigger surface";
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
        private static DoorTileObservation ReadTile(FieldMap field,int id,bool opaqueOnly=false)
        {
            // Share live renderer observations across every exit and candidate
            // in this frame. Refresh on the next frame or scene replacement.
            if(!ReferenceEquals(_tileScene,field.scene) || _tileFrame!=Time.frameCount){_tiles.Clear();_tileScene=field.scene;_tileFrame=Time.frameCount;}
            int key=opaqueOnly?~id:id;
            DoorTileObservation tile;if(!_tiles.TryGetValue(key,out tile)){tile=ReadTile(field.scene.overlayList[id],id,opaqueOnly);_tiles[key]=tile;}return tile;
        }
        private static DoorTileObservation ReadTile(BGOVERLAY_DEF overlay,int id,bool opaqueOnly=false)
        {
            var tile=new DoorTileObservation{Overlay=id,Camera=overlay.camNdx,Flags=(int)overlay.flags,HierarchyActive=overlay.transform!=null && overlay.transform.gameObject.activeInHierarchy};
            if(!tile.HierarchyActive || (tile.Flags&2)==0 || (tile.Flags&(1|8|128))!=0 || overlay.viewportNdx!=0)return tile;
            foreach(var renderer in overlay.transform.GetComponentsInChildren<Renderer>())if(renderer.enabled && renderer.gameObject.activeInHierarchy) {
                bool drawn=false;foreach(var material in renderer.sharedMaterials)if(material!=null && (!material.HasProperty("_Color") || (opaqueOnly?FieldPanoramaPresentation.IsOpaque(material.GetColor("_Color").a):material.GetColor("_Color").a>0))){drawn=true;break;}
                if(!drawn)continue;var bounds=renderer.bounds;if(bounds.size.x<=0 || bounds.size.y<=0)continue;
                if(!tile.HasRenderer){tile.MinX=bounds.min.x;tile.MinY=bounds.min.y;tile.MaxX=bounds.max.x;tile.MaxY=bounds.max.y;tile.HasRenderer=true;}
                else {tile.MinX=Math.Min(tile.MinX,bounds.min.x);tile.MinY=Math.Min(tile.MinY,bounds.min.y);tile.MaxX=Math.Max(tile.MaxX,bounds.max.x);tile.MaxY=Math.Max(tile.MaxY,bounds.max.y);}
            }return tile;
        }
        private static bool RenderedBackgroundAt(FieldMap field,Vector3 point,int[] overlayIds=null,bool opaqueOnly=false)
        {
            if(field.scene==null)return false;var projected=Project(field,point);
            int count=overlayIds==null || overlayIds.Length==0?field.scene.overlayList.Count:overlayIds.Length;
            for(int index=0;index<count;index++) {
                int i=overlayIds==null || overlayIds.Length==0?index:overlayIds[index];if(i<0 || i>=field.scene.overlayList.Count)continue;
                var tile=ReadTile(field,i,opaqueOnly);int overlay;
                if(FieldDoorEvidence.TryBind(tile.Camera,field.camIdx,new[]{tile},Point(projected),true,out overlay))return true;
            }return false;
        }
        private static Vector3 Project(FieldMap field,Vector3 point)
        {
            var bg=field.GetCurrentBgCamera();return PSX.CalculateGTE_RTPT(point,Matrix4x4.identity,bg.GetMatrixRT(),bg.GetViewDistance(),field.GetProjectionOffset());
        }
        private static bool EntranceOnScreen(FieldMap field,Vector3 point)
        {
            var camera=field.GetMainCamera();var bg=field.GetCurrentBgCamera();if(camera==null || bg==null)return false;
            var projected=PSX.CalculateGTE_RTPT_POS(point,Matrix4x4.identity,bg.GetMatrixRT(),bg.GetViewDistance(),field.GetProjectionOffset());
            if(!Point(projected).IsFinite || projected.z<=0)return false;
            projected.z=0;
            return FieldDoorEvidence.InViewport(Point(camera.WorldToViewportPoint(projected)),1,1);
        }
        private static FieldCameraPanorama ReadPassagePanorama(NavigationContext context,FieldMap field,Quad quad,string script,NavigationPoint[] region)
        {
            var panorama=ReadAreaPanorama(field);if(panorama==null)return null;
            if(FieldPanoramaEvidence.IsReviewed(context.LogicalMapKey,quad.sid,field.camIdx) && (field.scene==null || field.scene.ebgBin==null ||
                !FieldPanoramaEvidence.Matches(context.LogicalMapKey,quad.sid,field.camIdx,script,Fingerprint(field.scene.ebgBin),CurrentBgiHash(field),region)))return null;
            return panorama;
        }
        private static FieldCameraPanorama ReadAreaPanorama(FieldMap field)
        {
            var camera=field.GetCurrentBgCamera();if(camera==null || field.camIdx!=field.curCamIdx)return null;
            return new FieldCameraPanorama{HalfWidth=FieldMap.HalfFieldWidth,HalfHeight=FieldMap.HalfFieldHeight,
                MinX=camera.vrpMinX,MaxX=camera.vrpMaxX,MinY=camera.vrpMinY,MaxY=camera.vrpMaxY};
        }
        private static bool PlainPassageOnPanorama(FieldMap field,Vector3 point,FieldCameraPanorama panorama)
        {
            if(panorama==null)return false;var camera=field.GetCurrentBgCamera();if(camera==null)return false;
            var projected=PSX.CalculateGTE_RTPT_POS(point,Matrix4x4.identity,camera.GetMatrixRT(),camera.GetViewDistance(),field.GetProjectionOffset());
            return FieldScenePresentation.Passage(true,RenderedBackgroundAt(field,point,null,true),panorama,Point(projected));
        }
        private static bool OnScreen(FieldMap field,Vector3 point)
        {
            var camera=field.GetMainCamera(); var bg=field.GetCurrentBgCamera(); if(camera==null || bg==null)return false;
            Vector3 projected=Project(field,point); projected=camera.WorldToScreenPoint(projected);
            return projected.x>=-24 && projected.x<=Screen.width+24 && projected.y>=-24 && projected.y<=Screen.height+24;
        }
        internal static string Fingerprint(byte[] bytes) { string hash; if(!_hashes.TryGetValue(bytes,out hash)) { using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(); _hashes[bytes]=hash; } return hash; }
        internal static string CurrentBgiHash(FieldMap field)
        {
            if(field==null)return null;
            if(_walkmeshHash==null) {
                try {var bytes=AssetManager.LoadBytes(FieldMap.GetMapResourcePath(field.mapName)+field.mapName+".bgi");_walkmeshHash=bytes==null?"":Fingerprint(bytes);}
                catch(Exception exception){_walkmeshHash="";Log.Message("[FF9Access] Entrance asset evidence unavailable: {0}",exception.Message);}
            }
            return _walkmeshHash;
        }
        internal static string ExitLabelSignature(Quad quad)
        {
            if(quad==null || quad.ebData==null || quad.q==null || quad.n<3 || quad.n>quad.q.Length)return null;
            var signature=new System.Text.StringBuilder(quad.sid+":"+Fingerprint(quad.ebData)+":");
            for(int i=0;i<quad.n;i++) {var p=quad.q[i].Vector3Val;signature.Append(p.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(p.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(p.z.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append(';');}
            return signature.ToString();
        }
        internal static string LabelSignature(Actor actor)
        {
            if(actor==null || actor.ebData==null)return null;
            ActorLabelIdentity entry;
            if(!_labelIdentities.TryGetValue(actor,out entry) || entry.Model!=actor.model || entry.Sid!=actor.sid || !ReferenceEquals(entry.Script,actor.ebData)) {
                entry=new ActorLabelIdentity{Model=actor.model,Sid=actor.sid,Script=actor.ebData,Signature=actor.model+":"+actor.sid+":"+Fingerprint(actor.ebData)};_labelIdentities[actor]=entry;
            }return entry.Signature;
        }
        private static NavigationPoint Point(Vector3 value) { return new NavigationPoint(value.x,value.y,value.z); }
        private static Vector3 Vector(NavigationPoint value) { return new Vector3(value.X,value.Y,value.Z); }
    }
}
