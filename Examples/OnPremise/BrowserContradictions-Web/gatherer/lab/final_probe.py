"""Replays lab captures through the phase 2 element rule against rows built
by rows.py, with the version independent rows, and checks every verdict.

  py -I final_probe.py <runs dir> <tests.json> [<pearl rows> <range name,...>]

Works from the hashed payload only, as the elements do: a claim's test is
counted when its coverage row exists, exact claim rows first, otherwise the
"*" rows when the claimed major is inside a range name's span. A request
whose tests are all uncovered is Unknown. False reads as Medium and True as
Low in the HumanConfidence rule.

Scenario A builds rows from every genuine capture. Scenario B holds out
Chrome 150 and 156, to check an uncaptured major inside the range passes
through the "*" rows and one above the range is Unknown.

The claim is what device detection would make of the user agent: Edg/ is
"Edge (Chromium) for Windows", HeadlessChrome is "Chrome Headless" (no
baseline, so Unknown), Firefox/ is Firefox, otherwise Chrome.
"""
import glob
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
rows = {}
exec(compile(open(os.path.join(HERE, "rows.py"), encoding="utf-8").read(), "rows.py", "exec"), rows)
runs_dir, catalogue = sys.argv[1:3]
pearl = sys.argv[3:5]


def claim_of(ua):
    platform = "Windows" if "Windows NT" in ua else "macOS" if "Mac OS X" in ua else "Linux" if "Linux" in ua else "?"
    for pat, name in ((r"Edg/(\d+)", "Edge (Chromium) for Windows"), (r"HeadlessChrome/(\d+)", "Chrome Headless"),
                      (r"Firefox/(\d+)", "Firefox"), (r"Chrome/(\d+)", "Chrome")):
        m = re.search(pat, ua)
        if m:
            return name, int(m.group(1)), platform
    return "?", 0, platform


class Element:
    def __init__(self, lines, ranges):
        self.hashes = {rows["fnv64"](r) for r in lines}
        self.ranges = []
        for name in ranges:
            browser, span, platform = name.split("|")
            lo, hi = map(int, span.split("-"))
            self.ranges.append((browser, platform, lo, hi))

    def has(self, row):
        return rows["fnv64"](row) in self.hashes

    def verdict(self, browser, major, platform, results):
        exact = f"{browser}|{major}|{platform}"
        star = f"{browser}|*|{platform}"
        in_range = any(b == browser and p == platform and lo <= major <= hi for b, p, lo, hi in self.ranges)
        counted, failed, scopes = 0, [], set()
        for test, value in sorted(results.items()):
            if self.has(f"{exact}|{test}"):
                use = exact; scopes.add("exact")
            elif in_range and self.has(f"{star}|{test}"):
                use = star; scopes.add("any")
            else:
                continue
            counted += 1
            if not self.has(f"{use}|{test}|{rows['pack'](value)}"):
                failed.append(test)
        if counted == 0:
            return "Unknown", 0, [], ""
        return ("True" if failed else "False"), counted, failed, "+".join(sorted(scopes))


# Expected verdict per wave 3 label (C:/bc-data/v4/runs), every label
# listed. The claim follows the user agent: a HeadlessChrome user agent is
# "Chrome Headless", a claim with no baseline, so Unknown.
EXPECT = [
    # Final hardening (C:/bc-data/v5/runs). Rows come from v5-train- only,
    # so every v5-stress- and v5-extra- run is held out. Genuine browsers
    # must be False whatever the start conditions.
    ("v5-train-cft", "True"), ("v5-train-", "False"), ("v5-stress-", "False"),
    # Protected content switched off refuses Widevine exactly as Chrome for
    # Testing does: the one known false positive (a_widevine.1).
    ("v5-extra-chrome155-noprotected", "True"), ("v5-extra-", "False"),
    ("v5-forge-", "True"),
    # Chrome for Testing is an automation build, caught on purpose by the
    # build flavour tests (a_voices.2, a_widevine.1).
    ("v4-win-cft", "True"),
    ("v4-win-chrome155-headed", "False"), ("v4-win-chrome156-headed", "False"),
    ("v4-win-edge155-headed", "False"), ("v4-win-firefox158-headed", "False"),
    ("v4-auto-chrome155-headlessflag", "Unknown"),
    # --headless=new with the headed user agent: c_screen800.1.
    ("v4-auto-chrome155-headlessua", "True"),
    ("v4-auto-puppeteer-chrome155-headless", "Unknown"), ("v4-auto-playwright-chrome155-headless", "Unknown"),
    ("v4-auto-puppeteer-chrome155-headed", "True"), ("v4-auto-playwright-chrome155-headed", "True"),
    ("v4-wsl-chromium141", "Unknown"),
    ("v4-forge-", "True"), ("v4-cloak-", "True"),
]


