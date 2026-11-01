using System;
using System.Linq;
using System.Text;
using Memoria.Accessibility.Navigation;
internal static class PrerequisiteTests
{
    private sealed class Fixture
    {
        internal readonly EventEngine Engine=UnityEngine.Object.FindObjectOfType<EventEngine>();internal readonly StoryPrerequisiteField Rule;
        internal readonly CatalogResult Catalog=new CatalogResult();internal readonly NavigationContext Context;
        internal Fixture(int field,int component=0)
        {
            Rule=StoryPrerequisiteData.All.Single(r=>r.Field==field);PersistenSingleton<EventEngine>.Instance=Engine;Engine.gMode=1;Engine.fieldmap.BgiHash=Rule.Bgi;Engine.Objects.Clear();Engine.Actors.Clear();
            Engine.Objects[0]=new Obj{uid=0,sid=0,ebData=Encoding.UTF8.GetBytes(Rule.Director)};
            var player=Rule.Players.First();Engine.Controlled.sid=player.Key;Engine.Controlled.ebData=Encoding.UTF8.GetBytes(player.Value);Engine.Controlled.fieldMapActorController.activeTri=Array.IndexOf(Rule.Components,component);
            FF9StateSystem.Common.FF9.fldMapNo=field;FF9StateSystem.EventState.gEventGlobal=new byte[512];FF9StateSystem.EventState.gEventGlobal[0]=(byte)Rule.Floor;FF9StateSystem.EventState.gEventGlobal[1]=(byte)(Rule.Floor>>8);
            Context=NativeNavigationContext.Read();NativeFieldTargets.BgiReads=0;
        }
        internal void Add(int sid,int uid,TargetKind kind=TargetKind.FieldActor)
        {
            Engine.Objects[uid]=kind==TargetKind.FieldActor?(Obj)new Actor{uid=uid,sid=sid,model=102,ebData=Encoding.UTF8.GetBytes(Rule.Entries[sid])}:new Quad{uid=uid,sid=sid,ebData=Encoding.UTF8.GetBytes(Rule.Entries[sid])};
            Catalog.Targets.Add(new NavigationTarget{Id=new TargetIdentity(kind,Context.LogicalMapKey,uid),Generation=Context.Generation,Category=kind==TargetKind.FieldExit?NavigationCategory.Exits:NavigationCategory.People,Label="Shown target",IsInteractive=true,ApproachCandidates=new[]{new NavigationPoint(1,0,1)}});
        }
        internal NavigationTarget Read(){Require(NativeStoryPrerequisites.TryAppend(Context,Catalog),"prerequisite scope not handled");return Catalog.Targets.Last(t=>t.Category==NavigationCategory.Story);}
    }
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    public static void NativeFragmentUsesActualUidAndShownIdentity()
    {var f=new Fixture(1251);f.Add(8,130);Require(f.Read().PhysicalIdentity.NativeId==130,"native UID was replaced by script SID");Require(NativeFieldTargets.BgiReads==0,"fragment reader loaded geometry unnecessarily");}
    public static void NativeFossilReadsCurrentFlagsAndActualTriangle()
    {var f=new Fixture(1419,2);f.Add(5,5);f.Add(7,7,TargetKind.FieldExit);Require(f.Read().PhysicalIdentity.NativeId==5,"initial switch not selected");f=new Fixture(1419,2);f.Add(5,5);f.Add(7,7,TargetKind.FieldExit);FF9StateSystem.EventState.gEventGlobal[404]=64;Require(f.Read().PhysicalIdentity.NativeId==7,"native switch update ignored");}
    public static void NativeArrivalPlatformReadsGeometryAndKeepsTheHubExit()
    {var f=new Fixture(1411);f.Add(12,12,TargetKind.FieldExit);Require(f.Read().PhysicalIdentity.NativeId==12,"first ride arrival was replaced by reboarding");Require(NativeFieldTargets.BgiReads==1,"native platform component identity was not checked");}
    public static void BlockedPrerequisiteFramesDoNotLoadGeometryOrAssumeCompletion()
    {var f=new Fixture(1419,2);f.Add(5,5);f.Context.CanInspect=f.Context.CanControl=false;Require(f.Read().PhysicalId==null,"blocked frame fabricated a walking target");Require(NativeFieldTargets.BgiReads==0,"blocked frame loaded geometry");}
    public static void ModifiedNativeDirectorAndBgiFailClosed()
    {var f=new Fixture(1419,2);f.Add(5,5);f.Engine.fieldmap.BgiHash="modified";Require(f.Read().PhysicalId==null,"modified BGI accepted");f=new Fixture(1251);f.Add(8,130);f.Engine.Objects[0].ebData=Encoding.UTF8.GetBytes("modified");Require(f.Read().PhysicalId==null,"modified director accepted");}
}
