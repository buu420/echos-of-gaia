using System;
using System.Collections.Generic;
using System.Linq;
using Memoria.Accessibility.Navigation;

internal static partial class Program
{
    private static void IifaCases()
    {
        var globals=FF9StateSystem.EventState.gEventGlobal;Array.Clear(globals,0,globals.Length);
        var engine=new EventEngine();
        engine.Objects[0]=new Obj{sid=0,uid=0};
        // The director creates different region pairs per entrance even
        // though all four traversals stay in one field ID.
        Spiral(1651,6700,6710,"368c8ef570e575a853459fb6668466c8a48688c6f2e89b64fe741837ae4c9c2f",
            new Dictionary<int,string>{
                {8,"a4e5def85984b778d403f0a87f6eb293c4595cd8fb854fd2ac9dabe3ced33f0b"},
                {6,"86aafb48aeb18f0f35f933438081d8c8233871c9235588d1a3461b71964d1238"},
                {4,"770fde1315fe52c56aadfe7001f950147be67ab77a5d72f6e9e6fcbcd0a8425a"},
                {2,"c55ac21473c286d363871ead24dd7f2f33086caa9f75898e35972b7172fceda4"}
            },new[]{4,6,11,13},new[]{8,6,4,2});
        Spiral(1758,6910,6920,"442deb61259b48a4734799671966826bea736663a2c6d551164a1f227ba91559",
            new Dictionary<int,string>{
                {10,"8448cc88a00eb4407dfcdc01f265004a8520df2134aa70f8f1cccdd5125909ce"},
                {8,"f41626b334fa7070f6a51254ba77403bbcb70255ffc19e23df1ff2df52dd4feb"},
                {6,"20d5c4f219ec498800c43fe3e85ff1fda43102e027a472412c10e7b2dfb39c7e"},
                {4,"b09269810cb3a1953305ffe170b8e70cd631473926697834a4aac47f84f1642d"}
            },new[]{14,19,17,23},new[]{10,8,6,4});

        void Spiral(int field,int counter,int milestone,string director,Dictionary<int,string> entries,int[] entrances,int[] uids)
        {
            SetCounter(counter);
            foreach(int uid in uids)engine.Objects[uid]=new Obj{sid=uid,uid=uid};
            Func<int,string> hashes=s=>s==0?director:entries.TryGetValue(s,out var hash)?hash:null;
            for(int screen=0;screen<entrances.Length;screen++) {
                globals[2]=(byte)entrances[screen];NativeStorySteps.RefreshTransitState();
                var stage=NativeStorySteps.SelectPhase(counter,field,hashes);
                int[] eligible=uids.Where(uid=>NativeStorySteps.Candidates(engine,field,counter,uid,hashes(uid),hashes,uid:uid)
                    .Any(step=>step.Source=="reviewed-prefix" && step.Milestone==milestone)).ToArray();
                Check(stage!=null && eligible.SequenceEqual(new[]{uids[screen]}),"Iifa "+field+" screen "+(screen+1)+" selects only its current forward region");
                if(stage==null || eligible.Length!=1)continue;
                var target=NativeStorySteps.Candidates(engine,field,counter,eligible[0],hashes(eligible[0]),hashes,uid:eligible[0]).First(step=>step.Source=="reviewed-prefix");
                Check(target.TargetLabel==null,"unlabeled Iifa native region keeps the real exit label");
            }
        }
    }
}
