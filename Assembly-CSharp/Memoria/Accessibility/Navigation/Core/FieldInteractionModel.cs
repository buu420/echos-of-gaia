using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace Memoria.Accessibility.Navigation
{
    // None: the actor has no handler for this channel. Unknown: the native guard could not be read,
    // so ordinary manual interaction remains. Unavailable is only produced from proven native state.
    public enum FieldInteractionState { None, Unknown, Available, Unavailable }
    public static class FieldInteractionFlags
    {
        // EventEngine.flagShow/flagCollInhC/flagCollInhNC/flagTalkInh.
        public const int Show=1, CollisionInhibitControl=2, CollisionInhibitOther=4, TalkInhibit=8;
    }
    public sealed class FieldInteractionOpcodes { public int IconOpcode; }
    // Detached copies of the live native gate inputs (EventCollision / WalkMesh.Collision / EventEngine.Request).
    public sealed class FieldInteractionGates
    {
        public bool HasTalkHandler, TalkBytesActive, ReviewedListener, MapTalkAllowed, HasPushHandler, MapPushAllowed;
        public int Level, Flags, PlayerFlags;
    }
    public static class FieldInteractionRules
    {
        // CheckNPCInput: WalkMesh.Collision(mode 4) skips flagTalkInh actors; Request needs 1 < level;
        // IsActuallyTalkable rejects a talk handler that only returns; IsNPCTalkable map rules.
        public static FieldInteractionState Talk(FieldInteractionGates gates,FieldInteractionState guard)
        {
            if(gates==null || !gates.HasTalkHandler)return FieldInteractionState.None;
            if((gates.Flags&FieldInteractionFlags.TalkInhibit)!=0 || !gates.MapTalkAllowed || gates.Level<=1 || (!gates.TalkBytesActive && !gates.ReviewedListener))return FieldInteractionState.Unavailable;
            // Some proven conversations are owned by the controlled actor's
            // script. The listener intentionally has an empty talk handler.
            if(gates.ReviewedListener)return guard==FieldInteractionState.Available?guard:FieldInteractionState.Unknown;
            return guard==FieldInteractionState.None?FieldInteractionState.Unknown:guard;
        }
        // CollisionRequest push: WalkMesh.Collision(mode 2) skips only when the controlled player carries
        // flagCollInhNC and the actor carries flagCollInhC; CheckNPCPush map rules; Request needs 1 < level.
        public static FieldInteractionState Push(FieldInteractionGates gates,FieldInteractionState guard)
        {
            if(gates==null || !gates.HasPushHandler)return FieldInteractionState.None;
            if(gates.Level<=1 || !gates.MapPushAllowed || ((gates.PlayerFlags&FieldInteractionFlags.CollisionInhibitOther)!=0 && (gates.Flags&FieldInteractionFlags.CollisionInhibitControl)!=0))return FieldInteractionState.Unavailable;
            return guard==FieldInteractionState.None?FieldInteractionState.Unknown:guard;
        }
        // Readiness of a contact (push) prop such as a chest: CollisionRequest requests its push handler while
        // the player is inside the push radius, and the handler shows its own bubble before waiting for Confirm.
        // Only that presented bubble proves the offer; without it nothing is ready and no turn is requested.
        public static InteractionReadiness PushReadiness(bool selected,bool inRange,bool permitted,bool presented)
        {return new InteractionReadiness{SelectedCollision=selected,InRange=selected && inRange,Talkable=permitted && presented,FacingWithinArc=permitted && presented};}
        // null: no native channel, leave the observation as it is.
        public static bool? Interactive(FieldInteractionState talk,FieldInteractionState push)
        {
            if(talk==FieldInteractionState.None && push==FieldInteractionState.None)return null;
            return Open(talk) || Open(push);
        }
        private static bool Open(FieldInteractionState state){return state==FieldInteractionState.Available || state==FieldInteractionState.Unknown;}
    }
    // Runs a detached handler from its entry point on private variable copies. A handler is proven
    // unavailable only when every executed step is control flow or a pure read and it returns.
    public static class FieldInteractionGuard
    {
        public static FieldInteractionState Evaluate(IList<ScriptInstruction> code,ScriptSnapshot snapshot,FieldInteractionOpcodes opcodes,bool requireIcon=false,Func<int,int,int?> objectProperty=null)
        {
            if(code==null || code.Count==0 || snapshot==null || opcodes==null)return FieldInteractionState.Unknown;
            var offsets=new Dictionary<int,ScriptInstruction>();foreach(var item in code)offsets[item.Offset]=item;
            int ip=code[0].Offset;int? value=null;
            for(int step=0;step<512;step++) {
                ScriptInstruction item;if(!offsets.TryGetValue(ip,out item))return FieldInteractionState.Unknown;
                int op=item.Opcode;
                if(op==4)return FieldInteractionState.Unavailable;
                if(op==0){ip=item.NextOffset;continue;}
                if(op==5) {
                    var expression=item.ArgumentExpression(0);if(expression==null)return FieldInteractionState.Unknown;
                    if(Assigns(expression))return FieldInteractionState.Unknown;
                    if(!ExitScriptAnalyzer.TryExpression(expression,snapshot,out value,objectProperty))return FieldInteractionState.Unknown;
                    ip=item.NextOffset;continue;
                }
                if(op>=1 && op<=3) {
                    int? argument=item.Constant(0);if(!argument.HasValue || (op!=1 && !value.HasValue))return FieldInteractionState.Unknown;
                    bool branch=op==1 || (op==2?value==0:value!=0);
                    ip=branch?item.NextOffset+(op==2?argument.Value:(short)argument.Value):item.NextOffset;continue;
                }
                if(op==6 || op==11 || op==13) {int? target=ScriptBranches.SelectSwitch(item,value);if(!target.HasValue)return FieldInteractionState.Unknown;ip=target.Value;continue;}
                // FICON(0) presents "?"; it is an interaction just like "!".
                // Any other native operation (icon, dialogue, request, movement, sound...) is an interaction.
                return !requireIcon || op==opcodes.IconOpcode?FieldInteractionState.Available:FieldInteractionState.Unknown;
            }
            return FieldInteractionState.Unknown;
        }
        public static FieldFacingGate FacingGate(IList<ScriptInstruction> code,ScriptSnapshot snapshot,FieldInteractionOpcodes opcodes,int playerUid,int heading)
        {
            var result=new FieldFacingGate();
            if(code==null || code.Count==0 || snapshot==null || opcodes==null)return result;
            Func<int,bool> offered=facing=>Evaluate(code,snapshot,opcodes,true,(uid,property)=>(uid==250 || uid==playerUid) && property==3?(int?)facing:null)==FieldInteractionState.Available;
            int current=heading&255;
            result.OfferedNow=offered(current);
            if(result.OfferedNow)return result;
            for(int facing=0;facing<256;facing++)if(facing!=current && offered(facing)){result.OfferedByTurning=true;break;}
            return result;
        }
        internal static bool Assigns(byte[] expression)
        {
            for(int p=0;p<expression.Length;) {
                int op=expression[p++];
                if((op>=4 && op<=11) || (op>=44 && op<=69))return true;
                p+=op==0x7e?4:op>=0xe0 || op==0x78 || op==0x7d?2:op>=0xc0 || op==0x29 || op==0x5f || op==0x79 || op==0x7a?1:0;
            }
            return false;
        }
    }
    // Native: EventEngineUtils.QuadTalkableData names the region's actor. NativeDirection: that entry is the
    // controlled party member (a direction-only rule), and the handler's single requested speaker is used.
    // Script: no native row; one direct request to a non-interactive speaker within MaxActorDistance.
    // PlayerExamines: the region's Confirm handler requests the controlled player's own function (REQ 250),
    // which first turns the player toward one prop (TURNA prop) and speaks about it; the prop has no handler
    // of its own and stays within MaxActorDistance. The requested level gates the player, not the prop.
    public enum FieldInteractionEvidence { Script, Native, NativeDirection, PlayerExamines }
    // Some Confirm regions offer their bubble only while the controlled player faces the object: the push
    // handler tests OBJ(250).3 (EBin.getvobj case 3, EventCollision.GetDir: 256 units per turn) before FICON.
    // OfferedNow: the bubble path is reached with the current heading. OfferedByTurning: another heading
    // reaches it, so turning is the only missing step. Neither is claimed from an unreadable guard.
    public sealed class FieldFacingGate { public bool OfferedNow, OfferedByTurning; }
    public sealed class FieldInteractionBinding
    {
        public readonly int Field,ActorSid,Model,ActorUid,RegionSid,RequestedLevel,RequestedTag;public readonly string ActorHash,RegionHash;
        public readonly FieldInteractionEvidence Evidence;
        public FieldInteractionBinding(int field,int actorSid,string actorHash,int model,int actorUid,int regionSid,string regionHash,int requestedLevel,int requestedTag,FieldInteractionEvidence evidence)
        {Field=field;ActorSid=actorSid;ActorHash=actorHash;Model=model;ActorUid=actorUid;RegionSid=regionSid;RegionHash=regionHash;RequestedLevel=requestedLevel;RequestedTag=requestedTag;Evidence=evidence;}
    }
    // Detached live facts for one reviewed pair. The player's facing is deliberately absent: it decides
    // readiness at the counter, not whether the person is the one the region talks to.
    public sealed class FieldInteractionPresence
    {
        public bool ActorRendered,ActorObserved,RegionTalkable,MapTalkPermitted;public int ActorLevel,RegionLevel,LiveRegions;
        public FieldInteractionState RegionGuard=FieldInteractionState.Unknown;public NavigationPoint ActorPosition;public NavigationPoint[] Region;
    }
    // Reviewed Confirm regions whose talk handler requests the dialogue/service function of exactly one actor
    // (docs/accessibility/field-interaction-evidence.json, generated rows in FieldInteractionBindingRows.cs).
    public static partial class FieldInteractionBindings
    {
        public const float MaxActorDistance=700f;
        public static IList<FieldInteractionBinding> All{get{return new ReadOnlyCollection<FieldInteractionBinding>(_bindings);}}
        public static FieldInteractionBinding FindActor(int field,int actorSid,string actorHash,int model,int actorUid)
        {
            foreach(var b in _bindings)if(b.Field==field && b.ActorSid==actorSid && b.ActorHash==actorHash && b.Model==model && b.ActorUid==actorUid)return b;
            return null;
        }
        public static FieldInteractionBinding FindRegion(int field,int regionSid,string regionHash)
        {
            foreach(var b in _bindings)if(b.Field==field && b.RegionSid==regionSid && b.RegionHash==regionHash)return b;
            return null;
        }
        public static bool CanRequest(FieldInteractionBinding binding,int actorLevel){return binding!=null && binding.RequestedLevel<actorLevel;}
        // The handler's request targets uid 250, the controlled player, whose level therefore gates it.
        public static bool RequestedObjectIsPlayer(FieldInteractionBinding binding){return binding!=null && binding.Evidence==FieldInteractionEvidence.PlayerExamines;}
        public static bool HasField(int field){foreach(var b in _bindings)if(b.Field==field)return true;return false;}
        // The native table proves the pair wherever the person stands behind the counter; only the
        // script-derived rows (and an examined prop) keep the reviewed counter-scale distance.
        public static bool WithinReach(FieldInteractionBinding binding,NavigationPoint actor,NavigationPoint[] polygon)
        {
            if(binding==null)return false;
            if(binding.Evidence==FieldInteractionEvidence.Script || binding.Evidence==FieldInteractionEvidence.PlayerExamines)return WithinReach(actor,polygon);
            if(!actor.IsFinite || polygon==null || polygon.Length<3)return false;
            foreach(var point in polygon)if(!point.IsFinite)return false;
            return true;
        }
        // The region still talks to this actor: rendered/observed instance, request level, one live region,
        // native talk bytes/level/map rule and a guard not proven unavailable.
        public static bool Present(FieldInteractionBinding binding,FieldInteractionPresence live)
        {
            return binding!=null && live!=null && live.ActorRendered && live.ActorObserved && CanRequest(binding,live.ActorLevel) && live.LiveRegions==1 &&
                WithinReach(binding,live.ActorPosition,live.Region) && live.RegionLevel>1 && live.RegionTalkable && live.MapTalkPermitted && live.RegionGuard!=FieldInteractionState.Unavailable;
        }
        // EventCollision.CheckQuadTalk: these two native regions never accept Confirm.
        public static bool QuadTalkPermitted(int field,int regionSid){return !(field==2108 && regionSid==7) && !(field==2504 && regionSid==9);}
        // Readiness at the counter. permitted: talk bytes, level and map rule without facing. facing: the native
        // IsQuadTalkable arc. When only facing is missing the player is asked to turn; when facing is allowed the
        // presented bubble must confirm that the game offers the interaction now.
        public static InteractionReadiness Readiness(bool selected,bool contains,bool permitted,bool facing,bool presented)
        {return new InteractionReadiness{SelectedCollision=selected,InRange=selected && contains,Talkable=permitted && (!facing || presented),FacingWithinArc=permitted && facing && presented};}
        // The native follower stops anywhere within its arrival radius of the approach point, possibly
        // past a region edge. Plan inside the region inset by that radius so the stop still lies inside
        // the native polygon (EventEngine.IsInQuad). Concave, thin or degenerate regions keep their
        // native polygon. The native quad itself is never changed; readiness reads the live quad.
        public static NavigationPoint[] StoppingRegion(NavigationPoint[] polygon,float margin)
        {
            if(polygon==null || polygon.Length<3 || polygon.Length>16 || float.IsNaN(margin) || float.IsInfinity(margin) || margin<=0)return polygon;
            int n=polygon.Length;float winding=0;
            for(int i=0;i<n;i++) {
                if(!polygon[i].IsFinite)return polygon;
                float cross=FieldTriangle.Cross(polygon[i],polygon[(i+1)%n],polygon[(i+2)%n]);
                if(Math.Abs(cross)<.001f || winding*cross<0)return polygon;
                if(winding==0)winding=cross;
            }
            var result=new NavigationPoint[n];
            for(int i=0;i<n;i++) {
                var previous=polygon[(i+n-1)%n];var vertex=polygon[i];var next=polygon[(i+1)%n];
                double ax,az,bx,bz;
                if(!Inward(previous,vertex,winding,margin,out ax,out az) || !Inward(vertex,next,winding,margin,out bx,out bz))return polygon;
                // The inset vertex is where the two inward-shifted edge lines meet.
                double dx1=vertex.X-previous.X,dz1=vertex.Z-previous.Z,dx2=next.X-vertex.X,dz2=next.Z-vertex.Z;
                double px=previous.X+ax,pz=previous.Z+az,qx=vertex.X+bx,qz=vertex.Z+bz;
                double denominator=dx1*dz2-dz1*dx2;if(Math.Abs(denominator)<1e-6)return polygon;
                double t=((qx-px)*dz2-(qz-pz)*dx2)/denominator;
                result[i]=new NavigationPoint((float)(px+dx1*t),vertex.Y,(float)(pz+dz1*t));
            }
            for(int i=0;i<n;i++) {
                float cross=FieldTriangle.Cross(result[i],result[(i+1)%n],result[(i+2)%n]);
                if(cross*winding<=0 || !result[i].IsFinite || !FieldRegionApproaches.CanStopInside(polygon,result[i],margin*.99f))return polygon;
            }
            return result;
        }
        // Offset of edge a->b toward the polygon interior: FieldTriangle.Cross(a,b,c)>0 puts c on the
        // (-dz,dx) side, and the interior lies on the side of the polygon's winding sign.
        private static bool Inward(NavigationPoint a,NavigationPoint b,float winding,float margin,out double x,out double z)
        {
            double dx=b.X-a.X,dz=b.Z-a.Z,length=Math.Sqrt(dx*dx+dz*dz);x=z=0;if(length<1e-6)return false;
            double sign=winding>0?1:-1;x=-dz/length*margin*sign;z=dx/length*margin*sign;return true;
        }
        // XZ distance from the actor to the native region polygon (0 inside), as in the reviewed table.
        public static bool WithinReach(NavigationPoint actor,NavigationPoint[] polygon)
        {
            if(!actor.IsFinite || polygon==null || polygon.Length<3)return false;
            bool inside=false;float best=float.MaxValue;
            for(int i=0;i<polygon.Length;i++) {
                var a=polygon[i];var b=polygon[(i+1)%polygon.Length];if(!a.IsFinite || !b.IsFinite)return false;
                if((a.Z>actor.Z)!=(b.Z>actor.Z) && actor.X<(b.X-a.X)*(actor.Z-a.Z)/(b.Z-a.Z)+a.X)inside=!inside;
                float dx=b.X-a.X,dz=b.Z-a.Z,length=dx*dx+dz*dz;
                float t=length<=0?0:Math.Max(0,Math.Min(1,((actor.X-a.X)*dx+(actor.Z-a.Z)*dz)/length));
                float x=actor.X-(a.X+t*dx),z=actor.Z-(a.Z+t*dz);best=Math.Min(best,(float)Math.Sqrt(x*x+z*z));
            }
            return inside || best<=MaxActorDistance;
        }
    }
}
