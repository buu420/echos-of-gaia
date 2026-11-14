using System;
using System.Collections.Generic;
using Assets.Scripts.Common;
using Memoria.Accessibility.AudioDescriptions;
using Memoria.Assets;
using Memoria.ScreenReader;
using UnityEngine;

public class EndGameHUD : UIScene
{
    public override void Show(UIScene.SceneVoidDelegate afterFinished = null)
    {
        base.Show(afterFinished);
        this.hasDoubleAllowed = false;
        this.hasSplitAllowed = false;
        this.hasJustChangeToPlayState = true;
        this.blackjackSpeechActive = false;
        this.blackjackSpeechRetryAt = 0;
        this.changeWageAmountCounter = EndGameHUD.CHANGE_WAGE_AMOUNT_DURATION;
        this.lastChangeWageButtonState = Control.None;
        this.doubleButton = this.doubleButtonGo.GetComponent<UIButton>();
        this.doubleButtonLabel = this.doubleButtonGo.GetComponentInChildren<UILabel>();
        this.splitButton = this.splitButtonGo.GetComponent<UIButton>();
        this.splitButtonLabel = this.splitButtonGo.GetComponentInChildren<UILabel>();
        this.buttonGoList = new List<GameObject>
        {
            this.commitButtonGo,
            this.standButtonGo,
            this.hitButtonGo,
            this.doubleButtonGo,
            this.splitButtonGo,
            this.backButtonGo
        };
        this.BankRollLabel.rawText = EndGameMain.Instance.bankRoll.ToString();
        this.WageAmountLabel.rawText = EndGameMain.Instance.wager.ToString();
        foreach (GameObject go in this.buttonGoList)
            UIEventListener.Get(go).onClick += this.onClick;
        UIEventListener.Get(this.minusButtonGo).onPress += this.onMinusOrPlusPress;
        SceneDirector.FadeEventSetColor(FadeMode.Sub, Color.black);
        this.gameObject.GetChild(0).GetComponent<UILabel>().rawText = Localization.GetWithDefault("BlackJackBankroll");
        this.gameObject.GetChild(2).GetComponent<UILabel>().rawText = Localization.GetWithDefault("BlackJackWager");
        this.standButtonGo.GetChild(0).GetComponent<UILabel>().rawText = Localization.GetWithDefault("BlackJackStand");
        this.hitButtonGo.GetChild(0).GetComponent<UILabel>().rawText = Localization.GetWithDefault("BlackJackHit");
    }

    public override void Hide(UIScene.SceneVoidDelegate afterFinished = null)
    {
        base.NextSceneIsModal = false;
        UIScene.SceneVoidDelegate afterFinishedDelegate = delegate
        {
            PersistenSingleton<UIManager>.Instance.State = UIManager.UIState.Initial;
        };
        if (afterFinished != null)
            afterFinishedDelegate += afterFinished;
        base.Hide(afterFinishedDelegate);
        ButtonGroupState.DisableAllGroup(true);
    }

    public void OnLocalize()
    {
        if (!isActiveAndEnabled)
            return;
        this.gameObject.GetChild(0).GetComponent<UILabel>().rawText = Localization.GetWithDefault("BlackJackBankroll");
        this.gameObject.GetChild(2).GetComponent<UILabel>().rawText = Localization.GetWithDefault("BlackJackWager");
        this.standButtonGo.GetChild(0).GetComponent<UILabel>().rawText = Localization.GetWithDefault("BlackJackStand");
        this.hitButtonGo.GetChild(0).GetComponent<UILabel>().rawText = Localization.GetWithDefault("BlackJackHit");
        if (this.doubleButton.isEnabled)
            this.doubleButtonLabel.rawText = Localization.GetWithDefault("BlackJackDouble");
        if (this.splitButton.isEnabled)
            this.splitButtonLabel.rawText = Localization.GetWithDefault("BlackJackSplit");
    }

    private void onMinusOrPlusPress(GameObject go, Boolean isDown)
    {
        if (isDown)
        {
            if (go == this.minusButtonGo)
            {
                this.isDecreaseWager = true;
            }
            else if (go == this.plusButtonGo)
            {
                this.isIncreaseWager = true;
            }
        }
        else
        {
            this.isDecreaseWager = false;
            this.isIncreaseWager = false;
        }
        base.onPress(go, isDown);
    }

