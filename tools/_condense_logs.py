import glob
CHATTER = ["motive sample minute=", "uidump-tree", "brainlive diag", "brainlive frame", "MOTIVE sample minute="]
KEEP = ["AUTOTEST RESULT", "AUTOTEST SUMMARY", "SUMMARY passed=", "RELPROBE SUMMARY", "exit-probe:", "AUTOTEST START"]
SDATA = ["IFF-literal", "shipped-save", "serve-proof", "canon=", "drift=", "pg-seen", "served.Count", "closure:", "criteria", "census="]
def condense(lines):
    kept=[]
    for i,l in enumerate(lines):
        s=l.strip("\n")
        if i<2: kept.append(l); continue
        if any(c in s for c in CHATTER): continue
        if "autotest: AUTOTEST " in s or s.startswith("AUTOTEST "):
            kept.append(l); continue
        if any(k in s for k in KEEP): kept.append(l); continue
        if any(d in s for d in SDATA): kept.append(l); continue
    return kept
def is_run_log(lines):
    return any("AUTOTEST RESULT" in l for l in lines)
allf = sorted(set(list(glob.glob('*.log'))+list(glob.glob('r*/r*-autotest-*.log'))+list(glob.glob('r*/r7*-*.log'))+list(glob.glob('r*/r8*-*.log'))))
total_orig=total_kept=condensed=0
for f in allf:
    with open(f,encoding='utf-8',errors='replace') as fh: lines=fh.readlines()
    if not is_run_log(lines):
        continue
    k=condense(lines)
    has = any("AUTOTEST RESULT" in l for l in k)
    if has:
        with open(f,"w",encoding='utf-8') as fh: fh.writelines(k)
        condensed+=1
        total_orig+=len(lines); total_kept+=len(k)
print("condensed run logs:", condensed)
print("TOTAL run-log lines: %d -> %d (%.1f%% cut)" % (total_orig,total_kept,100*(1-total_kept/total_orig)))
for probe in ["r80/r80-autotest-run2.log","r81/r81-autotest-run1.log"]:
    with open(probe,encoding='utf-8') as fh: t=fh.read()
    print(probe, "deathchain:", "deathchain" in t, "savesim:", "savesim" in t, "RESULT:", "AUTOTEST RESULT" in t)
