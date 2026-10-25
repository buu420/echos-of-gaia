using System;
using System.Linq;
using System.Reflection;
namespace FF9Access.Tests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string suite = args.Length == 2 && args[0] == "--suite" ? args[1] : "All";
            int count = 0;
            try
            {
                if (args.Length == 2 && args[0] == "--verify-input") { NativeReaderVerification.Run(args[1]); return 0; }
                if (args.Length == 2 && args[0] == "--verify-speech-lifetime") { NativeReaderVerification.VerifySpeechLifetime(args[1]); return 0; }
                if (args.Length == 2 && args[0] == "--verify-world-preparation") { NativeReaderVerification.VerifyWorldPreparation(args[1]); return 0; }
                if (args.Length == 2 && args[0] == "--verify-battle-boundaries") { NativeReaderVerification.VerifyBattleBoundaries(args[1]); return 0; }
                if (args.Length == 2 && args[0] == "--verify-world-footsteps") { NativeReaderVerification.VerifyWorldFootsteps(args[1]); return 0; }
                if (args.Length == 2 && args[0] == "--verify-field-exits") { NativeReaderVerification.VerifyFieldExits(args[1]); return 0; }
                if (args.Length == 2 && args[0] == "--verify-world-reachability") { NativeReaderVerification.VerifyWorldReachability(args[1]); return 0; }
                if (args.Length == 2 && args[0] == "--verify-hot-cold") { NativeReaderVerification.VerifyHotCold(args[1]); return 0; }
                if (args.Length == 2 && args[0] == "--verify-forest-routes") { NativeReaderVerification.VerifyForestRoutes(args[1]); return 0; }
                if (args.Length == 2 && args[0] == "--verify-story") { NativeReaderVerification.VerifyStory(args[1]); return 0; }
                if (args.Length == 2 && args[0] == "--verify-descriptions") { NativeReaderVerification.VerifyDescriptions(args[1]); return 0; }
                if (args.Length == 2 && args[0] == "--verify-field-descriptions") { FieldDescriptionNativeVerification.Verify(args[1]); return 0; }
                if (args.Length == 2 && args[0] == "--verify-hot-cold-text") { NativeReaderVerification.VerifyHotColdText(args[1]); return 0; }
                foreach (Type type in Assembly.GetExecutingAssembly().GetTypes().OrderBy(t => t.Name))
                {
                    if (!type.Name.EndsWith("Tests") || (suite != "All" && type.Name != suite + "Tests")) continue;
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static).OrderBy(m => m.Name))
                    {
                        if (method.GetParameters().Length != 0 || method.ReturnType != typeof(void)) continue;
                        try { method.Invoke(null, null); }
                        catch (TargetInvocationException e) { throw new Exception(type.Name + "." + method.Name + ": " + e.InnerException.Message, e.InnerException); }
                        count++;
                    }
                }
                if (count == 0 && suite != "All") throw new Exception("No tests found for " + suite);
                Console.WriteLine("PASS " + suite + ": " + count);
                return 0;
            }
            catch (Exception e) { Console.Error.WriteLine("FAIL " + suite + ": " + e.Message); return 1; }
        }
    }
}
