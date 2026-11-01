using System;
using Memoria.Accessibility.Navigation;
using Memoria.ScreenReader;
using Memoria.Assets;
internal static class NumericTests
{
    private static Dialog Window()
    {
        NativeNumericDialogs.Reset();NavigationLifecycleBridge.Generation++;FF9StateSystem.Common.FF9.fldMapNo=909;Memoria.Accessibility.AudioDescriptions.NativeAudioDescriptions.NarrationPlaying=false;
        ScreenReaderManager.Instance.Enabled=ScreenReaderManager.Instance.Accept=true;ScreenReaderManager.Instance.Speaking=false;ScreenReaderManager.Instance.Spoken.Clear();ScreenReaderManager.Instance.Interrupts.Clear();
        var d=new Dialog{CurrentParser=new TextParser{InitialText="Bid: [NUMB=5][NUMB=6][NUMB=7]00 Gil",ParsedText="Bid: 100 Gil"}};d.gameObject.SetActive(true);d.CurrentParser.VariableMessageValues[5]=0;d.CurrentParser.VariableMessageValues[6]=0;d.CurrentParser.VariableMessageValues[7]=1;return d;
    }
    private static void Require(bool value,string reason){if(!value)throw new Exception(reason);}
    public static void NumericUpdatesReadRenderedValuesAndSelectedDigit()
    {var d=Window();NativeNumericDialogs.Opened(d);NativeNumericDialogs.Tick(true);Require(ScreenReaderManager.Instance.Spoken.Count==1 && ScreenReaderManager.Instance.Spoken[0].Contains("100 gil"),"initial displayed status missing");NativeNumericDialogs.Tick(true);Require(ScreenReaderManager.Instance.Spoken.Count==1,"idle numeric status repeated");d.CurrentParser.VariableMessageValues[7]=2;d.CurrentParser.ParsedText="Bid: 200 Gil";NativeNumericDialogs.Tick(true);Require(ScreenReaderManager.Instance.Spoken.Count==2 && ScreenReaderManager.Instance.Spoken[1].Contains("200 gil"),"changed rendered amount silent");d.OverlayMessageNumber=6;NativeNumericDialogs.Tick(true);Require(ScreenReaderManager.Instance.Spoken[2]=="Thousands digit 0.","native selected digit missing");}
    public static void UnrenderedAndOverlayWindowsAreNotRead()
    {var d=Window();d.IsOverlayDialog=true;NativeNumericDialogs.Opened(d);NativeNumericDialogs.Tick(true);Require(ScreenReaderManager.Instance.Spoken.Count==0,"overlay announced");d.IsOverlayDialog=false;NativeNumericDialogs.Opened(d);d.CurrentParser.Step=TextParser.ParseStep.ChoiceSetup;NativeNumericDialogs.Tick(true);Require(ScreenReaderManager.Instance.Spoken.Count==0,"unrendered parser announced");d.CurrentParser.Step=TextParser.ParseStep.Render;NativeNumericDialogs.Tick(true);Require(ScreenReaderManager.Instance.Spoken.Count==1,"ready parser not read");}
    public static void NumericOutputRetriesAndDropsClosedSceneState()
    {var d=Window();NativeNumericDialogs.Opened(d);ScreenReaderManager.Instance.Accept=false;NativeNumericDialogs.Tick(true);d.CurrentParser.VariableMessageValues[7]=3;d.CurrentParser.ParsedText="Bid: 300 Gil";ScreenReaderManager.Instance.Accept=true;NativeNumericDialogs.Tick(true);Require(ScreenReaderManager.Instance.Spoken.Count==1 && ScreenReaderManager.Instance.Spoken[0].Contains("300 gil"),"retry used stale or missing value");NavigationLifecycleBridge.Generation++;NativeNumericDialogs.Tick(true);Require(ScreenReaderManager.Instance.Spoken.Count==1,"old scene numeric dialog read");}
    public static void MenuAndNarrationGatesKeepLatestNumericState()
    {var d=Window();NativeNumericDialogs.Opened(d);NativeNumericDialogs.Tick(false);Require(ScreenReaderManager.Instance.Spoken.Count==0,"blocked frame announced");Memoria.Accessibility.AudioDescriptions.NativeAudioDescriptions.NarrationPlaying=true;NativeNumericDialogs.Tick(true);Require(ScreenReaderManager.Instance.Spoken.Count==0,"numeric reader overlapped narration");d.CurrentParser.VariableMessageValues[7]=4;d.CurrentParser.ParsedText="Bid: 400 Gil";Memoria.Accessibility.AudioDescriptions.NativeAudioDescriptions.NarrationPlaying=false;NativeNumericDialogs.Tick(true);Require(ScreenReaderManager.Instance.Spoken[0].Contains("400 gil"),"deferred output was stale");NativeNumericDialogs.Forget(d);NativeNumericDialogs.Tick(true);Require(ScreenReaderManager.Instance.Spoken.Count==1,"forgotten dialog announced");}
    public static void OpeningControlsSurviveInitialAndAutomaticNumericUpdates()
    {
        var d=Window();var reader=ScreenReaderManager.Instance;
        Memoria.Accessibility.AudioDescriptions.NativeAudioDescriptions.SpeakDialogue(d,"Adjust number. Select digit. Confirm. Cancel.");
        NativeNumericDialogs.Opened(d);NativeNumericDialogs.Tick(true);
        Require(reader.Spoken.Count==2 && !reader.Interrupts[1],"first numeric snapshot cut off the native opening controls");
        var status=new Dialog{TextId=201,CurrentParser=new TextParser{InitialText="Bid Status\nFunds: [NUMB=4] Gil\nPrice: [NUMB=1] Gil",ParsedText="Bid Status\nFunds: 1000 Gil\nItem: Shown item\nPrice: 100 Gil"}};
        status.gameObject.SetActive(true);status.CurrentParser.VariableMessageValues[1]=100;status.CurrentParser.VariableMessageValues[4]=1000;
        NativeNumericDialogs.Opened(status);NativeNumericDialogs.Tick(true);
        status.CurrentParser.VariableMessageValues[1]=200;status.CurrentParser.ParsedText="Bid Status\nFunds: 1000 Gil\nItem: Shown item\nPrice: 200 Gil";NativeNumericDialogs.Tick(true);
        status.CurrentParser.VariableMessageValues[1]=300;status.CurrentParser.ParsedText="Bid Status\nFunds: 1000 Gil\nItem: Shown item\nPrice: 300 Gil";NativeNumericDialogs.Tick(true);
        Require(reader.Spoken.Count==2,"automatic price updates interrupted or queued stale prices during controls");
        reader.Speaking=false;NativeNumericDialogs.Tick(true);
        Require(reader.Spoken.Count==3 && reader.Spoken[2].Contains("300 gil") && !reader.Interrupts[2],"latest automatic price was lost or interrupting");
        status.CurrentParser.VariableMessageValues[1]=400;status.CurrentParser.ParsedText="Bid Status\nFunds: 1000 Gil\nItem: Shown item\nPrice: 400 Gil";NativeNumericDialogs.Tick(true);
        Require(!reader.Interrupts[3],"an unavailable speech-busy report allows automatic prices to cut off controls");
        d.OverlayMessageNumber=6;NativeNumericDialogs.Tick(true);Require(reader.Interrupts[4],"deliberate digit navigation lost immediate feedback");
    }
}
