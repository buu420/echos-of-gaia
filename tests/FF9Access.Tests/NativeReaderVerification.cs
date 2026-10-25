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
    // Inspect emitted calls without loading Unity or running a game singleton.
    internal static class NativeReaderVerification
    {
        private static readonly Dictionary<ushort, OpCode> Opcodes = BuildOpcodes();
        public static void VerifyDescriptions(string library)
        {
            using(var stream=File.OpenRead(library))using(var pe=new PEReader(stream)) {
                var metadata=pe.GetMetadataReader();var required=new HashSet<string>{
                    "MBG.HonoAwake|NativeMovieDescriptionProbe.Attach",
                    "MBG.SetMovieCamera|NativeMovieDescriptionProbe.SetCamera",
                    "MovieMaterial.Open|NativeAudioDescriptions.MovieLoaded",
                    "MovieMaterial.Pause|NativeAudioDescriptions.MoviePaused",
                    "MovieMaterial.Stop|NativeAudioDescriptions.MovieStopped",
                    "MovieMaterial.Destroy|NativeAudioDescriptions.MovieStopped",
                    "MovieMaterial.Update|NativeAudioDescriptions.MovieStopped",
                    "NativeMovieDescriptionProbe.OnWillRenderObject|NativeAudioDescriptions.MoviePresented",
                    "NativeAudioDescriptions.MoviePresented|MovieMaterial.get_DescriptionPresentedTime",
                      "FieldMap.HonoAwake|NativeAudioDescriptions.Initialize",
                    "Dialog.AnnounceDialogForScreenReader|NativeAudioDescriptions.SpeakDialogue",
                    "Dialog.AnnounceChoicesForScreenReader|NativeAudioDescriptions.SpeakDialogue",
                    "NavigationRuntime.Tick|NativeAudioDescriptions.get_NarrationPlaying",
                    "NavigationRuntime.Tick|ScreenReaderManager.TrySpeak",
                    "NavigationRuntime.Tick|NavigationAnnouncements.Acknowledge",
                    "NativeAudioDescriptions.get_NarrationPlaying|NativeDescriptionPlayback.get_Playing"
                };bool catalog=false;
                foreach(var resource in metadata.ManifestResources)if(metadata.GetString(metadata.GetManifestResource(resource).Name)=="Memoria.Accessibility.AudioDescriptions.json")catalog=true;
                foreach(var handle in metadata.TypeDefinitions) {
                    var type=metadata.GetTypeDefinition(handle);string name=metadata.GetString(type.Name),owner=name;
                    var parent=type.GetDeclaringType();while(!parent.IsNil){var enclosing=metadata.GetTypeDefinition(parent);owner=metadata.GetString(enclosing.Name);parent=enclosing.GetDeclaringType();}
                    foreach(var methodHandle in type.GetMethods()) {
                        var method=metadata.GetMethodDefinition(methodHandle);if(method.RelativeVirtualAddress==0)continue;string methodName=metadata.GetString(method.Name);
                        foreach(string call in Calls(metadata,pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes())) {
                            required.Remove(name+"."+methodName+"|"+call);
                            if(owner=="NativeAudioDescriptions" || owner=="NativeMovieDescriptionProbe" || owner=="MovieDescriptionSession" || owner=="DescriptionAudioBank") {
                                if(call=="EventEngine.Request" || call.StartsWith("EventInput.") || call.StartsWith("Input.") || call.StartsWith("ETb.") || call=="GC.Collect" || call.StartsWith("FieldWrite.Actor.") || call.StartsWith("FieldWrite.PosObj.") || call.StartsWith("SoundLib.Stop"))throw new Exception("Description code modifies the game or its original audio: "+call);
                            }
                            if(name=="NativeMovieDescriptionProbe" && methodName=="OnWillRenderObject" && (call.StartsWith("File.") || call.StartsWith("AssetManager.") || call.StartsWith("Thread.") || call.StartsWith("ThreadPool.")))throw new Exception("Blocking description work in render observation: "+call);
                        }
                    }
                }
                if(!catalog || required.Count!=0)throw new Exception("Incomplete native audio-description integration: "+String.Join(", ",required)+"; catalog="+catalog);
                Console.WriteLine("PASS emitted descriptions: actual film rendering/time, owned pause/stop/destruction, dialogue bridge and embedded catalog; no game input/state or original-audio commands in "+library);
            }
        }
        public static void VerifyStory(string library)
        {
            using(var stream=File.OpenRead(library))using(var pe=new PEReader(stream)) {
                var metadata=pe.GetMetadataReader();var required=new HashSet<string>{
                    "AccessibleNavigationManager.Initialize|NativeStoryTargets.Initialize",
                    "NativeFieldTargets.Annotate|NativeStoryTargets.Annotate",
                    "NavigationRuntime.Tick|NativeStoryTargets.Append",
                    "NativeFieldTargets.Read|ScriptSnapshot.InstanceBytes",
                    "OwnedWorldPlanner.TryStart|FieldRead.NavigationTarget.PhysicalId",
                    "OwnedWorldPlanner.Run|FieldRead.NavigationTarget.PhysicalId",
                    "NativeWorldMovement.BeforeMovement|FieldRead.NavigationTarget.PhysicalId",
                    "WorldPlanResult.IsCurrent|NavigationTarget.get_PhysicalIdentity",
                    "NativeInteractionProbe.Read|NativeInteractionProbe.NpcConsumesConfirm",
                    "NativeInteractionProbe.NpcConsumesConfirm|EventEngine.GetIP",
                    "NativeInteractionProbe.Read|NativeFieldTargets.IsReviewedListener",
                    "NativeFieldTargets.Read|NativeFieldTargets.ReadOpeningCandle",
                    "NativeInteractionProbe.Read|NativeFieldTargets.TryOpeningCandleReadiness",
                    "NativeFieldTargets.ReadOpeningCandle|OpeningCandleApproachCache.Read",
                    "NativeFieldTargets.OpeningCandleState|OpeningCandleEvidence.Available",
                    "NativeFieldTargets.OpeningCandleState|OpeningCandleEvidence.MatchesRegion",
                    "NativeFieldTargets.TryOpeningCandleReadiness|EventEngine.NavigationContainsQuad",
                    "EventCollision.NavigationCanTalkQuad|EventCollision.CheckQuadTalk",
                    "NavigationAnnouncements.Guidance|NavigationTarget.get_PhysicalIdentity"
                };int quadPriority=0;bool path=false,hash=false;
                foreach(var handle in metadata.TypeDefinitions) {
                    var type=metadata.GetTypeDefinition(handle);string name=metadata.GetString(type.Name),owner=name;
                    var parent=type.GetDeclaringType();while(!parent.IsNil){var enclosing=metadata.GetTypeDefinition(parent);owner=metadata.GetString(enclosing.Name);parent=enclosing.GetDeclaringType();}
                    foreach(var methodHandle in type.GetMethods()) {
                        var method=metadata.GetMethodDefinition(methodHandle);if(method.RelativeVirtualAddress==0)continue;string methodName=metadata.GetString(method.Name);
                        foreach(string call in Calls(metadata,pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes())) {
                            required.Remove(name+"."+methodName+"|"+call);
                            if(name=="NativeInteractionProbe" && methodName=="Read" && call=="NativeInteractionProbe.NpcConsumesConfirm")quadPriority++;
                            if(owner=="NativeFieldTargets" && call=="ScriptStoryAnalyzer.Read")path=true;
                            if(owner=="NativeFieldTargets" && call=="NativeStoryTargets.Matches")hash=true;
                            if(owner=="NativeStoryTargets" || owner=="ScriptStoryAnalyzer" || owner=="NativeInteractionProbe") {
                                if(call=="EventEngine.Request" || call.StartsWith("ETb.") || call.StartsWith("Input.") || call.StartsWith("AssetManager.") || call.StartsWith("Thread.") || call=="GC.Collect" || call.StartsWith("FieldWrite.Actor.") || call.StartsWith("FieldWrite.PosObj."))throw new Exception("Story/readiness executes native actions or adds blocking work: "+call);
                            }
                        }
                    }
                }
                bool resource=false;foreach(var handle in metadata.ManifestResources)if(metadata.GetString(metadata.GetManifestResource(handle).Name)=="Memoria.Accessibility.StoryNavigation.json")resource=true;
                if(!resource || !path || !hash || quadPriority<2 || required.Count!=0)throw new Exception("Incomplete native Story integration: "+String.Join(", ",required)+"; resource="+resource+"; path="+path+"; hash="+hash+"; quad guards="+quadPriority);
                Console.WriteLine("PASS emitted Story: warmed embedded metadata, exact-hash current path/listener bridge, native instance offsets, physical world identity and both native request-priority guards; no native requests, input writes or blocking reads in "+library);
            }
        }
        public static void VerifyForestRoutes(string library)
        {
            using(var stream=File.OpenRead(library))using(var pe=new PEReader(stream)) {
                var metadata=pe.GetMetadataReader();var required=new HashSet<string>{
                    "NativeFieldTargets.IsForestInteraction|ForestNavigationEvidence.MeneRegion",
                    "NativeFieldTargets.Read|ForestNavigationEvidence.NoninteractiveObject",
                    "NativeInteractionProbe.Read|EventEngine.TreadQuad",
                    "NativeInteractionProbe.Read|EventCollision.NavigationCanTalkQuad",
                    "NativeInteractionProbe.Read|EIcon.get_NavigationPresentedUid",
                    "FieldRoutePlanner.Plan|FieldTargetApproaches.Prepare",
                    "FieldTargetApproaches.Prepare|FieldRegionApproaches.Interaction",
                    "FieldTargetApproaches.Prepare|FieldRegionApproaches.Viewing",
                    "FieldTargetApproaches.Prepare|FieldActorApproaches.OnSurfaces",
                    "NavigationRuntime.ReadMovement|GuidanceRouteValidation.FieldApproachChanged",
                    "NavigationCoordinator.Tick|FieldRead.RoutePlan.ViewingPoint",
                    "FieldTargetCatalog.Copy|FieldRead.NavigationTarget.InteractionRegion"
                };
                foreach(var handle in metadata.TypeDefinitions) {
                    var type=metadata.GetTypeDefinition(handle);string name=metadata.GetString(type.Name);
                    foreach(var methodHandle in type.GetMethods()) {
                        var method=metadata.GetMethodDefinition(methodHandle);if(method.RelativeVirtualAddress==0)continue;string methodName=metadata.GetString(method.Name);
                        foreach(string call in Calls(metadata,pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes())) {
                            required.Remove(name+"."+methodName+"|"+call);
                            if(name=="NativeInteractionProbe" || name=="FieldRegionApproaches" || name=="ForestNavigationEvidence" || name=="EventCollision" && methodName=="NavigationCanTalkQuad") {
                                if(call=="EventEngine.Request" || call.StartsWith("ETb.") || call.StartsWith("Input.") || call.StartsWith("TimerUI.") || call.StartsWith("AssetManager.") || call.StartsWith("Thread.") || call=="GC.Collect" || call.StartsWith("FieldWrite.Actor.") || call.StartsWith("FieldWrite.PosObj."))throw new Exception("Forest approach/readiness changes gameplay or adds inappropriate work: "+call);
                            }
                        }
                    }
                }
                if(required.Count!=0)throw new Exception("Missing Forest route integration: "+String.Join(", ",required));
                Console.WriteLine("PASS emitted Forest routes: script-scoped ground region, selected native quad and shown bubble, observed region copies and honest viewing arrival; no game requests or input writes in "+library);
            }
        }
        public static void VerifyHotColdText(string evidence)
        {
            int languages=0,cries=0;
            using(var document=System.Text.Json.JsonDocument.Parse(File.ReadAllText(evidence)))foreach(var locale in document.RootElement.EnumerateObject()) {
                int found=locale.Value.GetProperty("found").GetInt32();int result=locale.Name=="US" || locale.Name=="UK"?294:locale.Name=="JP"?299:300;
                var feedback=locale.Value.GetProperty("feedback");
                for(int id=found;id<=result+26;id++) {
                    string raw=feedback.GetProperty(id.ToString()).GetString();
                    string clean=System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(raw,@"\[[^\]]*\]",""),@"\s+"," ").Trim();
                    Memoria.Accessibility.Navigation.HotColdCue cue;
                    var role=Memoria.Accessibility.Navigation.HotColdPresentationBindings.Classify(locale.Name,id,raw,clean,out cue);
                    var expected=id==found?Memoria.Accessibility.Navigation.HotColdDialogRole.FoundCount:id==found+1?Memoria.Accessibility.Navigation.HotColdDialogRole.DepthLabel:id==found+2?Memoria.Accessibility.Navigation.HotColdDialogRole.DepthNumber:id>found+3 && id<result?Memoria.Accessibility.Navigation.HotColdDialogRole.Cry:Memoria.Accessibility.Navigation.HotColdDialogRole.Result;
                    if(role!=expected)throw new Exception("Installed text mismatch: "+locale.Name+" ID "+id+" expected "+expected+", got "+role);
                    if(role==Memoria.Accessibility.Navigation.HotColdDialogRole.Cry){if(cue==Memoria.Accessibility.Navigation.HotColdCue.Unknown)throw new Exception("Cry meaning unavailable");cries++;}
                }
                languages++;
            }
            if(languages!=7 || cries!=137)throw new Exception("Incomplete installed Hot and Cold language evidence");
            Console.WriteLine("PASS installed Hot and Cold text: "+languages+" language tables, "+cries+" cry variants, HUD numbers and shown result ranges");
        }
        public static void VerifyHotCold(string library)
        {
            using(var stream=File.OpenRead(library))using(var pe=new PEReader(stream)) {
                var metadata=pe.GetMetadataReader();
                var required=new HashSet<string>{
                    "Dialog.AnnounceDialogForScreenReader|NativeChocoboHotCold.TryHandleOpenedDialog",
                    "NativeNavigationContext.Read|NativeChocoboHotCold.ObserveContext",
                    "NavigationRuntime.Tick|NativeChocoboHotCold.Tick",
                    "NavigationRuntime.Tick|NativeChocoboHotCold.DrainResults",
                    "NativeChocoboHotCold.ObserveContext|ChocoboHotColdReader.get_HasPendingResults",
                    "NavigationRuntime.DetachScene|NativeChocoboHotCold.Reset",
                    "NativeChocoboHotCold.ReadFrame|TimerUI.get_Time",
                    "NativeChocoboHotCold.ReadFrame|TimerUI.get_Enable",
                    "NativeChocoboHotCold.ReadFrame|TimerUI.GetDisplay",
                    "NativeChocoboHotCold.ReadFrame|DisplayedNumber.Read",
                    "DisplayedNumber.Read|FieldRead.TextParser.VariableMessageValues",
                    "NativeChocoboHotCold.TryHandleOpenedDialog|ChocoboHotColdReader.ObserveCue",
                    "NativeChocoboHotCold.TryHandleOpenedDialog|ChocoboHotColdReader.ObserveResult",
                    "NativeChocoboHotCold.Tick|ScreenReaderManager.TrySpeak",
                    "NativeChocoboHotCold.Tick|ChocoboHotColdReader.Acknowledge"
                };
                foreach(var typeHandle in metadata.TypeDefinitions) {
                    var type=metadata.GetTypeDefinition(typeHandle);string name=metadata.GetString(type.Name);
                    foreach(var methodHandle in type.GetMethods()) {
                        var method=metadata.GetMethodDefinition(methodHandle);if(method.RelativeVirtualAddress==0)continue;
                        string methodName=metadata.GetString(method.Name);var body=pe.GetMethodBody(method.RelativeVirtualAddress);
                        var calls=new List<string>(Calls(metadata,body.GetILBytes()));
                        if(name=="NavigationRuntime" && methodName=="Tick" && calls.IndexOf("NativeChocoboHotCold.Tick")>=calls.IndexOf("AccessibleNavigationManager.ReadTargets"))throw new Exception("Minigame inspection does not bypass the field catalog");
                        if(name=="NativeChocoboHotCold" && (methodName=="ObserveContext" || methodName=="TryHandleOpenedDialog" || methodName=="Tick") && body.ExceptionRegions.Length==0)throw new Exception("Hot and Cold hook lacks an exception boundary: "+methodName);
                        foreach(string call in calls) {
                            required.Remove(name+"."+methodName+"|"+call);
                            if(name=="NativeChocoboHotCold" || name=="DisplayedNumber" || name=="ChocoboHotColdReader" || name=="HotColdPresentationBindings") {
                                if(call.StartsWith("ETb.") || call.StartsWith("EventEngine.") || call.StartsWith("EBin.") || call.StartsWith("FieldRead.ETb.") || call.StartsWith("EventInput.") || call.StartsWith("Input.") || call.StartsWith("AssetManager.") || call.StartsWith("Resources.") || call.StartsWith("Object.Find") || call.StartsWith("Thread.") || call.StartsWith("ThreadPool.") || call.StartsWith("NativeFieldMovement.") || call.StartsWith("NativeWorldMovement.") || call=="TextParser.Parse" || call=="GC.Collect" || call=="Dialog.get_HasChoices" || call.StartsWith("TimerUI.Set") || call=="TimerUI.set_Play")throw new Exception("Hot and Cold reader reads latent state, changes gameplay, or adds frame work: "+call);
                            }
                        }
                    }
                }
                if(required.Count!=0)throw new Exception("Missing Hot and Cold integration: "+String.Join(", ",required));
                Console.WriteLine("PASS emitted Hot and Cold: shown dialog/parser/timer reads, inspection before catalog work, contained hooks; no hidden variables, forced parsing, scene searches or game commands in "+library);
            }
        }
        public static void VerifyFieldExits(string library)
        {
            using(var stream=File.OpenRead(library))using(var pe=new PEReader(stream)) {
                var metadata=pe.GetMetadataReader();var required=new HashSet<string>{
                    "NativeFieldTargets.VisibleBoundary|FieldDoorEvidence.TryTriggerSurface",
                    "NativeFieldTargets.VisibleBoundary|FieldDoorEvidence.CurrentPlainWalkOut",
                    "NativeFieldTargets.Surface|FieldRegionApproaches.Interaction",
                    "NativeFieldTargets.VisibleBoundary|FieldExitEvidence.TrySurface",
                    "NativeFieldTargets.Read|NativeFieldActorInteractions.Annotate",
                    "NativeFieldTargets.Read|NativeFieldActorInteractions.IsBoundRegion",
                    "NativeInteractionProbe.Read|NativeFieldActorInteractions.IsBoundPair",
                    "NavigationRuntime.Tick|NativeFieldExitCues.Tick",
                    "NavigationRuntime.Tick|NativeFieldExitCues.UpdateGates",
                    "NavigationLifecycleBridge.DetachScene|NativeFieldExitCues.Suspend",
                    "NavigationLifecycleBridge.ReplaceSave|NativeFieldExitCues.Suspend",
                    "NativeFieldExitCues.Tick|FieldSpatialCueTracker.Update",
                    "NativeFieldExitCues.Tick|FieldSpatialAudioWorker.Submit",
                    "NativeFieldExitCues.Suspend|FieldSpatialAudioWorker.Submit",
                    "FieldSpatialAudioWorker.Run|SpatialAudioMixer.Render",
                    "FieldSpatialAudioWorker.Run|SpatialWaveOutput.TryWrite",
                    "SpatialAudioMixer.Render|ISpatialHrtf.Mix"
                };
                foreach(var typeHandle in metadata.TypeDefinitions) {
                    var type=metadata.GetTypeDefinition(typeHandle);string name=metadata.GetString(type.Name);
                    foreach(var methodHandle in type.GetMethods()) {
                        var method=metadata.GetMethodDefinition(methodHandle);if(method.RelativeVirtualAddress==0)continue;
                        string owner=name+"."+metadata.GetString(method.Name);
                        foreach(string call in Calls(metadata,pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes())) {
                            required.Remove(owner+"|"+call);
                            if(name=="NativeFieldExitCues" || name=="FieldExitProximityTracker" || name=="FieldSpatialCueTracker" || name=="SpatialAudioMixer" || name=="FieldTargetApproaches" || name=="FieldRegionApproaches" || name=="NativeFieldActorInteractions") {
                                if(call.StartsWith("SaXAudio.") || call.StartsWith("SoundLib.") && call!="SoundLib.get_SoundEffectIsMute" || call.StartsWith("AudioSource.") || call.StartsWith("Input.") || call.StartsWith("EventInput.") || call.StartsWith("ETb.") || call.EndsWith(".Request") || call=="GC.Collect" || call=="GC.GetTotalMemory" || call=="Thread.Sleep" || call.EndsWith(".PlaySound"))throw new Exception("Exit feature crosses native ownership: "+owner+" -> "+call);
                                if(owner=="NativeFieldExitCues.Tick" && (call.StartsWith("File.") || call.StartsWith("Directory.") || call.StartsWith("Thread.")))throw new Exception("Exit pulse performs file/thread work: "+call);
                                if(name=="NativeFieldActorInteractions" && (call=="EBin.getVarManually" || call=="EBin.getv" || call=="EIcon.PollCollisionIcon" || call.StartsWith("AssetManager.") || call.StartsWith("Thread.")))throw new Exception("Actor reader crosses native read boundary: "+call);
                            }
                        }
                    }
                }
                if(required.Count!=0)throw new Exception("Missing field-exit integration: "+String.Join(", ",required));
                Console.WriteLine("PASS emitted field exits: native trigger surfaces, cached owned audio and lifecycle gates; no game audio/input ownership in "+library);
            }
        }
        public static void VerifyWorldReachability(string library)
        {
            using(var stream=File.OpenRead(library))using(var pe=new PEReader(stream)) {
                var metadata=pe.GetMetadataReader();
                var required=new HashSet<string>{
                    "WorldMapAccessibilityManager.ReadTargets|WorldTargetCatalog.Filter",
                    "WorldMapAccessibilityManager.ReadTargets|WorldReachabilityCache.TryRead",
                    "WorldMapAccessibilityManager.ReadTargets|NativeWorldGeometry.Read",
                    "WorldReachabilityCache.Run|WorldReachabilityIndex.Build"
                };
                foreach(var typeHandle in metadata.TypeDefinitions) {
                    var type=metadata.GetTypeDefinition(typeHandle);string name=metadata.GetString(type.Name);
                    foreach(var methodHandle in type.GetMethods()) {
                        var method=metadata.GetMethodDefinition(methodHandle);if(method.RelativeVirtualAddress==0)continue;
                        string owner=name+"."+metadata.GetString(method.Name);
                        foreach(string call in Calls(metadata,pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes())) {
                            required.Remove(owner+"|"+call);
                            if(name=="WorldReachabilityIndex" || name=="WorldReachabilityCache") {
                                if(call.StartsWith("Native",StringComparison.Ordinal) || call.StartsWith("Input.",StringComparison.Ordinal) || call.StartsWith("GameObject.",StringComparison.Ordinal) || call.StartsWith("Object.Find",StringComparison.Ordinal) || call=="GC.Collect" || call=="GC.GetTotalMemory" || call=="WorldRoutePlanner.Plan")
                                    throw new Exception("Reachability worker accesses native state, collects the heap, or plans each destination: "+call);
                            }
                        }
                    }
                }
                if(required.Count!=0)throw new Exception("Missing world catalog reachability integration: "+String.Join(", ",required));
                Console.WriteLine("PASS emitted world reachability: native catalog filtered through detached cached components; no Unity/input/heap collection or route-per-target work in "+library);
            }
        }
        public static void VerifyWorldFootsteps(string library)
        {
            using (var stream = File.OpenRead(library)) using (var pe = new PEReader(stream))
            {
                var metadata = pe.GetMetadataReader();
                var required = new HashSet<string> {
                    "WMScriptDirector.HonoUpdate|WorldMapAccessibilityManager.UpdateWorldMovement",
                    "WorldMapAccessibilityManager.UpdateWorldMovement|NativeWorldFootsteps.Tick",
                    "NavigationRuntime.Tick|NativeWorldFootsteps.UpdateGates",
                    "NavigationLifecycleBridge.DetachScene|NativeWorldFootsteps.Suspend",
                    "NativeWorldFootsteps.Tick|WorldFootstepCadence.Observe",
                    "NativeWorldFootsteps.Tick|CachedFootstepPlayer.WarmOne",
                    "NativeWorldFootsteps.Tick|CachedFootstepPlayer.Play",
                    "NativeWorldFootsteps.UpdateGates|NativeWorldFootsteps.Suspend",
                    "NativeWorldFootsteps.Suspend|WorldFootstepCadence.Reset",
                    "NativeWorldFootsteps.Suspend|NativeWorldFootsteps.StopPlaying",
                    "NativeWorldFootsteps.StopPlaying|CachedFootstepPlayer.Stop",
                    "NativeWorldFootsteps.LoadSample|SaXAudio.BankLoadOggFile",
                    "NativeWorldFootsteps.PlaySample|SaXAudio.CreateVoice",
                    "NativeWorldFootsteps.PlaySample|SaXAudio.SetVolume",
                    "NativeWorldFootsteps.PlaySample|SaXAudio.SetLooping",
                    "NativeWorldFootsteps.PlaySample|SaXAudio.Start",
                    "NativeWorldFootsteps.StopSample|SaXAudio.VoiceExist",
                    "NativeWorldFootsteps.StopSample|SaXAudio.Stop"
                };
                foreach (var typeHandle in metadata.TypeDefinitions)
                {
                    var type = metadata.GetTypeDefinition(typeHandle); string name = metadata.GetString(type.Name);
                    foreach (var methodHandle in type.GetMethods())
                    {
                        var method = metadata.GetMethodDefinition(methodHandle); if (method.RelativeVirtualAddress == 0) continue;
                        string methodName = metadata.GetString(method.Name);
                        var calls = new List<string>(Calls(metadata, pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()));
                        if (name == "WMScriptDirector" && methodName == "HonoUpdate" &&
                            (calls.LastIndexOf("WMScriptDirector.HonoUpdate20FPS") < 0 ||
                            calls.IndexOf("WorldMapAccessibilityManager.UpdateWorldMovement") <= calls.LastIndexOf("WMScriptDirector.HonoUpdate20FPS")))
                            throw new Exception("Footsteps run before accepted world movement");
                        foreach (string call in calls)
                        {
                            required.Remove(name + "." + methodName + "|" + call);
                            if (name == "NativeWorldFootsteps" && (call == "SaXAudio.Init" || call == "SaXAudio.Release" || call == "SaXAudio.StopAll" ||
                                call == "SaXAudio.StopEngine" || call == "SaXAudio.PauseAll" || call == "Object.FindObjectOfType" || call == "GC.Collect"))
                                throw new Exception("Footsteps interfere with game audio or add unrelated work: " + call);
                            if (name == "CachedFootstepPlayer" && methodName == "Play" && (call.StartsWith("File.", StringComparison.Ordinal) || call.StartsWith("Thread.", StringComparison.Ordinal)))
                                throw new Exception("Footstep playback performs file or thread work");
                            if (((name == "NavigationRuntime" && methodName == "Tick") || (name == "WorldMapAccessibilityManager" && methodName == "Update")) && call == "NativeWorldFootsteps.Tick")
                                throw new Exception("Rendered frames are being sampled as world movement");
                            if (name == "NativeWorldFootsteps" && methodName == "UpdateGates" &&
                                (call == "WorldFootstepCadence.Observe" || call.StartsWith("CachedFootstepPlayer.", StringComparison.Ordinal)))
                                throw new Exception("Rendered gate checks perform footstep movement or audio work");
                        }
                        if (name == "NativeWorldFootsteps" && (methodName == "Tick" || methodName == "Suspend" || methodName == "UpdateGates") &&
                            pe.GetMethodBody(method.RelativeVirtualAddress).ExceptionRegions.Length == 0)
                            throw new Exception("Footstep failure lacks a native exception boundary: " + methodName);
                    }
                }
                if (required.Count != 0) throw new Exception("Missing world footstep integration: " + String.Join(", ", required));
                Console.WriteLine("PASS emitted world footsteps: accepted native movement, cached samples, owned voices, existing audio engine in " + library);
            }
        }
        public static void VerifyBattleBoundaries(string library)
        {
            using(var stream=File.OpenRead(library))using(var pe=new PEReader(stream)) {
                var metadata=pe.GetMetadataReader();
                var required=new HashSet<string>{
                    "NativeNavigationInput.EnsureUpdated|NavigationInputBroker.Suspend",
                    "NativeNavigationInput.EnsureUpdated|BattlePerformanceTrace.ObserveFrame",
                    "XInputManager.Refresh|NativeNavigationInput.ObserveControllerConnection",
                    "NativeNavigationInput.ObserveControllerConnection|NavigationInputBroker.ObserveControllerConnection",
                    "NativeNavigationInput.ObserveControllerConnection|NavigationLifecycleBridge.Release",
                    "SceneDirector.Replace|BattlePerformanceTrace.BeginEncounter",
                    "SceneDirector.ChangeScene|BattlePerformanceTrace.Stage",
                    "SceneDirector.ChangeSceneAsync|BattlePerformanceTrace.Stage",
                    "BattleSwirl.Awake|BattlePerformanceTrace.Stage",
                    "BattleSwirl.Update|BattlePerformanceTrace.Stage",
                    "HonoluluBattleMain.Start|BattlePerformanceTrace.FinishStartup",
                    "BattleHUD.OnItemSelect|UIScene.OnItemSelect"
                };
                foreach(var typeHandle in metadata.TypeDefinitions) {
                    var type=metadata.GetTypeDefinition(typeHandle);string name=metadata.GetString(type.Name);
                    foreach(var methodHandle in type.GetMethods()) {
                        var method=metadata.GetMethodDefinition(methodHandle);if(method.RelativeVirtualAddress==0)continue;
                        string methodName=metadata.GetString(method.Name);
                        // Iterator calls are emitted in the generated MoveNext method.
                        string owner=name.StartsWith("<ChangeSceneAsync>",StringComparison.Ordinal)?"SceneDirector.ChangeSceneAsync":name+"."+methodName;
                        foreach(string call in Calls(metadata,pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes())) {
                            required.Remove(owner+"|"+call);
                            if(name=="BattleHUD" && methodName=="AnnounceSelectedTarget" && call=="FieldRead.BattleHUD._matchBattleIdEnemyList")throw new Exception("Target narration still reads hidden enemy statistics");
                            if(name=="BattlePerformanceTrace" && (call=="GC.Collect" || call=="GC.GetTotalMemory"))throw new Exception("Battle diagnostic performs a heap collection or heap scan");
                        }
                    }
                }
                if(required.Count!=0)throw new Exception("Missing battle boundary: "+String.Join(", ",required));
                Console.WriteLine("PASS emitted battle boundaries: suspended navigation input, displayed target labels, bounded startup timings in "+library);
            }
        }
        public static void VerifyWorldPreparation(string library)
        {
            using(var stream=File.OpenRead(library))using(var pe=new PEReader(stream)) {
                var metadata=pe.GetMetadataReader();
                var required=new HashSet<string>{
                    "NavigationRuntime.Tick|NativeWorldGeometry.Warm",
                    "WMWorld.LoadBlocks|NativeWorldGeometry.BeginLoad",
                    "WMWorld.LoadBlocks|NativeWorldGeometry.CaptureLoadedBlock",
                    "ff9.w_frameMainRoutine|NativeWorldGeometry.CompleteLoad",
                    "NativeWorldGeometry.BeginLoad|WorldCaptureBoundary.TryCapture",
                    "NativeWorldGeometry.CaptureLoadedBlock|WorldCaptureBoundary.TryCapture",
                    "NativeWorldGeometry.CompleteLoad|WorldCaptureBoundary.TryCapture",
                    "NativeWorldGeometry.Detach|WorldCaptureBoundary.TryCapture",
                    "NativeWorldGeometry.FlushLoading|WorldGeometryPreparation.TryAppendOwned",
                    "NativeWorldGeometry.CaptureLoadedBlockCore|WorldGeometryContent.MatchesBlock",
                    "NavigationLifecycleBridge.DetachScene|NativeWorldGeometry.Detach",
                    "NativeWorldGeometry.AdvanceCapture|WorldCaptureBudget.Take",
                    "NativeWorldGeometry.AdvanceCapture|WorldGeometryPreparation.TryStartOwned",
                    "NativeWorldGeometry.Read|WorldGeometryPreparation.TryRead",
                    "NativeWorldGeometry.EnsureIdentity|WorldIdentity.Matches",
                    "WorldIdentity.Matches|BlockIdentity.Matches",
                    "BlockIdentity.Matches|MeshIdentity.Matches",
                    "MeshIdentity.Matches|FieldRead.WMMesh.Transform",
                    "MeshIdentity.Matches|FieldRead.WMMesh.Vertices",
                    "MeshIdentity.Matches|FieldRead.WMMesh.Triangles",
                    "MeshIdentity.Matches|FieldRead.WMMesh.Tangents",
                    "MeshIdentity.Matches|FieldRead.WMMesh.TriangleNormals",
                    "MeshIdentity.Matches|IL.ReferenceComparison",
                    "WorldGeometryPreparation.TryRead|WorldGeometrySnapshot.ForContext"
                };
                foreach(var typeHandle in metadata.TypeDefinitions) {
                    var type=metadata.GetTypeDefinition(typeHandle);string name=metadata.GetString(type.Name);
                    foreach(var methodHandle in type.GetMethods()) {
                        var method=metadata.GetMethodDefinition(methodHandle);if(method.RelativeVirtualAddress==0)continue;
                        string methodName=metadata.GetString(method.Name);
                        if(name=="NativeWorldGeometry" && (methodName=="BeginLoad" || methodName=="CaptureLoadedBlock" || methodName=="CompleteLoad" || methodName=="Detach")) {
                            bool contained=false;
                            foreach(var region in pe.GetMethodBody(method.RelativeVirtualAddress).ExceptionRegions)
                                if(region.Kind==ExceptionRegionKind.Catch && region.CatchType.Kind==HandleKind.TypeReference && metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)region.CatchType).Name)=="Exception")contained=true;
                            if(!contained)throw new Exception("Native loader hook lacks an exception boundary: "+methodName);
                        }
                        foreach(string call in Calls(metadata,pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes())) {
                            required.Remove(name+"."+methodName+"|"+call);
                            if(name=="NativeWorldGeometry" && methodName=="AdvanceCapture" && call=="WorldGeometrySnapshot..ctor")throw new Exception("Full world indexing still runs in the native capture frame");
                            if(name=="WorldGeometryPreparation" || name=="WorldGeometrySnapshot")
                                if(call.StartsWith("Transform.") || call.StartsWith("Time.") || call.StartsWith("Physics.") || call.StartsWith("WMWorld.") || call.StartsWith("WMBlock.") || call.StartsWith("ff9.") || call.StartsWith("Input.") || call.StartsWith("NativeWorldGeometry.") || call.StartsWith("Object.Find"))throw new Exception("Detached preparation accesses native state: "+name+"."+methodName+" -> "+call);
                        }
                    }
                }
                if(required.Count!=0)throw new Exception("Missing world preparation seam: "+String.Join(", ",required));
                Console.WriteLine("PASS emitted world preparation: warm-up, per-frame copy budget, exact mesh identity and detached background indexing in "+library);
            }
        }
        public static void VerifySpeechLifetime(string library)
        {
            using (var stream = File.OpenRead(library)) using (var pe = new PEReader(stream)) {
                var metadata = pe.GetMetadataReader(); bool request=false, confirmed=false;
                foreach (var handle in metadata.TypeDefinitions) {
                    var type = metadata.GetTypeDefinition(handle); string name=metadata.GetString(type.Name);
                    if (name!="HonoInputManager" && name!="UIKeyTrigger") continue;
                    foreach (var methodHandle in type.GetMethods()) {
                        var method=metadata.GetMethodDefinition(methodHandle);
                        if (metadata.GetString(method.Name)!="OnApplicationQuit" || method.RelativeVirtualAddress==0) continue;
                        var calls=new List<string>(Calls(metadata,pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()));
                        if (name=="HonoInputManager") {
                            if (calls.Contains("ScreenReaderManager.Dispose")) throw new Exception("A cancellable quit request permanently disposes menu speech");
                            request=true;
                        } else {
                            if (!calls.Contains("Application.CancelQuit") || !calls.Contains("ScreenReaderManager.Dispose") || calls.IndexOf("ScreenReaderManager.Dispose")<=calls.IndexOf("Application.CancelQuit")) throw new Exception("Speech cleanup is missing from the confirmed native quit branch");
                            confirmed=true;
                        }
                    }
                }
                if (!request || !confirmed) throw new Exception("Native speech lifetime callbacks were not found");
                Console.WriteLine("PASS emitted speech lifetime: cancellable requests retain speech, confirmed native quit owns disposal in "+library);
            }
        }
        public static void Run(string library)
        {
            using (var stream = File.OpenRead(library)) using (var pe = new PEReader(stream)) {
                var metadata = pe.GetMetadataReader();
                var checks = new[] {
                    "UnityXInput.XInputManager|get_CurrentState|NativeNavigationInput.FilterPad",
                    "UnityXInput.XInputManager|get_PreviousState|NativeNavigationInput.FilterPad",
                    "GamePad|GetState|XInputManager.get_CurrentState",
                    "HonoInputManager|CheckPersistentInput|GamePad.GetState",
                    "HonoInputManager|IsInput|NativeNavigationInput.IsControlCaptured",
                    "HonoInputManager|IsInputDown|NativeNavigationInput.IsControlCaptured",
                    "HonoInputManager|IsInputUp|NativeNavigationInput.IsControlCaptured",
                    "HonoInputManager|CheckPersistentDirectionInput|NativeNavigationInput.IsControlCaptured",
                    "UnityXInput.Input|GetAxis|NativeNavigationInput.FilterAxis",
                    "UnityXInput.Input|GetAxisRaw|NativeNavigationInput.FilterAxis",
                    "UnityXInput.Input|GetKey|NativeNavigationInput.IsKeyCaptured",
                    "UnityXInput.Input|GetKeyDown|NativeNavigationInput.IsKeyCaptured",
                    "UnityXInput.Input|GetKeyUp|NativeNavigationInput.IsKeyCaptured",
                    "FieldMapActorController|MovePC|XInputManager.get_CurrentState",
                    "FieldMapActorController|MovePC|FieldAnalogMovement.Speed",
                    "FieldMapActorController|MovePC|FieldAnalogMovement.ArrivalRadius",
                    "FieldMapActorController|MovePC|FieldAnalogMovement.StepMagnitude",
                    "Memoria.Accessibility.Navigation.NativeNavigationContext|Read|EventInput.get_IsMovementControl",
                    "Memoria.Accessibility.Navigation.NativeNavigationContext|Read|DialogManager.GetChoiceDialog",
                    "Memoria.Accessibility.Navigation.NavigationLifecycleBridge|Release|NavigationSession.RevokeAutomaticResume",
                    "Dialog|AnnounceDialogForScreenReader|NativeDisplayedIdentity.Observe",
                    "Memoria.Accessibility.Navigation.NativeNavigationInput|ReadPhysical|ControllerKeyBindings.Button",
                    "Memoria.Accessibility.Navigation.NativeNavigationInput|ReadPhysical|ControllerKeyBindings.TriggerConfirm",
                    "Memoria.Accessibility.Navigation.NavigationRuntime|ReadMovement|GuidanceRouteValidation.Check",
                    "Memoria.Accessibility.Navigation.NavigationRuntime|ReadMovement|GuidanceRouteValidation.WorldLeg",
                    "WMPad|CollectInput|NativeNavigationInput.get_IsCapturingController",
                    "ff9|w_moveGetPadStateR|XInputManager.get_CurrentState",
                    "ff9|w_moveGetPadStateLY|HonoInputManager.GetAxis",
                    "HonoInputManager|GetHorizontalNavigation|Input.GetAxis",
                    "HonoInputManager|GetVerticalNavigation|Input.GetAxis",
                    "ff9|w_movementControl|NativeWorldMovement.BeforeMovement",
                    "ff9|w_moveGetPadStateLX|WorldMovementOverlay.TryLX",
                    "ff9|w_moveGetPadStateLY|WorldMovementOverlay.TryLY",
                    "ff9|w_moveGetPadStateR|WorldMovementOverlay.TryThrottle",
                    "ff9|w_movementPlaneOperation|WorldMovementOverlay.ApplyFlightY"
                };
                foreach (string check in checks) {
                    string[] parts = check.Split('|'); bool found = false;
                    foreach (var typeHandle in metadata.TypeDefinitions) {
                        var type = metadata.GetTypeDefinition(typeHandle);
                        string name = metadata.GetString(type.Name), ns = metadata.GetString(type.Namespace);
                        if ((ns.Length == 0 ? name : ns + "." + name) != parts[0]) continue;
                        foreach (var methodHandle in type.GetMethods()) {
                            var method = metadata.GetMethodDefinition(methodHandle);
                            if (metadata.GetString(method.Name) != parts[1] || method.RelativeVirtualAddress == 0) continue;
                            foreach (var call in Calls(metadata, pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes())) if (call.EndsWith(parts[2], StringComparison.Ordinal)) found = true;
                        }
                    }
                    if (!found) throw new Exception("Missing emitted input seam: " + check);
                }
                Console.WriteLine("PASS emitted input readers: " + checks.Length + " in " + library);
                var allowed=new HashSet<string>{"w_moveGetPadStateLX","w_moveGetPadStateLY","w_moveGetPadStateR","w_movementPlaneOperation"};int consumers=0;
                foreach(var typeHandle in metadata.TypeDefinitions) {
                    var type=metadata.GetTypeDefinition(typeHandle);string name=metadata.GetString(type.Name);
                    foreach(var methodHandle in type.GetMethods()) {
                        var method=metadata.GetMethodDefinition(methodHandle);if(method.RelativeVirtualAddress==0)continue;
                        string methodName=metadata.GetString(method.Name);
                        foreach(string call in Calls(metadata,pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes())) {
                            if(call.StartsWith("WorldMovementOverlay.",StringComparison.Ordinal)) {
                                if(name!="WorldMovementOverlay" && name!="NativePort" && !(name=="NativeWorldMovement" && methodName=="ValidateNextNativeStep") && !(name=="ff9" && allowed.Contains(methodName)))throw new Exception("Overlay leaked into non-movement consumer: "+name+"."+methodName);
                                consumers++;
                            }
                            if(name=="WorldMovementOverlay" || name=="MovementOverlayState" || name=="OwnedWorldTravel" || name=="WorldSteering" || name=="NativePort")
                                if(call.StartsWith("EventInput.",StringComparison.Ordinal) || call.StartsWith("ETb.",StringComparison.Ordinal) || call.EndsWith(".Request",StringComparison.Ordinal))throw new Exception("Synthetic movement calls an event/input request: "+call);
                        }
                    }
                }
                if(consumers<6)throw new Exception("Missing overlay consumer evidence");
                Console.WriteLine("PASS movement-only overlay consumers: "+consumers);
            }
        }
        private static IEnumerable<string> Calls(MetadataReader metadata, byte[] il)
        {
            for (int offset = 0; offset < il.Length;) {
                ushort code = il[offset++]; if (code == 0xfe) code = (ushort)(0xfe00 | il[offset++]);
                var opcode = Opcodes[code]; int size;
                switch (opcode.OperandType) {
                    case OperandType.InlineNone: size = 0; break;
                    case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: size = 1; break;
                    case OperandType.InlineVar: size = 2; break;
                    case OperandType.InlineI8: case OperandType.InlineR: size = 8; break;
                    case OperandType.InlineSwitch: size = 4 + 4 * BitConverter.ToInt32(il, offset); break;
                    default: size = 4; break;
                }
                // The legacy game build's compiler emits ReferenceEquals as
                // reference branches/ceq, without an Object.ReferenceEquals call.
                if(opcode==OpCodes.Ceq || opcode==OpCodes.Bne_Un || opcode==OpCodes.Bne_Un_S)yield return "IL.ReferenceComparison";
                if(opcode.OperandType==OperandType.InlineField && (opcode==OpCodes.Ldfld || opcode==OpCodes.Ldflda || opcode==OpCodes.Ldsfld || opcode==OpCodes.Stfld || opcode==OpCodes.Stsfld)) {
                    string access=opcode==OpCodes.Stfld || opcode==OpCodes.Stsfld?"FieldWrite.":"FieldRead.";
                    EntityHandle fieldHandle=MetadataTokens.EntityHandle(BitConverter.ToInt32(il,offset));
                    if(fieldHandle.Kind==HandleKind.FieldDefinition) {
                        var field=metadata.GetFieldDefinition((FieldDefinitionHandle)fieldHandle);
                        yield return access+metadata.GetString(metadata.GetTypeDefinition(field.GetDeclaringType()).Name)+"."+metadata.GetString(field.Name);
                    }else if(fieldHandle.Kind==HandleKind.MemberReference) {
                        var member=metadata.GetMemberReference((MemberReferenceHandle)fieldHandle);
                        if(member.Parent.Kind==HandleKind.TypeReference)yield return access+metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)member.Parent).Name)+"."+metadata.GetString(member.Name);
                    }
                }
                if (opcode.OperandType == OperandType.InlineMethod) {
                    EntityHandle handle = MetadataTokens.EntityHandle(BitConverter.ToInt32(il, offset));
                    if(handle.Kind==HandleKind.MethodSpecification)handle=metadata.GetMethodSpecification((MethodSpecificationHandle)handle).Method;
                    if (handle.Kind == HandleKind.MemberReference) {
                        var member = metadata.GetMemberReference((MemberReferenceHandle)handle);
                        if (member.Parent.Kind == HandleKind.TypeReference) yield return metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)member.Parent).Name) + "." + metadata.GetString(member.Name);
                    } else if (handle.Kind == HandleKind.MethodDefinition) {
                        var method = metadata.GetMethodDefinition((MethodDefinitionHandle)handle);
                        yield return metadata.GetString(metadata.GetTypeDefinition(method.GetDeclaringType()).Name) + "." + metadata.GetString(method.Name);
                    }
                }
                offset += size;
            }
        }
        private static Dictionary<ushort, OpCode> BuildOpcodes()
        {
            var result = new Dictionary<ushort, OpCode>(); foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)) if (field.FieldType == typeof(OpCode)) { var value = (OpCode)field.GetValue(null); result[unchecked((ushort)value.Value)] = value; } return result;
        }
    }
}
