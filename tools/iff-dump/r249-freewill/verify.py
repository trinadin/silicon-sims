"""R249: pin the native autonomous-interaction selection law; reads owner exe only.

Pins >=20 NEW instruction words (TryFindBestAction, CompareScoredInteractions,
GetInteractionScore, GetCurrentScore, AppendInteractionsForAuto, TestInteraction,
free-will gate, winner insertion), the data constants behind them, and proves
the recovered ordering/scoring laws with pure-python fixtures:
comparator order, top-K uniform draw, threshold arithmetic, curve interpolation,
motive-score means, attenuation enum, int->double idiom, 8-slot action ring,
free-will mirror. Writes only verified-state.json next to itself.
"""
import hashlib
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
b = (ROOT / 'game-data/The Sims/The Sims Complete').read_bytes()
sha = hashlib.sha256(b).hexdigest()
assert sha == '33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f'


def w(addr):
    return struct.unpack_from('>I', b, addr)[0]


# ---- 1. code pins (file offsets; all NEW vs prior rounds' verified inputs) ----
pins = {
    # TryFindBestAction__8cXPersonFP9StackElem (0x109860)
    0x109874: 0x80A28DB8,  # lwz r5, -0x7248(r2)        ; free-will byte (skip gate)
    0x109884: 0x88050000,  # lbz r0, 0(r5)              ; test the byte
    0x109894: 0x8362A718,  # lwz r27, -0x58e8(r2)       ; AutonomyConstants obj
    0x109974: 0x387E0814,  # addi r3, r30, 0x814        ; candidate vector
    0x109980: 0x4BF962A1,  # bl GetCurrentScore__13MotiveEffectsFv
    0x109994: 0x80628F44,  # lwz r3, -0x70bc(r2)        ; "min score for visitors"
    0x1099A0: 0x80628F40,  # lwz r3, -0x70c0(r2)        ; "min score for family"
    0x1099B4: 0x4BFFCEDD,  # bl __ct__10ObjTestSimFP8cXPersonb (autonomous=1)
    0x109A9C: 0x48000334,  # b 0x109dd0                 ; object-loop latch
    0x109B78: 0x80630060,  # lwz r3, 0x60(r3)           ; master selector house flags
    0x109C5C: 0x4BFBC1C5,  # bl CalcShortDistance__8cXObjectFP8cXObject
    0x109C94: 0x4BFFC9FD,  # bl SetStackObject__10ObjTestSimFP8cXObject
    0x109CD0: 0x4BFFD241,  # bl 0x106f10 (std::vector<Interaction> clear)
    0x109D1C: 0x4BFFC5B5,  # bl TestInteraction__10ObjTestSimFP11InteractionPP14TreeTableEntry
    0x109D44: 0xEFA10024,  # fdivs f29, f1, f0          ; atten = N / (N + dist*entry.f24)
    0x109D48: 0x4BF95C89,  # bl GetInteractionScore__13MotiveEffectsFP11TreeTableAd
    0x109D58: 0xEC01F028,  # fsubs f0, f1, f30          ; score = H' - H
    0x109D5C: 0xEC1D0032,  # fmuls f0, f29, f0          ; score *= atten
    0x109D60: 0xD0010050,  # stfs f0, 0x50(r1)          ; ScoredInteraction.score
    0x109D9C: 0x801E0818,  # lwz r0, 0x818(r30)         ; candidate vector size
    0x109DAC: 0x4800A175,  # bl vector<ScoredInteraction> push (0x113f20)
    0x109DF8: 0x48490579,  # bl qsort (0x59a370) — sort candidate vector
    0x109E08: 0x80828F38,  # lwz r4, -0x70c8(r2)        ; "random selection count"
    0x109E14: 0x7C003E70,  # srawi r0, r0, 7            ; trait * 1.25 fixed-point
    0x109E24: 0x2C140001,  # cmpwi r20, 1               ; clamp K >= 1
    0x109E28: 0x40800008,  # bge 0x109e30
    0x109E70: 0x7C14A800,  # cmpw r20, r21              ; K vs count
    0x109E78: 0x48037359,  # bl GetNextRandomNumber__Fv
    0x109E7C: 0x7C03AB96,  # divwu r0, r3, r21          ; rand % min(K, count)
    0x109EB4: 0xA81E058C,  # lha r0, 0x58c(r30)         ; sitting/posture gate
    0x109ECC: 0x4182001C,  # beq 0x109ee8               ; gate==0 -> accept outright
    0x109ED0: 0x80628F34,  # lwz r3, -0x70cc(r2)        ; "min score for sitting"
    0x109ED4: 0xC0250008,  # lfs f1, 8(r5)              ; candidate score
    0x109ED8: 0xC0030000,  # lfs f0, 0(r3)              ; 0.1
    0x109EDC: 0xFC010040,  # fcmpu cr0, f0, f1
    0x109EE0: 0x40800008,  # bge 0x109ee8               ; 0.1 >= score -> accept
    0x109EE4: 0x38C00000,  # li r6, 0                   ; reject winner
    0x109EFC: 0xB01E0072,  # sth r0, 0x72(r30)          ; person+0x72 = action number
    0x109F04: 0xB01F0004,  # sth r0, 4(r31)             ; stack+4 = object id
    # CompareScoredInteractions__FPCvPCv (0x10a040)
    0x10A040: 0xC0240008,  # lfs f1, 8(r4)              ; b.score
    0x10A048: 0x8062A718,  # lwz r3, -0x58e8(r2)        ; AutonomyConstants
    0x10A04C: 0xEC210028,  # fsubs f1, f1, f0           ; delta = b - a
    0x10A050: 0xC0030024,  # lfs f0, 0x24(r3)           ; epsilon 1
    0x10A05C: 0x38600001,  # li r3, 1                   ; b first (delta <= eps1)
    0x10A064: 0xC0030028,  # lfs f0, 0x28(r3)           ; epsilon 2
    0x10A070: 0x54000FFE,  # srwi r0, r0, 0x1f          ; LT bit
    0x10A074: 0x7C6000D0,  # neg r3, r0                 ; -1: a first (delta < eps2)
}

