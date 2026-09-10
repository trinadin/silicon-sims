using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Content;
using FSO.SimAntics.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using Simitone.Client.UI.Screens;

namespace Simitone.Client.UI.Controls
{
    public class UIValueBar : UIElement
    {
        public Texture2D BarBase;
        public int Width;
        public float Value;

        public UIValueBar (Texture2D tex)
        {
            BarBase = tex;
        }

        public void DrawSlice(UISpriteBatch batch, int width, Color col, int drawFrom)
        {
            DrawSlice(batch, BarBase, width, col, drawFrom);
        }

        public void DrawSlice(UISpriteBatch batch, Texture2D tex, int width, Color col, int drawFrom)
        {
            var w = tex.Width / 3;
            if (width < w * 2)
            {
                if (drawFrom <= 0) DrawLocalTexture(batch, tex, new Rectangle(0, 0, width / 2, tex.Height), Vector2.Zero, Vector2.One, col);
                DrawLocalTexture(batch, tex, new Rectangle(tex.Width-((width + 1) / 2), 0, (width+1) / 2, tex.Height), new Vector2(width/2, 0), Vector2.One, col);
            } else
            {
                if (drawFrom <= 0) DrawLocalTexture(batch, tex, new Rectangle(0, 0, w, tex.Height), Vector2.Zero, Vector2.One, col);
                if (drawFrom <= 1) DrawLocalTexture(batch, tex, new Rectangle(w, 0, w, tex.Height), new Vector2(w, 0), new Vector2((float)(width-2*w)/w, 1), col);
                DrawLocalTexture(batch, tex, new Rectangle(w*2, 0, w, tex.Height), new Vector2(width - w, 0), Vector2.One, col);
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            var p = Value;
            Color barcol = new Color((byte)(57 * (1 - p)), (byte)(213 * p + 97 * (1 - p)), (byte)(49 * p + 90 * (1 - p)));
            Color bgcol = new Color((byte)(57 * p + 214 * (1 - p)), (byte)(97 * p), (byte)(90 * p));

            DrawSlice(batch, Width, bgcol, 0);

            var activeWidth = (int)Math.Round(p * Width);
            DrawSlice(batch, activeWidth, Color.White, 2);
            DrawSlice(batch, activeWidth-2, barcol, 0);
        }
    }

    /// <summary>
    /// R184/R187: one cWinMotive gauge (cWinMotive::Init/TSPaint). The control is
    /// 100x20, but its bar is only (20,12)-(80,17): a 60x5 active/remainder
    /// strip with a black final active column. The caption is the native
    /// font[8] type-4 button caption, centered in the full 100px text rect.
    /// Earlier rounds incorrectly painted a dark 100x20 plate and a 20px-high
    /// value ramp, which was the large repeated block visible behind Needs.
    /// R187 adds the owned cAverageHistory(10,5) and cWinDeltaMeter state:
    /// exact five-slot masks/lanes, 500ms target ramps, and CPState cadence.
    /// </summary>
    public class UIOriginalMotiveGauge : UIContainer
    {
        public const int GAUGE_W = 100, GAUGE_H = 20;
        public const int BAR_X = 20, BAR_Y = 12, BAR_W = 60, BAR_H = 5;
        public const int FILL_MAX = BAR_W;
        public const int BAR_SENTINEL_RAMP_MS = 500;
        public const int DELTA_SLOTS = 5, DELTA_RAMP_MS = 500;
        public static readonly Color NativeCaptionColor = new Color(0xC3, 0xCD, 0xCD, 0xFF);
        public static readonly Color NativeBarRemainderLow = new Color(0x00, 0xD1, 0x00, 0xFF);
        public static readonly Color NativeBarRemainderHigh = new Color(0x5D, 0x40, 0x5F, 0xFF);
        public static readonly Color NativeBarActiveLow = new Color(0x40, 0x5D, 0x5F, 0xFF);
        public static readonly Color NativeBarActiveHigh = new Color(0x00, 0xCA, 0x39, 0xFF);
        public static readonly Color NativeBarRedSentinel = new Color(0xFF, 0x00, 0x00, 0xFF);
        public static readonly Color NativeBarGreenSentinel = new Color(0x00, 0xFF, 0x00, 0xFF);
        public static readonly Color NativeDeltaHidden = new Color(0x00, 0x00, 0x52, 0xFF);
        public static readonly Color NativeDeltaGreen = new Color(0x00, 0xFF, 0x00, 0xFF);
        public static readonly Color NativeDeltaGreenMuted = new Color(0x00, 0xCA, 0x39, 0xFF);
        public static readonly Color NativeDeltaRed = new Color(0xFF, 0x00, 0x00, 0xFF);
        public static readonly Color NativeDeltaRedMuted = new Color(0xD1, 0x00, 0x00, 0xFF);

