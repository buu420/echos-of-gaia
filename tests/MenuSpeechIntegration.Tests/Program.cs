using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assets.Sources.Scripts.UI.Common;
using Memoria.Assets;
using Memoria.ScreenReader;
using UnityEngine;

internal static class Program
{
    private static int Main()
    {
        var cases = new (string Name, Action Run)[]
        {
            ("opening waits for fade and character tween", OpeningWaitsForBoth),
            ("character tween can complete before fade", CharacterFirst),
            ("all seven ready submenu selections read native labels", ReadySubmenus),
            ("displayed patched label wins over localization key", DisplayedLabel),
            ("disabled submenu reflects game availability", DisabledSubmenu),
            ("selection suppressed by loading is replayed once ready", SuppressedSelection),
            ("character row reads shown level HP MP stones and row", CharacterDetails),
            ("order target reads its sibling character content", OrderDetails),
            ("empty character row is announced without stale labels", EmptyCharacter),
            ("hidden character values are withheld", HiddenCharacterValues),
            ("only shown recognized status icons are announced", VisibleStatusIcons),
            ("transparent labels and status icons are withheld", TransparentValues),
            ("closing cancels late opening callbacks", ClosingCancels),
            ("leaving the current scene cancels pending speech", SceneChangeCancels),
            ("reopening ignores callbacks from the previous opening", Reopening),
            ("return from submenu reads its native selected item", ReturnFromSubmenu),
            ("world opening uses the same menu lifecycle", WorldOpening),
            ("unrelated and inactive selections stay silent", UnrelatedSelection),
            ("rejected opening speech retries after backend cooldown", RejectedOpening),
            ("rejected primary opening reads once through compatibility", CompatibilityOpening),
            ("retry reads latest selection without per-frame retry logs", RetryLatestSelection),
            ("hide cancels rejected delivery", HideCancelsRetry),
            ("scene change cancels rejected delivery", SceneChangeCancelsRetry),
            ("background opening waits for activation", BackgroundOpening),
            ("background retry waits and returns latest selection", BackgroundRetry),
            ("transparent opening label retries when its parent becomes visible", TransparentOpening),
            ("transparent empty row stays silent until shown", TransparentEmptyRow),
        };
        int failed=0;
        foreach (var item in cases)
        {
            try { item.Run(); Console.WriteLine("PASS " + item.Name); }
            catch(Exception error) { failed++; Console.WriteLine("FAIL " + item.Name + ": " + error.Message); }
        }
        Console.WriteLine($"{cases.Length-failed}/{cases.Length} menu speech integration scenarios passed.");
        return failed==0?0:1;
    }

