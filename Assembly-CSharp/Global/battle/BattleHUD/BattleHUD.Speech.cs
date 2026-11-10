using Assets.Sources.Scripts.UI.Common;
using Memoria.Assets;
using Memoria.Prime;
using Memoria.Scenes;
using Memoria.ScreenReader;
using System;
using UnityEngine;

public partial class BattleHUD : UIScene
{
    private String _lastBattleSpeechGroup;
    private GameObject _lastBattleSpeechButton;
    private Int32 _lastBattleSpeechPlayer = -1;
    private Int32 _lastBattleSpeechRow;
    private String _lastBattleSpeechText;
    private Single _battleSpeechRetryAt;
    private Int32 _battleCommandSpeechLogs;

    protected override void AnnounceButtonForScreenReader(GameObject go)
    {
        String group = ButtonGroupState.ActiveGroup;
        if (IsBattleSpeechGroup(group))
            UpdateBattleMenuSpeech(group, go);
        else
            base.AnnounceButtonForScreenReader(go);
    }

    private void LateUpdate()
    {
        String group = ButtonGroupState.ActiveGroup;
        GameObject selection = ButtonGroupState.ActiveButton;
        if (group == CommandGroupButton)
        {
            // NGUI sets its selection first. ButtonGroupState may commit it later in the frame.
            GameObject current = UICamera.SelectedObjectForAccessibility;
            if (current != null && current.activeInHierarchy && CommandPanel != null &&
                current.transform.parent == CommandPanel.transform && current.GetComponent<ButtonGroupState>()?.GroupName == group)
                selection = current;
        }
        UpdateBattleMenuSpeech(group, selection);
    }

    private void UpdateBattleMenuSpeech(String group, GameObject selection)
    {
        UIManager ui = PersistenSingleton<UIManager>.Instance;
        ScreenReaderManager reader = ScreenReaderManager.Instance;
        // Native navigation can commit focus before the label is populated or the fade is done.
        // Read the settled HUD even if the generic selection notification was skipped.
        if (!_commandEnable || !IsBattleSpeechGroup(group) || !reader.Enabled || ui == null ||
            ui.State != UIManager.UIState.BattleHUD || ui.GetSceneFromState(ui.State) != this || ui.QuitScene.isShowQuitUI ||
            AllMenuPanel == null || !AllMenuPanel.activeInHierarchy ||
            (FadingComponent != null && FadingComponent.IsAnimating) || !HonoInputManager.ApplicationIsActivated())
        {
            ClearBattleSpeechFocus();
            return;
        }

        Single now = Time.realtimeSinceStartup;
        if (now < _battleSpeechRetryAt)
            return;
        try
        {
            ReadBattleMenuFocus(group, selection, reader, now);
        }
        catch (Exception error)
        {
            _battleSpeechRetryAt = now + 1f;
            Log.Error("Battle menu speech failed: " + error.Message);
        }
    }

    private void OnDisable()
    {
        ClearBattleSpeechFocus();
        _battleCommandSpeechLogs = 0;
        _battleSpeechRetryAt = 0f;
    }

    private void ClearBattleSpeechFocus()
    {
        _lastBattleSpeechGroup = null;
        _lastBattleSpeechButton = null;
        _lastBattleSpeechPlayer = -1;
        _lastBattleSpeechText = null;
    }

    private static Boolean IsBattleSpeechGroup(String group)
    {
        return group == CommandGroupButton || group == ItemGroupButton || group == AbilityGroupButton || group == TargetGroupButton;
    }

    private void ReadBattleMenuFocus(String group, GameObject selection, ScreenReaderManager reader, Single now)
    {
        Int32 row = -1;
        RecycleListItem listItem = null;
        String text;
        if (group == TargetGroupButton && _cursorType != CursorGroup.Individual)
        {
            if (!TargetPanel.activeInHierarchy)
                return;
            selection = null;
            switch (_cursorType)
            {
                case CursorGroup.AllPlayer: text = "All allies"; break;
                case CursorGroup.AllEnemy: text = "All enemies"; break;
                case CursorGroup.All: text = "All targets"; break;
                default: return;
            }
        }
        else
        {
            UILabel label = GetBattleFocusLabel(group, selection, out row, out listItem);
            text = ReadVisibleBattleLabel(label);
        }

        if (String.IsNullOrEmpty(text) || (group == _lastBattleSpeechGroup && selection == _lastBattleSpeechButton &&
            CurrentPlayerIndex == _lastBattleSpeechPlayer && row == _lastBattleSpeechRow && text == _lastBattleSpeechText))
            return;
        if (listItem != null && !listItem.VerifyVisibility())
            return;

        String announcement = CleanBattleSpeechText(text);
        if (String.IsNullOrEmpty(announcement))
            return;
        if (group == CommandGroupButton && CurrentPlayerIndex != _lastBattleSpeechPlayer && CurrentPlayerIndex >= 0)
        {
            String actor = CleanBattleSpeechText(ReadVisibleBattleLabel(_partyDetail.FindCharacter(CurrentPlayerIndex)?.Name.Label));
            if (!String.IsNullOrEmpty(actor))
                announcement = actor + ", " + announcement;
        }
        if (group == TargetGroupButton && row >= 0 && row < HonoluluBattleMain.EnemyStartIndex && row < _matchBattleIdPlayerList.Count)
            announcement = AppendDisplayedAllyPoints(announcement, _matchBattleIdPlayerList[row]);
        if (selection != null && IsButtonDisabled(selection))
            announcement += ", disabled";

        Boolean accepted = reader.TrySpeak(announcement, true);
        Boolean wasRetrying = _battleSpeechRetryAt > 0f;
        Boolean traceCommandChange = group == CommandGroupButton && _battleCommandSpeechLogs < 32;
        if (accepted ? wasRetrying || group != _lastBattleSpeechGroup || traceCommandChange : !wasRetrying)
        {
            if (group == CommandGroupButton)
                _battleCommandSpeechLogs++;
            Log.Message($"[FF9Access] Battle menu speech: group={group}; row={row}; player={CurrentPlayerIndex}; accepted={accepted}; backend={reader.BackendDiagnostic}");
        }
        _battleSpeechRetryAt = accepted ? 0f : now + 1f;
        _lastBattleSpeechText = accepted ? text : null;
        if (!accepted)
            return;

        _lastBattleSpeechGroup = group;
        _lastBattleSpeechButton = selection;
        _lastBattleSpeechPlayer = CurrentPlayerIndex;
        _lastBattleSpeechRow = row;
    }