        // cWinMotive's static motive-index table. The history bank pumps all
        // eight rows as one CPState batch, even though each gauge paints only
        // the history currently bound to it.
        private static readonly VMMotive[] NativeMotiveOrder =
        {
            VMMotive.Hunger, VMMotive.Energy, VMMotive.Comfort, VMMotive.Fun,
            VMMotive.Hygiene, VMMotive.Social, VMMotive.Bladder, VMMotive.Room
        };

        private static readonly string[] NativeLeftArrowMask =
        {
            "..#", ".##", "###", ".##", "..#"
        };

        private static readonly string[] NativeRightArrowMask =
        {
            "#..", "##.", "###", "##.", "#.."
        };

        /// <summary>
        /// Exact cAverageHistory(10,5) port. Native retains both the ten raw
        /// records and the ten rounded rolling means; the displayed delta is
        /// the newest retained mean minus the oldest retained mean.
        /// </summary>
        private sealed class NativeAverageHistory
        {
            private readonly List<int> Raw = new List<int>(10);
            private readonly List<int> Means = new List<int>(10);
            private int Sum;

            public int AddSample(int sample)
            {
                if (Raw.Count == 10)
                {
                    Sum -= Raw[0];
                    Raw.RemoveAt(0);
                    Means.RemoveAt(0);
                }
                Raw.Add(sample);
                Sum += sample;
                Means.Add(NativeRound((float)Sum / Raw.Count));
                return Math.Max(-5, Math.Min(5, Means[Means.Count - 1] - Means[0]));
            }
        }

        private sealed class NativeMotivePump
        {
            public readonly int[][] Deltas = new int[NativeMotiveOrder.Length][];
        }

        /// <summary>
        /// One native cWinPeople lifetime worth of histories. The original
        /// cWinMotive vectors are positional (house-list index), grow but never
        /// shrink, and survive switching among the seven People subpanels.
        /// A weak screen key gives the port the same lifetime without keeping a
        /// departed lot alive.
        /// </summary>
        private sealed class NativeMotiveHistoryBank
        {
            private readonly List<NativeAverageHistory>[] Histories =
                new List<NativeAverageHistory>[NativeMotiveOrder.Length];
            public readonly List<NativeMotivePump> Pumps = new List<NativeMotivePump>();
            public FSO.SimAntics.VMAvatar[] People = Array.Empty<FSO.SimAntics.VMAvatar>();
            public long FrameSerial;
            public double NowMilliseconds;

            private bool ClockStarted;
            private bool PulsePrimed;
            private long LastFrameTicks = long.MinValue;
            private double LastMilliseconds;
            private double AccumulatedMilliseconds;
            private FSO.Common.Utils.UpdateHook PumpHook;
            private FSO.SimAntics.VM OwnerVM;

            public NativeMotiveHistoryBank(TS1GameScreen screen)
            {
                for (int i = 0; i < Histories.Length; i++)
                    Histories[i] = new List<NativeAverageHistory>();

                // cWinPeople pumps motives even while another standard People
                // panel is selected. Keep that cadence after the visible Mood
                // host has been removed; a weak screen reference plus the
                // current-screen check prevents this update hook from extending
                // the departed lot's lifetime.
                var weakScreen = new WeakReference<TS1GameScreen>(screen);
                PumpHook = FSO.Common.Utils.GameThread.EveryUpdate(state =>
                {
                    TS1GameScreen liveScreen;
                    if (!weakScreen.TryGetTarget(out liveScreen)
                        || !object.ReferenceEquals(GameFacade.Screens?.CurrentUIScreen, liveScreen))
                    {
                        People = Array.Empty<FSO.SimAntics.VMAvatar>();
                        PumpHook.Remove();
                        return;
                    }
                    Advance(state, liveScreen);
                });
            }

            public void Advance(UpdateState state, TS1GameScreen screen)
            {
                if (state == null || state.Time == null || screen == null || screen.vm == null)
                {
                    People = CurrentPeople(screen);
                    return;
                }

                if (!object.ReferenceEquals(OwnerVM, screen.vm))
                    ResetForVM(screen.vm);

                var ticks = state.Time.TotalGameTime.Ticks;
                if (ticks == LastFrameTicks) return; // the other seven gauges share this batch
                LastFrameTicks = ticks;
                People = CurrentPeople(screen);
                FrameSerial++;
                Pumps.Clear();
                // TimeBase_Sims::Now is integer-valued; truncate the framework
                // clock rather than feeding sub-millisecond fractions to ramps.
                NowMilliseconds = (long)state.Time.TotalGameTime.TotalMilliseconds;

                var speed = screen.vm.SpeedMultiplier;
                if (!ClockStarted)
                {
                    ClockStarted = true;
                    LastMilliseconds = NowMilliseconds;
                }
                var elapsed = Math.Max(0, NowMilliseconds - LastMilliseconds);
                LastMilliseconds = NowMilliseconds;

                // Simitone's UI wall clock continues while the VM is paused or
                // in build/buy. Preserve the pulse remainder, but move this
                // bridge's baseline so unpausing cannot synthesize a wall-clock
                // catch-up that was never simulation time.
                if (speed <= 0) return;

                var period = NativePulsePeriodForMultiplierProbe(speed);
                if (!PulsePrimed)
                {
                    // PulseGenerator::AdvanceTime primes its first call with one
                    // complete period, yielding the native immediate first pump.
                    PulsePrimed = true;
                    AccumulatedMilliseconds = period;
                }
                else AccumulatedMilliseconds += elapsed;

                while (AccumulatedMilliseconds >= period)
                {
                    AccumulatedMilliseconds -= period;
                    Pump();
                }
            }

