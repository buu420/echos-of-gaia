using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Sources.Scripts.UI.Common;
using Memoria.Data;

// Test boundary only: components, hierarchy, queued animation completions, party display and
// native speech. No menu readiness or announcement formatting is implemented here.
namespace UnityEngine
{
    public class Object
    {
        public static implicit operator bool(Object value) => value != null;
        public static T Instantiate<T>(T source) where T : Object => throw new NotSupportedException("Optional Party menu cloning is outside these fixtures.");
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
    }
    public class MonoBehaviour : Component
    {
        public bool enabled = true;
        public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy;
    }
    public class GameObject : Object
    {
        private readonly List<Component> components = new List<Component>();
        public readonly Transform transform;
        public string name, tag;
        public bool activeSelf = true;
        public bool activeInHierarchy => activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public GameObject(string name = "") { this.name = name; transform = new Transform { gameObject = this }; components.Add(transform); }
        public void SetActive(bool active) { activeSelf = active; }
        public T AddComponent<T>() where T : Component, new() { T result = new T { gameObject = this }; components.Add(result); return result; }
        public T GetComponent<T>() where T : Component => components.OfType<T>().FirstOrDefault();
        public T GetComponentInChildren<T>() where T : Component => GetComponentsInChildren<T>().FirstOrDefault();
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : Component
        {
            var result = components.OfType<T>().ToList();
            foreach (Transform child in transform)
                if (includeInactive || child.gameObject.activeInHierarchy)
                    result.AddRange(child.gameObject.GetComponentsInChildren<T>(includeInactive));
            return result.ToArray();
        }
        public GameObject GetChild(int index) => transform.GetChild(index).gameObject;
        public GameObject GetParent() => transform.parent.gameObject;
    }
    public class Transform : Component, IEnumerable<Transform>
    {
        private readonly List<Transform> children = new List<Transform>();
        private Transform parentValue;
        public Transform parent { get => parentValue; set { parentValue?.children.Remove(this); parentValue = value; value?.children.Add(this); } }
        public Vector3 localPosition, localScale;
        public int childCount => children.Count;
        public Transform GetChild(int index) => children[index];
        public int GetSiblingIndex() => parentValue == null ? 0 : parentValue.children.IndexOf(this);
        public bool IsChildOf(Transform other) { for (var item = this; item != null; item = item.parent) if (item == other) return true; return false; }
        public IEnumerator<Transform> GetEnumerator() => children.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    public class BoxCollider : MonoBehaviour { public Vector3 size; }
    public struct Vector2 { public float x,y; public Vector2(float x,float y) { this.x=x; this.y=y; } }
    public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z=0) { this.x=x; this.y=y; this.z=z; } public static Vector3 one => new Vector3(1,1,1); }
    public struct Color { public float r,g,b,a; public Color(float r,float g,float b,float a=1) { this.r=r; this.g=g; this.b=b; this.a=a; } public static Color black => new Color(0,0,0); }
    public enum KeyCode { None, UpArrow, DownArrow }
    public static class Time { public static float realtimeSinceStartup; public static int frameCount; }
    public sealed class SerializeField : Attribute { }
}

