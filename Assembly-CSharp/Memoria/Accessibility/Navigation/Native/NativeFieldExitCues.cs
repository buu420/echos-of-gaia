using System;
using System.Collections.Generic;
using System.IO;
using Memoria.Prime;
using Memoria.ScreenReader;
using UnityEngine;
namespace Memoria.Accessibility.Navigation
{
    // Keep the established lifecycle boundary; all field cue kinds now use
    // detached snapshots and a dedicated genuine HRTF worker.
    public static class NativeFieldExitCues
    {
        private static readonly FieldSpatialCueTracker _tracker=new FieldSpatialCueTracker();
        private static FieldSpatialAudioWorker _worker;private static double _nextProbe;private static long _epoch;
        private static string _visit;private static bool _quitting,_failed,_reported,_readyLogged,_suspended=true;
        static NativeFieldExitCues()
        {
            NavigationLifecycleBridge.Releasing+=reason=>{if(reason==StopReason.FocusLost || reason==StopReason.Disconnected || reason==StopReason.Battle || reason==StopReason.MapChanged || reason==StopReason.Menu || reason==StopReason.Dialogue || reason==StopReason.SaveChanged || reason==StopReason.UnknownContext)Suspend();};
            GameLoopManager.Quit+=delegate{_quitting=true;_tracker.Reset();if(_worker!=null)_worker.Dispose();};
        }
        private static float Gain(FieldSpatialCueKind kind)
        {
            bool enabled=kind==FieldSpatialCueKind.Exit?Configuration.Accessibility.FieldExitSounds:kind==FieldSpatialCueKind.Npc?Configuration.Accessibility.FieldNpcSounds:Configuration.Accessibility.FieldInteractableSounds;
            int volume=kind==FieldSpatialCueKind.Exit?Configuration.Accessibility.FieldExitSoundVolume:kind==FieldSpatialCueKind.Npc?Configuration.Accessibility.FieldNpcSoundVolume:Configuration.Accessibility.FieldInteractableSoundVolume;
            return FieldExitProximityTracker.VolumeGain(enabled,volume,SoundLib.SoundEffectIsMute,Configuration.Audio.SoundVolume);
        }
        private static bool Enabled(NavigationContext context)
        {return FieldExitProximityTracker.CanPlay(context) && (Gain(FieldSpatialCueKind.Exit)>0 || Gain(FieldSpatialCueKind.Npc)>0 || Gain(FieldSpatialCueKind.Interactable)>0);}
        public static void UpdateGates(NavigationContext context){if(!_quitting && !Enabled(context))Suspend();}
        public static void Tick(NavigationContext context,CatalogResult catalog)
        {
            if(_quitting)return;
            try {
                if(!Enabled(context) || catalog==null){Suspend();return;}
                if(_failed)return;if(_worker==null)Prepare();
                string failure=_worker.Failure;if(failure!=null)throw new InvalidOperationException(failure);
                if(_worker.Ready && !_readyLogged){_readyLogged=true;NavigationRuntime.Trace("Positional field sounds ready; backend=Steam Audio 4.6.1 HRTF; bits="+(IntPtr.Size*8)+"; sources=NPC/interactable/exit; queue<=64 ms");}
                string visit=context.Generation+":"+context.LogicalMapKey+":"+context.SaveIdentity+":"+context.PlayerIdentity;
                if(_visit!=visit){Suspend();_visit=visit;}
                _suspended=false;
                double now=Time.realtimeSinceStartup*1000d;if(now<_nextProbe)return;_nextProbe=now+200;
                var eligible=new List<NavigationTarget>();
                float exitGain=Gain(FieldSpatialCueKind.Exit),npcGain=Gain(FieldSpatialCueKind.Npc),objectGain=Gain(FieldSpatialCueKind.Interactable);
                foreach(var target in catalog.Targets)if(target!=null && (target.Category==NavigationCategory.Exits?exitGain:target.Category==NavigationCategory.People?npcGain:target.Category==NavigationCategory.Objects || target.Category==NavigationCategory.Items?objectGain:0)>0)eligible.Add(target);
                var cues=_tracker.Update(context,eligible,now,()=>NativeFieldGeometry.Read(context));
                var audible=new List<FieldSpatialCue>();foreach(var cue in cues){float gain=cue.Gain*Gain(cue.Kind);if(gain>0)audible.Add(new FieldSpatialCue(cue.Kind,cue.Identity,cue.Position,cue.Direction,gain,cue.PulseSerial));}
                _worker.Submit(audible.ToArray(),_epoch);
            }catch(Exception exception){_failed=true;Suspend();Report(exception.Message);}
        }
        private static void Prepare()
        {
            var architecture=Directory.GetParent(Application.dataPath);if(architecture==null || architecture.Parent==null)throw new InvalidOperationException("Proximity recording installation path unavailable");
            string assets=Path.Combine(architecture.Parent.FullName,"accessibility/navigation"),library=Path.Combine(Application.dataPath,"Managed/Memoria.SpatialAudio.dll");
            _worker=new FieldSpatialAudioWorker(assets,library);
        }
        public static void Suspend()
        {
            if(_suspended)return;_suspended=true;_tracker.Reset();_nextProbe=0;_visit=null;_epoch++;
            if(!_quitting && _worker!=null)_worker.Submit(new FieldSpatialCue[0],_epoch);
        }
        private static void Report(string message)
        {
            if(_reported)return;_reported=true;Log.Message("[FF9Access] Positional field sounds unavailable: {0}",message);NavigationRuntime.Trace("Positional field sounds unavailable: "+message);
            ScreenReaderManager.Instance.Speak("Positional NPC, interactable and exit sounds are unavailable. See the accessibility log.",false);
        }
    }
}
