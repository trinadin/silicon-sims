#!/usr/bin/env python3
"""R251: re-pin the executable SHA256 and assert the exact instruction words at
the addresses cited in decode.md, so the mood-cadence decode is falsifiable.

Run:  python3 tools/iff-dump/r251-mood-cadence/verify.py
Exit 0 = all assertions hold. Exit 1 = a word or the SHA256 diverged.
"""
import hashlib
import struct
import sys

BIN = '/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete'

EXPECTED_SHA256 = '33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f'

# (file-offset, expected-word, meaning)  -- addresses are FILE offsets in the PEF image.
ASSERT = [
    # ---- cXPerson::CalcHappy (symbol 0x10b9c2, +2 convention -> file offset 0x10b9c0) ----
    (0x10b9c0, 0x8102a718, 'CalcHappy: lwz r8, -0x58e8(r2)  (TOC table base)'),
    (0x10bad4, 0xa8c3060e, 'CalcHappy: lha r6, 1550(r3)  (r6 = person+1550 = suit index)'),
    (0x10bad8, 0x38000001, 'CalcHappy: li r0, 1  (rA=0 -> load-immediate, NOT addi r0,r0,1)'),
    (0x10badc, 0x7cc00278, 'CalcHappy: xor r0, r6, r0  (r0 = suit ^ 1)'),
    (0x10bae0, 0x7c050e70, 'CalcHappy: srawi r5, r0, 1'),
    (0x10bae4, 0x7c003038, 'CalcHappy: and r0, r0, r6'),
    (0x10bae8, 0x7c002850, 'CalcHappy: subf r0, r0, r5'),
    (0x10baec, 0x54000fff, 'CalcHappy: rlwinm. r0, r0, 1, 31, 31  (sign bit / suit 2-5 test)'),
    (0x10baf0, 0x41820010, 'CalcHappy: beq 0x10bb00  (suit in {0,1} -> accumulate)'),
    (0x10baf4, 0xc0080030, 'CalcHappy: lfs f0, 0x30(r8)  (gate constant)'),
    (0x10baf8, 0xfc002000, 'CalcHappy: fcmpu cr0, f0, f4'),
    (0x10bafc, 0x4182000c, 'CalcHappy: beq 0x10bb08  (suit 2-5 && motive==const -> skip)'),
    (0x10bb00, 0xecc4307a, 'CalcHappy: fmadds f6, f4, f1, f6  (f6 += m*w)'),
    (0x10bb04, 0xeca5082a, 'CalcHappy: fadds f5, f5, f1  (f5 += w)'),
    (0x10bb14, 0xec062824, 'CalcHappy: fdivs f0, f6, f5  (f0 = sum(m*w)/sum(w))'),
    (0x10bb18, 0xd0030798, 'CalcHappy: stfs f0, 1944(r3)  (mood = person[1944] = slate 3)  <-- THE MOOD WRITE'),

    # ---- cXPerson::Simulate cadence gate (symbol 0x10bff2) ----
    (0x10c0d4, 0x80790810, 'Simulate: lwz r3, 0x810(r25)  (person[2064] = last motives-update time)'),
    (0x10c0d8, 0x3803003c, 'Simulate: addi r0, r3, 0x3c  (+ 60 time units)'),
    (0x10c0dc, 0x7c1a0000, 'Simulate: cmpw r26, r0  (current time vs last+60)'),
    (0x10c0e0, 0x41800028, 'Simulate: blt 0x10c108  (if current < last+60, skip)'),
    (0x10c0e4, 0x93590810, 'Simulate: stw r26, 0x810(r25)  (last = current)'),
    (0x10c100, 0x3879078c, 'Simulate: addi r3, r25, 1932  (motive array base)'),
    (0x10c104, 0x4bf929ad, 'Simulate: bl 0x9eab0  (Motives::Sim -> CalcHappy)'),

    # ---- Motives::Sim -> CalcHappy (symbol 0x09eab2) ----
    (0x09edc4, 0x80630080, 'Motives::Sim: lwz r3, 128(r3)  (person ptr)'),
    (0x09edc8, 0x4806cbf9, 'Motives::Sim: bl 0x10b9c0  (CalcHappy)  <-- direct caller, NOT vtable'),

    # ---- SAnimator::ResetSuits writes person+1550 = suit index 0..5 ----
    (0x361528, 0xb003060e, 'ResetSuits: sth r0, 1550(r3)  (r0 = 0)'),
    (0x361574, 0xb003060e, 'ResetSuits: sth r0, 1550(r3)  (r0 = 1)'),
    (0x3615c0, 0xb003060e, 'ResetSuits: sth r0, 1550(r3)  (r0 = 2)'),
    (0x36160c, 0xb003060e, 'ResetSuits: sth r0, 1550(r3)  (r0 = 3)'),
    (0x361658, 0xb003060e, 'ResetSuits: sth r0, 1550(r3)  (r0 = 4)'),
    (0x3616a4, 0xb003060e, 'ResetSuits: sth r0, 1550(r3)  (r0 = 5)'),
]


def verify_suit_gate():
    """Derived, falsifiable claim from the +1550 gate block (0x10bad8-0x10baf0).

    With r0 = 1 (li), after xor/srawi/and/subf/rlwinm the sign bit is the "gate
    active" flag. It is 0 for suit in {0,1} (always accumulate) and 1 for suit
    in {2,3,4,5} (value-gate applies). Return the dict to compare in decode.md.
    """
    out = {}
    for suit in range(6):
        t = (suit ^ 1) & 0xFFFFFFFF
        r5 = (t >> 1) | (t & 0x80000000)          # srawi (arithmetic) by 1
        r0 = t & suit
        diff = (r5 - r0) & 0xFFFFFFFF
        sign = (diff >> 31) & 1
        out[suit] = 'include' if sign == 0 else 'value-gate'
    return out


def main():
    data = open(BIN, 'rb').read()
    sha = hashlib.sha256(data).hexdigest()
    status = 0
    if sha != EXPECTED_SHA256:
        print(f'FAIL SHA256\n  got   {sha}\n  expect {EXPECTED_SHA256}')
        status = 1
    else:
        print(f'OK SHA256 {sha}')

    for off, exp, note in ASSERT:
        w = struct.unpack('>I', data[off:off + 4])[0]
        if w != exp:
            print(f'FAIL 0x{off:08x}  got 0x{w:08x}  expect 0x{exp:08x}  {note}')
            status = 1
        else:
            print(f'OK   0x{off:08x}  0x{w:08x}  {note}')

    gate = verify_suit_gate()
    print('suit->gate table:', gate)
    expected = {0: 'include', 1: 'include', 2: 'value-gate',
                3: 'value-gate', 4: 'value-gate', 5: 'value-gate'}
    if gate != expected:
        print(f'FAIL suit-gate table {gate} != {expected}')
        status = 1
    else:
        print('OK suit-gate table matches (suit 0/1 include-all; suit 2-5 value-gate)')

    print('\nRESULT:', 'PASS' if status == 0 else 'FAIL')
    sys.exit(status)


if __name__ == '__main__':
    main()
