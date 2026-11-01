using System;
namespace Memoria.Accessibility.Navigation
{
    // Knowledge is created only by an executed jump followed by a real, ready arrival.
    public sealed class ObservedExitJourney
    {
        private int _stage,_destination;
        private TargetIdentity _source;
        private string _scope,_save;
        private long _generation;
        public void Reset(){_stage=0;_scope=_save=null;}
        public void Capture(TargetIdentity source,string scope,long generation,string save)
        {Reset();if(source.Kind!=TargetKind.FieldExit || String.IsNullOrEmpty(scope))return;_source=source;_scope=scope;_generation=generation;_save=save;_stage=1;}
        public void Executed(int destination){if(_stage!=1){Reset();return;}_destination=destination;_stage=2;}
        public void Detach(bool battle){if(battle || _stage!=2){Reset();return;}_stage=3;}
        public void Attach(bool world){if(world || _stage!=3){Reset();return;}_stage=4;}
        public bool TryComplete(NavigationContext context,int map,out TargetIdentity source,out string scope,out string label)
        {
            source=default(TargetIdentity);scope=label=null;if(_stage==0 || context==null)return false;
            if(context.SaveIdentity!=_save || context.GateReason==GateReason.Battle || context.BattleTransition!=BattleTransition.None){Reset();return false;}
            if(_stage!=4 || !context.CanControl)return false;
            if(context.Generation<=_generation || map!=_destination || context.LogicalMapKey==_source.MapKey || context.LogicalMapKey==null || !context.LogicalMapKey.StartsWith("field:")){Reset();return false;}
            if(String.IsNullOrEmpty(context.CurrentLocation))return false;
            source=_source;scope=_scope;label="Last led to "+context.CurrentLocation;Reset();return true;
        }
    }
}
