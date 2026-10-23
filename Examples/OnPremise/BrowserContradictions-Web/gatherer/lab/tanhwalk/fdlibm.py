"""Bit exact Python port of fdlibm tanh and expm1 (as in Firefox
modules/fdlibm and V8 src/base/ieee754.cc). Pure IEEE double arithmetic,
no platform libm, so any host computes the same bits."""
import struct


def _hi(x):
    return struct.unpack(">q", struct.pack(">d", x))[0] >> 32 & 0xFFFFFFFF


def _lo(x):
    return struct.unpack(">q", struct.pack(">d", x))[0] & 0xFFFFFFFF


def _s32(u):
    return u - (1 << 32) if u & 0x80000000 else u


def _insert(hi, lo):
    return struct.unpack(">d", struct.pack(">Q", ((hi & 0xFFFFFFFF) << 32) | (lo & 0xFFFFFFFF)))[0]


def _set_high(x, hi):
    return _insert(hi, _lo(x))


o_threshold = 7.09782712893383973096e+02
ln2_hi = 6.93147180369123816490e-01
ln2_lo = 1.90821492927058770002e-10
invln2 = 1.44269504088896338700e+00
Q1 = -3.33333333333331316428e-02
Q2 = 1.58730158725481460165e-03
Q3 = -7.93650757867487942473e-05
Q4 = 4.00821782732936239552e-06
Q5 = -2.01099218183624371326e-07
huge = 1.0e+300
tiny = 1.0e-300


def expm1(x):
    hx = _s32(_hi(x))
    xsb = hx & 0x80000000
    hx &= 0x7FFFFFFF
    if hx >= 0x4043687A:
        if hx >= 0x40862E42:
            if hx >= 0x7FF00000:
                if ((hx & 0xFFFFF) | _lo(x)) != 0:
                    return x + x
                return x if xsb == 0 else -1.0
            if x > o_threshold:
                return float("inf")
        if xsb != 0:
            return tiny - 1.0
    if hx > 0x3FD62E42:
        if hx < 0x3FF0A2B2:
            if xsb == 0:
                hi = x - ln2_hi
                lo = ln2_lo
                k = 1
            else:
                hi = x + ln2_hi
                lo = -ln2_lo
                k = -1
        else:
            k = int(invln2 * x + (0.5 if xsb == 0 else -0.5))
            t = float(k)
            hi = x - t * ln2_hi
            lo = t * ln2_lo
        x = hi - lo
        c = (hi - x) - lo
    elif hx < 0x3C900000:
        t = huge + x
        return x - (t - (huge + x))
    else:
        k = 0
        c = 0.0
    hfx = 0.5 * x
    hxs = x * hfx
    r1 = 1.0 + hxs * (Q1 + hxs * (Q2 + hxs * (Q3 + hxs * (Q4 + hxs * Q5))))
    t = 3.0 - r1 * hfx
    e = hxs * ((r1 - t) / (6.0 - x * t))
    if k == 0:
        return x - (x * e - hxs)
    twopk = _insert(0x3FF00000 + (k << 20), 0)
    e = (x * (e - c) - c)
    e -= hxs
    if k == -1:
        return 0.5 * (x - e) - 0.5
    if k == 1:
        if x < -0.25:
            return -2.0 * (e - (x + 0.5))
        return 1.0 + 2.0 * (x - e)
    if k <= -2 or k > 56:
        y = 1.0 - (e - x)
        if k == 1024:
            y = y * 2.0 * 8.98846567431157953865e+307
        else:
            y = y * twopk
        return y - 1.0
    if k < 20:
        t = _insert(0x3FF00000 - (0x200000 >> k), 0)
        y = t - (e - x)
        y = y * twopk
    else:
        t = _insert((0x3FF - k) << 20, 0)
        y = x - (e + t)
        y += 1.0
        y = y * twopk
    return y


def tanh(x):
    one, two = 1.0, 2.0
    jx = _s32(_hi(x))
    ix = jx & 0x7FFFFFFF
    if ix >= 0x7FF00000:
        if jx >= 0:
            return one / x + one
        return one / x - one
    if ix < 0x40360000:
        if ix < 0x3E300000:
            if huge + x > one:
                return x
        if ix >= 0x3FF00000:
            t = expm1(two * abs(x))
            z = one - two / (t + two)
        else:
            t = expm1(-two * abs(x))
            z = -t / (t + two)
    else:
        z = one - tiny
    return z if jx >= 0 else -z
