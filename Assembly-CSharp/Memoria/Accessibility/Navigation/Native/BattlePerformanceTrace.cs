using System;
using System.Diagnostics;
using System.Text;
namespace Memoria.Accessibility.Navigation
{
    // Collect a small startup record, then write it once. Normal combat frames
    // perform only the inactive flag check; this never scans or collects the heap.
    internal static class BattlePerformanceTrace
    {
        private static bool _active;
        private static long _begin, _lastStage, _lastFrame;
        private static int _collections, _gaps, _operations;
        private static StringBuilder _record;
        public static void BeginEncounter()
        {
            _begin=_lastStage=_lastFrame=Stopwatch.GetTimestamp();
            _collections=GC.CollectionCount(0);_gaps=0;_operations=0;_record=new StringBuilder(2048);_active=true;
            Stage("encounter requested");
        }
        public static void Stage(string stage)
        {
            if(!_active)return;
            long now=Stopwatch.GetTimestamp();
            _record.Append(stage).Append(" total_ms=").Append(Milliseconds(now-_begin))
                .Append(" stage_ms=").Append(Milliseconds(now-_lastStage))
                .Append(" collections=").Append(GC.CollectionCount(0)-_collections).Append("; ");
            _lastStage=now;
        }
        // Record at most 32 animation loads during startup, including repeated
        // paths. These timings are buffered with the encounter record; there
        // is no file access, asset lookup or retained Unity reference here.
        public static long BeginOperation()
        {
            return _active && _operations<32?Stopwatch.GetTimestamp():0;
        }
        public static void EndOperation(string operation,string asset,long begin)
        {
            if(!_active || begin==0 || _operations>=32)return;
            _operations++;
            _record.Append(operation).Append(" asset=").Append(asset)
                .Append(" elapsed_ms=").Append(Milliseconds(Stopwatch.GetTimestamp()-begin)).Append("; ");
        }
        public static void ObserveFrame(int mode)
        {
            if(!_active)return;
            long now=Stopwatch.GetTimestamp(),gap=Milliseconds(now-_lastFrame);_lastFrame=now;
            if(gap>=250 && _gaps<4){_gaps++;_record.Append("frame gap_ms=").Append(gap).Append(" mode=").Append(mode).Append("; ");}
            if(now-_begin>30*Stopwatch.Frequency){Stage("startup timing expired");Flush();}
        }
        public static void FinishStartup()
        {
            if(!_active)return;
            Stage("battle startup complete");Flush();
        }
        private static long Milliseconds(long ticks){return ticks*1000/Stopwatch.Frequency;}
        private static void Flush()
        {
            _active=false;string record=_record.ToString();_record=null;
            NavigationRuntime.Trace("Battle transition timings; "+record);
        }
    }
}