            private void ResetForVM(FSO.SimAntics.VM vm)
            {
                OwnerVM = vm;
                for (int i = 0; i < Histories.Length; i++) Histories[i].Clear();
                People = Array.Empty<FSO.SimAntics.VMAvatar>();
                Pumps.Clear();
                FrameSerial++;
                NowMilliseconds = 0;
                ClockStarted = false;
                PulsePrimed = false;
                LastFrameTicks = long.MinValue;
                LastMilliseconds = 0;
                AccumulatedMilliseconds = 0;
            }

            private static FSO.SimAntics.VMAvatar[] CurrentPeople(TS1GameScreen screen)
            {
                try
                {
                    return screen?.vm?.Context?.ObjectQueries?.Avatars?
                        .OfType<FSO.SimAntics.VMAvatar>().ToArray()
                        ?? Array.Empty<FSO.SimAntics.VMAvatar>();
                }
                catch { return Array.Empty<FSO.SimAntics.VMAvatar>(); }
            }

            private void Pump()
            {
                var result = new NativeMotivePump();
                for (int motive = 0; motive < NativeMotiveOrder.Length; motive++)
                {
                    var histories = Histories[motive];
                    while (histories.Count < People.Length)
                        histories.Add(new NativeAverageHistory());

                    var deltas = new int[People.Length];
                    for (int person = 0; person < People.Length; person++)
                    {
                        // The port stores motive data as an integer short. This
                        // is already the result of native roundAway(float).
                        var sample = People[person].GetMotiveData(NativeMotiveOrder[motive]);
                        deltas[person] = histories[person].AddSample(sample);
                    }
                    result.Deltas[motive] = deltas;
                }
                Pumps.Add(result);
            }
        }

        private sealed class NativeColorFader
        {
            private Color From = NativeDeltaHidden;
            private Color To = NativeDeltaHidden;
            private double Start;
            private double End;

            public void Set(Color from, Color to, double now, double duration)
            {
                From = from;
                To = to;
                Start = now;
                End = now + duration;
            }

            public void SetFromCurrent(Color to, double now, double duration)
            {
                Set(Current(now), to, now, duration);
            }

            public bool Done(double now) { return now >= End; }

            public Color Current(double now)
            {
                if (now >= End || End <= Start) return To;
                var amount = (now - Start) / (End - Start);
                if (amount < 0) amount = 0;
                else if (amount > 1) amount = 1;
                return new Color(
                    NativeColorChannel(From.R, To.R, amount),
                    NativeColorChannel(From.G, To.G, amount),
                    NativeColorChannel(From.B, To.B, amount), (byte)0xFF);
            }
        }

        /// <summary>
        /// cWinMotive stores each bar colour as start vec3, target vec3,
        /// target-minus-start vec3, and a RampGenerator. Normal SetVal calls
        /// arm a zero-duration 0..1 ramp; TSPaint's otherwise dormant green
        /// sentinel path is the sole 500ms bar-colour transition.
        /// </summary>
        private sealed class NativeBarColorRamp
        {
            private float Red;
            private float Green;
            private float Blue;
            private float DeltaRed;
            private float DeltaGreen;
            private float DeltaBlue;
            private float TargetAmount;
            private float Slope;
            private double EndMilliseconds;

            public void Set(Color from, Color to, double now, int duration)
            {
                Red = from.R;
                Green = from.G;
                Blue = from.B;
                DeltaRed = to.R - from.R;
                DeltaGreen = to.G - from.G;
                DeltaBlue = to.B - from.B;
                TargetAmount = 1f;
                Slope = duration == 0 ? 0f : 1f / duration;
                EndMilliseconds = now + duration;
            }

            public bool Done(double now) { return now >= EndMilliseconds; }

            public Color Current(double now)
            {
                float amount;
                if (now >= EndMilliseconds) amount = TargetAmount;
                else amount = MathF.FusedMultiplyAdd(
                    -(float)(EndMilliseconds - now), Slope, TargetAmount);
                // Native executes vec scalar-multiply and vec addition as two
                // separate single-precision helpers (fmuls, then fadds).
                var red = NativeBarAdd(Red, NativeBarMultiply(DeltaRed, amount));
                var green = NativeBarAdd(Green, NativeBarMultiply(DeltaGreen, amount));
                var blue = NativeBarAdd(Blue, NativeBarMultiply(DeltaBlue, amount));
                return new Color(
                    NativeBarChannel(red), NativeBarChannel(green),
                    NativeBarChannel(blue), (byte)0xFF);
            }
        }

