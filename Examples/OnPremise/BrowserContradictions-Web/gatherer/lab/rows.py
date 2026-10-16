"""Builds baseline payload version 2 rows (PHASE2-FORMATS) from lab runs.

  py rows.py <runs dir> <tests.json> <out prefix> [--deprecate id,id]

Writes <out prefix>.rows.txt, one plain row per line for a harness that
hashes each line, and <out prefix>.payload.txt, the finished payload
("2|h64", the factor line, then the sorted FNV-1a 64 hashes).

Rows are written for active tests only. --deprecate treats the listed ids as
deprecated, which is what Pearl does when the catalogue retires a test: no
rows, so the elements stop counting it.

Runs from the version 2 gatherer carry unversioned ids, which are version 1.
Their values are what gatherer v3 sends for the same ids with its default
inputs, so they become ".1" rows unchanged. f_grid.1 is new in v3; for a v2
run it is worked out from the widths f_metrics and f_emoji captured, which
are exactly the widths f_grid.1 measures with the default inputs.

Noise filter: where runs of one claim from one exact browser build (same
user agent and same a_uachigh answer) disagree on a test, the test is noise
for that claim, not a genuine alternative, and gets no rows for it. Values
that differ between builds of one major version are all kept, as several
genuine answers.
"""
import glob, json, os, struct, sys

# claim as device detection names it -> lab label prefixes of genuine runs
GENUINE = {
    "Chrome|155|Windows": ["win-chrome-headed", "v3-win-chrome-headed"],
    "Edge (Chromium) for Windows|155|Windows": ["win-edge-headed", "v3-win-edge-headed"],
    "Firefox|157|Windows": ["win-firefox-headless", "v3-win-firefox-headless"],
    "Chrome|141|Linux": ["wsl-chromium", "v3-wsl-chromium"],
}
# Inferred claims, for browsers the lab has no genuine capture of.
# Chrome 146 on Windows: the Windows Chrome 155 rows, less c_timezone,
# whose zone name text changes with the browser build.
# Chrome 146 on macOS: f_grid only. Every genuine Chromium captured, on
# Windows and on Linux, measures on the 1/4096 grid, because the grid comes
# from Chromium's own text stack, not from the OS.
INFERRED = [
    ("Chrome|155|Windows", "Chrome|146|Windows", lambda t: t != "c_timezone.1"),
    ("Chrome|155|Windows", "Chrome|146|macOS", lambda t: t == "f_grid.1"),
]


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
        if "f_grid.1" not in r:
            g = grid(r)
            if g:
                r["f_grid.1"] = g
        out.append((d.get("ua", "") + "|" + r.get("a_uachigh.1", ""), r))
    return out


def main():
    runs, catalogue, prefix = sys.argv[1:4]
    deprecate = set()
    if "--deprecate" in sys.argv:
        deprecate = set(sys.argv[sys.argv.index("--deprecate") + 1].split(","))
    tests = json.load(open(catalogue, encoding="utf-8"))["tests"]
    active = {t["id"] for t in tests if t["status"] == "active"} - deprecate
    rows = set()
    values = {}
    report = []

    for claim, labels in GENUINE.items():
        rs = [x for label in labels for x in load(runs, label)]
        seen = {}
        for build, r in rs:
            for test, value in r.items():
                if test in active:
                    seen.setdefault(test, {}).setdefault(build, set()).add(pack(value))
        noise = sorted(t for t, builds in seen.items() if any(len(v) > 1 for v in builds.values()))
        values[claim] = {t: set().union(*builds.values()) for t, builds in seen.items() if t not in noise}
        report.append((claim, f"{len(rs)} runs", noise))
    for source, claim, keep in INFERRED:
        values[claim] = {t: v for t, v in values[source].items() if keep(t)}
        report.append((claim, "inferred", []))

    for claim, tv in values.items():
        for test, vs in tv.items():
            rows.add(f"{claim}|{test}")
            for v in vs:
                rows.add(f"{claim}|{test}|{v}")

    lines = sorted(rows)
    open(prefix + ".rows.txt", "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")
    hashes = sorted({"%016x" % fnv64(r) for r in lines})
    payload = "2|h64\nbrowser|major|platform|test|value\n" + "".join(h + "\n" for h in hashes)
    open(prefix + ".payload.txt", "w", encoding="utf-8", newline="\n").write(payload)
    coverage = [r for r in lines if r.count("|") == 3]
    print(f"{len(active)} active tests, {len(lines)} rows ({len(coverage)} coverage), payload {len(payload)} bytes")
    for claim, source, noise in report:
        multi = sorted(t for t, v in values[claim].items() if len(v) > 1)
        print(f"  {claim:42} {source:9} covers {len(values[claim]):2}"
              + (f"  noise {','.join(noise)}" if noise else "")
              + (f"  several genuine {','.join(multi)}" if multi else ""))
    if deprecate:
        print("  deprecated for this build:", ", ".join(sorted(deprecate)))


if __name__ == "__main__":
    main()
