using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Assets.Scripts.Common;
using Memoria.Prime;
using UnityEngine;
namespace Memoria.Accessibility.AudioDescriptions
{
    // Thin native bridge for field (in-engine) visual-action descriptions. It only reads engine
    // state the frame already computed; it never calls speech/audio APIs, never changes game
    // state and never searches assets per frame. Root subscribes Cue/SceneClosed.
    public static class NativeFieldDescriptions
    {
        // cueId, fixed reviewed voiceText, main-thread "observation still current" predicate.
        public static event Action<string, string, Func<bool>> Cue;
        public static event Action SceneClosed;

        private static FieldDescriptionTracker _tracker;
        private static bool _open, _focused = true, _blocked, _presentationBlocked, _poseVisibilityLogged, _motionVisibilityLogged;
        // FF9Char.attr bits set by CHRFX model codes (fldchar.FF9FieldCharDispatch): 0x10000 selects the
        // PSX/Actor_Abr_N blend shaders (50% alpha or additive), 0x100000 slices the model away (geoSlice).
        private const uint CharSemiTransparent = 0x10000, CharSliced = 0x100000;
        private static long _visibilityFrame = -1;
        private static readonly Dictionary<Actor, bool> _rendered = new Dictionary<Actor, bool>();
        private static readonly Dictionary<byte[], string> _hashes = new Dictionary<byte[], string>();
        private static readonly List<long> _live = new List<long>();

        public static void Initialize()
        {
            if (_tracker == null) _tracker = new FieldDescriptionTracker(Publish);
        }
        public static void Reset()
        {
            Initialize(); _tracker.ResetScene("reset"); _hashes.Clear(); _rendered.Clear();
        }

        // FieldMap.HonoAwake (FieldMap.cs, after navigation attach).
        public static void OnFieldOpened()
        {
            Guard(() => { Reset(); _open = true; _focused = HonoInputManager.ApplicationIsActivated(); _blocked = _presentationBlocked = false; _poseVisibilityLogged = _motionVisibilityLogged = false; });
        }
        // FieldMap.HonoOnDestroy, and ServiceEvents results that leave the field.
        public static void OnFieldClosed() { Guard(() => Close("field closed")); }
        // FieldMap.OnApplicationFocus.
        public static void OnFocusChanged(bool focused)
        {
            Guard(() => { _focused = focused; if (!focused && _tracker != null) _tracker.Invalidate("focus lost"); });
        }
        // The global update keeps running while HonoBehaviorSystem skips field
        // updates for pause/quit. Invalidate once at the boundary, before resume.
        public static void ObservePresentationBoundary()
        {
            if(_tracker==null || !_open)return;
            Guard(() => {
                bool blocked=!_focused || !HonoInputManager.ApplicationIsActivated()
                    || PersistenSingleton<UIManager>.Instance.IsPause
                    || PersistenSingleton<UIManager>.Instance.State!=UIManager.UIState.FieldHUD
                    || SceneDirector.IsScreenObscured || PersistenSingleton<SceneDirector>.Instance.IsFading
                    || (!MBG.IsNull && (MBG.Instance.IsPlaying() & 1UL)!=0 && !MBG.Instance.IsFinished());
                if(blocked && !_presentationBlocked)_tracker.Invalidate("presentation blocked");
                _presentationBlocked=blocked;
            });
        }

