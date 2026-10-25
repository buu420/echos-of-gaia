using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Memoria.Accessibility.AudioDescriptions;
using Memoria.ScreenReader;
using static AudioDescriptionIntegration.Tests.World;
namespace AudioDescriptionIntegration.Tests
{
    internal static partial class Scenarios
    {
        private const string SceneA="scene-a",SceneB="scene-b",SceneText="A reviewed visible action.";
        private static JsonObject BankCatalog()
        {
            JsonObject root;using(var reader=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Memoria.Accessibility.AudioDescriptions.json")))root=JsonNode.Parse(reader.ReadToEnd()).AsObject();
            foreach(string id in new[]{SceneA,SceneB}) {
                var row=root["clips"][0].DeepClone().AsObject();row["id"]=id;row["text"]=SceneText;row["file"]=id+".wav";root["clips"].AsArray().Add(row);
            }
            root["fieldBanks"]=JsonNode.Parse("[{\"id\":\"group-a\",\"fields\":[50,51],\"clips\":[\"scene-a\"]},{\"id\":\"group-b\",\"fields\":[52],\"clips\":[\"scene-b\"]}]");
            return root;
        }
        private static void OpenField(int field)
        {
            var method=typeof(NativeAudioDescriptions).GetMethod("FieldOpened",BindingFlags.Static|BindingFlags.Public);
            Check(method!=null,"field-open bank selection is missing");method.Invoke(null,new object[]{field});
        }
        private static DescriptionAudioBank RuntimeBank(string name)
        {
            var member=typeof(NativeAudioDescriptions).GetField(name,BindingFlags.NonPublic|BindingFlags.Static);
            Check(member!=null,"replaceable scene bank is missing");return (DescriptionAudioBank)member.GetValue(null);
        }
        private static long SceneOwner(){return (long)typeof(NativeAudioDescriptions).GetField("_sceneOwner",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);}
        private static List<Action> SceneFixture(bool controlled=true)
        {
            Install(initialize:false);NativeDescriptionCatalog.Initialize();var catalog=BankCatalog();
            string source=Path.Combine(AppContext.BaseDirectory,"Fixtures",(string)catalog["clips"][0]["file"]);
            foreach(string id in new[]{SceneA,SceneB})File.Copy(source,Path.Combine(Root,"accessibility","audio-descriptions",id+".wav"));
            NativeDescriptionCatalog.Load(catalog.ToJsonString());NativeAudioDescriptions.Initialize();WaitForBank("FieldBank",-1,Crouch);
            var work=new List<Action>();if(controlled)typeof(DescriptionAudioBank).GetField("_queue",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(RuntimeBank("SceneBank"),(Action<Action>)(job=>work.Add(job)));
            return work;
        }
        private static void RejectCatalog(JsonObject catalog,string message)
        {
            bool rejected=false;try{NativeDescriptionCatalog.Load(catalog.ToJsonString());}catch(InvalidOperationException){rejected=true;}catch(ArgumentException){rejected=true;}
            Check(rejected,message);
        }
        public static void B01_CatalogRejectsMovieMembershipAndDuplicateMapOrCueOwners()
        {
            var catalog=BankCatalog();catalog["fieldBanks"][0]["clips"][0]="fmv001-001";RejectCatalog(catalog,"movie cue accepted into scene bank");
            catalog=BankCatalog();catalog["fieldBanks"][1]["fields"][0]=50;RejectCatalog(catalog,"map has two bank owners");
            catalog=BankCatalog();catalog["fieldBanks"][1]["clips"][0]=SceneA;RejectCatalog(catalog,"scene cue has two bank owners");
            catalog=BankCatalog();catalog["fieldBanks"][0]["clips"][0]="missing";RejectCatalog(catalog,"unknown bank cue accepted");
            catalog=BankCatalog();catalog["fieldBanks"][0]["fields"][0]="50";RejectCatalog(catalog,"string field accepted");
            catalog=BankCatalog();catalog["fieldBanks"][0]["fields"][0]=true;RejectCatalog(catalog,"boolean field accepted");
        }
        public static void B08_CatalogRejectsBankIdsWithTrailingNewlines()
        {
            var catalog=BankCatalog();catalog["fieldBanks"][0]["id"]="group-a\n";
            RejectCatalog(catalog,"bank ID with trailing newline accepted");
        }
        public static void B10_CatalogBankTypesAndOptionalShapesStayStrict()
        {
            foreach(string field in new[]{"50.0","5e1","\"50\"","true","null","{}","[]","-1","2147483648"}) {
                string json=BankCatalog().ToJsonString().Replace("[50,51]","["+field+",51]");
                bool rejected=false;try{NativeDescriptionCatalog.Load(json);}catch(InvalidOperationException){rejected=true;}catch(ArgumentException){rejected=true;}
                Check(rejected,"invalid bank map token accepted: "+field);
            }
            foreach(string shape in new[]{"null","{}","50","\"banks\""}) {
                var catalog=BankCatalog();catalog["fieldBanks"]=JsonNode.Parse(shape);RejectCatalog(catalog,"invalid optional bank shape accepted: "+shape);
            }
            var empty=BankCatalog();empty["fieldBanks"]=new JsonArray();NativeDescriptionCatalog.Load(empty.ToJsonString());
            Check(NativeDescriptionCatalog.FieldBanks.Count==0 && NativeDescriptionCatalog.FieldCueBanks.Count==0,"empty bank array did not preserve generic catalog");
            var bounds=BankCatalog();bounds["fieldBanks"][0]["fields"][0]=0;bounds["fieldBanks"][0]["fields"][1]=Int32.MaxValue;
            NativeDescriptionCatalog.Load(bounds.ToJsonString());
            Check(NativeDescriptionCatalog.FieldBanks.ContainsKey(0) && NativeDescriptionCatalog.FieldBanks.ContainsKey(Int32.MaxValue),"valid Int32 map bounds were rejected");
        }
        public static void B09_InFlightAbandonedReadCannotReplacePublishedNewBank()
        {
            NativeDescriptionCatalog.Initialize();var work=new List<Action>();var bank=new DescriptionAudioBank(job=>work.Add(job));
            var fixtures=Path.Combine(AppContext.BaseDirectory,"Fixtures");var oldWave=File.ReadAllBytes(Path.Combine(fixtures,Crouch+".wav"));var newWave=File.ReadAllBytes(Path.Combine(fixtures,Nod+".wav"));
            using(var entered=new ManualResetEventSlim(false))using(var release=new ManualResetEventSlim(false)) {
                bank.Prepare(1001,new[]{NativeDescriptionCatalog.Audio[Crouch]},()=>true,file=>{
                    entered.Set();if(!release.Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException("Abandoned read was not released");return oldWave;
                });
                var old=Task.Run(work[0]);
                try {
                    Check(entered.Wait(TimeSpan.FromSeconds(5)),"old bank never entered its read");
                    bank.Prepare(1002,new[]{NativeDescriptionCatalog.Audio[Nod]},()=>true,file=>newWave);work[1]();byte[] bytes;
                    Check(bank.TryGet(1002,Nod,out bytes) && Object.ReferenceEquals(bytes,newWave),"new bank did not publish while old read was blocked");
                    release.Set();Check(old.Wait(TimeSpan.FromSeconds(5)),"abandoned worker did not finish");
                    Check(bank.TryGet(1002,Nod,out bytes) && Object.ReferenceEquals(bytes,newWave),"abandoned read displaced new bank bytes");
                    Check(!bank.TryGet(1001,Crouch,out bytes) && !bank.TryGet(1002,Crouch,out bytes),"old cue published into replacement bank");
                }finally{release.Set();Check(old.Wait(TimeSpan.FromSeconds(5)),"old worker did not release during cleanup");}
            }
        }
        public static void B02_GlobalAndPerBankMetadataCapsAreIndependent()
        {
            var catalog=BankCatalog();var clips=catalog["clips"].AsArray();
            for(int n=clips.Count;n<4097;n++){var row=clips[0].DeepClone();row["id"]="extra-"+n;row["file"]="extra-"+n+".wav";clips.Add(row);}
            RejectCatalog(catalog,"catalog accepted more than 4096 recordings");
            catalog=BankCatalog();catalog["clips"][0]["bytes"]=33554433;RejectCatalog(catalog,"bank byte budget ignored");
        }
        public static void B07_CatalogSeparatesGenericAndSceneBankCountBudgets()
        {
            var catalog=BankCatalog();var clips=catalog["clips"].AsArray();
            for(int n=clips.Count;n<517;n++){var row=clips[0].DeepClone();row["id"]="extra-"+n;row["file"]="extra-"+n+".wav";clips.Add(row);}
            // Two movies + two scene members leave 513 generic clips.
            RejectCatalog(catalog,"generic bank count budget ignored");
            catalog["fieldBanks"][0]["clips"].AsArray().Add("extra-516");NativeDescriptionCatalog.Load(catalog.ToJsonString());
            Check(NativeDescriptionCatalog.GenericFieldAudio.Length==512,"assigned scene member still counted in generic bank");
            catalog["fieldBanks"][0]["privateReference"]="private.wav";RejectCatalog(catalog,"private bank property accepted");
        }
        public static void B03_SameGroupAndBattleReturnsReuseVerifiedBytesWithoutPreparation()
        {
            var work=SceneFixture();OpenField(50);Check(work.Count==1,"field open did not schedule exactly one bank");work[0]();long owner=SceneOwner();
            NativeFieldDescriptions.Raise(SceneA,SceneText,Always);Tick();Check(Playing && Played==1,"selected scene recording did not play");
            NativeFieldDescriptions.Close();Check(!Playing,"battle close retained old scene playback");
            File.Delete(Path.Combine(Root,"accessibility","audio-descriptions",SceneA+".wav"));OpenField(51);
            Check(SceneOwner()==owner && work.Count==1,"same-group return prepared/read the bank again");
            NativeFieldDescriptions.Raise(SceneA,SceneText,Always);Tick();Check(Played==2 && Playing,"same-group bank bytes were evicted on battle return");
            OpenField(999);Check(!Playing,"unmapped scene did not stop owned output");
            NativeFieldDescriptions.Raise(Crouch,CrouchText,Always);Tick();Check(Played==3,"legacy generic recording unavailable outside groups");
        }
        public static void B04_ReplacedLateWorkerCannotPublishOrReviveOldQueuedCues()
        {
            var work=SceneFixture();OpenField(50);long old=SceneOwner();NativeFieldDescriptions.Raise(SceneA,SceneText,Always);
            OpenField(52);long current=SceneOwner();Check(old!=current,"replacement reused bank owner");
            work[0]();byte[] bytes;Check(!RuntimeBank("SceneBank").TryGet(old,SceneA,out bytes),"late old worker published a replaced bank");
            work[1]();Tick();Check(Played==0 && Spoken(SceneText)==0,"queued previous-scene cue leaked after replacement");
            NativeFieldDescriptions.Raise(SceneB,SceneText,Always);Tick();Check(Played==1 && Playing,"new bank could not play its cue");
        }
        public static void B05_LoadingOrCorruptBankUsesOwnedFallbackWithoutRepreparing()
        {
            var work=SceneFixture();OpenField(50);NativeFieldDescriptions.Raise(SceneA,SceneText,Always);Tick();
            Check(Played==0 && NativeDescriptionSpeech.Outputs.Count==1,"loading bank swallowed accessible text or blocked on preparation");
            NativeFieldDescriptions.Close();File.WriteAllBytes(Path.Combine(Root,"accessibility","audio-descriptions",SceneB+".wav"),new byte[50]);OpenField(52);work[1]();
            NativeFieldDescriptions.Raise(SceneB,SceneText,Always);Tick();Check(Played==0 && NativeDescriptionSpeech.Outputs.Count==2,"corrupt bank lost owned fallback");
            NativeFieldDescriptions.Close();OpenField(52);Check(work.Count==2,"corrupt bank was rehashed on ordinary return");
        }
        public static void B06_FieldOpenedStopsOldSceneButFlushesHeldNativeDialogue()
        {
            var work=SceneFixture();OpenField(50);work[0]();NativeFieldDescriptions.Raise(SceneA,SceneText,Always);Tick();
            NativeAudioDescriptions.SpeakDialogue(new Dialog(),"Already displayed native text.");Check(Spoken("Already displayed native text.")==0,"fixture did not hold dialogue");
            OpenField(51);Check(!Playing && Spoken("Already displayed native text.")==1 && FakeSpeech.Silences==0,"field open dropped or silenced held native text");
            Tick();Check(Played==1,"old field cue revived in same-group scene");
        }
    }
}
