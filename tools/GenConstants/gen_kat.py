#!/usr/bin/env python3
"""Generates testdata/functions/kat.txt: known answers for every function, both precisions.

Each line is `name f64|f32 a_bits b_bits result_bits` (hex). Inputs come from a seeded generator plus fixed edge
points; each expected result is the correctly rounded (nearest, ties to even) value of the exact function, computed
with mpmath at 700 bits and rounded by the integer code below. mpmath is used purely as an independent black box.

    python3 gen_kat.py > ../../testdata/functions/kat.txt
"""
import math
import random
import struct
import sys

from mpmath import mp, mpf

mp.prec = 700


def rnd(v, p, qmin, emax_biased_inf):
    """Round mpf v to p significant bits with minimum ulp exponent qmin; returns (sign, mant, q) or None for overflow."""
    if v == 0:
        return (0, 0, 0)
    sign = 1 if v < 0 else 0
    man, exp = abs(v).man, abs(v).exp  # value = man * 2^exp
    man = int(man)
    bl = man.bit_length()
    q = max(exp + bl - p, qmin)
    shift = q - exp
    if shift <= 0:
        mant = man << (-shift)
    else:
        mant = man >> shift
        rem = man - (mant << shift)
        half = 1 << (shift - 1)
        if rem > half or (rem == half and (mant & 1)):
            mant += 1
    if mant.bit_length() > p:
        mant >>= 1
        q += 1
    return (sign, mant, q)


def to_bits64(v):
    s, m, q = rnd(v, 53, -1074, None)
    if m == 0:
        return s << 63
    if m < (1 << 52):
        return (s << 63) | m
    be = q + 1075
    if be >= 2047:
        return (s << 63) | 0x7FF0000000000000
    return (s << 63) | (be << 52) | (m - (1 << 52))


def to_bits32(v):
    s, m, q = rnd(v, 24, -149, None)
    if m == 0:
        return s << 31
    if m < (1 << 23):
        return (s << 31) | m
    be = q + 150
    if be >= 255:
        return (s << 31) | 0x7F800000
    return (s << 31) | (be << 23) | (m - (1 << 23))


def d2b(d):
    return struct.unpack('<Q', struct.pack('<d', d))[0]


def f2b(f):
    return struct.unpack('<I', struct.pack('<f', f))[0]


def tof32(d):
    try:
        return struct.unpack('<f', struct.pack('<f', d))[0]
    except OverflowError:
        return math.inf


def fns():
    two = mpf(2)
    return {
        'sin': (1, mp.sin), 'cos': (1, mp.cos), 'tan': (1, mp.tan),
        'asin': (1, mp.asin), 'acos': (1, mp.acos), 'atan': (1, mp.atan),
        'atan2': (2, lambda y, x: mp.atan2(y, x)),
        'exp': (1, mp.exp), 'exp2': (1, lambda x: two ** x), 'expm1': (1, mp.expm1),
        'log': (1, mp.log), 'log2': (1, lambda x: mp.log(x) / mp.log(2)),
        'log10': (1, lambda x: mp.log(x) / mp.log(10)), 'log1p': (1, mp.log1p),
        'cbrt': (1, lambda x: -mp.cbrt(-x) if x < 0 else mp.cbrt(x)), 'hypot': (2, mp.hypot), 'pow': (2, lambda x, y: x ** y),
        'sinh': (1, mp.sinh), 'cosh': (1, mp.cosh), 'tanh': (1, mp.tanh),
    }


def logu(r, lo, hi):
    return 2.0 ** r.uniform(lo, hi)


def sgn(r):
    return -1.0 if r.random() < 0.5 else 1.0


def gen(name, r):
    """One input pair for `name` (b is 0 for unary functions)."""
    if name in ('sin', 'cos', 'tan'):
        c = r.random()
        if c < 0.4:
            return r.uniform(-12, 12), 0.0
        if c < 0.8:
            return sgn(r) * logu(r, -30, 30), 0.0
        return sgn(r) * logu(r, 30, 1000), 0.0
    if name in ('asin', 'acos'):
        c = r.random()
        if c < 0.5:
            return r.uniform(-1, 1), 0.0
        if c < 0.8:
            return sgn(r) * (1 - logu(r, -40, -1)), 0.0
        return sgn(r) * logu(r, -40, -1), 0.0
    if name == 'atan':
        return sgn(r) * logu(r, -40, 60), 0.0
    if name == 'atan2':
        return sgn(r) * logu(r, -30, 30), sgn(r) * logu(r, -30, 30)
    if name == 'exp':
        return (r.uniform(-745, 709) if r.random() < 0.5 else r.uniform(-3, 3)), 0.0
    if name == 'exp2':
        return (r.uniform(-1074, 1023) if r.random() < 0.5 else r.uniform(-4, 4)), 0.0
    if name == 'expm1':
        c = r.random()
        return (r.uniform(-37, 709) if c < 0.4 else (sgn(r) * logu(r, -50, 0) if c < 0.8 else r.uniform(-1, 1))), 0.0
    if name in ('log', 'log2', 'log10'):
        c = r.random()
        if c < 0.6:
            return logu(r, -1074, 1023), 0.0
        return 1.0 + r.uniform(-0.3, 0.4), 0.0
    if name == 'log1p':
        c = r.random()
        return (sgn(r) * logu(r, -50, -1) if c < 0.5 else (logu(r, -1, 60) if c < 0.8 else -1.0 + logu(r, -50, 0))), 0.0
    if name == 'cbrt':
        return sgn(r) * logu(r, -1074, 1023), 0.0
    if name == 'hypot':
        return sgn(r) * logu(r, -500, 500), sgn(r) * logu(r, -500, 500)
    if name == 'pow':
        c = r.random()
        if c < 0.3:
            return logu(r, -20, 20), float(r.randint(-40, 40))
        return logu(r, -30, 30), r.uniform(-30, 30)
    if name == 'sinh':
        return (r.uniform(-30, 30) if r.random() < 0.6 else sgn(r) * logu(r, -40, 9.47)), 0.0
    if name == 'cosh':
        return (r.uniform(-30, 30) if r.random() < 0.6 else sgn(r) * logu(r, -40, 9.47)), 0.0
    if name == 'tanh':
        return (r.uniform(-12, 12) if r.random() < 0.6 else sgn(r) * logu(r, -40, 3)), 0.0
    raise KeyError(name)


