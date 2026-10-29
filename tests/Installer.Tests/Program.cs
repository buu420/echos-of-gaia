using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;
using EchosOfGaia.Installer;

internal static class Program
{
    private static int count;
    private static int failures;
    private static string root;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--service-fixture")
            return Finish(InstallerService.RunAsync(args[1], args[2], Console.WriteLine));
        Application.EnableVisualStyles();
        Control.CheckForIllegalCrossThreadCalls = true;
        root = Path.Combine(Path.GetTempPath(), "Gaia installer é & $ ` (test)'s " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Run("literal selected folder and both output streams", LiteralPath);
            Run("smart Unicode apostrophes stay literal", SmartQuotes);
            Run("nonzero child exit stays a failure", FailureExit);
            Run("large concurrent output drains without deadlock", LargeOutput);
            Run("missing installer refuses before execution", MissingInstaller);
            Run("nonexistent selected folder refuses before execution", MissingGame);
            Run("Steam alternate library discovery", SteamLibrary);
            Run("Steam manifest traversal rejected", SteamTraversal);
            Run("native accessible controls and keyboard order", AccessibleControls);
            Run("busy window cannot close and success receives focus", BusyAndSuccess);
            Run("failed installation remains visible and retryable", VisibleFailure);
            Run("failed log storage cannot strand completed UI", LogFailure);
            Run("second process cannot install the same folder concurrently", ConcurrentInstall);
            Console.WriteLine((failures == 0 ? "PASS" : "FAIL") + " Installer: " + count + " passed, " + failures + " failed; game launches 0");
            return failures == 0 ? 0 : 1;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { Directory.Delete(root, true); }
    }

