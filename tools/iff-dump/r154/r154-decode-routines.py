#!/usr/bin/env python3
"""R154 TASK 2: full instruction listings of BHAV 9761 ('Convert Interests to
0-1000') and BHAV 39200 ('Add Hot Date Interests') in every defining file,
with byte citations and a copy-vs-copy diff.

Files (from the r154 corpus index):
  9761: PersonGlobals + RentalClerk/SalesClerk/VacationCarnie/VacationDirector/
        VacationMascot/Waiter Globals (23 instructions each)
  39200: same 7 + CatGlobals + DogGlobals (38 instructions; pets carry a
        DIFFERENT, 6-instruction variant)

Operand decode mirrors the port's VM:
  op 2        = expression {lhsData s16, rhsData s16, isSigned u8, operator u8,
                lhsScope u8, rhsScope u8} (VMExpressionOperand.Read)
  op >= 256   = subroutine call; the opcode IS the tree id; args = 4 x s16
                (VMThread.ExecuteSubRoutine; >=8192 -> this file = SEMI-GLOBAL
                of the calling object, 4096..8191 -> caller's local file,
                <4096 -> global.iff)
  op < 256    = primitive; named for the common ones.

Writes r154-routine-listings.txt.
"""
import importlib.util
import os
import struct
import sys

HERE = os.path.dirname(__file__)
ROOT = os.path.normpath(os.path.join(HERE, '..', '..', '..', 'game-data', 'The Sims'))

_spec = importlib.util.spec_from_file_location(
    'r154_callers_scan', os.path.join(HERE, 'r154-callers-scan.py'))
_scan = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_scan)
far_members, walk_chunks, parse_bhav = _scan.far_members, _scan.walk_chunks, _scan.parse_bhav

SCOPES = {0: 'MyObjectAttributes', 1: 'StackObjectAttributes', 2: 'TargetObjectAttributes',
          3: 'MyObject', 4: 'StackObject', 5: 'TargetObject', 6: 'Global', 7: 'Literal',
          8: 'Temps', 9: 'Parameters', 10: 'StackObjectID', 11: 'TempByTemp',
          12: 'TreeAdRange', 13: 'StackObjectTemp', 14: 'MyMotives', 15: 'StackObjectMotives',
          16: 'StackObjectSlot', 17: 'StackObjectMotiveByTemp', 18: 'MyPersonData',
          19: 'StackObjectPersonData', 20: 'MySlot', 21: 'StackObjectDefinition',
          22: 'StackObjectAttributeByParameter', 23: 'RoomByTemp0', 24: 'NeighborInStackObject',
          25: 'Local', 26: 'Tuning', 27: 'DynSpriteFlagForTempOfStackObject',
          28: 'TreeAdPersonalityVar', 29: 'TreeAdMin', 30: 'MyPersonDataByTemp',
          31: 'StackObjectPersonDataByTemp', 32: 'NeighborPersonData', 33: 'JobData',
          34: 'NeighborhoodData', 35: 'StackObjectFunction', 36: 'MyTypeAttr',
          37: 'StackObjectTypeAttr', 38: 'NeighborsObjectDefinition'}
OPERATORS = {0: '>', 1: '<', 2: '==', 3: '+=', 4: '-=', 5: '=', 6: '*=', 7: '/=',
             8: 'flagSet', 9: 'setFlag', 10: 'clearFlag', 11: '++&<', 12: '%=',
             13: '&=', 14: '>=', 15: '<=', 16: '!=', 17: '--&>', 18: 'push/|=', 19: 'pop/^=', 20: 'sqrt='}
PRIMS = {0: 'sleep', 2: 'expression', 4: 'grab', 5: 'drop', 6: 'change suit/accessory',
         7: 'refresh', 8: 'random number', 9: 'burn', 10: 'get distance to',
         11: 'get direction to', 12: 'push interaction', 13: 'find best object for function',
         14: 'breakpoint', 15: 'find location for', 16: 'idle for input',
         17: 'remove object instance', 18: 'run functional tree', 19: 'show string',
         20: 'look towards', 21: 'play sound', 22: 'relationship (short)', 23: 'relationship (long)',
         24: 'goto relative position', 25: 'gotorouted', 26: 'set motive change',
         27: 'run tree by name', 28: 'syslog', 29: 'set to next', 30: 'test object type',
         31: 'special effect', 32: 'dialog private strings', 33: 'test sim interacting with',
         34: 'dialog global strings', 35: 'dialog semi-global strings', 36: 'online jobs call',
         37: 'set balloon headline', 38: 'create object instance', 39: 'drop onto',
         40: 'animate sim', 41: 'goto routing slot', 42: 'snap', 43: 'reach',
         44: 'stop all sounds', 45: 'notify out of idle', 46: 'change action string',
         47: 'inventory ops (TSO)', 48: 'invoke plugin', 49: 'get terrain info',
         50: 'find best action', 51: 'generic TS1 call', 52: 'make new character',
         53: 'TS1 budget', 54: 'gosub found action', 55: 'TS1 inventory ops',
         56: 'generic TSO call', 57: 'transfer funds'}

