using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using Memoria.Prime;
using Code = EventEngineUtils.BinaryScript.Entry.Function.Code;
namespace Memoria.Accessibility.Navigation
{
    // Applies the live native interaction gates to an observed field actor, and binds proven
    // region-mediated conversations (FieldInteractionBindings) to their rendered speaker. Reads only:
    // no Request, KeyOn, icon, listener or EBin expression-stack writes. Confirm stays player controlled.
    public static class NativeFieldActorInteractions
    {
        private static long _generation=long.MinValue;
        private static readonly Dictionary<byte[],string> _hashes=new Dictionary<byte[],string>();
        private static readonly Dictionary<byte[],Dictionary<int,List<ScriptInstruction>>> _decoded=new Dictionary<byte[],Dictionary<int,List<ScriptInstruction>>>();
        private static readonly HashSet<string> _logs=new HashSet<string>();
        private static readonly Dictionary<Actor,int> _observedActors=new Dictionary<Actor,int>();
        private static int _snapshotFrame=-1;private static ScriptSnapshot _frameSnapshot;
        private static readonly Dictionary<Obj,Dictionary<int,FieldInteractionState>> _guards=new Dictionary<Obj,Dictionary<int,FieldInteractionState>>();
        private static readonly FieldInteractionOpcodes _opcodes=new FieldInteractionOpcodes{IconOpcode=(int)EBin.event_code_binary.FICON};
        // Called by NativeFieldTargets after its own actor, story and Forest annotation.
        public static void Annotate(NavigationContext context,EventEngine engine,Actor player,Actor actor,FieldMap field,FieldTargetObservation observation)
        {
            try {
                if(context==null || engine==null || actor==null || observation==null || actor.ebData==null || context.LogicalMapKey==null || !context.LogicalMapKey.StartsWith("field:"))return;
                Reset(context);
                if(observation.Visible)_observedActors[actor]=observation.NativePresentationIdentity;
                else if(!observation.OutOfView)_observedActors.Remove(actor);
                // Only this actor's own bubble is presented and no body is drawn (model-less touch props):
                // the game offers something here, and nothing about it is guessed.
                if(!observation.Visible && observation.ShowsIcon && String.IsNullOrEmpty(observation.VisibleLabel))observation.VisibleLabel=FieldBackgroundObjects.PresentedPrompt;
                // A reviewed binding already owns this actor's interaction (Forest Mene).
                if(observation.InteractionUid>=0)return;
                Quad region;FieldInteractionBinding binding;
                if(TryBind(context,engine,player,actor,out region,out binding)) {
                    var polygon=Polygon(region);
                    observation.InteractionUid=region.uid;observation.InteractionRegion=polygon;observation.HasInteraction=true;observation.UseViewingPoint=false;
                    observation.ShowsIcon|=observation.Visible && EIcon.NavigationPresentedUid==region.uid && PlayerOnScreen(field,context);
                    // The requested function runs on this actor (its windows open on it): its role heading
                    // may describe the person when the person's own Talk heading gives none.
                    if(observation.IsPerson && NativeFieldTargets.OwnSpeakerLabel(context,engine,actor)==null) {
                        string role=NativeFieldTargets.RequestedSpeakerLabel(context,engine,actor,binding.RequestedTag);
                        if(role!=null)observation.VisibleLabel=FieldSpeakerRoles.Prefer(role,observation.VisibleLabel);
                    }
                    // The region is not listed separately, so its own Story evidence belongs to this person.
                    NativeFieldTargets.AnnotateBoundRegionStory(context,engine,region,observation);
                    Note(context,actor,"bound talk region "+region.uid+" (sid "+region.sid+", "+binding.Evidence+")");
                    return;
                }
                ApplyGates(context,engine,player,actor,observation);
            }catch(Exception exception){if(_logs.Count<64 && _logs.Add("error:"+exception.GetType().Name))Log.Message("[FF9Access] Field interaction state unavailable: {0}",exception.Message);}
        }
        // True when the region's reviewed actor currently owns it, so no second generic object is needed.
        public static bool IsBoundRegion(NavigationContext context,EventEngine engine,Quad quad)
        {
            try {
                if(context==null || engine==null || quad==null || quad.ebData==null || !FieldInteractionBindings.HasField(FF9StateSystem.Common.FF9.fldMapNo))return false;
                Reset(context);
                var binding=FieldInteractionBindings.FindRegion(FF9StateSystem.Common.FF9.fldMapNo,quad.sid,Hash(quad.ebData));
                if(binding==null)return false;
                var actor=engine.GetObjUID(binding.ActorUid) as Actor;Quad bound;FieldInteractionBinding owner;
                return actor!=null && TryBind(context,engine,engine.GetControlChar() as Actor,actor,out bound,out owner) && ReferenceEquals(bound,quad);
            }catch(Exception){return false;}
        }
        // NativeInteractionProbe: the presented target's reviewed talk region.
        public static bool IsBoundPair(NavigationContext context,EventEngine engine,Actor player,Actor actor,Quad region)
        {
            try {Reset(context);Quad bound;FieldInteractionBinding binding;return region!=null && TryBind(context,engine,player,actor,out bound,out binding) && ReferenceEquals(bound,region);}
            catch(Exception){return false;}
        }
        // Readiness at a reviewed counter (for NativeInteractionProbe). The native facing arc of
        // IsQuadTalkable decides only "turn to face"; the first selected Talk region, containment,
        // talk bytes, level and map rule must already hold. Reads only; Confirm stays with the player.
        public static InteractionReadiness ReadBoundReadiness(NavigationContext context,EventEngine engine,Actor player,Actor actor,Quad region)
        {
            try {
                if(!IsBoundPair(context,engine,player,actor,region) || player.fieldMapActorController==null)return null;
                var position=player.fieldMapActorController.curPos;
                bool permitted=region.level>1 && NativeFieldTalkability.IsTalkable(region) && FieldInteractionBindings.QuadTalkPermitted(FF9StateSystem.Common.FF9.fldMapNo,region.sid);
                var result=FieldInteractionBindings.Readiness(engine.TreadQuad(player,4)==region,engine.NavigationContainsQuad(region,position.x,position.z),permitted,
                    EventCollision.NavigationCanTalkQuad(player,region),EIcon.NavigationPresentedUid==region.uid);
                result.Evidence="Reviewed native talking region of this speaker, selected quad, request level and native facing arc; Confirm remains player controlled";
                return result;
            }catch(Exception){return null;}
        }
        // Readiness of a contact prop such as a chest (for NativeInteractionProbe): the actor has a push
        // handler and its Talk channel is closed. CollisionRequest selects it with walkMesh.Collision mode 2
        // (talk collision skips flagTalkInh actors) and its handler shows the bubble for itself (FICON keeps
        // the executing UID) before waiting for Confirm. Only that presented bubble makes it ready.
        // Reads only; nothing is requested or pressed.
        public static InteractionReadiness ReadPushReadiness(NavigationContext context,EventEngine engine,Actor player,Actor npc)
        {
            try {
                if(context==null || engine==null || engine.gMode!=1 || player==null || npc==null || npc.ebData==null || player.fieldMapActorController==null || player.fieldMapActorController.walkMesh==null)return null;
                if(engine.GetIP(npc.sid,EventEngine.tagPush,npc.ebData)==engine.nil)return null;
                // An open Talk channel (as NativeInteractionProbe reads it) keeps the ordinary talk readiness.
                if(engine.GetIP(npc.sid,EventEngine.tagTalk,npc.ebData)!=engine.nil && (npc.flags&EventEngine.flagTalkInh)==0 &&
                    (NativeFieldTalkability.IsTalkable(npc) || NativeFieldTargets.IsReviewedListener(context,engine,npc)))return null;
                float distance;bool selected=ReferenceEquals(player.fieldMapActorController.walkMesh.Collision(player.fieldMapActorController,2,out distance),npc);
                bool permitted=npc.level>1 && EventCollision.NavigationCanPushNpc(npc) &&
                    !((player.flags&FieldInteractionFlags.CollisionInhibitOther)!=0 && (npc.flags&FieldInteractionFlags.CollisionInhibitControl)!=0);
                var result=FieldInteractionRules.PushReadiness(selected,distance<0,permitted,EIcon.NavigationPresentedUid==npc.uid);
                result.Evidence="Native push contact selection, request level and the bubble this actor presents; Confirm remains player controlled";
                return result;
            }catch(Exception){return null;}
        }
        private static bool TryBind(NavigationContext context,EventEngine engine,Actor player,Actor actor,out Quad region,out FieldInteractionBinding binding)
        {
            region=null;binding=null;int map=FF9StateSystem.Common.FF9.fldMapNo;
            if(context==null || engine==null || player==null || actor==null || actor.ebData==null || engine.gMode!=1 || !FieldInteractionBindings.HasField(map) || !context.LogicalMapKey.StartsWith("field:"+map+":"))return false;
            binding=FieldInteractionBindings.FindActor(map,actor.sid,Hash(actor.ebData),actor.model,actor.uid);
            if(binding==null)return false;
            int observedIdentity;
            var live=new FieldInteractionPresence{ActorRendered=Rendered(actor),ActorLevel=actor.level,
                ActorObserved=_observedActors.TryGetValue(actor,out observedIdentity) && actor.go!=null && observedIdentity==actor.go.GetInstanceID(),
                ActorPosition=actor.fieldMapActorController==null?new NavigationPoint(actor.pos[0],actor.pos[1],actor.pos[2]):Point(actor.fieldMapActorController.curPos)};
            Quad found=null;
            for(var list=engine.GetActiveObjList();list!=null;list=list.next) {
                var quad=list.obj as Quad;
                if(quad==null || quad.cid!=3 || quad.sid!=binding.RegionSid || quad.ebData==null || quad.q==null || quad.n<3 || quad.n>quad.q.Length || Hash(quad.ebData)!=binding.RegionHash)continue;
                live.LiveRegions++;found=quad;
            }
            if(live.LiveRegions==1) {
                // Native Confirm permission of the talk spot itself. The facing arc (IsQuadTalkable) is
                // readiness only: a person across a counter is not dropped while the player looks away.
                live.Region=Polygon(found);live.RegionLevel=found.level;live.RegionTalkable=NativeFieldTalkability.IsTalkable(found);
                live.MapTalkPermitted=FieldInteractionBindings.QuadTalkPermitted(map,found.sid);
            }
            if(!FieldInteractionBindings.Present(binding,live))return false;
            live.RegionGuard=Guard(engine,found,EventEngine.tagTalk);
            if(!FieldInteractionBindings.Present(binding,live))return false;
            region=found;return true;
        }
        private static void ApplyGates(NavigationContext context,EventEngine engine,Actor player,Actor actor,FieldTargetObservation observation)
        {
            var gates=new FieldInteractionGates{
                HasTalkHandler=engine.GetIP(actor.sid,EventEngine.tagTalk,actor.ebData)!=engine.nil,
                HasPushHandler=engine.GetIP(actor.sid,EventEngine.tagPush,actor.ebData)!=engine.nil,
                Level=actor.level,Flags=actor.flags,PlayerFlags=player==null?0:player.flags};
            if(!gates.HasTalkHandler && !gates.HasPushHandler)return;
            if(gates.HasTalkHandler) {
                gates.TalkBytesActive=NativeFieldTalkability.IsTalkable(actor);
                gates.ReviewedListener=!gates.TalkBytesActive && NativeFieldTargets.IsReviewedListener(context,engine,actor);
                gates.MapTalkAllowed=NativeInteractionProbe.NativeTalkGuard(engine,actor);
            }
            if(gates.HasPushHandler)gates.MapPushAllowed=EventCollision.NavigationCanPushNpc(actor);
            var talk=FieldInteractionRules.Talk(gates,FieldInteractionState.Unknown);
            if(talk==FieldInteractionState.Unknown && !gates.ReviewedListener)talk=FieldInteractionRules.Talk(gates,Guard(engine,actor,EventEngine.tagTalk));
            var push=FieldInteractionRules.Push(gates,FieldInteractionState.Unknown);
            if(push==FieldInteractionState.Unknown)push=FieldInteractionRules.Push(gates,Guard(engine,actor,EventEngine.tagPush));
            bool? interactive=FieldInteractionRules.Interactive(talk,push);
            if(!interactive.HasValue)return;
            observation.HasInteraction=interactive.Value;
            if(!interactive.Value) {
                observation.UseViewingPoint=true;
                Note(context,actor,"native interaction unavailable (talk "+talk+", push "+push+", level "+gates.Level+", flags 0x"+gates.Flags.ToString("X2")+")");
            }
        }
        // The detached Confirm guard of a background object region (NativeFieldBackgroundObjects).
        internal static FieldInteractionState QuadTalkGuard(NavigationContext context,EventEngine engine,Quad quad)
        {
            try {Reset(context);return quad==null || quad.ebData==null?FieldInteractionState.Unknown:Guard(engine,quad,EventEngine.tagTalk);}
            catch(Exception){return FieldInteractionState.Unknown;}
        }
        private static FieldInteractionState Guard(EventEngine engine,Obj obj,int tag)
        {
            if(_snapshotFrame!=Time.frameCount){_guards.Clear();_snapshotFrame=Time.frameCount;_frameSnapshot=null;}
            Dictionary<int,FieldInteractionState> states;FieldInteractionState cached;
            if(!_guards.TryGetValue(obj,out states)){states=new Dictionary<int,FieldInteractionState>();_guards[obj]=states;}
            if(states.TryGetValue(tag,out cached))return cached;
            var code=Decode(engine,obj,tag);if(code.Count==0)return FieldInteractionState.Unknown;
            // The evaluator stops before every assignment. Read-only global/map
            // copies can therefore be shared within one observed game frame.
            if(_frameSnapshot==null)_frameSnapshot=new ScriptSnapshot(FF9StateSystem.EventState.gEventGlobal,engine.GetMapVar(),null){UserControl=true};
            var snapshot=_frameSnapshot.WithInstance(ScriptSnapshot.InstanceBytes(obj.buffer,obj.vofs));
            cached=FieldInteractionGuard.Evaluate(code,snapshot,_opcodes);states[tag]=cached;return cached;
        }
        private static void Reset(NavigationContext context)
        {
            if(_generation==context.Generation)return;
            _generation=context.Generation;_hashes.Clear();_decoded.Clear();_logs.Clear();_observedActors.Clear();_guards.Clear();_frameSnapshot=null;_snapshotFrame=-1;
        }
        private static bool Rendered(Actor actor)
        {
            if((actor.flags&EventEngine.flagShow)==0 || !actor.isEnableRenderer || actor.go==null || !actor.go.activeInHierarchy)return false;
            foreach(Renderer renderer in actor.go.GetComponentsInChildren<Renderer>())if(renderer.enabled)return true;
            return false;
        }
        private static NavigationPoint[] Polygon(Quad quad)
        {
            var points=new NavigationPoint[quad.n];for(int i=0;i<quad.n;i++)points[i]=Point(quad.q[i].Vector3Val);return points;
        }
        private static bool PlayerOnScreen(FieldMap field,NavigationContext context)
        {
            if(field==null)return false;var camera=field.GetMainCamera();var bg=field.GetCurrentBgCamera();if(camera==null || bg==null)return false;
            Vector3 projected=PSX.CalculateGTE_RTPT(new Vector3(context.Position.X,context.Position.Y,context.Position.Z),Matrix4x4.identity,bg.GetMatrixRT(),bg.GetViewDistance(),field.GetProjectionOffset());
            projected=camera.WorldToScreenPoint(projected);
            return projected.x>=-24 && projected.x<=Screen.width+24 && projected.y>=-24 && projected.y<=Screen.height+24;
        }
        private static void Note(NavigationContext context,Actor actor,string message)
        {
            string key=actor.uid+":"+message;if(_logs.Count>=64 || !_logs.Add(key))return;
            Log.Message("[FF9Access] Field interaction: map={0}; uid={1}; sid={2}; model={3}; {4}",context.LogicalMapKey,actor.uid,actor.sid,actor.model,message);
        }
        // Bounded, cached decoding of one native function (same reader as the event engine).
        private static List<ScriptInstruction> Decode(EventEngine engine,Obj obj,int tag)
        {
            var result=new List<ScriptInstruction>();if(obj==null || obj.ebData==null)return result;
            int start=engine.GetIP(obj.sid,tag,obj.ebData);if(start==engine.nil || start<0)return result;
            Dictionary<int,List<ScriptInstruction>> functions;List<ScriptInstruction> cached;
            if(!_decoded.TryGetValue(obj.ebData,out functions)){functions=new Dictionary<int,List<ScriptInstruction>>();_decoded[obj.ebData]=functions;}
            if(functions.TryGetValue(start,out cached))return cached;
            int end=obj.ebData.Length;int count=obj.ebData.Length>1?obj.ebData[1]:0;
            for(int i=0;i<count;i++){int p=2+4*i;if(p+3>=obj.ebData.Length)break;int offset=2+obj.ebData[p+2]+(obj.ebData[p+3]<<8);if(offset>start && offset<end)end=offset;}
            try {
                uint cursor=(uint)start;
                while(cursor<end && result.Count<1024) {
                    int offset=(int)cursor;var code=new Code(obj.ebData,ref cursor);if(cursor>end){result.Clear();break;}
                    var item=new ScriptInstruction(offset,(int)cursor,code.opcode){Arguments=new int?[code.arguments.Count],ArgumentExpressions=new byte[code.arguments.Count][]};
                    for(int argument=0;argument<code.arguments.Count;argument++) {
                        var constant=code.arguments[argument] as Code.ArgumentConstant;if(constant!=null)item.Arguments[argument]=(int)constant.value;
                        var expression=code.arguments[argument] as Code.ArgumentExpression;
                        if(expression!=null){var bytes=new List<byte>();foreach(var operation in expression.operations){bytes.Add(operation.operation);if(operation.arguments!=null)bytes.AddRange(operation.arguments);}item.ArgumentExpressions[argument]=bytes.ToArray();}
                    }
                    item.Argument=item.Constant(0);item.Expression=item.ArgumentExpression(0);result.Add(item);
                }
            }catch(Exception){result.Clear();}
            functions[start]=result;return result;
        }
        private static string Hash(byte[] bytes)
        {
            string hash;if(_hashes.TryGetValue(bytes,out hash))return hash;
            using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            _hashes[bytes]=hash;return hash;
        }
        private static NavigationPoint Point(Vector3 value){return new NavigationPoint(value.x,value.y,value.z);}
    }
}