    private static void Run(string name, Action test)
    {
        try { test(); count++; Console.WriteLine("PASS " + name); }
        catch (Exception error) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static string Fixture(string script)
    {
        string path = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "Install-EchosOfGaia.ps1"), "param([string]$GameFolder)\r\n" + script, new UTF8Encoding(true));
        return path;
    }
    private static int Finish(Task<int> task)
    {
        Check(task.Wait(TimeSpan.FromSeconds(45)), "Installer child did not finish within the fixture deadline");
        return task.GetAwaiter().GetResult();
    }
    private static void LiteralPath()
    {
        string package = Fixture("[Console]::WriteLine('selected=' + $GameFolder)\r\n[Console]::Error.WriteLine('fixture diagnostic')\r\nWrite-Host 'host progress'\r\nWrite-Output 'pipeline progress'\r\nexit 0");
        var lines = new List<string>();
        Check(Finish(InstallerService.RunAsync(package, root, lines.Add)) == 0, "Valid fixture failed");
        Check(lines.Contains("selected=" + root), "The selected path was changed, truncated, interpreted or misdecoded");
        Check(lines.Contains("fixture diagnostic"), "stderr was lost");
        Check(lines.Contains("host progress") && lines.Contains("pipeline progress"), "Installer progress was serialized instead of readable text: " + string.Join(" | ", lines));
    }
    private static void FailureExit()
    {
        string package = Fixture("[Console]::Error.WriteLine('fixture failure')\r\nexit 17");
        var lines = new List<string>();
        Check(Finish(InstallerService.RunAsync(package, root, lines.Add)) == 17, "Nonzero child exit was hidden");
        Check(lines.Contains("fixture failure"), "Failure details were lost");
    }
    private static void SmartQuotes()
    {
        string package = Path.Combine(root, "John’s ‘package‚‛"), game = Path.Combine(root, "John’s ‘game‚‛");
        Directory.CreateDirectory(package); Directory.CreateDirectory(game);
        File.WriteAllText(Path.Combine(package, "Install-EchosOfGaia.ps1"), "param([string]$GameFolder)\r\n[Console]::WriteLine($GameFolder)\r\nexit 0", new UTF8Encoding(true));
        var lines = new List<string>();
        int code = Finish(InstallerService.RunAsync(package, game, lines.Add));
        Check(code == 0 && lines.Contains(game), "Unicode punctuation was interpreted as script syntax; exit=" + code + "; " + string.Join(" | ", lines));
    }
    private static void LargeOutput()
    {
        string package = Fixture("for($i=0;$i -lt 3500;$i++){[Console]::Out.WriteLine('out'+$i+('x'*100));[Console]::Error.WriteLine('err'+$i+('y'*100))}\r\nexit 0");
        int lines = 0;
        Check(Finish(InstallerService.RunAsync(package, root, line => lines++)) == 0, "Output fixture failed");
        Check(lines == 7000, "Output was lost or callbacks were not serialized: " + lines);
    }
    private static void MissingInstaller()
    {
        bool rejected = false;
        try { Finish(InstallerService.RunAsync(root, root, line => { })); }
        catch (Exception error) { rejected = error.ToString().Contains("Extract"); }
        Check(rejected, "Missing package did not explain extracting the complete ZIP");
    }
    private static void MissingGame()
    {
        string marker = Path.Combine(root, "must-not-run.txt");
        string package = Fixture("[IO.File]::WriteAllText('" + marker.Replace("'", "''") + "','ran')");
        bool rejected = false;
        try { Finish(InstallerService.RunAsync(package, Path.Combine(root, "absent"), line => { })); }
        catch (Exception) { rejected = true; }
        Check(rejected && !File.Exists(marker), "Invalid destination reached the installer script");
    }
    private static string MakeGame(string directory)
    {
        foreach (string arch in new[] { "x86", "x64" })
        {
            Directory.CreateDirectory(Path.Combine(directory, arch));
            File.WriteAllText(Path.Combine(directory, arch, "FF9.exe"), "synthetic file, never executed");
        }
        return directory;
    }
    private static void SteamLibrary()
    {
        string steam = Path.Combine(root, "Steam"), library = Path.Combine(root, "other library é");
        Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
        string game = MakeGame(Path.Combine(library, "steamapps", "common", "FINAL FANTASY IX"));
        File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"), "\"libraryfolders\" { \"1\" { \"path\" \"" + library.Replace("\\", "\\\\") + "\" } }");
        File.WriteAllText(Path.Combine(library, "steamapps", "appmanifest_377840.acf"), "\"AppState\" { \"appid\" \"377840\" \"installdir\" \"FINAL FANTASY IX\" }");
        Check(SteamLocator.FindGameFolder(new[] { steam }) == game, "Secondary Steam library was not discovered");
    }
    private static void SteamTraversal()
    {
        string steam = Path.Combine(root, "bad Steam"), apps = Path.Combine(steam, "steamapps");
        Directory.CreateDirectory(apps);
        MakeGame(Path.Combine(apps, "other game"));
        File.WriteAllText(Path.Combine(apps, "appmanifest_377840.acf"), "\"AppState\" { \"appid\" \"377840\" \"installdir\" \"..\\\\other game\" }");
        Check(SteamLocator.FindGameFolder(new[] { steam }) == null, "Manifest escaped its common folder");
    }
    private static Control Find(Form form, string name)
    {
        Control[] found = form.Controls.Find(name, true);
        Check(found.Length == 1, "Missing or duplicate native control: " + name);
        return found[0];
    }
    private static void AccessibleControls()
    {
        using (var form = new InstallerForm(root, root))
        {
            ShowForm(form);
            foreach (string name in new[] { "gameFolder", "browse", "install", "status", "log", "close" })
            {
                var control = Find(form, name);
                var peer = AutomationElement.FromHandle(control.Handle);
                Check(!string.IsNullOrWhiteSpace(peer.Current.Name), "Unlabelled accessibility peer: " + name);
                Check(control.TabStop, "Cannot reach control with keyboard: " + name);
            }
            Control folder = Find(form, "gameFolder");
            folder.Focus(); form.SelectNextControl(folder, true, true, true, false);
            Check(form.ActiveControl == Find(form, "browse"), "Browse does not follow the folder in keyboard order");
            Check(((TextBox)Find(form, "status")).ReadOnly && ((TextBox)Find(form, "log")).ReadOnly, "Status or log editable");
        }
    }
    private static void PumpUntil(Func<bool> done)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        while (!done() && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(10); }
        Check(done(), "GUI fixture timed out");
    }
    private static void ShowForm(Form form)
    {
        // UI Automation may leave a base SynchronizationContext on this STA.
        // The installer must marshal explicitly to its native window regardless.
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
        form.Show(); Application.DoEvents();
    }
    private static void BusyAndSuccess()
    {
        string package = Fixture("[Console]::WriteLine('fixture started')\r\nStart-Sleep -Milliseconds 800\r\nexit 0");
        using (var form = new InstallerForm(package, root))
        {
            ShowForm(form);
            var install = (Button)Find(form, "install"); install.PerformClick();
            Check(!install.Enabled && !Find(form, "close").Enabled, "Busy transaction can be duplicated or closed");
            form.Close(); Check(form.Visible, "Closing interrupted an active transaction");
            PumpUntil(() => Find(form, "close").Enabled);
            Check(Find(form, "status").Text.Contains("installed"), "Success was not reported");
            Check(form.ActiveControl == Find(form, "status"), "Completion status did not receive accessible focus");
            Check(Find(form, "log").Text.Contains("fixture started"), "Child output was not visible");
        }
    }
    private static void VisibleFailure()
    {
        string package = Fixture("[Console]::Error.WriteLine('fixture visible failure')\r\nexit 17");
        using (var form = new InstallerForm(package, root))
        {
            ShowForm(form);
            ((Button)Find(form, "install")).PerformClick();
            PumpUntil(() => Find(form, "close").Enabled);
            Check(Find(form, "status").Text.Contains("failed") && Find(form, "status").Text.Contains("17"), "Failure exit was displayed as success");
            Check(Find(form, "log").Text.Contains("fixture visible failure"), "Error details were hidden");
            Check(Find(form, "install").Enabled, "Install was not re-enabled after failure");
            Check(form.Visible, "Failure window closed; disposed=" + form.IsDisposed);
        }
    }
    private sealed class FailingLogStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) { throw new IOException("fixture log storage failure"); }
    }
    private static void LogFailure()
    {
        using (var form = new InstallerForm(root, root))
        {
            ShowForm(form);
            var type = typeof(InstallerForm);
            type.GetField("busy", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(form, true);
            Find(form, "install").Enabled = Find(form, "close").Enabled = false;
            type.GetField("logWriter", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(form, new StreamWriter(new FailingLogStream()));
            type.GetMethod("CompleteInstallation", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { 0, null });
            Check(Find(form, "install").Enabled && Find(form, "close").Enabled, "Log storage failure left the completed window busy");
            Check(Find(form, "status").Text.Contains("installed") && Find(form, "log").Text.Contains("fixture log storage failure"), "Game outcome or logging failure was hidden");
            form.Close(); Check(!form.Visible, "Completed installer cannot close after log failure");
        }
    }
    private static void ConcurrentInstall()
    {
        string marker = Path.Combine(root, "concurrent-started.txt");
        string package = Fixture("[IO.File]::WriteAllText('" + marker.Replace("'", "''") + "','started')\r\nStart-Sleep -Milliseconds 1500\r\nexit 0");
        var start = new ProcessStartInfo(typeof(Program).Assembly.Location,
            "--service-fixture \"" + package + "\" \"" + root + "\"")
            { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        using (var first = Process.Start(start))
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (!File.Exists(marker) && !first.HasExited && DateTime.UtcNow < deadline) Thread.Sleep(10);
            Check(File.Exists(marker), "First installer process did not reach its script");
            bool refused = false;
            try { Finish(InstallerService.RunAsync(package, root, line => { })); }
            catch (Exception error) { refused = error.ToString().Contains("already running"); }
            Check(first.WaitForExit(15000) && first.ExitCode == 0, "First process did not complete normally");
            Check(refused, "A second process started a transaction for the same game folder");
            Check(Finish(InstallerService.RunAsync(package, root, line => { })) == 0, "Completed transaction did not release its folder lock");
        }
    }
}
