using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace EchosOfGaia.Installer
{
    public sealed class InstallerForm : Form
    {
        private readonly string packageRoot;
        private readonly TextBox gameFolder, status, log;
        private readonly Button browse, install, close, openLog;
        private readonly Label elapsed;
        private readonly Timer timer = new Timer { Interval = 10000 };
        private readonly Stopwatch watch = new Stopwatch();
        private StreamWriter logWriter;
        private string logPath;
        private string logError;
        private bool busy;

        public InstallerForm(string packageRoot, string initialGameFolder)
        {
            this.packageRoot = packageRoot;
            Text = "Echos of Gaia installer";
            AccessibleName = Text;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(730, 500);
            MinimumSize = new Size(650, 440);
            AutoScaleMode = AutoScaleMode.Font;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 3, RowCount = 9 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (int row = 0; row < 9; row++) layout.RowStyles.Add(new RowStyle(row == 6 ? SizeType.Percent : SizeType.AutoSize, row == 6 ? 100 : 0));
            Controls.Add(layout);
            var intro = new Label { Text = "Close FINAL FANTASY IX and its launcher. Check the game folder, then choose Install. Existing files are backed up.", AutoSize = true, MaximumSize = new Size(680, 0), Margin = new Padding(3, 3, 3, 12) };
            layout.Controls.Add(intro, 0, 0); layout.SetColumnSpan(intro, 3);
            layout.Controls.Add(new Label { Text = "&Game folder:", AutoSize = true, TabIndex = 0 }, 0, 1);
            gameFolder = new TextBox { Name = "gameFolder", AccessibleName = "FINAL FANTASY IX game folder", Text = initialGameFolder ?? "", Dock = DockStyle.Fill, TabIndex = 1 };
            gameFolder.AccessibleDescription = "The folder containing the game's x86 and x64 folders. You can type a path or choose Browse.";
            layout.Controls.Add(gameFolder, 0, 2); layout.SetColumnSpan(gameFolder, 2);
            browse = Button("browse", "&Browse...", "Browse for game folder", 2);
            browse.Click += (sender, args) =>
            {
                using (var dialog = new FolderBrowserDialog { Description = "Choose the FINAL FANTASY IX installation folder", ShowNewFolderButton = false })
                {
                    if (Directory.Exists(gameFolder.Text)) dialog.SelectedPath = gameFolder.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK) gameFolder.Text = dialog.SelectedPath;
                }
                gameFolder.Focus();
            };
            layout.Controls.Add(browse, 2, 2);
            layout.Controls.Add(new Label { Text = "&Status:", AutoSize = true, TabIndex = 3 }, 0, 3);
            status = new TextBox { Name = "status", AccessibleName = "Installation status", ReadOnly = true, Multiline = true, Height = 64, Dock = DockStyle.Fill, TabIndex = 4, Text = "Ready. Check the game folder, then choose Install." };
            layout.Controls.Add(status, 0, 4); layout.SetColumnSpan(status, 3);
            layout.Controls.Add(new Label { Text = "Installation &log:", AutoSize = true, TabIndex = 5 }, 0, 5);
            log = new TextBox { Name = "log", AccessibleName = "Installation log", ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, TabIndex = 6 };
            layout.Controls.Add(log, 0, 6); layout.SetColumnSpan(log, 3);
            elapsed = new Label { AutoSize = true, Text = "", Margin = new Padding(3, 8, 3, 8), TabIndex = 7 };
            layout.Controls.Add(elapsed, 0, 7); layout.SetColumnSpan(elapsed, 3);
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, TabIndex = 8 };
            install = Button("install", "&Install", "Install", 0);
            install.Click += (sender, args) => StartInstallation();
            openLog = Button("openLog", "&Open log", "Open installation log", 1); openLog.Enabled = false;
            openLog.Click += (sender, args) =>
            {
                try { Process.Start(new ProcessStartInfo(logPath) { UseShellExecute = true }); }
                catch (Exception error) { status.Text = "Could not open the log: " + error.Message; status.Focus(); }
            };
            close = Button("close", "&Close", "Close", 2); close.Click += (sender, args) => Close();
            buttons.Controls.AddRange(new Control[] { install, openLog, close });
            layout.Controls.Add(buttons, 0, 8); layout.SetColumnSpan(buttons, 3);
            AcceptButton = install; CancelButton = close;
            Shown += (sender, args) => gameFolder.Focus();
            FormClosing += (sender, args) =>
            {
                if (!busy) return;
                args.Cancel = true;
                status.Text = "Installation is still running. Please wait for it to finish before closing.";
                status.Focus();
            };
            timer.Tick += (sender, args) => elapsed.Text = "Installation running for " + (int)watch.Elapsed.TotalMinutes + " minutes " + watch.Elapsed.Seconds + " seconds.";
        }

        private static Button Button(string name, string text, string accessibleName, int tabIndex)
        {
            return new Button { Name = name, Text = text, AccessibleName = accessibleName, AutoSize = true, TabIndex = tabIndex };
        }
        private void AppendLine(string line)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(AppendLine), line); return; }
            log.AppendText(line + Environment.NewLine);
            WriteLogLine(line);
            if (line.StartsWith("Checking ", StringComparison.Ordinal) || line.StartsWith("Validation passed", StringComparison.Ordinal))
                status.Text = line;
        }
        private void StartInstallation()
        {
            if (busy) return;
            logPath = null; logError = null;
            busy = true; install.Enabled = browse.Enabled = gameFolder.Enabled = close.Enabled = openLog.Enabled = false;
            log.Clear(); status.Text = "Installing. This can take several minutes. Please leave this window open.";
            watch.Restart(); timer.Start(); status.Focus();
            try
            {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EchosOfGaia", "Installer");
                Directory.CreateDirectory(directory);
                logPath = Path.Combine(directory, "install-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".log");
                logWriter = new StreamWriter(logPath, false, new UTF8Encoding(false));
                AppendLine("Echos of Gaia installation log"); AppendLine("Game folder: " + gameFolder.Text);
                InstallerService.RunAsync(packageRoot, gameFolder.Text, AppendLine).ContinueWith(completed =>
                {
                    // Explicitly marshal back to this window. Host accessibility clients
                    // can replace SynchronizationContext; controls still belong here.
                    Exception error = completed.IsFaulted ? completed.Exception.GetBaseException() : null;
                    int code = error == null ? completed.Result : -1;
                    BeginInvoke(new Action(() => CompleteInstallation(code, error)));
                });
            }
            catch (Exception error)
            {
                CompleteInstallation(-1, error);
            }
        }
        private void CompleteInstallation(int exitCode, Exception error)
        {
            try
            {
                if (error != null)
                {
                    AppendLine(error.Message);
                    status.Text = "Installation failed. " + error.Message;
                }
                else status.Text = exitCode == 0
                    ? "Echos of Gaia is installed. Start your screen reader, then launch FINAL FANTASY IX normally through Steam."
                    : "Installation failed (exit code " + exitCode + "). Read the installation log for details. If access was denied, close this window and use Run as administrator on the installer.";
            }
            finally
            {
                try
                {
                    WriteLogLine(status.Text);
                    CloseLogWriter();
                    if (logPath != null && File.Exists(logPath)) log.AppendText("Log file: " + logPath + Environment.NewLine);
                }
                finally
                {
                    timer.Stop(); watch.Stop();
                    busy = false; install.Enabled = browse.Enabled = gameFolder.Enabled = close.Enabled = true;
                    openLog.Enabled = logPath != null && File.Exists(logPath);
                    elapsed.Text = "Finished after " + (int)watch.Elapsed.TotalMinutes + " minutes " + watch.Elapsed.Seconds + " seconds.";
                    if (logError != null) status.Text += " The log could not be saved completely; read the on-screen log.";
                    status.Focus(); status.SelectAll();
                }
            }
        }
        private void WriteLogLine(string line)
        {
            if (logWriter == null) return;
            try { logWriter.WriteLine(line); logWriter.Flush(); }
            catch (Exception error) when (IsLogStorageError(error))
            {
                ReportLogError(error);
                CloseLogWriter();
            }
        }
        private void CloseLogWriter()
        {
            var writer = logWriter; logWriter = null;
            if (writer == null) return;
            try { writer.Dispose(); }
            catch (Exception error) when (IsLogStorageError(error)) { ReportLogError(error); }
        }
        private static bool IsLogStorageError(Exception error)
        {
            return error is IOException || error is UnauthorizedAccessException || error is ObjectDisposedException;
        }
        private void ReportLogError(Exception error)
        {
            if (logError != null) return;
            logError = error.Message;
            log.AppendText("Could not save the complete installation log: " + error.Message + Environment.NewLine);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Dispose(); CloseLogWriter(); }
            base.Dispose(disposing);
        }
    }
}
