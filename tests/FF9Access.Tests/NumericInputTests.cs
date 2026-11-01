using System.Collections.Generic;
using Memoria.Accessibility.Navigation;
namespace FF9Access.Tests
{
    internal static class NumericInputTests
    {
        private static NumericInputObservation Bid(){return new NumericInputObservation{Field=909,TextId=203,WindowIdentity=1,Generation=1,Visible=true,RenderReady=true,Foreground=true,Template="Bid: [FEED=1][NUMB=5][NUMB=6][NUMB=7]00 Gil",RenderedText="Bid: 100 Gil",SelectedVariable=7,Values=new Dictionary<int,int>{{5,0},{6,0},{7,1}}};}
        public static void BidReadsTheDisplayedAmountAndHighlightedPlace()
        {var a=new NumericInputReader().Observe(Bid());TestAssert.True(a.Text.Contains("Bid 100 gil") && a.Text.Contains("Hundreds digit 1"));}
        public static void IdleValuesAreSilentAndDigitChangesDoNotRepeatInstructions()
        {var r=new NumericInputReader();var o=Bid();var a=r.Observe(o);r.Acknowledge(a,true);TestAssert.True(r.Observe(o)==null);o.SelectedVariable=6;a=r.Observe(o);TestAssert.Equal("Thousands digit 0.",a.Text);}
        public static void RejectedSpeechRetriesTheLatestShownValue()
        {var r=new NumericInputReader();var o=Bid();var old=r.Observe(o);r.Acknowledge(old,false);o.Values[7]=2;var current=r.Observe(o);r.Acknowledge(old,true);TestAssert.True(r.Observe(o)!=null);r.Acknowledge(current,true);TestAssert.True(r.Observe(o)==null);TestAssert.True(current.Text.Contains("200 gil"));}
        public static void HiddenOverlayAndUnrenderedSnapshotsAreNotRead()
        {var r=new NumericInputReader();var o=Bid();o.Visible=false;TestAssert.True(r.Observe(o)==null);o.Visible=true;o.IsOverlay=true;TestAssert.True(r.Observe(o)==null);o.IsOverlay=false;o.RenderReady=false;TestAssert.True(r.Observe(o)==null);o.RenderReady=true;o.Foreground=false;TestAssert.True(r.Observe(o)==null);}
        public static void MissingOrNonDigitVariablesCannotInventAnAmount()
        {var o=Bid();o.Values.Remove(6);TestAssert.True(new NumericInputReader().Observe(o)==null);o=Bid();o.Values[5]=10;TestAssert.True(new NumericInputReader().Observe(o)==null);}
        public static void WrongMapTemplateAndDialogIdStayUnsupported()
        {var o=Bid();o.Field=103;TestAssert.True(new NumericInputReader().Observe(o)==null);o=Bid();o.TextId=204;TestAssert.True(new NumericInputReader().Observe(o)==null);o=Bid();o.Template="Other values [NUMB=7][NUMB=5]";TestAssert.True(new NumericInputReader().Observe(o)==null);}
        public static void QuantityUsesOnlyTheRenderedDigitsAndRemainingCount()
        {var o=Bid();o.Field=2800;o.TextId=105;o.Template="Place how many? [NUMB=6][NUMB=7]. [NUMB=5] remaining";o.Values[5]=9;o.Values[6]=2;o.Values[7]=3;o.SelectedVariable=6;var a=new NumericInputReader().Observe(o);TestAssert.True(a.Text.Contains("Amount 23") && a.Text.Contains("9 remaining") && a.Text.Contains("Tens digit 2"));}
        public static void NewWindowAndRecoveredOutputReannounceCurrentState()
        {var r=new NumericInputReader();var o=Bid();r.Acknowledge(r.Observe(o),true);o.WindowIdentity=2;TestAssert.True(r.Observe(o).Text.Contains("Bid"));r.Acknowledge(r.Observe(o),true);o.OutputGeneration++;TestAssert.True(r.Observe(o).Text.Contains("Bid"));}
        public static void UnselectedDigitCanStillReportTheShownAmount()
        {var o=Bid();o.SelectedVariable=-1;var a=new NumericInputReader().Observe(o);TestAssert.True(a.Text.Contains("100 gil") && !a.Text.Contains("digit"));}
        public static void AuctionStatusTracksShownItemPriceAndFundsWithoutRawItemLookup()
        {var o=Bid();o.TextId=201;o.Template="Bid Status\nFunds: [NUMB=4] Gil\nItem: [ITEM=0]\nPrice: [NUMB=1] Gil";o.RenderedText="Bid Status\nFunds: 900 Gil\nItem: Une's Mirror\nPrice: 100 Gil";o.Values=new Dictionary<int,int>{{1,100},{4,900},{0,999999}};var r=new NumericInputReader();var a=r.Observe(o);TestAssert.True(a.Text.Contains("Une's Mirror") && a.Text.Contains("Current price 100") && a.Text.Contains("Funds 900") && !a.Text.Contains("999999"));r.Acknowledge(a,true);o.RenderedText=o.RenderedText.Replace("Une's Mirror","Doga's Artifact");TestAssert.True(r.Observe(o).Text.Contains("Doga's Artifact"));}
    }
}