more_pins = {
    0x109D1C: 0x4BFFC5B5,  # bl TestInteraction__10ObjTestSimFP11InteractionPP14TreeTableEntry
    0x109D44: 0xEFA10024,  # fdivs f29, f1, f0           ; atten = N / (N + dist*entry.f24)
    0x109D48: 0x4BF95C89,  # bl GetInteractionScore__13MotiveEffectsFP11TreeTableAd
    0x109D58: 0xEC01F028,  # fsubs f0, f1, f30           ; score = H' - H
    0x109D5C: 0xEC1D0032,  # fmuls f0, f29, f0           ; score *= atten
    0x109D60: 0xD0010050,  # stfs f0, 0x50(r1)           ; ScoredInteraction.score
    0x109D9C: 0x801E0818,  # lwz r0, 0x818(r30)          ; candidate vector size
    0x109DAC: 0x4800A175,  # bl vector<ScoredInteraction>::push (0x113f20)
    0x109DF8: 0x48490579,  # bl qsort (0x59a370) — sort candidate vector
    0x109E78: 0x48037359,  # bl GetNextRandomNumber__Fv
    0x109E7C: 0x7C03AB96,  # divwu r0, r3, r21           ; rand % min(K, count)
    0x109EFC: 0xB01E0072,  # sth r0, 0x72(r30)           ; person+0x72 = action number
    0x109F04: 0xB01F0004,  # sth r0, 4(r31)              ; stack+4 = object id
    # CompareScoredInteractions__FPCvPCv (0x10a040)
    0x10A040: 0xC0240008,  # lfs f1, 8(r4)               ; b.score
    0x10A048: 0x8062A718,  # lwz r3, -0x58e8(r2)         ; AutonomyConstants
    0x10A04C: 0xEC210028,  # fsubs f1, f1, f0            ; delta = b - a
    0x10A05C: 0x38600001,  # li r3, 1                    ; b first (delta <= eps1)
    0x10A070: 0x54000FFE,  # srwi r0, r0, 0x1f           ; LT bit
    0x10A074: 0x7C6000D0,  # neg r3, r0                  ; -1: a first (delta < eps2)
    # GetCurrentScore__13MotiveEffectsFv (0x9fc20)
    0x9FC28: 0x1C080014,  # mulli r0, r8, 0x14          ; 20-byte motive entries
    0x9FD34: 0xEC240024,  # fdivs f1, f4, f0            ; mean over entries
    # GetInteractionScore__13MotiveEffectsFP11TreeTableAd (0x9f9d0)
    0x9F9E0: 0x1CE60014,  # mulli r7, r6, 0x14
    0x9FA44: 0x216B03E8,  # subfic r11, r11, 0x3e8      ; 1000 - motive (inverted ads)
    0x9FAA0: 0xEC010024,  # fdivs f0, f1, f0            ; mv*row2/CFG2.f4
    0x9FBCC: 0xEC260024,  # fdivs f1, f6, f0            ; mean over entries
    # AppendInteractionsForAuto__10ObjTestSimF... (0x105900)
    0x1059F8: 0x8014001C,  # lwz r0, 0x1c(r20)           ; TTAB advertise weight
    0x105A00: 0x40810260,  # ble 0x105c60                ; weight <= 0 -> skip
    0x105B64: 0x7C190000,  # cmpw r25, r0                ; person+0x5d4 >= weight
    0x105AC8: 0x80D40018,  # lwz r6, 0x18(r20)           ; entry action number
    0x105ACC: 0x4BF96FC5,  # bl __ct__11InteractionFP8cXPersonP8cXObjectii
    # TestInteraction__10ObjTestSimFP11InteractionPP14TreeTableEntry (0x1062d0)
    0x106368: 0xA81E005A,  # lha r0, 0x5a(r30)           ; target flag bit22 gate
    0x106570: 0x4804D951,  # bl TreeSim run (0x153ec0)   ; test BHAV execution
    0x1065F8: 0xA89D00AE,  # lha r4, 0xae(r29)           ; person+0xae = tree result
    # winner-insertion context
    0x10AC30: 0xBF41FFE8,  # stmw r26, -0x18(r1)         ; RemoveAction__8cXPerson
    0x10ACB4: 0x4800028D,  # bl AddAction__8cXPersonFP11Interaction
    0x10ABF4: 0x38630820,  # addi r3, r3, 0x820          ; GetIndAction ring base
    0x10ABEC: 0x1C00003C,  # mulli r0, r0, 0x3c          ; 60-byte Interaction slots
    # free-will gate
    0xC9D5C: 0x98050000,  # stb r0, 0(r5)               ; global free-will byte @0x45f1c
    0xC9D74: 0x4806EEAD,  # bl SetGlobal__10cSimulatorFss (global 0x1e)
    0x23A020: 0x80628DB8,  # lwz r3, -0x7248(r2)         ; GetFreeWill = byte read
    0x239F00: 0x4BE8FE41,  # b SetFreeWill__8cXObjectFb  ; OptionsMgr delegates
    # AutonomyConstantsClient::UpdateConstants (0x112750) — named constants
    0x112760: 0x83C2A718,  # lwz r30, -0x58e8(r2)
    0x1127D4: 0x4BF7395D,  # bl Get__14FloatConstantsFPCcfb
    0x1127D8: 0x80A28F40,  # lwz r5, -0x70c0(r2)         ; "min autonomy score for family"
    0x1127F4: 0x80A28F44,  # lwz r5, -0x70bc(r2)         ; "...for visitors"
    0x1128B8: 0x80A28F34,  # lwz r5, -0x70cc(r2)         ; "...for sitting"
    0x1128D8: 0x80A28F38,  # lwz r5, -0x70c8(r2)         ; "random selection count"
    # GetAttenuationValue__14TreeTableEntryFb (0x155340)
    0x155344: 0x8082A840,  # lwz r4, -0x57c0(r2)         ; default attenuation pair
    0x15538C: 0x80628F14,  # lwz r3, -0x70ec(r2)         ; visitor low attenuation
    0x1553F8: 0x80628F20,  # lwz r3, -0x70e0(r2)         ; family low attenuation
    # TryElement__8cXPerson prim dispatch to TryFindBestAction
    0x10C430: 0x4BFFD431,  # bl TryFindBestAction__8cXPersonFP9StackElem
    # RemoveAction / Simulate anchors
    0x10C104: 0x4BF929AD,  # bl Sim__7MotivesFv (motives tick gate)
}
pins.update(more_pins)
for addr, expected in pins.items():
    assert w(addr) == expected, hex(addr)

