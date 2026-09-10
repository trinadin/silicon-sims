#!/usr/bin/env python3
# verify_lane.py - ENV-02 lane acceptance verifier.
#
# Checks (each printed as PASS/FAIL, exit 1 on any FAIL):
#   manifest   lane manifest exists and parses; baseline/submodules/diffs recorded
#   checkout   src is a detached worktree at the manifest baseline; live
#              `git submodule status` SHAs match the manifest
#   symlinks   every symlink under the lane resolves inside the lane
#              (paths with a .git component are git plumbing and exempt;
#              this is what proves no lane link reaches another writer's
#              source or output)
#   probes     lane probe files match the manifest sha256s; the engine-fixed
#              /tmp copies, when present, are byte-identical to the lane files
#   data       the lane game-data copy still matches game-data.manifest taken
#              at bootstrap; with --original <dir>, the SHARED original tree
#              must hash to the same manifest (proves the original was never
#              modified). Expensive (~GB hashing); --skip-data-hash for quick runs
#   runlog     with --last-log <file>: the run's private userdir is NOT under
#              ~/Documents/Simitone; with --expect-pass: an AUTOTEST RESULT
#              PASS verdict line exists
#
#   python3 verify_lane.py --lane <dir> [--original <dir>] [--skip-data-hash]
#                          [--last-log <file>] [--expect-pass]
import argparse
import hashlib
import os
import subprocess
import sys

EXTRACTIONS_EXEMPT_COMPONENT = ".git"


def fail(check, msg):
    print("FAIL %-9s %s" % (check, msg))
    return False


def ok(check, msg):
    print("PASS %-9s %s" % (check, msg))
    return True


def parse_manifest(path):
    fields = {}
    subs, diffs = [], []
    section = None
    with open(path) as f:
        for line in f.read().splitlines():
            if line.startswith("# "):
                section = line[2:]
                continue
            if not line.strip():
                continue
            if section == "submodule status at creation:":
                subs.append(line)
            elif section == "applied diffs:":
                diffs.append(line)
            elif "=" in line and section is None:
                k, v = line.split("=", 1)
                fields[k] = v
    return fields, subs, diffs


def sub_sha(line):
    """SHA from a `git submodule status` line (`[ +U]<sha> <path> ...`)."""
    return line.split()[0].lstrip("+-U ")


def tree_manifest(root):
    """{bytes relpath: sha256} for every regular file under root (bytes keys so
    non-UTF8 filenames from the original data compare exactly)."""
    root_b = os.fsencode(root)
    out = {}
    for dirpath, dirnames, filenames in os.walk(root_b):
        dirnames.sort()
        for fn in sorted(filenames):
            p = os.path.join(dirpath, fn)
            if os.path.islink(p):
                continue
            h = hashlib.sha256()
            with open(p, "rb") as f:
                for chunk in iter(lambda: f.read(1 << 20), b""):
                    h.update(chunk)
            rel = os.path.relpath(p, root_b)
            if rel.startswith(b"./"):
                rel = rel[2:]
            out[rel] = h.hexdigest()
    return out


def load_data_manifest(path):
    """{bytes relpath: sha256} from the bootstrap game-data.manifest
    (`shasum -a 256` lines: `<sha>  <./path>`)."""
    out = {}
    with open(path, "rb") as f:
        for line in f:
            line = line.rstrip(b"\n")
            if not line:
                continue
            sha, p = line.split(b"  ", 1)
            if p.startswith(b"./"):
                p = p[2:]
            out[p] = sha.decode()
    return out


