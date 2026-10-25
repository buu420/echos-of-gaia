using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
namespace FF9Access.Tests
{
    // Emitted-assembly gate for the field description observer (root can wire it as
    // "--verify-field-descriptions <Assembly-CSharp.dll>"). It proves every bounded hook exists
    // in the compiled game and that observer code cannot speak, play audio, write actor state,
    // run scripts, inject input or load assets.
    internal static class FieldDescriptionNativeVerification
    {
        private static readonly Dictionary<ushort, OpCode> Opcodes = Build();
        public static void Verify(string library)
        {
            using (var stream = File.OpenRead(library)) using (var pe = new PEReader(stream))
            {
                var metadata = pe.GetMetadataReader();
                var required = new HashSet<string> {
                    "EventEngine|ExecAnim|NativeFieldDescriptions.OnAnimationStarted",
                    "EventEngine|DoEventCode|NativeFieldDescriptions.OnSequenceStarted",
                    "FieldMapActorController|PlayAnimationViaEventScript|NativeFieldDescriptions.OnClipSampled",
                    "HonoluluFieldMain|FF9FieldMapMain|NativeFieldDescriptions.AfterFieldEvents",
                    "FieldMap|HonoAwake|NativeFieldDescriptions.OnFieldOpened",
                    "FieldMap|HonoOnDestroy|NativeFieldDescriptions.OnFieldClosed",
                    "FieldMap|OnApplicationFocus|NativeFieldDescriptions.OnFocusChanged",
                    "NativeFieldDescriptions|ScreenClear|SceneDirector.get_IsScreenObscured",
                    "NativeFieldDescriptions|OnClipSampled|FieldDescriptionTracker.SamplePose",
                    "NativeFieldDescriptions|OnAnimationStarted|FieldDescriptionTracker.StartPose",
                    "NativeFieldDescriptions|OnAnimationStarted|FieldDescriptionCatalog.CanReviewPose",
                    "NativeFieldDescriptions|OnAnimationStarted|NativeFieldDescriptions.Site",
                    "FieldDescriptionTracker|StartPose|FieldContextPoseCatalog.FindContextPose",
                    "NativeFieldDescriptions|OnAnimationStarted|FieldWrite.FieldPoseStart.InFrame",
                    "NativeFieldDescriptions|OnAnimationStarted|FieldWrite.FieldPoseStart.OutFrame",
                    "NativeFieldDescriptions|OnAnimationStarted|FieldWrite.FieldPoseStart.FrameCount",
                    "NativeFieldDescriptions|OnAnimationStarted|FieldWrite.FieldPoseStart.Forward",
                    "NativeFieldDescriptions|OnAnimationStarted|FieldWrite.FieldPoseStart.NonJump",
                    "NativeFieldDescriptions|OnAnimationStarted|FieldContextPoseCatalog.FindContextPose",
                    "NativeFieldDescriptions|OnAnimationStarted|NativeFieldDescriptions.TryPosition",
                    "NativeFieldDescriptions|OnAnimationStarted|NativeFieldDescriptions.CompanionVisible",
                    "NativeFieldDescriptions|OnAnimationStarted|FieldWrite.FieldPoseStart.PositionKnown",
                    "NativeFieldDescriptions|OnAnimationStarted|FieldWrite.FieldPoseStart.X",
                    "NativeFieldDescriptions|OnAnimationStarted|FieldWrite.FieldPoseStart.Y",
                    "NativeFieldDescriptions|OnAnimationStarted|FieldWrite.FieldPoseStart.Z",
                    "NativeFieldDescriptions|OnAnimationStarted|FieldWrite.FieldPoseStart.CompanionActor",
                    "NativeFieldDescriptions|OnAnimationStarted|FieldWrite.FieldPoseStart.CompanionVisible",
                    "NativeFieldDescriptions|OnClipSampled|FieldDescriptionTracker.PendingProof",
                    "NativeFieldDescriptions|OnClipSampled|NativeFieldDescriptions.TryPosition",
                    "NativeFieldDescriptions|OnClipSampled|NativeFieldDescriptions.CompanionVisible",
                    "NativeFieldDescriptions|OnClipSampled|FieldWrite.FieldPoseSample.PositionKnown",
                    "NativeFieldDescriptions|OnClipSampled|FieldWrite.FieldPoseSample.X",
                    "NativeFieldDescriptions|OnClipSampled|FieldWrite.FieldPoseSample.Y",
                    "NativeFieldDescriptions|OnClipSampled|FieldWrite.FieldPoseSample.Z",
                    "NativeFieldDescriptions|OnClipSampled|FieldWrite.FieldPoseSample.CompanionActor",
                    "NativeFieldDescriptions|OnClipSampled|FieldWrite.FieldPoseSample.CompanionVisible",
                    "NativeFieldDescriptions|OnSequenceStarted|FieldDescriptionTracker.OnSequenceStarted",
                    "NativeFieldDescriptions|OnSequenceStarted|NativeFieldDescriptions.Site",
                    "NativeFieldDescriptions|TryPosition|FieldRead.FieldMapActorController.curPos",
                    "NativeFieldDescriptions|CompanionVisible|EventEngine.GetActiveObjList",
                    "NativeFieldDescriptions|CompanionVisible|Obj.get_sid",
                    "NativeFieldDescriptions|CompanionVisible|FieldRead.PosObj.model",
                    "NativeFieldDescriptions|CompanionVisible|NativeFieldDescriptions.Rendered",
                    "NativeFieldDescriptions|CompanionVisible|NativeFieldDescriptions.Project",
                    "NativeFieldDescriptions|CompanionVisible|FieldPoseProof.AcceptsCompanionAttachment",
                    "NativeFieldDescriptions|CompanionVisible|FieldRead.PosObj.attatchTargetUid",
                    "NativeFieldDescriptions|CompanionVisible|FieldRead.PosObj.attachTargetBoneIndex",
                    "NativeFieldDescriptions|CompanionVisible|Transform.get_parent",
                    "NativeFieldDescriptions|CompanionVisible|Object.get_name",
                    "NativeFieldDescriptions|CompanionVisible|Transform.IsChildOf",
                    "NativeFieldDescriptions|CompanionVisible|FieldRead.FieldPoseProof.AttachmentBoneName",
                    "NativeFieldDescriptions|OnClipSampled|FieldDescriptionTracker.HasPresentedObjects",
                    "NativeFieldDescriptions|OnClipSampled|FieldDescriptionTracker.CanReviewPresentedObject",
                    "NativeFieldDescriptions|OnClipSampled|FieldDescriptionTracker.IsPresentedPending",
                    "NativeFieldDescriptions|OnClipSampled|FieldDescriptionTracker.SamplePresentedObject",
                    "NativeFieldDescriptions|OnClipSampled|FieldDescriptionTracker.NeedsPresentedObjectSample",
                    "NativeFieldDescriptions|OnClipSampled|NativeFieldDescriptions.EntryHash",
                    "NativeFieldDescriptions|OnClipSampled|Time.get_frameCount",
                    "NativeFieldDescriptions|OnClipSampled|FieldWrite.FieldPresentedObjectSample.PresentationFrame",
                    "NativeFieldDescriptions|OnClipSampled|FieldWrite.FieldPresentedObjectSample.Sid",
                    "NativeFieldDescriptions|OnClipSampled|FieldWrite.FieldPresentedObjectSample.EntrySha256",
                    "NativeFieldDescriptions|OnClipSampled|FieldWrite.FieldPresentedObjectSample.Model",
                    "NativeFieldDescriptions|OnClipSampled|FieldWrite.FieldPresentedObjectSample.Clip",
                    "NativeFieldDescriptions|OnClipSampled|FieldWrite.FieldPresentedObjectSample.FullRange",
                    "NativeFieldDescriptions|OnClipSampled|FieldWrite.FieldPresentedObjectSample.PositionKnown",
                    "NativeFieldDescriptions|EntryHash|FieldRead.Obj.ebData",
                    "NativeFieldDescriptions|Site|NativeFieldDescriptions.EntryHash",
                    "FieldDescriptionTracker|SamplePresentedObject|FieldPresentedObjectCatalog.Find",
                    "FieldDescriptionTracker|SamplePresentedObject|FieldDescriptionTracker.ObserveActor",
                    "FieldDescriptionTracker|NeedsPresentedObjectSample|FieldPresentedObjectCatalog.Find",
                    "FieldDescriptionTracker|NeedsPresentedObjectSample|FieldDescriptionTracker.ObserveActor",
                    "NativeFieldDescriptions|AfterFieldEvents|FieldDescriptionTracker.ObserveMotion",
                    "NativeFieldDescriptions|AfterFieldEvents|FieldDescriptionTracker.ObserveActor",
                    "NativeAudioDescriptions|Tick|NativeFieldDescriptions.ObservePresentationBoundary",
                    "NativeFieldDescriptions|ScreenClear|UIManager.get_IsPause",
                    "NativeFieldDescriptions|BlendedAway|FieldRead.FF9Char.attr",
                    "NativeFieldDescriptions|Rendered|NativeFieldDescriptions.BlendedAway",
                    "NativeFieldDescriptions|OnClipSampled|NativeFieldDescriptions.LogVisibility",
                    "NativeFieldDescriptions|AfterFieldEvents|NativeFieldDescriptions.LogVisibility",
                    // Observation-only fade state: written where the shader fade changes, read only by the accessor.
                    "SceneDirector|ClearFadeColor|FieldWrite.SceneDirector.observedFadeCleared",
                    "SceneDirector|ClearFadeColor|Shader.SetGlobalColor",
                    "SceneDirector|FadeEventSetColor|FieldWrite.SceneDirector.observedFadeCleared",
                    "SceneDirector|InitFade|FieldWrite.SceneDirector.observedFadeCleared",
                    "SceneDirector|ServiceFade|FieldWrite.SceneDirector.observedFadeCleared",
                    "SceneDirector|ToggleFadeAll|FieldWrite.SceneDirector.observedFadeDisabled",
                    "SceneDirector|_SetFadeMode|FieldWrite.SceneDirector.observedFadeDisabled",
                    "SceneDirector|get_IsScreenObscured|FieldRead.SceneDirector.observedFadeCleared",
                    "SceneDirector|get_IsScreenObscured|FieldRead.SceneDirector.observedFadeDisabled"
                };
                var events = new HashSet<string> { "Cue", "SceneClosed" };int sequenceHooks=0;
                foreach (var handle in metadata.TypeDefinitions)
                {
                    var type = metadata.GetTypeDefinition(handle); string owner = metadata.GetString(type.Name);
                    var parent = type.GetDeclaringType();
                    while (!parent.IsNil) { var enclosing = metadata.GetTypeDefinition(parent); owner = metadata.GetString(enclosing.Name); parent = enclosing.GetDeclaringType(); }
                    if (metadata.GetString(type.Name) == "NativeFieldDescriptions")
                        foreach (var e in type.GetEvents()) events.Remove(metadata.GetString(metadata.GetEventDefinition(e).Name));
                    bool observer = owner == "NativeFieldDescriptions" || owner.StartsWith("FieldDescription") || owner.StartsWith("FieldContextPose") || owner.StartsWith("FieldPresentedObject") || owner == "DiscOneFieldDescriptionCatalog" || owner == "DiscOnePresentedObjectCatalog" || owner == "FieldPoseDescription" || owner == "FieldPoseProof" || owner == "FieldMotionDescription";
                    foreach (var methodHandle in type.GetMethods())
                    {
                        var method = metadata.GetMethodDefinition(methodHandle); if (method.RelativeVirtualAddress == 0) continue;
                        string name = metadata.GetString(method.Name);bool sequenceConstructed=false,presentationPreflight=false;
                        foreach (string call in Calls(metadata, pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()))
                        {
                            if(owner=="NativeFieldDescriptions" && name=="OnClipSampled") {
                                if(call=="FieldDescriptionTracker.NeedsPresentedObjectSample")presentationPreflight=true;
                                if((call=="NativeFieldDescriptions.Rendered" || call=="NativeFieldDescriptions.Project" || call=="FieldPresentedObjectSample..ctor") && !presentationPreflight)
                                    throw new Exception("Presented object preflight must precede renderer/projection/sample work");
                            }
                            if(owner=="EventEngine" && name=="DoEventCode") {
                                if(call=="Seq..ctor")sequenceConstructed=true;
                                if(call=="NativeFieldDescriptions.OnSequenceStarted") {
                                    if(!sequenceConstructed)throw new Exception("Sequence observation precedes native Seq construction");
                                    sequenceHooks++;
                                }
                            }
                            required.Remove(owner + "|" + name + "|" + call);
                            if (call.StartsWith("FieldRead.SceneDirector.observedFade") && !(owner == "SceneDirector" && name == "get_IsScreenObscured"))
                                throw new Exception("Accessibility fade observation leaked into game logic: " + owner + "." + name);
                            if (owner == "SceneDirector" && name == "ClearFadeColor" && call.StartsWith("FieldRead.SceneDirector.abrColor"))
                                throw new Exception("ClearFadeColor behavior changed: it now touches abrColor");
                            if (observer && (call.StartsWith("ScreenReaderManager.") || call.StartsWith("SoundLib.") || call.StartsWith("AudioSource.") || call == "EventEngine.Request" ||
                                call.StartsWith("File.") || call.StartsWith("Directory.") || call.StartsWith("FileInfo.") || call.StartsWith("FileStream.") || call.StartsWith("StreamReader.") ||
                                call.StartsWith("Transform.set_") || call=="Transform.SetParent" ||
                                call.StartsWith("EventInput.") || call.StartsWith("Input.") || call.StartsWith("AssetManager.") || call.StartsWith("Resources.") || call == "EventEngine.SetUserControl" ||
                                call.StartsWith("FieldWrite.Actor.") || call.StartsWith("FieldWrite.PosObj.") || call.StartsWith("FieldWrite.Obj.") || call.StartsWith("FieldWrite.FieldMapActorController.") ||
                                call.StartsWith("Animation.Play") || call == "Animation.Sample" || call.StartsWith("ETb.") || call.StartsWith("DescriptionWaveOutput.")))
                                throw new Exception("Field description observer may change the game or output audio: " + owner + "." + name + " -> " + call);
                        }
                    }
                }
                if (required.Count != 0) throw new Exception("Missing field description hook(s): " + string.Join(", ", required));
                if(sequenceHooks!=1)throw new Exception("Expected exactly one read-only STARTSEQ observation hook");
                if (events.Count != 0) throw new Exception("Missing NativeFieldDescriptions event(s): " + string.Join(", ", events));
            }
        }
        private static IEnumerable<string> Calls(MetadataReader metadata, byte[] il)
        {
            for (int offset = 0; offset < il.Length;)
            {
                ushort code = il[offset++]; if (code == 0xfe) code = (ushort)(0xfe00 | il[offset++]);
                var opcode = Opcodes[code]; int size;
                switch (opcode.OperandType)
                {
                    case OperandType.InlineNone: size = 0; break;
                    case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: size = 1; break;
                    case OperandType.InlineVar: size = 2; break;
                    case OperandType.InlineI8: case OperandType.InlineR: size = 8; break;
                    case OperandType.InlineSwitch: size = 4 + 4 * BitConverter.ToInt32(il, offset); break;
                    default: size = 4; break;
                }
                if (opcode == OpCodes.Stfld || opcode == OpCodes.Stsfld || opcode == OpCodes.Ldfld || opcode == OpCodes.Ldsfld)
                {
                    string access = opcode == OpCodes.Stfld || opcode == OpCodes.Stsfld ? "FieldWrite." : "FieldRead.";
                    EntityHandle field = MetadataTokens.EntityHandle(BitConverter.ToInt32(il, offset));
                    if (field.Kind == HandleKind.FieldDefinition) { var f = metadata.GetFieldDefinition((FieldDefinitionHandle)field); yield return access + metadata.GetString(metadata.GetTypeDefinition(f.GetDeclaringType()).Name) + "." + metadata.GetString(f.Name); }
                }
                if (opcode.OperandType == OperandType.InlineMethod)
                {
                    EntityHandle handle = MetadataTokens.EntityHandle(BitConverter.ToInt32(il, offset));
                    if (handle.Kind == HandleKind.MethodSpecification) handle = metadata.GetMethodSpecification((MethodSpecificationHandle)handle).Method;
                    if (handle.Kind == HandleKind.MemberReference)
                    {
                        var member = metadata.GetMemberReference((MemberReferenceHandle)handle);
                        if (member.Parent.Kind == HandleKind.TypeReference) yield return metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)member.Parent).Name) + "." + metadata.GetString(member.Name);
                    }
                    else if (handle.Kind == HandleKind.MethodDefinition)
                    {
                        var method = metadata.GetMethodDefinition((MethodDefinitionHandle)handle);
                        yield return metadata.GetString(metadata.GetTypeDefinition(method.GetDeclaringType()).Name) + "." + metadata.GetString(method.Name);
                    }
                }
                offset += size;
            }
        }
        private static Dictionary<ushort, OpCode> Build()
        {
            var result = new Dictionary<ushort, OpCode>();
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)) if (field.FieldType == typeof(OpCode)) { var value = (OpCode)field.GetValue(null); result[unchecked((ushort)value.Value)] = value; }
            return result;
        }
    }
}
