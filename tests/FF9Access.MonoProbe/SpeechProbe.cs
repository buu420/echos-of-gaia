using System;
using System.IO;
using System.Reflection;
internal static class SpeechProbe
{
    private static int Main(string[] args)
    {
        try {
            if(args.Length!=2)throw new ArgumentException("Speech assembly and expected architecture required");
            string architecture=IntPtr.Size==8?"x64":"x86";
            if(args[1]!=architecture)throw new InvalidOperationException("Mono architecture mismatch");
            Assembly reader=Assembly.LoadFrom(args[0]);
            string hostBase=Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd('\\');
            string readerBase=Path.GetFullPath(Path.GetDirectoryName(reader.Location)).TrimEnd('\\');
            if(String.Equals(hostBase,readerBase,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Probe must host speech from a separate library directory");
            Console.WriteLine("Exact installed Mono "+Environment.Version+" "+architecture+"; host="+hostBase+"; speech="+readerBase);
            Type managerType=reader.GetType("Memoria.ScreenReader.ScreenReaderManager",true);
            object manager=managerType.GetProperty("Instance").GetValue(null,null);
            try {
                managerType.GetMethod("Initialize").Invoke(manager,null);
                string diagnostic=(string)managerType.GetProperty("BackendDiagnostic").GetValue(manager,null);
                Console.WriteLine("Backend="+diagnostic);
                if(!(bool)managerType.GetProperty("IsInitialized").GetValue(manager,null) || diagnostic==null || !diagnostic.StartsWith("Prism: "))throw new InvalidOperationException("Default Prism loader did not select its architecture-matched library");
                string text="Final Fantasy IX Mono speech validation. Alexandrie, entrée.";
                managerType.GetMethod("Speak").Invoke(manager,new object[]{text,true});
                if((string)managerType.GetProperty("LastAnnouncement").GetValue(manager,null)!=text)throw new InvalidOperationException("Mono speech output was not accepted");
                Console.WriteLine("PASS Mono speech "+architecture+": default assembly-relative Prism and UTF-8 output accepted; audible speech requires listener verification");
                System.Threading.Thread.Sleep(1000);
            }finally{managerType.GetMethod("Dispose").Invoke(manager,null);}
            WorldPlanningProbe.Run(Path.Combine(readerBase,"Assembly-CSharp.dll"));
            AudioDescriptionProbe.Run(Path.Combine(readerBase,"Assembly-CSharp.dll"));
            SpatialAudioProbe.Run(Path.Combine(readerBase,"Assembly-CSharp.dll"),Path.Combine(readerBase,"Memoria.SpatialAudio.dll"),Path.Combine(readerBase,"navigation"));
            return 0;
        }catch(Exception exception){Console.Error.WriteLine("FAIL Mono speech: "+exception);return 1;}
    }
}