    private static void OpeningWaitsForBoth()
    {
        var f=new Fixture(); f.Menu.Show(); f.Fade.CompleteShow();
        Equal(0,SpeechOutput.Text.Count,"Speech must wait while character bars still move");
        f.Tween.CompleteShow(); f.Tick();
        Sequence("Item"); f.Tick(); Sequence("Item");
    }
    private static void CharacterFirst()
    {
        var f=new Fixture(); f.Menu.Show(); f.Tween.CompleteShow(); f.Tick();
        Equal(0,SpeechOutput.Text.Count,"No selection until fade finishes");
        f.Fade.CompleteShow(); f.Tick(); Sequence("Item");
    }
    private static void ReadySubmenus()
    {
        var f=new Fixture(); f.Ready(); SpeechOutput.Text.Clear();
        foreach(var name in Fixture.MenuNames) ButtonGroupState.ActiveButton=f.Buttons[name];
        Sequence("Item","Ability","Equip","Status","Order","Card","Config");
    }
    private static void DisplayedLabel()
    {
        var f=new Fixture(); f.Ready(); SpeechOutput.Text.Clear();
        f.Buttons["Item"].GetComponentInChildren<UILabel>().rawText="[C8C8C8]Objets";
        ButtonGroupState.ActiveButton=f.Buttons["Item"];
        Sequence("Objets");
    }
    private static void DisabledSubmenu()
    {
        var f=new Fixture(); f.Menu.EnabledSubMenus.Add("Item"); f.Ready(); SpeechOutput.Text.Clear();
        ButtonGroupState.ActiveButton=f.Buttons["Equip"]; Sequence("Equip, disabled");
    }
    private static void SuppressedSelection()
    {
        var f=new Fixture(); f.Ready(); SpeechOutput.Text.Clear();
        f.Menu.Loading=true; ButtonGroupState.ActiveButton=f.Buttons["Status"]; f.Tick();
        Equal(0,SpeechOutput.Text.Count,"No speech while loading");
        f.Menu.Loading=false; f.Tick(); Sequence("Status"); f.Tick(); Sequence("Status");
    }
    private static void CharacterDetails()
    {
        var f=new Fixture(); f.Ready(); SpeechOutput.Text.Clear(); f.SelectCharacter(0);
        Sequence("Zidane, Level 5, HP 77 of 120, MP 9 of 20, Magic stones 3 of 8, front row");
    }
    private static void OrderDetails()
    {
        var f=new Fixture(); f.Ready(); SpeechOutput.Text.Clear();
        ButtonGroupState.ActiveGroup="MainMenu.Order"; ButtonGroupState.ActiveButton=f.OrderButtons[0];
        Sequence("Zidane, Level 5, HP 77 of 120, MP 9 of 20, Magic stones 3 of 8, front row");
    }
    private static void EmptyCharacter()
    {
        var f=new Fixture(); f.Ready(); SpeechOutput.Text.Clear(); f.SelectCharacter(1);
        Sequence("Empty slot");
    }
    private static void HiddenCharacterValues()
    {
        var f=new Fixture(); f.Ready(); SpeechOutput.Text.Clear();
        f.Huds[0].HPPanel.SetActive(false); f.Huds[0].MagicStoneLabel.transform.parent.gameObject.SetActive(false);
        f.Huds[0].LvLabel.enabled=false; f.SelectCharacter(0);
        Sequence("Zidane, MP 9 of 20, front row");
    }
    private static void ClosingCancels()
    {
        var f=new Fixture(); f.Menu.Show(); f.Menu.Hide();
        f.Fade.CompleteShow(); f.Tween.CompleteShow(); f.Tick();
        Equal(0,SpeechOutput.Text.Count,"Late opening callbacks cannot announce a closing menu");
    }
    private static void TransparentValues()
    {
        var f=new Fixture(); f.Ready(); SpeechOutput.Text.Clear();
        f.Huds[0].LvLabel.color=new Color(1,1,1,0);
        f.Huds[0].StatusesSpriteList[0].spriteName="icon_poison";
        f.Huds[0].StatusesSpriteList[0].alpha=1; f.Huds[0].StatusesPanel.AddComponent<UIPanel>().alpha=0;
        f.SelectCharacter(0);
        Sequence("Zidane, HP 77 of 120, MP 9 of 20, Magic stones 3 of 8, front row");
    }
    private static void VisibleStatusIcons()
    {
        var f=new Fixture(); f.Ready(); SpeechOutput.Text.Clear();
        f.Huds[0].StatusesSpriteList[0].spriteName="icon_poison"; f.Huds[0].StatusesSpriteList[0].alpha=1;
        f.Huds[0].StatusesSpriteList[1].spriteName="unknown_icon"; f.Huds[0].StatusesSpriteList[1].alpha=1;
        f.Huds[0].StatusesSpriteList[2].spriteName="icon_sleep"; f.Huds[0].StatusesSpriteList[2].alpha=0;
        f.SelectCharacter(0);
        Sequence("Zidane, Level 5, HP 77 of 120, MP 9 of 20, Magic stones 3 of 8, front row, Poison");
    }
    private static void SceneChangeCancels()
    {
        var f=new Fixture(); f.Menu.Show(); f.Tween.CompleteShow();
        PersistenSingleton<UIManager>.Instance.State=UIManager.UIState.Item;
        f.Fade.CompleteShow(); f.Tick(); Equal(0,SpeechOutput.Text.Count,"Outgoing menu stays silent");
    }
    private static void Reopening()
    {
        var f=new Fixture(); f.Menu.Show(); f.Menu.Hide(); f.Fade.CompleteHide();
        f.Menu.Show(); f.Fade.CompleteShow(); f.Tween.CompleteShow(); f.Tick();
        Equal(0,SpeechOutput.Text.Count,"Old opening cannot mark the new presentation ready");
        f.Fade.CompleteShow(); f.Tick(); Equal(0,SpeechOutput.Text.Count,"New character tween is still pending");
        f.Tween.CompleteShow(); f.Tick(); Sequence("Item");
    }
    private static void ReturnFromSubmenu()
    {
        var f=new Fixture(); f.Ready(); f.Menu.NeedTweenAndHideSubMenu=false;
        f.Menu.CurrentSubMenu=MainMenuUI.SubMenu.Equip; f.Menu.Hide(); f.Fade.CompleteHide();
        PersistenSingleton<UIManager>.Instance.State=UIManager.UIState.Equip;
        ButtonGroupState.DisableAllGroup(true);
        PersistenSingleton<UIManager>.Instance.PreviousState=UIManager.UIState.Equip;
        PersistenSingleton<UIManager>.Instance.State=UIManager.UIState.MainMenu;
        SpeechOutput.Text.Clear(); f.Menu.Show(); f.Fade.CompleteShow(); f.Tick(); Sequence("Equip");
    }
    private static void WorldOpening()
    {
        var f=new Fixture(); PersistenSingleton<UIManager>.Instance.UnityScene=UIManager.Scene.World;
        PersistenSingleton<UIManager>.Instance.PreviousState=UIManager.UIState.WorldHUD;
        f.Ready(); Sequence("Item");
    }
    private static void UnrelatedSelection()
    {
        var f=new Fixture(); f.Ready(); SpeechOutput.Text.Clear();
        var unrelated=Fixture.Label(new GameObject("OtherScene"),"Hidden command");
        f.Menu.OnItemSelect(unrelated.gameObject);
        f.Buttons["Item"].SetActive(false); f.Menu.OnItemSelect(f.Buttons["Item"]);
        Equal(0,SpeechOutput.Text.Count,"Only shown main-menu selections are spoken");
    }
    private static void CompatibilityOpening()
    {
        var f=new Fixture(); SpeechOutput.Accept=false; SpeechOutput.CompatibilityAccept=true;
        f.Ready(); Sequence("Item");
        Equal(2,SpeechOutput.Attempts,"Preferred rejection must retry compatibility once");
        f.Tick(); f.Advance(2); f.Tick();
        Equal(2,SpeechOutput.Attempts,"Accepted opening must not keep retrying");
        ButtonGroupState.ActiveButton=f.Buttons["Ability"];
        Sequence("Item","Ability");
        Equal(3,SpeechOutput.Attempts,"Later selections must keep the working compatibility backend");
    }
    private static void RejectedOpening()
    {
        var f=new Fixture(); SpeechOutput.Accept=false; f.Ready();
        Equal(1,SpeechOutput.Attempts,"First opening delivery should reach native backend");
        SpeechOutput.Accept=true; f.Advance(1.01f); f.Tick(); Sequence("Item");
        f.Tick(); Sequence("Item");
    }
    private static void RetryLatestSelection()
    {
        var f=new Fixture(); f.Ready(); SpeechOutput.Text.Clear(); SpeechOutput.Attempts=0;
        SpeechOutput.Accept=false; ButtonGroupState.ActiveButton=f.Buttons["Status"];
        int logCount=Memoria.Prime.Log.Messages.Count;
        for(int i=0;i<20;i++)f.Tick();
        Equal(logCount,Memoria.Prime.Log.Messages.Count,"Waiting frames must not flood the log");
        f.Advance(.5f); SpeechOutput.Accept=true; ButtonGroupState.ActiveButton=f.Buttons["Config"]; f.Tick();
        Equal(1,SpeechOutput.Attempts,"Cooldown must not reach backend before one second");
        f.Advance(.51f); f.Tick(); Sequence("Config");
    }
    private static void HideCancelsRetry()
    {
        var f=new Fixture(); SpeechOutput.Accept=false; f.Ready();
        f.Menu.Hide(); SpeechOutput.Accept=true; f.Advance(1.01f); f.Tick(); Sequence();
    }
    private static void SceneChangeCancelsRetry()
    {
        var f=new Fixture(); SpeechOutput.Accept=false; f.Ready();
        PersistenSingleton<UIManager>.Instance.State=UIManager.UIState.Item;
        SpeechOutput.Accept=true; f.Advance(1.01f); f.Tick(); Sequence();
    }
    private static void BackgroundOpening()
    {
        var f=new Fixture(); HonoInputManager.Activated=false; f.Ready();
        Equal(0,SpeechOutput.Attempts,"Background opening must not reach native speech");
        HonoInputManager.Activated=true; f.Tick(); Sequence("Item");
    }
    private static void BackgroundRetry()
    {
        var f=new Fixture(); f.Ready(); SpeechOutput.Text.Clear(); SpeechOutput.Attempts=0;
        SpeechOutput.Accept=false; ButtonGroupState.ActiveButton=f.Buttons["Status"];
        HonoInputManager.Activated=false; SpeechOutput.Accept=true; f.Advance(1.01f); f.Tick();
        Equal(1,SpeechOutput.Attempts,"Background retry must wait for activation");
        ButtonGroupState.ActiveButton=f.Buttons["Config"]; f.Tick();
        HonoInputManager.Activated=true; f.Tick(); Sequence("Config");
    }
    private static void TransparentOpening()
    {
        var f=new Fixture(); var panel=f.Menu.SubMenuPanel.AddComponent<UIPanel>(); panel.alpha=0; f.Ready();
        Sequence(); int logCount=Memoria.Prime.Log.Messages.Count;
        for(int i=0;i<10;i++)f.Tick();
        Equal(logCount,Memoria.Prime.Log.Messages.Count,"Unpresented label retry must be bounded");
        panel.alpha=1; f.Advance(1.01f); f.Tick(); Sequence("Item"); f.Tick(); Sequence("Item");
    }
    private static void TransparentEmptyRow()
    {
        var f=new Fixture(); f.Ready(); SpeechOutput.Text.Clear();
        var widget=f.Huds[1].Self.GetComponent<UIWidget>(); widget.alpha=0; f.SelectCharacter(1); Sequence();
        widget.alpha=1; f.Advance(1.01f); f.Tick(); Sequence("Empty slot");
    }
    private static void Sequence(params string[] expected)
    {
        string want=String.Join(" | ",expected), actual=String.Join(" | ",SpeechOutput.Text);
        if(actual!=want)throw new Exception("Expected ["+want+"], got ["+actual+"]");
        if(Memoria.Prime.Log.Errors.Count!=0)throw new Exception("Runtime logged: "+String.Join("; ",Memoria.Prime.Log.Errors));
    }
    private static void Equal(int expected,int actual,string message) { if(expected!=actual)throw new Exception(message+$"; expected {expected}, got {actual}"); }

