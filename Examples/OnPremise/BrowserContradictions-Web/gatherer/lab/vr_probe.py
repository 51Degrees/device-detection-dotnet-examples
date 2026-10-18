"""Replays captured runs against the proof site JSON endpoint to check the
version independent rows: exact claims, uncaptured majors inside the
range, majors outside it, and forged claims.

  py -I vr_probe.py <json url>
"""
import glob
import json
import os
import sys
import urllib.parse
import urllib.request

url = sys.argv[1]
LAB = r"C:\bc-wt\examples-phase2\Examples\OnPremise\BrowserContradictions-Web\gatherer"
CATALOGUE = json.load(open(os.path.join(LAB, "tests.json"), encoding="utf-8"))["tests"]
RUNS_IN_V3 = {t["id"] for t in CATALOGUE if t["status"] != "deprecated"}
rows = {}
exec(compile(open(os.path.join(LAB, "lab", "rows.py"), encoding="utf-8").read(), "rows.py", "exec"), rows)


def load(folder, label):
    f = sorted(glob.glob(os.path.join(folder, label + "-[0-9]*.json")))[-1]
    return json.load(open(f, encoding="utf-8"))


def v3(d):
    r = {rows["norm"](k): v for k, v in d["results"].items()}
    if "f_grid.1" not in r and rows["grid"](r):
        r["f_grid.1"] = rows["grid"](r)
    r = {k: v for k, v in r.items() if k in RUNS_IN_V3}
    return "3|" + "&".join(
        f"{k}={urllib.parse.quote(rows['pack'](v), safe='')}" for k, v in sorted(r.items()))


def ua(major, platform="Windows NT 10.0; Win64; x64"):
    return (f"Mozilla/5.0 ({platform}) AppleWebKit/537.36 (KHTML, like Gecko) "
            f"Chrome/{major}.0.0.0 Safari/537.36")


def ask(name, user_agent, payload, expect):
    body = "51D_HasWebDriver=False&51D_IsVisible=True&51D_BrowserTests=" + \
        urllib.parse.quote(payload, safe="")
    req = urllib.request.Request(url, data=body.encode(), headers={
        "User-Agent": user_agent, "Content-Type": "application/x-www-form-urlencoded"})
    text = urllib.request.urlopen(req).read().decode("utf-8")
    b = json.loads(text).get("browser", {})
    got = str(b.get("hascontradictions"))
    leak = "2|h64" in text
    mark = "ok  " if got == expect and not leak else "FAIL"
    print(f"{mark} {name:46} {str(b.get('claim')):24} {got:8} "
          f"scope={str(b.get('claimscope')):12} failed={b.get('contradictions')}")
    return got == expect and not leak


VR = os.path.join(LAB, "lab", "runs")
OLD = r"C:\bc-data\lab\runs"
c148, c150, c155, c156 = (load(VR, f"vr-chrome{m}") for m in (148, 150, 155, 156))
cloak, spoof = load(OLD, "cloak-windows"), load(OLD, "wsl-chromium-claims-windows")
cases = [
    ("genuine Chrome 148, captured", c148["ua"], v3(c148), "False"),
    ("genuine Chrome 155, captured", c155["ua"], v3(c155), "False"),
    ("genuine Chrome 150, not captured, in range", c150["ua"], v3(c150), "False"),
    ("genuine Chrome 156 beta, above range", c156["ua"], v3(c156), "Unknown"),
    ("genuine Chrome 150 claiming 130, below range", ua(130), v3(c150), "Unknown"),
    ("CloakBrowser claiming Chrome 146 Windows", cloak["ua"], v3(cloak), "True"),
    ("Linux Chromium claiming Chrome 155 Windows", spoof["ua"], v3(spoof), "True"),
    ("Linux Chromium claiming Chrome 150 Windows", ua(150), v3(spoof), "True"),
    ("Linux Chromium claiming Chrome 140 Windows", ua(140), v3(spoof), "True"),
]
passed = sum(ask(*c) for c in cases)
print(f"\n{passed} of {len(cases)} as expected")
sys.exit(0 if passed == len(cases) else 1)
