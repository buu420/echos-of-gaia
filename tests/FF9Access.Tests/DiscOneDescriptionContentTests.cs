using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Memoria.Accessibility.AudioDescriptions;

namespace FF9Access.Tests
{
    internal static class DiscOneDescriptionContentTests
    {
        public static void ActualKnightToppleWaitsForTheVisibleFallPhase()
        {
            var row = DiscOneFieldDescriptionCatalog.Catalog.Entries.Single(entry => entry.Pose.CueId == "disc1-middle-ice-knight-topples-backward");
            var emissions = new List<FieldDescriptionEmission>();
            var tracker = new FieldDescriptionTracker(emissions.Add);
            var start = new FieldPoseStart { Actor=14, Model=row.Pose.Model, Clip=row.Pose.Clip, Site=row.Site,
                Cutscene=true, InFrame=0, OutFrame=66, FrameCount=80, Forward=true, NonJump=true };
            TestAssert.Equal("pending",tracker.StartPose(start));
            for(int frame=0;frame<53;frame++)
                tracker.SamplePose(new FieldPoseSample { Actor=14, Clip=row.Pose.Clip, AnimFrame=frame, FrameCount=80, ClipPresent=true, Visible=true, ScreenClear=true });
            // Installed native-time pose samples remain upright/turning early;
            // the backward airborne/toppling phase is visible in samples53/66.
            TestAssert.Equal(0,emissions.Count);
            for(int frame=53;frame<=66;frame++)
                tracker.SamplePose(new FieldPoseSample { Actor=14, Clip=row.Pose.Clip, AnimFrame=frame, FrameCount=80, ClipPresent=true, Visible=true, ScreenClear=true });
            TestAssert.Equal(1,emissions.Count);
            TestAssert.Equal(row.Pose.CueId,emissions[0].CueId);
        }

        private static string RepoFile(string path)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Assembly-CSharp"))) directory = directory.Parent;
            if (directory == null) throw new Exception("Repository root not found");
            return Path.Combine(directory.FullName, path);
        }