TARGET_IDS = (9761, 39200)
GLOBAL_FAR = os.path.join(ROOT, 'GameData', 'Global', 'Global.far')


def decode_instr(op, tp, fp, args, global_names=None):
    s = 'op=%-6d' % op
    if op == 2:
        # operand bytes: lhs s16 @0, rhs s16 @2, isSigned @4, operator @5,
        # lhsScope @6, rhsScope @7  -> as 4 s16: [lhs, rhs, ss|op<<8, lsc|rsc<<8]
        lhs, rhs = args[0], args[1]
        issigned, oper = args[2] & 0xFF, (args[2] >> 8) & 0xFF
        lsc, rsc = args[3] & 0xFF, (args[3] >> 8) & 0xFF
        s += 'EXPR  %s[%d]%s %s %s[%d]' % (SCOPES.get(lsc, 'sc%d' % lsc), lhs,
                                           ' (signed)' if issigned else '',
                                           OPERATORS.get(oper, 'op%d' % oper),
                                           SCOPES.get(rsc, 'sc%d' % rsc), rhs)
    elif op >= 256:
        s += 'CALL  tree %d (args=%s)' % (op, args)
    else:
        s += 'PRIM  %s args=%s' % (PRIMS.get(op, 'prim%d' % op), args)
    return s


def extract():
    """Extract BHAV 9761/39200 from every Global.far member that has them.
    Returns {id: {member: (chunk_off, raw_bytes, bhav)}} plus labels."""
    found = {9761: {}, 39200: {}}
    labels = {}
    with open(GLOBAL_FAR, 'rb') as fh:
        for nm, data in far_members(GLOBAL_FAR, fh):
            if not nm.lower().endswith('.iff'):
                continue
            for tag, off, size, cid, flags, label, dstart in walk_chunks(data):
                if tag == 'BHAV' and cid in TARGET_IDS:
                    b = parse_bhav(data, dstart)
                    if b:
                        found[cid][nm] = (dstart, size, b, data[dstart:off+size])
                        labels[(cid, nm)] = label
    return found, labels


def main():
    outp = os.path.join(HERE, 'r154-routine-listings.txt')
    out = open(outp, 'w')

    def P(s=''):
        print(s)
        out.write(s + '\n')

    found, labels = extract()

    for cid in TARGET_IDS:
        P('=' * 100)
        P('BHAV %d in %d file(s)' % (cid, len(found[cid])))
        P('=' * 100)
        # copy diff (instruction bytes + label)
        base_nm = sorted(found[cid])[0]
        _, _, b0, raw0 = found[cid][base_nm]
        P('copy diff vs %s:' % base_nm)
        for nm in sorted(found[cid]):
            _, _, b, raw = found[cid][nm]
            same_instr = all(a == c for a, c in zip(b0['instrs'], b['instrs'])) and len(b0['instrs']) == len(b['instrs'])
            same_hdr = (b0['args'], b0['locals'], b0['type'], b0['sig']) == (b['args'], b['locals'], b['type'], b['sig'])
            P('  %-40s label=%-32r instr-bytes %s hdr %s' % (
                nm, labels.get((cid, nm)), 'IDENTICAL' if same_instr else 'DIFFER',
                'IDENTICAL' if same_hdr else 'DIFFER'))
        P('')

        for nm in sorted(found[cid]):
            off, size, b, raw = found[cid][nm]
            P('--- %s :: BHAV %d %r' % (nm, cid, labels.get((cid, nm))))
            P('    Global.far member data offset %#x, chunk size %d, sig 0x%04x, args %d, locals %d, %d instructions' % (
                off, size, b['sig'], b['args'], b['locals'], len(b['instrs'])))
            for i, ins in enumerate(b['instrs']):
                P('    [%2d] T=%-3d F=%-3d %s' % (i, ins[1], ins[2], decode_instr(ins[0], ins[1], ins[2], ins[3:])))
            P('')

    P('=== raw hex, first instruction words (byte citations) ===')
    for cid in TARGET_IDS:
        for nm in sorted(found[cid]):
            off, size, b, raw = found[cid][nm]
            P('%-12s BHAV %-6d @Global.far!%s+%#x: %s' % (nm, cid, nm, off, raw[:36].hex(' ')))
    out.close()
    print('wrote', outp)


if __name__ == '__main__':
    main()
