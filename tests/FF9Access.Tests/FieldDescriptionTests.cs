using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Memoria.Accessibility.AudioDescriptions;
namespace FF9Access.Tests
{
    internal static class FieldDescriptionTests
    {
        private const int Zidane = 98;
        private const string Crouch = "ANH_MAIN_F0_ZDN_HIZA_1";
        private static string RepoFile(string relative)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assembly-CSharp"))) dir = dir.Parent;
            if (dir == null) throw new Exception("Repository root not found");
            return Path.Combine(dir.FullName, relative);
        }
        private static FieldDescriptionTracker Tracker(List<FieldDescriptionEmission> output) { return new FieldDescriptionTracker(output.Add); }
        private static FieldPoseStart Start(string clip = Crouch, string site = "50:17:abc:617", bool cutscene = true, bool dialog = false, bool full = true, long actor = 17, int model = Zidane)
        { return new FieldPoseStart { Actor = actor, Model = model, Clip = clip, Site = site, Cutscene = cutscene, DialogOpen = dialog, FullRange = full }; }
        private static FieldPoseSample Sample(int frame, string clip = Crouch, bool clipPresent = true, bool visible = true, bool clear = true, long actor = 17, int count = 25)
        { return new FieldPoseSample { Actor = actor, Clip = clip, AnimFrame = frame, FrameCount = count, ClipPresent = clipPresent, Visible = visible, ScreenClear = clear }; }
        private static void Play(FieldDescriptionTracker t, int from, int to, Func<int, FieldPoseSample> sample) { for (int f = from; f <= to; f++) t.SamplePose(sample(f)); }

