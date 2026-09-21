using System;
using FSO.Common.Rendering.Framework.Model;
using FSO.Content;
using FSO.SimAntics;
using FSO.SimAntics.Model;
using FSO.Vitaboy;

namespace Simitone.Client.UI.Panels
{
    /// ROUND-212 'uivitaplay' (tools/iff-dump/r212/r212-vita-playback-law.md):
    /// the CAS Vita preview's idle PLAYBACK. cWinVitaBtnSolo::TSPaint 0x2db862
    /// calls AnimatePet 0x2dac20 every paint: the one-shot trigger byte
    /// (this+0x18c, armed by SetPerson 0x2db640's tail) seeds the cursor
    /// (this+0x210) to 0 and plays the cTSString slot at this+0x218[cursor];
    /// when the channel finishes (channel+0x3c) the cursor advances and wraps
    /// at the list count (this+0x214). The cycle is SEQUENTIAL — the repeated
    /// breathe entries ARE the engine's weights. The engine plays through its
    /// own per-paint channel (0x38dbb0), NOT the simulator, so this port
    /// advances a private VMAnimationState each frame (30 frames/sec, the FSO
    /// anim convention) and poses the avatar with Animator.RenderFrame; the
    /// avatar's own Animations list is left untouched (the CAS vm does not
    /// tick — InitializeLot ticks it exactly once). SetOutfit 0x2daf32 is a
    /// separate entry point: spinner changes do NOT re-arm the cycle; only a
    /// person change does (SetPerson semantics).
    public class UIOriginalVitaIdlePlayer
    {
        /// R258 (r258-law.md item 4; UI-31): the amplitude is SetAmplitude(
        /// gA[0]×gB[0]) from two STATIC code-section float pools, not BSS —
        /// gA file 0x5a4dc8 = 3.1415927410125732f (pi), gB file 0x5a4830 =
        /// {0.25f, 32767.0f, 0.5f, ...}; the multiply is single-float
        /// (lfs/fmuls @0x2dbc38..0x2dbc40, verified at Solo::Init 0x2DBC2C
        /// and cWinVitaBtn::Init 0x2DC5BC). Init amplitude = pi/4; the ctor
        /// sites (0x2DBD80/0x2DC700) compute gA[0]*gB[+8] = pi/2 for the
        /// initial rotation matrix. The r209 "BSS, statically unrecoverable"
        /// label and this port's old ±0.05 rad substitute are superseded.
        public const float EnginePiF = 3.1415927410125732f;  // gA[0]
        public const float EngineGB0 = 0.25f;                // gB[+0]
        public const float EngineGB8 = 0.5f;                 // gB[+8]
        public const float InitAmplitudeRad = EnginePiF * EngineGB0;  // pi/4 — SetAmplitude
        public const float CtorAmplitudeRad = EnginePiF * EngineGB8;  // pi/2 — ctor rotation

        /// Solo::UpdateTransform 0x2DB250: the sine path runs ONLY when
        /// this[0x209] != 0 AND this[0x208] != 0 (the dog and cat window
        /// flag bytes; lbz/cmplwi pair @0x2db26c..0x2db284). SetPerson
        /// 0x2DB738 writes stb 1 / stb 0 pairs — sets one flag, clears the
        /// other; the human child/adult branches leave both untouched — so
        /// the normal solo preview has NO idle sway (the plain-copy path
        /// uses the shared identity/base matrix). Both flags co-hold only
        /// after a cat->dog SetPerson transition on the same window.
        public byte FlagCat, FlagDog;   // engine this+0x208 / this+0x209

        public VMAvatar Avatar;      // for the CAS wiring + gate
        public bool Child, Male;
        private string[] Names;
        private VMAnimationState State;
        private int Cursor = -1;     // engine this+0x210 (-2 = stopped)
        private bool TriggerArmed;   // engine this+0x18c
        private double SwayMs;       // the SineGenerator's running clock

        public int Plays, Skips, Wraps, FramesAdvanced; // gate counters

        public int CursorForProbe { get { return Cursor; } }
        public string CurrentName
        {
            get { return (Names != null && Cursor >= 0 && Cursor < Names.Length) ? Names[Cursor] : null; }
        }
        public VMAnimationState StateForProbe { get { return State; } }
        public double ProbeSwayMs() { return SwayMs; }   // the SineGenerator clock, for the gate