def diff_maps(live, recorded, limit=3):
    """First few human-readable differences between two {path: sha} maps."""
    diffs = []
    for k in sorted(set(recorded) - set(live))[:limit]:
        diffs.append("missing %s" % k.decode("utf-8", "replace"))
    for k in sorted(set(live) - set(recorded))[:limit]:
        diffs.append("unexpected %s" % k.decode("utf-8", "replace"))
    for k in sorted(set(live) & set(recorded))[:limit]:
        if live[k] != recorded[k]:
            diffs.append("changed %s" % k.decode("utf-8", "replace"))
    return diffs


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--lane", required=True)
    ap.add_argument("--original", help="shared original game-data dir to prove untouched")
    ap.add_argument("--skip-data-hash", action="store_true")
    ap.add_argument("--last-log", help="comma-separated run logs to scan (game log and/or"
                                      " launcher stdout; userdir is only in launcher stdout)")
    ap.add_argument("--expect-pass", action="store_true")
    args = ap.parse_args()

    lane = os.path.realpath(args.lane)
    src = os.path.join(lane, "src")
    results = []

    # manifest
    mpath = os.path.join(lane, "manifest.txt")
    if not os.path.isfile(mpath):
        results.append(fail("manifest", "missing %s" % mpath))
        print("\nlane verification: FAIL (no manifest)")
        return 1
    fields, subs, diffs = parse_manifest(mpath)
    missing = [k for k in ("lane", "created_utc", "owner", "baseline") if k not in fields]
    if missing:
        results.append(fail("manifest", "missing fields: %s" % missing))
    else:
        results.append(ok("manifest", "lane=%s baseline=%s owner=%s diffs=%d"
                          % (fields["lane"], fields["baseline"][:12], fields["owner"], len(diffs))))

    # checkout
    gitfile = os.path.join(src, ".git")
    if not (os.path.isfile(gitfile) or os.path.isdir(gitfile)):
        results.append(fail("checkout", "no .git in %s" % src))
    else:
        head = subprocess.run(["git", "-C", src, "rev-parse", "HEAD"],
                              capture_output=True, text=True)
        detached = subprocess.run(["git", "-C", src, "symbolic-ref", "-q", "HEAD"],
                                  capture_output=True, text=True).returncode
        if head.returncode != 0:
            results.append(fail("checkout", "git rev-parse failed: %s" % head.stderr.strip()))
        elif "baseline" in fields and head.stdout.strip() != fields["baseline"]:
            results.append(fail("checkout", "HEAD %s != baseline %s"
                                % (head.stdout.strip()[:12], fields["baseline"][:12])))
        elif detached == 0:
            results.append(fail("checkout", "src HEAD is on a branch, expected detached"))
        else:
            live_subs = [l for l in subprocess.run(
                ["git", "-C", src, "submodule", "status"],
                capture_output=True, text=True).stdout.splitlines() if l.strip()]
            live_shas = sorted(sub_sha(l) for l in live_subs)
            rec_shas = sorted(sub_sha(l) for l in subs if l.split())
            if live_shas != rec_shas:
                results.append(fail("checkout", "submodule SHAs diverged: live=%s recorded=%s"
                                    % (live_shas, rec_shas)))
            else:
                results.append(ok("checkout", "detached at %s, %d submodules pinned"
                                  % (head.stdout.strip()[:12], len(live_shas))))

    # symlinks
    bad_links, n_links = [], 0
    for dirpath, dirnames, filenames in os.walk(lane, followlinks=False):
        rel = os.path.relpath(dirpath, lane).split(os.sep)
        if EXTRACTIONS_EXEMPT_COMPONENT in rel:
            dirnames[:] = []
            continue
        for name in list(dirnames) + filenames:
            p = os.path.join(dirpath, name)
            if not os.path.islink(p):
                continue
            if EXTRACTIONS_EXEMPT_COMPONENT in os.path.relpath(p, lane).split(os.sep):
                continue
            n_links += 1
            target = os.path.realpath(p)
            if target != lane and not target.startswith(lane + os.sep):
                bad_links.append((os.path.relpath(p, lane), target))
    if bad_links:
        results.append(fail("symlinks", "%d of %d escape the lane: %s"
                            % (len(bad_links), n_links, bad_links[:5])))
    else:
        results.append(ok("symlinks", "%d symlink(s) checked, all resolve inside the lane" % n_links))

    # probes
    probes_ok = True
    recorded = {}
    section = None
    with open(mpath) as f:
        for line in f.read().splitlines():
            if line.startswith("# "):
                section = line[2:]
                continue
            if section == "probe sha256:" and "  " in line:
                sha, p = line.split(None, 1)
                recorded[os.path.basename(p.strip())] = sha
    if not recorded:
        results.append(fail("probes", "no probe sha256 recorded in manifest"))
    else:
        for name, sha in sorted(recorded.items()):
            p = os.path.join(lane, "probes", name)
            if not os.path.isfile(p):
                results.append(fail("probes", "missing lane probe %s" % p))
                probes_ok = False
                continue
            h = hashlib.sha256(open(p, "rb").read()).hexdigest()
            fixed = os.path.join("/tmp", name)
            fixed_state = "absent"
            if os.path.exists(fixed):
                fh = hashlib.sha256(open(fixed, "rb").read()).hexdigest()
                if fh != h:
                    results.append(fail("probes", "/tmp/%s diverged from lane probe" % name))
                    probes_ok = False
                    continue
                fixed_state = "installed+identical"
            if h != sha:
                results.append(fail("probes", "lane probe %s hash != manifest" % name))
                probes_ok = False
                continue
            print("     probe %s ok (%s)" % (name, fixed_state))
        if probes_ok:
            results.append(ok("probes", "%d lane probe(s) match manifest; /tmp copies identical" % len(recorded)))

    # data
    if args.skip_data_hash:
        results.append(ok("data", "skipped (--skip-data-hash)"))
    else:
        gm = os.path.join(lane, "game-data.manifest")
        if os.path.getsize(gm) == 0:
            results.append(ok("data", "no private game copy recorded (manifest empty)"))
        else:
            recorded = load_data_manifest(gm)
            lane_root = os.path.join(lane, "game-data", "The Sims")
            live = tree_manifest(lane_root)
            if live != recorded:
                results.append(fail("data", "lane copy diverged from bootstrap manifest: %s"
                                    % diff_maps(live, recorded)))
            else:
                results.append(ok("data", "lane game-data copy matches bootstrap manifest (%d files)"
                                  % len(live)))
            if args.original:
                orig_root = os.path.realpath(args.original)
                orig = tree_manifest(orig_root)
                if orig != recorded:
                    results.append(fail("data", "ORIGINAL %s differs from manifest (changed on disk!): %s"
                                        % (orig_root, diff_maps(orig, recorded))))
                else:
                    results.append(ok("data", "shared original game-data unchanged (%d files)"
                                      % len(orig)))

    # runlog
    if args.last_log:
        log = ""
        for path in args.last_log.split(","):
            path = path.strip()
            if not path:
                continue
            try:
                log += open(path, errors="replace").read() + "\n"
            except OSError as e:
                results.append(fail("runlog", "cannot read %s: %s" % (path, e)))
        userdir = None
        for line in log.splitlines():
            if line.startswith("run-autotest: userdir="):
                userdir = line.split("=", 1)[1].strip()
        if userdir is None and not any(l.startswith("run-autotest:") for l in log.splitlines()):
            results.append(fail("runlog", "no run-autotest userdir= line found (pass the"
                                          " launcher stdout file, not only the game log)"))
        elif userdir is not None:
            home_docs = os.path.join(os.path.expanduser("~"), "Documents", "Simitone")
            if os.path.realpath(userdir) == home_docs or \
                    os.path.realpath(userdir).startswith(home_docs + os.sep):
                results.append(fail("runlog", "run used the SHARED userdir %s" % userdir))
            else:
                results.append(ok("runlog", "private userdir %s" % userdir))
        if args.expect_pass:
            if "AUTOTEST RESULT PASS" in log:
                verdict = [l for l in log.splitlines() if "AUTOTEST RESULT" in l][-1]
                results.append(ok("runlog", "verdict: %s" % verdict.strip()))
            else:
                results.append(fail("runlog", "no AUTOTEST RESULT PASS verdict in log"))

    print("\nlane verification: %s (%d checks)" % ("PASS" if all(results) else "FAIL", len(results)))
    return 0 if all(results) else 1


if __name__ == "__main__":
    sys.exit(main())
