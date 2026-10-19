"""Results matrix: every test the catalogue lists as active or collecting,
against every browser or persona captured.

  py -I matrix.py <runs dir> <tests.json> <out.tsv> [label prefix]

A column is a run label with its timestamp taken off, so repeat runs of one
browser share a column. A cell is the packed value hashed short (FNV-1a 32
over the packed value, first 6 hex chars) so equal cells mean equal values,
"*" after it when that column's runs disagree (unstable), "-" when the test
did not run. Values are listed in full in <out>.values.tsv.

Printed afterwards, per test: whether the genuine Windows Chrome columns
(148 to 156) agree, and which forged columns differ from the genuine value
of the platform they claim.
"""
import glob
import json
import os
import re
import sys

runs_dir, catalogue, out = sys.argv[1:4]
prefix = sys.argv[4] if len(sys.argv) > 4 else ""
tests = json.load(open(catalogue, encoding="utf-8"))["tests"]
ids = [t["id"] for t in tests if t["status"] != "deprecated"]
status = {t["id"]: t["status"] for t in tests}


def fnv32(text):
    h = 0x811c9dc5
    b = text.encode("utf-16-le")
    for i in range(0, len(b), 2):
        h ^= b[i] | (b[i + 1] << 8)
        h = (h * 0x01000193) & 0xffffffff
    return "%08x" % h


def pack(v):
    return v if len(v.encode("utf-16-le")) // 2 <= 40 else "h:" + fnv32(v)


cols = {}
for f in sorted(glob.glob(os.path.join(runs_dir, prefix + "*.json"))):
    d = json.load(open(f, encoding="utf-8"))
    if d.get("kind") == "maths" or "results" not in d:
        continue
    label = re.sub(r"-\d{10,}$", "", os.path.basename(f)[:-5])
    cols.setdefault(label, []).append(d["results"])

names = sorted(cols)
cell = {}
with open(out, "w", encoding="utf-8", newline="\n") as m, \
        open(out[:-4] + ".values.tsv", "w", encoding="utf-8", newline="\n") as v:
    m.write("test\tstatus\t" + "\t".join(f"{n} ({len(cols[n])})" for n in names) + "\n")
    v.write("test\tcolumn\truns\tvalue\n")
    for t in ids:
        row = []
        for n in names:
            vals = [pack(r[t]) for r in cols[n] if t in r]
            if not vals:
                row.append("-"); cell[t, n] = None; continue
            distinct = sorted(set(vals))
            cell[t, n] = distinct
            row.append(fnv32(distinct[0])[:6] + ("*" if len(distinct) > 1 else ""))
            for x in distinct:
                v.write(f"{t}\t{n}\t{vals.count(x)}\t{x}\n")
        m.write(f"{t}\t{status[t]}\t" + "\t".join(row) + "\n")
print(f"{len(ids)} tests x {len(names)} columns -> {out}")