        private struct NativeBarPaint
        {
            public Color Active;
            public Color Remainder;
        }

        private static readonly ConditionalWeakTable<TS1GameScreen, NativeMotiveHistoryBank>
            NativeHistoryBanks = new ConditionalWeakTable<TS1GameScreen, NativeMotiveHistoryBank>();

        public readonly VMMotive Motive;
        private short _RawValue;               // engine raw [-100,100]
        public short RawValue
        {
            get { return _RawValue; }
            set
            {
                if (_RawValue == value && NativeBarInitialized) return;
                _RawValue = value;
                SetNativeBarValue(value, NativeNowMilliseconds);
                Invalidate();
            }
        }
        public UIOriginalText Label;
        public Action<UIOriginalMotiveGauge> Activated;
        private bool Hover;
        private bool Down;
        private Texture2D Pixel;
        private readonly NativeColorFader[] DeltaFaders = new NativeColorFader[DELTA_SLOTS];
        private bool DeltaActive;
        private bool DeltaStopAfterFades;
        private int DeltaValue;
        private int BoundHistoryIndex = int.MinValue;
        private FSO.SimAntics.VMAvatar BoundPerson;
        private long LastPumpFrame = long.MinValue;
        private double NativeNowMilliseconds;
        private readonly NativeBarColorRamp NativeRemainderRamp = new NativeBarColorRamp();
        private readonly NativeBarColorRamp NativeActiveRamp = new NativeBarColorRamp();
        private bool NativeBarInitialized;

        private Vector2 _Size = new Vector2(GAUGE_W, GAUGE_H);
        public override Vector2 Size
        {
            get { return _Size; }
            set { _Size = value; }
        }

        public UIOriginalMotiveGauge(VMMotive motive,
            Action<UIOriginalMotiveGauge> activated = null)
        {
            Motive = motive;
            Activated = activated;
            for (int i = 0; i < DeltaFaders.Length; i++)
            {
                DeltaFaders[i] = new NativeColorFader();
                DeltaFaders[i].Set(NativeDeltaHidden, NativeDeltaHidden, 0, 0);
            }
            ResetNativeBarColors(0, 0);
            // Native popup/hit law covers the complete motive button. This
            // registration also lets the shared quick-tip path see Tooltip.
            ListenForMouse(new Rectangle(0, 0, GAUGE_W, GAUGE_H), MouseEvent);
            FSO.Client.Utils.UIUtils.GiveTooltip(this);
        }

        /// <summary>
        /// Native cTSWinBtn state handling. The parent command is issued on
        /// mouse-down; motive buttons are read-only and only select/toggle the
        /// shared cWinLivePopup client.
        /// </summary>
        internal void MouseEvent(UIMouseEventType type, UpdateState state)
        {
            if (type == UIMouseEventType.MouseOver) Hover = true;
            else if (type == UIMouseEventType.MouseOut)
            {
                Hover = false;
                Down = false;
            }
            else if (type == UIMouseEventType.MouseDown)
            {
                Down = true;
                Activated?.Invoke(this);
            }
            else if (type == UIMouseEventType.MouseUp) Down = false;
            ApplyCaptionState();
            Invalidate();
        }

        private void ApplyCaptionState()
        {
            if (Label == null) return;
            Label.Color = Down ? new Color(0x00, 0xFF, 0xFF, 0xFF)
                : Hover ? Color.White : NativeCaptionColor;
        }

        private static int NativeRound(float value)
        {
            return value >= 0 ? (int)(value + 0.5f) : (int)(value - 0.5f);
        }

        private static byte NativeColorChannel(byte from, byte to, double amount)
        {
            return (byte)(from + (to - from) * amount + 0.5);
        }

        private static byte NativeBarChannel(float value)
        {
            return (byte)(int)(0.5f + value);
        }

        private static float NativeBarMultiply(float left, float right)
        {
            return (float)((double)left * right);
        }

        private static float NativeBarAdd(float left, float right)
        {
            return (float)((double)left + right);
        }

        /// <summary>
        /// blend_sims(start,end,factor): quantized byte interpolation with a
        /// positive half-up result. cWinMotive deliberately quantizes the
        /// magnitude to a byte before blending each RGB channel.
        /// </summary>
        private static byte NativeBarBlend(byte start, byte end, byte factor)
        {
            var amount = NativeBarMultiply(1f / 255f, factor);
            return NativeBarChannel(MathF.FusedMultiplyAdd(amount, end - start, start));
        }

        private static byte NativeBarFactor(float magnitude)
        {
            if (magnitude < 0f) magnitude = 0f;
            else if (magnitude > 1f) magnitude = 1f;
            return (byte)(int)(0.5f + NativeBarMultiply(255f, magnitude));
        }