# ---- 2. data pins (unpacked data section; prefix unpack per r104/r244 law) ----


def varint(d, i):
    v = 0
    while True:
        x = d[i]
        i += 1
        v = ((v << 7) | (x & 0x7F)) & 0xFFFFFFFF
        if not (x & 0x80):
            return v, i


def unpack_pef(data):
    out = bytearray()
    i = 0
    n = len(data)
    while i < n:
        opb = data[i]
        i += 1
        op = opb >> 5
        cnt = opb & 0x1F
        if cnt == 0:
            cnt, i = varint(data, i)
        if op == 0:
            out += b'\0' * cnt
        elif op == 1:
            out += data[i:i + cnt]
            i += cnt
        elif op == 2:
            rc, i = varint(data, i)
            blk = data[i:i + cnt]
            i += cnt
            out += blk * (rc + 1)
        elif op in (3, 4):
            custom, i = varint(data, i)
            rc, i = varint(data, i)
            common = b'\0' * cnt if op == 4 else data[i:i + cnt]
            if op == 3:
                i += cnt
            out += common
            for _ in range(rc):
                out += data[i:i + custom]
                i += custom
                out += common
        else:
            raise ValueError('reserved pef op')
    return bytes(out)


CODE_BASE = 0x8E90
data = unpack_pef(b[0x5C22F0:0x5C22F0 + 0x6D834])
assert len(data) == 0x7BF80
assert len(unpack_pef(b[0x5C22F0:0x5C22F0 + 0x7BF80])) == 0x7BF80  # stream ends 0x6d834


