"""Pin R247 tutorial lifecycle decode; reads owner executable only.

Pins the instruction words relied on this round: the three lifecycle functions
(ResetTutorial 0x258350, TutorialCompleted 0xad9e0, CancelTutorial 0xad930),
every bl call site used as evidence (Options slot 0x21, generic calls 0/8/9,
the TSOnKeyDown cancel case, the LoadHouse spawner, GetHouseInfo ->
GetHouseFileInfo), the SIMI/NGBH chunk patch sites, the cheat registrations,
and key data words (string tables, jump tables, 'SIMI' constants, the
'Tutorial.FAM' literal, the IFF FILE magic pattern).
Fixtures prove the decoded laws in pure python: the ResetTutorial path build
(GetString(7)='UserData/' / GetString(0x58)='UserData/Import/' + separator
fixup + 'Tutorial.FAM'), the file-attribute law (attr & ~1 clears READONLY,
-1 = INVALID_FILE_ATTRIBUTES), the TutorialCompleted state machine (arg+1
writes, arg==0-only global-26 clears, spawn predicate state < 3), the
generic-call table decode, the HouseInfo fill law (global 58 -> +0x14), and
the move-in refusal ordering.
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


def varint(d, i):
    v = 0
    while True:
        x = d[i]; i += 1
        v = ((v << 7) | (x & 0x7F)) & 0xFFFFFFFF
        if not (x & 0x80):
            return v, i


def unpack_pef(data):
    out = bytearray(); i = 0; n = len(data)
    while i < n:
        opb = data[i]; i += 1
        op = opb >> 5; cnt = opb & 0x1F
        if cnt == 0:
            cnt, i = varint(data, i)
        if op == 0:
            out += b'\0' * cnt
        elif op == 1:
            out += data[i:i + cnt]; i += cnt
        elif op == 2:
            rc, i = varint(data, i)
            blk = data[i:i + cnt]; i += cnt
            out += blk * (rc + 1)
        elif op in (3, 4):
            custom, i = varint(data, i)
            rc, i = varint(data, i)
            common = b'\0' * cnt if op == 4 else data[i:i + cnt]
            if op == 3:
                i += cnt
            out += common
            for _ in range(rc):
                out += data[i:i + custom]; i += custom
                out += common
        else:
            raise ValueError('reserved pef op')
    return bytes(out)


DATA = unpack_pef(b[0x5C22F0:0x5C22F0 + 0x6D834])
assert len(DATA) == 0x7BF80

# address -> expected big-endian instruction word (file offsets)
pins = {
    # CancelTutorial 0xad930: app chain -> ObjectModule (core+0x14), owner
    # fetch, named tree at strtab+0xc5, KillObject by owner->+0xf0
    0x0AD938: 0x80628874, 0x0AD950: 0x83C30014, 0x0AD958: 0x48035429,
    0x0AD974: 0x8084000C, 0x0AD978: 0x38C600C5, 0x0AD97C: 0x480423B5,
    0x0AD980: 0xA89F00F0, 0x0AD988: 0x480390D9,
    # TutorialCompleted 0xad9e0: TOC constants, fmt offset 0xd5, temp-sim SIMI
    # patch (0x8d720 / SetGlobal(26,0) / 0x8d9b0), NGBH 'NGBH' constant,
    # temp-Neighborhood object +0x12a/+0x12c writes (stack frame 0x492/0x494 =
    # r1+0x368 base + the object offsets; NOT chunk offsets), live
    # SetGlobal(26,0) and live +0x12a/+0x12c writes
    0x0AD9EC: 0x83828CE4, 0x0AD9F0: 0x83A28CE8, 0x0ADA28: 0x837F0164,
    0x0ADA70: 0x388400D5, 0x0ADB0C: 0x2C190000, 0x0ADB14: 0x80BD0000,
    0x0ADB1C: 0xA8DC0000, 0x0ADB28: 0x4BFDFBF9, 0x0ADB30: 0x3880001A,
    0x0ADB34: 0x38A00000, 0x0ADB38: 0x4808B0E9, 0x0ADB4C: 0x80E10040,
    0x0ADB50: 0x4BFDFE61, 0x0ADB54: 0x3CA04E47, 0x0ADB60: 0x38A54248,
    0x0ADB64: 0x38E10040, 0x0ADB6C: 0x4800A845, 0x0ADB74: 0xB3410492,
    0x0ADB80: 0xB0010494, 0x0ADB94: 0x4800AA2D, 0x0ADBAC: 0x80630000,
    0x0ADBB4: 0x80630020, 0x0ADBB8: 0x4808B069, 0x0ADBBC: 0xB35F012A,
    0x0ADBC8: 0xB01F012C,
    # ResetTutorial 0x258350: slot-0x114 dialog (result 3 gate), DoSave,
    # blob + Tutorial.FAM literal, GetString(7)/(0x58), CopyFileA,
    # GetFileAttributesA (INVALID check), SetFileAttributesA(attr & ~1),
    # DoNbhdScreen(true)
    0x25838C: 0x818C0114, 0x258398: 0x2C030003, 0x2583A4: 0x48000FED,
    0x2583DC: 0x8082AE80, 0x2583E0: 0x38600007, 0x2583E8: 0x3BE41621,
    0x2584A8: 0x38600058, 0x258568: 0x80610048, 0x258574: 0x4BDDAB5D,
    0x258578: 0x2C030000, 0x2585AC: 0x4BDD44F5, 0x2585B8: 0x2800FFFF,
    0x2585EC: 0x5484003C, 0x2585F0: 0x4BDD4251, 0x2585F4: 0x2C030000,
    0x25862C: 0x4BFFCF85,
    # cWinOptions::TSOnCommand: command-table slot index dispatch and the
    # ResetTutorial case (slot 0x21)
    0x281C60: 0x7C002850, 0x281C64: 0x7C001670, 0x281C68: 0x7F200195,
    0x281D50: 0x2C190021, 0x281D54: 0x41820D18, 0x282A6C: 0x7FE3FB78,
    0x282A70: 0x4BFD58E1,
    # TryGenericSimCall: operand index, jump table TOC-0x59a0, app+0x24
    # Neighborhood fetch, the three TutorialCompleted call sites
    0x0F1F68: 0xA8050000, 0x0F1F6C: 0x2800002B, 0x0F1F74: 0x8082A660,
    0x0F1F90: 0x80630024, 0x0F1F94: 0x4BFBBA4D, 0x0F1FA8: 0x4BFBBA39,
    0x0F1FBC: 0x4BFBBA25,
    # cDDDSimsView::TSOnKeyDown: CancelTutorial then highlighter HideWindow
    0x217AC8: 0x80790000, 0x217ACC: 0x80630024, 0x217AD0: 0x4BE95E61,
    0x217AD8: 0x8063020C, 0x217AE8: 0x818C00A0,
    # LoadHouse spawner: inhibit flag TOC-0x727c, state < 3 gate, SIMI
    # global-26 (= sim+0x44) gate, GUID 0xc3249a1d literal twice, both GUID
    # lookups, MakeNewOutOfWorldObject; cross-house object sweep fields
    0x0B0540: 0x80628D84, 0x0B0544: 0x80030000, 0x0B0550: 0xA81F012A,
    0x0B0554: 0x2C000003, 0x0B055C: 0xA81B0044, 0x0B0568: 0x3C80C325,
    0x0B0570: 0x38849A1D, 0x0B0574: 0x480370DD, 0x0B0584: 0x3C60C325,
    0x0B0588: 0x38839A1D, 0x0B058C: 0x80650028, 0x0B0590: 0x48029B51,
    0x0B05A0: 0x48036AA1, 0x0B050C: 0xA8050612, 0x0B0524: 0xA88500F0,
    0x0B052C: 0x48036535,
    # SetGlobal layout law: globals array at sim+0x10, u16 stride
    0x138C20: 0x7C800734, 0x138C24: 0x5400083C, 0x138C28: 0x7C630214,
    0x138C2C: 0xB0A30010,
    # GetHouseInfo(int) -> path + tail into inner overload; +0x1c/+0x14 out
    # params for GetHouseFileInfo
    0x232D8C: 0x4BE82365, 0x232C48: 0x38BF001C, 0x232C4C: 0x38DF0014,
    0x232C50: 0x80630000, 0x232C5C: 0x80630024, 0x232C68: 0x4BE7DDD9,
    # GetHouseFileInfo: SIMI constants from TOC-0x7318/-0x731c, temp sim,
    # global reads 54/69/58/53 (sim+0x7c/+0x9a/+0x84/+0x7a), stores to
    # +0x14 (r26) and price (r31)
    0x0B0A84: 0x80A50000, 0x0B0A88: 0xA8C30000, 0x0B0AB0: 0xA801007C,
    0x0B0ABC: 0xA801009A, 0x0B0AC0: 0x901D0000, 0x0B0AC4: 0xA8010084,
    0x0B0AC8: 0x2C000000, 0x0B0ACC: 0x901A0000,
    0x0B0AD4: 0xA801007A, 0x0B0AE4: 0x5460063E,
    0x0B0AEC: 0x901C0000, 0x0B0B40: 0x901F0000,
    # free ImportFamily: nbhd fetch, real ImportFamily call, family 0xfa0 case
    0x231E18: 0x80660024, 0x231E20: 0x4BE7BE31, 0x231E8C: 0x2C1D0FA0,
    # cheat registrations: restore_tut = id 0x2d name blob+0xd47; tutorial =
    # id 0x3d type 2 name blob+0xdea; tutorial-cheat case sets the spawner
    # inhibit at *(TOC-0x727c) to (param == 0): cntlzw+rlwinm booleanize,
    # 'on' -> li r24,1 / 'off' -> li r24,0
    0x251D38: 0x38DE0D47, 0x251D3C: 0x3880002D, 0x251D44: 0x48000AFD,
    0x251FB8: 0x38DE0DEA, 0x251FBC: 0x3880003D, 0x25012C: 0x80628D84,
    0x250130: 0x7C000034, 0x250134: 0x5400DE3E, 0x250138: 0x90030000,
    0x24E62C: 0x3B000001, 0x24E658: 0x3B000000,
    # skeptic pass: ESC (0x1B) is the CancelTutorial key in cDDDSimsView::
    # TSOnKeyDown (cmpwi r30, 0x1b; beq 0x217ac8); DoSave returns 1 on gate
    # failure / non-1-non-4 results, 0 on result-1 abort or failed save, and
    # clears the +0xbf dirty byte after a successful SaveGame; the price
    # float comes from *(TOC-0x7274) = BSS 0x852b0 (r7 reassigned before the
    # lfs); temp Neighborhood ctor at r1+0x368
    0x217610: 0x2C1E001B, 0x217614: 0x418204B4,
    0x259398: 0x3BE00000, 0x259468: 0x3BE00000, 0x259470: 0x41820034,
    0x259438: 0x38600000, 0x259440: 0x20030004, 0x259494: 0x38600000,
    0x25949C: 0x38000000, 0x2594A0: 0x981E00BF, 0x2594A4: 0x38600001,
    0x0B0AF4: 0x80E28D8C, 0x0ADB08: 0x48009539,
}
for address, expected in sorted(pins.items()):
    actual = struct.unpack_from('>I', b, address)[0]
    assert actual == expected, hex(address)


# ---- data-section evidence: packed unpack + TOC-slot words ----
def dw(off):
    return struct.unpack_from('>I', DATA, off)[0]


def slot_word(toc_delta):
    return dw(0x8000 + toc_delta)


def dstr(off):
    return DATA[off:].split(b'\0')[0].decode('latin1')


data_pins = {
    'app_holder_-0x778c': (slot_word(-0x778C), 0x0008510C),
    'app_holder2_-0x7790': (slot_word(-0x7790), 0x00092660),
    'string_env_-0x744c': (slot_word(-0x744C), 0x00042248),
    'strtable_-0x5ad0': (slot_word(-0x5AD0), 0x00045604),
    'blob_-0x5180': (slot_word(-0x5180), 0x000534D8),
    'simi_cc_slot_-0x7318': (slot_word(-0x7318), 0x00044864),
    'simi_ver_slot_-0x731c': (slot_word(-0x731C), 0x00044860),
    'getstring_jt_-0x5bec': (slot_word(-0x5BEC), 0x00043B60),
    'getstring_tbl_-0x5be8': (slot_word(-0x5BE8), 0x00043CE0),
    'genericcall_jt_-0x59a0': (slot_word(-0x59A0), 0x000485A8),
    'cheat_jt_-0x5198': (slot_word(-0x5198), 0x0005323C),
    'cheat_vec_-0x5190': (slot_word(-0x5190), 0x00092648),
    'iff_magic_-0x5bc0': (slot_word(-0x5BC0), 0x00044B48),
}
for name, (actual, expected) in data_pins.items():
    assert actual == expected, (name, hex(actual))

# string-table entries: cancel-tutorial tree name and the completion format
assert dstr(0x45604 + 0xC5) == 'cancel tutorial'
assert dstr(0x45604 + 0xD5) == '%sHouses/House%02d.iff'
# the 'Tutorial.FAM' literal inside the app blob
assert dstr(0x534D8 + 0x1621) == 'Tutorial.FAM'
# SIMI chunk id ('SIMI' as a word at the slot target), version halfword 1,
# and the IFF FILE 2.5 wildcard magic
assert dw(0x44864) == 0x53494D49
assert struct.unpack_from('>H', DATA, 0x44860)[0] == 1
assert DATA[0x44B48:0x44B58] == b'IFF FILE *.*:TYP'
# cheat names for the two tutorial cheats (blob offsets from registrations)
assert dstr(0x534D8 + 0x0D47) == 'restore_tut'
assert dstr(0x534D8 + 0x0DEA) == 'tutorial'
assert dstr(0x534D8 + 0x0D53) == 'crash'  # id 0x2e neighbor sanity pin

# ---- fixtures ----
fixtures = {}


# (a) generic sim call table decode: indices 0/8/9 -> TutorialCompleted(0/1/2)
GEN_JT = 0x485A8
gen = {k: dw(GEN_JT + 4 * k) + 0x8E90 for k in range(0x2C)}
assert gen[0] == 0xF1F88 and gen[8] == 0xF1F9C and gen[9] == 0xF1FB0
assert {gen[k] for k in gen} >= {0xF1F88, 0xF1F9C, 0xF1FB0}
fixtures['generic_calls'] = {
    'law': 'generic sim call operand (s16) -> jump table TOC-0x59a0; '
           'entries 0/8/9 = TutorialCompleted(0/1/2) on the global neighborhood',
    '0': gen[0], '8': gen[8], '9': gen[9],
}

# (b) ResetTutorial path build: FindDataDirectory + GetString(idx) + sep fix
#     + 'Tutorial.FAM'; GetString = inline-jump-table case `addi r6, r5, off`
#     (table data 0x43b60, strings data 0x43ce0)
GS_JT = 0x43B60
GS_TBL = 0x43CE0


def get_string(idx):
    a = dw(GS_JT + 4 * idx) + 0x8E90
    w = struct.unpack_from('>I', b, a)[0]
    assert (w >> 26) == 14 and ((w >> 21) & 31) == 6 and ((w >> 16) & 31) == 5
    return dstr(GS_TBL + (w & 0xFFFF))


assert get_string(7) == 'UserData/'
assert get_string(0x58) == 'UserData/Import/'


def reset_paths(datadir):
    fam = dstr(0x534D8 + 0x1621)

    def join(directory, sub):
        out = directory + sub
        if out[-1:] not in ('/', '\\'):
            out += '/'
        return out + fam

    return join(datadir, get_string(7)), join(datadir, get_string(0x58))


src, dst = reset_paths('/Apps/The Sims/')
assert src == '/Apps/The Sims/UserData/Tutorial.FAM'
assert dst == '/Apps/The Sims/UserData/Import/Tutorial.FAM'
fixtures['reset_paths'] = {
    'law': "ResetTutorial copies <DataDir>UserData/Tutorial.FAM to "
           "<DataDir>UserData/Import/Tutorial.FAM (CopyFileA flag 1 = fail "
           'if destination exists); separator appended only if the buffer '
           "does not already end in '/' or '\\\\'",
    'src': src, 'dst': dst,
}

# (c) file-attribute law: GetFileAttributesA -1 = INVALID; SetFileAttributes
#     receives attr & ~1 (rlwinm clears bit 0 = FILE_ATTRIBUTE_READONLY)
INVALID = 0xFFFFFFFF


def reset_attributes(attr):
    if attr == INVALID:
        return None
    return attr & ~1


assert reset_attributes(INVALID) is None
assert reset_attributes(0x1) == 0
assert reset_attributes(0x21) == 0x20
fixtures['reset_attributes'] = {
    'law': 'attr == 0xFFFFFFFF (INVALID_FILE_ATTRIBUTES) -> failure dialog; '
           'otherwise SetFileAttributesA(attr & ~1) clears READONLY',
    'invalid': hex(INVALID), 'ro_only': hex(reset_attributes(0x1)),
    'ro_plus_archive': hex(reset_attributes(0x21)),
}

# (d) TutorialCompleted state machine (0xad9e0 decode; skeptic-corrected:
#     ONE field pair, Neighborhood object +0x12a/+0x12c, written on BOTH the
#     NGBH-reconstituted temp (r1+0x368; stores land at stack 0x492/0x494)
#     and the live object)


def completed(arg, g26):
    """Returns (nbhd_12a, nbhd_12c, house_file_g26_write, live_g26_write)."""
    nbh_12a = arg + 1                      # sth r26, 0x12a (live) / 0x492(stack)
    nbh_12c = 0                            # sth r0, 0x12c (live) / 0x494(stack)
    house_g26 = 0 if arg == 0 else g26     # SIMI patch, arg==0 only
    live_g26 = 0 if arg == 0 else g26      # live SetGlobal, arg==0 only
    return nbh_12a, nbh_12c, house_g26, live_g26


assert completed(0, 1) == (1, 0, 0, 0)
assert completed(1, 1) == (2, 0, 1, 1)
assert completed(2, 1) == (3, 0, 1, 1)


def spawner_allowed(inhibit, nbh_state, g26, object_present, selector_exists):
    if inhibit:
        return False
    if nbh_state >= 3:
        return False
    if g26 == 0:
        return False
    return not object_present and selector_exists


assert spawner_allowed(0, 0, 1, False, True)
assert not spawner_allowed(0, 3, 1, False, True)   # after TutorialCompleted(2)
assert not spawner_allowed(0, 0, 0, False, True)   # after TutorialCompleted(0)
assert not spawner_allowed(1, 0, 1, False, True)   # `tutorial 1` cheat
assert not spawner_allowed(0, 0, 1, True, True)
fixtures['lifecycle_state'] = {
    'law': 'TutorialCompleted(arg): Neighborhood object +0x12a := arg+1 and '
           '+0x12c := 0 on BOTH the NGBH-reconstituted temp (persisted via '
           'ReconstituteSaveObject) and the live object (0x492/0x494 in the '
           'raw words are stack addresses = r1+0x368 + the object offsets); '
           'sim global 26 cleared (house-file SIMI + live) only for arg 0; '
           'LoadHouse spawns the Tutorial object (GUID 0xc3249a1d, '
           'out-of-world) iff !inhibit && state(+0x12a) < 3 && g26 != 0 && '
           'absent && selector exists',
    'after_arg0': completed(0, 1), 'after_arg1': completed(1, 1),
    'after_arg2': completed(2, 1),
    'spawn_states': [st for st in range(5)
                     if spawner_allowed(0, st, 1, False, True)],
    'guid': '0xc3249a1d',
    'cheat_polarity': 'inhibit = (param == 0): tutorial on/1 -> spawn '
                      'allowed, tutorial off/0 -> spawn disabled',
}

# (e) HouseInfo fill law (GetHouseFileInfo): fields from the temp sim's
#     globals array (base sim+0x10, u16 each)


def global_offset(index):
    return 0x10 + 2 * index


assert global_offset(26) == 0x44     # the spawner/spawn-latch global
assert global_offset(53) == 0x7A
assert global_offset(54) == 0x7C
assert global_offset(58) == 0x84
assert global_offset(69) == 0x9A


def fill_house_info(g):
    return {
        'flag10': 1 if (g[58] == 0 and g[53] == 0) else 0,
        'tutorial14': g[58],
        'built18': g[54],
        'g20': g[69],
    }


assert fill_house_info({53: 0, 54: 1, 58: 0, 69: -1}) == {
    'flag10': 1, 'tutorial14': 0, 'built18': 1, 'g20': -1}
assert fill_house_info({53: 0, 54: 0, 58: 1, 69: 0}) == {
    'flag10': 0, 'tutorial14': 1, 'built18': 0, 'g20': 0}
fixtures['house_info_fill'] = {
    'law': 'GetHouseFileInfo reconstitutes the house SIMI chunk into a temp '
           'cSimulator; HouseInfo+0x14 = (s16)g58 (the tutorial-lot flag), '
           '+0x18 = g54, +0x20 = g69, +0x10 = (g58==0 && g53==0); +0x1c '
           'price is the float-scaled 64-bit adjust (see decode.md)',
    'tutorial_house': fill_house_info({53: 0, 54: 0, 58: 1, 69: 0}),
    'normal_house': fill_house_info({53: 0, 54: 1, 58: 0, 69: -1}),
}

# (f) move-in refusal ordering (r197 handler + this round's flag origin)
MOVEIN_STRS = {'tutorial': (16, 17), 'family': None, 'cant_afford': (2, 3),
               'occupied': (14, 15), 'house_confirm': (0, 1), 'lot_confirm': (6, 7)}


def movein_guard(lot):
    """Returns the STR#132 pair to show, or the confirm pair on success."""
    if lot['tutorial'] != 0:                      # +0x14 != 0
        return MOVEIN_STRS['tutorial']
    if not lot['family_ok']:
        return None                               # silent SetMode(0) exit
    if lot['built']:
        if lot['occupied']:
            return MOVEIN_STRS['occupied']
        if lot['price'] > lot['budget']:
            return MOVEIN_STRS['cant_afford']
        return MOVEIN_STRS['house_confirm']
    if lot['price'] > lot['budget']:
        return MOVEIN_STRS['cant_afford']
    return MOVEIN_STRS['lot_confirm']


