using System;
using System.Collections.Generic;
using System.IO;
using Assets.Sources.Graphics.Movie;
using Assets.Scripts.Common;
using Memoria.Prime;
using Memoria.ScreenReader;
using UnityEngine;
namespace Memoria.Accessibility.AudioDescriptions
{
    public static class NativeAudioDescriptions
    {
        private sealed class Film {internal MovieMaterial Source;internal NativeDescriptionFilm Definition;internal long Owner;internal double Position,Seen;internal bool Warned;}
        private sealed class FieldCue {internal string Id,Text;internal Func<bool> Current;internal bool SpeechOnly,SharedSpeechOnly;internal DescriptionAudioBank Bank;internal long Owner,Scene;}
        private sealed class Dialogue {internal Dialog Window;internal object Parser;internal string Line,Choice;}
        private static readonly DescriptionAudioBank MovieBank=new DescriptionAudioBank(),FieldBank=new DescriptionAudioBank(),SceneBank=new DescriptionAudioBank();
        private static readonly Queue<FieldCue> Pending=new Queue<FieldCue>();
        private static readonly List<Dialogue> DialogueQueue=new List<Dialogue>();
        private static readonly HashSet<Dialog> Windows=new HashSet<Dialog>();
        private static readonly NativeDescriptionPlayback Playback=new NativeDescriptionPlayback(Report);
        private static readonly MovieDescriptionSession MovieSession=new MovieDescriptionSession(Playback);
        private static readonly HashSet<string> Reported=new HashSet<string>();
        private const long FieldOwner=-1;
        private static Film _film;
        private static Dialogue _dialogue;
        private static FieldCue _fieldVoice;
        private static long _nextOwner,_sceneOwner,_sceneGeneration;
        private static NativeDescriptionFieldBank _sceneBank;
        private static double _voiceDeadline;
        private static string _directory;
        private static bool _initialized,_quitting;
        private static bool _fieldSegmentPlayed;
        public static bool NarrationPlaying {get{return _initialized && Playback.Playing;}}
        public static void Initialize()
        {
            try{InitializeCore();}catch(Exception exception){Report(exception.Message);WarnInitialization();}
        }
        private static void InitializeCore()
        {
            if(_initialized)return;_initialized=true;
            GameLoopManager.Update+=Tick;GameLoopManager.Quit+=Quit;
            NativeFieldDescriptions.Cue+=QueueFieldCue;NativeFieldDescriptions.SceneClosed+=ClearField;
            NativeDescriptionCatalog.Initialize();
            var architecture=Directory.GetParent(Application.dataPath);
            if(architecture==null || architecture.Parent==null){Report("Audio-description installation path unavailable");return;}
            _directory=Path.Combine(architecture.Parent.FullName,"accessibility/audio-descriptions");
            var audio=NativeDescriptionCatalog.GenericFieldAudio;FieldBank.Prepare(FieldOwner,audio,()=>true,file=>NativeDescriptionFiles.ReadClip(_directory,file,audio));
            if(NativeDescriptionCatalog.Failure!=null){Report(NativeDescriptionCatalog.Failure);WarnInitialization();}
        }
        // Called only at the native field-open boundary. Routing is fixed
        // metadata; all reads/hash/WAV validation remain on the bank worker.
        public static void FieldOpened(int field)
        {
            try {
                Initialize();if(_quitting)return;ClearField();NativeDescriptionFieldBank selected;
                NativeDescriptionCatalog.FieldBanks.TryGetValue(field,out selected);
                if(System.Object.ReferenceEquals(selected,_sceneBank))return;
                // Release the previous published bytes before starting a replacement.
                // Clear playback's reference as well as the bank's internal state.
                SceneBank.Reset();Playback.Bank=null;_sceneBank=selected;_sceneOwner=0;
                if(selected==null || _directory==null)return;
                _sceneOwner=++_nextOwner;var audio=selected.Audio;
                SceneBank.Prepare(_sceneOwner,audio,()=>true,file=>NativeDescriptionFiles.ReadClip(_directory,file,audio));
            }catch(Exception exception){Report(exception.Message);}
        }
        private static void WarnInitialization(){try{if(Enabled())ScreenReaderManager.Instance.TrySpeak("Audio descriptions could not load.",false);}catch(Exception){}}
        public static void MovieLoaded(MovieMaterial source,string path)
        {
            try {
                Initialize();MovieStopped(_film==null?null:_film.Source);ClearField();NativeDescriptionFilm definition;
                if(_quitting || !NativeDescriptionCatalog.Films.TryGetValue(source.movieKey,out definition))return;
                var film=new Film{Source=source,Definition=definition,Owner=++_nextOwner,Seen=Time.realtimeSinceStartup};_film=film;
                Playback.Bank=MovieBank;Playback.Owner=film.Owner;Playback.MovieFallback=true;Playback.Volume=Configuration.Accessibility.AudioDescriptionVolume;
                MovieBank.Prepare(film.Owner,definition.Audio,()=>NativeDescriptionFiles.VerifyFilm(path,definition),file=>NativeDescriptionFiles.ReadClip(_directory,file,definition.Audio));
            }catch(Exception exception){Fail(exception);}
        }
        public static void MoviePresented(MovieMaterial source)
        {
            try {
                var film=_film;if(film==null || film.Source!=source || _quitting)return;
                double position=source.DescriptionPresentedTime;if(position<0)return;
                if(Math.Abs(film.Position-position)>.00001)film.Seen=Time.realtimeSinceStartup;
                film.Position=position;
                bool enabled=Enabled(),foreground=HonoInputManager.ApplicationIsActivated();
                bool visible=source.GetFirstFrame && source.Transparency>.05f && !SceneDirector.IsScreenObscured && !SceneDirector.Instance.IsFading;
                bool advancing=source.DescriptionAdvancing && !UIManager.Instance.IsPause && Time.realtimeSinceStartup-film.Seen<=.25;
                if(enabled && foreground && visible && advancing)Playback.RecoverMovieSpeech(position);
                MovieSession.Observe(film.Owner,film.Definition.Timeline,position,visible,advancing,foreground,enabled);
                if(!film.Warned && MovieBank.Failure(film.Owner)!=null && foreground && enabled) {
                    film.Warned=true;Report(MovieBank.Failure(film.Owner));
                    if(!MovieBank.SourceVerified(film.Owner))ScreenReaderManager.Instance.TrySpeak("Audio descriptions are unavailable for this movie because its visual source could not be verified.",false);
                }
            }catch(Exception exception){Fail(exception);}
        }
        public static void MoviePaused(MovieMaterial source){try{if(_film!=null && _film.Source==source)MovieSession.Pause(_film.Owner);}catch(Exception exception){Fail(exception);}}
        public static void MovieStopped(MovieMaterial source)
        {
            try{if(_film==null || _film.Source!=source)return;long owner=_film.Owner;_film=null;MovieSession.Stop(owner);MovieBank.Reset();}catch(Exception exception){Fail(exception);}
        }
        private static bool Enabled(){return !_quitting && Configuration.Accessibility.AudioDescriptions && Configuration.Accessibility.AudioDescriptionVolume>0 && ScreenReaderManager.Instance.Enabled;}
        private static void QueueFieldCue(string id,string text,Func<bool> current)
        {
            try {
                Initialize();if(!Enabled() || _film!=null || current==null || !current())return;
                // The observed event carries fixed, reviewed wording. A missing
                // recording can still be delivered together with current dialog.
                if(Pending.Count>=64){Report("Field descriptions exceeded the current scene queue");return;}
                NativeDescriptionFieldBank membership;DescriptionAudioBank bank=FieldBank;long owner=FieldOwner;
                if(NativeDescriptionCatalog.FieldCueBanks.TryGetValue(id,out membership)) {
                    bank=SceneBank;owner=System.Object.ReferenceEquals(membership,_sceneBank)?_sceneOwner:0;
                }
                Pending.Enqueue(new FieldCue{Id=id,Text=text,Current=current,Bank=bank,Owner=owner,Scene=_sceneGeneration});
            }catch(Exception exception){Report(exception.Message);}
        }
        private static bool Current(FieldCue cue){try{return cue!=null && cue.Scene==_sceneGeneration && cue.Current!=null && cue.Current();}catch(Exception){return false;}}
        private static bool Current(Dialogue dialogue){return dialogue!=null && dialogue.Window!=null && dialogue.Window.gameObject.activeInHierarchy && System.Object.ReferenceEquals(dialogue.Window.CurrentParser,dialogue.Parser);}
        // Called at the native text boundary. The game keeps its own Confirm,
        // windows and timing; only our subsequent speech waits for owned audio.
        public static void SpeakDialogue(Dialog window,string text,bool choice=false)
        {
            try {
                Initialize();Windows.Add(window);
                // Keep every already presented line in order. Its native window
                // may close or advance while a recording finishes. Only choices
                // in the same question coalesce to the latest selected value.
                if(!choice || !Current(_dialogue) || _dialogue.Window!=window) {
                    _dialogue=new Dialogue{Window=window,Parser=window.CurrentParser};
                    DialogueQueue.Add(_dialogue);
                }
                if(choice)_dialogue.Choice=text;else _dialogue.Line=text;
                if(!Enabled() || _film!=null || !HonoInputManager.ApplicationIsActivated()) {
                    if(_fieldVoice!=null){Playback.Stop();_fieldVoice=null;}
                    SpeakQueuedDialogue();return;
                }
                DeliverField();
            }catch(Exception exception){Report(exception.Message);ScreenReaderManager.Instance.Speak(text,true);}
        }
        public static void DialogueHidden(Dialog window)
        {
            try{Windows.Remove(window);if(_dialogue!=null && _dialogue.Window==window)_dialogue=null;if(Windows.Count==0 && _film==null)DeliverField();}catch(Exception exception){Fail(exception);}
        }
        private static string QueuedDialogueText()
        {
            var text=new System.Text.StringBuilder();
            foreach(var dialogue in DialogueQueue){if(text.Length!=0)text.Append(" ");text.Append(DialogueText(dialogue));}
            return text.ToString();
        }
        private static bool SpeakQueuedDialogue()
        {
            if(DialogueQueue.Count==0)return true;
            // Submit the ordered batch once. Separate interrupting submissions
            // would cut off all but the last line on NVDA.
            if(!ScreenReaderManager.Instance.TrySpeak(QueuedDialogueText(),true))return false;
            DialogueQueue.Clear();_dialogue=null;_fieldSegmentPlayed=false;return true;
        }
        private static void DeliverField()
        {
            if(_film!=null || !Enabled() || !HonoInputManager.ApplicationIsActivated())return;
            if(_fieldVoice!=null && Playback.Playing)return;
            _fieldVoice=null;Playback.MovieFallback=false;Playback.Volume=Configuration.Accessibility.AudioDescriptionVolume;
            if(_fieldSegmentPlayed && DialogueQueue.Count!=0){SpeakQueuedDialogue();return;}
            while(Pending.Count!=0) {
                var cue=Pending.Peek();if(!Current(cue)){Pending.Dequeue();continue;}
                Playback.Bank=cue.Bank;Playback.Owner=cue.Owner;
                string failure=cue.Bank==null?null:cue.Bank.Failure(cue.Owner);if(failure!=null)Report(failure);
                DescriptionAudioSpec spec;string recorded;
                bool known=NativeDescriptionCatalog.Audio.TryGetValue(cue.Id,out spec) && NativeDescriptionCatalog.Text.TryGetValue(cue.Id,out recorded) && recorded==cue.Text;
                if(known && !cue.SpeechOnly && Playback.TryStart(new DescriptionCue(cue.Id,0,spec.Duration,spec.Duration,cue.Text))) {
                    Pending.Dequeue();_fieldVoice=cue;_fieldSegmentPlayed=true;_voiceDeadline=Time.realtimeSinceStartup+spec.Duration+2;return;
                }
                // Standalone fallback has its own cancellable SAPI voice. A
                // caption joined to native text below retains native ownership.
                if(!cue.SharedSpeechOnly && DialogueQueue.Count==0 && known && Playback.TryOwnedFallback(new DescriptionCue(cue.Id,0,spec.Duration,spec.Duration,cue.Text))) {
                    Pending.Dequeue();_fieldVoice=cue;cue.SpeechOnly=true;_fieldSegmentPlayed=true;
                    _voiceDeadline=Time.realtimeSinceStartup+Math.Max(spec.Duration,cue.Text.Length*.2)+2;return;
                }
                // A failed or absent recording must not swallow visual facts.
                // Combine it with the current line so interrupt:true cannot cut it.
                var combined=new System.Text.StringBuilder();
                foreach(var pending in Pending)if(Current(pending)){if(combined.Length!=0)combined.Append(" ");combined.Append(pending.Text);}
                if(DialogueQueue.Count!=0){combined.Append(" ");combined.Append(QueuedDialogueText());}
                bool containsDialogue=DialogueQueue.Count!=0;
                if(ScreenReaderManager.Instance.TrySpeak(combined.ToString(),containsDialogue)){
                    Pending.Clear();DialogueQueue.Clear();_dialogue=null;_fieldSegmentPlayed=false;
                }return;
            }
            if(!_quitting)SpeakQueuedDialogue();else DialogueQueue.Clear();
        }
        private static string DialogueText(Dialogue dialogue){return ((dialogue.Line??"")+" "+(dialogue.Choice??"")).Trim();}
        private static void Tick()
        {
            if(_quitting)return;
            try {
                NativeFieldDescriptions.ObservePresentationBoundary();
                Windows.RemoveWhere(window=>window==null || window.gameObject==null || !window.gameObject.activeInHierarchy);
                if(!Enabled() || !HonoInputManager.ApplicationIsActivated()){if(_film!=null)MovieSession.Observe(_film.Owner,_film.Definition.Timeline,_film.Position,false,false,false,false);ClearField();return;}
                if(_film!=null) {
                    SpeakQueuedDialogue();
                    if(Time.realtimeSinceStartup-_film.Seen>.25 || UIManager.Instance.IsPause)MovieSession.Pause(_film.Owner);return;
                }
                if(UIManager.Instance.IsPause){ClearField();return;}
                if(_fieldVoice!=null) {
                    if(!Current(_fieldVoice)){Playback.Stop();_fieldVoice=null;}
                    else if(Playback.NeedsSpeechRecovery){
                        Playback.Stop();_fieldVoice.SpeechOnly=_fieldVoice.SharedSpeechOnly=true;
                        RequeueFieldVoice();
                    }
                    else if(Playback.Playing && Time.realtimeSinceStartup>_voiceDeadline){
                        if(_fieldVoice.SpeechOnly){Playback.Stop();_fieldVoice.SharedSpeechOnly=true;}
                        else Playback.RecoverRecordedOutput("Description audio did not complete before its deadline");
                        _fieldVoice.SpeechOnly=true;RequeueFieldVoice();
                    }
                    else if(!Playback.Playing)_fieldVoice=null;
                }
                if(_fieldVoice==null && (DialogueQueue.Count!=0 || Windows.Count==0))DeliverField();
            }catch(Exception exception){Fail(exception);}
        }
        private static void RequeueFieldVoice(){var following=Pending.ToArray();Pending.Clear();Pending.Enqueue(_fieldVoice);foreach(var cue in following)Pending.Enqueue(cue);_fieldVoice=null;_fieldSegmentPlayed=false;}
        private static void ClearField()
        {
            // Screen-reader fallbacks share the native dialogue queue and cannot
            // be cancelled individually. Stop only the recording that we own.
            ++_sceneGeneration;Pending.Clear();Windows.Clear();_dialogue=null;
            if(_fieldVoice!=null){Playback.Stop();_fieldVoice=null;}
            _fieldSegmentPlayed=false;
            // These are observed native lines, independent of a gesture's
            // lifetime. Closing a scene must not silently delete unread text.
            if(!_quitting)SpeakQueuedDialogue();else DialogueQueue.Clear();
        }
        private static void Fail(Exception exception)
        {
            Report(exception.Message);try{if(_film!=null){_film=null;MovieSession.Reset();MovieBank.Reset();}ClearField();}catch(Exception cleanup){Report(cleanup.Message);SpeakQueuedDialogue();}
        }
        private static void Report(string text){if(Reported.Add(text))try{Log.Message("[FF9Access] Audio descriptions: {0}",text);}catch(Exception){}}
        private static void Quit(){_quitting=true;MovieBank.Reset();FieldBank.Reset();SceneBank.Reset();_sceneBank=null;_sceneOwner=0;try{MovieSession.Reset();ClearField();Playback.Dispose();}catch(Exception exception){Report(exception.Message);}}
    }
}
