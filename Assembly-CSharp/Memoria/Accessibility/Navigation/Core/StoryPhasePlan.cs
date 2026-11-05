using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    // A required local action before a counter changes. Rules are kept in
    // story order and bind the native director of every room they cover.
    public sealed class StoryPhase
    {
        public int Floor,Ceiling;
        public Dictionary<int,string> Directors;
        public int[] ScopeFields;
        public byte[][] Guards;
        public StoryPhaseItem[] Items;
        public StoryPhaseLocal Local;
        public StoryStage Stage;
    }
    public sealed class StoryPhaseItem
    {
        public int NativeId;
        public bool Owned;
    }
    // Some floors in one native scene are linked only by a lift or a jump.
    // The actual supporting triangle identifies the current side.
    public sealed class StoryPhaseLocal
    {
        public int Field;
        public string Walkmesh;
        public int[] ActiveTriangles;
        public byte[][] MapGuards;
    }
    public sealed class StoryPhasePlan
    {
        private readonly Dictionary<int,List<StoryPhase>> _rooms=new Dictionary<int,List<StoryPhase>>();
        private readonly Dictionary<int,StoryStage> _unavailable=new Dictionary<int,StoryStage>();
        public StoryPhasePlan(IList<StoryPhase> phases)
        {
            if(phases==null)return;
            foreach(var phase in phases)if(phase!=null && phase.Stage!=null && phase.Directors!=null) {
                IEnumerable<int> scope=phase.ScopeFields==null?(IEnumerable<int>)phase.Directors.Keys:phase.ScopeFields;
                foreach(int room in scope) {
                    if(!phase.Directors.ContainsKey(room))continue;
                    List<StoryPhase> list;if(!_rooms.TryGetValue(room,out list)){list=new List<StoryPhase>();_rooms[room]=list;}
                    list.Add(phase);
                }
                if(!_unavailable.ContainsKey(phase.Stage.Milestone))_unavailable.Add(phase.Stage.Milestone,new StoryStage{
                    Milestone=phase.Stage.Milestone,InformationOnly=true,InformationReason="The current puzzle or conversation state could not be read."});
            }
        }
        public StoryStage Select(int progress,int field,Func<int,string> scripts,Func<byte[],bool?> evaluate,Func<StoryPhaseLocal,bool?> readLocal=null,Func<int,int?> readItem=null)
        {
            List<StoryPhase> phases;if(!_rooms.TryGetValue(field,out phases))return null;
            StoryStage unreviewedSide=null;
            foreach(var phase in phases) {
                if(progress<phase.Floor || progress>=phase.Ceiling)continue;
                if(scripts==null || scripts(0)!=phase.Directors[field])return _unavailable[phase.Stage.Milestone];
                bool unknown=false,rejected=false;
                foreach(var guard in phase.Guards??new byte[0][]) {
                    bool? state=evaluate==null?null:evaluate(guard);
                    if(state==false){rejected=true;break;}if(!state.HasValue)unknown=true;
                }
                if(!rejected && phase.Items!=null)foreach(var item in phase.Items) {
                    int? count=readItem==null?null:readItem(item.NativeId);
                    if(!count.HasValue){unknown=true;continue;}
                    if((count.Value>0)!=item.Owned){rejected=true;break;}
                }
                if(rejected)continue;
                if(phase.Local!=null && phase.Local.Field==field) {
                    bool? present=readLocal==null?null:readLocal(phase.Local);
                    if(present==false){unreviewedSide=_unavailable[phase.Stage.Milestone];continue;}if(!present.HasValue)unknown=true;
                }
                if(unknown)return _unavailable[phase.Stage.Milestone];
                return phase.Stage;
            }
            return unreviewedSide;
        }
    }
}
