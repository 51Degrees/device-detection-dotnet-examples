"""Large scale false positive check for the Windows family. The UCRT walk
(this host's math.tanh, proven bit identical to Windows Chrome and Edge) must
pass the tie tolerant verifier for every seed.

  py -I scale.py <seeds> <steps>
"""
import math
import os
import random
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import walk  # noqa: E402

count, n = int(sys.argv[1]), int(sys.argv[2])
r = random.Random(20261023)
t0 = time.time()
fails = skips = differs_from_pure_cr = 0
for i in range(count):
    a, b = r.getrandbits(32), r.getrandbits(32)
    got = walk.walk(a, b, n, math.tanh)
    pure = walk.walk(a, b, n, lambda x: walk.cr_choices(x)[0])
    differs_from_pure_cr += got != pure
    v = walk.verify_tolerant(a, b, n, got)
    fails += v is False
    skips += v is None
print(f"{count} seeds x {n} steps: fails {fails}, skips {skips}, "
      f"walks that needed the tie tolerance {differs_from_pure_cr}, {time.time() - t0:.0f}s")
