namespace BrowserContradictions.Web;

/// <summary>
/// Server side of the m_tanhwalk browser test: a chained walk through
/// Math.tanh from a seed the server chooses, so a recorded answer is no use
/// for any other seed.
/// <para>
/// Each step makes a 52 bit input in [-0.05, 0.05) from two 32 bit states,
/// calls tanh once and folds all 64 bits of the answer back into the
/// states. A one ulp difference at any step changes every later input and
/// the result. Everything except tanh is integer arithmetic or exactly
/// rounded IEEE double arithmetic, so it is identical in every engine and
/// here. The JavaScript twin is tanhWalk in gatherer/browser-tests-v3.js.
/// </para>
/// <para>
/// Which tanh a browser has (lab, 23 October 2026, 4000 single inputs and
/// 300 walks per browser):
/// </para>
/// <list type="bullet">
/// <item>Chromium 148 and later calls std::tanh (V8 c1486295), so it is the
/// platform C library's. On Windows that is the UCRT, which is within
/// 0.0012 ulp of correctly rounded for |x| below 0.05: it only misrounds
/// when the exact value is that close to a rounding tie.</item>
/// <item>Chromium 147 and earlier on every platform, and Firefox, use
/// fdlibm, ported exactly below.</item>
/// <item>Linux Chromium 148 and later uses glibc, which is fdlibm's algorithm
/// over glibc's expm1: neither model accepts it.</item>
/// </list>
/// The server cannot run the UCRT (the cloud is a Linux container), so the
/// Windows model is tolerant: correctly rounded at every step, except that
/// a step whose exact value is within <see cref="TieWindow"/> ulps of a tie
/// may round either way. fdlibm misrounds about 30% of these inputs and
/// under 2% of those misroundings are that close to a tie, so 64 steps of
/// fdlibm or glibc pass with odds of about 1 in 10^9.
/// </summary>
public static class TanhWalk
{
    /// <summary>Steps per walk. Changing it changes the test's version.</summary>
    public const int Steps = 64;

    /// <summary>
    /// Ulps either side of a rounding tie within which the Windows model
    /// accepts either rounding. The UCRT's largest seen is 0.00117.
    /// </summary>
    public const double TieWindow = 0.02;

    /// <summary>Most steps the tolerant check walks across its branches.</summary>
    public const int Budget = 4096;

    /// <summary>The gatherer's default seed (DEFAULTS.walk).</summary>
    public static readonly (uint A, uint B) DefaultSeed = (0x9E3779B9, 0x7F4A7C15);

    public enum Family { None, Fdlibm, WindowsCrt, Unknown }

    /// <summary>
    /// The value the gatherer sends for a seed: the tag the gatherer's
    /// tag() function adds for non default inputs, then 16 hex chars.
    /// </summary>
    public static string Tag(uint a, uint b) =>
        (a, b) == DefaultSeed ? "" : "p" + Fnv1a32($"[[{a},{b}]]") + ":";

    /// <summary>The walk under a given tanh.</summary>
    public static string Walk(uint a, uint b, Func<double, double> tanh, int steps = Steps)
    {
        uint s1 = a, s2 = b;
        for (var i = 0; i < steps; i++)
        {
            Mix(ref s1, ref s2);
            Fold(ref s1, ref s2, tanh(Input(s1, s2)));
        }
        return Finish(s1, s2);
    }

    /// <summary>
    /// Which model produced an answer (the 16 hex chars, without the tag)
    /// for the seed. Unknown when the tolerant check ran out of budget.
    /// </summary>
    public static Family Classify(uint a, uint b, string answer, int steps = Steps)
    {
        if (answer == Walk(a, b, Fdlibm.Tanh, steps))
        {
            return Family.Fdlibm;
        }
        return AcceptsWindows(a, b, answer, steps) switch
        {
            true => Family.WindowsCrt,
            false => Family.None,
            null => Family.Unknown
        };
    }