    private UILabel GetBattleFocusLabel(String group, GameObject selection, out Int32 row, out RecycleListItem listItem)
    {
        row = -1;
        listItem = null;
        if (selection == null || !selection.activeInHierarchy)
            return null;
        if (group == CommandGroupButton)
        {
            if (CommandPanel == null || selection.transform.parent != CommandPanel.transform)
                return null;
            row = selection.transform.GetSiblingIndex();
            return GetBattleChildLabel(selection, 0);
        }
        if (group == TargetGroupButton)
        {
            for (Int32 i = 0; i < _targetPanel.AllTargets.Length; i++)
            {
                GONavigationButton target = _targetPanel.AllTargets[i];
                if (target != null && selection == target.GameObject)
                {
                    row = i;
                    return target.Name.Label;
                }
            }
            return null;
        }

        Boolean isItem = group == ItemGroupButton;
        listItem = selection.GetComponent<RecycleListItem>();
        if (listItem == null || listItem.ListPopulator != (isItem ? _itemScrollList : _abilityScrollList) ||
            listItem.ItemDataIndex < 0 || selection.transform.childCount == 0)
            return null;
        row = listItem.ItemDataIndex;
        // Match the layouts used by ItemListDetailWithIconHUD and ItemListDetailHUD.
        GameObject content = selection.GetChild(0);
        if (isItem && content.transform.childCount == 0)
            content = selection;
        return GetBattleChildLabel(content, isItem ? 1 : 0);
    }

    private static UILabel GetBattleChildLabel(GameObject parent, Int32 index)
    {
        return parent.transform.childCount > index ? parent.GetChild(index).GetComponent<UILabel>() : null;
    }

    private static String ReadVisibleBattleLabel(UILabel label)
    {
        if (label == null || !label.isActiveAndEnabled || label.CalculateFinalAlpha(Time.frameCount) <= 0f)
            return null;
        TextParser parser = label.Parser;
        parser.Parse(TextParser.ParseStep.VariableReplaceTags);
        return parser.ParsedText;
    }

    private static String CleanBattleSpeechText(String text)
    {
        return String.IsNullOrEmpty(text) ? null : FF9TextTool.RemoveOpCode(text).Trim();
    }

    private String AppendDisplayedAllyPoints(String announcement, Int32 player)
    {
        for (Int32 i = 0; i < _partyDetail.Characters.Count; i++)
        {
            UI.PanelParty.Character character = _partyDetail.Characters[i];
            if (character.PlayerId != player)
                continue;
            UI.ContainerStatus.ValueWidget hp = _statusPanel.HP.IsActive ? _statusPanel.HP.Array[i] : null;
            UI.ContainerStatus.ValueWidget mp = _statusPanel.MP.IsActive ? _statusPanel.MP.Array[i] : null;
            announcement = AppendDisplayedBattlePoints(announcement, "HP", hp?.Value.Label ?? character.HP.Label, hp?.MaxValue.Label);
            return AppendDisplayedBattlePoints(announcement, "MP", mp?.Value.Label ?? character.MP.Label, mp?.MaxValue.Label);
        }
        return announcement;
    }

    private static String AppendDisplayedBattlePoints(String announcement, String stat, UILabel current, UILabel maximum)
    {
        String value = CleanBattleSpeechText(ReadVisibleBattleLabel(current));
        if (String.IsNullOrEmpty(value))
            return announcement;
        String max = CleanBattleSpeechText(ReadVisibleBattleLabel(maximum));
        if (!String.IsNullOrEmpty(max))
            value += " of " + max;
        return announcement + ", " + value + " " + stat;
    }
}
