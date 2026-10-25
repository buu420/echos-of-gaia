using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.AudioDescriptions
{
    // Unity-free proof tracker for field action descriptions. The native bridge feeds it
    // what the engine actually did (clip started, clip sampled, per-frame actor state);
    // it emits a fixed reviewed cue only once the observation is proven visible.
    public sealed partial class FieldDescriptionTracker
    {
        public const int ProofSamples = 3;          // distinct progressed sampled frames
        public const double ProofFraction = 0.3;    // of the clip or exact reviewed range, all while visible
        public const int PendingLimitFrames = 300;  // ~10 s at 30 fps
        public const float TeleportDistance = 240f; // per frame, or 4x the actor speed
        public const float JumpHeight = 150f;       // field units, Unity Y-up
        public const float TurnAroundDegrees = 150f;
        public const int MotionRepeatFrames = 150;

        private sealed class Pending
        {
            internal FieldPoseDescription Entry; internal FieldContextPoseDescription Range;
            internal FieldPoseProof Proof; internal string SequenceSite; internal bool PhaseSeen;
            internal float OriginX,OriginY,OriginZ; internal int NativeFrameCount;
            internal long CompanionActor,CompanionToken;
            internal string Clip, Key, SiteKey; internal bool Contextual; internal int First = -1, Last = -1, Progressed, Age;
        }
        private sealed class Motion
        {
            internal bool HasLast; internal float X, Y, Z;
            internal int Offscreen, Enter, EnterRun, Visible, VisibleRun; internal bool Entering, LastNearEdge;
            internal bool Jumping, JumpVisible, GroundedVisible; internal float JumpY;
            internal bool Turning, TurnVisible; internal float TurnYaw;
            internal void ResetEpisodes() { Offscreen = Enter = EnterRun = Visible = VisibleRun = 0; Entering = LastNearEdge = false; JumpVisible = TurnVisible = GroundedVisible = false; }
        }
        private readonly Action<FieldDescriptionEmission> _emit;
        private readonly FieldContextPoseCatalog _context;
        private readonly Dictionary<long, Pending> _pending = new Dictionary<long, Pending>();
        private readonly Dictionary<long, Motion> _motion = new Dictionary<long, Motion>();
        private sealed class ActorLifetime { internal int Model; internal long Token; }
        private readonly Dictionary<long, ActorLifetime> _actors = new Dictionary<long, ActorLifetime>();
        private long _nextActorToken;
        private readonly HashSet<string> _seenSites = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _emitted = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _lastMotion = new Dictionary<string, long>(StringComparer.Ordinal);
        private long _generation = 1, _frame;
        public FieldDescriptionTracker(Action<FieldDescriptionEmission> emit) : this(emit, DiscOneFieldDescriptionCatalog.Catalog) { }
        public FieldDescriptionTracker(Action<FieldDescriptionEmission> emit, FieldContextPoseCatalog context)
            : this(emit,context,DiscOnePresentedObjectCatalog.Catalog) { }
        public FieldDescriptionTracker(Action<FieldDescriptionEmission> emit,FieldContextPoseCatalog context,FieldPresentedObjectCatalog presented)
        {
            if (emit == null) throw new ArgumentNullException("emit");
            if (context == null) throw new ArgumentNullException("context");
            if (presented == null) throw new ArgumentNullException("presented");
            _emit = emit; _context = context;_presentedCatalog=presented;
        }
        public long Generation { get { return _generation; } }
        public int PendingCount { get { return _pending.Count; } }
        public bool IsPending(long actor) { return _pending.ContainsKey(actor); }
        public FieldPoseProof PendingProof(long actor) { Pending pending;return _pending.TryGetValue(actor,out pending)?pending.Proof:null; }
        public bool IsCurrent(long generation) { return generation == _generation; }
        public bool IsCurrent(FieldDescriptionEmission emission)
        {
            ActorLifetime actor,companion;
            return emission!=null && IsCurrent(emission.Generation) && _actors.TryGetValue(emission.Actor,out actor) && actor.Token==emission.ActorToken
                && (emission.CompanionToken==0 || _actors.TryGetValue(emission.CompanionActor,out companion) && companion.Token==emission.CompanionToken);
        }
        public void ObserveActor(long actor, int model)
        {
            ActorLifetime lifetime;
            if(_actors.TryGetValue(actor,out lifetime) && lifetime.Model==model)return;
            _actors[actor]=new ActorLifetime{Model=model,Token=++_nextActorToken};
            _pending.Remove(actor);_motion.Remove(actor);
            _presentedPending.Remove(actor);
        }

        // Menu, focus, battle transition, actor replacement: queued cues become stale and unproven
        // observations are discarded, but this field visit's dedupe survives.
        public void Invalidate(string reason) { _generation++; _pending.Clear(); _motion.Clear(); _actors.Clear();InvalidatePresentedObjects(); }
        // A different field visit: everything is new again.
        public void ResetScene(string reason) { Invalidate(reason); _seenSites.Clear(); _emitted.Clear(); _lastMotion.Clear();ResetPresentedObjects(); }
        // Only a subsequent event for this exact current pending actor can establish
        // its phase. There is no scene-wide history for a later animation to reuse.
        public void OnSequenceStarted(long actor,string site,int sequenceId)
        {
            Pending pending;
            if(!_pending.TryGetValue(actor,out pending) || pending.Proof==null || !pending.Proof.HasSequence)return;
            pending.PhaseSeen=String.Equals(site,pending.SequenceSite,StringComparison.Ordinal) && sequenceId==pending.Proof.SequenceId;
        }

        public string StartPose(FieldPoseStart start)
        {
            if (start == null) return "invalid";
            ObserveActor(start.Actor,start.Model);
            _pending.Remove(start.Actor); // a new clip replaces any unproven one for this actor
            var context = _context.FindContextPose(start.Model, start.Clip, start.Site);
            var entry = context == null ? null : context.Pose;
            bool contextual = context != null;
            if (entry == null) entry = FieldDescriptionCatalog.FindPose(start.Model, start.Clip);
            if (entry == null) return "unreviewed";
            bool ranged = contextual && context.HasReviewedRange;
            if (ranged)
            {
                // Exact authored bounds do not grant permission to narrate a reversed clip,
                // a native jump or a different-length asset. These facts are independent of
                // FullRange, whose old combined eligibility gate remains in force below.
                if (!start.Forward || !start.NonJump) return "unsupported playback";
                if (start.InFrame != context.FirstFrame || start.OutFrame != context.LastFrame || start.FrameCount != context.FrameCount) return "range mismatch";
            }
            else if (!start.FullRange) return "partial";
            if (!start.Cutscene && !start.DialogOpen) return "ambient";
            FieldPoseProof proof=context==null?null:context.Proof;long companionToken=0;
            if(proof!=null) {
                if(start.FrameCount<2 || start.FrameCount>=Byte.MaxValue)return "invalid proof frames";
                if(proof.MinimumDisplacement>0 && (!start.PositionKnown || !Finite(start.X,start.Y,start.Z)))return "position unavailable";
                if(proof.HasCompanion) {
                    if(!start.CompanionVisible || start.CompanionActor==start.Actor)return "companion unavailable";
                    ObserveActor(start.CompanionActor,proof.CompanionModel);companionToken=_actors[start.CompanionActor].Token;
                }
            }
            string key = (start.Site ?? "") + "|" + start.Actor;
            // Re-execution of the same instruction for the same actor in one visit is a loop or a
            // continuation, never a new action.
            // Preserve the generic loop policy. A contextual row is committed only after
            // positive presentation proof, so a failed observation is not a delivered action.
            if (contextual ? _seenSites.Contains(key) : !_seenSites.Add(key)) return "repeat";
            _pending[start.Actor] = new Pending {
                Entry=entry,Range=ranged?context:null,Clip=start.Clip,Key=key+"|"+start.Clip,SiteKey=key,Contextual=contextual,
                Proof=proof,SequenceSite=context==null?null:context.SequenceSite,PhaseSeen=proof==null || !proof.HasSequence,
                OriginX=start.X,OriginY=start.Y,OriginZ=start.Z,NativeFrameCount=start.FrameCount,
                CompanionActor=start.CompanionActor,CompanionToken=companionToken
            };
            return "pending";
        }
        public void SamplePose(FieldPoseSample sample)
        {
            Pending p;
            if (sample == null || !_pending.TryGetValue(sample.Actor, out p)) return;
            if (!String.Equals(sample.Clip, p.Clip, StringComparison.Ordinal) || !sample.ClipPresent || !sample.Visible || !sample.ScreenClear || sample.FrameCount < 2)
            { _pending.Remove(sample.Actor); return; }
            if (p.Range != null && (sample.FrameCount != p.Range.FrameCount || sample.AnimFrame < p.Range.FirstFrame || sample.AnimFrame > p.Range.LastFrame))
            { _pending.Remove(sample.Actor); return; }
            if(p.Proof!=null) {
                ActorLifetime companion;
                if(sample.FrameCount!=p.NativeFrameCount || sample.AnimFrame<0 || sample.AnimFrame>=sample.FrameCount ||
                    p.Proof.MinimumDisplacement>0 && (!sample.PositionKnown || !Finite(sample.X,sample.Y,sample.Z)) ||
                    p.Proof.HasCompanion && (!sample.CompanionVisible || sample.CompanionActor!=p.CompanionActor || !_actors.TryGetValue(p.CompanionActor,out companion) || companion.Token!=p.CompanionToken))
                { _pending.Remove(sample.Actor); return; }
            }
            if (p.First < 0) { p.First = p.Last = sample.AnimFrame; return; }
            if (sample.AnimFrame == p.Last && p.Proof==null) return;
            if (sample.AnimFrame < p.Last) { _pending.Remove(sample.Actor); return; } // looped/rewound before proof
            // A contextual semantic wait can finish while its already-proven
            // late pose is held. Duplicate samples never earn progress credit.
            if(sample.AnimFrame!=p.Last){p.Last=sample.AnimFrame;p.Progressed++;}
            int span = p.Range == null ? sample.FrameCount - 1 : p.Range.LastFrame - p.Range.FirstFrame;
            if (p.Progressed < ProofSamples || (double)(p.Last - p.First) / span < ProofFraction) return;
            if(p.Proof!=null) {
                if((double)sample.AnimFrame/sample.FrameCount<p.Proof.MinimumSampleFraction || !p.PhaseSeen)return;
                if(p.Proof.MinimumDisplacement>0) {
                    double dx=(double)sample.X-p.OriginX,dy=(double)sample.Y-p.OriginY,dz=(double)sample.Z-p.OriginZ;
                    if(dx*dx+dy*dy+dz*dz<=(double)p.Proof.MinimumDisplacement*p.Proof.MinimumDisplacement)return;
                }
            }
            _pending.Remove(sample.Actor);
            if (p.Contextual) _seenSites.Add(p.SiteKey);
            if (_emitted.Add(p.Key)) _emit(new FieldDescriptionEmission(p.Entry.CueId,p.Entry.VoiceText,_generation,sample.Actor,_actors[sample.Actor].Token,"pose "+p.Key,p.CompanionActor,p.CompanionToken));
        }
        private static bool Finite(float x,float y,float z)
        {return !Single.IsNaN(x) && !Single.IsInfinity(x) && !Single.IsNaN(y) && !Single.IsInfinity(y) && !Single.IsNaN(z) && !Single.IsInfinity(z);}

        public void ObserveMotion(FieldMotionSample s)
        {
            if (s == null) return;
            ObserveActor(s.Actor,s.Model);
            if(!FieldDescriptionCatalog.HasSubject(s.Model))return;
            Motion m;
            if (!_motion.TryGetValue(s.Actor, out m)) _motion[s.Actor] = m = new Motion();
            float previousY=m.Y;bool groundedVisible=m.GroundedVisible;
            float d = m.HasLast ? (float)Math.Sqrt((s.X - m.X) * (s.X - m.X) + (s.Z - m.Z) * (s.Z - m.Z)) : 0f;
            bool jumping = s.Jumping || m.Jumping;
            bool teleported = m.HasLast && !jumping && d > Math.Max(TeleportDistance, 4f * s.Speed);
            m.HasLast = true; m.X = s.X; m.Y = s.Y; m.Z = s.Z;
            bool eligible = s.Cutscene && s.ScreenClear;
            bool seen = s.Rendered && s.OnScreen;
            if (teleported || !eligible) m.ResetEpisodes();
            bool moving = !teleported && d > 0.5f && (s.Locomotion == FieldLocomotion.Walk || s.Locomotion == FieldLocomotion.Run);
            bool run = s.Locomotion == FieldLocomotion.Run;
            if (eligible && !teleported)
            {
                // Into view: it was rendered and walking just outside the view, then crossed the edge.
                if (moving && s.Rendered && !s.OnScreen) { m.Offscreen++; m.Entering = false; }
                else if (moving && seen)
                {
                    if (!m.Entering) { if (m.Offscreen >= 2 && s.NearEdge) { m.Entering = true; m.Enter = m.EnterRun = 0; } else m.Offscreen = 0; }
                    if (m.Entering && ++m.Enter > 0) { if (run) m.EnterRun++; if (m.Enter >= 6) { EmitMotion(s, m.EnterRun * 2 > m.Enter ? FieldMotionKind.RunIntoView : FieldMotionKind.WalkIntoView); m.Entering = false; m.Offscreen = 0; } }
                }
                else { m.Offscreen = 0; m.Entering = false; }
                // Out of view: seen walking for a while, last seen at the edge, now just outside the view.
                if (moving && seen) { m.Visible++; if (run) m.VisibleRun++; m.LastNearEdge = s.NearEdge; }
                else if (moving && s.Rendered && !s.OnScreen)
                {
                    if (m.Visible >= 10 && m.LastNearEdge) EmitMotion(s, m.VisibleRun * 2 > m.Visible ? FieldMotionKind.RunOutOfView : FieldMotionKind.WalkOutOfView);
                    m.Visible = m.VisibleRun = 0; m.LastNearEdge = false;
                }
                else { m.Visible = m.VisibleRun = 0; m.LastNearEdge = false; }
            }
            // Jumps: native jump state, actual height change, visible from take-off to landing.
            if (s.Jumping && !m.Jumping) { m.Jumping = true; m.JumpY = previousY; m.JumpVisible = groundedVisible && seen && eligible && !teleported; }
            else if (s.Jumping) { if (!seen || !eligible) m.JumpVisible = false; }
            else if (m.Jumping)
            {
                m.Jumping = false;
                if (m.JumpVisible && seen && eligible)
                {
                    float dy = s.Y - m.JumpY;
                    EmitMotion(s, dy >= JumpHeight ? FieldMotionKind.JumpUp : dy <= -JumpHeight ? FieldMotionKind.JumpDown : FieldMotionKind.Jump);
                }
            }
            m.GroundedVisible=!s.Jumping && seen && eligible && !teleported;
            // Turns: native turning state with an actual heading change of at least 150 degrees.
            if (s.Turning && !m.Turning) { m.Turning = true; m.TurnYaw = s.Yaw; m.TurnVisible = seen && eligible; }
            else if (s.Turning) { if (!seen || !eligible) m.TurnVisible = false; }
            else if (m.Turning)
            {
                m.Turning = false;
                float delta = s.Yaw - m.TurnYaw;
                while (delta > 180f) delta -= 360f;
                while (delta < -180f) delta += 360f;
                if (m.TurnVisible && seen && eligible && Math.Abs(delta) >= TurnAroundDegrees) EmitMotion(s, FieldMotionKind.TurnAround);
            }
        }
        private void EmitMotion(FieldMotionSample s, FieldMotionKind kind)
        {
            var entry = FieldDescriptionCatalog.FindMotion(s.Model, kind);
            if (entry == null) return;
            string key = s.Actor + "|" + kind; long last;
            if (_lastMotion.TryGetValue(key, out last) && _frame - last < MotionRepeatFrames) return;
            _lastMotion[key] = _frame;
            _emit(new FieldDescriptionEmission(entry.CueId, entry.VoiceText, _generation, s.Actor, _actors[s.Actor].Token, "motion " + key));
        }

        // Once per engine frame with every live actor key: ages unproven poses and forgets
        // actors that were deleted or replaced.
        public void EndFrame(IEnumerable<long> liveActors)
        {
            _frame++;
            var live = new HashSet<long>(liveActors ?? new long[0]);
            EndPresentedFrame(live);
            var drop = new List<long>();
            foreach (var pair in _pending) if (!live.Contains(pair.Key) || ++pair.Value.Age > PendingLimitFrames) drop.Add(pair.Key);
            foreach (var actor in drop) _pending.Remove(actor);
            drop.Clear();
            foreach (var actor in _motion.Keys) if (!live.Contains(actor)) drop.Add(actor);
            foreach (var actor in drop) _motion.Remove(actor);
            drop.Clear();
            foreach(var actor in _actors.Keys)if(!live.Contains(actor))drop.Add(actor);
            foreach(var actor in drop)_actors.Remove(actor);
        }
    }
}