    public override Boolean OnKeyConfirm(GameObject go)
    {
        if (base.OnKeyConfirm(go))
        {
            if (go == this.commitButtonGo)
            {
                this.CommitWager();
            }
            else if (go == this.standButtonGo)
            {
                this.Stand();
            }
            else if (go == this.hitButtonGo)
            {
                this.Hit();
            }
            else if (go == this.doubleButtonGo)
            {
                this.Double();
            }
            else if (go == this.splitButtonGo)
            {
                this.Split();
            }
        }
        return true;
    }

    public override Boolean OnKeyCancel(GameObject go)
    {
        if (base.OnKeyCancel(go))
        {
            this.Exit();
        }
        return true;
    }

    public override Boolean OnKeySelect(GameObject go)
    {
        return false;
    }

    protected override void AnnounceButtonForScreenReader(GameObject go)
    {
        // The settled native focus is spoken together with this frame's game state.
    }

    private void LateUpdate()
    {
        try
        {
            var main = EndGameMain.Instance;
            if (main == null || main.endGame == null || main.endGameScore == null)
                return;

            BlackjackSpeech speech = main.endGame.Speech;
            var ui = UIManager.Instance;
            var camera = main.CanvasCamera;
            if (this.Loading || ui.State != UIManager.UIState.EndGame || ui.IsPause || !HonoInputManager.ApplicationIsActivated() ||
                !ScreenReaderManager.Instance.Enabled || camera == null || !camera.isActiveAndEnabled ||
                SceneDirector.IsScreenObscured || PersistenSingleton<SceneDirector>.Instance.IsFading)
            {
                if (this.blackjackSpeechActive)
                    speech.ResetAnnouncements();
                this.blackjackSpeechActive = false;
                return;
            }

            this.blackjackSpeechActive = true;
            EndGameDef.FF9EndingGameState state = main.endGame.GetEndingGameState();
            var display = new BlackjackDisplay
            {
                Dealing = state == EndGameDef.FF9EndingGameState.ENDGAME_STATE_DEAL,
                Bankroll = VisibleText(this.BankRollLabel),
                Wager = VisibleText(this.WageAmountLabel),
                DealerTotal = VisibleScore(this.dealerCardTotalLabel, main.endGameScore.dealerCardTotal),
                SplitTotal = VisibleScore(this.splitCardTotalLabel, main.endGameScore.splitCardTotal),
                SplitAlternate = VisibleScore(this.splitMinTotalLabel, main.endGameScore.splitMinTotal)
            };
            bool split = !String.IsNullOrEmpty(main.endGameScore.splitCardTotal);
            display.PlayerTotal = VisibleScore(split ? this.playerSplitCardTotalLabel : this.playerCardTotalLabel, main.endGameScore.playerCardTotal);
            display.PlayerAlternate = VisibleScore(split ? this.playerSplitMinTotalLabel : this.playerMinTotalLabel, main.endGameScore.playerMinTotal);

            GameObject selected = ButtonGroupState.ActiveButton;
            EndSys.ENDSYS_MODE_ENUM mode = EndSys.Endsys_GetMode();
            if (state == EndGameDef.FF9EndingGameState.ENDGAME_STATE_WAGER && mode == EndSys.ENDSYS_MODE_ENUM.ENDSYS_MODE_BET &&
                ButtonGroupState.ActiveGroup == BlackJackWagerGroupButton)
            {
                display.Mode = BlackjackMode.Wager;
                if (selected == this.minusButtonGo)
                    display.Focus = this.ChoiceLabel(selected, "Decrease wager");
                else if (selected == this.plusButtonGo)
                    display.Focus = this.ChoiceLabel(selected, "Increase wager");
                else if (selected == this.commitButtonGo)
                    display.Focus = this.ChoiceLabel(selected, "Deal");
            }
            else if (state == EndGameDef.FF9EndingGameState.ENDGAME_STATE_PROCESSPLAYER && mode == EndSys.ENDSYS_MODE_ENUM.ENDSYS_MODE_ACTION &&
                ButtonGroupState.ActiveGroup == BlackJackDealGroupButton)
            {
                display.Mode = BlackjackMode.Choose;
                var choices = new List<string>(4);
                this.ReadAction(this.standButtonGo, selected, choices, ref display);
                this.ReadAction(this.hitButtonGo, selected, choices, ref display);
                this.ReadAction(this.doubleButtonGo, selected, choices, ref display);
                this.ReadAction(this.splitButtonGo, selected, choices, ref display);
                display.Choices = String.Join(", ", choices.ToArray());
            }
            if (selected == this.backButtonGo)
                display.Focus = this.ChoiceLabel(selected, "Back");

            speech.Observe(display, Time.realtimeSinceStartup, this.SpeakCurrentBlackjackText);
        }
        catch (Exception)
        {
            // Optional speech cannot block the game HUD or its input handlers.
        }
    }

