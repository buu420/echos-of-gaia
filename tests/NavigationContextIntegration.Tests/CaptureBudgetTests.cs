using System;
using Memoria.Accessibility;
using Memoria.Accessibility.Navigation;

internal static class CaptureBudgetTests
{
    private static NavigationContext Context(bool visible,string map="field:100")
    { return new NavigationContext{Generation=1,LogicalMapKey=map,SaveIdentity="fixture",PlayerIdentity=17,Foreground=true,CanControl=visible,CanInspect=visible,GateReason=visible?GateReason.None:GateReason.Menu}; }
    private static void Require(bool condition,string message) { if(!condition)throw new Exception(message); }
    public static void MenuDoesNotScanFieldTargets()
    {
        NativeFieldTargets.Reads=0;NativeFieldTargets.Observations.Clear();
        new AccessibleNavigationManager().ReadTargets(Context(false));
        Require(NativeFieldTargets.Reads==0,"native field targets were scanned behind the game menu");
    }
    public static void MenuDoesNotScanWorldTargetsOrGeometry()
    {
        NativeWorldTargets.Reads=NativeWorldGeometry.Reads=0;
        new WorldMapAccessibilityManager().ReadTargets(Context(false,"world:0"));
        Require(NativeWorldTargets.Reads==0 && NativeWorldGeometry.Reads==0,"world targets/geometry were captured behind the game menu");
    }
    public static void BlockedFramesDoNotEraseLastSeenPeople()
    {
        var manager=new AccessibleNavigationManager();var context=Context(true);
        NativeFieldTargets.Observations.Clear();
        var actor=new FieldTargetObservation{Id=new TargetIdentity(TargetKind.FieldActor,"field:100",9),Position=new NavigationPoint(10,0,10),IsPerson=true,Visible=true,NativePresentationIdentity=123};
        NativeFieldTargets.Observations.Add(actor);Require(manager.ReadTargets(context).Targets.Count==1,"visible person was not observed");
        NativeFieldTargets.Observations.Clear();manager.ReadTargets(Context(false));
        actor.Visible=false;actor.OutOfView=true;NativeFieldTargets.Observations.Add(actor);
        var after=manager.ReadTargets(context);Require(after.Targets.Count==1 && after.Targets[0].Evidence==VisibilityEvidence.ObservedFieldLandmark,"a menu erased a previously seen NPC");
    }
    public static void NativeMenuIsDistinctFromLoading()
    {
        var menuFlag=typeof(NavigationContext).GetField("MenuOpen");Require(menuFlag!=null,"native menu state is indistinguishable from loading");
        var ui=UnityEngine.Object.FindObjectOfType<UIManager>();ui.State=UIManager.UIState.Menu;
        Require((bool)menuFlag.GetValue(NativeNavigationContext.Read()),"open native menu not reported");
        ui.State=UIManager.UIState.FieldHUD;ui.IsLoading=true;
        Require(!(bool)menuFlag.GetValue(NativeNavigationContext.Read()),"native loading was treated as an open menu, preventing preparation");
    }
}
