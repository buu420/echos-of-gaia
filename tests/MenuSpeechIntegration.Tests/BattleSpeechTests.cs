using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Memoria.Data;
using Memoria.ScreenReader;
using UnityEngine;

internal static class BattleSpeechTests
{
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    public static void SettledCommand()
    {
        var fixture = new Fixture();
        var fade = fixture.Hud.gameObject.AddComponent<HonoFading>();
        fixture.Hud.SetFadeForFixture(fade);
        fade.FadeOut(null);
        fixture.Hud.Loading = true;
        fixture.Focus(BattleHUD.CommandGroupButton, fixture.Attack);
        fixture.Tick();
        Read();
        fade.FinishWithoutCallback();
        fixture.Tick();
        Read("Zidane, Attack");
        fixture.Hud.Loading = false;
        fixture.Tick();
        Read("Zidane, Attack");
    }

    public static void ItemAndTargets()
    {
        var fixture = new Fixture();
        fixture.Attack.GetChild(0).AddComponent<UILocalize>().key = "Unpatched command";
        fixture.Focus(BattleHUD.CommandGroupButton, fixture.Attack);
        fixture.Tick();
        fixture.Focus(BattleHUD.CommandGroupButton, fixture.Item);
        fixture.Tick();
        fixture.Focus(BattleHUD.ItemGroupButton, fixture.Potion);
        fixture.Tick();
        fixture.Focus(BattleHUD.TargetGroupButton, fixture.PrisonCage);
        fixture.Tick();
        fixture.Focus(BattleHUD.TargetGroupButton, fixture.Garnet);
        fixture.Tick();
        fixture.Tick();
        Read("Zidane, Attack", "Item", "Potion", "Prison Cage", "Garnet");
        if (SpeechOutput.Interrupts.Any(interrupt => !interrupt))
            throw new Exception("Focus must replace old queued commands.");
        if (ButtonGroupState.ActiveButton != fixture.Garnet)
            throw new Exception("Speech changed the native target.");
    }

    public static void RecycledAbility()
    {
        var fixture = new Fixture();
        fixture.Focus(BattleHUD.AbilityGroupButton, fixture.Ability);
        var row = fixture.Ability.GetComponent<RecycleListItem>();
        row.Visible = false;
        fixture.Tick();
        Read();
        row.Visible = true;
        fixture.Tick();
        Read("Free Energy");
        row.ItemDataIndex++;
        fixture.Ability.GetChild(0).GetChild(0).GetComponent<UILabel>().rawText = "[C8C8C8]Tidal Flame";
        fixture.Tick();
        Read("Free Energy", "Tidal Flame");
    }

    public static void RetryCurrentFocus()
    {
        var fixture = new Fixture();
        SpeechOutput.Accept = false;
        fixture.Focus(BattleHUD.CommandGroupButton, fixture.Attack);
        fixture.Tick();
        fixture.Focus(BattleHUD.CommandGroupButton, fixture.Item);
        fixture.Tick();
        Read();
        if (SpeechOutput.Attempts != 1)
            throw new Exception("Rejected speech must use a bounded retry.");
        SpeechOutput.Accept = true;
        fixture.Advance(1.1f);
        fixture.Tick();
        fixture.Tick();
        Read("Zidane, Item");
    }

    public static void ForegroundAndVisibility()
    {
        var fixture = new Fixture();
        HonoInputManager.Activated = false;
        fixture.Focus(BattleHUD.CommandGroupButton, fixture.Attack);
        fixture.Tick();
        Read();
        HonoInputManager.Activated = true;
        fixture.Hud.CommandPanel.GetComponent<UIPanel>().alpha = 0;
        fixture.Focus(BattleHUD.CommandGroupButton, fixture.Item);
        fixture.Tick();
        Read();
        fixture.Hud.CommandPanel.GetComponent<UIPanel>().alpha = 1;
        fixture.Tick();
        Read("Zidane, Item");
        fixture.Manager.QuitScene.isShowQuitUI = true;
        fixture.Focus(BattleHUD.CommandGroupButton, fixture.Attack);
        fixture.Tick();
        Read("Zidane, Item");
        fixture.Manager.QuitScene.isShowQuitUI = false;
        fixture.Manager.BattleScene = null;
        fixture.Tick();
        Read("Zidane, Item");
        fixture.Manager.BattleScene = fixture.Hud;
        fixture.Tick();
        Read("Zidane, Item", "Zidane, Attack");
    }

    public static void AllyAndGroupTargets()
    {
        var fixture = new Fixture();
        fixture.Focus(BattleHUD.TargetGroupButton, fixture.Zidane);
        fixture.Tick();
        fixture.Hud.ShowHpForFixture(fixture.HpCurrent, fixture.HpMaximum);
        fixture.Focus(BattleHUD.TargetGroupButton, fixture.Garnet);
        fixture.Tick();
        fixture.Focus(BattleHUD.TargetGroupButton, fixture.Zidane);
        fixture.Tick();
        fixture.SetAllEnemies();
        fixture.Tick();
        fixture.Tick();
        Read("Zidane, 74 HP", "Garnet", "Zidane, 68 of 100 HP", "All enemies");
    }

