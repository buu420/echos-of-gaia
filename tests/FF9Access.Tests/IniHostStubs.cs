using System;
namespace UnityEngine
{
    // Only the unused path defaults require Unity in the actual Prime INI code.
    // The reader, bindings and string parsing are the production source files.
    public enum RuntimePlatform { WindowsPlayer }
    public static class Random { public static int Range(int minimum, int maximum) { throw new NotSupportedException("Unity random is not part of the INI test."); } }
    public static class Application
    {
        public static string dataPath = "", persistentDataPath = "", temporaryCachePath = "", streamingAssetsPath = "";
        public static RuntimePlatform platform = RuntimePlatform.WindowsPlayer;
    }
}
namespace Memoria.Prime
{
    public static class Log
    {
        public static void Warning(string message, params object[] values) { throw new InvalidOperationException(String.Format(message, values)); }
        public static void Error(Exception error, string message = "", params object[] values) { throw new InvalidOperationException(message, error); }
    }
}
