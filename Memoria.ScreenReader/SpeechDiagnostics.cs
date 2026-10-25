using System;
using System.IO;
namespace Memoria.ScreenReader
{
    internal static class SpeechDiagnostics
    {
        public static string LastWriteFailure { get; private set; }
        public static string LogPath { get; private set; }
        public static void Write(string message)
        {
            try {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FF9Access");
                Directory.CreateDirectory(directory);
                LogPath = Path.GetFullPath(Path.Combine(directory, "speech.log"));
                File.AppendAllText(LogPath, DateTime.UtcNow.ToString("o") + " [" + System.Diagnostics.Process.GetCurrentProcess().Id + "] " + message + Environment.NewLine);
                LastWriteFailure = null;
            } catch (IOException exception) { LastWriteFailure = exception.ToString(); } catch (UnauthorizedAccessException exception) { LastWriteFailure = exception.ToString(); }
        }
    }
}
