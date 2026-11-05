using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    public enum StoryStepKind { Talk,Touch,RegionConfirm,RegionWalk,Position }
    public enum StoryTransitKind { Player,Position,Scripted }
    // One native guard (raw EBin expression bytes) owned by a live entry. The owner's instance
    // variables evaluate the expression; a missing or changed owner never proves anything.
    public sealed class StoryStepGuard {public int Sid,Uid=-1;public string Hash;public byte[] Expression;}
    public sealed class StoryStepEffect {public int Sid,Tag,Offset,Uid=-1;public string Hash,Proof="assigned";}
    // A native player action the current field scripts wait for (tools/Export-FF9StorySteps.py):
    // a Confirm/touch handler that writes the counter or advances the field phase, a handler that
    // sets the variable an automatic phase waits on, or the controlled player's own position wait.
    public sealed class StoryStep
    {
        public int Field,Sid,Uid=-1,Milestone=-1,PlayerSid=-1,PlayerUid=-1,WaitSid=-1,WaitUid=-1,Priority=100;public string Hash,Source,WaitHash,Action,TargetLabel;public StoryStepKind Kind;
        public int[] WaitIps=new int[0];public StoryStepEffect[] Effects=new StoryStepEffect[0];
        public StoryStepGuard[] WaitGuards=new StoryStepGuard[0];
        public int? Floor,Ceiling,Minimum,Maximum,SupportFloor,ApproachFacing,MinimumHeight;public OpeningGoalAxis Axis;
        public StoryStepGuard[] Guards=new StoryStepGuard[0],Completion=new StoryStepGuard[0];
    }
    public sealed class StoryStepCatalog
    {
        public const string PositionAction="Walk to the next story position";
        private readonly Dictionary<int,List<StoryStep>> _fields=new Dictionary<int,List<StoryStep>>();
        public StoryStepCatalog(IList<StoryStep> steps)
        {
            if(steps!=null)foreach(var step in steps) {
                if(step==null)continue;List<StoryStep> list;
                if(!_fields.TryGetValue(step.Field,out list)){list=new List<StoryStep>();_fields[step.Field]=list;}
                list.Add(step);
            }
        }
        public int Count{get{int count=0;foreach(var list in _fields.Values)count+=list.Count;return count;}}
        // Candidacy is not eligibility. The native adapter must prove the bound
        // effect on the actual detached trigger/request path before annotation.
        public List<StoryStep> Candidates(int field,int progress,int sid,string hash,Func<int,string> entryHash,Func<StoryStepGuard,int?> evaluate,Func<StoryStep,bool?> atWait,int uid=-1)
        {
            var result=new List<StoryStep>();List<StoryStep> list;
            if(hash==null || !_fields.TryGetValue(field,out list))return result;
            foreach(var step in list) {
                if(step.Kind==StoryStepKind.Position || step.Sid!=sid || step.Uid>=0 && step.Uid!=uid || step.Hash!=hash)continue;
                // Generated assignment paths prove their own branch guards at
                // the prospective interaction point. A reviewed prefix can end
                // before that assignment/choice, so its current prerequisites
                // must additionally be true on the native scene state.
                bool current=step.Source=="reviewed-prefix"?IsCurrent(step,progress,entryHash,evaluate,atWait):Scope(step,progress,entryHash,evaluate,atWait);
                if(current)result.Add(step);
            }
            return result;
        }
        public static bool HasEffect(StoryStep step,ScriptStoryEvidence evidence,Func<int,string> entryHash)
        {
            if(step==null || evidence==null || entryHash==null || step.Effects==null || step.Effects.Length==0)return false;
            int trigger=step.Kind==StoryStepKind.Talk || step.Kind==StoryStepKind.RegionConfirm?3:2;
            foreach(var effect in step.Effects) {
                if(effect==null || entryHash(effect.Sid)!=effect.Hash || effect.Proof!="assigned" && effect.Proof!="reached")return false;bool reached=false;
                var sites=effect.Proof=="reached"?evidence.Reached:evidence.Effects;
                foreach(var site in sites)if(site.Trigger==trigger && site.Sid==effect.Sid && site.Tag==effect.Tag && site.Offset==effect.Offset && (effect.Uid<0 || site.Uid==effect.Uid)){reached=true;break;}
                if(!reached)return false;
            }
            return true;
        }
        // The live entry's own current non-position steps.
        public List<StoryStep> Current(int field,int progress,int sid,string hash,Func<int,string> entryHash,Func<StoryStepGuard,int?> evaluate,Func<StoryStep,bool?> atWait=null)
        {
            var result=new List<StoryStep>();List<StoryStep> list;
            if(hash==null || !_fields.TryGetValue(field,out list))return result;
            foreach(var step in list)if(step.Kind!=StoryStepKind.Position && step.Sid==sid && step.Hash==hash && IsCurrent(step,progress,entryHash,evaluate,atWait))result.Add(step);
            return result;
        }
        // Current position waits of the controlled player (uid player). A wait read from another
        // entry on OBJ(250) belongs to whoever is controlled; a wait in a player entry only to it.
        public List<StoryStep> CurrentPositions(int field,int progress,int player,Func<int,string> entryHash,Func<StoryStepGuard,int?> evaluate,Func<StoryStep,bool?> atWait=null,int playerUid=-1)
        {
            var result=new List<StoryStep>();List<StoryStep> list;
            if(!_fields.TryGetValue(field,out list))return result;
            foreach(var step in list)if(step.Kind==StoryStepKind.Position && (step.PlayerSid<0 || step.PlayerSid==player) && (step.PlayerUid<0 || step.PlayerUid==playerUid) && IsCurrent(step,progress,entryHash,evaluate,atWait))result.Add(step);
            return result;
        }
        public static bool IsCurrent(StoryStep step,int progress,Func<int,string> entryHash,Func<StoryStepGuard,int?> evaluate,Func<StoryStep,bool?> atWait=null)
        {
            if(!Scope(step,progress,entryHash,evaluate,atWait))return false;
            if(step.Guards!=null)foreach(var guard in step.Guards) {
                if(guard==null || guard.Hash==null || entryHash(guard.Sid)!=guard.Hash)return false;
                int? value=evaluate(guard);if(!value.HasValue || value.Value==0)return false;
            }
            return true;
        }
        private static bool Scope(StoryStep step,int progress,Func<int,string> entryHash,Func<StoryStepGuard,int?> evaluate,Func<StoryStep,bool?> atWait)
        {
            if(step==null || entryHash==null || evaluate==null || step.Hash==null || entryHash(step.Sid)!=step.Hash)return false;
            if(step.Floor.HasValue && progress<step.Floor.Value || step.Ceiling.HasValue && progress>=step.Ceiling.Value)return false;
            if(step.Source=="direct-counter" && step.Milestone>=0 && progress>=step.Milestone)return false;
            if((step.WaitSid>=0 || step.WaitIps!=null && step.WaitIps.Length!=0) && (atWait==null || atWait(step)!=true))return false;
            // Completion must be proven. An unreadable completion holds the step.
            if(step.Completion!=null)foreach(var done in step.Completion) {
                if(done==null || done.Hash==null || entryHash(done.Sid)!=done.Hash)continue;
                int? value=evaluate(done);if(value.HasValue && value.Value!=0)return false;
            }
            return true;
        }
        public static string Verb(StoryStepKind kind)
        {
            switch(kind) {
                case StoryStepKind.Talk:return "Talk to";
                case StoryStepKind.RegionConfirm:return "Interact with";
                case StoryStepKind.RegionWalk:return "Walk to";
                case StoryStepKind.Touch:return "Approach";
                default:return null;
            }
        }
        public static string Action(StoryStepKind kind,string label)
        {string verb=Verb(kind);return verb==null?PositionAction:verb+" "+label;}
        // Equivalent active waits may have both a generated row and a reviewed
        // instruction. Their destination is identical; prefer the reviewed wording.
        public static StoryStep SelectPosition(IList<StoryStep> current)
        {
            StoryStep best=null;
            if(current==null)return null;
            foreach(var step in current) {
                if(step==null || step.Kind!=StoryStepKind.Position)continue;
                if(best!=null && (step.Axis!=best.Axis || step.Minimum!=best.Minimum || step.Maximum!=best.Maximum ||
                    step.SupportFloor!=best.SupportFloor || step.MinimumHeight!=best.MinimumHeight))return null;
                if(best==null || step.Priority<best.Priority)best=step;
            }
            return best;
        }
        public static OpeningPositionGoal Goal(StoryStep step,string map,int phaseIdentity)
        {
            if(step==null || step.Kind!=StoryStepKind.Position)return null;
            return new OpeningPositionGoal{Field=step.Field,Map=map,Phase=phaseIdentity,Milestone=step.Milestone,Action=String.IsNullOrEmpty(step.Action)?PositionAction:step.Action,Axis=step.Axis,Minimum=step.Minimum,Maximum=step.Maximum,SupportFloor=step.SupportFloor,MinimumHeight=step.MinimumHeight};
        }
        public static StoryStepKind ParseKind(string kind)
        {
            switch(kind) {
                case "talk":return StoryStepKind.Talk;
                case "touch":return StoryStepKind.Touch;
                case "region-confirm":return StoryStepKind.RegionConfirm;
                case "region-walk":return StoryStepKind.RegionWalk;
                default:return StoryStepKind.Position;
            }
        }
        public static StoryTransitKind ParseTransit(string kind)
        {return kind=="scripted"?StoryTransitKind.Scripted:kind=="position"?StoryTransitKind.Position:StoryTransitKind.Player;}
    }
}
