using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EchosOfGaia.Installer
{
    public static class InstallerService
    {
        public static Task<int> RunAsync(string packageRoot, string gameFolder, Action<string> output)
        {
            return Task.Run(() =>
            {
                string script = Path.Combine(Path.GetFullPath(packageRoot), "Install-EchosOfGaia.ps1");
                if (!File.Exists(script))
                    throw new InvalidOperationException("Extract all of the ZIP into a folder, then open Install-EchosOfGaia.exe from that folder. The installation script is missing.");
                string game = Path.GetFullPath(gameFolder.Trim().Trim('"'));
                if (!Directory.Exists(game))
                    throw new DirectoryNotFoundException("Select the existing FINAL FANTASY IX game folder before installing.");

                string lockId;
                using (var hash = SHA256.Create())
                    lockId = BitConverter.ToString(hash.ComputeHash(Encoding.Unicode.GetBytes(game.TrimEnd('\\').ToUpperInvariant()))).Replace("-", "");
                using (var gate = new Mutex(false, @"Local\EchosOfGaia.Install." + lockId))
                {
                    bool acquired = false;
                    try
                    {
                        try { acquired = gate.WaitOne(0); }
                        catch (AbandonedMutexException) { acquired = true; }
                        if (!acquired) throw new InvalidOperationException("An installation is already running for this game folder. Wait for it to finish before installing again.");
                        return RunProcess(script, packageRoot, game, output);
                    }
                    finally { if (acquired) gate.ReleaseMutex(); }
                }
            });
        }

        private static int RunProcess(string script, string packageRoot, string game, Action<string> output)
        {
                // Encode values separately: PowerShell also treats several Unicode
                // apostrophes as delimiters, even inside an ASCII-quoted string.
                string scriptValue = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                string gameValue = Convert.ToBase64String(Encoding.Unicode.GetBytes(game));
                string command = "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; " +
                    "[Console]::OutputEncoding=New-Object System.Text.UTF8Encoding($false); " +
                    "$gaiaScript=[Text.Encoding]::Unicode.GetString([Convert]::FromBase64String('" + scriptValue + "')); " +
                    "$gaiaGame=[Text.Encoding]::Unicode.GetString([Convert]::FromBase64String('" + gameValue + "')); " +
                    "try { & $gaiaScript -GameFolder $gaiaGame; $ok=$?; if (!$ok) { if ($LASTEXITCODE) { exit $LASTEXITCODE }; exit 1 }; exit 0 } catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }";
                var start = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
                    Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " +
                        Convert.ToBase64String(Encoding.Unicode.GetBytes(command)),
                    WorkingDirectory = Path.GetFullPath(packageRoot),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                object outputLock = new object();
                DataReceivedEventHandler receive = (sender, args) =>
                {
                    if (args.Data != null && output != null)
                        lock (outputLock) output(args.Data);
                };
                using (var process = new Process { StartInfo = start })
                {
                    process.OutputDataReceived += receive;
                    process.ErrorDataReceived += receive;
                    process.Start();
                    process.StandardInput.Close();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit(); // Also drains both asynchronous output readers.
                    return process.ExitCode;
                }
        }
    }
}
