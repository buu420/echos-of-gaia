using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
namespace Memoria.Accessibility.AudioDescriptions
{
    // A reviewed, prerecordable field pose: the exact actor model playing the exact clip NAME
    // (the engine resolves clips by name, so duplicate animation IDs share one entry).
    public sealed class FieldPoseDescription
    {
        public readonly int Model;
        public readonly string Clip, CueId, VoiceText;
        public FieldPoseDescription(int model, string clip, string cueId, string voiceText)
        {
            if (String.IsNullOrEmpty(clip) || String.IsNullOrEmpty(cueId) || String.IsNullOrEmpty(voiceText)) throw new ArgumentException("Invalid field pose description");
            Model = model; Clip = clip; CueId = cueId; VoiceText = voiceText;
        }
    }
    // Extra positive evidence for a reviewed composite gesture. Optional native
    // sequence and companion identities use paired -1 values when not required.
    public sealed class FieldPoseProof
    {
        public readonly double MinimumSampleFraction;
        public readonly int SequenceInstruction, SequenceId, CompanionSid, CompanionModel,AttachmentBone;
        public readonly float MinimumDisplacement;
        public readonly string AttachmentBoneName;
        public bool HasSequence { get { return SequenceInstruction >= 0; } }
        public bool HasCompanion { get { return CompanionSid >= 0; } }
        public bool RequiresAttachment { get { return AttachmentBone >= 0; } }
        public FieldPoseProof(double minimumSampleFraction, int sequenceInstruction, int sequenceId, float minimumDisplacement, int companionSid, int companionModel)
            : this(minimumSampleFraction,sequenceInstruction,sequenceId,minimumDisplacement,companionSid,companionModel,-1) { }
        public FieldPoseProof(double minimumSampleFraction,int sequenceInstruction,int sequenceId,float minimumDisplacement,int companionSid,int companionModel,int attachmentBone)
        {
            if (Double.IsNaN(minimumSampleFraction) || Double.IsInfinity(minimumSampleFraction) || minimumSampleFraction < 0 || minimumSampleFraction >= 1 ||
                Single.IsNaN(minimumDisplacement) || Single.IsInfinity(minimumDisplacement) || minimumDisplacement < 0 || minimumDisplacement > FieldDescriptionTracker.TeleportDistance ||
                sequenceInstruction < -1 || sequenceId < -1 || sequenceId > Byte.MaxValue || (sequenceInstruction < 0) != (sequenceId < 0) ||
                companionSid < -1 || companionSid > Byte.MaxValue || companionModel < -1 || companionModel >= UInt16.MaxValue || (companionSid < 0) != (companionModel < 0) ||
                attachmentBone < -1 || attachmentBone > Byte.MaxValue || attachmentBone >= 0 && companionSid < 0 ||
                minimumSampleFraction == 0 && sequenceInstruction < 0 && minimumDisplacement == 0 && companionSid < 0)
                throw new ArgumentException("Invalid reviewed field pose proof");
            MinimumSampleFraction = minimumSampleFraction; SequenceInstruction = sequenceInstruction; SequenceId = sequenceId;
            MinimumDisplacement = minimumDisplacement; CompanionSid = companionSid; CompanionModel = companionModel;
            AttachmentBone=attachmentBone;
            if(attachmentBone>=0)AttachmentBoneName="bone"+attachmentBone.ToString("D3",CultureInfo.InvariantCulture);
        }
        public bool AcceptsCompanionAttachment(int ownerUid,int attachedToUid,int bone,bool actualParentMatches)
        {return !RequiresAttachment || actualParentMatches && ownerUid>=0 && ownerUid<=Byte.MaxValue && attachedToUid==ownerUid && bone==AttachmentBone;}
    }
    // A pose caption reviewed for one exact installed script site. The clip is the
    // engine's actual animation name; it never grants this wording to the generic catalog.
    public sealed class FieldContextPoseDescription
    {
        public readonly int Map, ExecutorSid, Instruction;
        public readonly string EntrySha256, Site;
        public readonly FieldPoseDescription Pose;
        public readonly FieldPoseProof Proof;
        public readonly string SequenceSite;
        public readonly int FirstFrame, LastFrame, FrameCount;
        public bool HasReviewedRange { get { return FirstFrame >= 0; } }
        public FieldContextPoseDescription(int map, int executorSid, string entrySha256, int instruction, int model, string clip, string cueId, string voiceText)
        {
            if (map < 0 || executorSid < 0 || instruction < 0 || String.IsNullOrEmpty(entrySha256) || entrySha256.Length != 64)
                throw new ArgumentException("Invalid contextual field site");
            foreach (char c in entrySha256)
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) throw new ArgumentException("Contextual entry hash must be lowercase SHA-256");
            Map = map; ExecutorSid = executorSid; EntrySha256 = entrySha256; Instruction = instruction;
            Site = map.ToString(CultureInfo.InvariantCulture) + ":" + executorSid.ToString(CultureInfo.InvariantCulture) + ":" + entrySha256 + ":" + instruction.ToString(CultureInfo.InvariantCulture);
            Pose = new FieldPoseDescription(model, clip, cueId, voiceText);
            FirstFrame = LastFrame = -1;
        }
        public FieldContextPoseDescription(int map, int executorSid, string entrySha256, int instruction, int model, string clip, string cueId, string voiceText, FieldPoseProof proof)
            : this(map, executorSid, entrySha256, instruction, model, clip, cueId, voiceText)
        {
            if (proof == null || proof.HasSequence && proof.SequenceInstruction == instruction) throw new ArgumentException("Invalid contextual field pose proof");
            Proof = proof;
            if (proof.HasSequence) SequenceSite = map.ToString(CultureInfo.InvariantCulture) + ":" + executorSid.ToString(CultureInfo.InvariantCulture) + ":" + entrySha256 + ":" + proof.SequenceInstruction.ToString(CultureInfo.InvariantCulture);
        }
        // A range is inclusive and pinned to the native frameN, not an offline renderer's
        // independently computed frame count. The old constructor still requires full playback.
        public FieldContextPoseDescription(int map, int executorSid, string entrySha256, int instruction, int model, string clip, string cueId, string voiceText, int firstFrame, int lastFrame, int frameCount)
            : this(map, executorSid, entrySha256, instruction, model, clip, cueId, voiceText)
        {
            if (firstFrame < 0 || lastFrame <= firstFrame || frameCount < 2 || frameCount >= Byte.MaxValue || lastFrame >= frameCount)
                throw new ArgumentException("Invalid reviewed field pose range");
            FirstFrame = firstFrame; LastFrame = lastFrame; FrameCount = frameCount;
        }
        public FieldContextPoseDescription(int map,int executorSid,string entrySha256,int instruction,int model,string clip,string cueId,string voiceText,int firstFrame,int lastFrame,int frameCount,FieldPoseProof proof)
            : this(map,executorSid,entrySha256,instruction,model,clip,cueId,voiceText,firstFrame,lastFrame,frameCount)
        {
            if(proof==null || proof.HasSequence && proof.SequenceInstruction==instruction || proof.MinimumSampleFraction>(double)lastFrame/frameCount)
                throw new ArgumentException("Invalid or unreachable reviewed range proof");
            Proof=proof;
            if(proof.HasSequence)SequenceSite=map.ToString(CultureInfo.InvariantCulture)+":"+executorSid.ToString(CultureInfo.InvariantCulture)+":"+entrySha256+":"+proof.SequenceInstruction.ToString(CultureInfo.InvariantCulture);
        }
    }
    // Immutable contextual lookup can also be supplied to a tracker by other reviewed
    // chapter catalogs without changing or mutating the global generic pose table.
    public sealed class FieldContextPoseCatalog
    {
        private readonly Dictionary<string, FieldContextPoseDescription> _sites = new Dictionary<string, FieldContextPoseDescription>(StringComparer.Ordinal);
        private readonly HashSet<string> _pairs = new HashSet<string>(StringComparer.Ordinal);
        private readonly ReadOnlyCollection<FieldContextPoseDescription> _entries;
        public IList<FieldContextPoseDescription> Entries { get { return _entries; } }
        public FieldContextPoseCatalog(IEnumerable<FieldContextPoseDescription> entries)
        {
            if (entries == null) throw new ArgumentNullException("entries");
            var copy = new List<FieldContextPoseDescription>();
            foreach (var entry in entries)
            {
                if (entry == null) throw new ArgumentException("Null contextual field description");
                string pair = Pair(entry.Pose.Model, entry.Pose.Clip);
                _sites.Add(entry.Site + "|" + pair, entry);
                _pairs.Add(pair); copy.Add(entry);
            }
            _entries = new ReadOnlyCollection<FieldContextPoseDescription>(copy);
        }
        private static string Pair(int model, string clip) { return model.ToString(CultureInfo.InvariantCulture) + "|" + clip; }
        public FieldPoseDescription FindPose(int model, string clip, string site)
        {
            var entry = FindContextPose(model, clip, site);
            return entry == null ? null : entry.Pose;
        }
        public FieldContextPoseDescription FindContextPose(int model, string clip, string site)
        {
            if (String.IsNullOrEmpty(clip) || String.IsNullOrEmpty(site)) return null;
            FieldContextPoseDescription result;
            return _sites.TryGetValue(site + "|" + Pair(model, clip), out result) ? result : null;
        }
        public bool CanReviewPose(int model, string clip) { return !String.IsNullOrEmpty(clip) && _pairs.Contains(Pair(model, clip)); }
    }
    public enum FieldMotionKind { WalkIntoView, RunIntoView, WalkOutOfView, RunOutOfView, JumpUp, JumpDown, Jump, TurnAround }
    public sealed class FieldMotionDescription
    {
        public readonly int Model;
        public readonly FieldMotionKind Kind;
        public readonly string CueId, VoiceText;
        public FieldMotionDescription(int model, FieldMotionKind kind, string cueId, string voiceText)
        {
            if (String.IsNullOrEmpty(cueId) || String.IsNullOrEmpty(voiceText)) throw new ArgumentException("Invalid field motion description");
            Model = model; Kind = kind; CueId = cueId; VoiceText = voiceText;
        }
    }
    public static partial class FieldDescriptionCatalog
    {
        private static Dictionary<string, FieldPoseDescription> _poseIndex;
        private static Dictionary<int, FieldMotionDescription[]> _motionIndex;
        private static Dictionary<int, string> _subjects;
        public static IList<FieldPoseDescription> Poses { get { return new ReadOnlyCollection<FieldPoseDescription>(_poses); } }
        public static IList<FieldMotionDescription> Motions { get { return new ReadOnlyCollection<FieldMotionDescription>(_motions); } }
        private static void Index()
        {
            if (_poseIndex != null) return;
            var poses = new Dictionary<string, FieldPoseDescription>(StringComparer.Ordinal);
            foreach (var p in _poses) poses.Add(p.Model + "|" + p.Clip, p);
            var motions = new Dictionary<int, FieldMotionDescription[]>();
            var subjects = new Dictionary<int, string>();
            foreach (var m in _motions)
            {
                FieldMotionDescription[] row;
                if (!motions.TryGetValue(m.Model, out row)) motions[m.Model] = row = new FieldMotionDescription[Enum.GetValues(typeof(FieldMotionKind)).Length];
                row[(int)m.Kind] = m;
                // Every motion text begins with the model's reviewed appearance subject.
                if (m.Kind == FieldMotionKind.TurnAround) subjects[m.Model] = m.VoiceText.Substring(0, m.VoiceText.Length - " turns around.".Length);
            }
            _motionIndex = motions; _subjects = subjects; _poseIndex = poses;
        }
        public static FieldPoseDescription FindPose(int model, string clip)
        {
            if (String.IsNullOrEmpty(clip)) return null;
            Index(); FieldPoseDescription result; return _poseIndex.TryGetValue(model + "|" + clip, out result) ? result : null;
        }
        public static FieldPoseDescription FindPose(int model, string clip, string site)
        { return DiscOneFieldDescriptionCatalog.Catalog.FindPose(model, clip, site) ?? FindPose(model, clip); }
        public static bool CanReviewPose(int model, string clip)
        { return FindPose(model, clip) != null || DiscOneFieldDescriptionCatalog.Catalog.CanReviewPose(model, clip); }
        public static FieldMotionDescription FindMotion(int model, FieldMotionKind kind)
        {
            Index(); FieldMotionDescription[] row; return _motionIndex.TryGetValue(model, out row) ? row[(int)kind] : null;
        }
        public static bool HasSubject(int model) { Index(); return _subjects.ContainsKey(model); }
        public static string Subject(int model) { Index(); string s; return _subjects.TryGetValue(model, out s) ? s : null; }
        public static bool HasPoseForModel(int model) { Index(); foreach (var p in _poses) if (p.Model == model) return true; return false; }
    }
    public sealed class FieldDescriptionEmission
    {
        public readonly string CueId, VoiceText, Diagnostic;
        public readonly long Generation, Actor, ActorToken;
        public readonly long CompanionActor, CompanionToken;
        public FieldDescriptionEmission(string cueId, string voiceText, long generation, long actor, long actorToken, string diagnostic)
            : this(cueId, voiceText, generation, actor, actorToken, diagnostic, 0, 0) { }
        public FieldDescriptionEmission(string cueId, string voiceText, long generation, long actor, long actorToken, string diagnostic, long companionActor, long companionToken)
        { CueId = cueId; VoiceText = voiceText; Generation = generation; Actor = actor; ActorToken = actorToken; Diagnostic = diagnostic; CompanionActor = companionActor; CompanionToken = companionToken; }
    }
    // Native constants mirrored for the Unity-free core; FieldDescriptionTests verifies them against
    // EventEngine.Static.cs so a drifted engine value fails the suite.
    public static class FieldEngineFlags
    {
        public const int FlagShow = 1, FlagTurn = 128, ActJump = 8, AfDir = 64, ModeField = 1;
    }
    public sealed class FieldPoseStart
    {
        public long Actor; public int Model; public string Clip, Site;
        public bool Cutscene, DialogOpen, FullRange;
        // Exact native playback metadata required only by explicitly reviewed range rows.
        public int InFrame, OutFrame, FrameCount;
        public bool Forward, NonJump;
        public bool PositionKnown, CompanionVisible;
        public float X, Y, Z;
        public long CompanionActor;
    }
    public sealed class FieldPoseSample
    {
        public long Actor; public string Clip; public int AnimFrame, FrameCount;
        public bool ClipPresent, Visible, ScreenClear;
        public bool PositionKnown, CompanionVisible;
        public float X, Y, Z;
        public long CompanionActor;
    }
    public enum FieldLocomotion { Still, Walk, Run, Other }
    public sealed class FieldMotionSample
    {
        public long Actor; public int Model, Speed; public float X, Y, Z, Yaw;
        public FieldLocomotion Locomotion;
        public bool Rendered, OnScreen, NearEdge, Jumping, Turning, Cutscene, ScreenClear;
    }
}
