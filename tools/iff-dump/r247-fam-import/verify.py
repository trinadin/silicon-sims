"""Pin R247 family-import decode; reads owner executable + UserData fixtures only.

Pins the instruction words of the FAMILY-IMPORT machinery (the consumer of the
Options "Reset Tutorial" flow): cWinNeighborhoodVC::CheckForNewImports (the
Tutorial.FAM auto-import, timer + Init cadence), CycleThroughImports,
GetImportInfoForFile, free ImportFamily, Neighborhood::ImportFamily (the
chunk law: EXPi/CTSS/NBRS/FINV/SIMI/uChr/Gtab, character creation, house
file .tmp shuffle), Family::LoadFamily (FAMI/FAMs/FAMh), AddNewCharacter
(template GUIDs, Characters/User%05d.iff), and the GUIDTranslation 'Gtab'
chunk — plus the skeptic-correction sites C1–C7 (RelMatrix old/new row
wiring, timer +0x16c gate, one-arg wildcard-in-path scan call, ctor +0x188
seed, id-assign-before-null-check, evicted-export guard form, guard-fail
order). Data pins: the neighborhood-UI globals blob, the strtable literals,
the GetString entries, and the function-record names.

Fixtures BIND TO THE PINNED BINARY WORDS (no pure-Python restatements): a
mini PPC interpreter re-executes the pinned AddNewCharacter branch tree to
derive the type→template-GUID table; instruction-field decoders re-derive
the uChr 'dog'/'cat'/human type dispatch (gate mask included), the C1
RelMatrix old→new register wiring (incl. the n==0 RemoveArray branch), the
C3 guard ordering (failure exits before Save; mutations precede guards),
and the C4/C5 one-arg scan call + timer gate; the file fixtures re-parse
UserData/Tutorial.FAM and prove byte identity of every common chunk with
the pristine UserData/Houses/House07.iff and the little-endian EXPi/FAMI
field decodes.
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

# ---- instruction pins: address -> expected big-endian word (file offsets) ----
pins = {
    # CheckForNewImports__18cWinNeighborhoodVCFv (0x46f460): +0x190 latch,
    # 'UserData/Import/' = GetString(0x58), strlen helper, globals-blob
    # pattern '*.FAM' at +0x9c, the C5 one-arg scan call (wildcard appended
    # to the path; no r4 setup between the append bl and the scan bl),
    # 'Tutorial.FAM' at +0xa2, FileExists gate, GetImportInfoForFile + free
    # ImportFamily, failure dialog, refresh trio, ImportInfo stack frame
    0x46F47C: 0x88030190, 0x46F484: 0x408201E0, 0x46F49C: 0x38600058,
    0x46F4BC: 0x387F009C, 0x46F4CC: 0x389F009C, 0x46F4D0: 0x4BC14EE1,
    0x46F4D4: 0x38610040, 0x46F4D8: 0x4803AB39, 0x46F4F0: 0x98650160,
    0x46F500: 0x481334E1, 0x46F53C: 0x387F00A2, 0x46F558: 0x4803F389,
    0x46F5A0: 0x4BCD2C91, 0x46F5A4: 0x38610160, 0x46F5B8: 0x4BDC2A19,
    0x46F5CC: 0x4BDC2825, 0x46F5D8: 0x88010270, 0x46F5E8: 0x800102B0,
    0x46F610: 0x4BDE48F1, 0x46F620: 0x4BDDCB71, 0x46F634: 0x4BDE5F7D,
    # cadence (C4): TSOnTimerMsg unsubscribes, gates on win+0x16c == 0
    # (else DoInternetUpdate and exit past the re-arm), +0x191 first-tick
    # latch; Init call; screen activate clears +0x190/+0x191
    0x472CE4: 0x480A947D, 0x472CE8: 0x881D016C, 0x472CEC: 0x28000000,
    0x472CF0: 0x41820020, 0x472CF8: 0x4BFFBB49, 0x472D0C: 0x4800018C,
    0x472D10: 0x881D0191, 0x472D18: 0x41820158,
    0x472E74: 0x4BFFC5ED, 0x472E80: 0x4BC39971, 0x472E8C: 0x38A003E8,
    0x472E90: 0x38C00000, 0x4749B4: 0x4BFFAAAD,
    0x4740FC: 0x981F0190, 0x474108: 0x981F0191,
    # free ImportFamily__FP10ImportInfoPi (0x231df0): nbhd = *(app)->+0x24,
    # real ImportFamily call, famB id read, family id 0xfa0 (4000) case,
    # GetString(0x57)='Export/' + ConstructUserDataPath + ExportFamily for
    # both the imported and the evicted family, *out = new family id
    0x231E18: 0x80660024, 0x231E20: 0x4BE7BE31, 0x231E3C: 0x8363010C,
    0x231E8C: 0x2C1D0FA0, 0x231EB4: 0x38600057, 0x231EC4: 0x4802A14D,
    0x231EE4: 0x4BE7D2BD, 0x231EF0: 0x901A0000, 0x231F6C: 0x4BE7D235,
    # GetImportInfoForFile (0x231fd0): open/validate, ExpFamilyInfo load,
    # temp Family load, name/networth/magicoins fills, GetHouseInfo inner,
    # -2 at +0x138 when house chunks present, +0x150 rewrite, path copy
    0x232000: 0x4BE65171, 0x232008: 0x4BE646C9, 0x23202C: 0x4BE854D5,
    0x232038: 0x4BE85459, 0x23206C: 0x4BE44F45, 0x23207C: 0x4BE44765,
    0x2320CC: 0x4BE44B85, 0x2320D8: 0x4833F369, 0x2320E0: 0x4BE44A71,
    0x2320FC: 0x4BE44965, 0x2321E8: 0x480002C9, 0x232200: 0x901E0138,
    0x232208: 0x901E0150, 0x232214: 0x4BF0FEFD,
    # ImportInfo ctor/dtor (C6): HouseInfo at +0x114; member array ptr at
    # +0x194; +0x110 byte; +0x188 seeded from app->+0x64
    0x231D40: 0x909E0114, 0x231D90: 0x901E0194, 0x231D94: 0x981E0110,
    0x231D9C: 0x88040064, 0x231DA0: 0x981E0188, 0x231C24: 0x83FD0194,
    # Neighborhood::ImportFamily (0xadc50): chunk ids 'EXPi' 0x45585069,
    # 'CTSS' 0x43545353 v1000, 'NBRS' 0x4e425253, 'FINV' 0x46494e56,
    # 'uChr' 0x75436872; SIMI recon + global-26 read (sim+0x44);
    # MakeNewFamily; out-write + id assign BEFORE the null check (C6);
    # name/money/handle move; eviction (+0x110/+0x13c scans, +0xfa delete);
    # uChr string 13 ('dog'/'cat' strtable literals, app+0x66 bit gate);
    # AddNewCharacter types 1/2/0; new id + AddMember; FINV carry; STR# 200
    # clone; CTSS pid+2000; object rename with ' - '; Gtab; RelMatrix
    # old->new wiring (C1); persistent fields; fixups; guards; flags;
    # nbhd+0x12c/+0x12e latch; type-attr sweep; '.tmp' shuffle; Save;
    # portrait pass
    0x0ADC80: 0x90050000, 0x0ADC88: 0x90060000,   # both out-params zeroed
    0x0ADCC8: 0x3CA04558, 0x0ADCDC: 0x38A55069, 0x0ADD84: 0x3CA04354,
    0x0ADD90: 0x38C55353, 0x0ADD94: 0x38A003E8, 0x0ADDE0: 0x3CA04E42,
    0x0ADDEC: 0x38A55253, 0x0ADE54: 0x3CA04649, 0x0ADE60: 0x38A54E56,
    0x0ADEA4: 0x4BFDF87D, 0x0ADEB0: 0xA861049C, 0x0ADED4: 0x4800602D,
    0x0ADF1C: 0x28060000, 0x0ADF20: 0x4082000C, 0x0ADF24: 0x80700000,
    0x0ADF28: 0x9083010C, 0x0ADF2C: 0x80100000,
    0x0ADFA8: 0x4BFC8CA9, 0x0ADFB4: 0x4BFC835D,
    0x0AE048: 0x80050110, 0x0AE05C: 0x8005013C, 0x0AE094: 0x90050110,
    0x0AE0E4: 0xA80400FA, 0x0AE0F4: 0x4800441D,
    0x0AE12C: 0x3FE07543, 0x0AE14C: 0x38DF6872, 0x0AE170: 0x3880000D,
    0x0AE178: 0x48096989, 0x0AE184: 0x388E00EC, 0x0AE188: 0x484EEEE9,
    0x0AE198: 0xA8030066, 0x0AE19C: 0x540006B5, 0x0AE1A0: 0x40820024,
    0x0AE1D0: 0x38A00001, 0x0AE1D4: 0x4800559D, 0x0AE1EC: 0x388E00F0,
    0x0AE204: 0x540006B5, 0x0AE208: 0x40820024, 0x0AE238: 0x38A00002,
    0x0AE23C: 0x48005535, 0x0AE254: 0x38A00000, 0x0AE258: 0x48005519,
    0x0AE2CC: 0xB0150000, 0x0AE2DC: 0x4BFC7F05, 0x0AE2F8: 0x9081004C,
    0x0AE344: 0x483537CD, 0x0AE360: 0x4BF87FC1, 0x0AE3B8: 0x480964D9,
    0x0AE3D0: 0x48096B41, 0x0AE3F8: 0x38C55223, 0x0AE410: 0x48097941,
    0x0AE43C: 0x380507D0, 0x0AE4E4: 0x4BFAEEED, 0x0AE510: 0x4BFAF001,
    0x0AE578: 0x48054859, 0x0AE588: 0x48093CA9, 0x0AE5A8: 0x48056C29,
    0x0AE5B4: 0x4805440D, 0x0AE620: 0x4BFDBA21, 0x0AE6DC: 0x4BFDC775,
    # C1 — RelMatrix copy (0xae6e0..0xae780): setup words fix the register
    # roles (r20 = FAM-record matrix = r17+0xc; r22 = live matrix =
    # r19+0xc; r29 = newIds array; r30 = efi.ids array; r23 = lha newIds,
    # r21 = lha efi.ids); GetArraySize/GetValue take (r20, r21) = FAM side
    # with the OLD id; RemoveArray/SetArraySize/SetValue take (r22, r23) =
    # live side with the NEW id; n==0 -> RemoveArray
    0x0AE650: 0x80610060, 0x0AE658: 0x7E23002E,   # FAM record = NBRS[id-1]
    0x0AE688: 0x806F0150, 0x0AE690: 0x7E63002E,   # live = nbhd chars[new-1]
    0x0AE6E0: 0x3AD3000C, 0x0AE6E4: 0x3A91000C,
    0x0AE6E8: 0x3BA10070, 0x0AE6EC: 0x3BC100A4,
    0x0AE6F8: 0xAAFD0000, 0x0AE6FC: 0x38740000, 0x0AE700: 0x38950000,
    0x0AE704: 0x480722FD, 0x0AE708: 0x7C781B79, 0x0AE70C: 0x40820014,
    0x0AE710: 0x38760000, 0x0AE714: 0x38970000, 0x0AE718: 0x480720D9,
    0x0AE71C: 0x48000050, 0x0AE720: 0x38760000, 0x0AE724: 0x38970000,
    0x0AE728: 0x38B80000, 0x0AE72C: 0x48071D85, 0x0AE730: 0x3B200000,
    0x0AE73C: 0x38740000, 0x0AE740: 0x38950000, 0x0AE744: 0x38B90000,
    0x0AE748: 0x48071B99, 0x0AE74C: 0x38C30000, 0x0AE750: 0x38760000,
    0x0AE754: 0x38970000, 0x0AE758: 0x38B90000, 0x0AE75C: 0x48071A95,
    0x0AE760: 0x3B390001, 0x0AE764: 0x7C19C000, 0x0AE780: 0xAABE0004,
    0x0AE798: 0x4BFF5E69, 0x0AE814: 0x4BFDB7BD, 0x0AE890: 0x480027C1,
    0x0AE898: 0x48006E49,
    # C3 — guards and their failure exits (all before the Save site at
    # 0xaecbc; failure converges at 0xae9c4 and returns via 0xaea14)
    0x0AE8F0: 0x2C000001, 0x0AE8F4: 0x4182000C, 0x0AE8F8: 0x3A20FFFF,
    0x0AE8FC: 0x480000C8, 0x0AE998: 0x28060000, 0x0AE99C: 0x4182000C,
    0x0AE9A0: 0x3A20FFFF, 0x0AE9A4: 0x48000020, 0x0AE9C4: 0x7E200735,
    0x0AE9C8: 0x41820050, 0x0AEA10: 0x7E238B78, 0x0AEA14: 0x48000404,
    0x0AE9A8: 0x93430110, 0x0AEA48: 0x88010B58, 0x0AEA54: 0xB34F012C,
    0x0AEA5C: 0xB00F012E, 0x0AEA74: 0xB00F012C, 0x0AEA90: 0x4802B7E1,
    0x0AEADC: 0x4804D985, 0x0AEB80: 0x388E00D5, 0x0AEB84: 0x484EB79D,
    0x0AEBA4: 0x388E00F8, 0x0AEBBC: 0x4849B535, 0x0AEC24: 0x4849B4CD,
    0x0AEC3C: 0x4849B4B5, 0x0AEC94: 0x4849B74D, 0x0AECA8: 0x4849B739,
    0x0AECBC: 0x48006765, 0x0AED38: 0x48183FF9, 0x0AED48: 0x2C000044,
    0x0AED78: 0x48184B19, 0x0AED9C: 0x48196755,
    # AddNewCharacter (0xb3770): the full type-dispatch branch tree (C8:
    # re-executed by the fixture below), '.iff'/Characters/ strtable
    # offsets, AddNewNeighbor
    0x0B3780: 0x2C050001, 0x0B37A8: 0x4182002C, 0x0B37AC: 0x40800010,
    0x0B37B0: 0x2C050000, 0x0B37B4: 0x40800014, 0x0B37B8: 0x48000034,
    0x0B37BC: 0x2C050003, 0x0B37C0: 0x4080002C, 0x0B37C4: 0x4800001C,
    0x0B37C8: 0x3C607FD9, 0x0B37CC: 0x3AC36B54, 0x0B37D0: 0x48000024,
    0x0B37D4: 0x3C604A71, 0x0B37D8: 0x3AC3DF92, 0x0B37DC: 0x48000018,
    0x0B37E0: 0x3C607BEA, 0x0B37E4: 0x3AC30977, 0x0B37E8: 0x4800000C,
    0x0B37EC: 0x3C607FD9, 0x0B37F0: 0x3AC36B54, 0x0B37F4: 0x2C160000,
    0x0B38F4: 0x389A000A, 0x0B3910: 0x389A000F, 0x0B3A54: 0x480012FD,
    # Family::LoadFamily (0x767e0): id -> Family+0x10c, 'FAMI' recon,
    # 'FAMs' stringset, 'FAMh' handle
    0x076810: 0x93DC010C, 0x076818: 0x38A44D49, 0x076828: 0x48000899,
    0x076860: 0x38C54D73, 0x0769B8: 0x38844D68,
    # GUIDTranslation::Load 'Gtab' 0x47746162; CTGFileManager holder
    0x08A044: 0x3CA04774, 0x08A04C: 0x38A56162, 0x550000: 0x80629F9C,
}
for address, expected in sorted(pins.items()):
    actual = struct.unpack_from('>I', b, address)[0]
    assert actual == expected, (hex(address), hex(actual), hex(expected))

# ---- data-section evidence ----
def dw(off):
    return struct.unpack_from('>I', DATA, off)[0]


def slot_word(toc_delta):
    return dw(0x8000 + toc_delta)


def dstr(off):
    return DATA[off:].split(b'\0')[0].decode('latin1')


data_pins = {
    'nbhd_ui_globals_-0x46a4': (slot_word(-0x46A4), 0x0006D1C0),
    'strtable_-0x5ad0': (slot_word(-0x5AD0), 0x00045604),
    'app_holder_-0x778c': (slot_word(-0x778C), 0x0008510C),
    'string_env_-0x744c': (slot_word(-0x744C), 0x00042248),
    'simi_cc_slot_-0x7318': (slot_word(-0x7318), 0x00044864),
    'simi_ver_slot_-0x731c': (slot_word(-0x731C), 0x00044860),
    'filemanager_holder_-0x6064': (dw(0x8000 - 0x6064), 0x00097950),
}
for name, (actual, expected) in data_pins.items():
    assert actual == expected, (name, hex(actual), hex(expected))

ST = 0x45604
str_pins = {
    'user5d': (0x01, 'User%05d'),
    'dot_iff': (0x0A, '.iff'),
    'characters_dir': (0x0F, 'Characters/'),
    'fam_pattern': (0x38, '*.FAM'),
    'townie': (0x57, 'Townie'),
    'house_fmt': (0xD5, '%sHouses/House%02d.iff'),
    'dog': (0xEC, 'dog'),
    'cat': (0xF0, 'cat'),
    'name_sep': (0xF4, ' - '),
    'tmp_suffix': (0xF8, '.tmp'),
    'fam_ext': (0x100, '.FAM'),
}
for name, (off, text) in str_pins.items():
    assert dstr(ST + off) == text, (name, dstr(ST + off))

GLOB = 0x6D1C0
glob_pins = {
    'dollar_family': (0x94, '$family'),
    'star_fam': (0x9C, '*.FAM'),
    'tutorial_fam': (0xA2, 'Tutorial.FAM'),
    'import_fail_msg': (0xAF, "Couldn't import the family"),
    'error_caption': (0xCA, 'Error'),
}
for name, (off, text) in glob_pins.items():
    assert dstr(GLOB + off) == text, (name, dstr(GLOB + off))

GS_JT, GS_TBL = 0x43B60, 0x43CE0


def get_string(idx):
    a = dw(GS_JT + 4 * idx) + 0x8E90
    w = struct.unpack_from('>I', b, a)[0]
    assert (w >> 26) == 14 and ((w >> 21) & 31) == 6 and ((w >> 16) & 31) == 5
    return dstr(GS_TBL + (w & 0xFFFF))


assert get_string(7) == 'UserData/'
assert get_string(0x57) == 'Export/'
assert get_string(0x58) == 'UserData/Import/'


def fn_records():
    recs = {}
    mark = struct.pack('>I', 0x00092041)
    i = 0x8E90
    while True:
        j = b.find(mark, i, 0x5C22E8)
        if j < 0:
            return recs
        size = struct.unpack_from('>I', b, j + 8)[0]
        ln = b[j + 13]
        if 0 < ln < 80:
            try:
                nm = b[j + 14:j + 14 + ln].decode('ascii')
                start = j + 8 - size - 12
                if nm.startswith('.') and start >= 0x8E90:
                    recs[start] = (nm, size)
            except UnicodeDecodeError:
                pass
        i = j + 4


recs = fn_records()
rec_pins = {
    0x0ADC50: '.ImportFamily__12NeighborhoodFRC16StackString<260>PP6FamilyPP6Family',
    0x231DF0: '.ImportFamily__FP10ImportInfoPi',
    0x231FD0: '.GetImportInfoForFile__FRC16StackString<260>P10ImportInfoi',
    0x46F460: '.CheckForNewImports__18cWinNeighborhoodVCFv',
    0x4704A0: '.CycleThroughImports__18cWinNeighborhoodVCFv',
    0xAF1A0: '.ExportFamily__12NeighborhoodFRC16StackString<260>P6Family',
    0xB1050: '.UpdateFamilyFriendsCount__12NeighborhoodFP6Family',
    0x233890: '.GetFamilyMembers__FiRQ23std30vector<i,Q23std12allocator<i>>',
    0xB8250: '.ReconLoadObject<13ExpFamilyInfo>__FP13ExpFamilyInfoP8iResFilelsPl_s',
    0x770C0: '.ReconLoadObject<6Family>__FP6FamilyP8iResFilelsPl_s',
    0x54A0F0: '.MoveFileA__14CTGFileManagerFPCcPCc',
    0x54A3E0: '.DeleteFileA__14CTGFileManagerFPCc',
    0x4AE8E0: '.FileExists__7cTSFileFRC9cTSString',
    0x4AA010: '.DoesAnyEntryExistThatMatchesPattern__12cTSDirectoryFRC9cTSString',
    0x8A040: '.Load__15GUIDTranslationFP8iResFile',
    0x89FD0: '.Save__15GUIDTranslationFP8iResFile',
}
for addr, name in rec_pins.items():
    assert recs.get(addr, (None,))[0] == name, (hex(addr), recs.get(addr))

# ---- instruction-field decoders (used by the binary-bound fixtures) ----
def word(addr):
    return struct.unpack_from('>I', b, addr)[0]


def dform(w):
    """(op, rD, rA, SIMM) for D-form words (SIMM sign-extended)."""
    simm = (w & 0xFFFF) - 0x10000 if w & 0x8000 else w & 0xFFFF
    return (w >> 26, (w >> 21) & 31, (w >> 16) & 31, simm)


def bc_target(w, pc):
    bd = w & 0xFFFC
    if bd & 0x8000:
        bd -= 0x10000
    return pc + bd


def b_target(w, pc):
    bd = w & 0x03FFFFFC
    if bd & 0x02000000:
        bd -= 0x04000000
    return pc + bd


def rlwinm_value_mask(w):
    """Mask VALUE of rlwinm rA,rS,0,MB,ME (bit m counts from the MSB, so
    bit m has value 2**(31-m)). Only the SH=0 extract form occurs here."""
    sh = (w >> 11) & 31
    mb = (w >> 6) & 31
    me = (w >> 1) & 31
    assert sh == 0
    bits = list(range(mb, 32)) + list(range(me + 1)) if mb > me \
        else list(range(mb, me + 1))
    mask = 0
    for m in bits:
        mask |= 1 << (31 - m)
    return mask


# ---- fixtures (C8: every fixture binds to pinned binary words or files) ----
fixtures = {}

# (a) AddNewCharacter type->template-GUID selector: RE-EXECUTE the pinned
# branch tree at 0xb3780..0xb37f4 with a mini PPC interpreter (cmpwi / bc /
# b / lis / addi; all other words are prologue nops). The GUID constants are
# never hardcoded into the fixture input — they are read out of the executed
# lis/addi pairs.
def exec_addnewchar_selector(char_type):
    regs = {5: char_type, 22: None, 3: None}
    cr0 = None
    pc = 0x0B3780
    for _ in range(100):
        if pc == 0x0B37F4:            # merge point: selection complete
            return regs[22]
        w = word(pc)
        op = w >> 26
        if op == 11:                  # cmpwi rA, SIMM -> cr0
            _, _, rA, simm = dform(w)
            v = regs.get(rA, 0)
            cr0 = 'LT' if v < simm else 'GT' if v > simm else 'EQ'
            pc += 4
        elif op == 16:                # bc
            bo = (w >> 21) & 31
            bi = (w >> 16) & 31
            assert not bo & 0x10, 'CTR branch inside the selector tree'
            want = bool(bo & 0x08)
            bitname = ('LT', 'GT', 'EQ', 'SO')[bi]
            take = cr0 is not None and ((cr0 == bitname) == want)
            pc = bc_target(w, pc) if take else pc + 4
        elif op == 18:                # b
            pc = b_target(w, pc)
        elif op == 15:                # lis
            _, rD, _, simm = dform(w)
            regs[rD] = simm << 16
            pc += 4
        elif op == 14:                # addi (rA=0 -> li)
            _, rD, rA, simm = dform(w)
            regs[rD] = (regs.get(rA, 0) if rA else 0) + simm
            pc += 4
        else:                         # prologue loads/moves: irrelevant here
            pc += 4
    raise AssertionError('branch tree did not converge')


derived_guids = {t: exec_addnewchar_selector(t) for t in (-1, 0, 1, 2, 3, 5)}
assert derived_guids[0] == 0x7FD96B54        # human
assert derived_guids[1] == 0x4A70DF92        # dog (lis 0x4a71 + addi -0x206e)
assert derived_guids[2] == 0x7BEA0977        # cat
assert derived_guids[3] == 0x7FD96B54        # >=3 falls back to human
assert derived_guids[5] == 0x7FD96B54
assert derived_guids[-1] == 0x7FD96B54       # negative falls back to human
assert len({derived_guids[0], derived_guids[1], derived_guids[2]}) == 3
fixtures['addnewchar_template_selector'] = {
    'law': 'type dispatch re-executed from the pinned words at '
           '0xb3780..0xb37f4: 0->0x7fd96b54 (human), 1->0x4a70df92 (dog), '
           '2->0x7bea0977 (cat), everything else -> human',
    'derived': {str(k): hex(v) for k, v in derived_guids.items()},
}

# (b) uChr string-13 type dispatch: decode the pinned words — GetString
# index, the two strtable offsets joined to their literals, the pets-gate
# rlwinm bit, the gate branch targets, and the type immediates at the three
# AddNewCharacter call sites.
gs13 = dform(word(0x0AE170))
off_dog = dform(word(0x0AE184))[3]
off_cat = dform(word(0x0AE1EC))[3]
assert gs13[0] == 14 and gs13[3] == 13          # GetString(13)
assert dstr(ST + off_dog) == 'dog'
assert dstr(ST + off_cat) == 'cat'
gate_mask = rlwinm_value_mask(word(0x0AE19C))
assert gate_mask == rlwinm_value_mask(word(0x0AE204)) == 0x20
gate1_taken = bc_target(word(0x0AE1A0), 0x0AE1A0)         # -> 0xae1c4
gate2_taken = bc_target(word(0x0AE208), 0x0AE208)         # -> 0xae22c
type_dog = dform(word(0x0AE1D0))[3]
type_cat = dform(word(0x0AE238))[3]
type_other = dform(word(0x0AE254))[3]
assert gate1_taken <= 0x0AE1D0 < gate1_taken + 0x20
assert gate2_taken <= 0x0AE238 < gate2_taken + 0x20
assert (type_dog, type_cat, type_other) == (1, 2, 0)
fixtures['uchr_type_dispatch'] = {
    'law': "uChr string 13 decoded from 0xae170 (=13); 'dog' @strtable+0xec "
           '-> AddNewCharacter type 1, cat @+0xf0 -> type 2, else 0; the '
           'pets gate extracts (s16)app+0x66 bit value 0x20 (rlwinm '
           '0x1a,0x1a decoded from 0xae19c/0xae204)',
    'dispatch': {'dog': type_dog, 'cat': type_cat, '<other>': type_other},
    'gate_mask': hex(gate_mask),
}

# (c) C1 — RelMatrix copy: decode the register wiring from the pinned words.
# Roles: r17 = FAM record (NBRS[id-1]), r19 = live neighbor (chars[new-1]),
# r20 = rec->+0xc, r22 = live->+0xc, r21 = efi.ids[j] (OLD), r23 = newIds[j]
# (NEW).
def arg_regs(site, count):
    """Register numbers of the `count` addi rX, rY, 0 (mr-style) words
    immediately preceding the bl at `site` (arg setup for the callee)."""
    out = []
    pc = site - 4
    while len(out) < count:
        w = word(pc)
        op, _rD, rA, simm = dform(w)
        if op == 14 and rA != 0 and simm == 0:
            out.append(rA)            # addi rD, rA, 0  == register copy
        elif op == 14 and rA == 0:
            out.append(('imm', simm))
        pc -= 4
    return tuple(reversed(out))


fam_mat = dform(word(0x0AE6E4))    # addi r20, r17, 0xc
live_mat = dform(word(0x0AE6E0))   # addi r22, r19, 0xc
new_arr = dform(word(0x0AE6E8))    # addi r29, r1, 0x70
old_arr = dform(word(0x0AE6EC))    # addi r30, r1, 0xa4
assert (fam_mat[1], fam_mat[2], fam_mat[3]) == (20, 17, 0xC)
assert (live_mat[1], live_mat[2], live_mat[3]) == (22, 19, 0xC)
assert (new_arr[1], new_arr[2], new_arr[3]) == (29, 1, 0x70)
assert (old_arr[1], old_arr[2], old_arr[3]) == (30, 1, 0xA4)
new_row = dform(word(0x0AE6F8))    # lha r23, 0(r29)   -> NEW id
old_row = dform(word(0x0AE780))    # lha r21, 4(r30)   -> OLD id
assert (new_row[1], new_row[2], new_row[3]) == (23, 29, 0)
assert (old_row[1], old_row[2], old_row[3]) == (21, 30, 4)
# record/live provenance: lwzx from NBRS holder array (r1+0x60 field) vs the
# live character array (nbhd+0x150)
assert dform(word(0x0AE650))[3] == 0x60 and dform(word(0x0AE658))[0] == 31
assert dform(word(0x0AE688))[3] == 0x150 and dform(word(0x0AE690))[0] == 31
gas = arg_regs(0x0AE704, 2)        # GetArraySize(rec.matrix, OLD)
getv = arg_regs(0x0AE748, 3)       # GetValue(rec.matrix, OLD, k)
rem = arg_regs(0x0AE718, 2)        # RemoveArray(live.matrix, NEW)
sas = arg_regs(0x0AE72C, 3)        # SetArraySize(live.matrix, NEW, n)
setv = arg_regs(0x0AE75C, 3)       # SetValue(live.matrix, NEW, k, v)
assert gas == (20, 21) and getv[:2] == (20, 21)   # FAM side, OLD id
assert rem == (22, 23) and setv[:2] == (22, 23)   # live side, NEW id
assert sas[:2] == (22, 23)
assert sas[2] == 24 and getv[2] == 25 and setv[2] == 25  # n / loop index
# n==0 branch: or. r24, r3, r3; bne -> else (set); fallthrough RemoveArray
n_zero = word(0x0AE70C)
assert (n_zero >> 26) == 16 and bc_target(n_zero, 0x0AE70C) == 0x0AE720
assert word(0x0AE718) == 0x480720D9               # bl RemoveArray
fixtures['relmatrix_law'] = {
    'law': 'C1: GetArraySize/GetValue read the FAM record matrix (r20 = '
           'NBRS[id-1]->+0xc) with the OLD FAM id (r21 = lha 4(efi.ids)); '
           'RemoveArray/SetArraySize/SetValue target the live neighbor '
           'matrix (r22 = chars[new-1]->+0xc) with the NEW id (r23 = lha '
           '0(newIds)); n==0 falls through to RemoveArray(live, new); the '
           'pair loop resets to the array bases each outer iteration, so '
           'EVERY member receives every imported relationship row',
    'fam_side_regs': list(gas), 'live_side_regs': list(rem),
}

# (d) C3 — guard ordering and the mutation leak: decode the guard branches,
# the failure convergence, and the Save-site position.
lot_ok = bc_target(word(0x0AE8F4), 0x0AE8F4)       # beq -> proceed
lot_fail = b_target(word(0x0AE8FC), 0x0AE8FC)      # b -> 0xae9c4
claim_ok = bc_target(word(0x0AE99C), 0x0AE99C)
claim_fail = b_target(word(0x0AE9A4), 0x0AE9A4)
fail_conv = lot_fail
assert claim_fail == fail_conv == 0x0AE9C4
assert lot_ok == 0x0AE900 and claim_ok == 0x0AE9A8
assert word(0x0AE8F8) == word(0x0AE9A0) == 0x3A20FFFF   # li r17, -1 (both)
exit_branch = bc_target(word(0x0AE9C8), 0x0AE9C8)       # beq -> success side
fail_ret = b_target(word(0x0AEA14), 0x0AEA14)           # -> 0xaee18 epilogue
assert exit_branch == 0x0AEA18 and fail_ret == 0x0AEE18
# save-site scan: exactly one bl to Save__12NeighborhoodFl (0xb5420)
save_sites = []
pc = 0x0ADC50
while pc < 0x0AEE2C:
    w = word(pc)
    if w >> 26 == 18 and (w & 1) and b_target(w, pc) == 0x0B5420:
        save_sites.append(pc)
    pc += 4
assert save_sites == [0x0AECBC]
# the leak ordering: delete/mutate/create all precede the guards; the
# failure leg jumps from 0xaea14 STRAIGHT to the shared epilogue at
# 0xaee18, spanning past the Save site — Save is unreachable on failure
mutation_sites = (0x0AE0F4, 0x0AE094, 0x0AE1D4, 0x0AE23C, 0x0AE258)
guard_sites = (0x0AE8F0, 0x0AE998)
assert max(mutation_sites) < min(guard_sites)
assert 0x0AEA14 < save_sites[0] < fail_ret
fixtures['guard_order_leak'] = {
    'law': 'C3: character deletion (0xae0f4), eviction (0xae094) and member '
           'creation (0xae1d4/0xae23c/0xae258) all precede the lot-table / '
           'house-claimed / id-0 guards (0xae8f0, 0xae998); both guard '
           'failures converge at 0xae9c4 and return -1 via 0xaea14 -> '
           '0xaee18 BEFORE the single Save site (0xaecbc, the only bl to '
           '0xb5420 in the function): the in-memory model stays mutated, '
           'only the file move has a .tmp rollback',
    'fail_convergence': hex(fail_conv),
    'save_site': hex(save_sites[0]),
}

# (e) C4/C5 — one-arg wildcard-in-path scan + timer gate: decode the call
# shapes from the pinned words.
# (i) the scan call at 0x46f4d8 is preceded (after the append bl at
# 0x46f4d0) ONLY by an r3-setting addi — no r4 write: one argument.
between = dform(word(0x46F4D4))
assert between[:3] == (14, 3, 1) and between[3] == 0x40
# (ii) the callee (0x4aa010) consumes only r3 and is FindFirstFileA-shaped:
# single arg in, FindFirstFileA bl, INVALID_HANDLE test (-1 + 1 == 0xffff...),
# FindClose, boolean normalize.
assert dform(word(0x4AA018))[:3] == (14, 31, 3)   # r31 = r3 (the only arg)
ff = b_target(word(0x4AA048), 0x4AA048)
fc = b_target(word(0x4AA05C), 0x4AA05C)
inv = word(0x4AA054)
assert inv == 0x2800FFFF                          # cmplwi r0, 0xffff
assert word(0x4AA050) == 0x3C1F0001               # addis r0, r31, 1
assert ff != fc and 0x8E90 <= ff < 0x5C22E8
# (iii) the timer gate: win+0x16c != 0 jumps to DoInternetUpdate and exits
# at 0x472e98 — past the CheckForNewImports call (0x472e74) and the re-arm
# (0x472e8c).
gate_off = dform(word(0x472CE8))[3]
gate_skip = b_target(word(0x472D0C), 0x472D0C)
gate_else = bc_target(word(0x472CF0), 0x472CF0)
assert gate_off == 0x16C
assert gate_else == 0x472D10                      # import path
assert gate_skip == 0x472E98 and 0x472E8C < gate_skip  # skips both
fixtures['scan_and_timer_gate'] = {
    'law': 'C5: the *.FAM presence scan is one-arg on the path string with '
           'the wildcard appended (no r4 write between the append bl '
           '0x46f4d0 and the scan bl 0x46f4d8); the callee takes only r3 '
           '(0x4aa018) and is FindFirstFileA + INVALID_HANDLE(-1) + '
           'FindClose. C4: TSOnTimerMsg tests win+0x16c (0x472ce8); the '
           'nonzero branch (DoInternetUpdate at 0x472cf8) exits at '
           '0x472e98, past the CheckForNewImports call (0x472e74) and the '
           '1000 ms re-arm (0x472e8c)',
    'scan_arg_registers': ['r3'], 'timer_gate_offset': hex(gate_off),
}

# (f) refresh condition: the caller's stack reads decode to ImportInfo
# +0x110 (byte) and +0x150 (int) — the same fields the ctor/filler write.
ii_base = dform(word(0x46F5A4))[3]
has_house = dform(word(0x46F5D8))[3] - ii_base
house_no = dform(word(0x46F5E8))[3] - ii_base
assert (has_house, house_no) == (0x110, 0x150)
assert dform(word(0x231D94))[:2] == (38, 0)       # stb r0, ...
assert dform(word(0x231D94))[3] == 0x110          # ... +0x110 (ctor zero)
assert dform(word(0x232208))[:2] == (36, 0)       # stw r0, ...
assert dform(word(0x232208))[3] == 0x150          # ... +0x150 (filler)
fixtures['refresh_fields'] = {
    'law': 'DoNbhdScreen(true) after a successful auto-import iff '
           'ImportInfo+0x110 (has-house) and +0x150 (house number) are '
           'both nonzero; offsets re-derived from the caller stack '
           'displacements (0x270/0x2b0 minus base 0x160) and cross-bound '
           'to the ctor/filler stores',
    'fields': [hex(has_house), hex(house_no)],
}

# ---- Tutorial.FAM / House07.iff file fixtures ----
FAM = (ROOT / 'game-data/The Sims/UserData/Tutorial.FAM').read_bytes()
H7 = (ROOT / 'game-data/The Sims/UserData/Houses/House07.iff').read_bytes()
assert dstr(0x534D8 + 0x1621) == 'Tutorial.FAM'  # the ResetTutorial literal


def iff_chunks(d):
    out = []
    off = 0x40
    while off + 8 <= len(d):
        tag = d[off:off + 4]
        size = struct.unpack_from('>I', d, off + 4)[0]
        if size < 8:
            break
        out.append((tag.decode('latin1'), off, size))
        off += size  # IFF FILE 2.5: size includes the 8-byte header
    return out


fc = iff_chunks(FAM)
hc = iff_chunks(H7)
assert [t for t, _, _ in fc][:2] == ['SIMI', 'HOUS']
assert fc[0][2] == 0x192 and hc[0][2] == 0x192


def chunk_body(d, cs, tag, idx=0):
    n = 0
    for t, o, s in cs:
        if t == tag:
            if n == idx:
                return d[o + 8:o + s]
            n += 1
    raise KeyError(tag)


# every House07 chunk present in the FAM is byte-identical EXCEPT 'rsmp',
# which exists in both files but differs (skeptic: all common chunks match,
# rsmp is the sole exception)
fam_tags = [t for t, _, _ in fc]
for tag, _, _ in hc:
    if tag == 'rsmp':
        continue
    assert chunk_body(FAM, fc, tag) == chunk_body(H7, hc, tag), tag
assert 'rsmp' in fam_tags
assert chunk_body(FAM, fc, 'rsmp') != chunk_body(H7, hc, 'rsmp')

# EXPi: little-endian s16 famId + s16[8] member ids after the 'iPXE' echo
expi = chunk_body(FAM, fc, 'EXPi')
fields = expi[expi.index(b'iPXE') + 4:]
fam_id = struct.unpack_from('<h', fields, 0)[0]
member_ids = list(struct.unpack_from('<8h', fields, 2))
assert fam_id == 1
assert member_ids[0] == 48 and member_ids[1] == 47
assert member_ids[2:] == [0] * 6
uchr_offsets = sorted(o for t, o, s in fc if t == 'uChr')
assert len(uchr_offsets) == 2

# FAMI: little-endian u32 fields after the 'IMAF' echo
fami = chunk_body(FAM, fc, 'FAMI')
fvals = fami[fami.index(b'IMAF') + 4:]
house = struct.unpack_from('<I', fvals, 0)[0]
assert house == 7
assert struct.unpack_from('<I', fvals, 8)[0] == 665   # funds 0x299
assert struct.unpack_from('<I', fvals, 24)[0] == 2    # member count
guids = struct.unpack_from('>II', fvals, 28)
assert guids != (0, 0)
assert len(fvals) == 0x24

fixtures['iff_files'] = {
    'law': 'Tutorial.FAM chunk walk (sizes include the 8-byte header); '
           'every common chunk byte-identical with pristine House07.iff; '
           'EXPi = LE s16 famId + s16[8] ids after the iPXE echo; FAMI = '
           'LE u32s after the IMAF echo (house first, funds, count, GUIDs)',
    'fam_chunks': len(fc), 'house07_chunks': len(hc),
    'fam_id': fam_id, 'member_ids': member_ids,
    'house': house, 'funds': 665, 'members': 2,
    'guids': [hex(g) for g in guids],
    'tutorial_fam_sha256': hashlib.sha256(FAM).hexdigest(),
    'house07_iff_sha256': hashlib.sha256(H7).hexdigest(),
}

out = {
    'sha256': sha,
    'pin_count': len(pins),
    'data_pin_count': len(data_pins) + len(str_pins) + len(glob_pins),
    'record_pin_count': len(rec_pins),
    'fixtures': fixtures,
    'skeptic_corrections_applied': [
        'C1 relmatrix old->new row law (fixture c + pins)',
        ('C2 simi g26 not pinned as a file fact (nothing asserts a g26 '
         'value; only the read site 0xadeb0 is pinned)'),
        'C3 guard ordering/leak (fixture d)',
        'C4 timer +0x16c gate (fixture e)',
        'C5 one-arg wildcard-in-path scan (fixture e)',
        'C6 ctor +0x188 seed and id-assign-before-null-check (pins)',
        'C7 evicted-export guard documented in decode.md section 2.3',
        'C8 fixtures re-bound to pinned binary words',
        ('F1 (self-caught during C8): the pets gate extracts app+0x66 '
         'bit value 0x20 (rlwinm 0x1a,0x1a = MSB-index 26), not 0x40; '
         'decode.md corrected'),
    ],
}
(HERE / 'verified-fam-import.json').write_text(json.dumps(out, indent=2) + '\n')
print(f"PASS: {len(pins)} instruction pins, "
      f"{len(data_pins) + len(str_pins) + len(glob_pins)} data pins, "
      f"{len(rec_pins)} record pins, {len(fixtures)} binary-bound fixtures")