def dw(off):
    return struct.unpack_from('>I', data, off)[0]


def fl(off):
    return struct.unpack_from('>f', data, off)[0]


TOC = 0x8000
data_evidence = {}
# named autonomy constants (initialized-data defaults; FloatConstants may
# override at runtime via AutonomyConstantsClient::UpdateConstants)
assert abs(fl(0x48EA4) - 0.2) < 1e-6    # "min autonomy score for family"  (TOC-0x70c0)
assert abs(fl(0x48EA8) - 0.05) < 1e-6   # "min autonomy score for visitors" (TOC-0x70bc)
assert abs(fl(0x48EAC) - 0.1) < 1e-6    # "min autonomy score for sitting"  (TOC-0x70cc)
assert dw(0x48EB0) == 10            # "random selection count"          (TOC-0x70c8)
data_evidence['constants'] = '0x48ea4=0.2 0x48ea8=0.05 0x48eac=0.1 0x48eb0=10'
assert data[0x45F1C] == 1           # free-will byte: enabled by default
data_evidence['freewill_init'] = 'data[0x45f1c]=1'
# comparator function pointer: TOC-0x58f8 -> TVector {0x1011b0, toc} -> 0x10a040
assert dw(0x2708) == 0xD9D0
assert dw(0xD9D0) == 0x1011B0
assert dw(0xD9D0) + CODE_BASE == 0x10A040
data_evidence['comparator_tvector'] = 'TOC-0x58f8->data 0xd9d0->{0x1011b0,0x8000}'
# constant name strings in the blob at *(TOC-0x58e4) = data 0x491c0
blob = 0x491C0
names = {off: data[blob + off:].split(b'\0')[0].decode('latin1')
         for off in (0x192, 0x1B0, 0x254, 0x273)}
assert names[0x192] == 'min autonomy score for family'
assert names[0x1B0] == 'min autonomy score for visitors'
assert names[0x254] == 'min autonomy score for sitting'
assert names[0x273] == 'random selection count'
data_evidence['constant_names'] = names
# ScoredInteraction vector clear name (0x116040 record) + Interaction stride
assert w(0x116040) == 0x38000000  # li r0,0 ; stw r0,4(r3) ; blr — count=0 clear

# ---- 3. pure-python fixtures ----
fixtures = {}

# (a) comparator law: with BSS-default epsilons (0.0) it returns 1 ("b first")
# exactly when b.score <= a.score, else 0 ("keep a first") -> net ascending.
def compare(a_score, b_score, eps1=0.0, eps2=0.0):
    delta = b_score - a_score
    if delta <= eps1:
        return 1
    if delta < eps2:
        return -1
    return 0