def expect(label):
    for prefix, want in EXPECT:
        if label.startswith(prefix):
            return want
    # Earlier waves' labels, as judged before.
    if label.startswith(("fin-win-cft", "vr-chrome148", "vr-chrome150")):
        return "True"
    if label.startswith(("fin-win-", "vr-")) and "headless" not in label:
        return "False"
    if label.startswith("fin-wsl-chromium141"):
        return "Unknown"
    if label.startswith(("fin-cloak-", "fin-forge-", "fin-win-firefox158-headless")):
        return "True"
    return None


def replay(element, title, runs, hold=(), pearl=False):
    print(f"\n== {title}")
    ok = bad = 0
    for label, d in runs:
        b, m, p = claim_of(d["ua"])
        results = {rows["norm"](k): v for k, v in d["results"].items()}
        got, n, failed, scope = element.verdict(b, m, p, results)
        want = expect(label)
        if label.startswith(hold):
            # With 156 held out only one branded major (155) carries the build
            # flavour tests, so they get no "*" rows and 150 is judged on the
            # engine tests alone.
            want = {150: "False", 156: "Unknown"}.get(m, want)
        if pearl and label.startswith(("fin-cloak-macos", "fin-cloak-linux", "v4-cloak-macos", "v4-cloak-linux")):
            want = "Unknown"  # Pearl has no macOS or Linux Chrome claim and infers no rows
        if pearl and label.startswith("fin-wsl-chromium141"):
            want = "False"  # Pearl keeps the Chrome Headless claim it was given
        mark = "    " if want is None else "ok  " if got == want else "FAIL"
        if want is not None:
            ok += got == want; bad += got != want
        print(f"{mark} {label[:44]:44} {b[:12]}|{m}|{p:8} {got:7} {scope:9} {n:2} counted"
              + (f"  failed {','.join(failed)}" if failed else ""))
    print(f"{ok} of {ok + bad} as expected")
    return bad == 0


def load_runs():
    out = []
    for f in sorted(glob.glob(os.path.join(runs_dir, "*.json"))):
        d = json.load(open(f, encoding="utf-8"))
        if "results" in d and d.get("kind") != "maths":
            out.append((re.sub(r"-\d{10,}$", "", os.path.basename(f)[:-5]), d))
    return out


all_runs = load_runs()
lines, ranges, _, _, _ = rows["build"](runs_dir, catalogue)
a = replay(Element(lines, ranges), "Scenario A: every genuine capture", all_runs)

saved = dict(rows["GENUINE"])
rows["GENUINE"].pop("Chrome|150|Windows")
rows["GENUINE"].pop("Chrome|156|Windows")
lines, ranges, _, _, _ = rows["build"](runs_dir, catalogue)
rows["GENUINE"].update(saved)
HOLD = ("v5-train-cft150", "v5-train-chrome156", "v5-stress-chrome156", "v4-win-cft150", "v4-win-chrome156", "fin-win-cft150", "fin-win-chrome156", "vr-chrome150", "vr-chrome156")
held = [(l, d) for l, d in all_runs if l.startswith(HOLD)]
print("\nranges without 150 and 156:", ", ".join(ranges))
b = replay(Element(lines, ranges), "Scenario B: Chrome 150 and 156 held out", held,
           hold=HOLD)
c = True
if pearl:
    lines = [l for l in open(pearl[0], encoding="utf-8").read().splitlines() if l.count("|") >= 3]
    c = replay(Element(lines, pearl[1].split(",")), "Scenario C: Pearl BaselineDryRun rows " + pearl[1], all_runs,
               pearl=True)
sys.exit(0 if a and b and c else 1)