    private static void Read(params string[] expected)
    {
        if (!expected.SequenceEqual(SpeechOutput.Text))
            throw new Exception($"Expected [{String.Join(" | ", expected)}], heard [{String.Join(" | ", SpeechOutput.Text)}].");
    }

    private sealed class Fixture
    {
        private DateTime _now = DateTime.UnixEpoch;
        public readonly UIManager Manager = new UIManager();
        public readonly BattleHUD Hud;
        public readonly GameObject Attack, Item, Potion, Ability, Zidane, PrisonCage, Garnet;
        public readonly UILabel HpCurrent, HpMaximum;

        public Fixture()
        {
            SpeechOutput.Text.Clear();
            SpeechOutput.Interrupts.Clear();
            SpeechOutput.Accept = true;
            SpeechOutput.CompatibilityAccept = false;
            SpeechOutput.Attempts = 0;
            ButtonGroupState.Reset();
            Time.realtimeSinceStartup = 0;
            Time.frameCount = 0;
            HonoInputManager.Activated = true;
            var reader = ScreenReaderManager.Instance;
            typeof(ScreenReaderManager).GetField("_clock", PrivateInstance).SetValue(reader, (Func<DateTime>)(() => _now));
            typeof(ScreenReaderManager).GetField("_backend", PrivateInstance).SetValue(reader, null);
            typeof(ScreenReaderManager).GetField("_nextAttempt", PrivateInstance).SetValue(reader, DateTime.MinValue);
            PersistenSingleton<UIManager>.Instance = Manager;
            Manager.State = UIManager.UIState.BattleHUD;
            Manager.BattleScene = Hud = new GameObject("Battle HUD").AddComponent<BattleHUD>();
            Hud.AllMenuPanel = Child(Hud.gameObject, "All Menu");
            Hud.CommandPanel = Child(Hud.AllMenuPanel, "Command Panel");
            Hud.CommandPanel.AddComponent<UIPanel>();
            Hud.ItemPanel = Child(Hud.AllMenuPanel, "Item Panel");
            Hud.AbilityPanel = Child(Hud.AllMenuPanel, "Ability Panel");
            Hud.TargetPanel = Child(Hud.AllMenuPanel, "Target Panel");
            Attack = Button(Hud.CommandPanel, "Attack", BattleHUD.CommandGroupButton);
            Item = Button(Hud.CommandPanel, "Item", BattleHUD.CommandGroupButton);
            var item = Child(Hud.ItemPanel, "Recycled item");
            var itemContent = Child(item, "Content");
            Child(itemContent, "Icon");
            Label(itemContent, "Potion");
            Label(itemContent, "2");
            Potion = item;
            var ability = Child(Hud.AbilityPanel, "Recycled ability");
            var abilityContent = Child(ability, "Content");
            Label(abilityContent, "Free Energy");
            Label(abilityContent, "6");
            Ability = ability;
            var playerList = Child(Hud.TargetPanel, "Players");
            var enemyList = Child(Hud.TargetPanel, "Enemies");
            Zidane = Button(playerList, "Zidane", BattleHUD.TargetGroupButton);
            PrisonCage = Button(enemyList, "Prison Cage", BattleHUD.TargetGroupButton);
            Garnet = Button(enemyList, "Garnet", BattleHUD.TargetGroupButton);
            var party = Child(Hud.AllMenuPanel, "Party");
            var actor = Label(party, "Zidane");
            var hp = Label(party, "74");
            var mp = Label(party, "42");
            mp.enabled = false;
            HpCurrent = Label(Hud.TargetPanel, "68");
            HpMaximum = Label(Hud.TargetPanel, "100");
            Hud.SetSpeechFixture(Potion, Ability, Zidane, PrisonCage, Garnet, actor, hp, mp);
        }

        public void Advance(float seconds)
        {
            _now = _now.AddSeconds(seconds);
            Time.realtimeSinceStartup += seconds;
        }

        public void Tick()
        {
            Time.frameCount++;
            typeof(BattleHUD).GetMethod("LateUpdate", PrivateInstance)?.Invoke(Hud, null);
        }

        public void Focus(string group, GameObject button)
        {
            Hud.CommandPanel.SetActive(group == BattleHUD.CommandGroupButton);
            Hud.ItemPanel.SetActive(group == BattleHUD.ItemGroupButton);
            Hud.AbilityPanel.SetActive(group == BattleHUD.AbilityGroupButton);
            Hud.TargetPanel.SetActive(group == BattleHUD.TargetGroupButton);
            ButtonGroupState.ActiveGroup = group;
            ButtonGroupState.ActiveButton = button;
        }

        public void SetAllEnemies() => Hud.SetAllEnemiesForFixture();

        private static GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.parent = parent.transform;
            return child;
        }

        private static UILabel Label(GameObject parent, string text)
        {
            var label = Child(parent, "Label").AddComponent<UILabel>();
            label.rawText = text;
            return label;
        }

