using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using Memoria.Accessibility.Navigation;
namespace Memoria.Accessibility.Navigation
{
    // Synthetic rows: same evidence kinds as the generated table, hashed from the synthetic scripts below.
    public static partial class FieldInteractionBindings
    {
        private static readonly FieldInteractionBinding[] _bindings={
            new FieldInteractionBinding(903,6,Fixtures.Sha(Fixtures.Clerk),11,6,12,Fixtures.Sha(Fixtures.Counter),1,16,FieldInteractionEvidence.Native),
            new FieldInteractionBinding(2102,2,Fixtures.Sha(Fixtures.Carpenter),103,2,7,Fixtures.Sha(Fixtures.Scaffold),4,19,FieldInteractionEvidence.Script),
            new FieldInteractionBinding(2108,4,Fixtures.Sha(Fixtures.Clerk),5478,4,7,Fixtures.Sha(Fixtures.Counter),1,16,FieldInteractionEvidence.Native),
            new FieldInteractionBinding(905,6,Fixtures.Sha(Fixtures.ClerkWithOwnTalk),11,6,12,Fixtures.Sha(Fixtures.Counter),1,16,FieldInteractionEvidence.Native),
        };
    }
}
internal static class Fixtures
{
    private static readonly byte[] Available={0x22,0x22,0x22,0x22,0x22,0x22,0x22,0x22,0x22,0x04};
    public static readonly byte[] Counter=Script(1,F(0,0x04),F(3,Available));
    public static readonly byte[] Scaffold=Script(1,F(0,0x04),F(3,0x22,0x22,0x22,0x22,0x22,0x22,0x22,0x22,0x22,0x22,0x04));
    public static readonly byte[] ReturnsOnly=Script(1,F(0,0x04),F(3,0x22,0x22,0x22,0x22,0x22,0x22,0x22,0x04,0x04,0x04));
    public static readonly byte[] GuardUnavailable=Script(1,F(0,0x04),F(3,0x04,0x22,0x22,0x22,0x22,0x22,0x22,0x22,0x22,0x04));
    public static readonly byte[] Clerk=Script(2,F(0,0x04),F(16,Available));
    public static readonly byte[] Carpenter=Script(2,F(0,0x04),F(19,Available));
    public static readonly byte[] ClerkWithOwnTalk=Script(2,F(0,0x04),F(3,Available),F(16,Available));
    public static readonly byte[] PushedChest=Script(2,F(0,0x04),F(2,Available));
    public static readonly byte[] ListenerWithPush=Script(2,F(0,0x04),F(2,Available),F(3,0x04));
    public static readonly byte[] InhibitedTalkWithPush=Script(2,F(0,0x04),F(2,Available),F(3,Available));
    public static KeyValuePair<int,byte[]> F(int tag,params byte[] body){return new KeyValuePair<int,byte[]>(tag,body);}
    public static byte[] Script(byte type,params KeyValuePair<int,byte[]>[] functions)
    {
        var bytes=new List<byte>{type,(byte)functions.Length};int cursor=2+4*functions.Length;
        foreach(var f in functions){int offset=cursor-2;bytes.Add((byte)f.Key);bytes.Add((byte)(f.Key>>8));bytes.Add((byte)offset);bytes.Add((byte)(offset>>8));cursor+=f.Value.Length;}
        foreach(var f in functions)bytes.AddRange(f.Value);
        return bytes.ToArray();
    }
    public static string Sha(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
}
internal static class Program
{
    private static int _generation=100;
    private sealed class Scene
    {
        public EventEngine Engine=new EventEngine();public Actor Player=new Actor{uid=250,sid=1};public Actor Speaker;public Quad Region;
        public NavigationContext Context;public FieldMap Field=new FieldMap();
    }
    private static Scene Counter(int field=903,byte sid=6,ushort model=11,byte[] actorScript=null,byte[] regionScript=null,byte regionSid=12,float actorDistance=50)
    {
        var scene=new Scene();FF9StateSystem.Common.FF9.fldMapNo=(short)field;Time.frameCount++;FF9TextTool.FieldZoneId++;EventCollision.Facing=true;EIcon.NavigationPresentedUid=-1;
        scene.Context=new NavigationContext{LogicalMapKey="field:"+field+":TEST",Generation=++_generation,SaveIdentity="save",PlayerIdentity=250,Position=new NavigationPoint(100,0,-50),CanControl=true};
        scene.Speaker=new Actor{uid=sid,sid=sid,model=model,ebData=actorScript??Fixtures.Clerk};
        scene.Speaker.fieldMapActorController.curPos=new Vector3(100,0,100+actorDistance);
        var corners=new[]{new Vector3(0,0,0),new Vector3(200,0,0),new Vector3(200,0,100),new Vector3(0,0,100)};
        scene.Region=new Quad{uid=regionSid,sid=regionSid,cid=3,n=4,q=corners.Select(c=>new QuadPos{Vector3Val=c}).ToArray(),ebData=regionScript??Fixtures.Counter};
        scene.Engine.Control=scene.Player;scene.Engine.Active.AddRange(new Obj[]{scene.Player,scene.Speaker,scene.Region});
        return scene;
    }
    private static FieldTargetObservation Observe(Scene scene,bool person=true)
    {
        var observation=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,scene.Context.LogicalMapKey,scene.Speaker.uid),Visible=true,IsPerson=person,
            NativePresentationIdentity=scene.Speaker.go.GetInstanceID(),Position=new NavigationPoint(0,0,0),VisibleLabel=person?"Person appearance":null};
        NativeFieldActorInteractions.Annotate(scene.Context,scene.Engine,scene.Player,scene.Speaker,scene.Field,observation);
        return observation;
    }
    private static bool Bound(Scene scene,FieldTargetObservation observation){return observation.InteractionUid==scene.Region.uid && observation.HasInteraction && observation.InteractionRegion!=null && observation.InteractionRegion.Length==4;}
    private static ScriptInstruction Message(int text,int next,int? talker=null)
    {
        var instruction=new ScriptInstruction(next-1,next,(int)(talker.HasValue?EBin.event_code_binary.MESA:EBin.event_code_binary.MES));
        instruction.Arguments=talker.HasValue?new int?[]{talker,0,128,text}:new int?[]{0,128,text};instruction.Argument=instruction.Constant(0);return instruction;
    }
    private static void Speech(byte[] script,int tag,int text,string heading,int? talker=null)
    {
        NativeFieldTargets.Codes[Convert.ToBase64String(script)+":"+tag]=new List<ScriptInstruction>{Message(text,script.Length,talker)};
        FF9TextTool.Texts[text]=heading+"\n“Hello.”";
    }
    private static int Main()
    {
        var cases=new List<KeyValuePair<string,Action>>{
            Case("counter speaker stays bound while the player faces away",()=>{var scene=Counter();EventCollision.Facing=false;var seen=Observe(scene);Need(Bound(scene,seen),"facing away dropped the native counter binding");
                Need(NativeFieldActorInteractions.IsBoundRegion(scene.Context,scene.Engine,scene.Region),"bound counter region would be listed twice");}),
            Case("native counter proof binds a speaker far behind the counter",()=>{var scene=Counter(actorDistance:1200);Need(Bound(scene,Observe(scene)),"native counter limited by review distance");}),
            Case("script-derived pair keeps the reviewed distance limit",()=>{var scene=Counter(2102,2,103,Fixtures.Carpenter,Fixtures.Scaffold,7,600);Need(Bound(scene,Observe(scene)),"nearby scripted pair");
                scene=Counter(2102,2,103,Fixtures.Carpenter,Fixtures.Scaffold,7,800);Need(!Bound(scene,Observe(scene)),"scripted pair beyond 700 units");}),
            Case("hidden or never-presented speakers are not bound",()=>{var scene=Counter();scene.Speaker.flags=0;Need(!Bound(scene,Observe(scene)),"hidden actor bound");
                scene=Counter();var unseen=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,scene.Context.LogicalMapKey,6),Visible=false,IsPerson=true};NativeFieldActorInteractions.Annotate(scene.Context,scene.Engine,scene.Player,scene.Speaker,scene.Field,unseen);Need(unseen.InteractionUid<0,"unpresented actor bound");}),
            Case("a speaker busy at the requested level is not bound",()=>{var scene=Counter();scene.Speaker.level=1;Need(!Bound(scene,Observe(scene)),"busy actor bound");}),
            Case("a second live counter instance is not the reviewed layout",()=>{var scene=Counter();scene.Engine.Active.Add(new Quad{uid=13,sid=12,cid=3,n=4,q=scene.Region.q,ebData=Fixtures.Counter});Need(!Bound(scene,Observe(scene)),"ambiguous region bound");}),
            Case("counter regions that only return or are refused natively stay unbound",()=>{
                var scene=Counter(regionScript:Fixtures.ReturnsOnly);Need(!Bound(scene,Observe(scene)),"return-only region bound");
                scene=Counter(regionScript:Fixtures.GuardUnavailable);Need(!Bound(scene,Observe(scene)),"guard-unavailable region bound");
                scene=Counter();scene.Region.level=1;Need(!Bound(scene,Observe(scene)),"busy region bound");
                scene=Counter(2108,4,5478,Fixtures.Clerk,Fixtures.Counter,7);Need(!Bound(scene,Observe(scene)),"CheckQuadTalk-refused region bound");}),
            Case("bound role heading from the requested function labels the person",()=>{var scene=Counter();Speech(Fixtures.Clerk,16,700,"Carpenter");Need(Observe(scene).VisibleLabel=="Carpenter","requested own speech role missing");}),
            Case("a generic requested heading keeps the reviewed appearance",()=>{var scene=Counter();Speech(Fixtures.Clerk,16,706,"Man");var seen=Observe(scene);Need(Bound(scene,seen) && seen.VisibleLabel=="Person appearance","generic heading replaced the appearance");}),
            Case("the person's own Talk role keeps priority",()=>{var scene=Counter(905,actorScript:Fixtures.ClerkWithOwnTalk);Speech(Fixtures.ClerkWithOwnTalk,3,701,"Soldier");Speech(Fixtures.ClerkWithOwnTalk,16,702,"Carpenter");
                var seen=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,scene.Context.LogicalMapKey,6),Visible=true,IsPerson=true,NativePresentationIdentity=scene.Speaker.go.GetInstanceID(),VisibleLabel="Soldier"};
                NativeFieldActorInteractions.Annotate(scene.Context,scene.Engine,scene.Player,scene.Speaker,scene.Field,seen);Need(Bound(scene,seen) && seen.VisibleLabel=="Soldier","own Talk role replaced");}),
            Case("unintroduced names and foreign speakers in the requested function never label",()=>{
                var scene=Counter();Speech(Fixtures.Clerk,16,703,"Wayne");Need(Observe(scene).VisibleLabel=="Person appearance","unintroduced proper name used");
                scene=Counter();Speech(Fixtures.Clerk,16,704,"Carpenter",8);scene.Engine.Active.Add(new Actor{uid=8,sid=8});Need(Observe(scene).VisibleLabel=="Person appearance","foreign MESA speaker labelled the clerk");
                scene=Counter();Speech(Fixtures.Clerk,16,705,"Carpenter");Need(Observe(scene,false).VisibleLabel==null,"object received a person role");}),
            Case("the suppressed region's Story evidence moves to the bound person",()=>{var scene=Counter();NativeFieldTargets.StoryByUid[12]=new[]{7600};var seen=Observe(scene);NativeFieldTargets.StoryByUid.Clear();
                Need(Bound(scene,seen) && seen.StoryMilestones!=null && seen.StoryMilestones.Contains(7600),"bound region Story milestone lost");
                Need(NativeFieldTargets.AnnotatedEntries.Contains("12:3:12:True"),"region evidence not read from its own presented Confirm handler");}),
            Case("unbound actors receive no region Story evidence",()=>{var scene=Counter();scene.Speaker.flags=0;NativeFieldTargets.StoryByUid[12]=new[]{7600};var seen=Observe(scene);NativeFieldTargets.StoryByUid.Clear();
                Need(seen.StoryMilestones==null || !seen.StoryMilestones.Contains(7600),"hidden actor inherited the region's Story evidence");}),
            Case("a touched chest is ready only while it presents its own bubble",()=>{var scene=Counter();var chest=new Actor{uid=20,sid=20,model=75,ebData=Fixtures.PushedChest};scene.Engine.Active.Add(chest);
                scene.Player.fieldMapActorController.walkMesh.Pushed=chest;scene.Player.fieldMapActorController.walkMesh.Distance=-8;EIcon.NavigationPresentedUid=20;
                var ready=NativeFieldActorInteractions.ReadPushReadiness(scene.Context,scene.Engine,scene.Player,chest);Need(ready!=null && ready.SelectedCollision && ready.InRange && ready.Talkable && ready.FacingWithinArc,"presented chest bubble not ready");
                EIcon.NavigationPresentedUid=-1;var quiet=NativeFieldActorInteractions.ReadPushReadiness(scene.Context,scene.Engine,scene.Player,chest);Need(quiet!=null && quiet.InRange && !quiet.Talkable,"unpresented chest claimed ready");
                chest.level=1;EIcon.NavigationPresentedUid=20;Need(!NativeFieldActorInteractions.ReadPushReadiness(scene.Context,scene.Engine,scene.Player,chest).Talkable,"busy chest claimed ready");
                chest.level=7;scene.Player.fieldMapActorController.walkMesh.Pushed=null;Need(!NativeFieldActorInteractions.ReadPushReadiness(scene.Context,scene.Engine,scene.Player,chest).InRange,"unselected chest reported in range");}),
            Case("people with a talkable Talk keep the ordinary talk readiness",()=>{var scene=Counter();var person=new Actor{uid=21,sid=21,model=111,ebData=Fixtures.ClerkWithOwnTalk};scene.Engine.Active.Add(person);
                Need(NativeFieldActorInteractions.ReadPushReadiness(scene.Context,scene.Engine,scene.Player,person)==null,"talkable person diverted to push readiness");
                Need(NativeFieldActorInteractions.ReadPushReadiness(scene.Context,scene.Engine,scene.Player,new Actor{uid=22,sid=22,ebData=Fixtures.Clerk})==null,"actor without a push handler got push readiness");}),
            Case("a reviewed listener with a push handler keeps the ordinary talk readiness",()=>{var scene=Counter();var listener=new Actor{uid=23,sid=23,model=111,ebData=Fixtures.ListenerWithPush};scene.Engine.Active.Add(listener);
                scene.Player.fieldMapActorController.walkMesh.Pushed=listener;scene.Player.fieldMapActorController.walkMesh.Distance=-8;NativeFieldTargets.ReviewedListeners.Add(23);
                try{Need(NativeFieldActorInteractions.ReadPushReadiness(scene.Context,scene.Engine,scene.Player,listener)==null,"reviewed listener diverted to push readiness");}finally{NativeFieldTargets.ReviewedListeners.Clear();}
                Need(NativeFieldActorInteractions.ReadPushReadiness(scene.Context,scene.Engine,scene.Player,listener)!=null,"empty Talk without a reviewed listener lost its push readiness");}),
            Case("a talk-inhibited actor presenting its push bubble is ready",()=>{var scene=Counter();var prop=new Actor{uid=24,sid=24,model=75,flags=EventEngine.flagShow|EventEngine.flagTalkInh,ebData=Fixtures.InhibitedTalkWithPush};scene.Engine.Active.Add(prop);
                scene.Player.fieldMapActorController.walkMesh.Pushed=prop;scene.Player.fieldMapActorController.walkMesh.Distance=-8;EIcon.NavigationPresentedUid=24;
                var ready=NativeFieldActorInteractions.ReadPushReadiness(scene.Context,scene.Engine,scene.Player,prop);Need(ready!=null && ready.InRange && ready.Talkable,"talk-inhibited prop's presented push bubble not ready");}),
            Case("an invisible actor presenting its own bubble is an honest interaction point",()=>{var scene=Counter();var prompt=new Actor{uid=26,sid=26,ebData=Fixtures.PushedChest};scene.Engine.Active.Add(prompt);
                var seen=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,scene.Context.LogicalMapKey,26),Visible=false,ShowsIcon=true,HasInteraction=true};
                NativeFieldActorInteractions.Annotate(scene.Context,scene.Engine,scene.Player,prompt,scene.Field,seen);Need(seen.VisibleLabel=="Interaction point","presented bubble without a drawn body got no honest label");
                var drawn=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,scene.Context.LogicalMapKey,26),Visible=true,ShowsIcon=true,HasInteraction=true};
                NativeFieldActorInteractions.Annotate(scene.Context,scene.Engine,scene.Player,prompt,scene.Field,drawn);Need(drawn.VisibleLabel==null,"a drawn actor was relabelled as a prompt");}),
            Case("facing away at the counter asks to turn instead of offering nothing",()=>{var scene=Counter();Observe(scene);scene.Engine.Selected=scene.Region;EventCollision.Facing=false;
                var turn=NativeFieldActorInteractions.ReadBoundReadiness(scene.Context,scene.Engine,scene.Player,scene.Speaker,scene.Region);
                Need(turn!=null && turn.SelectedCollision && turn.InRange && turn.Talkable && !turn.FacingWithinArc,"turn-needed readiness missing");
                EventCollision.Facing=true;EIcon.NavigationPresentedUid=12;var ready=NativeFieldActorInteractions.ReadBoundReadiness(scene.Context,scene.Engine,scene.Player,scene.Speaker,scene.Region);
                Need(ready!=null && ready.Talkable && ready.FacingWithinArc,"ready counter not reported");
                scene.Engine.Selected=null;var outside=NativeFieldActorInteractions.ReadBoundReadiness(scene.Context,scene.Engine,scene.Player,scene.Speaker,scene.Region);Need(outside!=null && !outside.InRange,"outside the counter reported in range");
                Need(NativeFieldActorInteractions.ReadBoundReadiness(scene.Context,scene.Engine,scene.Player,scene.Speaker,new Quad{uid=40,sid=40,cid=3})==null,"unrelated region produced readiness");}),
        };
        int failed=0;
        foreach(var test in cases){try{test.Value();Console.WriteLine("PASS "+test.Key);}catch(Exception exception){failed++;Console.WriteLine("FAIL "+test.Key+": "+exception.Message);}}
        Console.WriteLine("ObjectInteractionIntegration: "+(cases.Count-failed)+" passed, "+failed+" failed");
        return failed==0?0:1;
    }
    private static KeyValuePair<string,Action> Case(string name,Action action){return new KeyValuePair<string,Action>(name,action);}
    private static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
}