    private sealed class Fixture
    {
        public static readonly string[] MenuNames={"Item","Ability","Equip","Status","Order","Card","Config"};
        public readonly MainMenuUI Menu;
        public readonly HonoFading Fade;
        public readonly HonoTweenPosition Tween;
        public readonly Dictionary<string,GameObject> Buttons=new Dictionary<string,GameObject>();
        public readonly List<CharacterDetailHUD> Huds=new List<CharacterDetailHUD>();
        public readonly List<GameObject> OrderButtons=new List<GameObject>();
        private DateTime now = new DateTime(2026,10,26,0,0,0,DateTimeKind.Utc);
        public Fixture()
        {
            SpeechOutput.Text.Clear(); SpeechOutput.Accept=true; SpeechOutput.CompatibilityAccept=false; SpeechOutput.Attempts=0;
            Memoria.Prime.Log.Errors.Clear(); Memoria.Prime.Log.Messages.Clear(); ButtonGroupState.Reset();
            Time.realtimeSinceStartup=0; Time.frameCount=0; HonoInputManager.Activated=true;
            // Control the actual speech facade's clock/backoff at the native-output boundary.
            // Instance readonly fields are changed only in this test utility, never production.
            Set(ScreenReaderManager.Instance,"_clock",(Func<DateTime>)(()=>now));
            Set(ScreenReaderManager.Instance,"_backend",null);
            Set(ScreenReaderManager.Instance,"_nextAttempt",DateTime.MinValue);
            Localization.Text.Clear(); FF9StateSystem.Common=new FF9StateSystem.CommonData();
            FF9StateSystem.Common.FF9.party.member[0]=new PLAYER();
            var manager=new UIManager(); PersistenSingleton<UIManager>.Instance=manager;
            var root=new GameObject("Main Menu Container"); Menu=root.AddComponent<MainMenuUI>(); manager.MainMenuScene=Menu;
            Menu.SubMenuPanel=new GameObject("SubMenu Panel");
            Menu.CharacterListPanel=Child(root,"Characters Panel");
            Menu.HelpDespLabelGameObject=Child(root,"Help");
            var screen=Child(root,"Screen Fade"); Fade=screen.AddComponent<HonoFading>(); Set(typeof(UIScene),Menu,"fading",Fade);
            Tween=Child(root,"Character Transition").AddComponent<HonoTweenPosition>(); Set(Menu,"characterTransition",Tween);
            Set(Menu,"submenuTransition",Child(root,"Submenu Transition").AddComponent<HonoTweenClipping>());
            Set(Menu,"screenFadePanel",root.AddComponent<UIPanel>());
            foreach(string name in MenuNames)
            {
                var button=Button(Child(Menu.SubMenuPanel,name+" SubMenu"),"MainMenu.SubMenu");
                var label=Label(Child(button,"Label"),name); label.gameObject.AddComponent<UILocalize>().key=name;
                Localization.Text[name]=name; Buttons[name]=button; Set(Menu,name+"SubMenu",button);
            }
            for(int i=0;i<4;i++)
            {
                var row=Button(Child(Menu.CharacterListPanel,"Character "+(i+1)),"MainMenu.Character");
                var content=Child(row,"Content"); Child(content,"Avatar").AddComponent<UISprite>();
                var name=Child(content,"Name and LV Panel"); Label(Child(name,"Name"),"Stale name"); Child(name,"Caption"); Label(Child(name,"Level"),"99");
                ValuePanel(content,"HP Panel"); ValuePanel(content,"MP Panel"); ValuePanel(content,"Magic Stone Panel");
                var statuses=Child(content,"Statuses Panel"); var icons=Child(statuses,"Icons");
                for(int k=0;k<7;k++)Child(icons,"Icon "+k).AddComponent<UISprite>().alpha=0;
                OrderButtons.Add(Button(Child(row,"Character Order"),"MainMenu.Order"));
                Huds.Add(new CharacterDetailHUD(row,false));
            }
            Set(Menu,"CharacterHUDList",Huds); Set(Menu,"CharacterOrderGameObjectList",OrderButtons);
            Set(Menu,"characterMemorize",Huds[0].Self); Set(Menu,"characterOrderMemorize",Huds[0].Self);
            foreach(string name in new[]{"gilLabel","hourLabel","minuteLabel","secondLabel","locationNameLabel"})Set(Menu,name,Label(Child(root,name),""));
            Set(Menu,"colonLabel",new[]{Label(Child(root,"Colon 1"),":"),Label(Child(root,"Colon 2"),":")});
        }
        public void Ready() { Menu.Show(); Fade.CompleteShow(); Tween.CompleteShow(); Tick(); }
        public void Advance(float seconds) { now=now.AddSeconds(seconds); Time.realtimeSinceStartup+=seconds; }
        public void Tick() { Time.frameCount++; typeof(MainMenuUI).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Menu,null); }
        public void SelectCharacter(int index) { ButtonGroupState.ActiveGroup="MainMenu.Character"; ButtonGroupState.ActiveButton=Huds[index].Self; }
        private static GameObject Child(GameObject parent,string name) { var go=new GameObject(name); go.transform.parent=parent.transform; return go; }
        public static UILabel Label(GameObject go,string text) { var label=go.AddComponent<UILabel>(); label.rawText=text; return label; }
        private static GameObject Button(GameObject go,string group) { go.AddComponent<UIWidget>(); go.AddComponent<ButtonGroupState>().GroupName=group; go.AddComponent<UIButton>(); go.AddComponent<BoxCollider>(); go.AddComponent<UIKeyNavigation>(); return go; }
        private static void ValuePanel(GameObject parent,string name) { var panel=Child(parent,name); Child(panel,"Caption"); Label(Child(panel,"Current"),"999"); Label(Child(panel,"Slash"),"/"); Label(Child(panel,"Maximum"),"999"); }
        private static void Set(object target,string field,object value)=>Set(target.GetType(),target,field,value);
        private static void Set(Type type,object target,string field,object value)=>type.GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    }
}
