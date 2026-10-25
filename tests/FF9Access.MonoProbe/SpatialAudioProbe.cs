using System;
using System.IO;
using System.Reflection;
using System.Threading;
internal static class SpatialAudioProbe
{
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    internal static void Run(string library,string native)
    {Run(library,native,null);}
    internal static void Run(string library,string native,string assets)
    {
        var game=Assembly.LoadFrom(library);var hrtf=game.GetType("Memoria.Accessibility.Navigation.SteamAudioHrtf",true);var point=game.GetType("Memoria.Accessibility.Navigation.NavigationPoint",true);
        object renderer=Activator.CreateInstance(hrtf,new object[]{native});var mix=hrtf.GetMethod("Mix");var reset=hrtf.GetMethod("Reset");
        try {
            float[] input=new float[512];input[0]=.5f;float[] right=new float[1024],left=new float[1024],front=new float[1024],back=new float[1024];
            object[] dirs={Activator.CreateInstance(point,new object[]{1f,0f,0f}),Activator.CreateInstance(point,new object[]{-1f,0f,0f}),Activator.CreateInstance(point,new object[]{0f,0f,-1f}),Activator.CreateInstance(point,new object[]{0f,0f,1f})};
            float[][] blocks={right,left,front,back};
            for(int i=0;i<4;i++){reset.Invoke(renderer,new object[]{0});mix.Invoke(renderer,new object[]{0,input,0,512,dirs[i],1f,blocks[i]});}
            double rl=0,rr=0,ll=0,lr=0,difference=0;int rightL=-1,rightR=-1,peakL=0,peakR=0;
            for(int i=0;i<512;i++){
                rl+=right[2*i]*right[2*i];rr+=right[2*i+1]*right[2*i+1];ll+=left[2*i]*left[2*i];lr+=left[2*i+1]*left[2*i+1];
                difference+=Math.Abs(front[2*i]-back[2*i])+Math.Abs(front[2*i+1]-back[2*i+1]);
                if(rightL<0 && Math.Abs(right[2*i])>.00001)rightL=i;if(rightR<0 && Math.Abs(right[2*i+1])>.00001)rightR=i;
                if(Math.Abs(right[2*i])>Math.Abs(right[2*peakL]))peakL=i;if(Math.Abs(right[2*i+1])>Math.Abs(right[2*peakR+1]))peakR=i;
                Check(!Single.IsNaN(right[2*i]) && !Single.IsInfinity(right[2*i]),"Non-finite HRTF output");
            }
            Console.WriteLine("Native impulse diagnostics; energies="+rl+"/"+rr+"; onsets="+rightL+"/"+rightR+"; peaks="+peakL+"/"+peakR+"; front/rear="+difference);
            // FFT filter pre-ringing can start in the same block for both ears.
            // The impulse peaks identify the actual interaural delay.
            Check(rr>rl && ll>lr,"HRTF left/right directions are reversed or mono");Check(peakR<peakL,"Right source lacks interaural peak-delay difference");Check(difference>.001,"Front and rear HRTF filters are identical");
            bool tail=true;int frames=0;while(tail && frames++<128){var block=new float[1024];tail=(bool)mix.Invoke(renderer,new object[]{0,input,0,0,dirs[3],1f,block});}Check(!tail,"Native HRTF tail never completed");
            Console.WriteLine("PASS Spatial HRTF "+(IntPtr.Size*8)+" bit; right energies="+rl+"/"+rr+"; left="+ll+"/"+lr+"; right peaks="+peakL+"/"+peakR+"; front/rear difference="+difference+"; tail blocks="+frames);
        }finally{((IDisposable)renderer).Dispose();}
        var wave=game.GetType("Memoria.Accessibility.Navigation.SpatialWaveOutput",true);object output=Activator.CreateInstance(wave);
        try {
            for(int i=0;i<3;i++)Check((bool)wave.GetMethod("TryWrite").Invoke(output,new object[]{new short[1024]}),"Stereo waveOut rejected silent block");
            Check((int)wave.GetProperty("PendingCount").GetValue(output,null)<=3,"Unbounded stereo queue");wave.GetMethod("Stop").Invoke(output,null);Check((int)wave.GetProperty("PendingCount").GetValue(output,null)==0,"Stereo stop retained queued sound");
        }finally{((IDisposable)output).Dispose();}
        Console.WriteLine("PASS Spatial waveOut "+(IntPtr.Size*8)+" bit; three bounded silent stereo blocks; reset and cleanup");
        if(assets!=null){
            var workerType=game.GetType("Memoria.Accessibility.Navigation.FieldSpatialAudioWorker",true);object worker=Activator.CreateInstance(workerType,new object[]{assets,native});
            var deadline=DateTime.UtcNow.AddSeconds(5);try{
                while(!(bool)workerType.GetProperty("Ready").GetValue(worker,null) && workerType.GetProperty("Failure").GetValue(worker,null)==null && DateTime.UtcNow<deadline)Thread.Sleep(2);
                Check((bool)workerType.GetProperty("Ready").GetValue(worker,null),"Background HRTF/asset preparation failed: "+workerType.GetProperty("Failure").GetValue(worker,null));
            }finally{((IDisposable)worker).Dispose();}
            deadline=DateTime.UtcNow.AddSeconds(3);while(!(bool)workerType.GetProperty("Finished").GetValue(worker,null) && DateTime.UtcNow<deadline)Thread.Sleep(2);
            Check((bool)workerType.GetProperty("Finished").GetValue(worker,null),"Background spatial worker did not release");Check(workerType.GetProperty("Failure").GetValue(worker,null)==null,"Spatial worker cleanup failed");
            Console.WriteLine("PASS Spatial worker "+(IntPtr.Size*8)+" bit; three pinned recordings; asynchronous native preparation and cleanup");
        }
    }
}