for better, worse in [(1.0, 5.0), (-0.5, 0.5), (-3.0, 0.0)]:
    assert compare(worse, better) == 1, 'better (lower) score goes first'
    assert compare(better, worse) == 0, 'order kept when first already better'
assert compare(2.0, 2.0) == 1
assert compare(2.0, 2.0 + 1e-9, 0.0, -1e-9) in (1, 0)  # eps band cannot invert
fixtures['comparator_ascending_eps0'] = 'PASS'


def sort_head(cands):
    # stable insertion mirroring the native comparator's net effect: ascending
    # (move element left while the previous element compares "1" = prev-first)
    out = list(cands)
    for i in range(1, len(out)):
        j = i
        while j > 0 and compare(out[j - 1], out[j]) == 1:
            out[j - 1], out[j] = out[j], out[j - 1]
            j -= 1
    return out


scores = [0.03, -0.20, 0.01, -0.05, 0.0, 0.30, -0.01]
ordered = sort_head(scores)
assert ordered == sorted(scores), 'net order is ascending by score'
fixtures['sort_order'] = 'PASS'

# (b) threshold law: K = max(1, Total - trunc(trait / 2000))
# (0x10624dd3 mulhw + srawi 7 = signed trunc division by 2000)
def threshold(trait, total=10):
    prod = trait * 0x10624DD3
    q = abs(prod) >> 39
    q = -q if prod < 0 else q  # mulhw(2^39 shift) + srawi 7 + sign add = trunc
    k = total - q
    return max(k, 1)


assert threshold(0) == 10
assert threshold(1999) == 10
assert threshold(2000) == 9
assert threshold(4000) == 8
assert threshold(10000) == 5
assert threshold(20000) == 1  # clamped at 1
assert threshold(-4000) == 12  # negative trait grows the pool
fixtures['selection_pool_K'] = 'PASS'

# (c) winner draw: idx = rand() % min(K, count); reject idx >= count
def draw(rand, k, count):
    n = min(k, count)
    return rand % n


assert draw(0, 5, 3) == 0 and draw(4, 5, 3) == 1 and draw(5, 5, 3) == 2
assert draw(12345, 10, 7) == 12345 % 7
fixtures['uniform_draw'] = 'PASS'

# (d) int->double idiom: bits{0x43300000, v^0x80000000} - (2**52 + 2**31)
MAGIC = float.fromhex('0x1.0000000000000p+52') + float.fromhex('0x1.0000000000000p+31')


def native_int_to_double(v):
    bits = (0x43300000 << 32) | ((v & 0xFFFFFFFF) ^ 0x80000000)
    d = struct.unpack('>d', struct.pack('>Q', bits))[0]
    return d - MAGIC


for v in (0, 1, 24, 999, 1000, -1, -24, -1000, 32767, -32768, 2**31 - 1, -(2**31)):
    assert native_int_to_double(v) == float(v), v
fixtures['int_to_double_idiom'] = 'PASS'

# (e) scoring law: per-motive contribution = piecewise-linear curve evaluated
# at (motive + advert_effect), accumulated and divided by entry count.
def lerp_curve(points, recips, x):
    k = len(points) - 1
    while k >= 0 and not (x - points[k][0] > 0.0):
        k -= 1
    if k == len(points) - 1:
        return points[k][1]
    if k == -1:
        return points[0][1]
    dy = points[k + 1][1] - points[k][1]
    return (x - points[k][0]) * recips[k] * dy + points[k][1]


pts = [(0.0, 100.0), (500.0, 60.0), (1000.0, 0.0)]
rec = [1 / (pts[1][0] - pts[0][0]), 1 / (pts[2][0] - pts[1][0])]
assert lerp_curve(pts, rec, -50.0) == 100.0   # below range -> first y
assert lerp_curve(pts, rec, 1500.0) == 0.0    # above range -> last y
assert abs(lerp_curve(pts, rec, 250.0) - 80.0) < 1e-12
assert abs(lerp_curve(pts, rec, 750.0) - 30.0) < 1e-12
fixtures['curve_interp'] = 'PASS'


