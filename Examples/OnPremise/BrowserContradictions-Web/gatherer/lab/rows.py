"""Builds baseline payload version 2 rows (PHASE2-FORMATS) from lab runs.

  py rows.py <runs dir> <tests.json> <out prefix> [--deprecate id,id]

Writes <out prefix>.rows.txt, one plain row per line for a harness that
hashes each line, <out prefix>.payload.txt, the finished payload ("2|h64",
the factor line, then the sorted FNV-1a 64 hashes), and
<out prefix>.ranges.txt, the version independent value names
(browser|from-to|platform) the element reads the range from.

Rows are written for active tests only, never for NO_ROWS. --deprecate treats the listed ids as
deprecated, which is what Pearl does when the catalogue retires a test: no
rows, so the elements stop counting it.

Runs from the version 2 gatherer carry unversioned ids, which are version 1.

Genuine runs: GENUINE maps each claim to the label prefixes of its genuine
runs. Chrome for Testing is a genuine engine but not the browser people run:
it has no Google voices, no Widevine and, on the lab VM, no WebGL. Its runs
therefore give rows for every test except the build flavour tests
(FLAVOUR), which come from branded Chrome only.

Noise filter: where runs of one claim from one exact browser build (same
user agent and same a_uachigh answer) disagree on a test, the test is noise
for that claim, not a genuine alternative, and gets no rows for it. Values
that differ between builds of one major version are all kept, as several
genuine answers.

Version independent rows (branch feature/bc-version-rows): where at least
two captured majors of one browser and platform give rows for a test and
all of them give the same values, the test also gets rows with the major
written as "*". The range is 12 majors below the oldest capture to the
newest capture.

Inferred rows: f_grid.2 answers "grid" in every Chromium captured, on
Windows (Chrome 148, 150, 155, 156, Edge 155) and on Linux (Chromium 141),
because the grid is HarfBuzz's, not the OS's. So it gets "*" rows for
Chrome on macOS and on Linux too, where the lab has no or one capture.
"""
import glob
import json
import os
import struct
import sys

# claim as device detection names it -> lab label prefixes of genuine runs
GENUINE = {
    # Wave 3 captures (C:/bc-data/v4/runs), 20 Oct 2026. Earlier waves used
    # the fin- and vr- labels. Chrome for Testing 148 and 150 give the engine
    # rows of their majors, branded Chrome the build flavour rows.
    # The final hardening training set (C:/bc-data/v5/runs, v5-train-) is
    # kept apart from its stress runs (v5-stress-, v5-extra-), which are
    # never rows.
    "Chrome|148|Windows": ["v5-train-cft148", "v4-win-cft148-headed", "fin-win-cft148-headed", "vr-chrome148"],
    "Chrome|150|Windows": ["v5-train-cft150", "v4-win-cft150-headed", "fin-win-cft150-headed", "vr-chrome150"],
    "Chrome|155|Windows": ["v5-train-chrome155", "v4-win-chrome155-headed", "fin-win-chrome155-headed", "vr-chrome155"],
    "Chrome|156|Windows": ["v5-train-chrome156", "v4-win-chrome156-headed", "fin-win-chrome156-headed", "vr-chrome156"],
    "Edge (Chromium) for Windows|155|Windows": ["v5-train-edge155", "v4-win-edge155-headed", "fin-win-edge155-headed"],
    # Headed only: headless Firefox measures text differently.
    "Firefox|158|Windows": ["v5-train-firefox158", "v4-win-firefox158-headed", "fin-win-firefox158-headed"],
    # Playwright's Linux Chromium is driven (Runtime enabled, IPC flooding
    # protection off), so from wave 3 it is automation, not a genuine Linux
    # browser. Kept for the earlier waves' runs only.
    "Chrome|141|Linux": ["fin-wsl-chromium141"],
}
# Label prefixes of Chrome for Testing runs, and the tests they give no rows for.
FOR_TESTING = ("v5-train-cft", "v4-win-cft", "fin-win-cft", "vr-chrome148", "vr-chrome150")
FLAVOUR = {"a_voices.2", "a_widevine.1", "g_renderer.2", "p_apis.1"}
# Bad actor rules that never get baseline rows, whatever their status: lab
# windows answer what a person never sends (s_offscreen.1 is "1h" or "1v"
# for an off-screen lab window).
NO_ROWS = {"s_offscreen.1"}
INFERRED_ANY = [("Chrome", "macOS", "f_grid.2", "grid"), ("Chrome", "Linux", "f_grid.2", "grid")]
RANGE_BELOW = 12


def fnv32(text):
    h = 0x811c9dc5
    b = text.encode("utf-16-le")
    for i in range(0, len(b), 2):
        h ^= b[i] | (b[i + 1] << 8)
        h = (h * 0x01000193) & 0xffffffff
    return "%08x" % h


def fnv64(text):
    h = 0xcbf29ce484222325
    for c in text.encode("utf-8"):
        h ^= c
        h = (h * 0x100000001b3) & 0xffffffffffffffff
    return h


def pack(v):
    return v if len(v.encode("utf-16-le")) // 2 <= 40 else "h:" + fnv32(v)


def norm(test):
    return test if "." in test else test + ".1"