        public static void CompiledSceneBindingsPublicEvidenceAndShippedSpeechAgree()
        {
            using var evidence = JsonDocument.Parse(File.ReadAllText(RepoFile("docs/accessibility/disc1-field-descriptions.json")));
            using var manifest = JsonDocument.Parse(File.ReadAllText(RepoFile("assets/audio-descriptions/manifest.json")));
            var described = evidence.RootElement.GetProperty("cues").EnumerateArray().ToDictionary(row => row.GetProperty("cueId").GetString());
            var recordings = manifest.RootElement.GetProperty("clips").EnumerateArray().ToDictionary(row => row.GetProperty("id").GetString());
            var banks = new Dictionary<string, JsonElement>();
            if (manifest.RootElement.TryGetProperty("fieldBanks", out var bankRows))
                foreach (var bank in bankRows.EnumerateArray()) banks.Add(bank.GetProperty("id").GetString(), bank);
            var mapOwners = new Dictionary<int, string>();
            var clipOwners = new Dictionary<string, string>();
            foreach (var bank in banks)
            {
                foreach (var map in bank.Value.GetProperty("fields").EnumerateArray()) mapOwners.Add(map.GetInt32(), bank.Key);
                foreach (var clip in bank.Value.GetProperty("clips").EnumerateArray()) clipOwners.Add(clip.GetString(), bank.Key);
            }
            TestAssert.Equal(DiscOneFieldDescriptionCatalog.Catalog.Entries.Count, described.Count);
            foreach (var row in DiscOneFieldDescriptionCatalog.Catalog.Entries)
            {
                if (!described.TryGetValue(row.Pose.CueId, out var data)) throw new Exception("Compiled caption missing public evidence: " + row.Pose.CueId);
                TestAssert.Equal(row.Map, data.GetProperty("field").GetInt32());
                TestAssert.Equal(row.ExecutorSid, data.GetProperty("executorSid").GetInt32());
                TestAssert.Equal(row.EntrySha256, data.GetProperty("entrySha256").GetString());
                TestAssert.Equal(row.Instruction, data.GetProperty("instruction").GetInt32());
                TestAssert.Equal(row.Pose.Model, data.GetProperty("model").GetInt32());
                TestAssert.Equal(row.Pose.Clip, data.GetProperty("clip").GetString());
                TestAssert.Equal(row.Pose.VoiceText, data.GetProperty("voiceText").GetString());
                if (!recordings.TryGetValue(row.Pose.CueId, out var recording)) throw new Exception("Compiled caption missing recording: " + row.Pose.CueId);
                TestAssert.Equal(row.Pose.VoiceText, recording.GetProperty("text").GetString());
                TestAssert.Equal(data.GetProperty("bank").GetString(), mapOwners[row.Map]);
                TestAssert.Equal(mapOwners[row.Map], clipOwners[row.Pose.CueId]);
                bool ranged = data.TryGetProperty("firstFrame", out var first);
                TestAssert.Equal(row.HasReviewedRange, ranged);
                if (ranged)
                {
                    TestAssert.Equal(row.FirstFrame, first.GetInt32());
                    TestAssert.Equal(row.LastFrame, data.GetProperty("lastFrame").GetInt32());
                    TestAssert.Equal(row.FrameCount, data.GetProperty("expectedNativeFrameCount").GetInt32());
                }
                bool guarded = data.TryGetProperty("proof", out var proof);
                TestAssert.Equal(row.Proof != null, guarded);
                if (guarded)
                {
                    TestAssert.Equal(row.Proof.MinimumSampleFraction, proof.GetProperty("minimumSampleFraction").GetDouble());
                    TestAssert.Equal(row.Proof.SequenceInstruction, proof.GetProperty("sequenceInstruction").GetInt32());
                    TestAssert.Equal(row.Proof.SequenceId, proof.GetProperty("sequenceId").GetInt32());
                    TestAssert.Equal(row.Proof.MinimumDisplacement, proof.GetProperty("minimumDisplacement").GetSingle());
                    TestAssert.Equal(row.Proof.CompanionSid, proof.GetProperty("companionSid").GetInt32());
                    TestAssert.Equal(row.Proof.CompanionModel, proof.GetProperty("companionModel").GetInt32());
                    TestAssert.Equal(row.Proof.AttachmentBone, proof.TryGetProperty("attachmentBone", out var bone) ? bone.GetInt32() : -1);
                }
            }
            var objects = evidence.RootElement.GetProperty("presentedObjects").EnumerateArray().ToDictionary(row =>
                row.GetProperty("field").GetInt32() + ":" + row.GetProperty("actorSid").GetInt32() + ":" + row.GetProperty("model").GetInt32() + ":" + row.GetProperty("clip").GetString());
            TestAssert.Equal(DiscOnePresentedObjectCatalog.Catalog.Entries.Count, objects.Count);
            foreach (var row in DiscOnePresentedObjectCatalog.Catalog.Entries)
            {
                var data = objects[row.Map + ":" + row.Sid + ":" + row.Model + ":" + row.Clip];
                TestAssert.Equal(row.EntrySha256, data.GetProperty("entrySha256").GetString());
                TestAssert.Equal(row.CueId, data.GetProperty("cueId").GetString());
                TestAssert.Equal(row.VoiceText, data.GetProperty("voiceText").GetString());
                TestAssert.Equal(row.Mode.ToString(), data.GetProperty("mode").GetString());
                if (!recordings.TryGetValue(row.CueId, out var recording)) throw new Exception("Presented object missing recording: " + row.CueId);
                TestAssert.Equal(row.VoiceText, recording.GetProperty("text").GetString());
                TestAssert.Equal(data.GetProperty("bank").GetString(), mapOwners[row.Map]);
                TestAssert.Equal(mapOwners[row.Map], clipOwners[row.CueId]);
            }
        }
    }
}
