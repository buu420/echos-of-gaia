using System;
using System.Collections.Generic;
using Memoria.Accessibility.AudioDescriptions;
namespace UnityEngine
{
    public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}}
    public struct Touch {}
    public static class Input {
        public static readonly HashSet<KeyCode> Down=new HashSet<KeyCode>();
        public static int touchCount;public static bool anyKey,anyKeyDown;public static Vector2 mousePosition;
        public static bool GetKeyDown(KeyCode key){return Down.Contains(key);}public static bool GetKey(KeyCode key){return Down.Contains(key);}public static bool GetKeyUp(KeyCode key){return false;}
        public static float GetAxis(string key){return 0;}public static float GetAxisRaw(string key){return 0;}
        public static bool GetButtonDown(string key){return false;}
        public static bool GetMouseButtonUp(int i){return false;}public static bool GetMouseButtonDown(int i){return false;}public static bool GetMouseButton(int i){return false;}
        public static Touch GetTouch(int i){return new Touch();}public static string[] GetJoystickNames(){return new string[0];}
    }
    public enum KeyCode {None,A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q,R,S,T,U,V,W,X,Y,Z,UpArrow,DownArrow,LeftArrow,RightArrow,Return,KeypadEnter,Space,Escape,Tab,Backspace,Alpha1,LeftShift,RightShift,LeftControl,RightControl,LeftAlt,RightAlt,KeypadPlus,Semicolon}
}
public static class PersistenSingleton<T> where T:new(){public static T Instance=new T();}
public sealed class HonoInputManager
{
    public static bool Active=true;public static bool ApplicationIsActivated(){return Active;}
    public static float AnalogThreadhold=.1f;
    public UnityEngine.KeyCode[] InputKeys2={UnityEngine.KeyCode.Return,UnityEngine.KeyCode.Escape,UnityEngine.KeyCode.None};
    public UnityEngine.KeyCode[] InputKeys3={UnityEngine.KeyCode.KeypadEnter,UnityEngine.KeyCode.None,UnityEngine.KeyCode.None};
    public PromptInputDevice AccessibilityInputDevice;
    public bool IsControllerConnect=true;
    public UnityEngine.KeyCode[] InputKeysPrimary={UnityEngine.KeyCode.X,UnityEngine.KeyCode.C,UnityEngine.KeyCode.B,UnityEngine.KeyCode.D,UnityEngine.KeyCode.G,UnityEngine.KeyCode.H,UnityEngine.KeyCode.F,UnityEngine.KeyCode.J,UnityEngine.KeyCode.Backspace,UnityEngine.KeyCode.Alpha1};
    public string[] JoystickKeysPrimary={"JoystickButton0","JoystickButton1","JoystickButton3","JoystickButton2","JoystickButton4","JoystickButton5","LeftTrigger","RightTrigger","JoystickButton7","JoystickButton6"};
    public string[] DefaultJoystickInputKeys={"JoystickButton0","JoystickButton1","JoystickButton3","JoystickButton2","JoystickButton4","JoystickButton5","LeftTrigger","RightTrigger","JoystickButton7","JoystickButton6"};
    public static UnityEngine.KeyCode[] MemoriaKeyBindings={UnityEngine.KeyCode.W,UnityEngine.KeyCode.A,UnityEngine.KeyCode.S,UnityEngine.KeyCode.D,UnityEngine.KeyCode.Backspace,UnityEngine.KeyCode.Alpha1};
    public int RemappedPhysicalCalls;public Control FixedControlResult=Control.Special;
    public Control PhysicalButtonToLogicalControl(int key){RemappedPhysicalCalls++;return FixedControlResult;}
}
public static class UICamera{public enum ControlScheme {Mouse,Touch,Controller} public static ControlScheme currentScheme;}
public static class NGUIText{public const string JoyStickButtonIcon="JCBT",KeyboardButtonIcon="KCBT";}
public static class EventInput{public static bool isJapaneseLayout;}
namespace Memoria{public static class Configuration{public static class Control{public static bool AlwaysCaptureGamepad;}public static class Accessibility{public static string ControllerNaming="PlayStation";}}}
namespace Memoria.Prime {}
namespace Memoria.Accessibility.Navigation {public static class NativeNavigationInput{public static float FilterAxis(string name,float value){return value;}public static bool IsKeyCaptured(UnityEngine.KeyCode key){return false;}}}
namespace UnityXInput {public sealed class XInputManager{public XInputDotNetPure.GamePadState CurrentState,PreviousState;}}
namespace Assets.Sources.Scripts.UI.Common{public static class FF9UIDataTool{public static readonly Dictionary<UnityEngine.KeyCode,string> KeyboardIconLabel=new Dictionary<UnityEngine.KeyCode,string>();}}
public static class FF9StateSystem{public sealed class CommonState{public readonly State FF9=new State();}public sealed class State{public int fldMapNo=64;}public static readonly CommonState Common=new CommonState();}
public sealed class UIManager{public enum UIState{FieldHUD,Pause,MainMenu,Title}public static UIManager Instance=PersistenSingleton<UIManager>.Instance;public UIState State=UIState.FieldHUD;}
public sealed class EventEngine{public int gMode=1;public EBin eBin=new EBin();}
public sealed class EBin{public bool ThrowOnRead;public readonly Dictionary<int,int> Values=new Dictionary<int,int>{{6357,3},{728,5}};public int getVarManually(int id){if(ThrowOnRead)throw new InvalidOperationException("field state is being replaced");return Values[id];}}
namespace Memoria.Accessibility.AudioDescriptions
{
    internal static class NativeCidEscapeCue { internal static bool IsKeySequence(EventEngine engine)=>false; }
    internal static class NativeEarthShrineCue { internal static bool IsTrapScene(EventEngine engine)=>false; }
}
