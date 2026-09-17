# R253: the native dialog newline law — LF is the only line terminator

User report 2026-09-17: career-promotion dialog (and "these windows" generally) shows far
too much vertical gap between paragraphs. This round decodes the native line-break law
and pins the port defect.

## 1. The native font API has no CR case

Tiny predicate functions in the cTSFont region, each identified by its trailing
CW symbol name (file offsets; code virtual = file − 0x8E90):

```text
0x4ace40  cmplwi r0, r4, 32 ; cntlzw ; rlwinm ; bclr   "IsCharWhitespace__7cTSFontFUl"
0x4ace80  subfic r0, r4, 10 ; cntlzw ; rlwinm>>27 ; bclr   "IsCharReturnChar__7cTSFontFUl"
0x4acec0  cmplwi r0, r4, 32 ; bne+ ; cmplwi r4, 45 ; beqlr ; li r3,1   "IsCharBreakingChar..."
```

(The 0x4ace80 word 0x2004000a is the GCC/MrC equality idiom — subfic r0,r4,10
followed by cntlzw — i.e. `c == 10`.)

So: whitespace = SPACE only; the RETURN character of the text pipeline is **0x0A (LF)**;
word-break characters are SPACE and HYPHEN. A whole-code scan of the code section
(file 0x8E90..0x5C22E8) over that equality-idiom class for immediate values 10 or 13
finds **nine** sites, **all with immediate 10** (LF), and **zero** with 13 (CR):

```text
0x27ea90 0x3fc04c 0x43e504 0x463c74 0x477bdc 0x499b00 0x49ca80 0x4ace80 0x58143c
```

CR (0x0D) is never tested by the text pipeline. Reviewer note (accepted, wording
corrected): the equality-idiom scan class above (ops 8/9/12/13 — subfic/cmpi
equality idioms and their cr1 variants) has no 0x0D site; direct cmplwi/cmpwi
compares with immediate 13 do exist elsewhere in the code (~100 sites, none in
the cTSFont/text region), so the load-bearing claim is "the TEXT PIPELINE has no
CR case", not "the whole binary has none". CR reaches the glyph pipeline as a
plain byte; the FFN tables ship no visible glyph for it, so an embedded CR
renders as nothing. The Mac port of TS1 inherited the Windows engine's
convention wholesale (LF = terminator; the Mac data's CRLF pairs are the
Windows authoring format).

## 2. Game data authors paragraph gaps as one blank CRLF pair

The promotion template (Objects.far, STR# "Job" table, entry "Job: Dialog: Text",
comment "After coming home from work and receiving a promotion..."):

```text
You have been promoted! (to $Job:4:5)\r\n\r\n$JobDesc:4:5\r\n\r\nYou now work from
$TimeLocal:2 to $TimeLocal:3 starting immediately. You brought home $$Local:0 today
and got a bonus of $$Local:8!
```

Every paragraph gap in the Job dialog family is a single `\r\n\r\n` (i.e. ONE blank
line once CR is glyph-less). Under the r169 line law (slot-12 body line = 23px), the
native paragraph gap is therefore **one 23px blank line**.

## 3. The port defect

`FreeSO/TSOClient/tso.simantics/Engine/VMDialogHandler.cs` `ParseDialogString`
ended with:

```csharp
if (context.Thread != null) output.Replace("\r\n", "\r\n\r\n");
```

An upstream FreeSO/TSO-ism (present verbatim in the `_freeso` upstream mirror).
`StringBuilder.Replace` matches non-overlapping occurrences of the ORIGINAL buffer,
so an authored `\r\n\r\n` becomes `\r\n\r\n\r\n\r\n` — the port's wrapper then
converts pairs to `\n` (UIMobileAlert.WrapOriginalBodyLines) yielding THREE blank
lines ≈ 69px between paragraphs.

Live measurement (user screenshot, 2x capture, dialog logical 830x413 = the r169
2:1 search): body line pitch exactly 46 phys = 23 logical (the r169 slot-12 law,
correctly ported); text line slots at 0, 4, 8 — i.e. 4-slot gaps = 3 blank lines,
matching the doubled template exactly. Simulation of the port constants against
the composed message reproduces the screenshot band positions (0/4/8 slots).

## 4. The fix (this round)

Keep the TSO behavior (upstream semantics for the TSO client this engine also
builds) but do not double for TS1:

```csharp
if (context.Thread != null && !context.VM.TS1) output.Replace("\r\n", "\r\n\r\n");
```

The port's dialog wrappers already treat `\r\n` as one break and strip the CR
(`Replace("\r\n", "\n")` before splitting), which is exactly the native semantics
(LF breaks, CR invisible).

Secondary effect (also native-correct): with the authored single blank lines the
body is ~4 lines shorter, so the r169 §8.3 aspect search settles near ~460x230
instead of exploding to ~830x413 — the "too much gap" around the button and the
off-center title come down with it.

## 5. Blast radius (checked)

* No gate pins VM-dialog message text: `uifriend`'s `MessageTextForProbe` pin
  compares a client-constructed dialog (BuildFriendCountDialog) against the raw
  STR# 162 entry — it never passes through ParseDialogString.
* `AutotestRunner.cs` contains zero `\r\n\r\n` fixtures.
* EXP-01's uncommitted llinteract WIP adds no dialog-text assertions (checked in
  the working diff).
* Client-side ported dialogs (phone book, help) pass raw STR# and never went
  through the doubling — they already render single blank lines; the fix makes
  VM dialogs consistent with them.
