using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Memoria.Accessibility.Navigation;
using Localization=Memoria.Assets.Localization;
public sealed class Actor:Obj{public string Signature="same-actor";}
public static class FF9TextTool {
    public static int FieldZoneId=2;public static readonly Dictionary<int,string> Texts=new Dictionary<int,string>();
    public static string FieldText(int id){string text;return Texts.TryGetValue(id,out text)?text:null;}
    public static string RemoveOpCode(string text){return Regex.Replace(text??"",@"\[[^\]]*\]|\{[^\}]*\}","");}
}
namespace Memoria.Assets{public static class Localization{public static string CurrentSymbol="US";}}
public sealed class EBin {public enum event_code_binary{MES=0x40,MESN=0x41,MESA=0x95,MESAN=0x96}}
namespace Assets.Sources.Scripts.UI.Common {}
namespace Memoria.Assets{public static class EmbadedTextResources{public static string CurrentSymbol;}}
namespace Memoria.Accessibility.Navigation {
    public static class NavigationSaveIdentity{public static ObservedIdentityStore Identities=new ObservedIdentityStore();}
    public static partial class NativeFieldTargets {
        public static readonly Dictionary<byte[],List<ScriptInstruction>> Codes=new Dictionary<byte[],List<ScriptInstruction>>();
        private static List<ScriptInstruction> Decode(EventEngine engine,Obj actor,int tag){return Codes[actor.ebData];}
        private static string LabelSignature(Actor actor){return actor.Signature;}
        private static int FunctionEnd(byte[] data,int start){return data.Length;}
        public static string ReadSpeaker(NavigationContext context,EventEngine engine,Actor actor){var method=typeof(NativeFieldTargets).GetMethod("SpeakerLabel",BindingFlags.NonPublic|BindingFlags.Static);return (string)method.Invoke(null,method.GetParameters().Length==2?new object[]{engine,actor}:new object[]{context,engine,actor});}
    }
}
internal static partial class Program {
    private static Actor Speaker(EventEngine engine,string heading,params ScriptInstruction[] rows){var actor=new Actor{sid=4,uid=4,ebData=new byte[rows.Length+1]};engine.Objects[4]=actor;for(int i=0;i<rows.Length;i++)rows[i].NextOffset=i+2;NativeFieldTargets.Codes[actor.ebData]=new List<ScriptInstruction>(rows);FF9TextTool.Texts[1]=heading+"\n“Hello.”";return actor;}
    private static ScriptInstruction Message(int id=1,int? speaker=null){return new ScriptInstruction(1,2,(int)(speaker.HasValue?EBin.event_code_binary.MESA:EBin.event_code_binary.MES)){Arguments=speaker.HasValue?new int?[]{speaker,0,128,id}:new int?[]{0,128,id}};}
    private static void SpeakerCase(string name,Action test){try{FF9TextTool.Texts.Clear();Localization.CurrentSymbol="US";Memoria.Assets.EmbadedTextResources.CurrentSymbol=null;NavigationSaveIdentity.Identities=new ObservedIdentityStore();FF9StateSystem.Common.FF9.party.member=new FF9StateSystem.FakeMember[0];test();passes++;Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
    private static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
    private static readonly NavigationContext SpeakerContext=new NavigationContext{LogicalMapKey="field:105"};
    private static void SpeakerCases(){
        SpeakerCase("native multi-word proper names remain unintroduced",()=>{foreach(string name in new[]{"Dante the Signmaker","Innkeeper Hal","Harold Pathknower","Alleyway Jack"}){var engine=new EventEngine();var actor=Speaker(engine,name,Message());Need(NativeFieldTargets.ReadSpeaker(SpeakerContext,engine,actor)==null,"unintroduced name "+name);}});
        SpeakerCase("reviewed visual roles include ordinary single-word headings",()=>{var engine=new EventEngine();var actor=Speaker(engine,"Soldier",Message());Need(NativeFieldTargets.ReadSpeaker(SpeakerContext,engine,actor)=="Soldier","role missing");});
        SpeakerCase("foreign MESA speaker cannot label the executing actor",()=>{var engine=new EventEngine();var actor=Speaker(engine,"Alexandrian Soldier",Message(1,8));engine.Objects[8]=new Actor{sid=8,uid=8};Need(NativeFieldTargets.ReadSpeaker(SpeakerContext,engine,actor)==null,"foreign message labelled actor");});
        SpeakerCase("explicit speaker cache cannot cross shared-script actor instances",()=>{var engine=new EventEngine();var actor=Speaker(engine,"Alexandrian Soldier",Message(1,4));Need(NativeFieldTargets.ReadSpeaker(SpeakerContext,engine,actor)=="Alexandrian Soldier","own explicit message fixture");var other=new Actor{uid=8,sid=4,ebData=actor.ebData};engine.Objects[8]=other;Need(NativeFieldTargets.ReadSpeaker(SpeakerContext,engine,other)==null,"cached explicit speaker crossed instances");});
        SpeakerCase("same actor signature may use its observed name",()=>{var engine=new EventEngine();var actor=Speaker(engine,"Blank",Message());NavigationSaveIdentity.Identities.Record(new TargetIdentity(TargetKind.FieldActor,SpeakerContext.LogicalMapKey,actor.uid),"Blank",actor.Signature);Need(NativeFieldTargets.ReadSpeaker(SpeakerContext,engine,actor)=="Blank","same actor name lost");});
        SpeakerCase("save-wide and unrelated party names establish no actor identity",()=>{var engine=new EventEngine();var actor=Speaker(engine,"Blank",Message());NavigationSaveIdentity.Identities.Record(new TargetIdentity(TargetKind.FieldActor,"other-map",99),"Blank","other");FF9StateSystem.Common.FF9.party.member=new[]{new FF9StateSystem.FakeMember{Name="Blank"}};engine.Objects[251]=new Actor{uid=9,sid=9};Need(NativeFieldTargets.ReadSpeaker(SpeakerContext,engine,actor)==null,"another actor's known name leaked");});
        SpeakerCase("role cache respects current text language",()=>{var engine=new EventEngine();var actor=Speaker(engine,"Alexandrian Soldier",Message());Need(NativeFieldTargets.ReadSpeaker(SpeakerContext,engine,actor)=="Alexandrian Soldier","US role fixture");Localization.CurrentSymbol="FR";FF9TextTool.Texts[1]="Soldat alexandrin\n“Bonjour.”";Need(NativeFieldTargets.ReadSpeaker(SpeakerContext,engine,actor)==null,"cached role crossed locale");});
        SpeakerCase("temporarily unavailable native text is retried",()=>{var engine=new EventEngine();var actor=Speaker(engine,"Alexandrian Soldier",Message());FF9TextTool.Texts.Clear();Need(NativeFieldTargets.ReadSpeaker(SpeakerContext,engine,actor)==null,"missing text gave name");FF9TextTool.Texts[1]="Alexandrian Soldier\n“Hello.”";Need(NativeFieldTargets.ReadSpeaker(SpeakerContext,engine,actor)=="Alexandrian Soldier","missing text was permanently cached");});
        SpeakerCase("a seventeenth different speaker prevents a unique label",()=>{var engine=new EventEngine();var rows=new ScriptInstruction[17];for(int i=0;i<16;i++)rows[i]=Message();rows[16]=Message(2);var actor=Speaker(engine,"Alexandrian Soldier",rows);FF9TextTool.Texts[2]="Female Soldier\n“Hello.”";Need(NativeFieldTargets.ReadSpeaker(SpeakerContext,engine,actor)==null,"truncated heading sample established uniqueness");});
    }
}
