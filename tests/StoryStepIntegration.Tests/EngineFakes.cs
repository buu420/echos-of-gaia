using System;
using System.Collections.Generic;
namespace Memoria.Prime { public static class Log { public static readonly List<string> Messages=new List<string>(); public static void Message(string text,params object[] args){Messages.Add(String.Format(text,args));} public static void Warning(string text,params object[] args){Messages.Add(String.Format(text,args));} public static void Error(Exception error,string text="",params object[] args){Messages.Add(text);} } }
public sealed class FF9StateSystem
{
    public static readonly FakeCommon Common=new FakeCommon();
    public sealed class FakeCommon{public FakeState FF9=new FakeState();}
    public sealed class FakeState{public FakeParty party=new FakeParty();}
    public sealed class FakeParty{public FakeMember[] member=new FakeMember[0];}
    public sealed class FakeMember{public string Name;}
    public static readonly EventStateData EventState=new EventStateData();
    public sealed class EventStateData { public byte[] gEventGlobal=new byte[2048]; }
}
public class Obj { public int uid,sid,ip;public byte[] ebData=new byte[]{0,0};public byte[] buffer=new byte[64];public int vofs; }
public sealed class ObjList{public Obj obj;public ObjList next;}
public sealed class EventEngine
{
    public int nil=-1;public const int tagTalk=3;
    public int GetIP(int sid,int tag,byte[] data){return Memoria.Accessibility.Navigation.NativeFieldTargets.Codes.ContainsKey(data)?1:-1;}
    public int gMode=1;public byte[] MapVars=new byte[1024];
    public readonly Dictionary<int,Obj> Objects=new Dictionary<int,Obj>();
    public byte[] GetMapVar(){return MapVars;}
    public Obj GetObjUID(int uid){Obj obj;return Objects.TryGetValue(uid,out obj)?obj:null;}
    public ObjList GetActiveObjList(){ObjList list=null;foreach(var obj in Objects.Values)list=new ObjList{obj=obj,next=list};return list;}
}
