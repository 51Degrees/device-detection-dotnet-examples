"""tanh(x) = sum c_k x^(2k+1), c_k = 2^(2k+2) (2^(2k+2) - 1) B_(2k+2) / (2k+2)!.
Prints each c_k as a double-double (hi, lo) in C# hex bit literals."""
import struct
from fractions import Fraction
from math import factorial

import mpmath


def bits(x):
    return struct.unpack(">Q", struct.pack(">d", x))[0]


for k in range(15):
    n = 2 * k + 2
    b = Fraction(*[int(v) for v in mpmath.bernfrac(n)])
    c = Fraction(2 ** n * (2 ** n - 1)) * b / factorial(n)
    hi = float(c)
    lo = float(c - Fraction(hi))
    print(f"        (0x{bits(hi):016X}, 0x{bits(lo):016X}), // {float(c):.17g}")
