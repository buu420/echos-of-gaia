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
        public static FieldInteractionState Evaluate(IList<ScriptInstruction> code,ScriptSnapshot snapshot,FieldInteractionOpcodes opcodes)
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
                    if(!ExitScriptAnalyzer.TryExpression(expression,snapshot,out value))return FieldInteractionState.Unknown;
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
                return FieldInteractionState.Available;
            }
            return FieldInteractionState.Unknown;
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
    public sealed class FieldInteractionBinding
    {
        public readonly int Field,ActorSid,Model,ActorUid,RegionSid,RequestedLevel;public readonly string ActorHash,RegionHash;
        public FieldInteractionBinding(int field,int actorSid,string actorHash,int model,int actorUid,int regionSid,string regionHash,int requestedLevel)
        {Field=field;ActorSid=actorSid;ActorHash=actorHash;Model=model;ActorUid=actorUid;RegionSid=regionSid;RegionHash=regionHash;RequestedLevel=requestedLevel;}
    }
    // Reviewed Confirm regions whose talk handler requests a dialogue function of exactly one nearby actor
    // that has no talk/push handler of its own (docs/accessibility/field-interaction-evidence.json).
    public static class FieldInteractionBindings
    {
        public const float MaxActorDistance=700f;
        private static readonly FieldInteractionBinding[] _bindings={
            new FieldInteractionBinding(111,4,"53a4e8c318190d94a390fca640b364de8198b6032cedc72b2c88aaad9d7febbb",124,4,5,"ed9f04326b1701dfa47f4d50299ef9b4f2931c24b73f38e0176c1425903e14f6",1),
            new FieldInteractionBinding(555,12,"fc9081e2f86645b54c7a939dcc9f20e1199fb306a8a615dc001f315a5fc370fd",112,12,23,"4dd2e02ed2e515fba2700b233a34b98807a8e556da842d84dc8120910e4a0de6",1),
            new FieldInteractionBinding(561,4,"dcee00bd10b7de2a80017312e0c7cf7a12ab4ad3640fa1c7c831cbb3bcc13cbe",24,4,8,"4e626287c790d533e902dad5eb96a6dcb75c419a469d418eb49e56a6f9e39deb",1),
            new FieldInteractionBinding(562,4,"ad233fce7a9a270bd2bad900bf8e7e9fa1519244aeea35e904e09ef484ccaa98",197,4,9,"9eb30b5a3d9f8150c6d59d121484ea34921b0847ddc6cbfbb32db53fdacb9a2b",1),
            new FieldInteractionBinding(806,4,"a764a6e4badfbdd1aee0970c9147841370c4e49ceedb7cd20ae07a36353d12ec",217,4,6,"51882b1adffd9d406ec5b0c3a2c2a28e654875cd44698521f4d0ed8fcd82e791",6),
            new FieldInteractionBinding(911,3,"78ea1b75f70014107fcb5120e2fc722fb113140f2218528df52428347e4cbb5f",417,3,6,"25f31187a5f6285e74bb7e23a8b0fb7fee09e4fc8e074e240af4382cf60b8a41",1),
            new FieldInteractionBinding(912,6,"1d3a69967922fafeeabc697e89b99da54a61f651e674e1b8078037227768e1ca",51,6,13,"9a2c095de654985fe97215dec8712076d1ad12f5966abe1282ac083f54ab0255",1),
            new FieldInteractionBinding(1860,7,"af296d9c0414aa7baf68d95f7898a6704301cd4278bb525c05eb69451eeda06c",123,7,8,"db67890a403ac848c856325894b3292df54a5c95b1be093198d5479268152d66",1),
            new FieldInteractionBinding(1860,9,"e87b35d781e6ae35793b56ebc6228fa09b7836f95e0d3b6436c6f94664b1d72b",52,9,10,"94f74d19ba33553eb4cb8c505b2fae40b2203cebd50dbb46b6b2b8b3a2cf72c2",1),
            new FieldInteractionBinding(1911,3,"7c8b87633058fc2d31eb2bcf30de4779fd6e07d6ca314290fb926455914cc32c",417,3,6,"7f3d0f5b731e9dd31f03f8822cdd76980e2f1411cc3005e236f450690e246eb4",1),
            new FieldInteractionBinding(1912,6,"1224ec71dc66441a0b8be17cf00d36a87d5272672f7ce78d56a719879c10245f",51,6,14,"d0b3da4c4293d82ecc1fadcb1cd289ddb01c22cbf2305b722e9a777dc76962d1",1),
            new FieldInteractionBinding(2303,2,"65375299f3a4eb704ecd2406d43a7979d233a703a764797d8b1c058aa487cd5c",597,2,11,"ede742dc6f5fbfb631040b2e531c68f6709815a299881955bfa45661b35f98dc",1),
            new FieldInteractionBinding(2455,4,"7e67469032218c8ec817e034e68aad4a87a5be2d696cc554cf3d5545058a5818",124,4,5,"d20191fbb581c6bd18d00eac74aa534bddb7f60052b387c47b0012e40a5c8323",1),
            new FieldInteractionBinding(2802,14,"7fe337f2a656729e9c360920321fe83eb7cc92138abc1334a60a68ef2226b30a",21,14,24,"c857ff5871878049e4a624835996b3a203be510f06ff451ebea81637e85c9d14",2),
            new FieldInteractionBinding(2955,14,"08dfa459333b12ec9e6d4801f36c53810931892cfef4e979800bc28dcf96abe9",615,14,25,"754b41031805a917a12aad68a72f6dca8d48b503e2c697b61e8218cb41413c2c",6),
            new FieldInteractionBinding(2955,13,"d1b2f88ce140edbd5b4b8bffb3fdf5736afa7693cd1e277daf3eb3bff3917b25",615,13,26,"a732b28f4d267f25e2518245e155854b05c23de3efd90230361c29cfe4f6f14c",6),
        };
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
        public static bool HasField(int field){foreach(var b in _bindings)if(b.Field==field)return true;return false;}
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