public static class ComponentExtensions { public static T GetExactComponent<T>(this UnityEngine.GameObject go) where T : UnityEngine.Component => go.GetComponent<T>(); }
public static class Singleton<T> where T : new() { public static T Instance = new T(); }
public static class PersistenSingleton<T> where T : new() { public static T Instance = new T(); }
public class UIAtlas : UnityEngine.Object { }
public class UIRect : UnityEngine.MonoBehaviour
{
    public enum AnchorUpdate { OnStart, OnUpdate }
    public class Anchor { public UnityEngine.Transform target; public float relative; public int absolute; public void Set(float relative,float absolute) { this.relative=relative; this.absolute=(int)absolute; } }
    public Anchor leftAnchor = new Anchor(), rightAnchor = new Anchor();
    public float finalAlpha=1;
    public virtual float alpha { get; set; } = 1;
    public virtual float CalculateFinalAlpha(int frame)
    {
        UIRect ancestor=null;
        for(var item=transform.parent;item!=null && ancestor==null;item=item.parent)ancestor=item.gameObject.GetComponent<UIRect>();
        return finalAlpha=alpha*(ancestor==null?1:ancestor.CalculateFinalAlpha(frame));
    }
    public AnchorUpdate updateAnchors;
    public void ResetAnchors() { } public void UpdateAnchors() { }
}
public class UIWidget : UIRect { public UnityEngine.Color color = new UnityEngine.Color(1,1,1); public override float alpha { get=>color.a; set=>color=new UnityEngine.Color(color.r,color.g,color.b,value); } public void SetRawRect(float x,float y,float w,float h) { } }
public class UILabel : UIWidget
{
    private Memoria.Assets.TextParser parser = new Memoria.Assets.TextParser("");
    public string rawText { get => parser.InitialText; set => parser = new Memoria.Assets.TextParser(value); }
    public Memoria.Assets.TextParser Parser => parser;
    public enum Overflow { ResizeFreely }
    public bool fixedAlignment;
    public Overflow overflowMethod;
    public NGUIText.Alignment alignment;
}
public class UISprite : UIWidget { public UIAtlas atlas; public string spriteName; }
public class UIPanel : UIRect { public int depth; }
public class UIButtonColor : UnityEngine.MonoBehaviour { public enum State { Normal, Disabled } public State state; }
public class UIButton : UIButtonColor { public void SetState(State state,bool instant) { this.state=state; } }
public class UILocalize : UnityEngine.MonoBehaviour { public string key; }
public class UIInput : UnityEngine.MonoBehaviour { }
public class UITable : UnityEngine.MonoBehaviour { }
public class UIKeyNavigation : UnityEngine.MonoBehaviour { public UnityEngine.GameObject onUp,onDown; public bool wrapUpDown; }
public class UICamera { public static UnityEngine.GameObject selectedObject; public static int currentTouchID; }
public class UIEventListener
{
    public delegate void VoidDelegate(UnityEngine.GameObject go);
    public event VoidDelegate Click;
    public static UIEventListener Get(UnityEngine.GameObject go) => new UIEventListener();
}
public class HonoFading : UnityEngine.MonoBehaviour
{
    public float fadeOutDuration,fadeInDuration;
    public UISprite ForegroundSprite = new UISprite();
    public readonly Queue<UIScene.SceneVoidDelegate> Shows = new Queue<UIScene.SceneVoidDelegate>();
    public readonly Queue<UIScene.SceneVoidDelegate> Hides = new Queue<UIScene.SceneVoidDelegate>();
    public void FadeOut(UIScene.SceneVoidDelegate action) => Shows.Enqueue(action);
    public void FadeIn(UIScene.SceneVoidDelegate action) => Hides.Enqueue(action);
    public void CompleteShow() => Shows.Dequeue()();
    public void CompleteHide() => Hides.Dequeue()();
}
public class HonoTweenPosition : UnityEngine.MonoBehaviour
{
    public readonly Queue<UIScene.SceneVoidDelegate> Shows = new Queue<UIScene.SceneVoidDelegate>();
    public void TweenIn(byte[] indexes,UIScene.SceneVoidDelegate action) => Shows.Enqueue(action);
    public void CompleteShow() => Shows.Dequeue()();
}
public class HonoTweenClipping : UnityEngine.MonoBehaviour
{
    public float AnimationTime; public UnityEngine.Vector2 ShiftContentClip;
    public void TweenIn(UIScene.SceneVoidDelegate action) { action?.Invoke(); }
    public void TweenOut(UIScene.SceneVoidDelegate action) { action?.Invoke(); }
}
public class PointerManager { public void SetAllPointerVisibility(bool visible) { } }
public class BubbleUI { public void SetGameObjectActive(bool active) { } }
public class ScrollButton : UnityEngine.Object { public void OnPageUpButtonClick() { } public void OnPageDownButtonClick() { } }
public class FieldHUD : UIScene { }
public class WorldHUD : UIScene { }
public class QuitUI { public bool isShowQuitUI; public void OnKeyConfirm(UnityEngine.GameObject go) { } public void OnKeyCancel(UnityEngine.GameObject go) { } public void Show(Action action) { } }
public class PartySettingUI { public bool AccessFromMenu; public object Info; }
public class CharacterUI { public int CurrentPartyIndex; }
public class UIManager : UnityEngine.MonoBehaviour
{
    public enum UIState { FieldHUD,WorldHUD,BattleHUD,MainMenu,Item,Ability,Equip,Status,Card,Config,PartySetting }
    public enum Scene { Field,World,Battle }
    public UIState State = UIState.MainMenu, PreviousState = UIState.FieldHUD;
    public Scene UnityScene = Scene.Field;
    public MainMenuUI MainMenuScene;
    public QuitUI QuitScene = new QuitUI();
    public PartySettingUI PartySettingScene = new PartySettingUI();
    public CharacterUI AbilityScene = new CharacterUI(),EquipScene = new CharacterUI(),StatusScene = new CharacterUI();
    public UIManager() { gameObject=new UnityEngine.GameObject("UIManager"); }
    public static bool IsUIStateMenu(UIState state) => state >= UIState.MainMenu;
    public void MenuOpenEvent() { }
    public void ChangeUIState(UIState state) { PreviousState=State; State=state; }
    public UIScene GetSceneFromState(UIState state) => state == UIState.MainMenu ? MainMenuScene : null;
}
public enum Control { Up, Down }
public enum SourceControl { Joystick }
public class HonoInputManager { public static bool Activated=true; public static bool ApplicationIsActivated()=>Activated; public SourceControl GetSource(Control control) => SourceControl.Joystick; }
public static class NGUIText
{
    public enum Alignment { Center }
    public static UnicodeBIDI.LanguageReadingDirection readingDirection;
}
public class UnicodeBIDI { public enum LanguageReadingDirection { LeftToRight,RightToLeft } }
public class ButtonGroupState : UnityEngine.MonoBehaviour
{
    public class HelpDetail { public string TextKey,Text; }
    public HelpDetail Help = new HelpDetail();
    public string GroupName;
    public static string ActiveGroup { get => activeGroup; set { if (activeGroup==value) return; activeGroup=value; if (cursors.TryGetValue(value,out var go)) ActiveButton=go; } }
    private static string activeGroup;
    private static UnityEngine.GameObject activeButton;
    private static readonly Dictionary<string,UnityEngine.GameObject> cursors = new Dictionary<string,UnityEngine.GameObject>();
    public static UnityEngine.GameObject ActiveButton
    {
        get => activeButton;
        set { activeButton=value; UICamera.selectedObject=value; if (value!=null) PersistenSingleton<UIManager>.Instance.GetSceneFromState(PersistenSingleton<UIManager>.Instance.State)?.OnItemSelect(value); }
    }
    public static ScrollButton ActiveScrollButton;
    public static void Reset() { activeGroup=null; activeButton=null; cursors.Clear(); }
    public static void SetCursorStartSelect(UnityEngine.GameObject go,string group) { cursors[group]=go; }
    public static void SetCursorMemorize(UnityEngine.GameObject go,string group) { if(go!=null)cursors[group]=go; }
    public static void RemoveCursorMemorize(string group) { }
    public static void SetPointerDepthToGroup(int depth,string group) { }
    public static void SetPointerOffsetToGroup(UnityEngine.Vector2 offset,string group) { }
    public static void DisableAllGroup(bool unused) { activeGroup=null; activeButton=null; }
    public static void SetActiveGroupEnable(bool enabled) { }
    public static void SetButtonEnable(UnityEngine.GameObject go,bool enabled) { go.GetComponent<ButtonGroupState>().enabled=enabled; go.GetComponent<UnityEngine.BoxCollider>().enabled=enabled; }
    public static void SetSecondaryOnGroup(string group) { }
    public static void HoldActiveStateOnGroup(string group) { }
    public static void HoldActiveStateOnGroup(UnityEngine.GameObject go,string group) { }
    public static bool ContainButtonInGroup(UnityEngine.GameObject go,string group) => go.GetComponent<ButtonGroupState>()?.GroupName==group;
    public static bool ContainButtonInSecondaryGroup(UnityEngine.GameObject go) => false;
    public static void ToggleHelp(bool enabled) { }
    public bool ProcessTouch() => true;
}
public class PLAYER
{
    public string Name="Zidane";
    public int level=5; public uint exp;
    public Point cur = new Point { hp=77,mp=9,capa=3 }, max = new Point { hp=120,mp=20,capa=8 };
    public PLAYER_INFO info = new PLAYER_INFO();
    public RegularItem[] equip = new RegularItem[5];
    public class Point { public uint hp,mp,capa; }
}
public class PLAYER_INFO { public byte row=1; }
public class FF9StateSystem
{
    public static bool PCPlatform=true,MobilePlatform,EnableAndroidTVJoystickMode,AndroidTVPlatform;
    public static SettingsData Settings = new SettingsData();
    public static CommonData Common = new CommonData();
    public class SettingsData { public bool IsFastForward; public float FastForwardFactor=1; public double time; public void SetMasterSkill() { } public void UpdateTickTime() { } }
    public class CommonData { public FF9Data FF9 = new FF9Data(); }
    public class FF9Data { public Party party=new Party(); public int fldMapNo; public string mapNameStr="Fixture"; }
    public class Party { public PLAYER[] member=new PLAYER[4]; public uint gil; }
}
public class EventEngine { public int GetSysvar(int index) => 0; }
public static class FF9Sfx { public static bool muteSfx; public static void FF9SFX_Play(int value) { } }
public class FF9ITEM_DATA { public int shape,color; }
public static class ff9item { public static Dictionary<RegularItem,FF9ITEM_DATA> _FF9Item_Data = new Dictionary<RegularItem,FF9ITEM_DATA>(); }
public static class ff9level { public const int LEVEL_COUNT=99; public static Level[] CharacterLevelUps = Enumerable.Range(0,100).Select(_=>new Level()).ToArray(); public class Level { public uint ExperienceToLevel; } }
public static class FF9TextTool
{
    public static UnityEngine.Color White=new UnityEngine.Color(1,1,1),Gray=new UnityEngine.Color(.5f,.5f,.5f),Red=White,Yellow=White,Cyan=White,Magenta=White,Green=White;
    public static string LocationName(int index)=>"Fixture";
    public static string WorldLocationText(int index)=>"Fixture";
    public static string ItemName(RegularItem item)=>item.ToString();
    public static string RemoveOpCode(string text)=>System.Text.RegularExpressions.Regex.Replace(text??"",@"\[[^\]]*\]","");
}
namespace Assets.Scripts.Common
{
    public enum FadeMode { Sub }
    public static class SceneDirector { public static void FadeEventSetColor(FadeMode mode,UnityEngine.Color color) { } public static void FF9Wipe_FadeInEx(int value) { } public static bool IsBattleScene()=>false; }
}
namespace Assets.Sources.Scripts.UI.Common
{
    public static class FF9UIDataTool
    {
        public static UIAtlas WindowAtlas,GeneralAtlas,IconAtlas;
        public static void DisplayCharacterDetail(PLAYER player,CharacterDetailHUD hud)
        {
            hud.NameLabel.rawText=player.Name; hud.LvLabel.rawText=player.level.ToString();
            hud.HPLabel.rawText=player.cur.hp.ToString(); hud.HPMaxLabel.rawText=player.max.hp.ToString();
            hud.MPLabel.rawText=player.cur.mp.ToString(); hud.MPMaxLabel.rawText=player.max.mp.ToString();
            if(hud.MagicStoneLabel!=null) { hud.MagicStoneLabel.rawText=player.cur.capa.ToString(); hud.MagicStoneMaxLabel.rawText=player.max.capa.ToString(); }
        }
        public static void DisplayCharacterAvatar(PLAYER player,UnityEngine.Vector3 front,UnityEngine.Vector3 back,UISprite avatar,bool update) { avatar.transform.localPosition=player.info.row==1?front:back; }
    }
}
namespace Memoria
{
    public static class Configuration
    {
        public static class Interface { public static float FadeDuration=.4f; }
        public static class Control { public static bool DisableMouseInBattles,DisableMouseForMenus,WrapSomeMenus; }
        public static class Hacks { public static int AllCharactersAvailable; }
        public static class Battle { public static bool IsMenuEnabledInBattle(MainMenuUI.SubMenu subMenu)=>false; }
    }
}
public static class BattleHUD { public static Dictionary<BattleStatusId,string> DebuffIconNames = new Dictionary<BattleStatusId,string> { { BattleStatusId.Poison,"icon_poison" },{ BattleStatusId.Sleep,"icon_sleep" } }; }
namespace Memoria.Data { public enum RegularItem { NoItem } public enum BattleStatusId { Poison,Sleep } }
namespace Memoria.Assets
{
    public class TextParser
    {
        public readonly string InitialText;
        public string ParsedText;
        public TextParser(string text) { InitialText=text; ParsedText=text; }
        public enum ParseStep { VariableReplaceTags }
        public void Parse(ParseStep step) { ParsedText=FF9TextTool.RemoveOpCode(InitialText); }
    }
    public static class Localization
    {
        public static readonly Dictionary<string,string> Text = new Dictionary<string,string>();
        public static string CurrentDisplaySymbol="US";
        public static string Get(string key)=>GetWithDefault(key);
        public static string GetWithDefault(string key)=>Text.TryGetValue(key,out string value)?value:key;
    }
}
namespace Memoria.Prime { public static class Log { public static readonly List<string> Errors = new List<string>(); public static readonly List<string> Messages = new List<string>(); public static void Error(string text)=>Errors.Add(text); public static void Message(string text)=>Messages.Add(text); } }
namespace Memoria.Scenes
{
    public class MenuUIControlPanel { public void ExitMenu() { } }
    public class GOMenuBackground { public GOMenuBackground(UnityEngine.GameObject go,string name) { } }
    public static class UISceneHelper { public static object GetCurrentPartyForMenu()=>new object(); }
}
namespace Memoria.ScreenReader
{
    public static class SpeechOutput { public static readonly List<string> Text = new List<string>(); public static bool Accept=true; public static int Attempts; }
    public class PrismSpeechBackend : ISpeechBackend
    {
        public string Name=>"Fixture"; public bool Initialize()=>true;
        public bool Output(string text,bool interrupt) { SpeechOutput.Attempts++; if(!SpeechOutput.Accept)return false; SpeechOutput.Text.Add(text); return true; }
        public bool IsSpeaking()=>false; public void Stop() { } public void Dispose() { }
    }
    public class TolkSpeechBackend : PrismSpeechBackend { }
    public static class SpeechDiagnostics { public static string LastWriteFailure=>null; public static string LogPath=>null; public static void Write(string text) { } }
}