        private static NativeBarPaint NativeSettledBarColors(int rawValue)
        {
            var greenMagnitude = (rawValue + 50) / 100f;
            if (greenMagnitude < 0f) greenMagnitude = 0f;
            else if (greenMagnitude > 1f) greenMagnitude = 1f;

            var remainderFactor = NativeBarFactor(1f - greenMagnitude);
            var activeFactor = NativeBarFactor(greenMagnitude);
            return new NativeBarPaint
            {
                Remainder = new Color(
                    NativeBarBlend(0x5D, 0x00, remainderFactor),
                    NativeBarBlend(0x40, 0xD1, remainderFactor),
                    NativeBarBlend(0x5F, 0x00, remainderFactor), (byte)0xFF),
                Active = new Color(
                    NativeBarBlend(0x40, 0x00, activeFactor),
                    NativeBarBlend(0x5D, 0xCA, activeFactor),
                    NativeBarBlend(0x5F, 0x39, activeFactor), (byte)0xFF)
            };
        }

        private void ResetNativeBarColors(int rawValue, double now)
        {
            var colors = NativeSettledBarColors(rawValue);
            NativeRemainderRamp.Set(colors.Remainder, colors.Remainder, now, 0);
            NativeActiveRamp.Set(colors.Active, colors.Active, now, 0);
            NativeBarInitialized = true;
        }

        private void SetNativeBarValue(int rawValue, double now)
        {
            // Both native SetVal sign branches install the same two settled
            // vectors and arm duration-zero ramps. The branch duplication in
            // the PPC body does not encode a rise/fall colour transition.
            ResetNativeBarColors(rawValue, now);
        }

        private NativeBarPaint PaintNativeBar(double now)
        {
            // TSPaint samples each colour before it tests/re-arms its sentinel,
            // so a sentinel itself is visible for this paint.
            var result = new NativeBarPaint
            {
                Remainder = NativeRemainderRamp.Current(now),
                Active = NativeActiveRamp.Current(now)
            };

            if (NativeRemainderRamp.Done(now) && result.Remainder == NativeBarRedSentinel)
            {
                var settled = NativeSettledBarColors(_RawValue).Remainder;
                NativeRemainderRamp.Set(settled, settled, now, 0);
            }
            if (NativeActiveRamp.Done(now) && result.Active == NativeBarGreenSentinel)
            {
                var settled = NativeSettledBarColors(_RawValue).Active;
                NativeActiveRamp.Set(NativeBarGreenSentinel, settled, now, BAR_SENTINEL_RAMP_MS);
            }

            if (!NativeRemainderRamp.Done(now) || !NativeActiveRamp.Done(now)) Invalidate();
            return result;
        }

        private static int NativeMotiveSlot(VMMotive motive)
        {
            for (int i = 0; i < NativeMotiveOrder.Length; i++)
                if (NativeMotiveOrder[i] == motive) return i;
            return -1;
        }

        /// <summary>Deterministic probe for the recovered round-away rule.</summary>
        public static int NativeRoundForProbe(float value) { return NativeRound(value); }

        /// <summary>
        /// Returns [active, remainder] after native magnitude-byte
        /// quantization and blend_sims channel rounding.
        /// </summary>
        public static Color[] NativeSettledBarColorsForProbe(int rawValue)
        {
            var colors = NativeSettledBarColors(rawValue);
            return new[] { colors.Active, colors.Remainder };
        }

        /// <summary>
        /// Installs TSPaint's otherwise dormant exact-primary sentinel. This
        /// makes its one-frame red repair and 500ms green fade independently
        /// testable without claiming normal SetVal can create either state.
        /// </summary>
        public void NativePrimeBarSentinelForProbe(bool active, double now)
        {
            if (active)
                NativeActiveRamp.Set(NativeBarGreenSentinel, NativeBarGreenSentinel, now, 0);
            else
                NativeRemainderRamp.Set(NativeBarRedSentinel, NativeBarRedSentinel, now, 0);
        }

        /// <summary>Returns [active, remainder] for one native paint evaluation.</summary>
        public Color[] NativePaintBarForProbe(double now)
        {
            var colors = PaintNativeBar(now);
            return new[] { colors.Active, colors.Remainder };
        }

        /// <summary>
        /// Returns the delta after each supplied cAverageHistory sample. This
        /// pins startup windows as well as the steady overlapping-window law.
        /// </summary>
        public static int[] NativeAverageDeltasForProbe(params int[] samples)
        {
            var history = new NativeAverageHistory();
            var result = new int[samples?.Length ?? 0];
            for (int i = 0; i < result.Length; i++) result[i] = history.AddSample(samples[i]);
            return result;
        }

        /// <summary>Exact CPState period formula for a native 0..1000 speed code.</summary>
        public static int NativePulsePeriodFromSpeedCodeForProbe(int speedCode)
        {
            var x = (uint)(0.5f + (172f - (speedCode / 1000f) * 171f));
            return (int)(uint)(0.5f + 1000f * (x / 42f));
        }