EDGES = {
    'sin': [1e22, 6381956970095103 * 2.0 ** 797, 2.0 ** 1000, 3.141592653589793, 1.5707963267948966, 1e-300, 5e-324, 0.7853981633974483],
    'cos': [1e22, 6381956970095103 * 2.0 ** 797, 2.0 ** 1000, 3.141592653589793, 1.5707963267948966, 1e-300, 0.7853981633974483],
    'tan': [1e22, 6381956970095103 * 2.0 ** 797, 1.5707963267948966, 3.141592653589793, 1e-300, 0.7853981633974483],
    'asin': [1.0, -1.0, 0.5, 0.9999999999999999, 1e-300], 'acos': [1.0, -1.0, 0.5, 0.0, 0.9999999999999999, -0.9999999999999999],
    'atan': [1.0, -1.0, 1e300, 1e-300, 5e-324],
    'atan2': [(1.0, 1.0), (1.0, -1.0), (-1.0, -1.0), (1e300, 1e-300), (1e-300, 1e300), (5e-324, 1.0), (1.0, 5e-324)],
    'exp': [1.0, 0.0, 709.78, -745.13, -745.0, 1e-300, 709.782712893384, -708.3964185322641],
    'exp2': [1.0, 3.0, -1074.0, -1075.0, 1023.9999, 0.5, -1022.0],
    'expm1': [1.0, 1e-10, -1e-10, 709.0, -36.0, 0.5],
    'log': [2.0, 10.0, 0.5, 5e-324, 1.7976931348623157e308, 1.0000000000000002, 0.9999999999999999],
    'log2': [2.0, 8.0, 1024.0, 0.125, 3.0, 5e-324], 'log10': [10.0, 1000.0, 1e-5, 2.0, 5e-324],
    'log1p': [1e-10, -0.5, 1.0, 1e300, -0.9999999999999999], 'cbrt': [8.0, 27.0, -64.0, 2.0, 5e-324, 1e300],
    'hypot': [(3.0, 4.0), (1e300, 1e300), (5e-324, 5e-324), (1.0, 1e-20)],
    'pow': [(2.0, 10.0), (10.0, -3.0), (2.0, 0.5), (0.5, 1074.0), (3.0, 3.0), (1.0000000000000002, 4503599627370496.0)],
    'sinh': [1.0, -1.0, 1e-10, 710.0, 30.0], 'cosh': [1.0, 0.0, 710.0, 30.0], 'tanh': [1.0, -1.0, 1e-10, 21.0],
}


def main():
    f = fns()
    out = []
    for name in sorted(f):
        arity, fn = f[name]
        r = random.Random(0x6F6E656D + sum(map(ord, name)))
        pairs64 = []
        for e in EDGES.get(name, []):
            pairs64.append(e if arity == 2 else (e, 0.0))
        while len(pairs64) < 90:
            pairs64.append(gen(name, r))
        for a, b in pairs64:
            if arity == 1:
                v = fn(mpf(a))
            else:
                v = fn(mpf(a), mpf(b))
            out.append('%s f64 %016x %016x %016x' % (name, d2b(a), d2b(b), to_bits64(v)))
        pairs32 = []
        while len(pairs32) < 70:
            a, b = gen(name, r)
            a32, b32 = tof32(a), tof32(b)
            if math.isinf(a32) or math.isinf(b32) or a32 == 0 and a != 0:
                continue
            if name == 'asin' or name == 'acos':
                if abs(a32) > 1:
                    continue
            if name in ('log', 'log2', 'log10') and a32 <= 0:
                continue
            if name == 'log1p' and a32 <= -1:
                continue
            if name == 'pow' and a32 <= 0:
                continue
            pairs32.append((a32, b32))
        for a, b in pairs32:
            v = fn(mpf(a)) if arity == 1 else fn(mpf(a), mpf(b))
            out.append('%s f32 %08x %08x %08x' % (name, f2b(a), f2b(b), to_bits32(v)))
    sys.stdout.write('\n'.join(out) + '\n')


main()