        // EventEngine.ExecAnim: every script-started clip (ANIM/DANIM, turn and jump clips).
        public static void OnAnimationStarted(EventEngine engine, Obj executing, int instruction, Actor actor, int anim)
        {
            if (_tracker == null || !_open || engine == null || actor == null || engine.gMode != FieldEngineFlags.ModeField) return;
            try
            {
                string clip;
                if (!FF9DBAll.AnimationDB.TryGetValue(anim, out clip)) clip = null;
                // Context-only pairs need their native site collected too; they must never
                // acquire a generic description merely to pass this eligibility check.
                bool reviewed = FieldDescriptionCatalog.CanReviewPose(actor.model, clip);
                var start = new FieldPoseStart { Actor = Key(actor), Model = actor.model, Clip = clip };
                if (reviewed)
                {
                    start.Site = Site(executing, instruction);
                    start.Cutscene = !engine.GetUserControl();
                    start.DialogOpen = Singleton<DialogManager>.Instance.Activate;
                    start.InFrame = actor.inFrame; start.OutFrame = actor.outFrame; start.FrameCount = actor.frameN;
                    start.Forward = actor.inFrame <= actor.outFrame && (actor.animFlag & FieldEngineFlags.AfDir) == 0;
                    // Engine jumps (AJUMP clip via PREJUMP/SETVY3/POSTJUMP) are observed as motion instead.
                    start.NonJump = anim != actor.jump && (actor.actf & FieldEngineFlags.ActJump) == 0;
                    // Unranged captions still require the whole forward clip. A contextual
                    // range must independently match these captured native facts exactly.
                    start.FullRange = actor.inFrame == 0 && actor.frameN >= 2 && actor.frameN != Byte.MaxValue && actor.outFrame >= actor.frameN - 1 && start.Forward && start.NonJump;
                    var context=DiscOneFieldDescriptionCatalog.Catalog.FindContextPose(actor.model,clip,start.Site);
                    var proof=context==null?null:context.Proof;
                    if(proof!=null) {
                        Vector3 position;start.PositionKnown=TryPosition(actor,out position);
                        start.X=position.x;start.Y=position.y;start.Z=position.z;
                        if(proof.HasCompanion) {
                            long companion;start.CompanionVisible=CompanionVisible(engine,actor,proof,ScreenClear(engine),out companion);
                            start.CompanionActor=companion;
                        }
                    }
                }
                string outcome = _tracker.StartPose(start);
                if (reviewed) Log.Message("[FF9Access] Field description {0}: map={1}; site={2}; uid={3}; model={4}; clip={5}; range={6}..{7}/{8}; forward={9}; nonJump={10}", outcome, FF9StateSystem.Common.FF9.fldMapNo, start.Site, actor.uid, actor.model, clip, start.InFrame, start.OutFrame, start.FrameCount, start.Forward, start.NonJump);
            }
            catch (Exception exception) { Fail(exception); }
        }

        // DoEventCode STARTSEQ, after the engine successfully constructed the Seq.
        // The executor supplies the exact script identity; actor is the engine's
        // current target, not a guessed SID lookup or a global sequence history.
        public static void OnSequenceStarted(EventEngine engine,Obj executing,int instruction,Actor actor,int sequenceId)
        {
            if(_tracker==null || _tracker.PendingCount==0 || !_open || engine==null || actor==null || engine.gMode!=FieldEngineFlags.ModeField)return;
            long key=Key(actor);var proof=_tracker.PendingProof(key);
            if(proof==null || !proof.HasSequence)return;
            try{_tracker.OnSequenceStarted(key,Site(executing,instruction),sequenceId);}
            catch(Exception exception){Fail(exception);}
        }

        // FieldMapActorController.PlayAnimationViaEventScript, right after the native clip was
        // sampled (clipPresent) or found missing. No object hashing/presentation work
        // without a reviewed candidate in this field or an existing pending proof.
        public static void OnClipSampled(Actor actor, string clip, bool clipPresent)
        {
            if (_tracker == null || !_open || actor == null) return;
            int map=FF9StateSystem.Common.FF9.fldMapNo;
            bool objectMap=_tracker.HasPresentedObjects(map);
            if(_tracker.PendingCount==0 && !objectMap && _tracker.PresentedPendingCount==0)return;
            long key = Key(actor);
            bool pose=_tracker.IsPending(key);
            bool presented=_tracker.IsPresentedPending(key) || objectMap && _tracker.CanReviewPresentedObject(map,actor.sid,actor.model,clip);
            if(!pose && !presented)return;
            try
            {
                var engine = PersistenSingleton<EventEngine>.Instance;
                if(engine.gMode!=FieldEngineFlags.ModeField)return;
                string objectHash=null;
                if(presented) {
                    objectHash=EntryHash(actor);
                    presented=_tracker.NeedsPresentedObjectSample(key,map,actor.sid,objectHash,actor.model,clip);
                }
                if(!pose && !presented)return;
                bool clear = ScreenClear(engine);
                bool onScreen, nearEdge;
                if(presented)_rendered.Remove(actor);
                bool rendered = Rendered(actor), projected = Project(engine, actor, out onScreen, out nearEdge);
                bool visible = clear && rendered && projected && onScreen;
                if (!_poseVisibilityLogged) { _poseVisibilityLogged = true; LogVisibility("pose", actor, rendered, projected, onScreen, clear); }
                if(pose) {
                    var sample=new FieldPoseSample { Actor=key,Clip=clip,AnimFrame=actor.animFrame,FrameCount=actor.frameN,ClipPresent=clipPresent,Visible=visible,ScreenClear=clear };
                    var proof=_tracker.PendingProof(key);
                    if(proof!=null) {
                        Vector3 position;sample.PositionKnown=TryPosition(actor,out position);
                        sample.X=position.x;sample.Y=position.y;sample.Z=position.z;
                        if(proof.HasCompanion) {
                            long companion;sample.CompanionVisible=CompanionVisible(engine,actor,proof,clear,out companion);
                            sample.CompanionActor=companion;
                        }
                    }
                    _tracker.SamplePose(sample);
                }
                if(presented) {
                    var sample=new FieldPresentedObjectSample {
                        Actor=key,PresentationFrame=Time.frameCount,Map=map,Sid=actor.sid,EntrySha256=objectHash,Model=actor.model,Clip=clip,
                        ClipPresent=clipPresent,Visible=visible,ScreenClear=clear,AnimFrame=actor.animFrame,FrameCount=actor.frameN,
                        InFrame=actor.inFrame,OutFrame=actor.outFrame,Speed=actor.speed
                    };
                    sample.Forward=actor.inFrame<=actor.outFrame && (actor.animFlag & FieldEngineFlags.AfDir)==0;
                    sample.NonJump=actor.anim!=actor.jump && (actor.actf & FieldEngineFlags.ActJump)==0;
                    sample.FullRange=actor.inFrame==0 && actor.frameN>=2 && actor.frameN!=Byte.MaxValue && actor.outFrame>=actor.frameN-1 && sample.Forward && sample.NonJump;
                    Vector3 position;sample.PositionKnown=TryPosition(actor,out position);sample.X=position.x;sample.Y=position.y;sample.Z=position.z;
                    _tracker.SamplePresentedObject(sample);
                }
            }
            catch (Exception exception) { Fail(exception); }
        }