        private static GameObject Button(GameObject parent, string name, string group)
        {
            var button = Child(parent, "Button");
            button.AddComponent<ButtonGroupState>().GroupName = group;
            Label(button, name);
            return button;
        }
    }
}

// Native battle code owns panel visibility, focus and row reuse. The tests exercise the real
// UIScene callback and BattleHUD speech code with those native presentation facts supplied here.
public partial class BattleHUD : UIScene
{
    public static Dictionary<BattleStatusId, string> DebuffIconNames = new Dictionary<BattleStatusId, string>
    {
        { BattleStatusId.Poison, "icon_poison" }, { BattleStatusId.Sleep, "icon_sleep" }
    };

    public const string CommandGroupButton = "Battle.Command", ItemGroupButton = "Battle.Item";
    public const string AbilityGroupButton = "Battle.Ability", TargetGroupButton = "Battle.Target";
    public GameObject AllMenuPanel, CommandPanel, ItemPanel, AbilityPanel, TargetPanel;
    public int CurrentPlayerIndex { get; private set; } = 1;
    private bool _commandEnable = true;
    private enum CursorGroup { Individual, AllPlayer, AllEnemy, All, None }
    private CursorGroup _cursorType;
    private readonly RecycleListPopulator _itemScrollList = new RecycleListPopulator();
    private readonly RecycleListPopulator _abilityScrollList = new RecycleListPopulator();
    private readonly List<int> _matchBattleIdPlayerList = new List<int> { 1 };
    private UI.PanelTarget _targetPanel;
    private UI.PanelParty _partyDetail;
    private UI.ContainerStatus _statusPanel;

    public void SetAllEnemiesForFixture() => _cursorType = CursorGroup.AllEnemy;
    public void SetFadeForFixture(HonoFading fade) => FadingComponent = fade;

    public void ShowHpForFixture(UILabel current, UILabel maximum)
    {
        _statusPanel.HP.IsActive = true;
        _statusPanel.HP.Array.Entries = new[]
        {
            new UI.ContainerStatus.ValueWidget { Value = new FixtureLabel(current), MaxValue = new FixtureLabel(maximum) }
        };
    }

    public void SetSpeechFixture(GameObject item, GameObject ability, GameObject ally, GameObject enemy,
        GameObject captive, UILabel actor, UILabel hp, UILabel mp)
    {
        item.AddComponent<RecycleListItem>().ListPopulator = _itemScrollList;
        ability.AddComponent<RecycleListItem>().ListPopulator = _abilityScrollList;
        var targets = new Memoria.Scenes.GONavigationButton[8];
        targets[0] = new Memoria.Scenes.GONavigationButton(ally);
        targets[4] = new Memoria.Scenes.GONavigationButton(enemy);
        targets[5] = new Memoria.Scenes.GONavigationButton(captive);
        _targetPanel = new UI.PanelTarget { AllTargets = targets };
        _partyDetail = new UI.PanelParty();
        _partyDetail.Characters.Entries = new[] { new UI.PanelParty.Character
        {
            PlayerId = 1, Name = new FixtureLabel(actor), HP = new FixtureLabel(hp), MP = new FixtureLabel(mp)
        } };
        _statusPanel = new UI.ContainerStatus();
    }

    private sealed class FixtureLabel
    {
        public readonly UILabel Label;
        public FixtureLabel(UILabel label) => Label = label;
    }

    private sealed class FixtureRows<T>
    {
        public T[] Entries = new T[0];
        public int Count => Entries.Length;
        public T this[int index] => Entries[index];
    }

    private sealed class UI
    {
        internal sealed class PanelTarget { public Memoria.Scenes.GONavigationButton[] AllTargets; }
        internal sealed class PanelParty
        {
            public readonly FixtureRows<Character> Characters = new FixtureRows<Character>();
            public Character FindCharacter(int player) => Characters.Entries.FirstOrDefault(row => row.PlayerId == player);
            internal sealed class Character { public int PlayerId; public FixtureLabel Name, HP, MP; }
        }
        internal sealed class ContainerStatus
        {
            public readonly PanelDetail HP = new PanelDetail(), MP = new PanelDetail();
            internal sealed class PanelDetail { public bool IsActive; public readonly FixtureRows<ValueWidget> Array = new FixtureRows<ValueWidget>(); }
            internal sealed class ValueWidget { public FixtureLabel Value, MaxValue; }
        }
    }
}

public static class HonoluluBattleMain { public const int EnemyStartIndex = 4; }
public sealed class RecycleListPopulator { }
public sealed class RecycleListItem : MonoBehaviour
{
    public RecycleListPopulator ListPopulator;
    public int ItemDataIndex;
    public bool Visible = true;
    public bool VerifyVisibility() => Visible;
}

namespace Memoria.Scenes
{
    internal sealed class GONavigationButton
    {
        public readonly GameObject GameObject;
        public readonly GOLabel Name;
        public GONavigationButton(GameObject go)
        {
            GameObject = go;
            Name = new GOLabel { Label = go.GetChild(0).GetComponent<UILabel>() };
        }
    }

    internal sealed class GOLabel { public UILabel Label; }
}
