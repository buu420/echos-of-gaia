using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

// A separate process exercises the staged adapter with the installed audio DLL.
// It owns its test engine and never attaches to FF9 or changes game state.
internal static class WorldFootstepAudioProbe
{
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    private static Type _audio;
    private static int _finished;
    private static object[][] _retainedHeap;
    private static void OnFinished(int voice) { Interlocked.Increment(ref _finished); }
    private static object Audio(string method, params object[] args) { return _audio.GetMethod(method).Invoke(null,args); }
    private static void Require(bool condition, string message) { if(!condition)throw new InvalidOperationException(message); }
    private static int Main(string[] args)
    {
        try
        {
            Require(args.Length==4 || (args.Length==5 && (args[4]=="--lifecycle" || args[4]=="--stress")),"Game assembly, native audio library, sample folder and architecture required");
            string architecture=IntPtr.Size==8?"x64":"x86";Require(args[3]==architecture,"Audio probe architecture mismatch");
            Require(LoadLibraryEx(Path.GetFullPath(args[1]),IntPtr.Zero,8)!=IntPtr.Zero,"Installed audio DLL did not load");
            Assembly game=Assembly.LoadFrom(args[0]);_audio=game.GetType("Global.Sound.SaXAudio.SaXAudio",true);
            Require((bool)Audio("Init"),"Probe-owned audio engine initialization failed");
            if(args.Length==5)
            {
                EventInfo finished=_audio.GetEvent("OnVoiceFinished");
                finished.AddEventHandler(null,Delegate.CreateDelegate(finished.EventHandlerType,typeof(WorldFootstepAudioProbe).GetMethod("OnFinished",BindingFlags.NonPublic|BindingFlags.Static)));
            }
            int bus=(int)Audio("CreateBus");Require(bus>0,"Probe-owned sound bus initialization failed");
            Type adapter=game.GetType("Memoria.Accessibility.Navigation.NativeWorldFootsteps",true);
            adapter.GetField("_bus",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,bus);
            var nativePlay=(Func<int,float,int>)Delegate.CreateDelegate(typeof(Func<int,float,int>),adapter.GetMethod("PlaySample",BindingFlags.NonPublic|BindingFlags.Static));
            var nativeStop=(Action<int>)Delegate.CreateDelegate(typeof(Action<int>),adapter.GetMethod("StopSample",BindingFlags.NonPublic|BindingFlags.Static));
            var banks=new List<int>();var voices=new List<int>();int loaded=0;
            Func<int,int> load=delegate(int index){int bank=(int)Audio("BankLoadOggFile",Path.Combine(args[2],(5020+index)+".ogg"));Require(bank>0,"Original native decoder rejected a shipped sample");banks.Add(bank);loaded++;return bank;};
            Func<int,float,int> play=delegate(int bank,float gain){int voice=nativePlay(bank,gain);if(voice>0)voices.Add(voice);return voice;};
            Action<string> report=delegate(string message){throw new Exception("Footstep playback diagnostic: "+message);};
            Type cacheType=game.GetType("Memoria.Accessibility.Navigation.CachedFootstepPlayer",true);
            object cache=Activator.CreateInstance(cacheType,new object[]{10,load,play,nativeStop,report});
            for(int i=0;i<12;i++)cacheType.GetMethod("WarmOne").Invoke(cache,null);
            Require(loaded==10,"Native cache loaded samples repeatedly");
            int foreign=(int)Audio("CreateVoice",banks[0],bus,true);Require(foreign>0,"Control voice was not created");
            Audio("SetLooping",foreign,true);Audio("SetVolume",foreign,0f,0f,false);Require((bool)Audio("Start",foreign),"Control voice did not start");
            for(int i=0;i<10;i++)
            {
                Require((bool)cacheType.GetMethod("Play").Invoke(cache,new object[]{.2f}),"Staged footstep adapter did not start a native voice");
                int voice=voices[voices.Count-1];Require(!(bool)Audio("GetLooping",voice),"Footstep voice loops");
                Require(Math.Abs((float)Audio("GetVolume",voice)-.2f)<.001f,"Footstep gain was not applied before playback");
                Thread.Sleep(10);
            }
            cacheType.GetMethod("Stop").Invoke(cache,null);Thread.Sleep(50);
            foreach(int voice in voices)Require(!(bool)Audio("VoiceExist",voice),"Suspended footstep voice remains active");
            Require((bool)Audio("VoiceExist",foreign),"Footstep cancellation stopped an unrelated voice");
            Require(loaded==10,"Step playback decoded samples again");
            if(args.Length==5)
            {
                Thread.Sleep(100);_finished=0;
                for(int i=0;i<16;i++)
                {
                    int voice=nativePlay(banks[i%banks.Count],.2f);Require(voice>0,"Natural footstep did not start");
                    Stopwatch wait=Stopwatch.StartNew();
                    while((bool)Audio("VoiceExist",voice) && wait.ElapsedMilliseconds<3000)Thread.Sleep(5);
                    Require(!(bool)Audio("VoiceExist",voice),"Natural footstep never completed");
                    while(_finished<i+1 && wait.ElapsedMilliseconds<3000)Thread.Sleep(5);
                    Require(_finished>=i+1,"Native completion did not reach original Mono");
                    Console.WriteLine("Natural callback "+(i+1)+" completed; collecting");
                    wait.Reset();wait.Start();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
                    Console.WriteLine("Collection after callback "+(i+1)+": "+wait.ElapsedMilliseconds+" ms");
                    Require(wait.ElapsedMilliseconds<2000,"Collection stalled after a native callback");
                }
                Console.WriteLine("PASS natural native footstep lifecycle: 16 completion callbacks with repeated full collections; unrelated voice preserved");
                Require((bool)Audio("VoiceExist",foreign),"Natural footsteps stopped unrelated audio");
            }
            if(args.Length==5 && args[4]=="--stress")
            {
                // Keep a pointer-rich heap comparable to the detached world snapshot.
                // Collect while native completion threads enter Mono, rather than
                // waiting until those callbacks have already returned.
                _retainedHeap=new object[268573][];
                for(int i=0;i<_retainedHeap.Length;i++)_retainedHeap[i]=new object[]{new int[12],new float[9],new int[3]};
                Console.WriteLine("Retained pointer-rich heap: "+GC.GetTotalMemory(false)+" bytes");
                int callbacksBefore=_finished;long longest=0;
                for(int cycle=0;cycle<12;cycle++)
                {
                    int[] active=new int[4];
                    for(int i=0;i<active.Length;i++)active[i]=nativePlay(banks[(cycle+i)%banks.Count],0f);
                    Stopwatch wait=Stopwatch.StartNew();bool alive=true;
                    while(alive && wait.ElapsedMilliseconds<5000)
                    {
                        object[][] transient=new object[4096][];
                        for(int i=0;i<transient.Length;i++)transient[i]=new object[]{new int[12],new float[9]};
                        Stopwatch collection=Stopwatch.StartNew();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
                        longest=Math.Max(longest,collection.ElapsedMilliseconds);GC.KeepAlive(transient);
                        alive=false;foreach(int voice in active)alive |= (bool)Audio("VoiceExist",voice);
                    }
                    Require(!alive,"Native voice completion stalled during concurrent full collection");
                    Console.WriteLine("Concurrent callback/collection cycle "+(cycle+1)+" completed; callbacks="+(_finished-callbacksBefore));
                }
                Stopwatch callbacks=Stopwatch.StartNew();
                while(_finished-callbacksBefore<48 && callbacks.ElapsedMilliseconds<5000)Thread.Sleep(5);
                Console.WriteLine("Concurrent callback drain: "+(_finished-callbacksBefore)+"; longest collection="+longest+" ms");
                Require(_finished-callbacksBefore>=48,"Concurrent collection lost a native completion callback");
                Require((bool)Audio("VoiceExist",foreign),"Concurrent completion stopped unrelated audio");
                Console.WriteLine("PASS concurrent native completion/full collection: 48 callbacks; longest collection="+longest+" ms");
                GC.KeepAlive(_retainedHeap);
            }
            nativeStop(foreign);Thread.Sleep(50);
            Require(!(bool)Audio("VoiceExist",foreign),"Control voice did not finish after explicit test cleanup");
            foreach(int bank in banks)Audio("BankRemove",bank);
            Audio("RemoveBus",bus);Audio("Release");
            Console.WriteLine("PASS original Mono/native audio "+architecture+": all 10 unchanged Ogg samples decoded once; staged adapter starts non-looping cached voices at the requested gain; owned cancellation preserves an unrelated voice. Listener audibility and live-game pacing remain unverified.");
            // The standalone probe releases only its own engine before Mono exits.
            return 0;
        }
        catch(Exception exception){Console.Error.WriteLine("FAIL original Mono/native footstep audio: "+exception);return 1;}
    }
}
