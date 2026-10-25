using System;
using System.Threading;

// No game, speech or audio dependencies: the native host must finish foreground
// threads before freeing Mono, while preserving the managed Main return value.
internal static class ForegroundShutdownProbe
{
    private static int Main(string[] args)
    {
        var started = new ManualResetEvent(false);
        var worker = new Thread(delegate() {
            started.Set();
            Thread.Sleep(200);
            Console.WriteLine("PASS Mono foreground shutdown: worker completed");
        });
        worker.IsBackground = false;
        worker.Start();
        if (!started.WaitOne(5000)) return 41;
        Console.WriteLine("Mono foreground shutdown: Main returns 37 before worker completes");
        return 37;
    }
}
