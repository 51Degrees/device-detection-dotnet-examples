"""How close to a rounding tie are the misroundings of the host tanh and of
fdlibm. dist = distance of the exact value from the midpoint between the
correctly rounded double and its neighbour, in ulps (0 = on the tie,
0.5 = exactly representable).

  py -I ties.py <lo> <hi> <count>
"""
import math
import os
import random
import sys

import mpmath

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fdlibm  # noqa: E402

mpmath.mp.prec = 200


def info(x):
    e = mpmath.tanh(mpmath.mpf(x))
    c = float(e)
    n = math.nextafter(c, math.inf) if e > c else math.nextafter(c, -math.inf)
    ulp = abs(n - c)
    mid = (mpmath.mpf(c) + mpmath.mpf(n)) / 2
    return c, n, float(abs(e - mid) / ulp)


lo, hi, count = float(sys.argv[1]), float(sys.argv[2]), int(sys.argv[3])
r = random.Random(11)
host_bad, fd_bad, fd_far = [], [], 0
for _ in range(count):
    x = r.uniform(lo, hi) * (1 if r.random() < 0.5 else -1)
    c, n, d = info(x)
    h = math.tanh(x)
    f = fdlibm.tanh(x)
    if h != c:
        host_bad.append((d, h == n, x))
    if f != c:
        fd_bad.append(d)
        if f != n:
            fd_far += 1
host_bad.sort()
print(f"{sys.platform} band [{lo},{hi}) n={count}")
print(f"  host misrounded {len(host_bad)}; all to the neighbour: {all(b[1] for b in host_bad)}; "
      f"max dist from tie {max((b[0] for b in host_bad), default=0):.5f} ulp")
fd_bad.sort()
q = lambda p: fd_bad[min(len(fd_bad) - 1, int(p * len(fd_bad)))] if fd_bad else 0
print(f"  fdlibm misrounded {len(fd_bad)} ({len(fd_bad) / count:.1%}), {fd_far} more than one ulp off; "
      f"dist quantiles 5% {q(0.05):.4f} 50% {q(0.5):.4f} 95% {q(0.95):.4f}")
for thr in (0.001, 0.005, 0.01, 0.02, 0.05):
    within = sum(1 for d in fd_bad if d < thr)
    print(f"  tie window {thr}: fdlibm misroundings it would forgive {within / max(1, len(fd_bad)):.2%}, "
          f"host misroundings outside it {sum(1 for b in host_bad if b[0] >= thr)}")
