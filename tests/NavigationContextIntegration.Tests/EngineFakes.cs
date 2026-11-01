using System;
using System.Collections.Generic;
using Memoria.Accessibility.Navigation;

// State-only substitutes for the engine surface read by the actual context adapter.
// They do not implement navigation gates or synthesize game input.
namespace UnityEngine
{
    public enum RuntimePlatform { WindowsPlayer,Other }
    public static class Application { public static RuntimePlatform platform=RuntimePlatform.WindowsPlayer; public static string persistentDataPath=System.IO.Path.GetTempPath(); }
    public class Object
    {
        private static readonly Dictionary<Type, object> Objects = new Dictionary<Type, object>();
        public static T FindObjectOfType<T>() where T : class { object value; return Objects.TryGetValue(typeof(T), out value) ? (T)value : null; }
        public static void Set<T>(T value) { Objects[typeof(T)] = value; }
    }
    public sealed class GameObject { }
    public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; } }
}
namespace Memoria.Prime
{
    public static class Log { public static void Error(Exception exception,string message) { } public static void Message(string text,params object[] args) { } }
}
namespace Memoria.Prime.WinAPI
{
    public static class User32
    {
        public static string Title="FINAL FANTASY IX";
        public static int Writes;
        public static bool Accept=true;
        public static IntPtr FindWindow(string name,string title) { return new IntPtr(1); }
        public static bool SetWindowText(IntPtr handle,string text) { Writes++;if(Accept)Title=text;return Accept; }
        public static void Reset() { Title="FINAL FANTASY IX";Writes=0;Accept=true; }
    }
}
namespace Memoria.ScreenReader
{
    public sealed class ScreenReaderManager
    {
        public static readonly ScreenReaderManager Instance=new ScreenReaderManager();
        public bool Enabled=true;
    }
}
namespace Memoria
{
    public sealed class Configuration
    {
        public static class Accessibility { public static bool AnnounceRegionChanges=true; }
    }
}
namespace Assets.Sources.Scripts.UI.Common
{
    public static class FF9TextTool
    {
        public static readonly Dictionary<int,string> Names=new Dictionary<int,string>();
        public static int LastLookup;
        public static string LocationName(int id) { LastLookup=id;string name;return Names.TryGetValue(id,out name)?name:String.Empty; }
        public static string RemoveOpCode(string value) { return value==null?null:value.Replace("[ENDN]",""); }
    }
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
    public int gMode=1;
    public readonly Dictionary<int,Actor> Actors=new Dictionary<int,Actor>();
    public readonly Dictionary<int,Obj> Objects=new Dictionary<int,Obj>();
    public object GetObjUID(int uid) { Actor value;Obj obj;return Controlled!=null && Controlled.uid==uid?Controlled:Objects.TryGetValue(uid,out obj)?obj:Actors.TryGetValue(uid,out value)?value:null; }
    public Actor Controlled;
    public bool UserControl;
    public Actor GetControlChar() { return Controlled; }
    public int GetControlUID() { return Controlled==null?-1:Controlled.uid; }
    public bool GetUserControl() { return UserControl; }
}
public class Obj { public int uid=17,sid,sx=2,level=1;public byte[] ebData=new byte[]{0,0}; }
public class PosObj:Obj { }
public sealed class Quad:Obj { public string Signature="polygon"; }
public sealed class Actor:PosObj
{
    public int index,model;
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
    public static bool IsUIStateMenu(UIState state) { return state==UIState.Menu || state==UIState.Tutorial; }
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
public class PersistenSingleton<T> where T:class { public static T Instance; }
namespace Memoria.Accessibility.Navigation
{
    public static class NavigationLifecycleBridge { public static long Generation=3; public static BattleTransition ReturnTransition; public static event Action<StopReason> Releasing; public static void Release(StopReason reason) { if(Releasing!=null)Releasing(reason); } }
    public static class NavigationSaveIdentity { public static string Current="test-save";public static readonly ObservedIdentityStore Identities=new ObservedIdentityStore();public static event Action<string> Loaded,Saved; }
    public static class NativeNavigationInput { public static void EnsureUpdated() { } }
    public static class NativeStoryTargets { public static void Initialize() { } }
    public static class NavigationRuntime { public static void Tick() { } public static void Trace(string text) { } }
    public static class NativeFieldTargets { public static int Reads;public static readonly List<FieldTargetObservation> Observations=new List<FieldTargetObservation>(); public static IList<FieldTargetObservation> Read(NavigationContext context){Reads++;return Observations;} public static string ExitLabelSignature(Quad quad){return quad.sid+":"+Convert.ToBase64String(quad.ebData)+":"+quad.Signature;} public static string LabelSignature(Actor actor){return actor.model+":"+actor.sid+":"+Convert.ToBase64String(actor.ebData);} }
    public static class NativeWorldTargets { public static int Reads;public static IList<WorldTargetObservation> Read(NavigationContext context){Reads++;return new List<WorldTargetObservation>();} }
    public static class NativeWorldGeometry { public static int Reads;public static WorldGeometrySnapshot Read(NavigationContext context,NavigationTarget target){Reads++;return null;} }
    public static class NativeRegionReader { public static bool TryRead(out int id,out string label){id=-1;label=null;return false;} }
    public static class NativeWorldFootsteps { public static void Tick(NavigationContext context) { } }
    public static class NativeChocoboHotCold
    {
        public static bool CanInspect;
        public static bool ObserveContext(NavigationContext c,UIManager ui,DialogManager d,int mode,bool pendingBattle) { return false; }
    }
}