    /// <summary>
    /// True when some walk whose every step is correctly rounded, or rounds
    /// the other way within <see cref="TieWindow"/> of a tie, ends at the
    /// answer. Null when the branches run past <see cref="Budget"/>.
    /// </summary>
    public static bool? AcceptsWindows(uint a, uint b, string answer, int steps = Steps)
    {
        var found = false;
        var spent = 0;
        bool Go(uint s1, uint s2, int i)
        {
            if (i == steps)
            {
                return Finish(s1, s2) == answer;
            }
            if (++spent > Budget)
            {
                throw new OperationCanceledException();
            }
            Mix(ref s1, ref s2);
            var x = Input(s1, s2);
            var c = CorrectlyRounded(x, out var alt);
            var f1 = s1; var f2 = s2;
            Fold(ref f1, ref f2, c);
            if (Go(f1, f2, i + 1))
            {
                return true;
            }
            if (alt.HasValue)
            {
                Fold(ref s1, ref s2, alt.Value);
                return Go(s1, s2, i + 1);
            }
            return false;
        }
        try
        {
            found = Go(a, b, 0);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        return found;
    }

    /// <summary>
    /// Every answer the Windows model accepts for a seed, for example to
    /// look the default seed's answers up in the baseline. Null when there
    /// are more than <paramref name="max"/> or the budget runs out.
    /// </summary>
    public static IReadOnlyList<string> WindowsAnswers(uint a, uint b, int max = 64, int steps = Steps)
    {
        var answers = new List<string>();
        var spent = 0;
        void Go(uint s1, uint s2, int i)
        {
            if (i == steps)
            {
                answers.Add(Finish(s1, s2));
                if (answers.Count > max) throw new OperationCanceledException();
                return;
            }
            if (++spent > Budget) throw new OperationCanceledException();
            Mix(ref s1, ref s2);
            var c = CorrectlyRounded(Input(s1, s2), out var alt);
            var f1 = s1; var f2 = s2;
            Fold(ref f1, ref f2, c);
            Go(f1, f2, i + 1);
            if (alt.HasValue)
            {
                Fold(ref s1, ref s2, alt.Value);
                Go(s1, s2, i + 1);
            }
        }
        try
        {
            Go(a, b, 0);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        return answers;
    }

    private static void Mix(ref uint s1, ref uint s2)
    {
        unchecked
        {
            s1 = (s1 ^ (s1 >> 16)) * 0x85EBCA6B;
            s1 ^= s1 >> 13;
            s2 = (s2 ^ (s2 >> 16)) * 0xC2B2AE35;
            s2 ^= s2 >> 13;
        }
    }

    // ((s1 * 2^20 + (s2 >>> 12)) / 2^52 - 0.5) * 0.1, every step but the
    // last exact, exactly as the JavaScript computes it.
    private static double Input(uint s1, uint s2) =>
        (((double)s1 * 1048576.0 + (s2 >> 12)) / 4503599627370496.0 - 0.5) * 0.1;

    private static void Fold(ref uint s1, ref uint s2, double t)
    {
        unchecked
        {
            var u = (ulong)BitConverter.DoubleToInt64Bits(t);
            var hi = (uint)(u >> 32);
            var lo = (uint)u;
            s1 ^= lo;
            s2 = s2 ^ hi ^ (lo * 0x27D4EB2F);
        }
    }

    private static string Finish(uint s1, uint s2)
    {
        unchecked
        {
            s1 = (s1 ^ (s1 >> 16)) * 0x85EBCA6B;
            s2 = (s2 ^ (s2 >> 16)) * 0xC2B2AE35;
        }
        return s1.ToString("x8") + s2.ToString("x8");
    }

    private static string Fnv1a32(string text)
    {
        var h = 0x811c9dc5u;
        foreach (var ch in text)
        {
            h ^= ch;
            h = unchecked(h * 0x01000193u);
        }
        return h.ToString("x8");
    }

    // ---- correctly rounded tanh for |x| < 0.05, in double-double ----------

    // c_k of tanh(x) = sum c_k x^(2k+1) as (hi, lo) bit patterns. 15 terms
    // reach 1e-42 relative for |x| < 0.05.
    private static readonly (double Hi, double Lo)[] Series = new (ulong, ulong)[]
    {
        (0x3FF0000000000000, 0x0000000000000000),
        (0xBFD5555555555555, 0xBC75555555555555),
        (0x3FC1111111111111, 0x3C41111111111111),
        (0xBFABA1BA1BA1BA1C, 0x3C47917917917918),
        (0x3F9664F4882C10FA, 0xBC09A5288B6C44FD),
        (0xBF8226E355E6C23D, 0x3C2C292B8F1A2C13),
        (0x3F6D6D3D0E157DE0, 0xBC0280CFC968D971),
        (0xBF57DA36452B75E3, 0x3BF25789B285D2ED),
        (0x3F43558248036744, 0x3BE488D9563F1F23),
        (0xBF2F57D7734D1664, 0xBBB0D55A913CCB50),
        (0x3F1967E18AFCAFAD, 0xBBBD7614A42D44E6),
        (0xBF0497D8EEA25259, 0xBB99F2D02E4D2863),
        (0x3EF0B132D39A6050, 0x3B93B274C2C19614),
        (0xBEDB0F72D3EE24E9, 0xBB61688DDD595609),
        (0x3EC5EF2DA474E5B7, 0xBB5C261D7EBA23CF),
    }.Select(p => (BitConverter.Int64BitsToDouble((long)p.Item1), BitConverter.Int64BitsToDouble((long)p.Item2))).ToArray();

    /// <summary>
    /// The correctly rounded tanh(x) for |x| below 0.05, and in
    /// <paramref name="alt"/> its neighbour when the exact value is within
    /// <see cref="TieWindow"/> ulps of the tie between them. The double-double
    /// sum is good to about 2^-100 relative, far finer than the window.
    /// </summary>
    public static double CorrectlyRounded(double x, out double? alt)
    {
        if (!(Math.Abs(x) < 0.05))
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }
        alt = null;
        if (x == 0)
        {
            return x;
        }
        var (zh, zl) = TwoProd(x, x);
        var (sh, sl) = Series[^1];
        for (var k = Series.Length - 2; k >= 0; k--)
        {
            (sh, sl) = Mul(sh, sl, zh, zl);
            (sh, sl) = Add(sh, sl, Series[k].Hi, Series[k].Lo);
        }
        var (ph, pe) = TwoProd(sh, x);
        pe += sl * x;
        var (hi, lo) = FastTwoSum(ph, pe);
        if (lo == 0)
        {
            return hi;
        }
        var n = lo > 0 ? Math.BitIncrement(hi) : Math.BitDecrement(hi);
        var ulp = Math.Abs(n - hi);
        if (Math.Abs(Math.Abs(lo) - ulp / 2) / ulp < TieWindow)
        {
            alt = n;
        }
        return hi;
    }

    private static (double, double) TwoProd(double a, double b)
    {
        var p = a * b;
        return (p, Math.FusedMultiplyAdd(a, b, -p));
    }

    private static (double, double) FastTwoSum(double a, double b)
    {
        var s = a + b;
        return (s, b - (s - a));
    }

    private static (double, double) TwoSum(double a, double b)
    {
        var s = a + b;
        var bb = s - a;
        return (s, (a - (s - bb)) + (b - bb));
    }

    private static (double, double) Add(double ah, double al, double bh, double bl)
    {
        var (s, e) = TwoSum(ah, bh);
        return FastTwoSum(s, e + al + bl);
    }

    private static (double, double) Mul(double ah, double al, double bh, double bl)
    {
        var (p, e) = TwoProd(ah, bh);
        return FastTwoSum(p, e + ah * bl + al * bh);
    }

    // ---- fdlibm ---------------------------------------------------------

    /// <summary>
    /// fdlibm 5.3 tanh and expm1, as in V8 before Chromium 148 and in
    /// Firefox (modules/fdlibm). Pure IEEE double arithmetic, so the same
    /// bits on every host.
    /// </summary>
    public static class Fdlibm
    {
        private const double Huge = 1.0e+300, Tiny = 1.0e-300;
        private const double OThreshold = 7.09782712893383973096e+02;
        private const double Ln2Hi = 6.93147180369123816490e-01, Ln2Lo = 1.90821492927058770002e-10;
        private const double InvLn2 = 1.44269504088896338700e+00;
        private const double Q1 = -3.33333333333331316428e-02, Q2 = 1.58730158725481460165e-03,
            Q3 = -7.93650757867487942473e-05, Q4 = 4.00821782732936239552e-06, Q5 = -2.01099218183624371326e-07;

        private static int High(double x) => (int)(BitConverter.DoubleToInt64Bits(x) >> 32);
        private static uint Low(double x) => (uint)BitConverter.DoubleToInt64Bits(x);
        private static double Make(int hi, uint lo) => BitConverter.Int64BitsToDouble(((long)hi << 32) | lo);

        public static double Tanh(double x)
        {
            var jx = High(x);
            var ix = jx & 0x7FFFFFFF;
            double z;
            if (ix >= 0x7FF00000)
            {
                return jx >= 0 ? 1.0 / x + 1.0 : 1.0 / x - 1.0;
            }
            if (ix < 0x40360000)
            {
                if (ix < 0x3E300000 && Huge + x > 1.0)
                {
                    return x;
                }
                if (ix >= 0x3FF00000)
                {
                    var t = Expm1(2.0 * Math.Abs(x));
                    z = 1.0 - 2.0 / (t + 2.0);
                }
                else
                {
                    var t = Expm1(-2.0 * Math.Abs(x));
                    z = -t / (t + 2.0);
                }
            }
            else
            {
                z = 1.0 - Tiny;
            }
            return jx >= 0 ? z : -z;
        }

        public static double Expm1(double x)
        {
            int k;
            double hi, lo, c = 0, t;
            var hx = High(x);
            var xsb = hx & unchecked((int)0x80000000);
            hx &= 0x7FFFFFFF;
            if (hx >= 0x4043687A)
            {
                if (hx >= 0x40862E42)
                {
                    if (hx >= 0x7FF00000)
                    {
                        if (((hx & 0xFFFFF) | (int)Low(x)) != 0) return x + x;
                        return xsb == 0 ? x : -1.0;
                    }
                    if (x > OThreshold) return double.PositiveInfinity;
                }
                if (xsb != 0) return Tiny - 1.0;
            }
            if (hx > 0x3FD62E42)
            {
                if (hx < 0x3FF0A2B2)
                {
                    if (xsb == 0) { hi = x - Ln2Hi; lo = Ln2Lo; k = 1; }
                    else { hi = x + Ln2Hi; lo = -Ln2Lo; k = -1; }
                }
                else
                {
                    k = (int)(InvLn2 * x + (xsb == 0 ? 0.5 : -0.5));
                    t = k;
                    hi = x - t * Ln2Hi;
                    lo = t * Ln2Lo;
                }
                x = hi - lo;
                c = (hi - x) - lo;
            }
            else if (hx < 0x3C900000)
            {
                t = Huge + x;
                return x - (t - (Huge + x));
            }
            else
            {
                k = 0;
            }
            var hfx = 0.5 * x;
            var hxs = x * hfx;
            var r1 = 1.0 + hxs * (Q1 + hxs * (Q2 + hxs * (Q3 + hxs * (Q4 + hxs * Q5))));
            t = 3.0 - r1 * hfx;
            var e = hxs * ((r1 - t) / (6.0 - x * t));
            if (k == 0) return x - (x * e - hxs);
            var twopk = Make(0x3FF00000 + (k << 20), 0);
            e = x * (e - c) - c;
            e -= hxs;
            if (k == -1) return 0.5 * (x - e) - 0.5;
            if (k == 1)
            {
                if (x < -0.25) return -2.0 * (e - (x + 0.5));
                return 1.0 + 2.0 * (x - e);
            }
            double y;
            if (k <= -2 || k > 56)
            {
                y = 1.0 - (e - x);
                y = k == 1024 ? y * 2.0 * 8.98846567431157953865e+307 : y * twopk;
                return y - 1.0;
            }
            if (k < 20)
            {
                t = Make(0x3FF00000 - (0x200000 >> k), 0);
                y = t - (e - x);
                y *= twopk;
            }
            else
            {
                t = Make((0x3FF - k) << 20, 0);
                y = x - (e + t);
                y += 1.0;
                y *= twopk;
            }
            return y;
        }
    }
}
