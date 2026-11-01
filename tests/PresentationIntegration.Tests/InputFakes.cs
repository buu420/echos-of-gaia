using System;
using System.Collections.Generic;
using UnityEngine;
public abstract class InputHandler
{
    public abstract void SetDelegate(InputHandler handler);
    public abstract void HandlePreSelection(PreBoard board,Hand hand,ref InputResult result);
    public abstract void HandleYourCardSelection(Board board,Hand hand,ref InputResult result);
    public abstract void HandleTargetCardSelection(Board board,QuadMistCard origin,List<QuadMistCard> choices,ref InputResult result);
    public abstract void HandlePostSelection(Hand hand,List<int> choices,ref InputResult result);
    public abstract void HandleConfirmation(ref InputResult result);
    public abstract void HandleDialog(ref InputResult result);
    public abstract void OnCursorForceChanged(int index);
}
public sealed class InputDelegatorHandler:InputHandler
{
    public enum InputType {None,Keyboard,Mouse}
    public InputType LastActiveInputHandler=InputType.Keyboard,StrictActiveInputHandler;
    public override void SetDelegate(InputHandler h){}public override void HandlePreSelection(PreBoard b,Hand h,ref InputResult r){}public override void HandleYourCardSelection(Board b,Hand h,ref InputResult r){}public override void HandleTargetCardSelection(Board b,QuadMistCard c,List<QuadMistCard> l,ref InputResult r){}public override void HandlePostSelection(Hand h,List<int> c,ref InputResult r){}public override void HandleConfirmation(ref InputResult r){}public override void HandleDialog(ref InputResult r){}public override void OnCursorForceChanged(int i){}
}
public enum Control {Up,Down,Left,Right,LeftBumper,RightBumper,Confirm,Cancel,Special,Select,Pause}
public static class UIManager {public static readonly FakeInput Input=new FakeInput();}
public sealed class FakeInput {public readonly HashSet<Control> Keys=new HashSet<Control>();public bool GetKey(Control c){return Keys.Contains(c);}public bool GetKeyTrigger(Control c){return Keys.Contains(c);}}
public sealed class InputResult {public QuadMistCard selectedCard;public int index,selectedHandIndex,x,y;private bool _valid;public bool IsValid(){return _valid;}public void Used(){_valid=true;}}
public sealed class FakeGameObject {public bool Active;public bool activeInHierarchy {get{return Active;}}public void SetActive(bool active){Active=active;}}
public sealed class FakeCursor {public readonly FakeGameObject gameObject=new FakeGameObject();public void SetNormalState(){}}
public sealed class Hand:List<QuadMistCard>
{
    public enum STATE {PLAYER_SELECT_CARD,PLAYER_SELECT_BOARD}
    public STATE State;public int Select;public readonly FakeCursor cursor=new FakeCursor(),CardCursor=new FakeCursor();
    public void UpdateShadowCard(int i){}public void UpdateCursorToShadowCard(){}public void HideShadowCard(){}public void HideCardCursor(){}public void ShowCardCursor(){}
}
public sealed class PreBoard {public QuadMistCard Preview;public int CountSelected(){return Preview==null?0:1;}public void SetPreviewCardID(int i){}public void PrevCard(){}public void NextCard(){}public void Add(QuadMistCard c){Preview=c;}public QuadMistCard RemoveSelected(){return Preview;}}
public sealed class QuadMistCardUI {public bool Black,Select;}
public sealed partial class Board
{
    public static bool USE_SMALL_BOARD;public readonly FakeCursor cursor=new FakeCursor();public readonly QuadMistCardUI[,] field=new QuadMistCardUI[4,4];
    public void ShowBoardCursor(){}public void HideBoardCursor(){}public void SetBoardCursorPosition(Vector2 point){}public void PlaceCursor(int x,int y){}public bool IsFree(int x,int y){return this[x,y]==null;}public QuadMistCardUI GetCardUI(QuadMistCard card){return new QuadMistCardUI();}
    public QuadMistCardUI GetCardUI(int index){return field[index%4,index/4];}
}
public static class Anim {public static float TickToTime(int ticks){return ticks/30f;}}
public enum GAME_STATE {PLAY,POSTGAME}
public sealed class QuadMistGame {public static readonly QuadMistGame main=new QuadMistGame();public GAME_STATE GameState=GAME_STATE.PLAY;public readonly FakeNameDialog CardNameDialogSlider=new FakeNameDialog();}
public sealed class FakeNameDialog {public bool IsShowCardName;public void ShowCardNameDialog(Hand h){}public void HideCardNameDialog(Hand h){}}
public static class QuadMistConfirmDialog {public static bool IsOK;public static void MessageSelect(bool value){IsOK=value;}}
public enum QuadMistSoundID {MINI_SE_CARD_MOVE,MINI_SE_CURSOL,MINI_SE_CANCEL,MINI_SE_WARNING}
public static class SoundEffect {public static void Play(QuadMistSoundID sound){}}
namespace UnityEngine
{
    public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
    public static class Time {public static float deltaTime;}
    public static class Mathf {public static int Clamp(int n,int lo,int hi){return Math.Max(lo,Math.Min(hi,n));}}
}
namespace Memoria.ScreenReader
{
    public sealed class ScreenReaderManager
    {
        public static readonly ScreenReaderManager Instance=new ScreenReaderManager();public bool Accept=true,Enabled=true,Speaking;public long SpeechGeneration;public readonly List<string> Spoken=new List<string>();public readonly List<bool> Interrupts=new List<bool>();
        public bool IsSpeaking(){return Speaking;}
        public void Speak(string text,bool interrupt){TrySpeak(text,interrupt);}public bool TrySpeak(string text,bool interrupt){if(!Accept)return false;Spoken.Add(text);Interrupts.Add(interrupt);return true;}
    }
}
