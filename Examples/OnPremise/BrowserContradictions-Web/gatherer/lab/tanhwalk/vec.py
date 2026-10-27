"""Test vectors for the C# verifier: x, correctly rounded tanh, the tie
neighbour or '-', and fdlibm tanh, as hex bits. Half the inputs are uniform
in (-0.05, 0.05), half are steered close to ties.

  py -I vec.py <count> > vectors.txt
"""
import math
import os
import random
import struct
import sys

import mpmath

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fdlibm  # noqa: E402
import walk  # noqa: E402

mpmath.mp.prec = 200


def b(x):
    return f"{struct.unpack('>Q', struct.pack('>d', x))[0]:016x}"


r = random.Random(5)
count = int(sys.argv[1])
ties = 0
for i in range(count):
    x = r.uniform(-0.05, 0.05)
    if i % 2:
        # walk x until tanh(x) lands within 0.05 ulp of a tie
        for _ in range(200):
            ch = walk.cr_choices(x)
            e = mpmath.tanh(mpmath.mpf(x))
            c = float(e)
            n = math.nextafter(c, math.inf) if e > c else math.nextafter(c, -math.inf)
            d = float(abs(e - (mpmath.mpf(c) + mpmath.mpf(n)) / 2) / abs(n - c))
            if d < 0.05:
                break
            x = r.uniform(-0.05, 0.05)
    ch = walk.cr_choices(x)
    ties += len(ch) > 1
    print(b(x), b(ch[0]), b(ch[1]) if len(ch) > 1 else "-", b(fdlibm.tanh(x)))
print(f"{count} vectors, {ties} inside the tie window", file=sys.stderr)
