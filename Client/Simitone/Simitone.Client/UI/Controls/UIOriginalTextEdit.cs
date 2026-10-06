using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Simitone.Client.UI.Controls
{
    /// <summary>
    /// A transparent multiline cTSWinTextEdit2 surface using the owned FFN
    /// metrics for wrap, caret, selection and hit testing. Scrapbook requests
    /// 4096 characters, unlimited lines and no focused/empty border.
    /// Keyboard text, clipboard, delete and select-all use the shared input
    /// manager; this class supplies glyph-based navigation and undo history.
    /// </summary>
    public class UIOriginalTextEdit : UIElement, IFocusableUI
    {
        public readonly OriginalGlyphFont Font;
        public readonly int EditorWidth, EditorHeight;
        // Native CAS text rect starts at x=5 and reserves another 5px for
        // wrapping. Default0 preserves existing Scrapbook placement.
        public readonly int HorizontalInset;
        private int WrapWidth => EditorWidth - 2 * HorizontalInset;
        private Rectangle TextBounds => new Rectangle(HorizontalInset, 0, EditorWidth - HorizontalInset, EditorHeight);
        public int Capacity = 4096;
        public bool Disabled;
        public bool IsFocused { get; private set; }
        public int SelectionStart { get; private set; }
        public int SelectionEnd { get; private set; } = -1;
        public int FirstVisibleLine { get; private set; }
        public int LineHeight => Font.LineHeight;
        public Color TextColor = new Color(195, 205, 205);
        private static readonly Dictionary<OriginalGlyphFont, Texture2D> SelectionAtlases = new Dictionary<OriginalGlyphFont, Texture2D>();
        public event Action<string> OnTextChanged;
        private StringBuilder Buffer = new StringBuilder();
        private readonly List<TextLine> Lines = new List<TextLine>();
        private readonly Stack<EditSnapshot> Undo = new Stack<EditSnapshot>();
        private readonly Stack<EditSnapshot> Redo = new Stack<EditSnapshot>();
        private bool LayoutDirty = true, Dragging, CaretVisible = true, WheelInitialized;
        private long BlinkTicks;
        private int LastWheel;
        private long LastClickTimestamp;
        private Vector2 LastClickPosition;
        private bool HasLastClick;
        private InputManager FocusManager;
        private struct TextLine { public int Start, End, Next; }
        private struct EditSnapshot { public string Text; public int Start, End; }
        public override Vector2 Size { get => new Vector2(EditorWidth, EditorHeight); set { } }
        public override Rectangle GetBounds() => new Rectangle(0, 0, EditorWidth, EditorHeight);
        public string Text
        {
            get => Buffer.ToString();
            set
            {
                var text = Normalize(value ?? "");
                if (text.Length > Capacity) text = text.Substring(0, Capacity);
                Buffer = new StringBuilder(text);
                SelectionStart = 0; SelectionEnd = -1; FirstVisibleLine = 0;
                Undo.Clear(); Redo.Clear(); LayoutDirty = true; HasLastClick = false;
            }
        }
        public int LineCount { get { RebuildLines(); return Lines.Count; } }
        private int Caret => SelectionEnd < 0 ? SelectionStart : SelectionEnd;
        private int PageLines => Math.Max(1, EditorHeight / LineHeight);

        public UIOriginalTextEdit(OriginalGlyphFont font, int width, int height, int horizontalInset = 0)
        {
            if (horizontalInset < 0 || 2 * horizontalInset >= width) throw new ArgumentOutOfRangeException(nameof(horizontalInset));
            HorizontalInset = horizontalInset;
            Font = font;
            EditorWidth = width;
            EditorHeight = height;
            ListenForMouse(GetBounds(), OnMouseEvent);
        }
        private static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
        public void OnFocusChanged(FocusEvent focus)
        {
            IsFocused = focus == FocusEvent.FocusIn;
            CaretVisible = true;
            if (!IsFocused) Dragging = false;
        }
        public void Blur()
        {
            if (FocusManager?.GetFocus() == this) FocusManager.SetFocus(null);
            IsFocused = false; Dragging = false;
        }
        public void OnMouseEvent(UIMouseEventType type, UpdateState state)
        {
            if (Disabled || !Visible) return;
            if (type == UIMouseEventType.MouseOver) GameFacade.Cursor.SetCursor(CursorType.IBeam);
            else if (type == UIMouseEventType.MouseOut) GameFacade.Cursor.SetCursor(CursorType.Normal);
            if (type == UIMouseEventType.MouseDown)
            {
                FocusManager = state.InputManager;
                FocusManager?.SetFocus(this);
                if (FocusManager == null) OnFocusChanged(FocusEvent.FocusIn);
                var point = GetMousePosition(state.MouseState);
                int at = HitTestText(point);
                long now = Stopwatch.GetTimestamp();
                bool doubleClick = HasLastClick && !state.ShiftDown
                    && (now-LastClickTimestamp)*1000.0/Stopwatch.Frequency <= 500
                    && Vector2.DistanceSquared(point,LastClickPosition) <= 16;
                if (state.ShiftDown) SelectionEnd = at;
                else { SelectionStart = at; SelectionEnd = -1; }
                if (doubleClick) SelectWordAt(at);
                LastClickTimestamp=now; LastClickPosition=point; HasLastClick=!doubleClick;
                Dragging = !doubleClick; CaretVisible = true;
            }
            else if (type == UIMouseEventType.MouseUp) Dragging = false;
        }
        public void SetSelection(int start, int end = -1)
        {
            SelectionStart = Math.Max(0, Math.Min(Buffer.Length, start));
            SelectionEnd = end < 0 ? -1 : Math.Max(0, Math.Min(Buffer.Length, end));
            EnsureCaretVisible();
        }
        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (!Visible || Disabled) { if (IsFocused) Blur(); return; }
            FocusManager = state.InputManager;
            var mouse = GetMousePosition(state.MouseState);
            int wheel = state.MouseState.ScrollWheelValue;
            if (WheelInitialized && GetBounds().Contains(mouse) && state.WindowFocused)
                ScrollBy(-(wheel - LastWheel) / 120);
            LastWheel = wheel; WheelInitialized = true;
            if (Dragging)
            {
                if (state.MouseState.LeftButton == ButtonState.Released) Dragging = false;
                else
                {
                    if (mouse.Y < 0) ScrollBy(-1);
                    if (mouse.Y >= EditorHeight) ScrollBy(1);
                    SelectionEnd = HitTestText(mouse);
                }
            }
            if (IsFocused && state.WindowFocused) ProcessInput(state);
            var ticks = state.Time?.TotalGameTime.Ticks ?? 0;
            if (ticks - BlinkTicks >= 5000000) { CaretVisible = !CaretVisible; BlinkTicks = ticks; }
        }
        /// <summary>The production keyboard path, also usable by input tests.</summary>
        public void ProcessInput(UpdateState state)
        {
            if (Disabled || !IsFocused || !state.WindowFocused || state.InputManager == null) return;
            var keyboard = state.KeyboardState;
            bool command = keyboard.IsKeyDown(Keys.LeftWindows) || keyboard.IsKeyDown(Keys.RightWindows);
            bool ctrl = state.CtrlDown || command;
            if (ctrl && state.NewKeys.Contains(Keys.Z))
            { Restore(state.ShiftDown ? Redo : Undo, state.ShiftDown ? Undo : Redo); return; }
            if (ctrl && state.NewKeys.Contains(Keys.Y)) { Restore(Redo, Undo); return; }
            // InputManager's clipboard shortcuts use Control. Alias Command
            // locally without changing the shared game keyboard state.
            UpdateState input = state;
            if (command)
            {
                input = new UpdateState { WindowFocused = state.WindowFocused,
                    KeyboardState = new KeyboardState(keyboard.GetPressedKeys().Concat(new[] { Keys.LeftControl }).ToArray()),
                    PreviousKeyboardState = state.PreviousKeyboardState, NewKeys = state.NewKeys,
                    FrameTextInput = state.FrameTextInput };
            }
            var before = Snapshot();
            // The shared clipboard helper accepts only positive end indices;
            // normalize a backwards selection locally for Copy/Cut (including
            // selecting back to index zero).
            int inputStart = SelectionStart, inputEnd = SelectionEnd;
            bool clipboardSelection = ctrl && (state.NewKeys.Contains(Keys.C) || state.NewKeys.Contains(Keys.X));
            if (clipboardSelection && inputEnd >= 0 && inputEnd < inputStart)
            { int swap = inputStart; inputStart = inputEnd; inputEnd = swap; }
            // The shared word-delete helper converts a zero-length range to
            // its "end of buffer" sentinel, deleting the final character at a
            // word boundary. Supply the intended range explicitly instead.
            if (ctrl && inputEnd < 0 && (state.NewKeys.Contains(Keys.Back) || state.NewKeys.Contains(Keys.Delete)))
            {
                int direction = state.NewKeys.Contains(Keys.Delete) ? 1 : -1;
                int target = inputStart;
                // AUD-17 A-2: word-delete crosses the adjacent whitespace run
                // first, then the word (macOS Option+Delete semantics — "foo |"
                // deletes "foo "). The old single loop stopped dead when the
                // char at the caret's edge was whitespace, handing the input
                // manager a zero-length range: Ctrl+Backspace after a space
                // was a silent no-op, forever.
                int step = direction < 0 ? -1 : 0; // back: the char before the caret; del: at it
                while (target + step >= 0 && target + step < Buffer.Length
                    && char.IsWhiteSpace(Buffer[target + step])) target += direction;
                while (target + step >= 0 && target + step < Buffer.Length
                    && !char.IsWhiteSpace(Buffer[target + step])) target += direction;
                inputEnd = target;
            }
            var result = state.InputManager.ApplyKeyboardInput(Buffer, input,
                inputStart, inputEnd, Buffer.Length < Capacity || SelectionEnd >= 0);
            if (result != null)
            {
                var raw = Buffer.ToString();
                var normalized = Normalize(raw);
                int removedStart = -1, removedCount = 0;
                if (normalized.Length > Capacity)
                {
                    // Preserve the preexisting suffix when incoming paste/frame
                    // text exceeds capacity in the middle of the caption. A
                    // simple Substring(0,Capacity) would erase existing text.
                    int commonPrefix = 0, commonSuffix = 0;
                    int preservedPrefix = before.End < 0 ? before.Start : Math.Min(before.Start, before.End);
                    while (commonPrefix < preservedPrefix && commonPrefix < normalized.Length
                        && before.Text[commonPrefix] == normalized[commonPrefix]) commonPrefix++;
                    while (commonSuffix < before.Text.Length - commonPrefix
                        && commonSuffix < normalized.Length - commonPrefix
                        && before.Text[before.Text.Length - commonSuffix - 1] == normalized[normalized.Length - commonSuffix - 1])
                        commonSuffix++;
                    removedCount = normalized.Length - Capacity;
                    removedStart = normalized.Length - commonSuffix - removedCount;
                    normalized = normalized.Remove(removedStart, removedCount);
                }
                int AdjustCursor(int index)
                {
                    if (index < 0) return normalized.Length;
                    index = Normalize(raw.Substring(0, Math.Min(raw.Length, index))).Length;
                    if (removedStart >= 0 && index > removedStart) index -= Math.Min(removedCount, index - removedStart);
                    return Math.Min(normalized.Length, index);
                }
                if (normalized != Buffer.ToString()) Buffer = new StringBuilder(normalized);
                SelectionStart = AdjustCursor(result.SelectionStart);
                SelectionEnd = result.SelectionEnd < 0 ? -1 : AdjustCursor(result.SelectionEnd);
                if (clipboardSelection && before.Text == Buffer.ToString())
                { SelectionStart = before.Start; SelectionEnd = before.End; }
                if (before.Text != Buffer.ToString())
                {
                    Undo.Push(before); Redo.Clear(); LayoutDirty = true;
                    OnTextChanged?.Invoke(Buffer.ToString());
                }
                if (result.ContentChanged || result.SelectionChanged) { CaretVisible = true; EnsureCaretVisible(); }
            }
            foreach (var key in state.NewKeys.Distinct())
            {
                if (key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down
                    || key == Keys.Home || key == Keys.End || key == Keys.PageUp || key == Keys.PageDown)
                    Navigate(key, state.ShiftDown, ctrl);
            }
        }
        private EditSnapshot Snapshot() => new EditSnapshot { Text = Buffer.ToString(), Start = SelectionStart, End = SelectionEnd };
        private void Restore(Stack<EditSnapshot> source, Stack<EditSnapshot> target)
        {
            if (source.Count == 0) return;
            target.Push(Snapshot());
            var next = source.Pop(); Buffer = new StringBuilder(next.Text);
            SelectionStart = next.Start; SelectionEnd = next.End; LayoutDirty = true;
            EnsureCaretVisible(); OnTextChanged?.Invoke(next.Text);
        }
        private void Navigate(Keys key, bool shift, bool ctrl)
        {
            RebuildLines();
            int target = Caret;
            int line = CaretLine(target);
            int pageTop = -1;
            switch (key)
            {
                case Keys.Left:
                case Keys.Right:
                    int delta = key == Keys.Left ? -1 : 1;
                    // MoveCursor clears an unshifted range, then advances from
                    // the active endpoint; it does not collapse to its edge.
                    target = ctrl ? WordBoundary(Caret,delta)
                        : Math.Max(0,Math.Min(Buffer.Length,Caret+delta));
                    break;
                case Keys.Home: target = ctrl ? 0 : Lines[line].Start; break;
                case Keys.End:
                    target = ctrl ? Buffer.Length : Lines[line].End;
                    // Original TSOnKeyDown backs over the final character of a
                    // soft-ended line. Its stored count includes that wrap edge;
                    // selecting the next line's start would move the caret down.
                    if (!ctrl && Lines[line].End == Lines[line].Next && Lines[line].Next < Buffer.Length)
                        target = Math.Max(Lines[line].Start,target-1);
                    break;
                default:
                    int step = key == Keys.Up ? -1 : key == Keys.Down ? 1 : key == Keys.PageUp ? -PageLines : PageLines;
                    if (ctrl)
                    {
                        // Native Ctrl+vertical navigation scrolls the viewport;
                        // adding Shift makes it a no-op, preserving selection.
                        if (!shift) ScrollBy(step);
                        return;
                    }
                    if (key == Keys.PageUp || key == Keys.PageDown)
                        pageTop = Math.Max(0,Math.Min(Math.Max(0,Lines.Count-PageLines),FirstVisibleLine+step));
                    int other = Math.Max(0, Math.Min(Lines.Count - 1, line + step));
                    // VertLineScroll531dbc..1f10 preserves the character column,
                    // not pixel X. Native count includes LF; our Next retains
                    // that count while End excludes it. The clamp is strictly >:
                    // equality at a terminated row advances into the next row.
                    int column = Caret - Lines[line].Start;
                    int nativeCount = Lines[other].Next - Lines[other].Start;
                    bool terminated = Lines[other].Next > Lines[other].End
                        || (Lines[other].End == Lines[other].Next && Lines[other].Next < Buffer.Length);
                    if (column > nativeCount)
                        column = Math.Max(0, nativeCount - (terminated ? 1 : 0));
                    target = Math.Min(Buffer.Length, Lines[other].Start + column);
                    break;
            }
            if (shift) SelectionEnd = target;
            else { SelectionStart = target; SelectionEnd = -1; }
            CaretVisible = true; EnsureCaretVisible();
            // VertPageScroll restores its separately computed viewport after
            // moving the caret; the selected row keeps its on-screen position.
            if (pageTop >= 0) FirstVisibleLine = pageTop;
        }
        // cTSWinTextEdit2::MoveWord uses space and LF boundaries. Right moves
        // through trailing separators to the next word; Left skips separators
        // before finding the previous word. Native DoubleL calls Left then
        // Right with selection enabled, including trailing spaces in the range.
        private static bool WordSeparator(char c) => c == ' ' || c == '\n';
        private int WordBoundary(int at, int direction)
        {
            at = Math.Max(0,Math.Min(Buffer.Length,at));
            char At(int index) => index < Buffer.Length ? Buffer[index] : '\0';
            if (direction < 0)
            {
                while (at > 1 && WordSeparator(At(at-1))) at--;
                if ((at > 0 && At(at)==' ') || At(at)=='\n') at--;
            }
            bool crossedSeparator=false;
            // Direct single-byte MoveWord law. Its leading-space behavior is
            // deliberate: Left at the initial spaces advances to the first word.
            while (at >= 0 && at <= Buffer.Length)
            {
                if (WordSeparator(At(at))) { crossedSeparator=true; at++; }
                else if (crossedSeparator) break;
                else at += direction;
            }
            return Math.Max(0,Math.Min(Buffer.Length,at));
        }
        internal void SelectWordAt(int at)
        {
            SelectionStart=WordBoundary(at,-1);
            SelectionEnd=WordBoundary(SelectionStart,1);
            EnsureCaretVisible();
        }
        internal int CaretRow { get { RebuildLines(); return CaretLine(Caret); } }

        private void RebuildLines()
        {
            if (!LayoutDirty) return;
            Lines.Clear();
            int start = 0;
            while (start < Buffer.Length)
            {
                int at = start, width = 0, lastSpace = -1;
                while (at < Buffer.Length && Buffer[at] != '\n')
                {
                    int next = width + Font.Advance(OriginalGlyphFont.MapChar(Buffer[at]));
                    if (next > WrapWidth && at > start) break;
                    width = next;
                    if (char.IsWhiteSpace(Buffer[at])) lastSpace = at;
                    at++;
                }
                int end = at, nextStart = at;
                if (at < Buffer.Length && Buffer[at] == '\n') nextStart = at + 1;
                else if (at < Buffer.Length && lastSpace >= start) end = nextStart = lastSpace + 1;
                Lines.Add(new TextLine { Start = start, End = end, Next = nextStart });
                start = nextStart;
            }
            if (Lines.Count == 0 || Buffer[Buffer.Length - 1] == '\n')
                Lines.Add(new TextLine { Start = Buffer.Length, End = Buffer.Length, Next = Buffer.Length });
            LayoutDirty = false;
        }
        private int CaretLine(int caret)
        {
            for (int i = 0; i < Lines.Count; i++)
                if (caret < Lines[i].Next || i == Lines.Count - 1) return i;
            return Lines.Count - 1;
        }
        private int WidthBetween(int start, int end)
        {
            int width = 0;
            for (int i = start; i < end && i < Buffer.Length; i++) width += Font.Advance(OriginalGlyphFont.MapChar(Buffer[i]));
            return width;
        }
        public int HitTestText(Vector2 point)
        {
            RebuildLines();
            int line = Math.Max(0, Math.Min(Lines.Count - 1, FirstVisibleLine + (int)Math.Floor(point.Y / LineHeight)));
            return HitTestLine(line, point.X - HorizontalInset);
        }
        private int HitTestLine(int lineIndex, float x)
        {
            var line = Lines[lineIndex];
            int width = 0;
            for (int i = line.Start; i < line.End; i++)
            {
                int advance = Font.Advance(OriginalGlyphFont.MapChar(Buffer[i]));
                if (x < width + advance / 2f) return i;
                width += advance;
            }
            // Native mouse-down/move/up subtract the terminal character at
            // an exhausted soft row. Hard LF is already excluded from End.
            return line.End == line.Next && line.Next < Buffer.Length
                ? Math.Max(line.Start,line.End-1) : line.End;
        }
        public void ScrollBy(int delta)
        {
            RebuildLines();
            FirstVisibleLine = Math.Max(0, Math.Min(Math.Max(0, Lines.Count - PageLines), FirstVisibleLine + delta));
        }
        private void EnsureCaretVisible()
        {
            RebuildLines();
            int line = CaretLine(Caret);
            if (line < FirstVisibleLine) FirstVisibleLine = line;
            else if (line >= FirstVisibleLine + PageLines) FirstVisibleLine = line - PageLines + 1;
        }
        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || Font?.Atlas == null) return;
            RebuildLines();
            int selStart = Math.Min(SelectionStart, SelectionEnd < 0 ? SelectionStart : SelectionEnd);
            int selEnd = Math.Max(SelectionStart, SelectionEnd < 0 ? SelectionStart : SelectionEnd);
            for (int lineIndex = FirstVisibleLine; lineIndex < Lines.Count; lineIndex++)
            {
                int y = (lineIndex - FirstVisibleLine) * LineHeight;
                if (y >= EditorHeight) break;
                var line = Lines[lineIndex];
                int x = HorizontalInset;
                for (int i = line.Start; i < line.End; i++)
                {
                    char c = OriginalGlyphFont.MapChar(Buffer[i]);
                    int advance = Font.Advance(c);
                    bool selected = IsFocused && i >= selStart && i < selEnd;
                    OriginalGlyphFont.Glyph glyph;
                    if (Font.ByChar.TryGetValue(c, out glyph) && glyph.W > 1)
                    {
                        var dest = new Rectangle(x, y + Font.GlyphY(glyph), glyph.W, glyph.H);
                        if (selected)
                        {
                            // TSDrawChar(selected=true) reverses the glyph mask:
                            // padding becomes ink, strokes retain the page below.
                            // It does not paint a separate selection background.
                            var cell = new Rectangle(x, y, advance + 1, LineHeight);
                            var ink = Rectangle.Intersect(cell, dest);
                            Fill(batch, new Rectangle(cell.X, cell.Y, cell.Width, ink.Top - cell.Top), TextColor);
                            Fill(batch, new Rectangle(cell.X, ink.Bottom, cell.Width, cell.Bottom - ink.Bottom), TextColor);
                            Fill(batch, new Rectangle(cell.X, ink.Top, ink.Left - cell.Left, ink.Height), TextColor);
                            Fill(batch, new Rectangle(ink.Right, ink.Top, cell.Right - ink.Right, ink.Height), TextColor);
                        }
                        var clip = Rectangle.Intersect(dest, TextBounds);
                        if (clip.Width > 0 && clip.Height > 0)
                            DrawLocalTexture(batch, selected ? SelectionAtlas() : Font.Atlas,
                                new Rectangle(glyph.U + clip.X - dest.X, glyph.V + clip.Y - dest.Y, clip.Width, clip.Height),
                                new Vector2(clip.X, clip.Y), Vector2.One, TextColor * Opacity);
                    }
                    else if (selected) Fill(batch, new Rectangle(x, y, advance + 1, LineHeight), TextColor);
                    x += advance;
                }
            }
            if (IsFocused && CaretVisible && !Disabled)
            {
                int line = CaretLine(Caret);
                Fill(batch, new Rectangle(Math.Min(EditorWidth - 1, HorizontalInset + WidthBetween(Lines[line].Start, Caret)),
                    (line - FirstVisibleLine) * LineHeight, 1, LineHeight), TextColor);
            }
        }
        private Texture2D SelectionAtlas()
        {
            if (SelectionAtlases.TryGetValue(Font, out var atlas)) return atlas;
            var pixels = new Color[Font.Atlas.Width * Font.Atlas.Height];
            Font.Atlas.GetData(pixels);
            for (int i = 0; i < pixels.Length; i++)
            {
                byte inverse = (byte)(255 - pixels[i].A);
                pixels[i] = new Color(inverse, inverse, inverse, inverse);
            }
            atlas = new Texture2D(Font.Atlas.GraphicsDevice, Font.Atlas.Width, Font.Atlas.Height);
            atlas.SetData(pixels);
            SelectionAtlases[Font] = atlas;
            return atlas;
        }
        private void Fill(UISpriteBatch batch, Rectangle area, Color color)
        {
            area = Rectangle.Intersect(area, TextBounds);
            if (area.Width > 0 && area.Height > 0)
                DrawLocalTexture(batch, TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice), null,
                    new Vector2(area.X, area.Y), new Vector2(area.Width, area.Height), color * Opacity);
        }
    }
}
