using UnityEngine;
using Assets.Sources.Scripts.UI.Common;
using System;
using Memoria.Prime;
namespace Memoria.Accessibility.Navigation
{
    public static class NativeDisplayedIdentity
    {
        public sealed class Speaker
        {
            internal Actor Actor;internal string Map,Save,Signature;internal long Generation;
        }
        private static string _lastUnassigned;
        public static Speaker Capture(PosObj subject)
        {
            var actor=subject as Actor;if(actor==null || actor.ebData==null)return null;
            var engine=UnityEngine.Object.FindObjectOfType<EventEngine>();
            if(engine==null || engine.gMode!=1 || actor.uid==engine.GetControlUID() || engine.GetObjUID(actor.uid)!=actor)return null;
            return new Speaker{Actor=actor,Map=Map(),Save=NavigationSaveIdentity.Current,Generation=NavigationLifecycleBridge.Generation,Signature=NativeFieldTargets.LabelSignature(actor)};
        }
        public static void Observe(PosObj nativeSpeaker,string displayed)
        {
            Observe(Capture(nativeSpeaker),displayed);
        }
        public static void Observe(Speaker speaker,string displayed)
        {
            string name;if(String.IsNullOrEmpty(displayed) || !DisplayedSpeakerLabel.TryRead(FF9TextTool.RemoveOpCode(displayed),out name))return;
            if(speaker==null) {
                string key=NavigationLifecycleBridge.Generation+":"+name;
                if(_lastUnassigned!=key){_lastUnassigned=key;Log.Message("[FF9Access] Displayed name has no native actor attribution: {0}",name);}return;
            }
            var actor=speaker.Actor;
            var engine=UnityEngine.Object.FindObjectOfType<EventEngine>();
            if(engine==null || engine.gMode!=1 || actor.uid==engine.GetControlUID() || engine.GetObjUID(actor.uid)!=actor ||
                speaker.Generation!=NavigationLifecycleBridge.Generation || speaker.Map!=Map() || speaker.Save!=NavigationSaveIdentity.Current || speaker.Signature!=NativeFieldTargets.LabelSignature(actor))return;
            var id=new TargetIdentity(TargetKind.FieldActor,speaker.Map,actor.uid);string previous;
            if(NavigationSaveIdentity.Identities.TryGetLabel(id,speaker.Signature,out previous) && previous==name)return;
            NavigationSaveIdentity.Identities.Record(id,name,speaker.Signature);
            Log.Message("[FF9Access] Target name observed: field={0}; uid={1}; label={2}",FF9StateSystem.Common.FF9.fldMapNo,actor.uid,name);
        }
        private static string Map(){return "field:"+FF9StateSystem.Common.FF9.fldMapNo+":"+FF9StateSystem.Field.SceneName;}
    }
}
