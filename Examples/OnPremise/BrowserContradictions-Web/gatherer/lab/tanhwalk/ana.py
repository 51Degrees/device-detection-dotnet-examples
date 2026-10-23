"""Which algorithm is each browser family using, and where do they differ."""
import json
import math
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fdlibm  # noqa: E402


def hb(x):
    return struct.pack(">d", x).hex()


def fb(h):
    return struct.unpack(">d", bytes.fromhex(h))[0]


inp = json.load(open(os.path.join(HERE, "inputs.json")))
res = {os.path.splitext(f)[0]: json.load(open(os.path.join(HERE, "results", f)))
       for f in os.listdir(os.path.join(HERE, "results"))}
cr = json.load(open(os.path.join(HERE, "refs", "cr.json")))
glibc = json.load(open(os.path.join(HERE, "refs", "glibc.json")))

xs = [fb(h) for h in inp["tanh"]]
fd = [hb(fdlibm.tanh(x)) for x in xs]
uc = [hb(math.tanh(x)) for x in xs]
for name in ("linux-pw-shell", "linux-cloak-win", "win-firefox", "win-chrome155"):
    r = res[name]["tanh"]
    print(f"{name:20} vs fdlibm port: {sum(a != b for a, b in zip(r, fd))}   vs ucrt: {sum(a != b for a, b in zip(r, uc))}")

# glibc vs fdlibm differing input
for x, a, b in zip(xs, fd, glibc["tanh"]):
    if a != b:
        print("glibc/fdlibm differ at", repr(x))

# where ucrt and cr differ, by |x| band
bands = [(0, 1e-3), (1e-3, 0.05), (0.05, 0.55), (0.55, 1), (1, 3), (3, 19), (19, 1e9)]
for lo, hi in bands:
    idx = [i for i, x in enumerate(xs) if lo <= abs(x) < hi]
    if not idx:
        continue
    d_cr = sum(uc[i] != cr["tanh"][i] for i in idx)
    d_fd = sum(uc[i] != fd[i] for i in idx)
    print(f"|x| in [{lo},{hi}): n={len(idx):5}  ucrt!=cr {d_cr:4}  ucrt!=fdlibm {d_fd:4}")

# hypothesis: ucrt tanh is fdlibm's structure on top of ucrt expm1
def hyp(x):
    ax = abs(x)
    if ax >= 22:
        z = 1.0
    elif ax >= 1:
        t = math.expm1(2 * ax)
        z = 1 - 2 / (t + 2)
    else:
        t = math.expm1(-2 * ax)
        z = -t / (t + 2)
    return z if x >= 0 else -z

print("fdlibm structure over ucrt expm1 vs ucrt tanh:", sum(hb(hyp(x)) != u for x, u in zip(xs, uc)))
