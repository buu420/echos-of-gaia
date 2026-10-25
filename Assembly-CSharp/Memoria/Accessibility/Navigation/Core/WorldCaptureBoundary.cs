using System;
namespace Memoria.Accessibility.Navigation
{
    internal sealed class WorldCaptureBoundary
    {
        private readonly WorldGeometryPreparation _preparation;
        private readonly Action<Exception> _report;
        public bool Unavailable { get; private set; }
        internal WorldCaptureBoundary(WorldGeometryPreparation preparation,Action<Exception> report){_preparation=preparation;_report=report;}
        public void Reset(){Unavailable=false;}
        public bool TryCapture(Action capture)
        {
            if(Unavailable)return false;
            try{capture();return true;}
            catch(Exception exception){Fail(exception);return false;}
        }
        public void Fail(Exception exception)
        {
            if(Unavailable)return;
            Unavailable=true;_preparation.Invalidate();
            try{_report(exception);}catch{ /* A diagnostic cannot interrupt the native loader. */ }
        }
    }
}
