using System.Collections.Generic;
namespace Memoria.Prime {public static class Log {public static void Message(string format,params object[] args){}}}
namespace Memoria.Accessibility.Navigation {public static class NavigationLifecycleBridge {public static long Generation=1;}}
namespace Memoria.Accessibility.AudioDescriptions {public static class NativeAudioDescriptions {public static bool NarrationPlaying;public static void SpeakDialogue(Dialog window,string text){Memoria.ScreenReader.ScreenReaderManager.Instance.TrySpeak(text,true);Memoria.ScreenReader.ScreenReaderManager.Instance.Speaking=true;}}}
public sealed class FF9StateSystem {public static readonly CommonState Common=new CommonState();public sealed class CommonState {public readonly Data FF9=new Data();}public sealed class Data {public int fldMapNo=909;}}
public sealed class UILabel {public bool isVisible=true;}
namespace Memoria.Assets
{
    public sealed class TextParser
    {
        public enum ParseStep {Page,ConstantReplaceTags,ChoiceSetup,VariableReplaceTags,BIDI,Wrapped,Render}
        public string InitialText,ParsedText;public ParseStep Step=ParseStep.Render;public readonly UILabel LabelContainer=new UILabel();public readonly Dictionary<int,int> VariableMessageValues=new Dictionary<int,int>();
    }
}
public sealed class Dialog
{
    public enum State {CompleteAnimation,CloseAnimation}
    public readonly FakeGameObject gameObject=new FakeGameObject();public Memoria.Assets.TextParser CurrentParser;public bool IsOverlayDialog;public int TextId=203,OverlayMessageNumber=7;
    public State CurrentState=State.CompleteAnimation;private static int _next;private readonly int _id=++_next;public int GetInstanceID(){return _id;}
}
