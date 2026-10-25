namespace Memoria.Accessibility.Navigation
{
    public sealed class WorldCaptureBudget
    {
        private int _frame,_used;private bool _started;
        public int Take(int frame,int limit,int remaining)
        {
            if(!_started || frame!=_frame){_frame=frame;_used=0;_started=true;}
            int count=System.Math.Max(0,System.Math.Min(remaining,limit-_used));_used+=count;return count;
        }
    }
}
