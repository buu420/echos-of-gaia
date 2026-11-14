using System;
using System.Collections.Generic;

namespace Memoria.Accessibility.AudioDescriptions
{
    internal enum BlackjackHand { Dealer, Player, Split }
    internal enum BlackjackMode { Watch, Wager, Choose }
    internal enum BlackjackResult { None, Win, Loss, Draw }

    internal struct BlackjackDisplay
    {
        internal BlackjackMode Mode;
        internal string Bankroll, Wager, Focus, Choices;
        internal string DealerTotal, PlayerTotal, PlayerAlternate, SplitTotal, SplitAlternate;
        internal bool Dealing;
    }

    internal sealed class BlackjackSpeech
    {
        private const int Gap = -2;
        private const int FaceDown = -1;
        private const double WagerSettleSeconds = .2;
        private static readonly string[] Ranks = { "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "jack", "queen", "king", "ace" };
        private static readonly string[] Suits = { "diamonds", "clubs", "hearts", "spades" };

        private sealed class RenderedHand
        {
            internal readonly int[] Cards = new int[22];
            internal int Count;
            internal BlackjackResult Result;
        }

        private readonly RenderedHand[] _hands = { new RenderedHand(), new RenderedHand(), new RenderedHand() };
        private readonly string[] _previousHands = new string[3];
        private readonly BlackjackResult[] _announcedResults = new BlackjackResult[3];
        private BlackjackMode _mode;
        private string _lastWager, _spokenWager, _spokenBankroll, _lastFocus, _lastChoices;
        private double _wagerChangedAt;

        internal void BeginRender()
        {
            foreach (RenderedHand hand in _hands)
            {
                hand.Count = 0;
                hand.Result = BlackjackResult.None;
            }
        }

        internal void ExpectCards(BlackjackHand hand, int count)
        {
            RenderedHand rendered = _hands[(int)hand];
            count = Math.Min(count, rendered.Cards.Length);
            while (rendered.Count < count)
                rendered.Cards[rendered.Count++] = Gap;
        }

        internal void DrawFaceDown(BlackjackHand hand, int position)
        {
            DrawCard(hand, position, FaceDown);
        }

        internal void DrawFaceUp(BlackjackHand hand, int position, int rank, int suit)
        {
            if (rank >= 0 && rank < Ranks.Length && suit >= 0 && suit < Suits.Length)
                DrawCard(hand, position, rank * Suits.Length + suit);
        }

        private void DrawCard(BlackjackHand hand, int position, int card)
        {
            RenderedHand rendered = _hands[(int)hand];
            if (position < 0 || position >= rendered.Cards.Length)
                return;
            ExpectCards(hand, position + 1);
            rendered.Cards[position] = card;
        }

        internal void DrawResult(BlackjackHand hand, BlackjackResult result, float alpha)
        {
            if (alpha > 0 && hand != BlackjackHand.Dealer)
                _hands[(int)hand].Result = result;
        }

        internal void ResetAnnouncements()
        {
            _mode = BlackjackMode.Watch;
            _lastWager = _spokenWager = _spokenBankroll = _lastFocus = _lastChoices = null;
            Array.Clear(_previousHands, 0, _previousHands.Length);
            Array.Clear(_announcedResults, 0, _announcedResults.Length);
        }

        internal void Observe(BlackjackDisplay display, double now, Func<string, bool> speak)
        {
            if (display.Mode == BlackjackMode.Wager && _mode != BlackjackMode.Wager)
                ResetAnnouncements();

            if (!String.Equals(_lastWager, display.Wager))
            {
                _lastWager = display.Wager;
                _wagerChangedAt = now;
            }

            var messages = new List<string>();
            if (!String.IsNullOrEmpty(display.Bankroll) && display.Bankroll != _spokenBankroll)
                messages.Add("Bankroll " + display.Bankroll + ".");

            bool wagerReady = display.Mode == BlackjackMode.Wager && !String.IsNullOrEmpty(display.Wager) &&
                (_mode != BlackjackMode.Wager || now - _wagerChangedAt >= WagerSettleSeconds);
            if (wagerReady && display.Wager != _spokenWager)
                messages.Add("Wager " + display.Wager + ".");

            bool split = _hands[(int)BlackjackHand.Split].Count > 0 || _hands[(int)BlackjackHand.Split].Result != BlackjackResult.None;
            string dealer = null, player = null, second = null;
            if (display.Mode != BlackjackMode.Wager && !display.Dealing)
            {
                dealer = DescribeHand(BlackjackHand.Dealer, "Dealer", display.DealerTotal, null);
                player = DescribeHand(BlackjackHand.Player, split ? "Hand one" : "Your hand", display.PlayerTotal, display.PlayerAlternate);
                second = DescribeHand(BlackjackHand.Split, "Hand two", display.SplitTotal, display.SplitAlternate);
                AppendChange(messages, BlackjackHand.Dealer, dealer);
                AppendChange(messages, BlackjackHand.Player, player);
                AppendChange(messages, BlackjackHand.Split, second);
                AppendResult(messages, BlackjackHand.Player, split ? "Hand one" : null);
                AppendResult(messages, BlackjackHand.Split, "Hand two");
            }

            if (display.Mode == BlackjackMode.Choose && !String.IsNullOrEmpty(display.Choices) &&
                (_mode != display.Mode || display.Choices != _lastChoices))
                messages.Add("Choices: " + display.Choices + ".");
            if (!String.IsNullOrEmpty(display.Focus) &&
                (_mode != display.Mode || display.Focus != _lastFocus))
                messages.Add(display.Focus + ".");

            if (messages.Count > 0 && !speak(String.Join(" ", messages.ToArray())))
                return;

            _mode = display.Mode;
            _spokenBankroll = display.Bankroll;
            if (wagerReady)
                _spokenWager = display.Wager;
            _lastFocus = display.Focus;
            _lastChoices = display.Choices;
            _previousHands[0] = dealer;
            _previousHands[1] = player;
            _previousHands[2] = second;
            if (display.Mode != BlackjackMode.Wager && !display.Dealing)
                for (int i = 1; i < _hands.Length; i++)
                    if (_hands[i].Result != BlackjackResult.None)
                        _announcedResults[i] = _hands[i].Result;
        }

        private string DescribeHand(BlackjackHand hand, string name, string total, string alternate)
        {
            RenderedHand rendered = _hands[(int)hand];
            if (rendered.Count == 0)
                return null;

            var cards = new List<string>(rendered.Count);
            bool complete = true;
            for (int i = 0; i < rendered.Count; i++)
            {
                int card = rendered.Cards[i];
                complete &= card >= 0;
                if (card != Gap)
                    cards.Add(card == FaceDown ? "face-down card" : Ranks[card / Suits.Length] + " of " + Suits[card % Suits.Length]);
            }
            if (cards.Count == 0)
                return null;

            string text = name + ": " + String.Join(", ", cards.ToArray()) + ".";
            if (complete && !String.IsNullOrEmpty(total))
                text += " Total " + total + (!String.IsNullOrEmpty(alternate) ? " or " + alternate : "") + ".";
            return text;
        }

        private void AppendChange(List<string> messages, BlackjackHand hand, string text)
        {
            if (text != null && text != _previousHands[(int)hand])
                messages.Add(text);
        }

        private void AppendResult(List<string> messages, BlackjackHand hand, string name)
        {
            BlackjackResult result = _hands[(int)hand].Result;
            if (result == BlackjackResult.None || result == _announcedResults[(int)hand])
                return;

            switch (result)
            {
                case BlackjackResult.Win: messages.Add(name == null ? "Win." : name + " wins."); break;
                case BlackjackResult.Loss: messages.Add(name == null ? "Loss." : name + " loses."); break;
                case BlackjackResult.Draw: messages.Add(name == null ? "Draw." : name + " draws."); break;
            }
        }
    }
}