    private void ReadAction(GameObject go, GameObject selected, List<string> choices, ref BlackjackDisplay display)
    {
        string name = this.ChoiceLabel(go, null);
        if (name == null)
            return;
        choices.Add(name);
        if (go == selected)
            display.Focus = name;
    }

    private string ChoiceLabel(GameObject go, string fallback)
    {
        if (go == null || !go.activeInHierarchy)
            return null;
        var button = go.GetComponent<UIButton>();
        if (button == null || !button.isEnabled)
            return null;
        var label = go.GetComponentInChildren<UILabel>();
        string text = VisibleText(label);
        return String.IsNullOrEmpty(text) || text == "+" || text == "-" || text == "−" ? fallback : text;
    }

    private static string VisibleText(UILabel label)
    {
        if (label == null || !label.isActiveAndEnabled || label.CalculateFinalAlpha(Time.frameCount) <= 0)
            return null;
        string text = label.rawText == null ? null : label.rawText.Trim();
        return String.IsNullOrEmpty(text) || text == "-----" ? null : text;
    }

    private static string VisibleScore(UILabel label, string nativeScore)
    {
        string text = VisibleText(label);
        return text == nativeScore ? text : null;
    }

    private bool SpeakCurrentBlackjackText(string text)
    {
        float now = Time.realtimeSinceStartup;
        if (now < this.blackjackSpeechRetryAt)
            return false;
        bool accepted = ScreenReaderManager.Instance.TrySpeak(text, true);
        if (!accepted)
            this.blackjackSpeechRetryAt = now + .15f;
        return accepted;
    }

