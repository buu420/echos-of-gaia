using System;
using Memoria.Accessibility.Navigation;
using Memoria.Assets;
using Memoria.ScreenReader;
internal static class AuctionPanelTests
{
    public static void ConcurrentShownPriceAndBidAreOneCurrentAnnouncement()
    {
        NativeNumericDialogs.Reset();NavigationLifecycleBridge.Generation++;FF9StateSystem.Common.FF9.fldMapNo=909;ScreenReaderManager.Instance.Enabled=ScreenReaderManager.Instance.Accept=true;ScreenReaderManager.Instance.Spoken.Clear();Memoria.Accessibility.AudioDescriptions.NativeAudioDescriptions.NarrationPlaying=false;
        var status=new Dialog{TextId=201,CurrentParser=new TextParser{InitialText="Bid Status\nFunds: [NUMB=4] Gil\nItem: [ITEM=0]\nPrice: [NUMB=1] Gil",ParsedText="Bid Status\nFunds: 900 Gil\nItem: Une's Mirror\nPrice: 100 Gil"}};
        status.gameObject.SetActive(true);status.CurrentParser.VariableMessageValues[1]=100;status.CurrentParser.VariableMessageValues[4]=900;
        var bid=new Dialog{CurrentParser=new TextParser{InitialText="Bid: [NUMB=5][NUMB=6][NUMB=7]00 Gil",ParsedText="Bid: 200 Gil"}};bid.gameObject.SetActive(true);bid.CurrentParser.VariableMessageValues[5]=0;bid.CurrentParser.VariableMessageValues[6]=0;bid.CurrentParser.VariableMessageValues[7]=2;
        NativeNumericDialogs.Opened(status);NativeNumericDialogs.Opened(bid);NativeNumericDialogs.Tick(true);
        var spoken=ScreenReaderManager.Instance.Spoken;if(spoken.Count!=1 || !spoken[0].Contains("Current price 100") || !spoken[0].Contains("Bid 200") || !spoken[0].Contains("Une's Mirror"))throw new Exception("one visible panel interrupted or hid the other");
    }
}
