using System;
using System.Linq;
using Memoria.Accessibility.Navigation;

internal static class Program
{
    private static FF9StateSystem state;
    private static EventEngine engine;
    private static UIManager ui;
    private static DialogManager dialogs;
    private static int Main()
    {
        Action[] tests={WalkingWithCandleHintAcceptsAllNavigationKeys,WalkingWithDismissibleHintAcceptsNavigation,
            ChoiceKeepsNavigationBlocked,NativeMovementPermissionsStillGateNavigation,
            TutorialPauseLoadingAndFocusKeepNavigationBlocked,HeldTutorialKeyNeedsReleaseBeforeNavigation,
            WorldHintUsesTheSameNativeControlRule,BattleAndUnknownActorStayBlocked};
        int failed=0;
        foreach(var test in tests)try { Reset();test();Console.WriteLine("PASS "+test.Method.Name); }
        catch(Exception error) { failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+error.Message); }
        Console.WriteLine("NavigationContextIntegration: "+(tests.Length-failed)+" passed, "+failed+" failed");
        return failed==0?0:1;
    }
    private static void Reset()
    {
        state=new FF9StateSystem{mode=1};engine=new EventEngine{Controlled=new Actor(),UserControl=true};
        ui=new UIManager{State=UIManager.UIState.FieldHUD,IsPlayerControlEnable=true};dialogs=new DialogManager();
        UnityEngine.Object.Set(state);UnityEngine.Object.Set(engine);UnityEngine.Object.Set(ui);UnityEngine.Object.Set(dialogs);
        HonoInputManager.Active=true;EventInput.IsMovementControl=true;EventHUD.CurrentHUD=MinigameHUD.None;
        FF9StateSystem.Field.FF9Field.loc.map.nextMode=0;FF9StateSystem.World.FF9World.map.nextMode=0;
    }
    private static void WalkingWithCandleHintAcceptsAllNavigationKeys()
    {
        dialogs.ActiveDialogList.Add(new Dialog{FlagButtonInh=true});
        var context=NativeNavigationContext.Read();
        Require(context.CanControl && context.CanInspect,"native walking is available, but the visible hint disabled navigation: "+context.GateReason);
        var bindings=NavigationBindings.Parse(null).Bindings;var broker=new NavigationInputBroker(bindings);
        broker.Update(context,new PhysicalInputSnapshot());
        foreach(var binding in bindings.All) {
            var physical=new PhysicalInputSnapshot{Shift=binding.Gesture.StartsWith("Shift+")};physical.KeysHeld.Add(binding.Key);
            Require(broker.Update(context,physical).Contains(binding.Command),"no command for "+binding.Gesture);
            broker.Update(context,new PhysicalInputSnapshot());
        }
    }
    private static void WalkingWithDismissibleHintAcceptsNavigation()
    {
        dialogs.ActiveDialogList.Add(new Dialog{FlagButtonInh=false});
        var context=NativeNavigationContext.Read();
        Require(context.CanControl && context.CanInspect,"a dismissible prompt cannot override the game's walking permissions");
        Require(Press(context,"I").Contains(NavigationCommand.ToggleGuidance),"guidance key suppressed");
        Require(Press(context,"P").Contains(NavigationCommand.ToggleAutomatic),"automatic key suppressed");
    }
    private static void ChoiceKeepsNavigationBlocked()
    {
        dialogs.ActiveDialogList.Add(new Dialog{HasChoices=true,FlagButtonInh=true});
        var context=NativeNavigationContext.Read();
        Require(!context.CanControl && !context.CanInspect && context.GateReason==GateReason.Dialogue,"a choice must keep input ownership");
        Require(Press(context,"P").Length==0,"automatic walking started while choosing");
    }
    private static void NativeMovementPermissionsStillGateNavigation()
    {
        dialogs.ActiveDialogList.Add(new Dialog{FlagButtonInh=true});
        engine.UserControl=false;RequireBlocked("event control");engine.UserControl=true;
        EventInput.IsMovementControl=false;RequireBlocked("movement mask");EventInput.IsMovementControl=true;
        ui.IsPlayerControlEnable=false;RequireBlocked("UI player control");
        dialogs.ActiveDialogList.Clear();RequireBlocked("native control without a dialog");
    }
    private static void TutorialPauseLoadingAndFocusKeepNavigationBlocked()
    {
        dialogs.ActiveDialogList.Add(new Dialog{FlagButtonInh=true});
        ui.State=UIManager.UIState.Tutorial;RequireBlocked("tutorial");ui.State=UIManager.UIState.FieldHUD;
        ui.IsPause=true;RequireBlocked("pause");ui.IsPause=false;
        ui.IsLoading=true;RequireBlocked("loading");ui.IsLoading=false;
        HonoInputManager.Active=false;RequireBlocked("background");
    }
    private static void HeldTutorialKeyNeedsReleaseBeforeNavigation()
    {
        dialogs.ActiveDialogList.Add(new Dialog{FlagButtonInh=true});
        var broker=new NavigationInputBroker(NavigationBindings.Parse(null).Bindings);
        var held=new PhysicalInputSnapshot();held.KeysHeld.Add("P");
        ui.State=UIManager.UIState.Tutorial;Require(!broker.Update(NativeNavigationContext.Read(),held).Contains(NavigationCommand.ToggleAutomatic),"tutorial started walking");
        ui.State=UIManager.UIState.FieldHUD;
        Require(!broker.Update(NativeNavigationContext.Read(),held).Contains(NavigationCommand.ToggleAutomatic),"held tutorial key replayed");
        broker.Update(NativeNavigationContext.Read(),new PhysicalInputSnapshot());
        Require(broker.Update(NativeNavigationContext.Read(),held).Contains(NavigationCommand.ToggleAutomatic),"fresh automatic key failed after tutorial");
    }
    private static void WorldHintUsesTheSameNativeControlRule()
    {
        state.mode=3;ui.State=UIManager.UIState.WorldHUD;
        var actor=engine.Controlled;actor.wmActor=new WMActor{originalActor=actor};ff9.w_moveActorPtr=actor.wmActor;
        dialogs.ActiveDialogList.Add(new Dialog{FlagButtonInh=true});
        Require(NativeNavigationContext.Read().CanControl,"world walking was blocked by a hint");
        dialogs.ActiveDialogList.Add(new Dialog{HasChoices=true});RequireBlocked("world choice");
    }
    private static void BattleAndUnknownActorStayBlocked()
    {
        engine.Controlled=null;RequireBlocked("unknown actor");engine.Controlled=new Actor();
        FF9StateSystem.Field.FF9Field.loc.map.nextMode=2;RequireBlocked("pending battle");
        FF9StateSystem.Field.FF9Field.loc.map.nextMode=0;state.mode=2;RequireBlocked("battle");
    }
    private static NavigationCommand[] Press(NavigationContext context,string key)
    {
        var broker=new NavigationInputBroker(NavigationBindings.Parse(null).Bindings);
        broker.Update(context,new PhysicalInputSnapshot());var physical=new PhysicalInputSnapshot();physical.KeysHeld.Add(key);
        return broker.Update(context,physical);
    }
    private static void RequireBlocked(string label)
    {
        var context=NativeNavigationContext.Read();
        Require(!context.CanControl,label+" allowed navigation movement");
        Require(!Press(context,"P").Contains(NavigationCommand.ToggleAutomatic),label+" accepted automatic walking");
    }
    private static void Require(bool condition,string message) { if(!condition)throw new Exception(message); }
}
