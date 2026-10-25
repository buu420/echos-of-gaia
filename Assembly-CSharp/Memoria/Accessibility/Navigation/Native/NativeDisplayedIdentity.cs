using UnityEngine;
using Assets.Sources.Scripts.UI.Common;
namespace Memoria.Accessibility.Navigation
{
    public static class NativeDisplayedIdentity
    {
        public static void Observe(PosObj nativeSpeaker,string displayed)
        {
            var actor=nativeSpeaker as Actor;if(actor==null)return;
            var engine=Object.FindObjectOfType<EventEngine>();
            if(engine==null || engine.gMode!=1 || actor.uid==engine.GetControlUID() || engine.GetObjUID(actor.uid)!=actor)return;
            string name;if(!DisplayedSpeakerLabel.TryRead(FF9TextTool.RemoveOpCode(displayed),out name))return;
            string map="field:"+FF9StateSystem.Common.FF9.fldMapNo+":"+FF9StateSystem.Field.SceneName;
            NavigationSaveIdentity.Identities.Record(new TargetIdentity(TargetKind.FieldActor,map,actor.uid),name);
        }
    }
}
