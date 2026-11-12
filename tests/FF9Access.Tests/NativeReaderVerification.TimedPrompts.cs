using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
namespace FF9Access.Tests
{
    internal static partial class NativeReaderVerification
    {
        public static void VerifyTimedPrompts(string library)
        {
            var required=new HashSet<string>{
                "Dialog.GetButtonKeyName|NativePromptInputNames.Read",
                "Dialog.GetCleanDialogText|PromptButtonNames.FormatDialogButtons",
                "Dialog.GetButtonKeyName|FieldRead.DialogImage.checkFromConfig",
                "Dialog.GetButtonKeyName|FieldRead.DialogImage.tag",
                "HonoInputManager.ScanSources|HonoInputManager.ObserveAccessibilityInputSource",
                "HonoInputManager.ObserveAccessibilityInputSource|FieldRead.HonoInputManager.inputDownSources",
                "HonoInputManager.ObserveAccessibilityInputSource|NativePromptInputSource.Observe",
                "NativePromptInputSource.Observe|PromptInputDeviceTracker.Observe",
                "NativePromptInputSource.Observe|HonoInputManager.ApplicationIsActivated",
                "NativePromptInputSource.Observe|Input.GetButtonDown",
                "NativePromptInputSource.Observe|Input.GetKeyDown",
                "NativePromptInputSource.Observe|XInputManager.get_CurrentState",
                "NativePromptInputSource.Observe|XInputManager.get_PreviousState",
                "HonoInputManager.get_AccessibilityInputDevice|PromptInputDeviceTracker.get_Device",
                "NativePromptInputNames.Read|HonoInputManager.get_AccessibilityInputDevice",
                "NativePromptInputNames.Read|HonoInputManager.get_JoystickKeysPrimary",
                "NativePromptInputNames.Read|HonoInputManager.get_DefaultJoystickInputKeys",
                "NativePromptInputNames.Read|HonoInputManager.get_InputKeysPrimary",
                "NativePromptInputNames.Read|HonoInputManager.PhysicalButtonToLogicalControl",
                "NativePromptInputNames.Read|PromptButtonNames.Controller",
                "NativePromptInputNames.Read|PromptButtonNames.Keyboard",
                "NativeTimedInputContext.get_Active|TimedPromptPhase.IsChanbara",
                "NativeTimedInputContext.get_Active|EBin.getVarManually",
                "NativeAudioDescriptions.SpeakDialogue|NativeAudioDescriptions.ObserveTimedInput",
                "NativeAudioDescriptions.SpeakDialogue|NativeAudioDescriptions.SpeakTimedPrompt",
                "NativeAudioDescriptions.Tick|NativeAudioDescriptions.ObserveTimedInput",
                "NativeAudioDescriptions.QueueFieldCue|NativeTimedInputContext.get_Active",
                "NativeAudioDescriptions.ObserveTimedInput|NativeDescriptionPlayback.Stop",
                "NativeAudioDescriptions.SpeakTimedPrompt|NativeAudioDescriptions.Current",
                "NativeAudioDescriptions.SpeakTimedPrompt|Dialog.get_IsActive",
                "NativeAudioDescriptions.SpeakTimedPrompt|ScreenReaderManager.TrySpeakWithTextStatus",
                "UIKeyTrigger.Start|ScreenReaderManager.set_OutputAllowed",
                "NativeFieldDescriptions.ObservePresentationBoundary|NativeTimedInputContext.get_Active",
                "NativeFieldDescriptions.OnAnimationStarted|NativeTimedInputContext.get_Active",
                "NativeFieldDescriptions.OnClipSampled|NativeTimedInputContext.get_Active",
                "NativeFieldDescriptions.AfterFieldEvents|NativeTimedInputContext.get_Active"
            };
            using(var stream=File.OpenRead(library))using(var pe=new PEReader(stream)) {
                var metadata=pe.GetMetadataReader();
                foreach(var handle in metadata.TypeDefinitions) {
                    var type=metadata.GetTypeDefinition(handle);string owner=metadata.GetString(type.Name);
                    string enclosing=owner;var parent=type.GetDeclaringType();
                    while(!parent.IsNil){var outer=metadata.GetTypeDefinition(parent);enclosing=metadata.GetString(outer.Name);parent=outer.GetDeclaringType();}
                    foreach(var methodHandle in type.GetMethods()) {
                        var method=metadata.GetMethodDefinition(methodHandle);if(method.RelativeVirtualAddress==0)continue;
                        string name=metadata.GetString(method.Name);
                        foreach(string call in Calls(metadata,pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes())) {
                            required.Remove(owner+"."+name+"|"+call);
                            // Guard(Action) lowers this boundary's body into a
                            // compiler-generated method rather than an inline call.
                            if(enclosing=="NativeFieldDescriptions" && name.StartsWith("<ObservePresentationBoundary>",StringComparison.Ordinal))
                                required.Remove("NativeFieldDescriptions.ObservePresentationBoundary|"+call);
                            if(owner=="NativePromptInputNames" && call=="HonoInputManager.get_IsControllerConnect")throw new Exception("Connected controller still overrides active keyboard input");
                            if(owner=="NativeTimedInputContext" || owner=="NativePromptInputNames") {
                                if(call.StartsWith("FieldWrite.") || call.StartsWith("Input.Get") || call.StartsWith("EventInput.Receive") || call.StartsWith("Thread.Sleep") || call=="EventEngine.Request" || call=="EventEngine.SetUserControl" || call.StartsWith("EBin.set"))
                                    throw new Exception("Input prompt reader changes/consumes native gameplay state: "+call);
                            }
                        }
                    }
                }
            }
            if(required.Count!=0)throw new Exception("Missing timed-prompt/native input wiring: "+String.Join(", ",required));
            Console.WriteLine("PASS emitted timed input: current device/bindings, first native phase, prompt priority, owned-audio cancellation and no gameplay writes");
        }
    }
}