        public static void ReviewedPoseIsDescribedOnlyAfterTheNativeClipVisiblyProgresses()
        {
            var output = new List<FieldDescriptionEmission>(); var t = Tracker(output);
            TestAssert.Equal("pending", t.StartPose(Start()));
            Play(t, 0, 3, f => Sample(f));
            TestAssert.Equal(0, output.Count);
            Play(t, 4, 12, f => Sample(f));
            TestAssert.Equal(1, output.Count);
            TestAssert.Equal("field-main-f0-zdn-hiza-1", output[0].CueId);
            TestAssert.Equal("The boy with the tail crouches down.", output[0].VoiceText);
            Play(t, 13, 24, f => Sample(f));
            TestAssert.Equal(1, output.Count);
        }
        public static void MissingClipNeverProducesADescription()
        {
            var output = new List<FieldDescriptionEmission>(); var t = Tracker(output);
            t.StartPose(Start()); Play(t, 0, 24, f => Sample(f, clipPresent: false));
            TestAssert.Equal(0, output.Count); TestAssert.Equal(0, t.PendingCount);
        }
        public static void HiddenOrFadedProgressIsNotEvidence()
        {
            var output = new List<FieldDescriptionEmission>(); var t = Tracker(output);
            t.StartPose(Start()); Play(t, 0, 2, f => Sample(f)); Play(t, 3, 24, f => Sample(f, visible: false));
            t.StartPose(Start(site: "50:17:abc:700")); Play(t, 0, 24, f => Sample(f, clear: false));
            TestAssert.Equal(0, output.Count);
        }
        public static void UnreviewedHoldFidgetAndOtherModelClipsAreIgnored()
        {
            var output = new List<FieldDescriptionEmission>(); var t = Tracker(output);
            TestAssert.Equal("unreviewed", t.StartPose(Start(clip: "ANH_MAIN_F0_ZDN_HIZA_2")));
            TestAssert.Equal("unreviewed", t.StartPose(Start(clip: "ANH_MAIN_F0_VIV_BREAK1", model: 8)));
            TestAssert.Equal("unreviewed", t.StartPose(Start(clip: "ANH_MAIN_F0_ZDN_TALK", site: "x")));
            TestAssert.Equal("unreviewed", t.StartPose(Start(model: 668)));
            TestAssert.True(FieldDescriptionCatalog.FindPose(Zidane, "ANH_MAIN_F0_ZDN_IDLE") == null);
        }
        public static void PartialFrameRangeIsUnsupported()
        {
            var output = new List<FieldDescriptionEmission>(); var t = Tracker(output);
            TestAssert.Equal("partial", t.StartPose(Start(full: false))); Play(t, 0, 24, f => Sample(f));
            TestAssert.Equal(0, output.Count);
        }
        public static void AmbientLoopsAndRepeatedSitesStaySilent()
        {
            var output = new List<FieldDescriptionEmission>(); var t = Tracker(output);
            TestAssert.Equal("ambient", t.StartPose(Start(cutscene: false))); Play(t, 0, 24, f => Sample(f));
            TestAssert.Equal(0, output.Count);
            TestAssert.Equal("pending", t.StartPose(Start(site: "s2"))); Play(t, 0, 24, f => Sample(f));
            TestAssert.Equal("repeat", t.StartPose(Start(site: "s2"))); Play(t, 0, 24, f => Sample(f));
            TestAssert.Equal(1, output.Count);
            TestAssert.Equal("pending", t.StartPose(Start(site: "s3", cutscene: false, dialog: true)));
        }
        public static void ReplacedAnimationBeforeProofIsDropped()
        {
            var output = new List<FieldDescriptionEmission>(); var t = Tracker(output);
            t.StartPose(Start()); Play(t, 0, 3, f => Sample(f));
            t.SamplePose(Sample(4, clip: "ANH_MAIN_F0_ZDN_IDLE")); Play(t, 5, 24, f => Sample(f));
            TestAssert.Equal(0, output.Count);
        }
        public static void BoundariesInvalidatePendingAndQueuedCues()
        {
            var output = new List<FieldDescriptionEmission>(); var t = Tracker(output);
            t.StartPose(Start()); Play(t, 0, 3, f => Sample(f)); t.Invalidate("menu"); Play(t, 4, 24, f => Sample(f));
            TestAssert.Equal(0, output.Count);
            t.StartPose(Start(site: "other")); Play(t, 0, 24, f => Sample(f));
            TestAssert.Equal(1, output.Count); TestAssert.True(t.IsCurrent(output[0]));
            t.Invalidate("focus"); TestAssert.False(t.IsCurrent(output[0].Generation));
        }
        public static void ReturningToIdleAfterTheCueKeepsItCurrent()
        {
            var output = new List<FieldDescriptionEmission>(); var t = Tracker(output);
            t.StartPose(Start()); Play(t, 0, 24, f => Sample(f)); t.SamplePose(Sample(0, clip: "ANH_MAIN_F0_ZDN_IDLE"));
            TestAssert.Equal(1, output.Count); TestAssert.True(t.IsCurrent(output[0].Generation));
        }
        public static void DeletedActorInvalidatesAnEmittedCue()
        {
            var output=new List<FieldDescriptionEmission>();var t=Tracker(output);
            t.StartPose(Start());Play(t,0,24,f=>Sample(f));t.EndFrame(new long[0]);
            TestAssert.Equal(1,output.Count);TestAssert.False(t.IsCurrent(output[0]));
        }
        public static void ReplacedActorModelInvalidatesAnEmittedCue()
        {
            var output=new List<FieldDescriptionEmission>();var t=Tracker(output);
            t.StartPose(Start());Play(t,0,24,f=>Sample(f));t.StartPose(Start(clip:"unreviewed",model:668));
            TestAssert.Equal(1,output.Count);TestAssert.False(t.IsCurrent(output[0]));
        }
        public static void ReusedActorKeyCannotReviveOldCueAndOtherActorsStayCurrent()
        {
            var output=new List<FieldDescriptionEmission>();var t=Tracker(output);
            t.StartPose(Start());Play(t,0,24,f=>Sample(f));
            t.StartPose(Start(actor:18));Play(t,0,24,f=>Sample(f,actor:18));
            t.EndFrame(new long[]{18});TestAssert.False(t.IsCurrent(output[0]));TestAssert.True(t.IsCurrent(output[1]));
            t.ObserveActor(17,Zidane);TestAssert.False(t.IsCurrent(output[0]));TestAssert.True(t.IsCurrent(output[1]));
        }
        public static void SceneResetForgetsSitesButFieldVisitDedupHolds()
        {
            var output = new List<FieldDescriptionEmission>(); var t = Tracker(output);
            t.StartPose(Start()); Play(t, 0, 24, f => Sample(f));
            t.Invalidate("menu"); TestAssert.Equal("repeat", t.StartPose(Start()));
            t.ResetScene("field change"); TestAssert.Equal("pending", t.StartPose(Start())); Play(t, 0, 24, f => Sample(f));
            TestAssert.Equal(2, output.Count);
        }
        public static void StalePendingObservationsExpire()
        {
            var output = new List<FieldDescriptionEmission>(); var t = Tracker(output);
            t.StartPose(Start()); for (int i = 0; i < 400; i++) t.EndFrame(new long[] { 17 });
            Play(t, 0, 24, f => Sample(f)); TestAssert.Equal(0, output.Count);
            t.StartPose(Start(site: "s9")); t.EndFrame(new long[0]); Play(t, 0, 24, f => Sample(f));
            TestAssert.Equal(0, output.Count);
        }
        public static void CatalogShipsOnlyFixedTextWithAppearanceSubjects()
        {
            // Pose-only models (props, generic NPC types) have no motion row, so read their reviewed subject from the published list.
            var subjects = new Dictionary<int, string>();
            using (var doc = JsonDocument.Parse(File.ReadAllText(RepoFile("docs/accessibility/field-action-descriptions.json"))))
                foreach (var s in doc.RootElement.GetProperty("subjects").EnumerateArray()) subjects.Add(s.GetProperty("model").GetInt32(), s.GetProperty("subject").GetString());
            const string forbidden = @"\b(his|her|him|he|she|Zidane|Garnet|Dagger|Vivi|Steiner|Eiko|Freya|Baku|Quina|Amarant|Beatrix|Blank|Marcus|Cinna|Ruby|Tot|Zorn|Thorn|Kuja|Cid|Moogle|Mog)\b";
            var ids = new HashSet<string>();
            foreach (var p in FieldDescriptionCatalog.Poses)
            {
                TestAssert.True(ids.Add(p.CueId)); TestAssert.True(Regex.IsMatch(p.CueId, "^[a-z0-9][a-z0-9-]*$"));
                TestAssert.True(p.VoiceText.StartsWith(subjects[p.Model] + " "));
                if (FieldDescriptionCatalog.HasSubject(p.Model)) TestAssert.Equal(subjects[p.Model], FieldDescriptionCatalog.Subject(p.Model));
                TestAssert.False(Regex.IsMatch(p.VoiceText, forbidden, RegexOptions.IgnoreCase));
                TestAssert.False(Regex.IsMatch(p.Clip, "^ANH_[A-Z0-9]+_[A-Z0-9]+_[A-Z0-9]+_(IDLE|WALK|RUN|TURN_L|TURN_R|TALK.*|BREAK.*)$") || Regex.IsMatch(p.Clip, "_[A-Z]+_2$"));
            }
            foreach (var m in FieldDescriptionCatalog.Motions)
            {
                TestAssert.True(ids.Add(m.CueId)); TestAssert.True(m.VoiceText.StartsWith(FieldDescriptionCatalog.Subject(m.Model) + " "));
                TestAssert.Equal(subjects[m.Model], FieldDescriptionCatalog.Subject(m.Model));
                TestAssert.False(Regex.IsMatch(m.VoiceText, forbidden, RegexOptions.IgnoreCase));
            }
            TestAssert.Equal(84, FieldDescriptionCatalog.Poses.Count);
            TestAssert.Equal(128, FieldDescriptionCatalog.Motions.Count);
        }
        public static void OriginalPublishedCuesStayFirstAndUnchanged()
        {
            // Recorded audio exists for the first 95 cues (39 poses, 56 motions); additions may only be appended.
            using (var doc = JsonDocument.Parse(File.ReadAllText(RepoFile("docs/accessibility/field-action-descriptions.json"))))
            {
                var cues = doc.RootElement.GetProperty("cues").EnumerateArray().Select(c => c.GetProperty("cueId").GetString() + "|" + c.GetProperty("voiceText").GetString()).ToList();
                TestAssert.True(cues.Count > 95);
                string original = string.Join("\n", cues.Take(95));
                string hash;
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    hash = string.Concat(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(original)).Select(b => b.ToString("x2")));
                TestAssert.Equal("d452a330de0db7537c5a1e3c557ffe807e102989d966de828696226a6d7025db", hash);
            }
        }
        public static void PublishedCueListMatchesTheCompiledCatalogExactly()
        {
            using (var doc = JsonDocument.Parse(File.ReadAllText(RepoFile("docs/accessibility/field-action-descriptions.json"))))
            {
                // Published order is append-only across review rounds; within each kind it matches the compiled order.
                var cues = doc.RootElement.GetProperty("cues").EnumerateArray().ToList();
                Func<string, string> published = kind => string.Join("\n", cues.Where(c => c.GetProperty("kind").GetString() == kind).Select(c => c.GetProperty("cueId").GetString() + "|" + c.GetProperty("voiceText").GetString()));
                TestAssert.Equal(string.Join("\n", FieldDescriptionCatalog.Poses.Select(p => p.CueId + "|" + p.VoiceText)), published("pose"));
                TestAssert.Equal(string.Join("\n", FieldDescriptionCatalog.Motions.Select(m => m.CueId + "|" + m.VoiceText)), published("motion"));
                TestAssert.Equal(FieldDescriptionCatalog.Poses.Count + FieldDescriptionCatalog.Motions.Count, cues.Count);
                foreach (var c in cues)
                    if (c.GetProperty("kind").GetString() == "motion")
                        TestAssert.True(FieldDescriptionCatalog.FindMotion(c.GetProperty("model").GetInt32(), (FieldMotionKind)Enum.Parse(typeof(FieldMotionKind), c.GetProperty("motion").GetString())).CueId == c.GetProperty("cueId").GetString());
                foreach (var c in doc.RootElement.GetProperty("cues").EnumerateArray())
                    if (c.GetProperty("kind").GetString() == "pose")
                        TestAssert.True(FieldDescriptionCatalog.FindPose(c.GetProperty("model").GetInt32(), c.GetProperty("clip").GetString()).CueId == c.GetProperty("cueId").GetString());
            }
        }
        public static void DuplicateAnimationIdsResolveToTheSameReviewedClipName()
        {
            // AnimationDB lists 2590 and 2591 as ANH_MAIN_F0_ZDN_HIZA_3; the engine plays clips by NAME
            // (AnimationFactory.AddAnimWithAnimatioName / GetRenameAnimationPath, first key of the name).
            string db = File.ReadAllText(RepoFile("Assembly-CSharp/Global/ff9/FF9DBAll.Animation.cs"));
            TestAssert.True(Regex.IsMatch(db, @"\{2590,\s*""ANH_MAIN_F0_ZDN_HIZA_3""\}") && Regex.IsMatch(db, @"\{2591,\s*""ANH_MAIN_F0_ZDN_HIZA_3""\}"));
            TestAssert.True(FieldDescriptionCatalog.FindPose(Zidane, "ANH_MAIN_F0_ZDN_HIZA_3") != null);
            string factory = File.ReadAllText(RepoFile("Assembly-CSharp/Global/AnimationFactory.cs"));
            TestAssert.True(factory.Contains("AnimationDB.TryGetKey(animName, out Int32 animKey)"));
        }
        public static void MirroredEngineConstantsMatchTheEngineSource()
        {
            string s = File.ReadAllText(RepoFile("Assembly-CSharp/Global/Event/Engine/EventEngine.Static.cs"));
            Func<string, int> value = name => int.Parse(Regex.Match(s, @"\b" + name + @"\s*=\s*(\d+);").Groups[1].Value);
            TestAssert.Equal(value("flagShow"), FieldEngineFlags.FlagShow);
            TestAssert.Equal(value("flagTurn"), FieldEngineFlags.FlagTurn);
            TestAssert.Equal(value("actJump"), FieldEngineFlags.ActJump);
            TestAssert.Equal(value("afDir"), FieldEngineFlags.AfDir);
            TestAssert.Equal(value("modeField"), FieldEngineFlags.ModeField);
            string events = File.ReadAllText(RepoFile("Assembly-CSharp/Global/Event/Engine/EventEngine.ProcessEvents.cs"));
            TestAssert.True(events.Contains("this.SetAnim(actor, actor.speed < actor.speedth ? actor.walk : actor.run);"));
            string stay = File.ReadAllText(RepoFile("Assembly-CSharp/Global/Event/Engine/EventEngine.cs"));
            TestAssert.True(stay.Contains("this.gExec.ip = this._lastIP;"));
        }
    }
}