    private void Update()
    {
        if (EndGameMain.Instance == null)
            return;
        EndGameDef.FF9EndingGameState endingGameState = EndGameMain.Instance.endGame.GetEndingGameState();
        if (endingGameState == EndGameDef.FF9EndingGameState.ENDGAME_STATE_WAGER)
        {
            if (this.hasJustChangeToPlayState)
            {
                this.hasJustChangeToPlayState = false;
                ButtonGroupState.ActiveGroup = EndGameHUD.BlackJackWagerGroupButton;
            }
            Int64 bankRoll = EndGameMain.Instance.bankRoll;
            Int64 wager = EndGameMain.Instance.wager;
            if (this.prevBankRoll == -1L || this.prevBankRoll != bankRoll)
            {
                this.BankRollLabel.rawText = bankRoll.ToString();
                this.prevBankRoll = bankRoll;
            }
            if (this.prevWager == -1L || this.prevWager != wager)
            {
                this.WageAmountLabel.rawText = wager.ToString();
                this.prevWager = wager;
            }
            this.ValidateWager();
            if (UIManager.Input.GetKey(Control.LeftBumper) || (UIManager.Input.GetKey(Control.Confirm) && ButtonGroupState.ActiveButton == this.minusButtonGo) || this.isDecreaseWager)
            {
                if (this.lastChangeWageButtonState != Control.LeftBumper && this.lastChangeWageButtonState != Control.RightBumper)
                {
                    this.DecreaseWager();
                    this.changeWageAmountCounter = 0.276000023f;
                }
                else
                {
                    this.changeWageAmountCounter -= Time.deltaTime;
                    if (this.changeWageAmountCounter <= 0f && this.lastChangeWageButtonState != Control.RightBumper)
                    {
                        this.DecreaseWager();
                        if (this.changeWageAmountCounter < 0.0575f)
                            this.changeWageAmountCounter = 0f;
                        this.changeWageAmountCounter += EndGameHUD.CHANGE_WAGE_AMOUNT_DURATION;
                    }
                }
                this.lastChangeWageButtonState = Control.LeftBumper;
            }
            else if (this.lastChangeWageButtonState == Control.LeftBumper)
            {
                this.lastChangeWageButtonState = Control.None;
            }
            if (UIManager.Input.GetKey(Control.RightBumper) || (UIManager.Input.GetKey(Control.Confirm) && ButtonGroupState.ActiveButton == this.plusButtonGo) || this.isIncreaseWager)
            {
                if (this.lastChangeWageButtonState != Control.RightBumper && this.lastChangeWageButtonState != Control.LeftBumper)
                {
                    this.IncreaseWager();
                    this.changeWageAmountCounter = 0.276000023f;
                }
                else
                {
                    this.changeWageAmountCounter -= Time.deltaTime;
                    if (this.changeWageAmountCounter <= 0f && this.lastChangeWageButtonState != Control.LeftBumper)
                    {
                        this.IncreaseWager();
                        if (this.changeWageAmountCounter < 0.0575f)
                            this.changeWageAmountCounter = 0f;
                        this.changeWageAmountCounter += EndGameHUD.CHANGE_WAGE_AMOUNT_DURATION;
                    }
                }
                this.lastChangeWageButtonState = Control.RightBumper;
            }
            else if (this.lastChangeWageButtonState == Control.RightBumper)
            {
                this.lastChangeWageButtonState = Control.None;
            }
        }
        else
        {
            if (!this.hasJustChangeToPlayState)
                this.HasJustChangeToPlayState();
            this.hasJustChangeToPlayState = true;
            if (EndGameMain.Instance.endGame.ff9endingGameDoubleAllowed != this.hasDoubleAllowed)
            {
                this.SetEnableDoubleButton(EndGameMain.Instance.endGame.ff9endingGameDoubleAllowed);
                this.hasDoubleAllowed = EndGameMain.Instance.endGame.ff9endingGameDoubleAllowed;
            }
            if (EndGameMain.Instance.endGame.ff9endingGameSplitAllowed != this.hasSplitAllowed)
            {
                this.SetEnableSplitButton(EndGameMain.Instance.endGame.ff9endingGameSplitAllowed);
                this.hasSplitAllowed = EndGameMain.Instance.endGame.ff9endingGameSplitAllowed;
            }
        }
        this.dealerCardTotalLabel.rawText = EndGameMain.Instance.endGameScore.dealerCardTotal;
        if (String.IsNullOrEmpty(EndGameMain.Instance.endGameScore.splitCardTotal))
        {
            this.playerSplitCardTotalLabel.rawText = String.Empty;
            this.playerSplitMinTotalLabel.rawText = String.Empty;
            this.splitCardTotalLabel.rawText = String.Empty;
            this.splitMinTotalLabel.rawText = String.Empty;
            this.playerCardTotalLabel.rawText = EndGameMain.Instance.endGameScore.playerCardTotal;
            this.playerMinTotalLabel.rawText = EndGameMain.Instance.endGameScore.playerMinTotal;
        }
        else
        {
            this.playerCardTotalLabel.rawText = String.Empty;
            this.playerMinTotalLabel.rawText = String.Empty;
            this.playerSplitCardTotalLabel.rawText = EndGameMain.Instance.endGameScore.playerCardTotal;
            this.playerSplitMinTotalLabel.rawText = EndGameMain.Instance.endGameScore.playerMinTotal;
            this.splitCardTotalLabel.rawText = EndGameMain.Instance.endGameScore.splitCardTotal;
            this.splitMinTotalLabel.rawText = EndGameMain.Instance.endGameScore.splitMinTotal;
        }
    }

    private void SetEnableDoubleButton(Boolean isEnable)
    {
        if (isEnable)
        {
            this.doubleButtonLabel.rawText = Localization.GetWithDefault("BlackJackDouble");
            this.doubleButton.isEnabled = true;
        }
        else
        {
            this.doubleButtonLabel.rawText = "-----";
            this.doubleButton.isEnabled = false;
            ButtonGroupState.ActiveButton = this.standButtonGo;
        }
    }

    private void SetEnableSplitButton(Boolean isEnable)
    {
        if (isEnable)
        {
            this.splitButtonLabel.rawText = Localization.GetWithDefault("BlackJackSplit");
            this.splitButton.isEnabled = true;
        }
        else
        {
            this.splitButtonLabel.rawText = "-----";
            this.splitButton.isEnabled = false;
            ButtonGroupState.ActiveButton = this.standButtonGo;
        }
    }

    private void HasJustChangeToPlayState()
    {
        ButtonGroupState.ActiveGroup = EndGameHUD.BlackJackDealGroupButton;
        ButtonGroupState.ActiveButton = this.standButtonGo;
        if (!this.hasDoubleAllowed)
        {
            this.doubleButtonLabel.rawText = "-----";
            this.doubleButton.isEnabled = false;
        }
        if (!this.hasSplitAllowed)
        {
            this.splitButtonLabel.rawText = "-----";
            this.splitButton.isEnabled = false;
        }
    }

    private void ValidateWager()
    {
        EndGameMain.Instance.ValidateWager();
    }

