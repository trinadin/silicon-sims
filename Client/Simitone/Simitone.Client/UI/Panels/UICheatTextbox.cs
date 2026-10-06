using FSO.Client;
using FSO.Client.GameContent;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Client.UI.Model;
using FSO.Common.Rendering.Framework.Model;
using FSO.SimAntics;
using FSO.SimAntics.NetPlay.Model.Commands;
using FSO.SimAntics.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simitone.Client.UI.Panels
{
    public class UICheatTextbox : UIContainer
    {
        private Dictionary<string, VMCheatContext.VMCheatType> cheatDefinitions = new Dictionary<string, VMCheatContext.VMCheatType>()
        {
            { "moveobjects", VMCheatContext.VMCheatType.MoveObjects},
            { "motherlode", VMCheatContext.VMCheatType.Budget },
            { "klapaucius", VMCheatContext.VMCheatType.Budget },
            { "rosebud", VMCheatContext.VMCheatType.Budget },
            // gives the user the submitted amount of money
            { "giveMoney", VMCheatContext.VMCheatType.Budget },
            // R247 (r247-tut-lifecycle §A cheat registry): `tutorial` (id 0x3d,
            // type 2 — "on"/"off"/numeric modifier) and `restore_tut` (id 0x2d,
            // type 0 — no parameters). Both dispatch through VMCheatContext via
            // the VMNetCheatCmd this console sends on ts1VM.SendCommand.
            { "tutorial", VMCheatContext.VMCheatType.Tutorial },
            { "restore_tut", VMCheatContext.VMCheatType.RestoreTut }
        };

        private UITextBox baseTextbox;
        private Texture2D baseTexture;
        private VM ts1VM;

        // R207: gate-readable surfaces.
        public UITextBox TextBoxForProbe { get { return baseTextbox; } }
        public Texture2D BackgroundTextureForProbe { get { return baseTexture; } }

        // R213 'uicheathelp' (tools/iff-dump/r213/r213-cheat-help-law.md): the
        // cTSWinCheatHelp surface — the registered-command autocomplete that
        // opens with the bar (edit event 1 -> full list) and filters as you
        // type (event 2 -> prefix filter), Enter completing the selected name
        // into the bar (the OK law: SetText + refocus + close the help;
        // submitting is a SECOND Enter), Escape closing only the help.
        private UIContainer HelpPanel;
        private List<Simitone.Client.UI.Controls.UIOriginalText> HelpRows = new List<Simitone.Client.UI.Controls.UIOriginalText>();
        private Simitone.Client.UI.Controls.UIOriginalText HelpNoMatchGlyph;
        private List<string> HelpMatches = new List<string>();
        private int HelpSelectedIndex = -1;
        private string HelpLastText;
        private bool HelpDismissed; // Escape/OK removed the help from the chain; only a bar re-show re-arms it
        private Texture2D HelpTexture;
        public static int HelpOpens, HelpCompletions;

        public bool HelpVisibleForProbe { get { return HelpPanel != null && HelpPanel.Visible; } }
        public int HelpCountForProbe { get { return HelpMatches.Count; } }
        public int HelpSelectedForProbe { get { return HelpSelectedIndex; } }
        public string HelpSelectedTextForProbe
        {
            get { return (HelpSelectedIndex >= 0 && HelpSelectedIndex < HelpMatches.Count) ? HelpMatches[HelpSelectedIndex] : null; }
        }

        /// <summary>
        /// An empty UICheatTextbox
        /// </summary>
        public UICheatTextbox(FSO.SimAntics.VM vm) : this(vm, "")
        {
            
        }
        /// <summary>
        /// A UICheatTextbox with text
        /// </summary>
        /// <param name="initialText"></param>
        public UICheatTextbox(FSO.SimAntics.VM vm, string initialText)
        {
            ts1VM = vm;
            baseTextbox = new UITextBox()
            {
                CurrentText = initialText,
                FlashOnEmpty = true,
                MaxLines = 1,
                //Tooltip = "Cheaters never win",
                //FrameColor = Color.Transparent,
                ID = "UICheatTextboxBase",
                
            };
            // R207 ENGINE LAW (tools/iff-dump/r207/r207-cheat-bar-law.md):
            // cSimsApp::FinishSlowInit 0x24d6f4 creates the cheat bar as a
            // cTSWinTextEdit2 (new(0x198)) with SetArea(0x14, 0x14, 0xdc,
            // 0x29) = (20,20)-(220,41) — 200x21 top-left — SetCapacity(255),
            // SetLinesAllowed(1), SetTransparent(FALSE: opaque), SetTextColor
            // WHITE, SetColors(*(BSS 0x9297c) = RGB(0x40,0x5D,0x5F), white,
            // white) — the InitSimsColors palette (r143 cas-layout-law §5).
            // Font = engine font table index 10 (the funds/CAS-name face).
            baseTexture = new Texture2D(GameFacade.GraphicsDevice, 1, 1);
            baseTexture.SetData(new Color[] { new Color((byte)0x40, (byte)0x5D, (byte)0x5F, (byte)255) });
            baseTextbox.SetBackgroundTexture(baseTexture, 0, 0, 0, 0);
            baseTextbox.MaxChars = 255;
            baseTextbox.TextStyle = baseTextbox.TextStyle.Clone();
            baseTextbox.TextStyle.Color = new Color((byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0xFF);
            Size = new Vector2(200, 21);
            baseTextbox.SetSize(200, 21);
            // R142: the container starts hidden — the inner box must too, or it reads
            // as a visible element in survey trees until the first Update sync.
            baseTextbox.Visible = false;
            Add(baseTextbox);
            BuildHelpPanel();
        }

        /// The cTSWinCheatHelp surface. The engine window's own rects are BSS
        /// (disclosed): the port places it directly under the (20,20)-(220,41)
        /// bar with the system-dialog fill RGB(0,0,82) (the R142 msgbox law)
        /// and original-glyph rows on the cheat face (font 10).
        private void BuildHelpPanel()
        {
            try
            {
                HelpPanel = new UIContainer();
                HelpTexture = new Texture2D(GameFacade.GraphicsDevice, 1, 1);
                HelpTexture.SetData(new Color[] { new Color((byte)0, (byte)0, (byte)0x52, (byte)255) });
                var back = new UIImage(HelpTexture);
                back.SetSize(200, 21 + 8 * 11);
                HelpPanel.Add(back);
                var font = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadByIndex(10, GameFacade.GraphicsDevice);
                for (int i = 0; i < 8; i++)
                {
                    var idx = i; // closure capture
                    var row = new Simitone.Client.UI.Controls.UIOriginalText("", font)
                    {
                        Color = Color.White,
                        Position = new Vector2(2, 23 + i * 11),
                    };
                    row.Size = new Vector2(196, 11);
                    row.ListenForMouse(new Rectangle(0, 23 + i * 11, 200, 11), (evt, state) =>
                    {
                        if (evt != FSO.Common.Rendering.Framework.IO.UIMouseEventType.MouseUp) return;
                        if (idx < HelpMatches.Count) { HelpSelectedIndex = idx; RefreshHelpRows(); }
                    });
                    HelpPanel.Add(row);
                    HelpRows.Add(row);
                }
                HelpNoMatchGlyph = new Simitone.Client.UI.Controls.UIOriginalText(UIOriginalCheatHelpLaw.NoMatchText, font)
                {
                    Color = Color.White,
                    Position = new Vector2(2, 4),
                };
                HelpNoMatchGlyph.Size = new Vector2(196, 16);
                HelpPanel.Add(HelpNoMatchGlyph);
                HelpPanel.Position = new Vector2(0, 24); // under the bar: absolute (20,44)
                HelpPanel.Visible = false;
                Add(HelpPanel);
            }
            catch (Exception he) { Simitone.Client.GameLog.Write("cheat-help ctor EXC " + he.GetType().Name + " " + he.Message); HelpPanel = null; }
        }

        /// Open 0x50da20: (re)fill the list from the filter, select the exact
        /// match else index 0, show the no-match label when empty.
        public void CheatHelpOpen(string prefix)
        {
            if (HelpPanel == null) return;
            HelpMatches = UIOriginalCheatHelpLaw.Filter(prefix);
            HelpSelectedIndex = UIOriginalCheatHelpLaw.SelectIndex(HelpMatches, prefix);
            HelpLastText = baseTextbox != null ? baseTextbox.CurrentText : null;
            HelpPanel.Visible = true;
            HelpDismissed = false;
            HelpOpens++;
            RefreshHelpRows();
        }

        private void RefreshHelpRows()
        {
            for (int i = 0; i < HelpRows.Count; i++)
            {
                var row = HelpRows[i];
                var has = i < HelpMatches.Count;
                row.Text = has ? HelpMatches[i] : "";
                row.Color = (has && i == HelpSelectedIndex)
                    ? new Color(0, 255, 255) : Color.White;
                row.Visible = has;
            }
            if (HelpNoMatchGlyph != null) HelpNoMatchGlyph.Visible = HelpMatches.Count == 0;
        }

        /// The OK/Cancel/close paths all remove the window from view.
        public void CheatHelpHide()
        {
            if (HelpPanel != null) HelpPanel.Visible = false;
            HelpSelectedIndex = -1;
            HelpMatches.Clear();
            if (HelpNoMatchGlyph != null) HelpNoMatchGlyph.Visible = false;
        }
        public override void Update(UpdateState state)
        {
            base.Update(state);
            var pressedKeys = state.KeyboardState.GetPressedKeys();
            // R207: the engine chord accepts BOTH keys — cTSMainWindowW95::
            // TSOnKeyDown 0x4d5b48 gates (modifiers & 3) == 3 then matches
            // 0x63/0x43 ('c'/'C') at 0x4d5ba4 AND 0x62/0x42 ('b'/'B') at
            // 0x4d5b68, both calling ShowCheatWidget(true).
            if ((pressedKeys.Contains(Keys.LeftControl) || pressedKeys.Contains(Keys.RightControl))
                && (pressedKeys.Contains(Keys.LeftShift) || pressedKeys.Contains(Keys.RightShift))
                && (state.NewKeys.Contains(Keys.C) || state.NewKeys.Contains(Keys.B)))
            {
                Visible = !Visible;
                if (!Visible) { state.InputManager.SetFocus(null); CheatHelpHide(); }
                else
                {
                    state.InputManager.SetFocus(baseTextbox);
                    // CheatCodeMgrCallback edit event 1: the bar opening shows
                    // the FULL registered list (Open(null)).
                    CheatHelpOpen(null);
                }
            }
            baseTextbox.Visible = Visible;
            if (HelpPanel != null && Visible && !HelpDismissed
                && baseTextbox.CurrentText != HelpLastText)
            {
                // edit event 2: every text change re-opens filtered.
                CheatHelpOpen(baseTextbox.CurrentText);
            }
            if (Visible)
            {
                if (HelpPanel != null && HelpPanel.Visible && state.NewKeys.Contains(Keys.Escape))
                {
                    CheatHelpHide(); // Cancel: closes the help, the bar stays
                    HelpDismissed = true;
                    return;
                }
                if (state.NewKeys.Contains(Keys.Enter))
                {
                    // The help window's OK law: with a selection, Enter
                    // COMPLETES the text and closes the help (SetText +
                    // refocus + remove); submitting is a second Enter.
                    if (HelpPanel != null && HelpPanel.Visible)
                    {
                        if (HelpSelectedIndex >= 0 && HelpSelectedIndex < HelpMatches.Count)
                        {
                            baseTextbox.CurrentText = HelpMatches[HelpSelectedIndex] + " ";
                            HelpCompletions++;
                        }
                        CheatHelpHide(); // OK: complete-or-close, never submit
                        HelpDismissed = true;
                        return;
                    }
                    commandEntered(baseTextbox.CurrentText, out bool shouldHide);
                    Visible = !shouldHide;
                    if (!Visible) { state.InputManager.SetFocus(null); CheatHelpHide(); } // Clear focus when hiding
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="commandString"></param>
        private void commandEntered(string commandString, out bool shouldHide)
        {
            shouldHide = true;
            if (string.IsNullOrWhiteSpace(commandString)) return; // a blank textbox should close after hitting enter -- even if a command was never run.

            // Handle weather commands separately (they don't use VMCheatContext)
            if (trimRepetitions(commandString).StartsWith("weather", StringComparison.OrdinalIgnoreCase))
            {
                HandleWeatherCommand(commandString);
                baseTextbox.CurrentText = "";
                return;
            }

            var cheat = new VMNetCheatCmd();
            var context = new VMCheatContext();
            var repetitions = getRepetitions(commandString);
            switch (trimRepetitions(commandString))
            {
                //These three are special cheatcodes that don't really match the parameterized cheats e.g. moveobjects on
                case "klapaucius":
                case "rosebud":
                    context.Amount = (int)VMCheatContext.BudgetCheatPresetAmount.KLAPAUCIUS;
                    context.CheatBehavior = VMCheatContext.VMCheatType.Budget;
                    break;
                case "motherlode":
                    context.Amount = (int)VMCheatContext.BudgetCheatPresetAmount.MOTHERLODE;
                    context.CheatBehavior = VMCheatContext.VMCheatType.Budget;
                    break;
                default: context = parseCommandString(commandString); break;
            }
            cheat.Context = context;
            shouldHide = false;
            if (cheat.Context == null) // the command was not recognized
            {
                FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Error); // in TS1 this was a dialog but a sound may be less intrusive
                return;
            }
            context.Repetitions = (byte)repetitions;
            var sndEvent = UISounds.Error;
            if (context.CheatBehavior != VMCheatContext.VMCheatType.InvalidCheat)
            {
                ts1VM.SendCommand(cheat);
                switch (context.CheatBehavior) // sound feedback
                {
                    case VMCheatContext.VMCheatType.Budget: sndEvent = UISounds.BuyPlace; break;
                    default: sndEvent = UISounds.Click; break;
                }
                shouldHide = true;
            }
            FSO.HIT.HITVM.Get().PlaySoundEvent(sndEvent);
        } 

        private String trimRepetitions(string input)
        {
            return input.Replace(";", "").Replace("!", "");
        }

        private int getRepetitions(string input)
        {
            var repetitions = input.Count(x => x == '!');
            if (repetitions == input.Count(x => x == ';'))
                return repetitions;
            return 0;
        }

        /// <summary>
        /// Creates a VMCheatContext which includes repetitions, parameters, and CheatBehavior
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        /// <remarks>internal: this is the production parse entry the R247
        /// battery drives directly (the console path — commandEntered — is
        /// exercised separately through reflection).</remarks>
        internal VMCheatContext parseCommandString(string command)
        {
            if (command.Length == 0)
                return null;
            var baseCmd = command;

            int NumberOfParameters = 1; // in the future if we want multiple parameters per cheat...
            
            string[] parameters = new string[NumberOfParameters];
            if (command.Contains(' ')) {
                baseCmd = command.Substring(0, command.IndexOf(' '));
                string parameterString = command.Substring(command.IndexOf(' ') + 1);
                for (int i = 0; i < NumberOfParameters; i++)
                {
                    var individualParameter = parameterString;
                    if (string.IsNullOrWhiteSpace(individualParameter))
                        break;
                    if (individualParameter.Contains(' '))
                    {
                        individualParameter = individualParameter.Substring(0, individualParameter.IndexOf(' '));
                        parameters[i] = trimRepetitions(individualParameter); // AUD-17 B-5: "on!" -> "on"
                        parameterString = parameterString.Substring(parameterString.IndexOf(' ') + 1);
                        continue;
                    }
                    parameters[i] = trimRepetitions(individualParameter);
                    break;
                }
            }
            // AUD-17 B-5: the trimmed result was discarded — "moveobjects;" /
            // "moveobjects!" never resolved (the preset switch above trims).
            baseCmd = trimRepetitions(baseCmd);
            if (!cheatDefinitions.TryGetValue(baseCmd, out VMCheatContext.VMCheatType cheatType))
            {
                // cheat not defined in cheatDefinitions
                return null;
            }
            VMCheatContext context = new VMCheatContext()
            {
                CheatBehavior = cheatType,                
            };
            foreach(var parameter in parameters)
            {
                switch (parameter)
                {
                    case "on": context.Modifier = true; break; // set modifier true
                    case "off": context.Modifier = false; break; // set modifer false
                    default:
                        if (int.TryParse(parameter, out int amount)) //check if the parameter is a number
                            context.Amount = amount; // if it is amount is set
                        break;
                }
            }
            return context;
        }

        private void HandleWeatherCommand(string command)
        {
            var parts = command.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Error);
                return;
            }

            string weatherType = parts[1];
            int intensity = 50;

            if (parts.Length >= 3 && int.TryParse(parts[2], out int parsedIntensity))
            {
                intensity = Math.Clamp(parsedIntensity, 0, 100);
            }

            short weatherData = 0;

            switch (weatherType)
            {
                case "rain":
                    weatherData = (short)((1 << 8) | (0 << 9) | intensity);
                    break;
                case "storm":
                case "thunder":
                    if (parts.Length < 3) intensity = 75;
                    weatherData = (short)((1 << 8) | (1 << 11) | (0 << 9) | intensity);
                    break;
                case "snow":
                    weatherData = (short)((1 << 8) | (1 << 9) | intensity);
                    break;
                case "hail":
                    weatherData = (short)((1 << 8) | (2 << 9) | intensity);
                    break;
                case "clear":
                    weatherData = (short)(1 << 8);
                    break;
                case "auto":
                    weatherData = 0;
                    break;
                default:
                    FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Error);
                    return;
            }

            if (ts1VM?.Context?.Blueprint?.Weather != null)
            {
                ts1VM.Context.Blueprint.Weather.SetWeather(weatherData);
                FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Click);
            }
            else
            {
                FSO.HIT.HITVM.Get().PlaySoundEvent(UISounds.Error);
            }
        }
    }
}
