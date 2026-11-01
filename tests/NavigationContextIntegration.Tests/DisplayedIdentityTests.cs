using System;
using System.Linq;
using System.Reflection;
using Memoria.Accessibility.Navigation;

internal static class DisplayedIdentityTests
{
    private sealed class Fixture
    {
        internal readonly EventEngine Engine=UnityEngine.Object.FindObjectOfType<EventEngine>();
        internal readonly Actor Npc=new Actor{uid=9,model=111,sid=3,ebData=new byte[]{1,2,3}};
        internal readonly TargetIdentity Id;
        internal Fixture()
        {
            Engine.Actors.Clear();Engine.Actors[9]=Npc;Engine.gMode=1;
            FF9StateSystem.Common.FF9.fldMapNo=50;NavigationLifecycleBridge.Generation=3;NavigationSaveIdentity.Current="test-save";NavigationSaveIdentity.Identities.SwitchSave("test-save");
            Id=new TargetIdentity(TargetKind.FieldActor,"field:50:"+FF9StateSystem.Field.SceneName,9);
        }
        internal bool Named(string scope=null) { string name;return NavigationSaveIdentity.Identities.TryGetLabel(Id,scope??NativeFieldTargets.LabelSignature(Npc),out name) && name=="Doug"; }
    }
    private static object Capture(PosObj subject)
    { var method=typeof(NativeDisplayedIdentity).GetMethod("Capture");Require(method!=null,"no native speaker evidence is retained independently of the bubble tail");return method.Invoke(null,new object[]{subject}); }
    private static void Observe(object evidence)
    { var method=typeof(NativeDisplayedIdentity).GetMethods().Single(m=>m.Name=="Observe" && m.GetParameters()[0].ParameterType!=typeof(PosObj));method.Invoke(null,new object[]{evidence,"Doug\n“Good afternoon!”"}); }
    private static void Require(bool condition,string message) { if(!condition)throw new Exception(message); }
    public static void OriginalNativeSubjectSurvivesRemovedBubbleTail()
    { var f=new Fixture();object evidence=Capture(f.Npc);PosObj bubbleTail=null;Observe(evidence);Require(f.Named(),"displayed name was lost when bubble tail was removed"); }
    public static void ReusedUidCannotTakeAnOldSpeaker()
    { var f=new Fixture();object evidence=Capture(f.Npc);f.Engine.Actors[9]=new Actor{uid=9,model=111,sid=3,ebData=f.Npc.ebData};Observe(evidence);Require(!f.Named(),"reused UID inherited an old actor's displayed name"); }
    public static void ChangedModelOrScriptCannotTakeAnOldSpeaker()
    { var f=new Fixture();object evidence=Capture(f.Npc);f.Npc.model=52;Observe(evidence);Require(!f.Named(),"changed actor model inherited an old name");f=new Fixture();evidence=Capture(f.Npc);f.Npc.ebData=new byte[]{4,5,6};Observe(evidence);Require(!f.Named(),"changed entry inherited an old name"); }
    public static void DifferentMapGenerationAndSaveRejectStaleSpeaker()
    {
        var f=new Fixture();object evidence=Capture(f.Npc);NavigationLifecycleBridge.Generation++;Observe(evidence);Require(!f.Named(),"old generation speaker accepted");
        f=new Fixture();evidence=Capture(f.Npc);FF9StateSystem.Common.FF9.fldMapNo=100;Observe(evidence);Require(!f.Named(),"old map speaker accepted");
        f=new Fixture();evidence=Capture(f.Npc);NavigationSaveIdentity.Current="another save";Observe(evidence);Require(!f.Named(),"old save speaker accepted");
    }
    public static void PlayerAndUnattributedSubjectsRemainUnassigned()
    { var f=new Fixture();Observe(Capture(f.Engine.Controlled));Observe(Capture(new PosObj()));Observe(Capture(null));Require(NavigationSaveIdentity.Identities.KnownCount==0,"unattributed dialogue assigned a nearby/selected NPC"); }
}
