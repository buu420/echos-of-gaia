using System;
using System.Threading;
using Assets.Sources.Graphics.Movie;
using Memoria;
using Memoria.Accessibility.AudioDescriptions;
using Memoria.ScreenReader;
using static AudioDescriptionIntegration.Tests.World;
namespace AudioDescriptionIntegration.Tests
{
    // Each public static method is one scenario, run in its own process. Assertions state the
    // REQUIRED player-facing behavior (no lost dialogue, no stale/duplicate narration).
    internal static partial class Scenarios
    {
        public static void N01_RecordedDescriptionReportsBusyIndependentlyOfFacadeSpeech()
        {
            Install();Check(!NativeAudioDescriptions.NarrationPlaying,"narration busy before playback");
            NativeFieldDescriptions.Raise(Crouch,CrouchText,Always);Tick(1);
            Check(Playing && !ScreenReaderManager.Instance.IsSpeaking(),"fixture needs recorded playback with an idle speech facade");
            Check(NativeAudioDescriptions.NarrationPlaying,"recorded narration omitted from navigation busy state");
            DescriptionWaveOutput.Finish();Tick(1);Check(!NativeAudioDescriptions.NarrationPlaying,"completed recording still blocks navigation");
        }
        public static void N02_OwnedDescriptionSpeechReportsBusyIndependentlyOfFacadeSpeech()
        {
            Install(fieldClips:false);NativeFieldDescriptions.Raise(Crouch,CrouchText,Always);Tick(1);
            Check(NativeDescriptionSpeech.Last.Playing && !ScreenReaderManager.Instance.IsSpeaking(),"fixture needs owned speech with an idle facade");
            Check(NativeAudioDescriptions.NarrationPlaying,"owned narration omitted from navigation busy state");
            NativeDescriptionSpeech.Finish();Tick(1);Check(!NativeAudioDescriptions.NarrationPlaying,"completed owned speech still blocks navigation");
        }
        public static void R01_MovieFallbackCannotCancelPrecedingNativeDialogue()
        {
            NativeDescriptionSpeech.Available=false;
            var m=Film(clips:false); NativeAudioDescriptions.SpeakDialogue(new Dialog(), "Long native line still being read.");
            Present(m,2.05); int silences=FakeSpeech.Silences; Present(m,4.01);
            Check(FakeSpeech.Silences==silences,"movie fallback at cue end silenced preceding native dialogue");
        }
        public static void R02_FieldFallbackCannotCancelPrecedingNativeDialogue()
        {
            NativeDescriptionSpeech.Available=false;
            Install(fieldClips:false);var d=new Dialog();NativeAudioDescriptions.SpeakDialogue(d,"Long native line still being read.");
            NativeAudioDescriptions.DialogueHidden(d);NativeFieldDescriptions.Raise(Crouch,CrouchText,Always);Tick(1);
            int silences=FakeSpeech.Silences;NativeFieldDescriptions.Close();
            Check(FakeSpeech.Silences==silences,"queued field fallback silenced preceding native dialogue");
        }
        public static void R03_PauseDeviceFailureKeepsRemainingFilm()
        {
            var m=Film();Present(m,2.05);DescriptionWaveOutput.ThrowOnPause=true;
            NativeAudioDescriptions.MoviePaused(m);DescriptionWaveOutput.ThrowOnPause=false;Present(m,6.02);
            Check(Played>1 || Spoken("Lights flicker below.")>0,"one pause error disabled remaining verified film");
        }
        public static void R04_ResumeDeviceFailureKeepsRemainingFilm()
        {
            var m=Film();Present(m,2.05);NativeAudioDescriptions.MoviePaused(m);DescriptionWaveOutput.ThrowOnResume=true;
            Present(m,2.15);DescriptionWaveOutput.ThrowOnResume=false;Present(m,6.02);
            Check(Played>1 || Spoken("Lights flicker below.")>0,"one resume error disabled remaining verified film");
        }
        public static void R05_PauseQuitMenuStopsFieldAndDropsItsQueue()
        {
            Install();NativeFieldDescriptions.Raise(Crouch,CrouchText,Always);NativeFieldDescriptions.Raise(Nod,NodText,Always);Tick(1);
            UIManager.Instance.IsPause=true;Tick(1);Check(!Playing,"field recording continued behind pause/quit menu");
            UIManager.Instance.IsPause=false;DescriptionWaveOutput.Finish();Tick(3);
            Check(Played==1 && Spoken(NodText)==0,"pre-menu field gesture revived after resume");
        }
        public static void R06_PauseDeviceErrorDefersFallbackUntilResume()
        {
            var m=Film();Present(m,2.05);DescriptionWaveOutput.ThrowOnPause=true;UIManager.Instance.IsPause=true;
            NativeAudioDescriptions.MoviePaused(m);Check(FakeSpeech.Outputs.Count==0 && NativeDescriptionSpeech.Outputs.Count==0,"pause error started a new caption behind the menu");
            DescriptionWaveOutput.ThrowOnPause=false;UIManager.Instance.IsPause=false;Present(m,2.15);
            Check(Spoken("A ship sails through clouds.")==1,"recovered current caption was not delivered on advancing resume");
        }
        public static void R07_OwnedMovieFallbackPausesAndResumes()
        {
            var m=Film(clips:false);Present(m,2.05);Check(NativeDescriptionSpeech.Outputs.Count==1,"owned fallback absent");
            NativeAudioDescriptions.MoviePaused(m);Check(NativeDescriptionSpeech.Last.Paused,"fallback continued behind pause");
            Present(m,2.15);Check(!NativeDescriptionSpeech.Last.Paused && NativeDescriptionSpeech.Outputs.Count==1,"resume restarted or lost fallback");
        }
        public static void R08_OwnedMovieFallbackStopsOnSkipAndPreservesNativeDialogue()
        {
            var m=Film(clips:false);NativeAudioDescriptions.SpeakDialogue(new Dialog(),"Native line.");Present(m,2.05);
            NativeAudioDescriptions.MovieStopped(m);Check(!NativeDescriptionSpeech.Last.Playing,"skip did not stop owned fallback");
            Check(Spoken("Native line.")==1 && FakeSpeech.Silences==0,"stopping owned fallback affected native dialogue");
        }
        public static void R09_OwnedFallbackStopsOnFocusLoss()
        {
            var m=Film(clips:false);Present(m,2.05);HonoInputManager.Active=false;Tick(1);
            Check(!NativeDescriptionSpeech.Last.Playing,"owned fallback continued without focus");
        }
        public static void R10_StandaloneFieldFallbackUsesOwnedCancellableOutput()
        {
            Install(fieldClips:false);NativeFieldDescriptions.Raise(Crouch,CrouchText,Always);Tick(1);
            Check(NativeDescriptionSpeech.Outputs.Count==1,"standalone field fallback has no owned output");
            UIManager.Instance.IsPause=true;Tick(1);Check(!NativeDescriptionSpeech.Last.Playing,"field fallback continued behind pause");
        }
        public static void R11_MovieSpeechStatusErrorRecoversCurrentCaption()
        {
            var m=Film(clips:false);Present(m,2.05);NativeDescriptionSpeech.ThrowOnStatus=true;Present(m,2.15);
            NativeDescriptionSpeech.ThrowOnStatus=false;Present(m,2.25);
            Check(FakeSpeech.Outputs.Count==1 && FakeSpeech.Last.Contains("A ship sails"),"failed status discarded accepted movie caption");
        }
        public static void R12_FieldSpeechStatusErrorRetainsCaptionAndHeldNativeLine()
        {
            Install(fieldClips:false);NativeFieldDescriptions.Raise(Crouch,CrouchText,Always);Tick(1);
            NativeDescriptionSpeech.ThrowOnStatus=true;NativeAudioDescriptions.SpeakDialogue(new Dialog(),"Native line.");
            NativeDescriptionSpeech.ThrowOnStatus=false;Tick(2);
            Check(FakeSpeech.Outputs.Count==1 && FakeSpeech.Last.StartsWith(CrouchText) && FakeSpeech.Last.EndsWith("Native line."),"failed status lost caption or native line");
        }
        public static void R13_MovieSpeechRecoveryWaitsForAdvancingAndExpires()
        {
            var m=Film(clips:false);Present(m,2.05);NativeDescriptionSpeech.ThrowOnStatus=true;Present(m,2.15);
            NativeDescriptionSpeech.ThrowOnStatus=false;UIManager.Instance.IsPause=true;Present(m,2.2);
            Check(FakeSpeech.Outputs.Count==0,"speech-status recovery started behind pause");
            UIManager.Instance.IsPause=false;Present(m,6.01);
            Check(FakeSpeech.Outputs.Count==1 && FakeSpeech.Last.Contains("Lights flicker"),"expired caption recovered into later shot");
        }
        public static void R14_LateSpeechRecoveryCannotSpillIntoNextShot()
        {
            var m=Film(clips:false);Present(m,2.05);NativeDescriptionSpeech.ThrowOnStatus=true;Present(m,3.2);
            NativeDescriptionSpeech.ThrowOnStatus=false;Present(m,3.3);
            Check(FakeSpeech.Outputs.Count==0,"full caption recovered with less than its duration left");
        }
        public static void R15_RewindCannotRecoverAFutureSceneCaption()
        {
            var m=Film(clips:false);Present(m,2.05);Present(m,6.01);NativeDescriptionSpeech.ThrowOnStatus=true;Present(m,6.15);
            NativeDescriptionSpeech.ThrowOnStatus=false;Present(m,2.15);
            Check(FakeSpeech.Outputs.Count==1 && FakeSpeech.Last.Contains("A ship sails"),"future-scene caption recovered before rewind reset");
        }
        public static void R16_LatePauseErrorRecoveryCannotRestartPastWindow()
        {
            var m=Film();Present(m,2.05);DescriptionWaveOutput.ThrowOnPause=true;NativeAudioDescriptions.MoviePaused(m);
            DescriptionWaveOutput.ThrowOnPause=false;Present(m,3.3);
            Check(NativeDescriptionSpeech.Outputs.Count==0 && FakeSpeech.Outputs.Count==0,"pause-error restart ignored remaining cue duration");
        }
        // ---------- field cues and native dialogue ----------
        public static void F01_RecordedFieldCuePlaysWhenNoDialogueIsOpen()
        {
            Install(); NativeFieldDescriptions.Raise(Crouch, CrouchText, Always); Tick(2);
            Check(Played == 1 && Playing, "observed field action was not voiced"); Check(FakeSpeech.Outputs.Count == 0, "recorded cue also spoken by TTS");
        }
        public static void F02_LineArrivingDuringRecordingIsSpokenAfterIt()
        {
            Install(); NativeFieldDescriptions.Raise(Crouch, CrouchText, Always); Tick(2);
            var d = new Dialog(); NativeAudioDescriptions.SpeakDialogue(d, "Baku: Line A"); Tick(3);
            DescriptionWaveOutput.Finish(); Tick(3);
            Check(Spoken("Line A") == 1, "line held behind the recording was not spoken exactly once");
        }
        public static void F03_LineWhoseWindowClosesDuringRecordingIsNotLost()
        {
            Install(); NativeFieldDescriptions.Raise(Crouch, CrouchText, Always); Tick(2);
            var d = new Dialog(); NativeAudioDescriptions.SpeakDialogue(d, "Baku: Line A"); Tick(1);
            NativeAudioDescriptions.DialogueHidden(d); // player confirms / timed window closes during the recording
            DescriptionWaveOutput.Finish(); Tick(5);
            Check(Spoken("Line A") == 1, "native dialogue line was never spoken");
        }
        public static void F04_TwoLinesDuringOneRecordingAreBothSpokenInOrder()
        {
            Install(); NativeFieldDescriptions.Raise(Crouch, CrouchText, Always); Tick(2);
            var d = new Dialog(); NativeAudioDescriptions.SpeakDialogue(d, "Baku: Line A"); Tick(1);
            d.NextPhrase(); NativeAudioDescriptions.SpeakDialogue(d, "Baku: Line B"); Tick(1);
            DescriptionWaveOutput.Finish(); Tick(5);
            Check(Spoken("Line A") == 1 && Spoken("Line B") == 1, "a dialogue line was overwritten while held");
            Check(Transcript().IndexOf("Line A") < Transcript().IndexOf("Line B"), "dialogue order changed");
        }
        public static void F05_MissingRecordingIsSpokenWithTheCurrentLineInOneUtterance()
        {
            Install(fieldClips: false); var d = new Dialog();
            NativeAudioDescriptions.SpeakDialogue(d, "Baku: Line A"); Tick(1);
            NativeFieldDescriptions.Raise(Crouch, CrouchText, Always); Tick(2);
            d.NextPhrase(); NativeAudioDescriptions.SpeakDialogue(d, "Baku: Line B"); Tick(1);
            Check(Played == 0, "unverified bank played audio");
            Check(FakeSpeech.Last != null && FakeSpeech.Last.StartsWith(CrouchText) && FakeSpeech.Last.EndsWith("Baku: Line B"), "fallback not combined ahead of the line");
        }
        public static void F06_StaleFieldCueIsNeverVoiced()
        {
            Install(); bool current = true; NativeFieldDescriptions.Raise(Crouch, CrouchText, () => current); current = false; Tick(3);
            Check(Played == 0 && FakeSpeech.Outputs.Count == 0, "stale observation was narrated");
        }
        public static void F07_LineArrivingWhileUnfocusedIsNotLost()
        {
            Install(); HonoInputManager.Active = false; var d = new Dialog();
            NativeAudioDescriptions.SpeakDialogue(d, "Baku: Line A"); Tick(3); HonoInputManager.Active = true; Tick(3);
            Check(Spoken("Line A") >= 1, "line shown while the window lacked focus was discarded (original code spoke it immediately)");
        }
        public static void F08_DisabledDescriptionsKeepOriginalDialogueSpeech()
        {
            Install(); Configuration.Accessibility.AudioDescriptions = false; var d = new Dialog();
            NativeAudioDescriptions.SpeakDialogue(d, "Baku: Line A");
            Check(Spoken("Line A") == 1 && FakeSpeech.Outputs[0].Value, "original interrupting dialogue speech changed");
        }
        public static void F09_WaveDeviceFailureFallsBackToSpeech()
        {
            Install(); DescriptionWaveOutput.ThrowOnPlay = true; NativeFieldDescriptions.Raise(Crouch, CrouchText, Always); Tick(3);
            Check(Spoken(CrouchText) == 1, "description swallowed after device failure");
        }
        public static void F10_StuckRecordingFallsBackOnceWithoutRepeatingLater()
        {
            Install(); NativeFieldDescriptions.Raise(Crouch, CrouchText, Always); Tick(1);
            Check(Played == 1, "not started"); Tick(1, 4f); Tick(3);
            Check(Spoken(CrouchText) <= 1, "stuck cue repeated");
            // A later, healthy device should still be usable for the next recording.
            DescriptionWaveOutput.Finish();NativeDescriptionSpeech.Finish(); NativeFieldDescriptions.Raise(Nod, NodText, Always); Tick(3);
            Check(Played == 2, "one late completion permanently disabled every later recording for the session");
        }
        public static void F11_ChoiceIsJoinedToItsHeldQuestion()
        {
            Install(); NativeFieldDescriptions.Raise(Crouch, CrouchText, Always); Tick(1);
            var d = new Dialog(); NativeAudioDescriptions.SpeakDialogue(d, "Baku: Ready?"); NativeAudioDescriptions.SpeakDialogue(d, "Yes", true); Tick(1);
            DescriptionWaveOutput.Finish(); Tick(3);
            Check(Spoken("Baku: Ready? Yes") == 1, "question/choice not delivered together once");
        }
        public static void F12_SceneCloseStopsVoiceAndPending()
        {
            Install(); NativeFieldDescriptions.Raise(Crouch, CrouchText, Always); NativeFieldDescriptions.Raise(Nod, NodText, Always); Tick(1);
            NativeFieldDescriptions.Close(); Tick(1); DescriptionWaveOutput.Finish(); Tick(3);
            Check(!Playing && Played == 1 && Spoken(NodText) == 0, "work survived scene close");
        }
        public static void F13_RejectedDialogueSpeechIsRetriedNotLost()
        {
            Install(); FakeSpeech.Accept = false; var d = new Dialog(); NativeAudioDescriptions.SpeakDialogue(d, "Baku: Line A"); Tick(2);
            FakeSpeech.Accept = true; Thread.Sleep(1200); Tick(3);
            Check(Spoken("Line A") == 1, "rejected line not retried exactly once");
        }
        public static void F14_CueObservedDuringOpenLineIsDeliveredWhenItCloses()
        {
            Install(); var d = new Dialog(); NativeAudioDescriptions.SpeakDialogue(d, "Baku: Line A"); Tick(1);
            NativeFieldDescriptions.Raise(Crouch, CrouchText, Always); Tick(3);
            Check(Played == 0, "description talked over the line still on screen");
            NativeAudioDescriptions.DialogueHidden(d); Tick(2);
            Check(Played == 1, "held description never delivered after the window closed");
        }
        public static void F15_WindowReleasedWithoutHiddenCallbackDoesNotBlockDescriptionsForever()
        {
            Install(); var d = new Dialog(); NativeAudioDescriptions.SpeakDialogue(d, "Baku: Line A"); Tick(1);
            d.gameObject.activeInHierarchy = false; // pooled/destroyed without AfterHidden
            NativeFieldDescriptions.Raise(Crouch, CrouchText, Always); Tick(30);
            Check(Played == 1, "an inactive window kept every later field description waiting");
        }
        public static void F16_InitializationSubscribesBeforeTheFirstFieldObservation()
        {
            Install(initialize: false);
            // The actual FieldMap.HonoAwake call is checked against the emitted
            // game DLL by --verify-descriptions; this harness has no FieldMap.
            NativeAudioDescriptions.Initialize(); NativeFieldDescriptions.Raise(Crouch, CrouchText, Always);
            Check(NativeFieldDescriptions.Subscribers > 0, "initialization did not install the field subscriber");
        }
        public static void F17_ClosingSceneDoesNotCancelNativeTextJoinedToCaptionFallback()
        {
            Install(fieldClips: false); var d=new Dialog(); NativeAudioDescriptions.SpeakDialogue(d,"Line A");
            NativeFieldDescriptions.Raise(Crouch,CrouchText,Always); d.NextPhrase(); NativeAudioDescriptions.SpeakDialogue(d,"Line B");
            int stopped=FakeSpeech.Silences; NativeFieldDescriptions.Close();
            Check(FakeSpeech.Silences==stopped,"scene close cancelled the utterance containing unread native dialogue");
        }
        public static void F18_MultipleHeldLinesUseOneOrderedUtterance()
        {
            Install();NativeFieldDescriptions.Raise(Crouch,CrouchText,Always);Tick(1);
            var d=new Dialog();NativeAudioDescriptions.SpeakDialogue(d,"Line A");d.NextPhrase();NativeAudioDescriptions.SpeakDialogue(d,"Line B");
            DescriptionWaveOutput.Finish();Tick(1);
            Check(FakeSpeech.Outputs.Count==1 && FakeSpeech.Last.Contains("Line A Line B"),"separate interrupting submissions cut off earlier lines");
        }
        public static void F19_ClosingSceneFlushesHeldNativeLine()
        {
            Install();NativeFieldDescriptions.Raise(Crouch,CrouchText,Always);Tick(1);
            NativeAudioDescriptions.SpeakDialogue(new Dialog(),"Line A");NativeFieldDescriptions.Close();
            Check(!Playing && Spoken("Line A")==1,"scene close lost held native text");
        }
        public static void F20_DisablingDescriptionsFlushesHeldNativeLine()
        {
            Install();NativeFieldDescriptions.Raise(Crouch,CrouchText,Always);Tick(1);
            NativeAudioDescriptions.SpeakDialogue(new Dialog(),"Line A");Configuration.Accessibility.AudioDescriptions=false;Tick(1);
            Check(!Playing && Spoken("Line A")==1,"disabling descriptions lost held native text");
        }
        public static void F21_HeldNativeLinePrecedesAnotherRecordedGesture()
        {
            Install();NativeFieldDescriptions.Raise(Crouch,CrouchText,Always);NativeFieldDescriptions.Raise(Nod,NodText,Always);Tick(1);
            var d=new Dialog();NativeAudioDescriptions.SpeakDialogue(d,"Line A");DescriptionWaveOutput.Finish();Tick(1);
            Check(Played==1 && Spoken("Line A")==1,"later gestures starved the native line");
        }
        public static void F22_InvalidRecordingDeclarationCannotBreakFieldStartupOrDialogue()
        {
            Install(initialize:false);NativeDescriptionCatalog.Initialize();
            NativeDescriptionCatalog.Audio.Add("bad",new DescriptionAudioSpec("bad","../bad.wav",new string('0',64),100,1));
            NativeAudioDescriptions.Initialize();NativeAudioDescriptions.SpeakDialogue(new Dialog(),"Line A");
            Check(Spoken("Line A")==1,"bad optional audio blocked native dialogue");
        }
        // ---------- films ----------
        private static MovieMaterial Film(bool clips = true, bool matches = true)
        {
            Install(movieClips: clips, movieMatches: matches); var movie = new MovieMaterial { movieKey = "FMV001" };
            NativeAudioDescriptions.MovieLoaded(movie, MoviePath);
            if (clips && matches) WaitForBank("MovieBank", MovieOwner(), "fmv001-001"); else WaitForBank("MovieBank", MovieOwner(), null);
            return movie;
        }
        public static void M01_CuesFollowUploadedFrameTime()
        {
            var m = Film(); Present(m, 1.0); Check(Played == 0, "early cue");
            Present(m, 2.05); Check(Played == 1 && Playing, "cue did not start at its uploaded-frame time");
            DescriptionWaveOutput.Finish(); Present(m, 3.0); Check(Played == 1, "cue replayed");
            Present(m, 6.02); Check(Played == 2, "second cue did not start");
        }
        public static void M02_PauseFollowsNativeAdvancingFlag()
        {
            var m = Film(); Present(m, 2.05); Present(m, 2.1, false);
            Check(DescriptionWaveOutput.Last.Paused, "narration kept playing while the film was paused");
            Present(m, 2.1, true); Check(!DescriptionWaveOutput.Last.Paused && Playing, "narration not resumed");
        }
        public static void M03_NoPresentedFramePausesWithoutAFreeRunningTimer()
        {
            var m = Film(); Present(m, 2.05); Tick(1, 0.5f);
            Check(DescriptionWaveOutput.Last.Paused, "narration continued without presented frames");
        }
        public static void M04_SkipAndStopEndNarration()
        {
            var m = Film(); Present(m, 2.05); NativeAudioDescriptions.MovieStopped(m);
            Check(!Playing, "narration survived the film stopping"); Present(m, 6.02); Check(Played == 1, "stopped film still narrated");
        }
        public static void M05_LateLoadSkipsCueThatCannotFitItsWindow()
        {
            var m = Film(); Present(m, 3.5); Check(Played == 0, "stale cue spilled into a later shot");
            Present(m, 6.01); Check(Played == 1, "next cue not started");
        }
        public static void M06_FocusLossSilencesFilmNarration()
        {
            var m = Film(); Present(m, 2.05); HonoInputManager.Active = false; Tick(1); Present(m, 2.1);
            Check(!Playing, "narration continued without focus");
        }
        public static void M07_MissingClipsUseNonInterruptingSpeechFallback()
        {
            var m = Film(clips: false); Present(m, 2.05);
            Check(Spoken("A ship sails") == 1 && NativeDescriptionSpeech.Outputs.Count==1 && FakeSpeech.Silences==0, "movie fallback missing or interrupting");
        }
        public static void M08_UnverifiedFilmSourceNeverNarratesAndWarnsOnce()
        {
            var m = Film(matches: false); Present(m, 2.05); Present(m, 2.2);
            Check(Played == 0 && Spoken("A ship sails") == 0, "unverified film narrated");
            Check(Spoken("could not be verified") == 1, "unverified film did not warn exactly once");
        }
        public static void M09_DialogueDuringFilmIsSpokenImmediately()
        {
            var m = Film(); Present(m, 2.05); NativeAudioDescriptions.SpeakDialogue(new Dialog(), "Kuja: Line"); Check(Spoken("Kuja: Line") == 1, "film dialogue delayed or lost");
        }
        public static void M10_FieldCueDuringFilmIsDropped()
        {
            var m = Film(); NativeFieldDescriptions.Raise(Crouch, CrouchText, Always); Present(m, 1.0); Tick(3);
            Check(Played == 0 && Spoken(CrouchText) == 0, "field description leaked into a film");
        }
        public static void M11_PauseMenuCannotBeUndoneByTheNextRenderCallback()
        {
            var m = Film(); Present(m, 2.05); UIManager.Instance.IsPause = true; Tick(1);
            Present(m, 2.1, true);
            Check(DescriptionWaveOutput.Last.Paused, "a rendered paused frame resumed narration behind the pause menu");
        }
        public static void M12_RepeatedUploadedFramePausesAndNewFrameResumes()
        {
            var m = Film(); Present(m, 2.05); Tick(1, .5f); Present(m, 2.05, true);
            Check(DescriptionWaveOutput.Last.Paused, "re-rendering a stalled uploaded frame resumed narration");
            Present(m, 2.12, true); Check(!DescriptionWaveOutput.Last.Paused, "a new uploaded frame did not resume narration");
        }
        public static void M13_FullyCoveredFilmCannotNarrateBehindFadeOverlay()
        {
            var m=Film();Assets.Scripts.Common.SceneDirector.IsScreenObscured=true;Present(m,2.05);
            Check(Played==0,"movie descriptions began behind a fully opaque fade");
        }
    }
}