        // HonoluluFieldMain.FF9FieldMapMain, immediately after ServiceEvents for this frame.
        public static void AfterFieldEvents(EventEngine engine, int result)
        {
            if (_tracker == null || !_open || engine == null) return;
            try
            {
                // 3 encounter, 4 field jump, 5 world map, 7 mini-game/ending path, 8 game over.
                if (result == 3 || result == 4 || result == 5 || result == 7 || result == 8) { Close("left field: " + result); return; }
                if (engine.gMode != FieldEngineFlags.ModeField) return;
                bool menu = PersistenSingleton<UIManager>.Instance.State != UIManager.UIState.FieldHUD;
                if (menu || !_focused) { if (!_blocked) { _blocked = true; _tracker.Invalidate(menu ? "menu" : "focus"); } }
                else _blocked = false;
                bool clear = ScreenClear(engine);
                bool cutscene = !engine.GetUserControl();
                _live.Clear();
                for (ObjList list = engine.GetActiveObjList(); list != null; list = list.next)
                {
                    var actor = list.obj as Actor;
                    if (actor == null || actor.cid != 4) continue;
                    long key = Key(actor); _live.Add(key);
                    _tracker.ObserveActor(key, actor.model);
                    if (_blocked || !FieldDescriptionCatalog.HasSubject(actor.model) || actor.fieldMapActorController == null) continue;
                    bool onScreen = false, nearEdge = false;
                    bool shown = Rendered(actor), projected = Project(engine, actor, out onScreen, out nearEdge);
                    bool rendered = shown && projected;
                    if (!_motionVisibilityLogged && cutscene) { _motionVisibilityLogged = true; LogVisibility("motion", actor, shown, projected, onScreen, clear); }
                    Vector3 position = actor.fieldMapActorController.curPos;
                    _tracker.ObserveMotion(new FieldMotionSample
                    {
                        Actor = key, Model = actor.model, Speed = actor.speed, X = position.x, Y = position.y, Z = position.z, Yaw = actor.rotAngle.y,
                        Locomotion = Locomotion(actor), Rendered = rendered, OnScreen = rendered && onScreen, NearEdge = nearEdge,
                        Jumping = (actor.actf & FieldEngineFlags.ActJump) != 0, Turning = (actor.flags & FieldEngineFlags.FlagTurn) != 0,
                        Cutscene = cutscene, ScreenClear = clear
                    });
                }
                _tracker.EndFrame(_live);
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void Publish(FieldDescriptionEmission emission)
        {
            // Main-thread check by the narration queue: same field visit, no invalidating boundary since,
            // and the field is still in front of the player (no menu, focus loss or other UI state).
            Func<bool> current = () => _open && _focused && !_presentationBlocked && _tracker != null && _tracker.IsCurrent(emission)
                && !PersistenSingleton<UIManager>.Instance.IsPause && PersistenSingleton<UIManager>.Instance.State == UIManager.UIState.FieldHUD;
            Log.Message("[FF9Access] Field description cue: {0}; {1}", emission.CueId, emission.Diagnostic);
            var handler = Cue;
            if (handler == null) return;
            try { handler(emission.CueId, emission.VoiceText, current); }
            catch (Exception exception) { Log.Message("[FF9Access] Field description subscriber failed: {0}", exception.Message); }
        }
        private static void Close(string reason)
        {
            bool wasOpen = _open; _open = false;
            if (_tracker != null) _tracker.ResetScene(reason);
            _hashes.Clear(); _rendered.Clear();
            if (!wasOpen) return;
            var handler = SceneClosed;
            if (handler != null) try { handler(); } catch (Exception exception) { Log.Message("[FF9Access] Field description close subscriber failed: {0}", exception.Message); }
        }

        // Engine's own locomotion choice (ProcessEvents: speed < speedth ? walk : run), and only when
        // that clip is actually loaded on the actor so it can be sampled.
        private static FieldLocomotion Locomotion(Actor actor)
        {
            if (actor.anim == actor.idle) return FieldLocomotion.Still;
            if (actor.anim != actor.walk && actor.anim != actor.run) return FieldLocomotion.Other;
            string clip; Animation animation = actor.go == null ? null : actor.go.GetComponent<Animation>();
            if (animation == null || !FF9DBAll.AnimationDB.TryGetValue(actor.anim, out clip) || animation.GetClip(clip) == null) return FieldLocomotion.Other;
            return actor.anim == actor.run && actor.run != actor.walk ? FieldLocomotion.Run : FieldLocomotion.Walk;
        }
        private static bool ScreenClear(EventEngine engine)
        {
            if (engine.gMode != FieldEngineFlags.ModeField || !_focused || _blocked || PersistenSingleton<UIManager>.Instance.IsPause) return false;
            if (SceneDirector.IsScreenObscured || PersistenSingleton<SceneDirector>.Instance.IsFading) return false;
            if (!MBG.IsNull && (MBG.Instance.IsPlaying() & 1UL) != 0 && !MBG.Instance.IsFinished()) return false;
            return PersistenSingleton<UIManager>.Instance.State == UIManager.UIState.FieldHUD;
        }
        // Mirrors EventEngine.SetRenderer: show flag, front of camera, an enabled field mesh renderer
        // (never the shadow or hidden battle_model meshes). Cached once per Unity frame.
        private static bool Rendered(Actor actor)
        {
            if (_visibilityFrame != Time.frameCount) { _rendered.Clear(); _visibilityFrame = Time.frameCount; }
            bool result;
            if (_rendered.TryGetValue(actor, out result)) return result;
            result = false;
            if (actor.go != null && actor.go.activeInHierarchy && (actor.flags & FieldEngineFlags.FlagShow) != 0 && actor.frontCamera && actor.model != UInt16.MaxValue && !BlendedAway(actor))
            {
                Renderer shadow = actor.fieldMapActor == null ? null : actor.fieldMapActor.shadowMeshRenderer;
                foreach (Renderer renderer in actor.go.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled || renderer == shadow || UnderBattleModel(renderer.transform, actor.go.transform)) continue;
                    result = true; break;
                }
            }
            _rendered[actor] = result;
            return result;
        }
        // Show and renderer flags stay set while a model is drawn semi-transparent or sliced, so those
        // states are treated as not reliably visible rather than guessed.
        private static bool BlendedAway(Actor actor)
        {
            var characters = FF9StateSystem.Common.FF9.charArray; FF9Char character;
            return characters != null && characters.TryGetValue(actor.uid, out character) && character != null && (character.attr & (CharSemiTransparent | CharSliced)) != 0;
        }
        // One concise line per field visit and kind, for live validation of the visibility gates.
        private static void LogVisibility(string kind, Actor actor, bool rendered, bool projected, bool onScreen, bool clear)
        {
            Log.Message("[FF9Access] Field description first {0} visibility: map={1}; uid={2}; model={3}; rendered={4}; projected={5}; onScreen={6}; screenClear={7}; obscured={8}; fading={9}; blended={10}; ui={11}",
                kind, FF9StateSystem.Common.FF9.fldMapNo, actor.uid, actor.model, rendered, projected, onScreen, clear, SceneDirector.IsScreenObscured,
                PersistenSingleton<SceneDirector>.Instance.IsFading, BlendedAway(actor), PersistenSingleton<UIManager>.Instance.State);
        }
        private static bool UnderBattleModel(Transform node, Transform root)
        {
            for (; node != null && node != root; node = node.parent) if (node.name == "battle_model") return true;
            return false;
        }
        private static bool Project(EventEngine engine, Actor actor, out bool onScreen, out bool nearEdge)
        {
            onScreen = nearEdge = false;
            FieldMap field = engine.fieldmap;
            if (field == null || actor.fieldMapActorController == null) return false;
            Camera camera = field.GetMainCamera(); BGCAM_DEF bg = field.GetCurrentBgCamera();
            if (camera == null || bg == null) return false;
            Vector3 projected = PSX.CalculateGTE_RTPT(actor.fieldMapActorController.curPos, Matrix4x4.identity, bg.GetMatrixRT(), bg.GetViewDistance(), field.GetProjectionOffset());
            Vector3 screen = camera.WorldToScreenPoint(projected);
            float w = Screen.width, h = Screen.height;
            onScreen = screen.x >= 0f && screen.x <= w && screen.y >= 0f && screen.y <= h;
            nearEdge = screen.x < 0.12f * w || screen.x > 0.88f * w || screen.y < 0.12f * h || screen.y > 0.88f * h;
            return true;
        }
        private static long Key(Actor actor) { return ((long)actor.uid << 32) | (uint)RuntimeHelpers.GetHashCode(actor); }
        private static bool TryPosition(Actor actor,out Vector3 position)
        {
            position=Vector3.zero;
            if(actor.fieldMapActorController==null)return false;
            position=actor.fieldMapActorController.curPos;
            return !Single.IsNaN(position.x) && !Single.IsInfinity(position.x) && !Single.IsNaN(position.y) && !Single.IsInfinity(position.y) && !Single.IsNaN(position.z) && !Single.IsInfinity(position.z);
        }
        private static bool CompanionVisible(EventEngine engine,Actor owner,FieldPoseProof proof,bool clear,out long key)
        {
            key=0;Actor companion=null;
            // Called only for an explicitly authored companion proof while pending.
            // Exactly one active model/SID match is required; ambiguity stays silent.
            for(ObjList list=engine.GetActiveObjList();list!=null;list=list.next) {
                var actor=list.obj as Actor;
                if(actor==null || actor.cid!=4 || actor.sid!=proof.CompanionSid || actor.model!=proof.CompanionModel)continue;
                if(companion!=null)return false;
                companion=actor;
            }
            if(companion==null)return false;
            bool actualParentMatches=false;
            if(proof.RequiresAttachment && owner.go!=null && companion.go!=null) {
                Transform parent=companion.go.transform.parent;
                actualParentMatches=parent!=null && parent.name==proof.AttachmentBoneName && parent.IsChildOf(owner.go.transform);
            }
            if(!proof.AcceptsCompanionAttachment(owner.uid,companion.attatchTargetUid,companion.attachTargetBoneIndex,actualParentMatches))return false;
            key=Key(companion);bool onScreen,nearEdge;
            // A required companion is sampled freshly: an earlier animation in
            // this Unity frame must not mask a later renderer/show change.
            _rendered.Remove(companion);
            return clear && Rendered(companion) && Project(engine,companion,out onScreen,out nearEdge) && onScreen;
        }
        private static string Site(Obj executing, int instruction)
        {
            string hash=EntryHash(executing);
            if(hash==null)return "unknown";
            return FF9StateSystem.Common.FF9.fldMapNo + ":" + executing.sid + ":" + hash + ":" + instruction;
        }
        private static string EntryHash(Obj actor)
        {
            if(actor==null || actor.ebData==null)return null;
            string hash;
            if(!_hashes.TryGetValue(actor.ebData,out hash))
                using(var sha=SHA256.Create())_hashes[actor.ebData]=hash=BitConverter.ToString(sha.ComputeHash(actor.ebData)).Replace("-", "").ToLowerInvariant();
            return hash;
        }
        private static void Guard(Action action) { try { Initialize(); action(); } catch (Exception exception) { Fail(exception); } }
        private static void Fail(Exception exception)
        {
            // A failed observation must never reach native game code; drop everything pending.
            try { if (_tracker != null) _tracker.Invalidate("observer failure"); Log.Message("[FF9Access] Field description observer recovered: {0}", exception.Message); } catch { }
        }
    }
}
