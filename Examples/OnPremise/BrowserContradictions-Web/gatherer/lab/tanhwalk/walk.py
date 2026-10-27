"""Server side of m_tanhwalk.1 in Python, the reference TanhWalk.cs is
checked against: the walk under a given tanh, and the tie tolerant verifier
for the Windows family. The browser side is tanhWalk in
gatherer/browser-tests-v3.js.

  py -I walk.py seeds <count> 64         write seeds.json (server chosen seeds;
                                         the gatherer always walks 64 steps)
  py -I walk.py host <label>             run the walk with this host's math.tanh
                                         (UCRT on Windows, glibc under WSL)
  py -I walk.py report                   verify every results/walk-*.json, which
                                         walk.html writes (mathlab.py serve)

Families:
  fdlibm   exact. Firefox on every OS, Chromium 147 and earlier on every OS.
  ucrt~    correctly rounded tanh, except that a step whose exact value lies
           within WINDOW ulps of a rounding tie may round either way.
           Chromium 148 and later on Windows (std::tanh is the UCRT's).
"""
import json
import math
import os
import random
import struct
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fdlibm  # noqa: E402

M32 = 0xFFFFFFFF
WINDOW = 0.02      # ulps either side of a tie that may round either way
BUDGET = 4096      # most walk steps the verifier will spend on branches
DEFAULT_SEED = (2654435769, 2135587861)   # the gatherer's DEFAULTS.walk


def fnv1a32(text):
    h = 0x811C9DC5
    for ch in text:
        h ^= ord(ch)
        h = (h * 0x01000193) & M32
    return f"{h:08x}"


def tag(a, b):
    """The gatherer's tag() for walk inputs [[a, b]]: "" for the default."""
    return "" if (a, b) == DEFAULT_SEED else "p" + fnv1a32(f"[[{a},{b}]]") + ":"


def untag(a, b, value):
    """The 16 hex chars of a sent value, or None when its tag is not the
    one for this seed. Python host runs write bare walks."""
    if value is None:
        return None
    if len(value) == 16:
        return value
    t = tag(a, b)
    return value[len(t):] if t and value.startswith(t) else None


def imul(a, b):
    return (a * b) & M32


def mix(s1, s2):
    s1 = imul(s1 ^ (s1 >> 16), 0x85EBCA6B)
    s1 = (s1 ^ (s1 >> 13)) & M32
    s2 = imul(s2 ^ (s2 >> 16), 0xC2B2AE35)
    s2 = (s2 ^ (s2 >> 13)) & M32
    return s1, s2


def step_input(s1, s2):
    return ((s1 * 1048576 + (s2 >> 12)) / 4503599627370496 - 0.5) * 0.1


def fold(s1, s2, t):
    u = struct.unpack(">Q", struct.pack(">d", t))[0]
    hi, lo = u >> 32, u & M32
    return (s1 ^ lo) & M32, (s2 ^ hi ^ imul(lo, 0x27D4EB2F)) & M32


def finish(s1, s2):
    s1 = imul(s1 ^ (s1 >> 16), 0x85EBCA6B)
    s2 = imul(s2 ^ (s2 >> 16), 0xC2B2AE35)
    return f"{s1:08x}{s2:08x}"


def walk(a, b, n, tanh):
    s1, s2 = a & M32, b & M32
    for _ in range(n):
        s1, s2 = mix(s1, s2)
        s1, s2 = fold(s1, s2, tanh(step_input(s1, s2)))
    return finish(s1, s2)


_mp = None


def cr_choices(x):
    """The correctly rounded tanh(x), plus its neighbour when the exact value
    is within WINDOW ulps of the tie between them."""
    global _mp
    if _mp is None:
        import mpmath
        mpmath.mp.prec = 200
        _mp = mpmath
    e = _mp.tanh(_mp.mpf(x))
    c = float(e)
    if e == c:
        return [c]
    n = math.nextafter(c, math.inf) if e > c else math.nextafter(c, -math.inf)
    mid = (_mp.mpf(c) + _mp.mpf(n)) / 2
    return [c, n] if abs(e - mid) / abs(n - c) < WINDOW else [c]


