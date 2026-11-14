using System.Collections.Generic;
using Memoria.Accessibility.AudioDescriptions;

namespace FF9Access.Tests
{
    internal static class BlackjackSpeechTests
    {
        public static void HidesDealerHoleCardAndTotalUntilTheCardsRenderFaceUp()
        {
            var reader = new BlackjackSpeech();
            var speech = new List<string>();
            var display = new BlackjackDisplay { Mode = BlackjackMode.Choose, DealerTotal = "17" };
            reader.BeginRender();
            reader.DrawFaceDown(BlackjackHand.Dealer, 1);
            reader.DrawFaceUp(BlackjackHand.Dealer, 0, 8, 2);
            reader.Observe(display, 0, Speak);
            TestAssert.Equal("Dealer: ten of hearts, face-down card.", speech[0]);

            reader.BeginRender();
            reader.DrawFaceUp(BlackjackHand.Dealer, 1, 5, 3);
            reader.DrawFaceUp(BlackjackHand.Dealer, 0, 8, 2);
            reader.Observe(display, 1, Speak);
            TestAssert.Equal("Dealer: ten of hearts, seven of spades. Total 17.", speech[1]);

            bool Speak(string text) { speech.Add(text); return true; }
        }

        public static void SplitHandsKeepTheirOwnCardsTotalsAndVisibleResult()
        {
            var reader = new BlackjackSpeech();
            var speech = new List<string>();
            var display = new BlackjackDisplay { Mode = BlackjackMode.Watch, PlayerTotal = "19", PlayerAlternate = "9", SplitTotal = "16", SplitAlternate = "6" };
            DrawHands();
            reader.DrawResult(BlackjackHand.Player, BlackjackResult.Win, 0);
            reader.Observe(display, 0, Speak);
            TestAssert.Equal("Hand one: nine of diamonds. Total 19 or 9. Hand two: ace of clubs. Total 16 or 6.", speech[0]);

            DrawHands();
            reader.DrawResult(BlackjackHand.Player, BlackjackResult.Win, .05f);
            reader.Observe(display, 1, Speak);
            reader.Observe(display, 1.5, Speak);
            DrawHands();
            reader.DrawResult(BlackjackHand.Split, BlackjackResult.Win, .05f);
            reader.Observe(display, 2, Speak);
            TestAssert.Equal(3, speech.Count);
            TestAssert.Equal("Hand one wins.", speech[1]);
            TestAssert.Equal("Hand two wins.", speech[2]);

            void DrawHands()
            {
                reader.BeginRender();
                reader.DrawFaceUp(BlackjackHand.Split, 0, 12, 1);
                reader.DrawFaceUp(BlackjackHand.Player, 0, 7, 0);
            }
            bool Speak(string text) { speech.Add(text); return true; }
        }

        public static void WagerChangesSettleWhileFocusContinuesToSpeak()
        {
            var reader = new BlackjackSpeech();
            var speech = new List<string>();
            var display = new BlackjackDisplay { Mode = BlackjackMode.Wager, Bankroll = "1000", Wager = "10", Focus = "Deal" };
            reader.Observe(display, 0, Speak);
            display.Wager = "20";
            reader.Observe(display, .10, Speak);
            display.Wager = "30";
            display.Focus = "Increase wager";
            reader.Observe(display, .20, Speak);
            reader.Observe(display, .31, Speak);
            reader.Observe(display, .42, Speak);
            reader.Observe(display, .70, Speak);
            TestAssert.Equal(3, speech.Count);
            TestAssert.Equal("Bankroll 1000. Wager 10. Deal.", speech[0]);
            TestAssert.Equal("Increase wager.", speech[1]);
            TestAssert.Equal("Wager 30.", speech[2]);

            bool Speak(string text) { speech.Add(text); return true; }
        }

        public static void OmitsTheTotalAndAnyPlaceholderWhenACardIsOutsideTheRenderedView()
        {
            var reader = new BlackjackSpeech();
            var speech = new List<string>();
            var display = new BlackjackDisplay { Mode = BlackjackMode.Choose, DealerTotal = "17" };
            reader.BeginRender();
            reader.ExpectCards(BlackjackHand.Dealer, 2);
            reader.DrawFaceUp(BlackjackHand.Dealer, 0, 8, 2);
            reader.Observe(display, 0, Speak);
            TestAssert.Equal("Dealer: ten of hearts.", speech[0]);

            bool Speak(string text) { speech.Add(text); return true; }
        }

        public static void LatestFocusAndChoicesReplaceRejectedSpeech()
        {
            var reader = new BlackjackSpeech();
            var speech = new List<string>();
            var display = new BlackjackDisplay { Mode = BlackjackMode.Choose, Focus = "Stand", Choices = "Stand, Hit" };
            reader.Observe(display, 0, _ => false);
            display.Focus = "Hit";
            reader.Observe(display, .01, Speak);
            display.Focus = "Double";
            display.Choices = "Stand, Hit, Double";
            reader.Observe(display, .03, Speak);
            TestAssert.Equal(2, speech.Count);
            TestAssert.Equal("Choices: Stand, Hit. Hit.", speech[0]);
            TestAssert.Equal("Choices: Stand, Hit, Double. Double.", speech[1]);

            bool Speak(string text) { speech.Add(text); return true; }
        }

        public static void InitialDealIsReadTogetherWhenFaceUpCardsHaveLanded()
        {
            var reader = new BlackjackSpeech();
            var speech = new List<string>();
            var display = new BlackjackDisplay { Mode = BlackjackMode.Watch, Dealing = true };
            reader.BeginRender();
            reader.DrawFaceUp(BlackjackHand.Player, 0, 12, 1);
            reader.Observe(display, 0, Speak);
            reader.BeginRender();
            reader.DrawFaceUp(BlackjackHand.Player, 1, 5, 3);
            reader.DrawFaceUp(BlackjackHand.Player, 0, 12, 1);
            reader.Observe(display, .5, Speak);
            TestAssert.Equal(0, speech.Count);

            display.Dealing = false;
            display.PlayerTotal = "18";
            reader.Observe(display, 1, Speak);
            TestAssert.Equal("Your hand: ace of clubs, seven of spades. Total 18.", speech[0]);

            bool Speak(string text) { speech.Add(text); return true; }
        }

        public static void NoAnnouncingOldCardsBackAtWagerAndAnotherResultCanSpeak()
        {
            var reader = new BlackjackSpeech();
            var speech = new List<string>();
            var display = new BlackjackDisplay { Mode = BlackjackMode.Watch };
            reader.BeginRender();
            reader.DrawResult(BlackjackHand.Player, BlackjackResult.Draw, .5f);
            reader.Observe(display, 0, Speak);
            display.Mode = BlackjackMode.Wager;
            display.Wager = "10";
            reader.Observe(display, 1, Speak);
            display.Mode = BlackjackMode.Watch;
            reader.BeginRender();
            reader.DrawResult(BlackjackHand.Player, BlackjackResult.Draw, .5f);
            reader.Observe(display, 2, Speak);
            TestAssert.Equal(3, speech.Count);
            TestAssert.Equal("Draw.", speech[2]);

            bool Speak(string text) { speech.Add(text); return true; }
        }
    }
}
