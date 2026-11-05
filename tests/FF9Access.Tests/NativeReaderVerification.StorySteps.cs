using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
namespace FF9Access.Tests
{
    internal static partial class NativeReaderVerification
    {
        public static void VerifyStorySteps(string library)
        {
            var required=new HashSet<string>{
                "NativeStoryTargets.Initialize|NativeStorySteps.get_Transit",
                "NativeStoryTargets.Annotate|NativeStoryTargets.AnnotateSteps",
                "NativeStoryTargets.AnnotateSteps|NativeStorySteps.Candidates",
                "NativeStoryTargets.AnnotateSteps|StoryStepCatalog.HasEffect",
                "NativeStoryTargets.AnnotateSteps|NativeStoryTargets.AcceptsStep",
                "NativeStoryTargets.AcceptsStep|StoryTargetCatalog.AcceptsStep",
                "NativeStoryTargets.GenericPositionGoal|NativeStorySteps.CurrentPositions",
                "NativeStoryTargets.GenericPositionGoal|StoryStepCatalog.Goal",
                "NativeFieldTargets.ReadStoryRegion|NativeStoryTargets.HasCurrentStep",
                "NativeFieldTargets.ReadStoryRegion|EventEngine.NavigationTalkQuadAt",
                "NativeFieldTargets.ReadStoryRegion|EventEngine.NavigationTouchQuadAt",
                "NativeFieldTargets.ReadStoryRegion|FieldStoryRegionApproaches.Selects",
                "NativeFieldTargets.ReadStoryRegion|FieldStoryRegionApproaches.Proven",
                "NativeFieldTargets.ReadStoryRegion|NativeFieldTargets.StoryProofMemory",
                "NativeFieldTargets.StoryProofMemory|NativeFieldTargets.ActiveObjects",
                "NativeFieldTargets.StoryProofMemory|ScriptSnapshot.InstanceBytes",
                "NativeStorySteps.AtWait|NativeStorySteps.FindOwner",
                "NativeStorySteps.FindOwner|NativeStorySteps.FindUniqueOwner",
                "NativeStorySteps.FindOwner|EventEngine.GetActiveObjList",
                "NativeStorySteps.Candidates|StoryStepCatalog.Candidates",
                "NativeStorySteps.CurrentPositions|NativeStorySteps.AtWait",
                "NativeStoryTargets.RefreshTransit|NativeStorySteps.RefreshTransitState",
                "NativeStoryTargets.RefreshTransit|StoryTargetCatalog.InvalidateState",
                "NativeStorySteps.EvaluateTransit|NativeStorySteps.EvaluateGlobalGuard",
                "NativeStorySteps.EvaluateGlobalGuard|ExitScriptAnalyzer.TryExpression",
                "NativeStorySteps.Evaluate|ScriptSnapshot.InstanceBytes",
                "NativeStorySteps.Evaluate|ExitScriptAnalyzer.TryExpression",
                "NativeStorySteps.Current|StoryStepCatalog.Current",
                "NativeStorySteps.CurrentPositions|StoryStepCatalog.CurrentPositions",
                "NativeStorySteps.Initialize|StoryStepCatalog.ParseTransit",
                "NativeFieldTargets.Read|NativeFieldTargets.SpeakerLabel",
                "NativeFieldTargets.SpeakerLabel|NativeFieldTargets.FunctionSpeakerLabel",
                "NativeFieldTargets.RequestedSpeakerLabel|NativeFieldTargets.FunctionSpeakerLabel",
                "NativeFieldTargets.FunctionSpeakerLabel|FieldSpeakerRoles.Label",
                "NativeFieldTargets.FunctionSpeakerLabel|NativeFieldTargets.Decode",
                "NativeFieldTargets.KnownName|ObservedIdentityStore.TryGetLabel",
                "NativeFieldTargets.KnownName|EventEngine.GetObjUID",
                "NavigationCoordinator.Tick|ArrivalNotice.ApproachReached"
            };
            bool resource=false;
            using(var stream=File.OpenRead(library))using(var pe=new PEReader(stream)) {
                var metadata=pe.GetMetadataReader();
                foreach(var handle in metadata.ManifestResources)
                    if(metadata.GetString(metadata.GetManifestResource(handle).Name)=="Memoria.Accessibility.StorySteps.json")resource=true;
                foreach(var handle in metadata.TypeDefinitions) {
                    var type=metadata.GetTypeDefinition(handle);string owner=metadata.GetString(type.Name);
                    string enclosing=owner;var parent=type.GetDeclaringType();
                    while(!parent.IsNil){var outer=metadata.GetTypeDefinition(parent);enclosing=metadata.GetString(outer.Name);parent=outer.GetDeclaringType();}
                    foreach(var methodHandle in type.GetMethods()) {
                        var method=metadata.GetMethodDefinition(methodHandle);if(method.RelativeVirtualAddress==0)continue;
                        string name=metadata.GetString(method.Name);
                        foreach(string call in Calls(metadata,pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes())) {
                            required.Remove(owner+"."+name+"|"+call);
                            foreach(string source in new[]{"AnnotateSteps","ReadStoryRegion","CurrentPositions"})
                                if(name.StartsWith("<"+source+">",StringComparison.Ordinal))required.Remove(enclosing+"."+source+"|"+call);
                            if(owner=="NativeStorySteps" || owner=="FieldSpeakerRoles") {
                                if(call=="EventEngine.Request" || call.StartsWith("EBin.set") || call=="EBin.getVarManually" || call.StartsWith("Input.") || call.StartsWith("EventInput.Receive") || call.StartsWith("Thread.Sleep") || call=="GC.Collect" || call.StartsWith("FieldWrite.Actor.") || call.StartsWith("FieldWrite.PosObj."))
                                    throw new Exception("Story step/name reader changes native gameplay or executes native expressions: "+call);
                            }
                        }
                    }
                }
            }
            if(!resource || required.Count!=0)throw new Exception("Missing global native Story/action integration: resource="+resource+"; "+String.Join(", ",required));
            Console.WriteLine("PASS emitted Story steps: embedded typed transit, current detached guards, local position/action binding, own-handler labels and native-readiness arrival separation");
        }
    }
}