        /// <summary>Bridge from Simitone VM multipliers to the shipped native speeds.</summary>
        public static int NativePulsePeriodForMultiplierProbe(int speedMultiplier)
        {
            switch (speedMultiplier)
            {
                case 10: return NativePulsePeriodFromSpeedCodeForProbe(994); // ultra
                case 3: return NativePulsePeriodFromSpeedCodeForProbe(924);  // fast
                default: return NativePulsePeriodFromSpeedCodeForProbe(760); // normal
            }
        }

        /// <summary>
        /// Slot zero is nearest the 60px bar; later slots move four pixels
        /// outward. Positive deltas use the right lane, negative the left.
        /// </summary>
        public static Point NativeArrowPointForProbe(bool positive, int slot)
        {
            if (slot < 0 || slot >= DELTA_SLOTS) throw new ArgumentOutOfRangeException(nameof(slot));
            return positive ? new Point(81 + slot * 4, 12) : new Point(16 - slot * 4, 12);
        }

        public static bool NativeArrowMaskPixelForProbe(bool positive, int x, int y)
        {
            if (x < 0 || x >= 3 || y < 0 || y >= 5) return false;
            var rows = positive ? NativeRightArrowMask : NativeLeftArrowMask;
            return rows[y][x] == '#';
        }

        public bool NativeDeltaActiveForProbe { get { return DeltaActive; } }
        public bool NativeDeltaStopForProbe { get { return DeltaStopAfterFades; } }
        public int NativeDeltaValueForProbe { get { return DeltaValue; } }
        public int NativeBoundHistoryIndexForProbe { get { return BoundHistoryIndex; } }

        public void NativeResetDeltaForProbe(bool bound, double now = 0)
        {
            ResetNativeDeltaMeter(bound, now);
        }

        private void ResetNativeDeltaMeter(bool bound, double now)
        {
            DeltaActive = false;
            DeltaStopAfterFades = !bound;
            DeltaValue = 0;
            for (int i = 0; i < DeltaFaders.Length; i++)
                DeltaFaders[i].Set(NativeDeltaHidden, NativeDeltaHidden, now, 0);
            Invalidate();
        }

        public void NativeTriggerDeltaForProbe(int delta, double now)
        {
            TriggerNativeDelta(Math.Max(-5, Math.Min(5, delta)), now);
        }

        private void TriggerNativeDelta(int next, double now)
        {
            var previous = DeltaValue;
            if (next != 0)
            {
                DeltaActive = true;
                DeltaValue = next;
                Invalidate();
            }
            else DeltaStopAfterFades = true;

            var previousMagnitude = Math.Abs(previous);
            var nextMagnitude = Math.Abs(next);
            var signTransition = previous == 0 || next == 0
                || (previous < 0) != (next < 0);
            if (!signTransition && previousMagnitude == nextMagnitude) return;

            var primary = next > 0 ? NativeDeltaGreen : NativeDeltaRed;
            if (signTransition)
            {
                for (int i = 0; i < nextMagnitude; i++)
                    DeltaFaders[i].Set(NativeDeltaHidden, primary, now, DELTA_RAMP_MS);
                for (int i = nextMagnitude; i < DELTA_SLOTS; i++)
                    DeltaFaders[i].Set(NativeDeltaHidden, NativeDeltaHidden, now, 0);
            }
            else if (nextMagnitude > previousMagnitude)
            {
                // Native deliberately includes the old outer edge, brightening
                // it again together with each newly exposed record.
                for (int i = Math.Max(previousMagnitude - 1, 0); i < nextMagnitude; i++)
                    DeltaFaders[i].SetFromCurrent(primary, now, DELTA_RAMP_MS);
            }
            else
            {
                for (int i = previousMagnitude - 1; i >= nextMagnitude; i--)
                    DeltaFaders[i].SetFromCurrent(NativeDeltaHidden, now, DELTA_RAMP_MS);
            }
        }

        /// <summary>
        /// Executes one native paint-state evaluation. Exact primary colours
        /// re-arm the second bright-to-muted 500ms ramp in this same paint.
        /// </summary>
        public Color[] NativePaintDeltaForProbe(double now)
        {
            return PaintNativeDelta(now);
        }

