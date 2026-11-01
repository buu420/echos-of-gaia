using System;
using Memoria;
using Memoria.Data;
internal static class Program
{
    private static QuadMistCard Card(byte attack=17){return new QuadMistCard{id=(TetraMasterCardId)0,atk=attack,pdef=47,mdef=63,arrow=5};}
    private static void Require(bool condition,string reason){if(!condition)throw new Exception(reason);}
    private static void SameCardFaceCannotRevealHiddenGrowth()
    {var a=Card(17);var b=Card(31);Require(a.ToString()==b.ToString(),"invalid fixture: native card faces differ");Require(QuadMistAccessibility.GetCardDescription(a)==QuadMistAccessibility.GetCardDescription(b),"speech reveals growth inside a single displayed grade");}
    private static void GradesMatchTheNativeHexFace()
    {var c=Card(175);c.pdef=188;c.mdef=237;c.type=QuadMistCard.Type.MAGIC;Require(c.ToString()=="ambe","native face fixture changed");string s=QuadMistAccessibility.GetCardDescription(c);Require(s.Contains("Attack A") && s.Contains("Physical Defense B") && s.Contains("Magic Defense E"),"spoken grades differ from native card face");}
    private static void BoardReadsVisibleDefenseGrades()
    {var b=new Board();b[0,0]=Card();string s=QuadMistAccessibility.GetBoardPositionDescription(b,0,0);Require(s.Contains("Physical Defense 2") && s.Contains("Magic Defense 3"),"board omits visible defense grades");}
    private static void DetailedSpeechDoesNotRepeatArrowDirections()
    {string s=QuadMistAccessibility.GetDetailedCardDescription(Card());Require(s.IndexOf("arrows at",StringComparison.Ordinal)==s.LastIndexOf("arrows at",StringComparison.Ordinal),"detailed description repeats arrow directions");}
    private static void TripleTriadReadsTheNativeFaceInsteadOfHiddenRawStats()
    {Configuration.TetraMaster.TripleTriad=1;TripleTriad.TripleTriadCardStats[(TetraMasterCardId)0]=new TripleTriadCard{atk=1,matk=2,pdef=3,mdef=4};var c=Card(255);Require(c.ToString()=="1234","invalid TripleTriad fixture");string s=QuadMistAccessibility.GetCardDescription(c);Require(s.Contains("Top 1") && s.Contains("Bottom 2") && s.Contains("Left 3") && s.Contains("Right 4"),"TripleTriad doesn't describe its displayed directional values");Require(!s.Contains("255") && !s.Contains("Physical type"),"TripleTriad announces hidden or unrelated Tetra Master stats");}
    private static void EmptyAndBlockedCellsStayReadable()
    {Require(QuadMistAccessibility.GetCardDescription(null)=="Empty","empty hand slot");var b=new Board();Require(QuadMistAccessibility.GetBoardPositionDescription(b,0,0).Contains("empty"),"empty board cell");b[0,0]=new QuadMistCard{id=(TetraMasterCardId)100};Require(QuadMistAccessibility.GetBoardPositionDescription(b,0,0).Contains("blocked"),"blocked cell");}
    private static void HiddenArrowsInTripleTriadAreNotAnnounced()
    {Configuration.TetraMaster.TripleTriad=2;TripleTriad.TripleTriadCardStats[(TetraMasterCardId)0]=new TripleTriadCard{atk=1,matk=2,pdef=3,mdef=4};string s=QuadMistAccessibility.GetCardDescription(Card());Require(!s.Contains("arrows") && !s.Contains("North"),"hidden arrows announced");}
    private static int Main()
    {Action[] tests={SameCardFaceCannotRevealHiddenGrowth,GradesMatchTheNativeHexFace,BoardReadsVisibleDefenseGrades,DetailedSpeechDoesNotRepeatArrowDirections,TripleTriadReadsTheNativeFaceInsteadOfHiddenRawStats,EmptyAndBlockedCellsStayReadable,HiddenArrowsInTripleTriadAreNotAnnounced,FocusTests.InitialHandSelectionSpeaksOnceWithoutMoving,FocusTests.NativeConfirmOpensBoardAndFirstCellSpeaksOnce,FocusTests.NativeCancelReturnsToAnAnnouncedHand,FocusTests.RejectedSpeechRetriesTheCurrentSelectionOnly,FocusTests.BoundaryInputDoesNotRepeatUnchangedFocus,FocusTests.RematchNewHandAnnouncesItsFirstSelection,FocusTests.SpeechRecoveryReannouncesUnchangedFocus,NumericTests.NumericUpdatesReadRenderedValuesAndSelectedDigit,NumericTests.UnrenderedAndOverlayWindowsAreNotRead,NumericTests.NumericOutputRetriesAndDropsClosedSceneState,NumericTests.MenuAndNarrationGatesKeepLatestNumericState,NumericTests.OpeningControlsSurviveInitialAndAutomaticNumericUpdates,AuctionPanelTests.ConcurrentShownPriceAndBidAreOneCurrentAnnouncement};int failed=0;foreach(var test in tests){Configuration.TetraMaster.TripleTriad=0;try{test();Console.WriteLine("PASS "+test.Method.Name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+e.Message);}}Console.WriteLine("PresentationIntegration: "+(tests.Length-failed)+" passed, "+failed+" failed");return failed==0?0:1;}
}
