using System;

namespace Memoria.Accessibility.AudioDescriptions
{
    // Read the currently visible movement cue once. A rejected submission
    // never queues yesterday's direction behind the next turn.
    public sealed class TimedTurtleCue
    {
        private int _spoken=-1;
        public void Observe(int? facing,Func<string,bool> speak)
        {
            if(!facing.HasValue || facing.Value<0 || facing.Value>1){_spoken=-1;return;}
            if(_spoken==facing.Value || speak==null)return;
            if(speak(facing.Value==0?"Away":"Watching"))_spoken=facing.Value;
        }
    }
}
