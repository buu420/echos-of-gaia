using System;
namespace Memoria.ScreenReader
{
    public interface ISpeechBackend : IDisposable
    {
        bool Initialize(); bool Output(string text, bool interrupt); void Stop(); bool IsSpeaking(); string Name { get; }
    }
}
