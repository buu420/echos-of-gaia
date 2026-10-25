using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Reflection;
using System.Diagnostics;
using Memoria.Launcher;
using Memoria.Accessibility.Navigation;
using FF9Access.Tests;
internal static class Program
{
    [STAThread] private static int Main(string[] args)
    {
        string original=Directory.GetCurrentDirectory();string stage=args.Length==2?Path.GetFullPath(args[1]):AppDomain.CurrentDomain.BaseDirectory;
        string fixture=Path.Combine(stage,"OwnedLauncherFixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);Directory.SetCurrentDirectory(fixture);
        int starts=0;UiLauncherPlayButton.ProcessLauncher=info=>{starts++;return null;};
        try {
            TestAssert.Equal(4, IntPtr.Size);Pass("LauncherProcessMatchesRootSpeechArchitecture");
            File.WriteAllText("Settings.ini","[Settings]\nAutoRunGame = False\nWindowMode = 0\nScreenResolution = 1280x720\n[Memoria]\nCheckUpdates = False\nVersion = 2000.01.01\n");
            File.WriteAllText("Memoria.ini","[Graphics]\nAntiAliasing = 8\n[Audio]\nBackend = 0\n[Mod]\nFolderNames = \"MyMod\"\n[Accessibility]\nNavigationBindings = ToggleAutomatic=P\n");
            var app=new App();app.InitializeComponent();
            foreach(string cursor in new[]{"moogle.cur","cursorHand.cur"})
                using(var resource=Application.GetResourceStream(new Uri("pack://application:,,,/Memoria.Launcher;component/images/"+cursor)).Stream)
                    TestAssert.True(resource.Length>0);
            Pass("LauncherCursorsResolveFromTheirOwningAssembly");
            var window=new MainWindow();app.MainWindow=window;window.Show();Pump();
            var play=(UiLauncherPlayButton)window.FindName("PlayButton");var peer=UIElementAutomationPeer.CreatePeerForElement(play);
            TestAssert.True(!String.IsNullOrWhiteSpace(peer.GetName()));TestAssert.True(peer.GetPattern(PatternInterface.Invoke) is IInvokeProvider);Pass("PlayHasNameAndInvokePattern");
            var tab=(TabItem)window.FindName("AccessibilityTab");TestAssert.True(tab!=null && tab.IsTabStop);tab.IsSelected=true;Pump();
            TestAssert.Equal("Accessibility",UIElementAutomationPeer.CreatePeerForElement(tab).GetName());Pass("AccessibilityTabUsesItsVisibleHeader");
            var panel=(AccessibilitySettingsPanel)window.FindName("AccessibilitySettings");
            var numericIni=new IniFile("Memoria.ini");numericIni.SetSetting("Accessibility","AnnounceRegionChanges","0");panel.Load(numericIni);
            TestAssert.False(panel.RegionChanges.IsChecked==true);Pass("NativeZeroLoadsRegionAnnouncementsDisabled");
            numericIni.SetSetting("Accessibility","AnnounceRegionChanges","1");panel.Load(numericIni);
            TestAssert.True(panel.RegionChanges.IsChecked==true);Pass("NativeOneLoadsRegionAnnouncementsEnabled");
            var checkboxPeer=UIElementAutomationPeer.CreatePeerForElement(panel.RegionChanges);TestAssert.True(checkboxPeer.GetName().Length>0);TestAssert.True(checkboxPeer.GetPattern(PatternInterface.Toggle) is IToggleProvider);Pass("CheckboxHasLabelAndState");
            var descriptionsPeer=UIElementAutomationPeer.CreatePeerForElement(panel.Descriptions);TestAssert.Equal("Audio descriptions",descriptionsPeer.GetName());TestAssert.True(descriptionsPeer.GetPattern(PatternInterface.Toggle) is IToggleProvider);
            var volumePeer=UIElementAutomationPeer.CreatePeerForElement(panel.DescriptionVolume);TestAssert.Equal("Description volume (0 to 200 percent)",volumePeer.GetName());TestAssert.True(volumePeer.GetPattern(PatternInterface.Value) is IValueProvider);Pass("DescriptionSettingsHaveNamesAndPatterns");
            File.WriteAllText("Narration.ini",File.ReadAllText("Memoria.ini")+"AudioDescriptions = 0\nAudioDescriptionVolume = 83\n");var descriptionIni=new IniFile("Narration.ini");panel.Load(descriptionIni);
            TestAssert.False(panel.Descriptions.IsChecked==true);TestAssert.Equal("83",panel.DescriptionVolume.Text);panel.DescriptionVolume.Text="201";string descriptionValidation;
            TestAssert.False(panel.TrySave(descriptionIni,out descriptionValidation));TestAssert.Equal("83",new IniFile("Narration.ini").GetSetting("Accessibility","AudioDescriptionVolume"));
            panel.DescriptionVolume.Text="83";panel.Descriptions.IsChecked=true;TestAssert.True(panel.TrySave(descriptionIni,out descriptionValidation));var descriptionSaved=new IniFile("Narration.ini");TestAssert.Equal("1",descriptionSaved.GetSetting("Accessibility","AudioDescriptions"));TestAssert.Equal("83",descriptionSaved.GetSetting("Accessibility","AudioDescriptionVolume"));Pass("DescriptionVolumeValidationPreservesSavedValues");
            var exitPeer=UIElementAutomationPeer.CreatePeerForElement(panel.ExitSounds);TestAssert.Equal("Exit proximity sounds",exitPeer.GetName());TestAssert.True(exitPeer.GetPattern(PatternInterface.Toggle) is IToggleProvider);
            var exitVolumePeer=UIElementAutomationPeer.CreatePeerForElement(panel.ExitSoundVolume);TestAssert.True(exitVolumePeer.GetPattern(PatternInterface.Value) is IValueProvider);TestAssert.True(panel.ExitSounds.IsChecked==true);TestAssert.Equal("100",panel.ExitSoundVolume.Text);Pass("ExitSoundControlsAreNamedAndEnabledByDefault");
            panel.ExitSounds.IsChecked=false;panel.ExitSoundVolume.Text="73";TestAssert.True(panel.TrySave(descriptionIni,out descriptionValidation));var exitSaved=new IniFile("Narration.ini");TestAssert.Equal("0",exitSaved.GetSetting("Accessibility","FieldExitSounds"));TestAssert.Equal("73",exitSaved.GetSetting("Accessibility","FieldExitSoundVolume"));TestAssert.Equal("8",exitSaved.GetSetting("Graphics","AntiAliasing"));Pass("ExitSoundSettingsPreserveOtherPreferences");
            string savedExitBytes=File.ReadAllText("Narration.ini");panel.ExitSoundVolume.Text="201";TestAssert.False(panel.TrySave(descriptionIni,out descriptionValidation));TestAssert.Equal(savedExitBytes,File.ReadAllText("Narration.ini"));panel.Load(descriptionIni);Pass("InvalidExitVolumeDoesNotRewriteSettings");
            foreach(string prefix in new[]{"Npc","Interactable"}){
                var toggleField=typeof(AccessibilitySettingsPanel).GetField(prefix+"Sounds");var volumeField=typeof(AccessibilitySettingsPanel).GetField(prefix+"SoundVolume");TestAssert.True(toggleField!=null && volumeField!=null);
                var toggle=(CheckBox)toggleField.GetValue(panel);var volume=(TextBox)volumeField.GetValue(panel);
                var spatialPeer=UIElementAutomationPeer.CreatePeerForElement(toggle);TestAssert.Equal(prefix=="Npc"?"NPC proximity sounds":"Interactable proximity sounds",spatialPeer.GetName());TestAssert.True(spatialPeer.GetPattern(PatternInterface.Toggle) is IToggleProvider);
                TestAssert.True(UIElementAutomationPeer.CreatePeerForElement(volume).GetPattern(PatternInterface.Value) is IValueProvider);TestAssert.True(toggle.IsChecked==true);TestAssert.Equal("100",volume.Text);Pass(prefix+"SpatialControlsAreAccessibleByDefault");
                string beforeSpatial=File.ReadAllText("Narration.ini");volume.Text="201";TestAssert.False(panel.TrySave(descriptionIni,out descriptionValidation));TestAssert.Equal(beforeSpatial,File.ReadAllText("Narration.ini"));volume.Text="64";toggle.IsChecked=false;TestAssert.True(panel.TrySave(descriptionIni,out descriptionValidation));
                var spatialIni=new IniFile("Narration.ini");TestAssert.Equal("0",spatialIni.GetSetting("Accessibility","Field"+prefix+"Sounds"));TestAssert.Equal("64",spatialIni.GetSetting("Accessibility","Field"+prefix+"SoundVolume"));TestAssert.Equal("73",spatialIni.GetSetting("Accessibility","FieldExitSoundVolume"));TestAssert.Equal("8",spatialIni.GetSetting("Graphics","AntiAliasing"));Pass(prefix+"SpatialValidationAndSavePreserveOtherSettings");
            }
            panel.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));Pump();TestAssert.True(Keyboard.FocusedElement is TextBox);Pass("KeyboardOrderReachesAccessibilitySettings");
            var ini=new IniFile("Memoria.ini");panel.Load(ini);panel.BindingEditors[NavigationCommand.ToggleAutomatic].Text="J";string validation;
            TestAssert.False(panel.TrySave(ini,out validation));TestAssert.Equal("ToggleAutomatic=P",new IniFile("Memoria.ini").GetSetting("Accessibility","NavigationBindings"));Pass("BindingConflictKeepsPreviousValue");
            TestAssert.Equal(0,starts);Pass("OpeningSettingsDoesNotLaunchGame");
            var after=new IniFile("Memoria.ini");TestAssert.Equal("8",after.GetSetting("Graphics","AntiAliasing"));TestAssert.Equal("0",after.GetSetting("Audio","Backend"));TestAssert.Equal("\"MyMod\"",after.GetSetting("Mod","FolderNames"));Pass("FirstRunPreservesGraphicsAudioAndModChoices");
            TestAssert.False(AccessibilityBuildPolicy.CanApplyUpdate("ff9-access-local","stock-memoria",0));TestAssert.False(AccessibilityBuildPolicy.CanApplyUpdate("","",1));Pass("StockPatcherCannotReplaceCustomBuild");
            ((TabItem)window.FindName("menuSettings")).IsSelected=true;Pump();int namedSettings=CheckSettings(window);
            TestAssert.True(namedSettings>5);Pass("GeneratedSettingsHaveNamesAndValuePatterns");
            tab.IsSelected=true;Pump();panel.Load(ini);string beforeSave=File.ReadAllText("Memoria.ini");
            panel.RegionChanges.IsChecked=false;TestAssert.True(panel.TrySave(ini,out validation));
            var saved=new IniFile("Memoria.ini");TestAssert.Equal("0",saved.GetSetting("Accessibility","AnnounceRegionChanges"));TestAssert.Equal("8",saved.GetSetting("Graphics","AntiAliasing"));TestAssert.Equal("\"MyMod\"",saved.GetSetting("Mod","FolderNames"));Pass("ExplicitSaveOnlyChangesAccessibilityOptions");
            saved.SetSetting("Accessibility","NavigationBindings",NavigationBindings.LegacyDefaultSerialization);panel.Load(saved);
            TestAssert.Equal("K",panel.BindingEditors[NavigationCommand.PreviousTarget].Text);TestAssert.Equal("L",panel.BindingEditors[NavigationCommand.Status].Text);TestAssert.Equal("Semicolon",panel.BindingEditors[NavigationCommand.NextTarget].Text);Pass("PreviousDefaultDisplaysShiftedBottomRow");
            panel.BindingEditors[NavigationCommand.NextTarget].Text=";";TestAssert.True(panel.TrySave(saved,out validation));
            var punctuationSaved=new IniFile("Memoria.ini");string safeBindings=punctuationSaved.GetSetting("Accessibility","NavigationBindings");
            TestAssert.False(safeBindings.Contains(";"));TestAssert.True(safeBindings.Contains("NextTarget=Semicolon"));panel.Load(punctuationSaved);
            TestAssert.Equal("Semicolon",panel.BindingEditors[NavigationCommand.NextTarget].Text);TestAssert.Equal("8",punctuationSaved.GetSetting("Graphics","AntiAliasing"));Pass("LiteralSemicolonSavesAndReloadsWithoutIniTruncation");
            string battleData=Path.Combine(fixture,"StreamingAssets","Data","Battle");Directory.CreateDirectory(battleData);File.WriteAllText(Path.Combine(battleData,"StatusSets.csv"),"native data path fixture");
            ProcessStartInfo captured=null;UiLauncherPlayButton.ProcessLauncher=info=>{captured=info;starts++;return Process.GetCurrentProcess();};
            play.GameSettings.IsDebugMode=false;
            foreach(string architecture in new[]{"x86","x64"}) {
                string executable=Path.Combine(fixture,architecture,"FF9.exe");Directory.CreateDirectory(Path.GetDirectoryName(executable));
                typeof(UiLauncherPlayButton).GetMethod("StartGameProcess",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(play,new object[]{executable,"-runbylauncher"});
                TestAssert.Equal(executable,captured.FileName);TestAssert.Equal("-runbylauncher",captured.Arguments);
                TestAssert.True(File.Exists(Path.Combine(captured.WorkingDirectory,"StreamingAssets","Data","Battle","StatusSets.csv")));
                TestAssert.True(File.Exists(Path.Combine(captured.WorkingDirectory,"Memoria.ini")));
                Pass("GameLaunchResolvesRootDataAndSettings"+architecture);
            }
            window.Close();Pump();app.Shutdown();Console.WriteLine("PASS LauncherAccessibility: "+_passes+"; actual WPF peers and focus; intercepted launch requests="+starts+"; real game starts=0");return 0;
        }catch(Exception exception){Console.Error.WriteLine("FAIL LauncherAccessibility: "+exception);return 1;}
        finally{Directory.SetCurrentDirectory(original);}
    }
    private static int _passes;
    private static void Pass(string name){_passes++;Console.WriteLine("PASS "+name);}
    private static int CheckSettings(DependencyObject root)
    {
        int count=0;
        var control=root as System.Windows.Controls.Control;
        if(control!=null && control.IsVisible && (control is ComboBox || control is Slider)) {
            var peer=UIElementAutomationPeer.CreatePeerForElement(control);
            if(String.IsNullOrWhiteSpace(peer.GetName()))throw new Exception("Unnamed setting: "+control.GetType().Name+" / "+control.Name+" / "+System.Windows.Automation.AutomationProperties.GetAutomationId(control));
            if(control is Slider && !(peer.GetPattern(PatternInterface.RangeValue) is IRangeValueProvider))throw new Exception("Missing range value: "+peer.GetName());count++;
        }
        for(int index=0;index<VisualTreeHelper.GetChildrenCount(root);index++)count+=CheckSettings(VisualTreeHelper.GetChild(root,index));
        return count;
    }
    private static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
}
