using System;
using System.Collections.Generic;
using UnityEngine;
using Memoria.Accessibility.Navigation;
namespace Memoria.Prime { public static class Log { public static readonly List<string> Messages=new List<string>(); public static void Message(string text,params object[] args){Messages.Add(String.Format(text,args));} public static void Warning(string text,params object[] args){Messages.Add(String.Format(text,args));} public static void Error(Exception error,string text="",params object[] args){Messages.Add(text);} } }
namespace Memoria.Assets { public static class Localization { public static string CurrentSymbol="US"; } public static class EmbadedTextResources { public static string CurrentSymbol; } }
namespace Assets.Sources.Scripts.UI.Common { }
public sealed class FF9StateSystem
{
    public static readonly FakeCommon Common=new FakeCommon();
    public sealed class FakeCommon{public FakeState FF9=new FakeState();}
    public sealed class FakeState{public Int16 fldMapNo;public FakeParty party=new FakeParty();}
    public sealed class FakeParty{public FakeMember[] member=new FakeMember[0];}
    public sealed class FakeMember{public string Name;}
    public static readonly EventStateData EventState=new EventStateData();
    public sealed class EventStateData { public byte[] gEventGlobal=new byte[2048]; }
}
public sealed class EBin { public enum event_code_binary { rsv04=0x04,MES=0x1F,MESN=0x20,FICON=0x68,MESA=0x95,MESAN=0x96 } }
public static class FF9TextTool
{
    public static int FieldZoneId=2;public static readonly Dictionary<int,string> Texts=new Dictionary<int,string>();
    public static string FieldText(int id){string text;return Texts.TryGetValue(id,out text)?text:null;}
    public static string RemoveOpCode(string text){return text;}
}
public static class EIcon { public static int NavigationPresentedUid=-1; }
public static class PSX { public static Vector3 CalculateGTE_RTPT(Vector3 vertex,Matrix4x4 localRTS,Matrix4x4 globalRT,float viewDist,Vector2 offset){return vertex;} }
public sealed class BGCAM_DEF { public Matrix4x4 GetMatrixRT(){return Matrix4x4.identity;} public float GetViewDistance(){return 1;} }
public sealed class FieldMap { public Camera Main=new Camera();public BGCAM_DEF Background=new BGCAM_DEF(); public Camera GetMainCamera(){return Main;} public BGCAM_DEF GetCurrentBgCamera(){return Background;} public Vector2 GetProjectionOffset(){return new Vector2();} }
public sealed class FieldMapActorController { public Vector3 curPos;public WalkMesh walkMesh=new WalkMesh(); }
// WalkMesh.Collision(controller, mode, out distance): the push (mode 2) contact selection used by CollisionRequest.
public sealed class WalkMesh { public PosObj Pushed;public float Distance=-1; public PosObj Collision(FieldMapActorController controller,int mode,out float distance){distance=Distance;return mode==2?Pushed:null;} }
public class Obj
{
    public byte uid,sid,cid,level=7,flags=1,vofs;public int ip;public byte[] ebData=new byte[]{0,0};public byte[] buffer=new byte[64];
}
public class PosObj:Obj { public float[] pos=new float[3];public GameObject go=new GameObject();public bool isEnableRenderer=true; }
public sealed class Actor:PosObj { public ushort model;public FieldMapActorController fieldMapActorController=new FieldMapActorController();public string Signature="same-actor"; }
public sealed class QuadPos { public Vector3 Vector3Val; }
public sealed class Quad:Obj { public int n;public QuadPos[] q; }
public sealed class ObjList { public Obj obj;public ObjList next; }
public static class EventCollision
{
    // The facing arc of EventCollision.IsQuadTalkable; tests turn it off to stand facing away.
    public static bool Facing=true;
    public static bool NavigationCanTalkQuad(PosObj ctrl,Obj quad){return ctrl!=null && quad!=null && quad.level>1 && Facing;}
    public static bool NavigationCanPushNpc(PosObj npc){return npc!=null;}
}
public sealed class EventEngine
{
    public const byte flagShow=1,flagTalkInh=8;public const int tagPush=2,tagTalk=3;
    public int nil=-1,gMode=1;public byte[] MapVars=new byte[1024];
    public readonly List<Obj> Active=new List<Obj>();public Obj Control;public Quad Selected;
    public int GetIP(int sid,int tag,byte[] data)
    {
        if(data==null || data.Length<2)return nil;
        int count=data[1];
        for(int i=0;i<count;i++){int p=2+4*i;if(p+3>=data.Length)return nil;int id=data[p]|data[p+1]<<8;int offset=data[p+2]|data[p+3]<<8;if(id==tag)return 2+offset;}
        return nil;
    }
    public ObjList GetActiveObjList(){ObjList list=null;for(int i=Active.Count-1;i>=0;i--)list=new ObjList{obj=Active[i],next=list};return list;}
    public Obj GetObjUID(int uid){if(uid==250)return Control;foreach(var obj in Active)if(obj.uid==uid)return obj;return null;}
    public Obj GetControlChar(){return Control;}
    public byte[] GetMapVar(){return MapVars;}
    public Obj TreadQuad(PosObj po,int mode){return Selected;}
    public bool NavigationContainsQuad(Quad quad,float x,float z){return quad!=null && quad==Selected;}
}
// Same shape as EventEngineUtils.BinaryScript.Entry.Function.Code. Synthetic scripts here use one-byte
// instructions without arguments; only the guard walk reads them.
public static class EventEngineUtils
{
    public class BinaryScript { public class Entry { public class Function { public class Code
    {
        public UInt16 opcode;public List<Argument> arguments=new List<Argument>();
        public Code(Byte[] raw,ref UInt32 pos){opcode=raw[pos++];}
        public abstract class Argument { }
        public class ArgumentConstant:Argument { public Int64 value; }
        public class ArgumentExpression:Argument { public List<ExpressionOperation> operations=new List<ExpressionOperation>(); }
        public class ExpressionOperation { public Byte operation;public Byte[] arguments; }
    } } } }
}
namespace Memoria.Accessibility.Navigation
{
    public static class NavigationSaveIdentity { public static ObservedIdentityStore Identities=new ObservedIdentityStore(); }
    public static class NativeInteractionProbe { internal static bool NativeTalkGuard(EventEngine engine,Actor npc){return true;} }
    public static partial class NativeFieldTargets
    {
        // Decoded functions for NativeFieldSpeakers, keyed by script bytes and tag.
        public static readonly Dictionary<string,List<ScriptInstruction>> Codes=new Dictionary<string,List<ScriptInstruction>>();
        // Story evidence the real NativeStoryTargets would attach to an entry's own observation.
        public static readonly Dictionary<int,int[]> StoryByUid=new Dictionary<int,int[]>();
        public static readonly List<string> AnnotatedEntries=new List<string>();
        private static List<ScriptInstruction> Decode(EventEngine engine,Obj obj,int tag){List<ScriptInstruction> code;return Codes.TryGetValue(Convert.ToBase64String(obj.ebData)+":"+tag,out code)?code:new List<ScriptInstruction>();}
        private static int FunctionEnd(byte[] data,int start){return data.Length;}
        internal static string LabelSignature(Actor actor){return actor.Signature;}
        internal static string ExitLabelSignature(Quad quad){return "region:"+quad.sid;}
        // UIDs whose empty Talk handler is a reviewed listener (the controlled actor owns the conversation).
        public static readonly HashSet<int> ReviewedListeners=new HashSet<int>();
        public static bool IsReviewedListener(NavigationContext context,EventEngine engine,Obj obj){return ReviewedListeners.Contains(obj.uid);}
        private static void Annotate(NavigationContext context,EventEngine engine,Obj obj,FieldTargetObservation observation,int onlyTag)
        {
            AnnotatedEntries.Add(obj.uid+":"+onlyTag+":"+observation.Id.NativeId+":"+(observation.Visible || observation.ShowsIcon || observation.OutOfView));
            int[] milestones;observation.StoryMilestones=StoryByUid.TryGetValue(obj.uid,out milestones)?milestones:new int[0];observation.NextFields=new int[0];
        }
    }
}
