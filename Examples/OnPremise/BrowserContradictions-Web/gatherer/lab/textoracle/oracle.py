"""The oracle table and the check against it: the server side of the per
window text tests.

  py -I oracle.py build        results/oracle-win-*.json -> oracle.json
  py -I oracle.py verify       check every run in results/ against oracle.json

The table is {window: {family: {test: value}}}. A family is the group of
browsers that must agree, here "Chromium|Windows" (Chrome, Edge and Chrome
for Testing on this machine) and "Firefox|Windows". An entry exists only
when every oracle run for the family gave the same value: a disagreement
means the test is not a safe check for that window and the element skips
it (Unknown, never Low). In production the oracle runs are a few genuine
machines per family, on each window ahead of time, and a value needs
agreement across machines as well as browsers.

verify(family, window, test, value) answers:
  "pass"      the value, tag stripped, equals the oracle's
  "fail"      it does not
  "skip"      no oracle entry, or the tag is not this window's, or the
              value is untagged (defaults) when the window issued params
"""
import collections
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import derive  # noqa: E402

CHECKED = ("f_metrics.1", "f_segoeshape.2", "m_tanhwalk.1")


def family_of(label):
    if not label.startswith("oracle-win-"):
        return None
    return "Firefox|Windows" if "firefox" in label else "Chromium|Windows"


def build():
    jobs = {j["window"]: j for j in json.load(open(os.path.join(HERE, "jobs.json"), encoding="utf-8"))}
    seen = collections.defaultdict(lambda: collections.defaultdict(collections.Counter))
    for f in sorted(os.listdir(os.path.join(HERE, "results"))):
        fam = family_of(f[:-5])
        if fam is None:
            continue
        r = json.load(open(os.path.join(HERE, "results", f), encoding="utf-8"))
        for j in r["jobs"]:
            for t in CHECKED:
                v = j["results"].get(t)
                if v is not None:
                    seen[j["window"]][(fam, t)][v] += 1
    table, dropped = {}, []
    for w, by in seen.items():
        table[w] = {}
        for (fam, t), counts in by.items():
            if len(counts) == 1:
                table[w].setdefault(fam, {})[t] = next(iter(counts))
            else:
                dropped.append((w, fam, t, dict(counts)))
    json.dump({"windows": table, "tags": {w: jobs[w]["tags"] for w in table}},
              open(os.path.join(HERE, "oracle.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=0)
    print(f"oracle: {len(table)} windows, {sum(len(t) for fams in table.values() for t in fams.values())} entries, {len(dropped)} dropped for disagreement")
    for d in dropped:
        print("   dropped", d[0], d[1], d[2], {k[:30]: v for k, v in d[3].items()})


def verify(oracle, family, window, test, value):
    entry = oracle["windows"].get(str(window), {}).get(family, {})
    if test not in entry or value is None:
        return "skip"
    tag = oracle["tags"][str(window)][test]
    bare = derive_untag(value, tag)
    if bare is None:
        return "skip"
    want = derive_untag(entry[test], tag)
    return "pass" if bare == want else "fail"


def derive_untag(value, tag):
    if tag == "":
        return None if (value.startswith("p") and len(value) > 9 and value[9] == ":") else value
    return value[len(tag):] if value.startswith(tag) else None


def verify_all():
    oracle = json.load(open(os.path.join(HERE, "oracle.json"), encoding="utf-8"))
    print(f"{'run':34} {'claims':18} " + " ".join(f"{t[:14]:>16}" for t in CHECKED))
    for f in sorted(os.listdir(os.path.join(HERE, "results"))):
        if not f.startswith("oracle-"):
            continue
        r = json.load(open(os.path.join(HERE, "results", f), encoding="utf-8"))
        label = f[:-5]
        # A visitor claims what its User-Agent says. CloakBrowser's Windows
        # persona and every genuine Windows run claim Chromium on Windows.
        ua = r["ua"]
        family = "Firefox|Windows" if "Firefox" in ua else "Chromium|Windows" if "Windows" in ua else "Chromium|Linux"
        cells = []
        for t in CHECKED:
            c = collections.Counter(verify(oracle, family, j["window"], t, j["results"].get(t)) for j in r["jobs"])
            cells.append(f"{c['pass']:>4}p {c['fail']:>3}f {c['skip']:>3}s")
        print(f"{label:34} {family:18} " + " ".join(f"{x:>16}" for x in cells))


if __name__ == "__main__":
    build() if sys.argv[1] == "build" else verify_all()
