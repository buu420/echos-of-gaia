using System;
namespace Memoria.Accessibility.Navigation
{
    // Cache only an object's identity; callers still read its current mutable state.
    internal sealed class FrameReference<T> where T:class
    {
        private readonly Func<T> _find;
        private readonly Func<T,bool> _alive;
        private T _value;
        private int _frame=-1;
        private long _generation=-1;
        public FrameReference(Func<T> find,Func<T,bool> alive){_find=find;_alive=alive;}
        public T Read(int frame,long generation)
        {
            if(_frame!=frame || _generation!=generation || !_alive(_value)) {
                _value=_find();_frame=frame;_generation=generation;
            }
            return _value;
        }
    }
}
