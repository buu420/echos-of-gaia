using System;
using System.Collections.Generic;
using Assets.Sources.Scripts.UI.Common;
namespace Memoria.Accessibility.Navigation
{
    public static partial class NativeFieldTargets
    {
        // Speaker headings of one of the actor's OWN functions (never a requested actor's dialogue), read
        // from the current field text with opcodes stripped. FieldSpeakerRoles decides whether the
        // heading may label the person now. Cached per script, function and text zone.
        private sealed class SpeakerTexts{public int Zone;public string Language;public string[] Texts;}
        private static readonly Dictionary<byte[],Dictionary<int,SpeakerTexts>> _speakerTexts=new Dictionary<byte[],Dictionary<int,SpeakerTexts>>();
        private static string SpeakerLabel(NavigationContext context,EventEngine engine,Actor actor)
        {return FunctionSpeakerLabel(context,engine,actor,EventEngine.tagTalk);}
        internal static string OwnSpeakerLabel(NavigationContext context,EventEngine engine,Actor actor)
        {return SpeakerLabel(context,engine,actor);}
        // A reviewed native region requests this function of the same actor (FieldInteractionBinding.
        // RequestedTag). MES opens its windows on that actor (gCur), so it is the actor's own speech.
        internal static string RequestedSpeakerLabel(NavigationContext context,EventEngine engine,Actor actor,int tag)
        {return tag<0 || tag==EventEngine.tagTalk?null:FunctionSpeakerLabel(context,engine,actor,tag);}
        private static string FunctionSpeakerLabel(NavigationContext context,EventEngine engine,Actor actor,int tag)
        {
            try {
                if(engine==null || actor==null || actor.ebData==null || engine.GetIP(actor.sid,tag,actor.ebData)==engine.nil)return null;
                int zone=FF9TextTool.FieldZoneId;string language=Memoria.Assets.EmbadedTextResources.CurrentSymbol??Memoria.Assets.Localization.CurrentSymbol;
                Dictionary<int,SpeakerTexts> functions;SpeakerTexts cached=null;
                if(!_speakerTexts.TryGetValue(actor.ebData,out functions) || !functions.TryGetValue(tag,out cached) || cached.Zone!=zone || cached.Language!=language) {
                    var texts=new List<string>();bool explicitSpeaker=false;
                    var code=Decode(engine,actor,tag);
                    int start=engine.GetIP(actor.sid,tag,actor.ebData);
                    if(code.Count==0 || code[code.Count-1].NextOffset!=FunctionEnd(actor.ebData,start))return null;
                    foreach(var instruction in code) {
                        if(instruction.Opcode!=(int)EBin.event_code_binary.MES && instruction.Opcode!=(int)EBin.event_code_binary.MESN &&
                           instruction.Opcode!=(int)EBin.event_code_binary.MESA && instruction.Opcode!=(int)EBin.event_code_binary.MESAN)continue;
                        if(instruction.Opcode==(int)EBin.event_code_binary.MESA || instruction.Opcode==(int)EBin.event_code_binary.MESAN) {
                            explicitSpeaker=true;
                            int? speaker=instruction.Constant(0);if(!speaker.HasValue)return null;
                            var owner=speaker.Value==255?actor:engine.GetObjUID(speaker.Value);
                            if(!System.Object.ReferenceEquals(owner,actor))continue;
                        }
                        if(instruction.Arguments==null || instruction.Arguments.Length==0)return null;
                        int? id=instruction.Arguments[instruction.Arguments.Length-1];if(!id.HasValue)return null;
                        string text=FF9TextTool.FieldText(id.Value);if(String.IsNullOrEmpty(text))return null;
                        texts.Add(FF9TextTool.RemoveOpCode(text));
                    }
                    if(_speakerTexts.Count>256)_speakerTexts.Clear();
                    cached=new SpeakerTexts{Zone=zone,Language=language,Texts=texts.ToArray()};
                    // Explicit UID/self/party selectors depend on the current
                    // instance, not merely shared script bytes. Recheck them.
                    if(!explicitSpeaker) {
                        if(!_speakerTexts.TryGetValue(actor.ebData,out functions)){functions=new Dictionary<int,SpeakerTexts>();_speakerTexts[actor.ebData]=functions;}
                        functions[tag]=cached;
                    }
                }
                return FieldSpeakerRoles.Label(cached.Texts,name=>KnownName(context,engine,actor,name),language);
            }catch(Exception){return null;}
        }
        // Displayed name on this actor/signature, or the native party slot bound to this exact instance.
        private static bool KnownName(NavigationContext context,EventEngine engine,Actor actor,string name)
        {
            string observed;
            if(context!=null && NavigationSaveIdentity.Identities!=null && NavigationSaveIdentity.Identities.TryGetLabel(
                new TargetIdentity(TargetKind.FieldActor,context.LogicalMapKey,actor.uid),LabelSignature(actor),out observed) && observed==name)return true;
            try {
                var party=FF9StateSystem.Common.FF9.party;
                if(party!=null && party.member!=null)for(int i=0;i<party.member.Length && i<4;i++)
                    if(party.member[i]!=null && System.Object.ReferenceEquals(engine.GetObjUID(251+i),actor) && FF9TextTool.RemoveOpCode(party.member[i].Name)==name)return true;
            }catch(Exception){}
            return false;
        }
    }
}