    private void IncreaseWager()
    {
        EndGameMain.Instance.IncreaseWager();
    }

    private void DecreaseWager()
    {
        EndGameMain.Instance.DecreaseWager();
    }

    private void CommitWager()
    {
        if (EndSys.Endsys_GetMode() == EndSys.ENDSYS_MODE_ENUM.ENDSYS_MODE_BET)
        {
            EndGameMain.Instance.endGame.ff9endingGameWager = EndGameMain.Instance.wager;
            EndSys.Endsys_SetMode(EndSys.ENDSYS_MODE_ENUM.ENDSYS_MODE_QUIT);
        }
    }

    private void Stand()
    {
        if (EndSys.Endsys_GetMode() == EndSys.ENDSYS_MODE_ENUM.ENDSYS_MODE_ACTION)
        {
            EndAct.Endact_SetMode(EndAct.ENDACT_MODE_ENUM.ENDACT_MODE_STAND);
            EndSys.Endsys_SetMode(EndSys.ENDSYS_MODE_ENUM.ENDSYS_MODE_QUIT);
        }
    }

    private void Hit()
    {
        if (EndSys.Endsys_GetMode() == EndSys.ENDSYS_MODE_ENUM.ENDSYS_MODE_ACTION)
        {
            EndAct.Endact_SetMode(EndAct.ENDACT_MODE_ENUM.ENDACT_MODE_HIT);
            EndSys.Endsys_SetMode(EndSys.ENDSYS_MODE_ENUM.ENDSYS_MODE_QUIT);
        }
    }

    private void Double()
    {
        if (EndSys.Endsys_GetMode() == EndSys.ENDSYS_MODE_ENUM.ENDSYS_MODE_ACTION)
        {
            EndAct.Endact_SetMode(EndAct.ENDACT_MODE_ENUM.ENDACT_MODE_DOUBLE);
            EndSys.Endsys_SetMode(EndSys.ENDSYS_MODE_ENUM.ENDSYS_MODE_QUIT);
        }
    }

    private void Split()
    {
        if (EndSys.Endsys_GetMode() == EndSys.ENDSYS_MODE_ENUM.ENDSYS_MODE_ACTION)
        {
            EndAct.Endact_SetMode(EndAct.ENDACT_MODE_ENUM.ENDACT_MODE_SPLIT);
            EndSys.Endsys_SetMode(EndSys.ENDSYS_MODE_ENUM.ENDSYS_MODE_QUIT);
        }
    }

    private void Exit()
    {
        EndGameMain.Instance.endGame.FF9SFX_Play(103u);
        FF9Snd.ff9endsnd_song_vol_intpl(156, 60, 0);
        SoundLib.GetAllSoundDispatchPlayer().StopCurrentSong(60);
        SceneDirector.Replace(PersistenSingleton<SceneDirector>.Instance.LastScene, SceneTransition.FadeOutToBlack_FadeIn, true);
    }

    private const Single CHANGE_WAGE_AMOUNT_DURATION = 0.115f;
    private const String BlackJackWagerGroupButton = "BlackJack.Buttons.Wager";
    private const String BlackJackDealGroupButton = "BlackJack.Buttons.Deal";

    public UILabel WageAmountLabel;

    public UILabel BankRollLabel;

    private Int64 prevBankRoll = -1L;

    private Int64 prevWager = -1L;

    private Boolean hasDoubleAllowed;

    private Boolean hasSplitAllowed;

    private Boolean hasJustChangeToPlayState;

    private Single changeWageAmountCounter;

    private Control lastChangeWageButtonState;

    private List<GameObject> buttonGoList;

    private UIButton doubleButton;

    private UILabel doubleButtonLabel;

    private UIButton splitButton;

    private UILabel splitButtonLabel;

    public GameObject minusButtonGo;

    public GameObject plusButtonGo;

    public GameObject commitButtonGo;

    public GameObject standButtonGo;

    public GameObject hitButtonGo;

    public GameObject doubleButtonGo;

    public GameObject splitButtonGo;

    public GameObject backButtonGo;

    public UILabel dealerCardTotalLabel;

    public UILabel playerCardTotalLabel;

    public UILabel playerMinTotalLabel;

    public UILabel playerSplitCardTotalLabel;

    public UILabel playerSplitMinTotalLabel;

    public UILabel splitCardTotalLabel;

    public UILabel splitMinTotalLabel;

    private Boolean isDecreaseWager;

    private Boolean isIncreaseWager;

    private bool blackjackSpeechActive;

    private float blackjackSpeechRetryAt;
}
