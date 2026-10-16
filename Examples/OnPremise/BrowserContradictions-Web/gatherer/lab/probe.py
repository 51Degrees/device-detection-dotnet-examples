"""Replays lab runs against a JSON endpoint and prints the verdicts.

  py probe.py <runs dir> <json url> [payload file]

Each case sends one captured run with the user agent it ran under (or the
one given), as a version 3 payload (versioned ids, deprecated tests left
out, as gatherer v3 sends) or as the original version 2 payload. Also checks
that no BrowserBaseline payload or row reaches the response.
"""
import glob, json, os, sys, urllib.parse, urllib.request

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
runs, url = sys.argv[1], sys.argv[2]
payload_file = sys.argv[3] if len(sys.argv) > 3 else None
HERE = os.path.dirname(os.path.abspath(__file__))
CATALOGUE = json.load(open(os.path.join(HERE, "..", "tests.json"), encoding="utf-8"))["tests"]
RUNS_IN_V3 = {t["id"] for t in CATALOGUE if t["status"] != "deprecated"}
CHROME155 = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/155.0.0.0 Safari/537.36"
LINUX141 = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36"

# Only the helpers of rows.py, its main() runs only as a script.
src = open(os.path.join(HERE, "rows.py"), encoding="utf-8").read()
rows = {}
exec(compile(src, "rows.py", "exec"), rows)


def run_of(label):
    f = sorted(glob.glob(os.path.join(runs, label + "-[0-9]*.json")))[-1]
    return json.load(open(f, encoding="utf-8"))


def v3(d, extra=None, change=None):
    r = {rows["norm"](k): v for k, v in d["results"].items()}
    if "f_grid.1" not in r and rows["grid"](r):
        r["f_grid.1"] = rows["grid"](r)
    r = {k: v for k, v in r.items() if k in RUNS_IN_V3}
    r.update(extra or {})
    r.update(change or {})
    return "3|" + "&".join(f"{k}={urllib.parse.quote(rows['pack'](v), safe='')}" for k, v in sorted(r.items()))


def v2(d):
    return "2|" + "&".join(f"{k}={urllib.parse.quote(rows['pack'](v), safe='')}" for k, v in sorted(d["results"].items()))


leak_rows = []
if payload_file:
    leak_rows = open(payload_file, encoding="utf-8").read().split("\n")[2:6]


def ask(name, ua, payload):
    body = "51D_HasWebDriver=False&51D_IsVisible=True"
    if payload is not None:
        body += "&51D_BrowserTests=" + urllib.parse.quote(payload, safe="")
    req = urllib.request.Request(url, data=body.encode(), headers={"User-Agent": ua, "Content-Type": "application/x-www-form-urlencoded"})
    text = urllib.request.urlopen(req).read().decode("utf-8")
    d = json.loads(text)
    b = d.get("browser", {})
    hc = [v for k, v in d.get("derived", {}).items() if "proof" in k]
    dev = d.get("device", {})
    leaked = "2|h64" in text or any(h and h in text for h in leak_rows) or any(v for k, v in dev.items() if k.startswith("browserbaseline") and not k.endswith("nullreason"))
    print(f"{name:40} {str(b.get('claim')):42} {str(b.get('hascontradictions')):8} {str(hc):12} leak={leaked} failed={b.get('contradictions')}")


chrome, edge, ff = run_of("win-chrome-headed"), run_of("win-edge-headed"), run_of("win-firefox-headless")
wsl, spoof = run_of("wsl-chromium"), run_of("wsl-chromium-claims-windows")
cw, cm = run_of("cloak-windows"), run_of("cloak-macos")
emoji_changed = {"f_emoji.1": "48.000000000000000,48.000000000000000,48.000000000000000,48.000000000000000"}
for name, ua, p in [
    ("no payload", CHROME155, None),
    ("v3 genuine Chrome 155", chrome["ua"], v3(chrome)),
    ("v3 genuine Edge 155", edge["ua"], v3(edge)),
    ("v3 genuine Firefox 157", ff["ua"], v3(ff)),
    ("v3 genuine Linux Chromium 141", LINUX141, v3(wsl)),
    ("v3 Linux Chromium claiming Windows", spoof["ua"], v3(spoof)),
    ("v3 CloakBrowser Windows persona", cw["ua"], v3(cw)),
    ("v3 CloakBrowser macOS persona", cm["ua"], v3(cm)),
    ("v2 payload genuine Chrome 155", chrome["ua"], v2(chrome)),
    ("v2 payload Linux claiming Windows", spoof["ua"], v2(spoof)),
    ("v3 Chrome + new collecting test", chrome["ua"], v3(chrome, {"e_intlfmt.1": "anything", "zz_unknown.7": "x"})),
    ("v3 CloakBrowser Win + collecting test", cw["ua"], v3(cw, {"e_intlfmt.1": "anything"})),
    ("v3 Chrome, f_emoji tampered", chrome["ua"], v3(chrome, change=emoji_changed)),
    ("v3 Chrome payload under Edge UA", edge["ua"], v3(chrome)),
    ("v3 only unknown tests", CHROME155, "3|zz_unknown.1=1&e_intlfmt.1=x"),
]:
    ask(name, ua, p)
