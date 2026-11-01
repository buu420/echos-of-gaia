using System;
using System.Collections.Generic;
using Memoria.Assets;
using Memoria.Prime;
using Memoria.ScreenReader;
namespace Memoria.Accessibility.Navigation
{
    // Reads only values used by a completed, currently visible native text parser.
    public static class NativeNumericDialogs
    {
        private sealed class Binding {internal Dialog Window;internal TextParser Parser;internal int Field,TextId;internal long Generation;internal readonly NumericInputReader Reader=new NumericInputReader();}
        private static readonly Dictionary<Dialog,Binding> Bindings=new Dictionary<Dialog,Binding>();
        private static readonly List<Dialog> Stale=new List<Dialog>();private static string _error;
        private sealed class Pending {internal Binding Binding;internal NumericInputAnnouncement Announcement;}
        private static readonly List<Pending> Updates=new List<Pending>();
        public static void Reset(){Bindings.Clear();Stale.Clear();Updates.Clear();_error=null;}
        public static void Forget(Dialog window){if(window!=null)Bindings.Remove(window);}
        public static void Opened(Dialog window)
        {
            try {
                int field=FF9StateSystem.Common.FF9.fldMapNo;
                if(window==null || window.IsOverlayDialog || window.CurrentParser==null || !NumericInputReader.Supports(field,window.TextId,window.CurrentParser.InitialText))return;
                Binding previous;if(Bindings.TryGetValue(window,out previous) && previous.Parser==window.CurrentParser && previous.Field==field && previous.TextId==window.TextId && previous.Generation==NavigationLifecycleBridge.Generation)return;
                Bindings[window]=new Binding{Window=window,Parser=window.CurrentParser,Field=field,TextId=window.TextId,Generation=NavigationLifecycleBridge.Generation};
            }catch(Exception exception){Report(exception);}
        }
        public static void Tick(bool canRead)
        {
            if(!canRead || Bindings.Count==0 || !ScreenReaderManager.Instance.Enabled || Memoria.Accessibility.AudioDescriptions.NativeAudioDescriptions.NarrationPlaying)return;
            Stale.Clear();Updates.Clear();
            foreach(var pair in Bindings) {
                try {
                    var b=pair.Value;var window=b.Window;
                    if(window==null || !window.gameObject.activeInHierarchy || window.CurrentParser!=b.Parser || window.TextId!=b.TextId || b.Field!=FF9StateSystem.Common.FF9.fldMapNo || b.Generation!=NavigationLifecycleBridge.Generation){Stale.Add(pair.Key);continue;}
                    var parser=b.Parser;
                    if(window.IsOverlayDialog || window.CurrentState!=Dialog.State.CompleteAnimation || parser.Step<TextParser.ParseStep.Render || parser.LabelContainer==null || !parser.LabelContainer.isVisible)continue;
                    // Automatic price updates wait for speech to finish and read
                    // the latest parser snapshot; never queue intermediate prices.
                    if(b.TextId==201 && ScreenReaderManager.Instance.IsSpeaking())continue;
                    var announcement=b.Reader.Observe(new NumericInputObservation{Field=b.Field,TextId=b.TextId,WindowIdentity=window.GetInstanceID(),Generation=b.Generation,OutputGeneration=ScreenReaderManager.Instance.SpeechGeneration,
                        Visible=true,RenderReady=true,Foreground=true,Template=parser.InitialText,RenderedText=parser.ParsedText,Values=parser.VariableMessageValues,SelectedVariable=window.OverlayMessageNumber});
                    if(announcement!=null)Updates.Add(new Pending{Binding=b,Announcement=announcement});
                }catch(Exception exception){Report(exception);}
            }
            foreach(var window in Stale)Bindings.Remove(window);Stale.Clear();
            if(Updates.Count!=0) {
                bool interrupt=true;var text=new System.Text.StringBuilder();foreach(var update in Updates){if(text.Length!=0)text.Append(' ');text.Append(update.Announcement.Text);if(update.Announcement.Initial || update.Binding.TextId==201)interrupt=false;}
                // The opening controls are an ordinary screen-reader utterance,
                // not owned narration. Queue initial/automatic updates even if a
                // backend cannot report its current speech-busy state.
                bool accepted=ScreenReaderManager.Instance.TrySpeak(text.ToString(),interrupt);
                foreach(var update in Updates)update.Binding.Reader.Acknowledge(update.Announcement,accepted);
                Updates.Clear();
            }
        }
        private static void Report(Exception exception){if(_error==exception.Message)return;_error=exception.Message;Log.Message("[FF9Access] Displayed numeric input unavailable: {0}",exception);}
    }
}