        private Color[] PaintNativeDelta(double now)
        {
            var result = new Color[DELTA_SLOTS];
            if (!DeltaActive) return result;

            var allDone = true;
            for (int i = 0; i < DELTA_SLOTS; i++)
            {
                var color = DeltaFaders[i].Current(now);
                result[i] = color;
                if (color == NativeDeltaRed)
                    DeltaFaders[i].Set(NativeDeltaRed, NativeDeltaRedMuted, now, DELTA_RAMP_MS);
                else if (color == NativeDeltaGreen)
                    DeltaFaders[i].Set(NativeDeltaGreen, NativeDeltaGreenMuted, now, DELTA_RAMP_MS);
                if (!DeltaFaders[i].Done(now)) allDone = false;
            }

            if (!allDone) Invalidate();
            else if (DeltaStopAfterFades)
            {
                DeltaActive = false;
                DeltaValue = 0;
                DeltaStopAfterFades = false;
            }
            return result;
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            var screen = GameFacade.Screens?.CurrentUIScreen as TS1GameScreen;
            if (screen == null || !screen.Desktop || screen.vm == null) return;

            var bank = NativeHistoryBanks.GetValue(screen, key => new NativeMotiveHistoryBank(key));
            bank.Advance(state, screen);
            NativeNowMilliseconds = bank.NowMilliseconds;

            var selected = screen.SelectedAvatar;
            var selectedIndex = selected == null ? -1 : Array.IndexOf(bank.People, selected);
            if (selectedIndex != BoundHistoryIndex || !object.ReferenceEquals(selected, BoundPerson))
            {
                BoundHistoryIndex = selectedIndex;
                BoundPerson = selected;
                ResetNativeDeltaMeter(selectedIndex >= 0, NativeNowMilliseconds);
            }

            if (LastPumpFrame != bank.FrameSerial)
            {
                LastPumpFrame = bank.FrameSerial;
                var motive = NativeMotiveSlot(Motive);
                if (motive >= 0 && selectedIndex >= 0)
                {
                    foreach (var pump in bank.Pumps)
                    {
                        var deltas = pump.Deltas[motive];
                        if (deltas != null && selectedIndex < deltas.Length)
                            TriggerNativeDelta(deltas[selectedIndex], NativeNowMilliseconds);
                    }
                }
            }

            if (DeltaActive && DeltaFaders.Any(fader => !fader.Done(NativeNowMilliseconds)))
                Invalidate();
            if (!NativeRemainderRamp.Done(NativeNowMilliseconds)
                || !NativeActiveRamp.Done(NativeNowMilliseconds))
                Invalidate();
        }

        public void SetName(string name, OriginalGlyphFont font)
        {
            if (name == null || font == null) return;
            if (Label == null)
            {
                Label = new UIOriginalText(name, font) { Color = NativeCaptionColor };
                Label.Size = new Vector2(GAUGE_W, GAUGE_H);
                Add(Label);
            }
            Label.Text = name;
            ApplyCaptionState();
            // cTSWinBtn centers its caption in the full text rectangle. The
            // native top is (10-characterHeight)/2; for shipped font8 this is
            // -3, intentionally allowing its clipped glyph cells to touch the
            // control's top edge.
            Label.Position = new Vector2(
                Math.Max(0, (GAUGE_W - font.Measure(name)) / 2),
                (10 - font.LineHeight) / 2);
        }

        /// the engine fill law as a pure function of RawValue (autotest pin):
        /// clamp(round(raw*60/200), 0, 60) px of the 100px gauge.
        public int FillWidthForProbe()
        {
            var raw = RawValue + 100;
            return Math.Max(0, Math.Min(FILL_MAX, (int)(0.5 + raw * 60 / 200.0)));
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            if (Pixel == null) Pixel = FSO.Common.Utils.TextureGenerator.GetPxWhite(FSO.Client.GameFacade.GraphicsDevice);
            var barColors = PaintNativeBar(NativeNowMilliseconds);

            var raw = RawValue + 100;                     // [0,200]
            var fill = Math.Max(0, Math.Min(FILL_MAX, (int)(0.5 + raw * 60 / 200.0)));
            if (fill < BAR_W)
                DrawLocalTexture(batch, Pixel, null, new Vector2(BAR_X + fill, BAR_Y),
                    new Vector2(BAR_W - fill, BAR_H), barColors.Remainder);
            if (fill > 0)
            {
                DrawLocalTexture(batch, Pixel, null, new Vector2(BAR_X, BAR_Y),
                    new Vector2(fill, BAR_H), barColors.Active);
                if (fill > 1)
                    DrawLocalTexture(batch, Pixel, null, new Vector2(BAR_X + fill - 1, BAR_Y),
                        new Vector2(1, BAR_H), Color.Black);
            }
            base.Draw(batch);

            // cWinDeltaMeter is a child of cWinMotive, so it paints after the
            // button caption. Capture the direction before PaintNativeDelta:
            // a completed stop paint clears the state only after drawing its
            // final (hidden-colour) five records.
            if (DeltaActive)
            {
                var deltaAtPaint = DeltaValue;
                var colors = PaintNativeDelta(NativeNowMilliseconds);
                if (deltaAtPaint != 0)
                {
                    var positive = deltaAtPaint > 0;
                    var mask = positive ? NativeRightArrowMask : NativeLeftArrowMask;
                    for (int slot = 0; slot < DELTA_SLOTS; slot++)
                    {
                        var point = NativeArrowPointForProbe(positive, slot);
                        for (int y = 0; y < 5; y++)
                        {
                            for (int x = 0; x < 3; x++)
                            {
                                if (mask[y][x] != '#') continue;
                                DrawLocalTexture(batch, Pixel, null,
                                    new Vector2(point.X + x, point.Y + y),
                                    Vector2.One, colors[slot]);
                            }
                        }
                    }
                }
            }
        }
    }

