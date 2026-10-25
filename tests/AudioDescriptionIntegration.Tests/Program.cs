using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
namespace AudioDescriptionIntegration.Tests
{
    internal static class Program
    {
        // Every scenario runs in a fresh process: the runtime under test is static by design.
        private static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--scenario")
            {
                var method = typeof(Scenarios).GetMethod(args[1], BindingFlags.Public | BindingFlags.Static);
                try { method.Invoke(null, null); Console.WriteLine("PASS " + args[1]); return 0; }
                catch (TargetInvocationException e) { Console.WriteLine("FAIL " + args[1] + ": " + e.InnerException.Message); return 1; }
                finally { World.Cleanup(); }
            }
            string filter = args.Length == 1 ? args[0] : null; int passed = 0, failed = 0;
            foreach (var m in typeof(Scenarios).GetMethods(BindingFlags.Public | BindingFlags.Static).OrderBy(m => m.Name))
            {
                if (filter != null && !m.Name.StartsWith(filter)) continue;
                var info = new ProcessStartInfo("dotnet", "\"" + Assembly.GetExecutingAssembly().Location + "\" --scenario " + m.Name) { RedirectStandardOutput = true, UseShellExecute = false };
                using (var p = Process.Start(info))
                {
                    string output = p.StandardOutput.ReadToEnd().Trim(); if (!p.WaitForExit(60000)) { p.Kill(); output = "FAIL " + m.Name + ": timeout"; }
                    Console.WriteLine(output.Length == 0 ? "FAIL " + m.Name + ": no result (exit " + p.ExitCode + ")" : output);
                    if (p.ExitCode == 0) passed++; else failed++;
                }
            }
            Console.WriteLine((failed == 0 ? "PASS" : "FAIL") + " AudioDescriptionIntegration: " + passed + " passed, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }
    }
}
