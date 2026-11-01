using System;
using Memoria.ScreenReader;
internal static class FocusTests
{
    private sealed class Fixture
    {
        internal readonly ButtonInputHandler Handler=new ButtonInputHandler();internal Board Board=new Board();internal Hand Hand=new Hand{new QuadMistCard{atk=17},new QuadMistCard{atk=33}};
        internal Fixture(){UIManager.Input.Keys.Clear();ScreenReaderManager.Instance.Spoken.Clear();ScreenReaderManager.Instance.Accept=true;ScreenReaderManager.Instance.Enabled=true;Handler.SetDelegate(new InputDelegatorHandler());}
        internal void Tick(Control? key=null){UIManager.Input.Keys.Clear();if(key.HasValue)UIManager.Input.Keys.Add(key.Value);var result=new InputResult();Handler.HandleYourCardSelection(Board,Hand,ref result);UIManager.Input.Keys.Clear();}
        internal int Spoken {get{return ScreenReaderManager.Instance.Spoken.Count;}}
        internal string Last {get{return Spoken==0?"":ScreenReaderManager.Instance.Spoken[Spoken-1];}}
    }
    private static void Require(bool value,string reason){if(!value)throw new Exception(reason);}
    public static void InitialHandSelectionSpeaksOnceWithoutMoving()
    {var f=new Fixture();f.Tick();Require(f.Spoken==1,"first selected card is silent");Require(f.Hand.Count==2 && f.Hand.Select==0,"speech changed native hand selection");f.Tick();Require(f.Spoken==1,"idle hand repeats");}
    public static void NativeConfirmOpensBoardAndFirstCellSpeaksOnce()
    {var f=new Fixture();f.Tick();f.Tick(Control.Confirm);Require(f.Hand.State==Hand.STATE.PLAYER_SELECT_BOARD,"native Confirm transition changed");int before=f.Spoken;f.Tick();Require(f.Spoken==before+1 && f.Last.StartsWith("A1, empty"),"initial board cell is silent");f.Tick();Require(f.Spoken==before+1,"idle board repeats");}
    public static void NativeCancelReturnsToAnAnnouncedHand()
    {var f=new Fixture();f.Tick();f.Tick(Control.Confirm);f.Tick();f.Tick(Control.Cancel);int before=f.Spoken;f.Tick();Require(f.Spoken==before+1 && f.Last.Contains("Attack 1"),"hand focus is silent after board cancellation");}
    public static void RejectedSpeechRetriesTheCurrentSelectionOnly()
    {var f=new Fixture();ScreenReaderManager.Instance.Accept=false;f.Tick();f.Tick();Require(f.Spoken==0,"rejected output recorded as spoken");ScreenReaderManager.Instance.Accept=true;f.Tick();Require(f.Spoken==1,"initial speech is never retried");ScreenReaderManager.Instance.Accept=false;f.Tick(Control.Down);ScreenReaderManager.Instance.Accept=true;f.Tick();Require(f.Spoken==2 && f.Last.Contains("Attack 2"),"changed selection retry lost or stale");f.Tick();Require(f.Spoken==2,"accepted retry repeats");}
    public static void BoundaryInputDoesNotRepeatUnchangedFocus()
    {var f=new Fixture();f.Tick();f.Tick(Control.Up);Require(f.Spoken==1,"boundary input repeats card");f.Tick(Control.Down);Require(f.Spoken==2 && f.Hand.Select==1,"real selection change missing");}
    public static void RematchNewHandAnnouncesItsFirstSelection()
    {var f=new Fixture();f.Tick();f.Hand=new Hand{new QuadMistCard{atk=49}};f.Board=new Board();int before=f.Spoken;f.Tick();Require(f.Spoken==before+1 && f.Last.Contains("Attack 3"),"new hand inherited old focus suppression");}
    public static void SpeechRecoveryReannouncesUnchangedFocus()
    {var f=new Fixture();f.Tick();ScreenReaderManager.Instance.Enabled=false;ScreenReaderManager.Instance.SpeechGeneration++;f.Tick();Require(f.Spoken==1,"disabled speech attempted");ScreenReaderManager.Instance.Enabled=true;f.Tick();Require(f.Spoken==2,"recovered output inherited old focus cache");f.Tick();Require(f.Spoken==2,"recovered focus repeats");}
}