assert movein_guard({'tutorial': 1, 'built': True, 'occupied': True,
                     'price': 0, 'budget': 999999, 'family_ok': True}) == (16, 17)
assert movein_guard({'tutorial': 0, 'built': True, 'occupied': True,
                     'price': 0, 'budget': 999999, 'family_ok': True}) == (14, 15)
assert movein_guard({'tutorial': 0, 'built': True, 'occupied': False,
                     'price': 50777, 'budget': 20000, 'family_ok': True}) == (2, 3)
assert movein_guard({'tutorial': 0, 'built': True, 'occupied': False,
                     'price': 10206, 'budget': 20000, 'family_ok': True}) == (0, 1)
fixtures['movein_order'] = {
    'law': 'guard order tutorial(+0x14=g58) -> family-valid -> occupied -> '
           'price; tutorial refusal = STR#132[16]/[17] and comes FIRST',
    'sample_r197_truth': {'house2_bin_family': (2, 3),
                          'house6_bin_family': (0, 1)},
}

# (g) cancel-tutorial law: named tree + KillObject(owner->+0xf0), then the
#     caller hides the highlighter (winmgr+0x20c, slot 0xa0)
fixtures['cancel'] = {
    'law': "owner.runTree('cancel tutorial'); KillObject((s16)owner->+0xf0); "
           'caller then hides the cTSWinTutorialHighlight at winmgr+0x20c',
    'tree_name': dstr(0x45604 + 0xC5),
    'highlighter_hide_slot': '0xa0',
}

result = {
    'executable_sha256': sha,
    'pin_count': len(pins),
    'data_pin_count': len(data_pins),
    'pins': {hex(k): hex(v) for k, v in sorted(pins.items())},
    'data_pins': {k: hex(v[0]) for k, v in data_pins.items()},
    'getstring_samples': {hex(k): get_string(k) for k in (0, 7, 0xE, 0x58)},
    'fixtures': fixtures,
}
(HERE / 'verified-tut-lifecycle.json').write_text(json.dumps(result, indent=2) + '\n')
print(f'{len(pins)} instruction words, {len(data_pins)} data pins and '
      f'{len(fixtures)} fixture groups verified')