def grid(results):
    """f_grid.1 worked out from a version 2 run's widths (default inputs)."""
    ws = []
    for k in ("f_metrics.1", "f_emoji.1"):
        v = results.get(k, "")
        if not v or v in ("na", "err") or ":" in v:
            return None
        ws += [float(x) for x in v.split(",")]
    if not ws:
        return None
    on_grid = all((w * 4096).is_integer() for w in ws)
    f32 = all(struct.unpack("f", struct.pack("f", w))[0] == w for w in ws)
    return "grid" if on_grid else "f32" if f32 else "off"


def load(runs, label):
    out = []
    for f in sorted(glob.glob(os.path.join(runs, label + "-[0-9]*.json"))):
        d = json.load(open(f, encoding="utf-8"))
        r = {norm(k): v for k, v in d["results"].items()}
        if "f_grid.1" not in r and "f_grid.2" not in r:
            g = grid(r)
            if g:
                r["f_grid.1"] = g
        if os.path.basename(f).startswith(FOR_TESTING):
            r = {k: v for k, v in r.items() if k not in FLAVOUR}
        out.append((d.get("ua", "") + "|" + r.get("a_uachigh.1", ""), r))
    return out


def build(runs, catalogue, deprecate=()):
    """Returns (rows, ranges, report, values, active) for the active tests."""
    tests = json.load(open(catalogue, encoding="utf-8"))["tests"]
    active = {t["id"] for t in tests if t["status"] == "active"} - set(deprecate) - NO_ROWS
    values, report = {}, []
    for claim, labels in GENUINE.items():
        rs = [x for label in labels for x in load(runs, label)]
        if not rs:
            continue
        seen = {}
        for build_, r in rs:
            for test, value in r.items():
                if test in active:
                    seen.setdefault(test, {}).setdefault(build_, set()).add(pack(value))
        noise = sorted(t for t, builds in seen.items() if any(len(v) > 1 for v in builds.values()))
        values[claim] = {t: set().union(*builds.values()) for t, builds in seen.items() if t not in noise}
        report.append((claim, f"{len(rs)} runs", noise))

    # Version independent rows.
    ranges = {}
    groups = {}
    for claim in list(values):
        browser, major, platform = claim.split("|")
        groups.setdefault((browser, platform), []).append(int(major))
    for (browser, platform), majors in groups.items():
        majors.sort()
        name = f"{browser}|{max(majors[0] - RANGE_BELOW, 0)}-{majors[-1]}|{platform}"
        star = f"{browser}|*|{platform}"
        stable, dependent = [], []
        for test in sorted(active):
            vs = [frozenset(values[f"{browser}|{m}|{platform}"][test]) for m in majors
                  if test in values[f"{browser}|{m}|{platform}"]]
            if len(vs) >= 2 and len(set(vs)) == 1:
                values.setdefault(star, {})[test] = set(vs[0])
                stable.append(test)
            elif len(vs) >= 2:
                dependent.append(test)
        if stable:
            ranges[name] = (majors, stable, dependent)
    for browser, platform, test, value in INFERRED_ANY:
        if test not in active:
            continue
        star = f"{browser}|*|{platform}"
        values.setdefault(star, {}).setdefault(test, set()).add(value)
        if not any(n.startswith(browser + "|") and n.endswith("|" + platform) for n in ranges):
            span = next(n for n in ranges if n.startswith(browser + "|")).split("|")[1]
            ranges[f"{browser}|{span}|{platform}"] = ([], [test], [])
        report.append((star, "inferred", []))

    rows = set()
    for claim, tv in values.items():
        for test, vs in tv.items():
            rows.add(f"{claim}|{test}")
            for v in vs:
                rows.add(f"{claim}|{test}|{v}")
    return sorted(rows), ranges, report, values, active


def main():
    runs, catalogue, prefix = sys.argv[1:4]
    deprecate = set()
    if "--deprecate" in sys.argv:
        deprecate = set(sys.argv[sys.argv.index("--deprecate") + 1].split(","))
    lines, ranges, report, values, active = build(runs, catalogue, deprecate)
    open(prefix + ".rows.txt", "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")
    hashes = sorted({"%016x" % fnv64(r) for r in lines})
    payload = "2|h64\nbrowser|major|platform|test|value\n" + "".join(h + "\n" for h in hashes)
    open(prefix + ".payload.txt", "w", encoding="utf-8", newline="\n").write(payload)
    open(prefix + ".ranges.txt", "w", encoding="utf-8", newline="\n").write("".join(
        f"{n}\t{','.join(map(str, m))}\t{','.join(s)}\t{','.join(d)}\n" for n, (m, s, d) in sorted(ranges.items())))
    coverage = [r for r in lines if r.count("|") == 3]
    print(f"{len(active)} active tests, {len(lines)} rows ({len(coverage)} coverage), payload {len(payload)} bytes")
    for claim, source, noise in report:
        multi = sorted(t for t, v in values[claim].items() if len(v) > 1)
        print(f"  {claim:42} {source:9} covers {len(values[claim]):2}"
              + (f"  noise {','.join(noise)}" if noise else "")
              + (f"  several genuine {','.join(multi)}" if multi else ""))
    for n, (m, s, d) in sorted(ranges.items()):
        print(f"  range {n:36} majors {m}  version independent {len(s)}"
              + (f"  version dependent {','.join(d)}" if d else ""))
    if deprecate:
        print("  deprecated for this build:", ", ".join(sorted(deprecate)))


if __name__ == "__main__":
    main()
