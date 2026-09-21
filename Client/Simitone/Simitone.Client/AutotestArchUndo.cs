/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/
using FSO.SimAntics;
using FSO.SimAntics.Model;
using FSO.SimAntics.NetPlay.Model;
using FSO.SimAntics.NetPlay.Model.Commands;
using FSO.SimAntics.Utils;
using Microsoft.Xna.Framework;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client
{
    /// <summary>
    /// UI-22 'archundo' (opt-in, focused): the build-mode architecture
    /// undo/redo acceptance battery — tranche 1 walls. Runs on the real
    /// in-lot VM right after LOT-READY (drives the PRODUCTION send paths:
    /// vm.SendCommand(VMNetArchitectureCmd) for the "drag" and
    /// vm.SendCommand(VMNetArchUndoCmd) for the undo/redo buttons' actions).
    ///
    /// The engine phases run in LIVE mode (the command stream is
    /// mode-independent; this keeps the VM clock running so the run does not
    /// depend on paused-tick processing). BUILD mode is entered only for the
    /// button refresh law (entering preserves the stack) and the final
    /// clear-on-exit law, in that order.
    ///
    /// Laws exercised (tools/iff-dump/r256-undo-decode/undo-machinery-law.md
    /// + coordination/evidence/UI-22/implementation-design.md §6):
    /// 1. One applied batch = one undo step (SubmitUndoable §2/§6); submit
    ///    clears redo.
    /// 2. Undo restores the wall state exactly (story snapshot law §4).
    /// 3. Redo replays the recorded batch (forward-iteration law §3/§4).
    /// 4. Money hooks: undo refunds the batch cost, redo re-charges it
    ///    (Purchase/Refund law §4) — via the family budget.
    /// 5. Previews never record (capture gate: transient/!RealMode batches
    ///    are skipped — design §2).
    /// 6. Entering build mode preserves the stack; leaving it clears both
    ///    stacks (CPState::SetMode old-mode==2 law §5), via the production
    ///    UIMainPanel.SetMode path.
    /// 7. Buttons enable exactly with CanUndo/CanRedo (refresh law §4) —
    ///    read off the mounted UIOriginalArchChrome after UIMainPanel's
    ///    per-frame refresh.
    /// </summary>
    public class AutotestArchUndo
    {
        private readonly Action<string> _log;
        private int _phase, _phaseTicks;
        private bool _done;
        private readonly List<string> _notes = new List<string>();
        private readonly List<string> _fails = new List<string>();

        /// <summary>
        /// Discriminator mode (runs 3/3b froze identically at the undo):
        /// 'archundo-probe' in the checks string runs the IDENTICAL scenario
        /// (same lot, wall batch, wall-clock window) with the UNDO/REDO SENDS
        /// disabled — phases 3/4 become pure observation windows. If the
        /// DEFECT-2 freeze still hits here, it is the ambient ENG-01 lottery;
        /// if this window is clean, the undo path is causal.
        /// </summary>
        private readonly bool _probe;

        private readonly TS1GameScreen _screen;
        private VM _vm;
        private UIMainPanel _panel;
        private VMArchitecture _arch;

        private int _walls0, _walls1;
        private int _budget0, _budget1, _budget2, _budget3;
        private bool _hasFamily;
        private ushort _wallStyle;
        private List<VMArchitectureCommand> _batch;
        private int _sendAttempts;
        private int _dialogAnswers;
        private int _p7Step; // phase-7 refusal-law sub-state (ENG-02/UI-22 P2)

        private const int SettleTicks = 30;
        private const int PhaseTimeout = 900; // ~15s at 60fps
        private const int MaxSends = 3;
        /// <summary>Probe observation window (~40s): 2x the runs-3/3b freeze point.</summary>
        private const int ProbeWindowTicks = 2400;

        public bool Passed { get { return _fails.Count == 0; } }
        public string Diagnostics { get { return string.Join("; ", _notes); } }
        public string Failures { get { return string.Join("; ", _fails); } }

        public AutotestArchUndo(Action<string> log, TS1GameScreen screen, bool probe = false)
        {
            _log = log;
            _screen = screen;
            _probe = probe;
        }

        private void Note(string s) { _notes.Add(s); _log("AUTOTEST archundo " + s); }
        private void Fail(string s) { _fails.Add(s); _log("AUTOTEST archundo FAIL " + s); }

        private static int WallTiles(VMArchitecture arch)
        {
            int n = 0;
            for (int i = 0; i < arch.Walls.Length; i++)
                for (int j = 0; j < arch.Walls[i].Length; j++)
                    if (arch.Walls[i][j].Segments > 0) n++;
            return n;
        }

        private static int FamilyBudget(VM vm)
        {
            return vm?.TS1State?.CurrentFamily?.Budget
                ?? global::FSO.Content.Content.Get().Neighborhood?.GameState?.ActiveFamily?.Budget
                ?? -1;
        }

        /// <summary>
        /// Find an empty 6-tile horizontal run on level 1 that the ENGINE
        /// itself accepts: VMArchitectureTools.VerifyDrawWall is the exact
        /// gate RunCommands applies (bounds, sloped terrain, object overlap
        /// via CheckWallValid). Scans from the lot centre outwards, skipping
        /// the street strip near the edges.
        /// </summary>
        private VMArchitectureCommand? FindWallSpot()
        {
            int midY = _arch.Height / 2;
            for (int radius = 0; radius < midY - 2; radius++)
            {
                int y = midY + radius;
                if (y > _arch.Height - 3) continue;
                for (int x = 8; x < _arch.Width - 9; x++)
                {
                    if (!_arch.OutsideClip((short)x, (short)y, 1) &&
                        _arch.GetWall((short)x, (short)y, 1).Segments == 0 &&
                        VMArchitectureTools.VerifyDrawWall(_arch, new Point(x, y), 6, 0, 1))
                    {
                        return new VMArchitectureCommand
                        {
                            Type = VMArchitectureCommandType.WALL_LINE,
                            x = x,
                            y = y,
                            x2 = 6, // length
                            y2 = 0, // direction (+x)
                            level = 1,
                            pattern = 0,
                            style = _wallStyle,
                        };
                    }
                }
            }
            return null;
        }

        /// <summary>True when the phase's wait condition held (caller records timing).</summary>
        private bool WaitCond(Func<bool> cond)
        {
            if (cond()) return true;
            if (++_phaseTicks > PhaseTimeout)
            {
                return false;
            }
            return true;
        }

        private void PhaseTimeoutFail(string what)
        {
            Fail(what + " timed out (ticks=" + _phaseTicks + ") walls=" + WallTiles(_arch)
                + " canUndo=" + _arch.UndoStack.CanUndo + " canRedo=" + _arch.UndoStack.CanRedo);
            _done = true;
        }

        public bool Tick()
        {
            if (_done) return true;
            // ENG-02 (run-3-diag law): a TS1 blocking dialog (GenDlg mount at
            // 6:31, tick 3) parks the VM at SpeedMultiplier=-2 and latches
            // GlobalBlockingDialog forever in a headless run — the world stops
            // ticking and every later phase races a paused VM. Answer it through
            // the production dismissal command (the OK-button law,
            // UILotControl.DialogResponse(0)) so the VM keeps running.
            if (_vm != null && _vm.GlobalBlockingDialog != null)
            {
                _dialogAnswers++;
                if (_dialogAnswers == 1)
                {
                    // ENG-02 decode addendum: name the CONTENT caller of the
                    // latch (the entity whose tree ran the blocking dialog
                    // primitive) — the park mechanism is engine-explained, but
                    // the content defect that fires it on this lot stays named.
                    string caller = "";
                    try
                    {
                        var gbd = _vm.GlobalBlockingDialog;
                        var fr = gbd.Thread?.Stack;
                        var top = (fr != null && fr.Count > 0) ? fr[fr.Count - 1] : null;
                        var ci = top?.GetCurrentInstruction();
                        caller = " caller=ent" + gbd.ObjectID + " iff="
                            + (gbd.Object?.Resource?.MainIff?.Filename ?? "?")
                            + " tree=" + (top?.Routine?.Rti?.Name ?? "?").TrimEnd('\0')
                            + ":ip" + (top != null ? (int)top.InstructionPointer : -1)
                            + " opcode=" + (ci?.Opcode ?? -1);
                    }
                    catch { caller = " caller=(read-failed)"; }
                    Note("blocking dialog latched; answering via VMNetDialogResponseCmd(0)" + caller);
                }
                _vm.SendCommand(new FSO.SimAntics.NetPlay.Model.Commands.VMNetDialogResponseCmd
                {
                    ResponseCode = 0,
                    ResponseText = ""
                });
            }
            try
            {
                RunPhase();
            }
            catch (Exception e)
            {
                Fail("phase " + _phase + " exception " + e.GetType().Name + ": " + e.Message);
                _done = true;
            }
            return _done;
        }

        private void RunPhase()
        {
            switch (_phase)
            {
                case 0: // settle on the loaded lot (LIVE), record baselines
                    {
                        if (_vm == null)
                        {
                            if (_screen.vm == null || !_screen.InLot)
                            {
                                _phaseTicks++;
                                if (_phaseTicks > PhaseTimeout) { Fail("vm never appeared"); _done = true; }
                                return;
                            }
                            _vm = _screen.vm;
                            _arch = _vm.Context.Architecture;
                            _panel = _screen.Frontend?.MainPanel;
                            if (_panel == null || _arch == null) { Fail("panel/architecture unavailable"); _done = true; return; }

                            var styles = FSO.Content.Content.Get().WorldWalls.WallStyleToIndex.Keys.ToList();
                            _wallStyle = (styles.Count == 0) ? (ushort)1 : (styles.Contains(1) ? (ushort)1 : (ushort)styles.Min());
                            _phaseTicks = 0; // settle window starts once the VM is mounted
                        }
                        if (++_phaseTicks < SettleTicks) return;

                        // previews never record: the stack must be empty after settle
                        if (_arch.UndoStack.CanUndo || _arch.UndoStack.CanRedo) Fail("stack not empty on fresh lot");
                        else Note("stack empty on fresh lot");

                        _walls0 = WallTiles(_arch);
                        _budget0 = FamilyBudget(_vm);
                        _hasFamily = _vm.TS1State?.CurrentFamily != null;
                        Note("baseline walls=" + _walls0 + " budget=" + _budget0 + " family=" + _hasFamily);
                        _phase = 1; _phaseTicks = 0; _sendAttempts = 0;
                    }
                    break;

                case 1: // send one engine-validated wall "drag" through the production path
                    {
                        if (_batch == null)
                        {
                            var cmd = FindWallSpot();
                            if (cmd == null) { Fail("no engine-valid wall run found"); _done = true; return; }
                            _batch = new List<VMArchitectureCommand> { cmd.Value };
                            _vm.SendCommand(new VMNetArchitectureCmd { Commands = new List<VMArchitectureCommand>(_batch) });
                            _sendAttempts++;
                            Note("sent WALL_LINE #" + _sendAttempts + " x=" + cmd.Value.x + " y=" + cmd.Value.y + " len=6 style=" + _wallStyle);
                            return;
                        }
                        if (_arch.UndoStack.CanUndo)
                        {
                            _walls1 = WallTiles(_arch);
                            _budget1 = FamilyBudget(_vm);
                            if (_walls1 <= _walls0) Fail("wall count did not increase (" + _walls0 + "->" + _walls1 + ")");
                            if (_arch.UndoStack.CanRedo) Fail("submit did not clear redo");
                            else Note("1 submit=1 undo step; redo cleared; walls " + _walls0 + "->" + _walls1);
                            if (_hasFamily && _budget1 >= _budget0) Fail("budget not charged (budget0=" + _budget0 + " budget1=" + _budget1 + ")");
                            Note("budget after place=" + _budget1);
                            _phase = 2; _phaseTicks = 0;
                            return;
                        }
                        if (++_phaseTicks > PhaseTimeout)
                        {
                            if (_sendAttempts < MaxSends)
                            {
                                // spot may have been taken by a moving object since
                                // validation — revalidate elsewhere and send again.
                                Note("no undo step yet; revalidating another spot");
                                _batch = null;
                                _phaseTicks = 0;
                                return;
                            }
                            PhaseTimeoutFail("phase 1 (batch never applied after " + _sendAttempts + " sends)");
                        }
                    }
                    break;

                case 2: // preview law: the production preview idiom records nothing
                    {
                        if (_phaseTicks == 0)
                        {
                            _arch.SimulateCommands(new List<VMArchitectureCommand>(_batch), true);
                            if (_arch.UndoStack.CanRedo) Fail("preview batch recorded an undo step");
                            else Note("preview never records (SimulateCommands transient)");
                        }
                        if (++_phaseTicks < SettleTicks) return;
                        _phase = 3; _phaseTicks = 0;
                    }
                    break;

                case 3: // undo through the production command (probe: skip, observe)
                    {
                        if (_probe)
                        {
                            if (_phaseTicks == 0)
                                Note("probe: undo send SKIPPED; observing " + ProbeWindowTicks + " ticks");
                            if (++_phaseTicks >= ProbeWindowTicks)
                            {
                                if (WallTiles(_arch) != _walls1) Fail("probe: walls drifted during observation");
                                if (!_arch.UndoStack.CanUndo) Fail("probe: undo entry vanished during observation");
                                Note("probe: observation window 1 clean (walls=" + WallTiles(_arch) + ")");
                                _phase = 4; _phaseTicks = 0;
                            }
                            return;
                        }
                        // ENG-02 fix: the old `if (_phaseTicks == 0) { send; return; }`
                        // never advanced _phaseTicks, so the send re-fired every frame
                        // and the completion check below was unreachable.
                        if (_phaseTicks++ == 0)
                        {
                            _vm.SendCommand(new VMNetArchUndoCmd { Redo = false });
                            return;
                        }
                        if (!_arch.UndoStack.CanUndo && _arch.UndoStack.CanRedo)
                        {
                            var wallsU = WallTiles(_arch);
                            _budget2 = FamilyBudget(_vm);
                            if (wallsU != _walls0) Fail("undo did not restore walls (" + wallsU + " != " + _walls0 + ")");
                            else Note("undo restores wall state");
                            if (_hasFamily && _budget2 != _budget0) Fail("undo refund wrong (budget2=" + _budget2 + " != " + _budget0 + ")");
                            if (_hasFamily) Note("undo refunded budget to " + _budget2);
                            _phase = 4; _phaseTicks = 0;
                            return;
                        }
                        if (!WaitCond(() => false)) PhaseTimeoutFail("phase 3 (undo)");
                    }
                    break;

                case 4: // redo through the production command (probe: skip, observe)
                    {
                        if (_probe)
                        {
                            if (++_phaseTicks >= ProbeWindowTicks / 4)
                            {
                                Note("probe: observation window 2 clean");
                                _phase = 5; _phaseTicks = 0;
                            }
                            return;
                        }
                        // ENG-02 fix: same frame-0 guard bug as case 3 (send-once).
                        if (_phaseTicks++ == 0)
                        {
                            _vm.SendCommand(new VMNetArchUndoCmd { Redo = true });
                            return;
                        }
                        if (_arch.UndoStack.CanUndo && !_arch.UndoStack.CanRedo)
                        {
                            var wallsR = WallTiles(_arch);
                            _budget3 = FamilyBudget(_vm);
                            if (wallsR != _walls1) Fail("redo did not replay walls (" + wallsR + " != " + _walls1 + ")");
                            else Note("redo replays the recorded batch");
                            if (_hasFamily && _budget3 != _budget1) Fail("redo re-charge wrong (budget3=" + _budget3 + " != " + _budget1 + ")");
                            if (_hasFamily) Note("redo re-charged budget to " + _budget3);
                            _phase = 5; _phaseTicks = 0;
                            return;
                        }
                        if (!WaitCond(() => false)) PhaseTimeoutFail("phase 4 (redo)");
                    }
                    break;

                case 5: // entering BUILD preserves the stack; button refresh law
                    {
                        // ENG-02 fix: the old frame-0 guard returned without ever
                        // incrementing _phaseTicks, so the settle loop below was
                        // unreachable and the gate sat in BUILD forever.
                        if (_phaseTicks++ == 0)
                        {
                            if (!_arch.UndoStack.CanUndo) { Fail("stack empty before button law (undo lost?)"); _done = true; return; }
                            _panel.SetMode(UIMainPanelMode.BUILD);
                            return;
                        }
                        if (_phaseTicks < SettleTicks) return;
                        if (!_arch.UndoStack.CanUndo) Fail("entering build mode cleared the stack (must only clear on exit)");
                        var chrome = _panel.ArchChrome;
                        if (chrome == null) { Note("arch chrome not mounted (mobile?); button law skipped"); }
                        else
                        {
                            if (chrome.UndoBtn.Disabled) Fail("undo button disabled with CanUndo");
                            if (!chrome.RedoBtn.Disabled) Fail("redo button enabled without CanRedo");
                            if (!chrome.UndoBtn.Disabled && chrome.RedoBtn.Disabled) Note("buttons track CanUndo/CanRedo (refresh law)");
                        }
                        _phase = 6; _phaseTicks = 0;
                    }
                    break;

                case 6: // leaving build mode clears both stacks (production SetMode path)
                    {
                        // ENG-02 fix: same frame-0 guard bug (increment after the act).
                        if (_phaseTicks++ == 0)
                        {
                            _panel.SetMode(UIMainPanelMode.LIVE);
                            return;
                        }
                        if (_phaseTicks < 5) return;
                        if (_arch.UndoStack.CanUndo || _arch.UndoStack.CanRedo) Fail("build-mode exit did not clear the stacks");
                        else Note("build-mode exit clears both stacks");
                        if (_probe) _done = true; // probe validates observation only — no sends past here
                        else { _phase = 7; _phaseTicks = 0; _batch = null; }
                    }
                    break;

                case 7: // ENG-02/UI-22 P2 refusal law: a refused undo/redo money hook
                        // must NOT replay the world. Fresh batch, undo (refund), then
                        // force-insolvent budget; the redo re-charge must be refused
                        // (walls unchanged, budget unchanged, chain dropped). Proves
                        // ApplyMoney's PerformTransaction verdict propagates end-to-end.
                    {
                        var family = _vm.TS1State?.CurrentFamily;
                        if (family == null)
                        {
                            Note("P2 refusal law skipped (no CurrentFamily on this lot)");
                            _done = true;
                            return;
                        }
                        switch (_p7Step)
                        {
                            case 0: // fresh batch (phase 6 cleared the stacks)
                                {
                                    var cmd = FindWallSpot();
                                    if (cmd == null) { Fail("P2: no engine-valid wall run found"); _done = true; return; }
                                    _batch = new List<VMArchitectureCommand> { cmd.Value };
                                    _vm.SendCommand(new VMNetArchitectureCmd { Commands = new List<VMArchitectureCommand>(_batch) });
                                    _p7Step = 1; _phaseTicks = 0;
                                    return;
                                }
                            case 1: // wait for the batch to apply
                                if (_arch.UndoStack.CanUndo) { _p7Step = 2; _phaseTicks = 0; return; }
                                if (++_phaseTicks > PhaseTimeout) PhaseTimeoutFail("phase 7 (batch never applied)");
                                return;
                            case 2: // undo: the refund path (always affordable) — puts the
                                    // world back to the _walls1 state and builds a redo entry
                                _vm.SendCommand(new VMNetArchUndoCmd { Redo = false });
                                _p7Step = 3; _phaseTicks = 0;
                                return;
                            case 3: // wait for the undo to apply
                                if (!_arch.UndoStack.CanUndo && _arch.UndoStack.CanRedo) { _p7Step = 4; _phaseTicks = 0; return; }
                                if (++_phaseTicks > PhaseTimeout) PhaseTimeoutFail("phase 7 (undo for refusal law)");
                                return;
                            case 4: // force insolvency, then send the doomed redo. Refused:
                                    // walls stay at _walls1, budget stays 0, chain drops.
                                    // (The pre-P2 bug would replay walls uncharged.)
                                family.Budget = 0;
                                _vm.SendCommand(new VMNetArchUndoCmd { Redo = true });
                                _phase = 8; _phaseTicks = 0;
                                return;
                        }
                    }
                    break;

                case 8: // refusal verdict checks (phase 7's redo was sent against a zero budget)
                    {
                        if (_phaseTicks++ < SettleTicks) return;
                        var family = _vm.TS1State?.CurrentFamily;
                        var wallsNow = WallTiles(_arch);
                        if (wallsNow != _walls1) Fail("P2: refused redo replayed the world anyway (" + wallsNow + " != " + _walls1 + ")");
                        else Note("P2: refused redo left walls untouched");
                        if (family != null && family.Budget != 0) Fail("P2: refused redo still charged (budget=" + family.Budget + ")");
                        else Note("P2: refused redo did not charge (budget=0)");
                        if (_arch.UndoStack.CanRedo) Fail("P2: refused redo kept the redo chain");
                        else Note("P2: refused redo dropped the redo chain (native failure law)");
                        _done = true;
                    }
                    break;
            }
        }
    }
}
