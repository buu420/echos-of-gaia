using System;
using Memoria;
using Memoria.Accessibility.AudioDescriptions;
using Memoria.ScreenReader;
using static AudioDescriptionIntegration.Tests.World;
namespace AudioDescriptionIntegration.Tests
{
    internal static partial class Scenarios
    {
        public static void T01_FirstTimedCommandPreemptsOwnedRecordingImmediately()
        {
            Install();NativeFieldDescriptions.Raise(Crouch,CrouchText,Always);Tick(1);
            Check(Playing,"fixture needs active narration");
            NativeTimedInputContext.Active=true;
            NativeAudioDescriptions.SpeakDialogue(new Dialog(),"Cross");
            Check(!Playing && Spoken("Cross")==1,"first timed command waited behind narration");
            Check(FakeSpeech.Last=="Cross" && FakeSpeech.Outputs[FakeSpeech.Outputs.Count-1].Value,"timed command must interrupt old speech");
        }
        public static void T02_NineTimedCommandsAreIndividualCurrentSubmissions()
        {
            Install();NativeFieldDescriptions.Raise(Crouch,CrouchText,Always);Tick(1);
            NativeTimedInputContext.Active=true;var d=new Dialog();
            for(int i=0;i<9;i++) {
                d.NextPhrase();string prompt="Command "+i;
                NativeAudioDescriptions.SpeakDialogue(d,prompt);
                Check(FakeSpeech.Last==prompt,"timed commands batched or delayed");
            }
            Check(FakeSpeech.Outputs.Count==9,"expected one immediate submission per command");
        }
        public static void T03_DescriptionCuesDuringTimedInputNeverReplayAtResults()
        {
            Install();NativeTimedInputContext.Active=true;
            NativeFieldDescriptions.Raise(Crouch,CrouchText,Always);Tick(1);
            NativeAudioDescriptions.SpeakDialogue(new Dialog(),"Triangle");
            NativeFieldDescriptions.Raise(Nod,NodText,Always);Tick(1);
            NativeTimedInputContext.Active=false;Tick(2);
            NativeAudioDescriptions.SpeakDialogue(new Dialog(),"Nobles impressed.");Tick(2);
            Check(Played==0 && Spoken(CrouchText)==0 && Spoken(NodText)==0,"timed-section descriptions leaked into prompts/results");
            Check(Spoken("Nobles impressed.")==1,"result dialogue was lost");
        }
        public static void T04_BackendRecoveryRetriesOnlyStillShownLatestCommand()
        {
            Install();NativeTimedInputContext.Active=true;FakeSpeech.Accept=false;var d=new Dialog();
            NativeAudioDescriptions.SpeakDialogue(d,"Old Cross");d.NextPhrase();
            NativeAudioDescriptions.SpeakDialogue(d,"Current Square");FakeSpeech.Accept=true;
            typeof(ScreenReaderManager).GetField("_nextAttempt",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ScreenReaderManager.Instance,DateTime.MinValue);
            Tick(2,2f);
            Check(Spoken("Current Square")==1 && Spoken("Old Cross")==0,"expired timed commands replayed after backend recovery");
        }
        public static void T05_ClosedTimedCommandNeverReplaysAfterTheRound()
        {
            Install();NativeTimedInputContext.Active=true;FakeSpeech.Accept=false;var d=new Dialog();
            NativeAudioDescriptions.SpeakDialogue(d,"Expired Circle");d.gameObject.activeInHierarchy=false;
            NativeAudioDescriptions.DialogueHidden(d);NativeTimedInputContext.Active=false;
            FakeSpeech.Accept=true;
            typeof(ScreenReaderManager).GetField("_nextAttempt",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ScreenReaderManager.Instance,DateTime.MinValue);
            Tick(2,2f);
            NativeAudioDescriptions.SpeakDialogue(new Dialog(),"Round finished.");
            Check(Spoken("Expired Circle")==0 && Spoken("Round finished.")==1,"round end replayed an expired command");
        }
        public static void T06_TimedCommandsPreemptOwnedFallbackWithoutGlobalSilence()
        {
            Install(fieldClips:false);NativeFieldDescriptions.Raise(Crouch,CrouchText,Always);Tick(1);
            Check(NativeDescriptionSpeech.Last.Playing,"fixture needs owned fallback");int silences=FakeSpeech.Silences;
            NativeTimedInputContext.Active=true;NativeAudioDescriptions.SpeakDialogue(new Dialog(),"Up");
            Check(!NativeDescriptionSpeech.Last.Playing && FakeSpeech.Last=="Up","owned fallback obscured timed prompt");
            Check(FakeSpeech.Silences==silences,"cancelling owned narration globally silenced the screen reader");
        }
        public static void T07_ClosingAnimationCannotRetryAnExpiredCommand()
        {
            Install();NativeTimedInputContext.Active=true;FakeSpeech.Accept=false;var d=new Dialog();
            NativeAudioDescriptions.SpeakDialogue(d,"Expired Triangle");
            // Native Hide clears IsActive before its closing animation ends.
            // The object and parser remain alive until DialogueHidden later.
            d.IsActive=false;FakeSpeech.Accept=true;
            typeof(ScreenReaderManager).GetField("_nextAttempt",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ScreenReaderManager.Instance,DateTime.MinValue);
            Tick(2,2f);
            Check(Spoken("Expired Triangle")==0,"closing command replayed while its object/parser still existed");
        }
        public static void T08_FocusAndPauseHoldOnlyTheStillOpenCommand()
        {
            Install();NativeTimedInputContext.Active=true;HonoInputManager.Active=false;var d=new Dialog();
            NativeAudioDescriptions.SpeakDialogue(d,"Old Cross");Tick(1);
            Check(Spoken("Old Cross")==0,"background prompt spoke");
            HonoInputManager.Active=true;UIManager.Instance.IsPause=true;d.NextPhrase();
            NativeAudioDescriptions.SpeakDialogue(d,"Current Circle");Tick(1);
            Check(Spoken("Current Circle")==0,"paused prompt spoke");
            UIManager.Instance.IsPause=false;Tick(1);
            Check(Spoken("Current Circle")==1 && Spoken("Old Cross")==0,"focus/pause recovery replayed an old command");
        }
    }
}
