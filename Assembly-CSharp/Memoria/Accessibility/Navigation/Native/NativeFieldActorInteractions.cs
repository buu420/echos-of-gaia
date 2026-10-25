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
                // A reviewed binding already owns this actor's interaction (Forest Mene).
                if(observation.InteractionUid>=0)return;
                Quad region;
                if(TryBind(context,engine,player,actor,out region)) {
                    var polygon=Polygon(region);
                    observation.InteractionUid=region.uid;observation.InteractionRegion=polygon;observation.HasInteraction=true;observation.UseViewingPoint=false;
                    observation.ShowsIcon|=observation.Visible && EIcon.NavigationPresentedUid==region.uid && PlayerOnScreen(field,context);
                    Note(context,actor,"bound talk region "+region.uid+" (sid "+region.sid+")");
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
                var actor=engine.GetObjUID(binding.ActorUid) as Actor;Quad bound;
                return actor!=null && TryBind(context,engine,engine.GetControlChar() as Actor,actor,out bound) && ReferenceEquals(bound,quad);
            }catch(Exception){return false;}
        }
        // NativeInteractionProbe: the presented target's reviewed talk region.
        public static bool IsBoundPair(NavigationContext context,EventEngine engine,Actor player,Actor actor,Quad region)
        {
            try {Reset(context);Quad bound;return region!=null && TryBind(context,engine,player,actor,out bound) && ReferenceEquals(bound,region);}
            catch(Exception){return false;}
        }
        private static bool TryBind(NavigationContext context,EventEngine engine,Actor player,Actor actor,out Quad region)
        {
            region=null;int map=FF9StateSystem.Common.FF9.fldMapNo;
            if(context==null || engine==null || player==null || actor==null || actor.ebData==null || engine.gMode!=1 || !FieldInteractionBindings.HasField(map) || !context.LogicalMapKey.StartsWith("field:"+map+":"))return false;
            var binding=FieldInteractionBindings.FindActor(map,actor.sid,Hash(actor.ebData),actor.model,actor.uid);
            int observedIdentity;
            if(binding==null || !FieldInteractionBindings.CanRequest(binding,actor.level) || !_observedActors.TryGetValue(actor,out observedIdentity) || actor.go==null || observedIdentity!=actor.go.GetInstanceID() || !Rendered(actor))return false;
            for(var list=engine.GetActiveObjList();list!=null;list=list.next) {
                var quad=list.obj as Quad;
                if(quad==null || quad.cid!=3 || quad.sid!=binding.RegionSid || quad.ebData==null || quad.q==null || quad.n<3 || quad.n>quad.q.Length || Hash(quad.ebData)!=binding.RegionHash)continue;
                if(region!=null)return false; // more than one live instance: not the reviewed layout
                region=quad;
            }
            if(region==null)return false;
            var position=actor.fieldMapActorController==null?new NavigationPoint(actor.pos[0],actor.pos[1],actor.pos[2]):Point(actor.fieldMapActorController.curPos);
            if(!FieldInteractionBindings.WithinReach(position,Polygon(region))){region=null;return false;}
            // Current native Confirm permission for the talk spot itself.
            if(region.level<=1 || !engine.IsActuallyTalkable(region) || !EventCollision.NavigationCanTalkQuad(player,region) ||
                Guard(engine,region,EventEngine.tagTalk)==FieldInteractionState.Unavailable){region=null;return false;}
            return true;
        }
        private static void ApplyGates(NavigationContext context,EventEngine engine,Actor player,Actor actor,FieldTargetObservation observation)
        {
            var gates=new FieldInteractionGates{
                HasTalkHandler=engine.GetIP(actor.sid,EventEngine.tagTalk,actor.ebData)!=engine.nil,
                HasPushHandler=engine.GetIP(actor.sid,EventEngine.tagPush,actor.ebData)!=engine.nil,
                Level=actor.level,Flags=actor.flags,PlayerFlags=player==null?0:player.flags};
            if(!gates.HasTalkHandler && !gates.HasPushHandler)return;
            if(gates.HasTalkHandler) {
                gates.TalkBytesActive=engine.IsActuallyTalkable(actor);
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
