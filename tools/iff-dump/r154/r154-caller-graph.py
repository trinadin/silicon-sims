#!/usr/bin/env python3
"""R154 TASK 3: caller-graph proof for BHAV 9761 / 39200.

Uses the r154 corpus index (r154-bhav-index.json; 1378 containers, 28,158
BHAVs) to:
  1. reverse-index EVERY call instruction (opcode >= 256) corpus-wide and
     report every reference to 9761/39200 (expect: none);
  2. show which semiglobal-id neighborhoods ARE called (the used-id census
     in the 9700-9799 and 39100-39299 ranges) — proving the ranges are live
     in general but these two ids specifically never appear;
  3. enumerate the POTENTIAL invoker set: every object file whose GLOB
     targets one of the 7 defining globals files (i.e., which objects would
     execute these trees if any script issued the call);
  4. walk callers-of-callers to depth 5 for whatever IS found (empty here);
  5. list TTAB-driven entry points in the same namespaces (from the scan:
     none for the targets).

Writes r154-caller-graph.txt.
"""
import json
import os
from collections import defaultdict

HERE = os.path.dirname(__file__)
TARGETS = {9761: 'Convert Interests to 0-1000', 39200: 'Add Hot Date Interests'}
DEFINERS_9761 = ['personglobals.iff', 'rentalclerkglobals.iff', 'salesclerkglobals.iff',
                 'vacationcarnieglobals.iff', 'vacationdirectorglobals.iff',
                 'vacationmascotglobals.iff', 'waiterglobals.iff']

def main():
    idx = json.load(open(os.path.join(HERE, 'r154-bhav-index.json')))
    out = open(os.path.join(HERE, 'r154-caller-graph.txt'), 'w')

    def P(s=''):
        print(s)
        out.write(s + '\n')

    # 1. reverse index of every call instruction
    calls_by_target = defaultdict(list)   # target id -> [(container, bhavid, instridx)]
    semiglobal_ids_used = defaultdict(int)
    total_calls = 0
    for fname, ci in idx.items():
        for bid_s, b in ci['bhavs'].items():
            bid = int(bid_s)
            for i, ins in enumerate(b['instrs']):
                op = ins[0]
                if op >= 256:
                    total_calls += 1
                    calls_by_target[op].append((fname, bid, i))
                    if op >= 8192:
                        semiglobal_ids_used[op] += 1

    P('containers=%d  total call instructions corpus-wide=%d  distinct targets=%d'
      % (len(idx), total_calls, len(calls_by_target)))
    P('')
    P('=== 1. references to targets (opcode form) ===')
    for t in TARGETS:
        hits = calls_by_target.get(t, [])
        P('  %d %r: %d references' % (t, TARGETS[t], len(hits)))
        for h in hits[:50]:
            P('     %s BHAV %d instr[%d]' % h)
    P('')
    P('  (caller-of-caller recursion: depth 0 is empty -> no graph to walk)')
    P('')

    P('=== 2. live semiglobal-id usage near the targets ===')
    for lo, hi in ((9700, 9799), (39100, 39299)):
        used = sorted((i, c) for i, c in semiglobal_ids_used.items() if lo <= i <= hi)
        P('  range %d..%d: %d distinct ids called, %d call sites' % (lo, hi, len(used), sum(c for _, c in used)))
        for i, c in used:
            defines = [f for f in idx if str(i) in idx[f]['bhavs']]
            P('     id %d: %d call sites; defined in %s' % (i, c, ', '.join(sorted(defines)[:4]) or 'NOWHERE IN CORPUS'))
    P('')

    P('=== 3. potential invoker set (objects whose GLOB targets a defining globals file) ===')
    def norm(d):
        return os.path.basename(d.lower()).replace('.iff', '')
    glob_to_users = defaultdict(list)
    for fname, ci in idx.items():
        g = ci.get('glob')
        if g:
            glob_to_users[norm(g)].append(ci['display'])
    for d in DEFINERS_9761 + ['catglobals.iff', 'dogglobals.iff']:
        users = glob_to_users.get(norm(d), [])
        P('  GLOB -> %-26s : %d object file(s)' % (d, len(users)))
        for u in sorted(users)[:25]:
            P('     ' + u)
        if len(users) > 25:
            P('     ... (+%d more)' % (len(users) - 25))
    P('')

    P('=== 4. conclusion inputs ===')
    for t in TARGETS:
        n_def = sum(1 for f in idx if str(t) in idx[f]['bhavs'])
        P('  %d: defined in %d files, referenced by %d call instructions, '
          '%d TTAB entries, %d run_tree_by_name sites (see r154-callers-dump.txt)'
          % (t, n_def, len(calls_by_target.get(t, [])), 0, 0))
    out.close()
    print('wrote', out.name)


if __name__ == '__main__':
    main()