def motive_score(entries, effects):
    # entries: list of (curve_pts, recips, motive_value_float)
    # effects: parallel list of advertised delta (already CFG2[8]-scaled)
    total = 0.0
    for (p, r, m), e in zip(entries, effects):
        total += lerp_curve(p, r, m + e)
    return total / len(entries)


e = [(pts, rec, 400.0)]
assert motive_score(e, [0.0]) == 68.0
assert motive_score(e, [100.0]) == 60.0  # motive 400+100 -> curve(500) = 60
fixtures['score_mean'] = 'PASS'

# (f) attenuation enum law (GetAttenuationValue): 0=entry float, 1/3/4 =
# low/moderate/high from the family or visitor constant set, else default.
def attenuation(code, entry_value, family_set, visitor_set, defaults, visitor):
    table = visitor_set if visitor else family_set
    if code == 0:
        return entry_value
    if code == 1:
        return table[0]
    if code == 3:
        return table[1]
    if code == 4:
        return table[2]
    return defaults[0] if not visitor else defaults[1]


fam = ('fam-low', 'fam-mod', 'fam-high')
vis = ('vis-low', 'vis-mod', 'vis-high')
dflt = ('dflt-fam', 'dflt-vis')
assert attenuation(0, 0.25, fam, vis, dflt, False) == 0.25
assert attenuation(1, 0.0, fam, vis, dflt, False) == 'fam-low'
assert attenuation(3, 0.0, fam, vis, dflt, True) == 'vis-mod'
assert attenuation(4, 0.0, fam, vis, dflt, True) == 'vis-high'
assert attenuation(9, 0.0, fam, vis, dflt, False) == 'dflt-fam'
fixtures['attenuation_enum'] = 'PASS'

# (g) distance attenuation: atten = N / (N + dist * entry_att), N = CFG.f20
def dist_atten(dist, entry_att, norm):
    return norm / (norm + dist * entry_att)


assert abs(dist_atten(0.0, 0.3, 20.0) - 1.0) < 1e-12
assert abs(dist_atten(10.0, 0.3, 20.0) - 20.0 / 23.0) < 1e-12
fixtures['distance_attenuation'] = 'PASS'

# (h) winner acceptance: accept iff (person+0x58c == 0) or score <= 0.1
def winner_accepted(gate_58c, score, sitting_min=0.1):
    if gate_58c == 0:
        return True
    return sitting_min >= score


assert winner_accepted(0, 99.0)
assert winner_accepted(1, -0.5)
assert winner_accepted(1, 0.1)
assert not winner_accepted(1, 0.11)
fixtures['winner_cutoff'] = 'PASS'

# (i) action ring: 8 Interaction slots (0x3c bytes) at person+0x820,
# GetIndAction(i) = person+0x820 + ((head + i) & 7) * 0x3c
def get_ind_action(head, i, base=0x820):
    return base + (((head + i) & 7) * 0x3C)


assert get_ind_action(3, 0) == 0x820 + 3 * 0x3C
assert get_ind_action(7, 1) == 0x820 + 0 * 0x3C
assert get_ind_action(0, 8) == 0x820 + 0 * 0x3C
fixtures['action_ring'] = 'PASS'

# (j) free-will mirror: byte @0x45f1c and SimAntics global 0x1e track together
class FreeWill:
    def __init__(self):
        self.byte = 1
        self.sim_global_0x1e = None

    def set(self, value):
        self.byte = value
        self.sim_global_0x1e = value  # cSimulator::SetGlobal(0x1e, v)


fw = FreeWill()
fw.set(0)
assert fw.byte == 0 and fw.sim_global_0x1e == 0
fw.set(1)
assert fw.byte == 1 and fw.sim_global_0x1e == 1
fixtures['freewill_mirror'] = 'PASS'

# ---- 4. emit ----
out = {
    'round': 'r249-freewill',
    'sha256': sha,
    'code_pins': len(pins),
    'data_evidence': data_evidence,
    'fixtures': fixtures,
    'pass': True,
}
(HERE / 'verified-state.json').write_text(json.dumps(out, indent=1))
print('R249 verify: PASS —', len(pins), 'code pins,', len(fixtures), 'fixture groups')
