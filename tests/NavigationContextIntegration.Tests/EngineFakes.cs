using System;
using System.Collections.Generic;
using Memoria.Accessibility.Navigation;

// State-only substitutes for the engine surface read by the actual context adapter.
// They do not implement navigation gates or synthesize game input.
namespace UnityEngine
{
    public class Object
    {
        private static readonly Dictionary<Type, object> Objects = new Dictionary<Type, object>();
        public static T FindObjectOfType<T>() where T : class { object value; return Objects.TryGetValue(typeof(T), out value) ? (T)value : null; }
        public static void Set<T>(T value) { Objects[typeof(T)] = value; }
    }
    public sealed class GameObject { }
    public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; } }
}
public static class HonoInputManager { public static bool Active; public static bool ApplicationIsActivated() { return Active; } }
public sealed class FF9StateSystem
{
    public int mode;
    public static readonly CommonState Common = new CommonState();
    public static readonly FieldState Field = new FieldState();
    public static readonly WorldState World = new WorldState();
    public static readonly BattleState Battle = new BattleState();
    public static readonly EventStateData EventState = new EventStateData();
    public sealed class CommonState { public readonly CommonData FF9 = new CommonData(); }
    public sealed class CommonData { public int fldMapNo = 50; }
    public sealed class FieldState { public string SceneName="FBG_N00_TSHP_MAP002_TH_CGR_1"; public readonly FieldData FF9Field = new FieldData(); }
    public sealed class FieldData { public readonly Location loc=new Location(); }
    public sealed class Location { public readonly MapData map=new MapData(); }
    public sealed class MapData { public int nextMode; }
    public sealed class WorldState { public readonly WorldData FF9World=new WorldData(); }
    public sealed class WorldData { public readonly MapData map=new MapData(); }
    public sealed class BattleState { public bool isRandomEncounter; }
    public sealed class EventStateData { public byte[] gEventGlobal={232,3}; }
}
public sealed class EventEngine
{
    public Actor Controlled;
    public bool UserControl;
    public Actor GetControlChar() { return Controlled; }
    public int GetControlUID() { return Controlled==null?-1:Controlled.uid; }
    public bool GetUserControl() { return UserControl; }
}
public sealed class Actor
{
    public int uid=17,index;
    public UnityEngine.GameObject go=new UnityEngine.GameObject();
    public FieldMapActorController fieldMapActorController=new FieldMapActorController();
    public WMActor wmActor;
}
public sealed class FieldMapActorController { public bool isPlayer=true; public UnityEngine.Vector3 curPos=new UnityEngine.Vector3(25,0,-664); }
public sealed class WMActor { public Actor originalActor; public UnityEngine.Vector3 RealPosition; }
public static class WMUIData { public static int ActiveMapNo; }
public static class ff9 { public static WMActor w_moveActorPtr; }
public sealed class UIManager
{
    public enum UIState { FieldHUD,WorldHUD,Tutorial,Menu }
    public UIState State;
    public bool IsPause,IsLoading,IsPlayerControlEnable;
}
public sealed class Dialog { public bool HasChoices; public bool FlagButtonInh; }
public sealed class DialogManager
{
    public readonly List<Dialog> ActiveDialogList=new List<Dialog>();
    public bool Visible { get { return ActiveDialogList.Count>0; } }
    public Dialog GetChoiceDialog() { return ActiveDialogList.Find(d=>d.HasChoices); }
}
public enum MinigameHUD { None,Other }
public static class EventHUD { public static MinigameHUD CurrentHUD; }
public static class EventInput { public static bool IsMovementControl; }
namespace Memoria.Accessibility.Navigation
{
    public static class NavigationLifecycleBridge { public static long Generation=3; public static BattleTransition ReturnTransition; }
    public static class NavigationSaveIdentity { public static string Current="test-save"; }
    public static class NativeChocoboHotCold
    {
        public static bool CanInspect;
        public static bool ObserveContext(NavigationContext c,UIManager ui,DialogManager d,int mode,bool pendingBattle) { return false; }
    }
}