        public UIOriginalVitaIdlePlayer(VMAvatar avatar, bool child, bool male)
        {
            Avatar = avatar;
            Child = child;
            Male = male;
            Names = Build(child, male);
            // SetPerson's tail (cursor -1 + trigger byte): the window is born
            // armed, exactly like an engine window that had SetPerson called.
            Cursor = -1;
            TriggerArmed = true;
        }

        /// Build{Adult,Child}AnimationList law: the adult list's slot [3] is
        /// the gender slot (person->0x60e == 0 male -> loop2, else loop1).
        public static string[] Build(bool child, bool male)
        {
            if (child) return (string[])UIOriginalVitaIdleLaw.ChildList.Clone();
            var a = (string[])UIOriginalVitaIdleLaw.AdultList.Clone();
            if (!male) a[3] = UIOriginalVitaIdleLaw.AdultGenderAlt;
            return a;
        }

        /// SineGenerator::GetVal 0x14E4D0: amplitude * sin(k*(t-t0)) — the
        /// pi/4 Init amplitude over the 10000 ms period (SetPeriod 10000
        /// @0x2dbc4c). Exposed for the gate.
        public static float SwayOffset(double elapsedMs)
        {
            return (float)(InitAmplitudeRad * Math.Sin(elapsedMs * 2.0 * Math.PI / UIOriginalVitaIdleLaw.SwayPeriodMs));
        }

        /// SetPerson 0x2db640 tail: pick, then cursor = -1 and the trigger
        /// byte = 1 — the next Update seeds and plays from the top.
        public void SetPerson(VMAvatar avatar, bool child, bool male)
        {
            Avatar = avatar;
            Child = child;
            Male = male;
            Names = Build(child, male);
            Cursor = -1;
            TriggerArmed = true;
        }

        /// The per-paint driver (TSPaint -> UpdateTransform sway, then
        /// AnimatePet). baseFacing: the CAS's static preview facing.
        public void Update(UpdateState state, float baseFacing)
        {
            if (Avatar == null) return;
            var ms = state.Time.ElapsedGameTime.TotalMilliseconds;
            SwayMs += ms;
            // Solo::UpdateTransform 0x2DB250 branch: gated sine path vs the
            // plain copy (the engine's shared identity/base matrix; the port
            // copies the CAS's static preview facing).
            Avatar.RadianDirection = (FlagCat != 0 && FlagDog != 0)
                ? baseFacing + SwayOffset(SwayMs)
                : baseFacing;
            Animate(ms);
        }

        private void Animate(double elapsedMs)
        {
            if (Cursor == -2) return;                       // AnimatePet's stopped guard
            if (TriggerArmed)
            {
                TriggerArmed = false;                       // this+0x18c clear-on-use
                if (Cursor == -1) Cursor = 0;               // reseed (cat's 8 is expansion pets)
                Start();
                return;
            }
            if (Cursor == -1 || State == null) return;      // idle, nothing playing
            State.CurrentFrame += 30f * (float)(elapsedMs / 1000.0);
            FramesAdvanced++;
            // RenderFrame is the real poser (bone translations/rotations) and
            // its return is the engine channel's finished flag (channel+0x3c).
            var status = Animator.RenderFrame(Avatar.Avatar, State.Anim, (int)State.CurrentFrame,
                State.CurrentFrame % 1f, 1f);
            if (status == AnimationStatus.IN_PROGRESS) return;
            Advance();                                      // channel+0x3c finished -> cursor++
            Start();
        }

        private void Advance()
        {
            Cursor++;
            if (Cursor > Names.Length - 1) { Cursor = 0; Wraps++; } // cmpw vs this+0x214
        }

        private void Start()
        {
            // the child builder's empty 13th slot (and any name the data
            // cannot resolve) is unplayable; the port skips forward — the
            // engine's failed channel is statically undecodable, disclosed.
            // (An empty cTSString is a terminator, not a reference: the raw
            // lookup alone would resolve a stray empty-named animation.)
            var guard = 0;
            while (guard++ <= Names.Length)
            {
                var anim = string.IsNullOrEmpty(Names[Cursor])
                    ? null : Content.Get().AvatarAnimations.Get(Names[Cursor] + ".anim");
                if (anim != null)
                {
                    State = new VMAnimationState(anim, false);
                    State.Loop = false;
                    Plays++;
                    Animator.RenderFrame(Avatar.Avatar, anim, 0, 0f, 1f);
                    return;
                }
                Skips++;
                Advance();
            }
            Cursor = -2; // entire list unresolvable: stop rather than spin
        }
    }
}