    public class UIMotiveBar : UIValueBar
    {
        public int TargetArrow;
        public int Arrow;
        public int MotiveValue;
        public int OldMotiveValue = -200;
        private Queue<int> ChangeBuffer = new Queue<int>();

        public Texture2D ArrowGfx;
        public Texture2D GreenTex;
        public Texture2D RedTex;
        public Texture2D GaugeTex;

        public float ArrowCycle;

        public UIMotiveBar() : base(Content.Get().CustomUI.Get("motive_bg.png").Get(GameFacade.GraphicsDevice))
        {
            ArrowGfx = Content.Get().CustomUI.Get("motive_arrow.png").Get(GameFacade.GraphicsDevice);
            // R85: IFF-first original fill (Greenbars/Redbars are the 3-slice motive-gauge fill:
            // 9px caps + 9px mid); png fallback only if the IFF member ever fails to mount.
            GreenTex = ResolveBar("cpanel\\Greenbars.BMP", "bars_green.png", BarBase);
            RedTex   = ResolveBar("cpanel\\Redbars.bmp", "bars_red.png", BarBase);
            // R86: original kLiveModeGauge backdrop behind the fill (same IFF-first policy).
            GaugeTex = ResolveBar("cpanel\\Backgrounds\\LiveGadget.bmp", "gauge_live.png", BarBase);
            Width = 150;
        }

        private static Texture2D ResolveBar(string iffName, string pngName, Texture2D fallback)
        {
            try
            {
                var iffTx = Simitone.Client.UI.Model.UIOriginal.EnsureResolved(iffName);
                if (iffTx != null) return iffTx.Get(GameFacade.GraphicsDevice);
            }
            catch { }
            try
            {
                var px = Content.Get().CustomUI.Get(pngName);
                if (px != null) return px.Get(GameFacade.GraphicsDevice);
            }
            catch { }
            return fallback;
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);

            if (OldMotiveValue != -200) ChangeBuffer.Enqueue(MotiveValue- OldMotiveValue);
            OldMotiveValue = MotiveValue;

            if (ChangeBuffer.Count > 240) ChangeBuffer.Dequeue();

            int sum = 0;
            foreach (var c in ChangeBuffer) sum += c;

            var diff = sum / 2.5;
            if (diff < 0) diff = Math.Floor(diff);
            else if (diff > 0) diff = Math.Ceiling(diff);
            TargetArrow = Math.Max(Math.Min((int)diff, 5), -5) * 60;

            if (Arrow > TargetArrow) Arrow--;
            if (Arrow < TargetArrow) Arrow++;

            Value = (MotiveValue+100) / 200f;

            ArrowCycle += Arrow / 240f;
            if (ArrowCycle < 0) ArrowCycle += 14f;
            ArrowCycle %= 14f;
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            // R123 (user-reported 'odd background' behind the status bars): the R86
            // composition drew kLiveModeGauge (cpanel\Backgrounds\LiveGadget.bmp,
            // 108x100 — the LIVE-TAB mood gauge, a different control) stretched to
            // 150x139 behind every 25px motive fill, producing eight overlapping
            // gray grid-textured plaques. The canon symbol map has NO track or
            // backdrop member for motive bars (kGreenbars 4506 / kRedBars 4510 are
            // self-contained 3-slice fills, with no paired Off/Background member —
            // unlike HouseSubBars/JobSubBars/FameSubBars/InterestBars which all
            // have one): the original draws the fill straight on the panel.
            // GaugeTex stays LOADED (the uitoolbar check pins its IFF mount).
            var low = Value < 0.25f;
            var tex = (low ? RedTex : GreenTex) ?? BarBase;
            var active = (int)Math.Round(Value * Width);
            DrawSlice(batch, tex, active, Color.White, 0);   // original 3-slice fill, no tint
            var w = BarBase.Width / 3;
            var spanw = (int)(Width * Value) - w * 2;
            var arrows = spanw / 14;
            var xStart = (Arrow > 0) ? 0 : (spanw);
            var dir = (Arrow > 0) ? 1 : -1;
            for (int i=0; i<arrows; i++)
            {
                float alpha = Math.Min(Math.Abs(Arrow) / 300f, 0.33f);
                if (i == 0) alpha *= (ArrowCycle/14f) * dir + (dir-1)/-2;
                else if (i == arrows - 1) alpha *= (1 - (ArrowCycle / 14f)) * dir + (dir - 1) / -2;

                DrawLocalTexture(batch, ArrowGfx, null, new Vector2(xStart + dir * (i * 14) + ArrowCycle, 0), new Vector2(dir, 1), Color.White * alpha);
            }
        }
    }
}