def verify_tolerant(a, b, n, answer):
    """True when some walk whose every step is correctly rounded, or rounds
    the other way at a near tie, ends at answer. None when the branches run
    past BUDGET (the element then skips the test)."""
    spent = [0]

    def go(s1, s2, i):
        if i == n:
            return finish(s1, s2) == answer
        spent[0] += 1
        if spent[0] > BUDGET:
            raise OverflowError
        m1, m2 = mix(s1, s2)
        for t in cr_choices(step_input(m1, m2)):
            f1, f2 = fold(m1, m2, t)
            if go(f1, f2, i + 1):
                return True
        return False

    try:
        return go(a & M32, b & M32, 0)
    except OverflowError:
        return None


def main():
    cmd = sys.argv[1]
    if cmd == "seeds":
        r = random.Random(int(time.time()))
        count, n = int(sys.argv[2]), int(sys.argv[3])
        json.dump({"n": n, "seeds": [[r.getrandbits(32), r.getrandbits(32)] for _ in range(count)]},
                  open(os.path.join(HERE, "seeds.json"), "w"))
        print(f"{count} seeds, {n} steps")
    elif cmd == "host":
        seeds = json.load(open(os.path.join(HERE, "seeds.json")))
        label = sys.argv[2]
        out = {"label": label, "ua": f"python {sys.platform}", "n": seeds["n"],
               "walks": [walk(a, b, seeds["n"], math.tanh) for a, b in seeds["seeds"]]}
        os.makedirs(os.path.join(HERE, "results"), exist_ok=True)
        json.dump(out, open(os.path.join(HERE, "results", label + ".json"), "w"))
        print("wrote", label)
    elif cmd == "report":
        seeds = json.load(open(os.path.join(HERE, "seeds.json")))
        n = seeds["n"]
        exp_fd = [walk(a, b, n, fdlibm.tanh) for a, b in seeds["seeds"]]
        da, db = DEFAULT_SEED
        d_fd = walk(da, db, n, fdlibm.tanh)
        print(f"{len(seeds['seeds'])} seeds, {n} steps, tie window {WINDOW} ulp\n")
        print(f"{'run':24} {'ms/walk':>8} {'bad tag':>8} {'fdlibm exact':>13} {'ucrt~ pass':>11} "
              f"{'ucrt~ skip':>11} {'default seed':>13}  ua")
        for f in sorted(os.listdir(os.path.join(HERE, "results"))):
            if not f.startswith("walk-"):
                continue
            r = json.load(open(os.path.join(HERE, "results", f)))
            if r.get("n") != n or len(r["walks"]) != len(seeds["seeds"]):
                print(f"{f}: stale, other seeds")
                continue
            answers = [untag(a, b, w) for (a, b), w in zip(seeds["seeds"], r["walks"])]
            badtag = sum(1 for x in answers if x is None)
            fd = sum(w == e for w, e in zip(answers, exp_fd))
            ok = skip = 0
            for (a, b), w in zip(seeds["seeds"], answers):
                if w is None:
                    continue
                v = verify_tolerant(a, b, n, w)
                ok += v is True
                skip += v is None
            dv = r.get("defaultSeed")
            dfam = "-" if dv is None else "fdlibm" if dv == d_fd else \
                "ucrt~" if verify_tolerant(da, db, n, dv) else "neither"
            ms = r.get("ms_per_walk")
            ms = "-" if ms is None else round(ms, 3)
            print(f"{r['label']:24} {ms:>8} {badtag:>8} {fd:>13} {ok:>11} {skip:>11} {dfam:>13}  {r['ua'][:60]}")


if __name__ == "__main__":
    main()
