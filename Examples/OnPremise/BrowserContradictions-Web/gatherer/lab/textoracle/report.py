"""Reads results/*.json and reports, per test and per A/B variant:
  stable     every genuine Windows Chromium run (all browsers, all
             conditions) gives one value per window
  separates  no Linux or CloakBrowser run matches the genuine value in any
             window
  distinct   the genuine value differs between windows (a replay of one
             window would not pass another)
  tags       the gatherer's tag equals the one derive.py computes

  py -I report.py
"""
import collections
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import derive  # noqa: E402

GENUINE = lambda label: label.startswith(("oracle-win-", "ab-win-", "ab2-win-")) and "firefox" not in label
FAKE = lambda label: "-linux-" in label


def load(prefix):
    out = {}
    for f in sorted(os.listdir(os.path.join(HERE, "results"))):
        if f.startswith(prefix):
            out[f[:-5]] = json.load(open(os.path.join(HERE, "results", f), encoding="utf-8"))
    return out


def judge(name, values):
    """values: {label: {window: value}}. Prints one line and the details."""
    gen = {l: v for l, v in values.items() if GENUINE(l)}
    fake = {l: v for l, v in values.items() if FAKE(l)}
    windows = sorted({w for v in values.values() for w in v})
    truth, unstable = {}, []
    for w in windows:
        vs = collections.Counter(v.get(w) for v in gen.values() if v.get(w) is not None)
        if len(vs) == 1:
            truth[w] = next(iter(vs))
        else:
            unstable.append((w, vs))
    fakes_passing = [(l, w) for l, v in fake.items() for w in windows if w in truth and v.get(w) == truth[w]]
    distinct = len(set(truth.values()))
    flag = lambda ok: "yes" if ok else "NO "
    print(f"{name:24} stable {flag(not unstable)}  separates {flag(not fakes_passing)}  "
          f"distinct {distinct}/{len(windows)}  genuine runs {len(gen)}  fake runs {len(fake)}")
    for w, vs in unstable:
        by = collections.defaultdict(list)
        for l, v in gen.items():
            by[v.get(w)].append(l.split("-win-")[1])
        print(f"    window {w}: " + " | ".join(f"{str(k)[:40]} <- {', '.join(ls)}" for k, ls in by.items()))
    for l, w in fakes_passing[:6]:
        print(f"    fake passes: {l} window {w}")
    return not unstable and not fakes_passing


def main():
    print("== Oracle: the real gatherer with per window params ==")
    runs = load("oracle-")
    jobs = {j["window"]: j for j in json.load(open(os.path.join(HERE, "jobs.json"), encoding="utf-8"))}
    tests = sorted({t for r in runs.values() for j in r["jobs"] for t in j["results"]})
    for t in tests:
        values = {}
        for label, r in runs.items():
            values[label] = {j["window"]: j["results"].get(t) for j in r["jobs"]}
        judge(t, values)
    # Tags: does every genuine run's tag match derive.py's for that window?
    bad = 0
    for label, r in runs.items():
        for j in r["jobs"]:
            for t, tg in jobs[j["window"]]["tags"].items():
                v = j["results"].get(t)
                if v is not None and not v.startswith(tg):
                    bad += 1
                    print(f"    tag mismatch {label} {t} window {j['window']}: got {v[:12]} want {tg}")
    print(f"tags: {'all match' if bad == 0 else str(bad) + ' mismatches'}")
    for label, r in runs.items():
        print(f"    {label:34} {r.get('ms', '?'):>6} ms for {len(r['jobs'])} windows  dpr {r['env'].get('dpr')} {r['env'].get('w')}x{r['env'].get('h')} {r['env'].get('lang')}")

    print("\n== A/B: candidate width tests ==")
    runs = load("ab-")
    variants = sorted({x["variant"] for r in runs.values() for x in r["runs"]}, key=lambda s: int(s[1:].split("_")[0]))
    for v in variants:
        values = {}
        for label, r in runs.items():
            values[label] = {x["window"]: x["value"] for x in r["runs"] if x["variant"] == v}
        judge(v, values)

    print("\n== A/B 2: glyph shape test, size and grid ==")
    runs = load("ab2-")
    variants = sorted({x["variant"] for r in runs.values() for x in r["runs"]}, key=lambda s: int(s[1:].split("_")[0]))
    for v in variants:
        values = {}
        for label, r in runs.items():
            values[label] = {x["window"]: x["value"] for x in r["runs"] if x["variant"] == v}
        ok = judge(v, values)
        ms = [r["ms"].get(v) for l, r in runs.items() if GENUINE(l) and r.get("ms")]
        print(f"    ms per window on genuine Windows runs: min {min(ms)} max {max(ms)}")


if __name__ == "__main__":
    main()
