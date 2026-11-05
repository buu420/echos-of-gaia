using System;
using System.Collections.Generic;
using UnityEngine;
namespace Memoria.Prime { public static class Log { public static readonly List<string> Messages=new List<string>(); public static void Message(string text,params object[] args){Messages.Add(String.Format(text,args));} public static void Warning(string text,params object[] args){Messages.Add(String.Format(text,args));} public static void Error(Exception error,string text="",params object[] args){Messages.Add(text);} } }
public sealed class FF9StateSystem
{
    public static readonly FakeCommon Common=new FakeCommon();
    public sealed class FakeCommon{public FakeState FF9=new FakeState();}
    public sealed class FakeState{public Int16 fldMapNo;}
}
public static class EMinigame{public static Int32 CreateNPCID(Int32 fldNo,Int32 uid){return fldNo*1000+uid;}}
public static class EventEngineUtils{public static Dictionary<Int32,QuadCircle> QuadCircleData=new Dictionary<Int32,QuadCircle>();}
public class Obj{public Byte cid,sid,uid;public Byte[] ebData=new Byte[]{0,0};}
public class PosObj:Obj{public GameObject go=new GameObject();}
public class Quad:Obj{public Int32 n;public QuadPos[] q;}
public sealed class ObjList{public Obj obj;public ObjList next;}
public sealed class EventContext{public ObjList activeObj;}
public partial class EventEngine
{
    public Int32 nil=-1;public Int32 gMode=1;
    private EventContext _context=new EventContext();
    public void SetActive(params Obj[] objects){ObjList list=null;for(int i=objects.Length-1;i>=0;i--)list=new ObjList{obj=objects[i],next=list};_context.activeObj=list;}
    // Same tag table walk as the native EventEngine.GetIP.
    public Int32 GetIP(Int32 objID,Int32 tagID,Byte[] eventData)
    {
        if(eventData.Length==0)return this.nil;
        int count=eventData[1];
        for(int i=0;i<count;i++){int p=2+4*i;int id=eventData[p]|eventData[p+1]<<8;int offset=eventData[p+2]|eventData[p+3]<<8;if(id==tagID)return 2+offset;}
        return this.nil;
    }
}
