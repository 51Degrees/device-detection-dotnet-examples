"""Classifies each gatherer test from the collected lab runs.

The claim a browser makes is browser plus platform, so genuine values are
grouped per browser. A test is:
  stable        every genuine headed run of the same browser gives one value
  USE           stable, and the Linux build claiming Windows Chrome answers a
                real value (not na, err or timeout) that differs from genuine
                Windows Chrome
  no-diff       stable but the Linux build answers the same
  env-only      the only difference is na or err, which is the lab setup and
                not the platform
  unstable      genuine runs of one browser disagree with each other
Headless and Electron columns say whether those runs match the genuine
headed value, the two known false positive classes.
"""
import json, glob, os, sys, collections

RUNS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "runs")
GEN = int(os.environ.get("GEN", "3"))  # only runs from this gatherer generation
runs = collections.defaultdict(list)
for f in sorted(glob.glob(os.path.join(RUNS, "*.json"))):
    d = json.load(open(f, encoding="utf-8"))
    if "gen" in d and int(d["gen"]) != GEN:
        continue
    runs[d["label"]].append(d)

labels = sorted(runs)
print("runs:", ", ".join(f"{l} x{len(runs[l])}" for l in labels))
tests = sorted({t for rs in runs.values() for r in rs for t in r["results"]})
NA = ("na", "err", "timeout", "<missing>")

def browser_of(label):
    return "chrome" if "chrome" in label else "edge" if "edge" in label else "electron" if "electron" in label else "firefox" if "firefox" in label else "chromium"

GENUINE = [l for l in labels if l.startswith("win-") and "headed" in l]
HEADLESS = [l for l in labels if l.startswith("win-") and "headless" in l and "firefox" not in l]
ELECTRON = [l for l in labels if "electron" in l]
SPOOF = [l for l in labels if "claims-windows" in l]

def values(test, label):
    return [r["results"].get(test, "<missing>") for r in runs[label]]

def short(v, n=48):
    v = str(v)
    return v if len(v) <= n else v[:n] + "..."

rows = []
for t in tests:
    per_browser = {}
    for l in GENUINE:
        per_browser.setdefault(browser_of(l), []).extend(values(t, l))
    stable = all(len(set(v)) == 1 for v in per_browser.values()) and bool(per_browser)
    chrome_val = per_browser.get("chrome", [""])[0]
    spoof_vals = [v for l in SPOOF for v in values(t, l)]
    spoof_real = [v for v in spoof_vals if v not in NA]
    if not stable:
        verdict = "unstable"
    elif chrome_val in NA:
        verdict = "env-only"
    elif spoof_real and all(v != chrome_val for v in spoof_real):
        verdict = "USE"
    elif spoof_vals and not spoof_real:
        verdict = "env-only"
    else:
        verdict = "no-diff"
    hl = [v for l in HEADLESS if browser_of(l) in per_browser for v in values(t, l)]
    headless_same = all(v == per_browser[browser_of(l)][0] for l in HEADLESS if browser_of(l) in per_browser for v in values(t, l)) if hl else None
    el = [v for l in ELECTRON for v in values(t, l)]
    electron_same = all(v == chrome_val for v in el) if el else None
    rows.append((verdict, t, headless_same, electron_same, chrome_val, spoof_vals[0] if spoof_vals else ""))

order = {"USE": 0, "no-diff": 1, "env-only": 2, "unstable": 3}
rows.sort(key=lambda r: (order[r[0]], r[1]))
def mark(b): return "-" if b is None else "same" if b else "DIFF"
print(f"\n{'verdict':9} {'test':20} {'hdless':6} {'electr':6}  genuine Windows Chrome  ->  Linux claiming Windows")
for verdict, t, hs, es, gv, sv in rows:
    print(f"{verdict:9} {t:20} {mark(hs):6} {mark(es):6}  {short(gv):50} -> {short(sv)}")
c = collections.Counter(r[0] for r in rows)
print(f"\nUSE {c['USE']}   no-diff {c['no-diff']}   env-only {c['env-only']}   unstable {c['unstable']}   total {len(rows)}")
if "--detail" in sys.argv:
    for t in tests:
        print("\n==", t)
        for l in labels:
            for v in values(t, l):
                print(f"  {l:32} {v}")
