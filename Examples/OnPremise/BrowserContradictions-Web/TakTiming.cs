using System.Globalization;

namespace BrowserContradictions.Web;

/// <summary>
/// Server side of the m_takwork browser test: timed runs of the Takeuchi
/// function on inputs the server chooses, checked against the work each
/// input is known to take.
/// <para>
/// tak(x, y, z) is pure recursion on small integers, so the number of calls
/// for any inputs is exact and cheap to work out here (<see cref="Calls"/>),
/// while a browser has to make every one of them. The server sends
/// <see cref="Variants"/> input sets whose call counts it picked to span
/// about ten to one. The browser runs each <see cref="Reps"/> times in a
/// dedicated worker, interleaved, and sends the answers and every elapsed
/// time.
/// </para>
/// <para>
/// The assertion: a genuine engine spends a near constant time per call, so
/// time = k x calls. The check takes each input's fastest repeat (other
/// work on the device only ever adds time), sets k to the median of fastest
/// time / calls over the inputs, then asserts every input's fastest time is
/// within <see cref="Tolerance"/> of k x calls, and that k is a speed some
/// real engine has. One input may be slower than that, never faster: a
/// burst of other work can cover all three repeats of one input. Taking k
/// from the run itself is what makes it hold across devices: a phone is
/// slower per call than a desktop but just as proportional.
/// </para>
/// <para>
/// What it catches: an answer whose times were not measured from the work
/// (made up, replayed from other inputs, a memoised or native tak, a faked
/// clock). What it cannot catch: a real browser engine, however automated,
/// does the work and passes. Timers a browser coarsens (Firefox
/// resistFingerprinting) cannot resolve the smaller inputs, and the check
/// answers Unresolved rather than guess.
/// </para>
/// Ackermann is left out on purpose: its call count grows so fast with
/// depth that inputs big enough to time on a desktop (millions of calls)
/// recurse thousands of frames deep, past what phone workers allow.
/// </summary>
public static class TakTiming
{
    /// <summary>Input sets per run. Changing it changes the test's version.</summary>
    public const int Variants = 7;

    /// <summary>Repeats of each input set, interleaved. The gatherer's R.</summary>
    public const int Reps = 3;

    /// <summary>Smallest and largest target call counts. About 3 ms and 30
    /// ms each on a 2026 desktop (about 5 ns a call), several times that on
    /// a low end phone.</summary>
    public const long MinTarget = 600_000, MaxTarget = 6_000_000;

    /// <summary>
    /// Allowed relative gap between an input's fastest time and k x calls,
    /// on top of <see cref="TimerSlack"/> timer ticks. Set from the lab.
    /// </summary>
    public const double Tolerance = 0.35;

    /// <summary>Timer ticks added to every allowance (two readings per time).</summary>
    public const double TimerSlack = 2;

    /// <summary>At least <see cref="MinResolved"/> inputs must be expected to
    /// take this many timer ticks, or the run cannot be judged.</summary>
    public const double MinTicks = 8;

    /// <summary>Inputs that must be well resolved by the timer. Firefox's 1
    /// ms tick resolves the larger inputs of a desktop run.</summary>
    public const int MinResolved = 3;

    /// <summary>A tick coarser than this (Firefox resistFingerprinting rounds
    /// to 16.7 ms) cannot be judged at all.</summary>
    public const double MaxTickMs = 5;

    /// <summary>Deepest recursion an input may need. Deeper inputs cost up
    /// to a quarter more per call in V8, which the check would read as a
    /// wrong time.</summary>
    public const int MaxDepth = 30;

    /// <summary>Plausible nanoseconds per tak call. Optimised JavaScript on a
    /// fast desktop is about 4 ns, an interpreter with no JIT tens of ns.
    /// Outside these the times were not measured from the work.</summary>
    public const double MinNsPerCall = 1.0, MaxNsPerCall = 2000;

    /// <summary>Timer tick assumed when the browser saw no tick within its
    /// bound, so coarse that the run is Unresolved.</summary>
    public const double CoarseTickMs = 16.7;

    /// <summary>The gatherer's default inputs (DEFAULTS.tak) come from this seed.</summary>
    public static readonly (uint A, uint B) DefaultSeed = (0x9E3779B9, 0x7F4A7C15);

    public enum Outcome { Pass, Fail, Unresolved }

    /// <summary>A verdict, with the fitted speed and the worst relative gap,
    /// for the lab and for logging.</summary>
    public sealed record Verdict(Outcome Outcome, string Reason, double NsPerCall, double WorstGap, double TickMs)
    {
        public override string ToString() => FormattableString.Invariant(
            $"{Outcome} {Reason} ns/call={NsPerCall:0.###} worst={WorstGap:0.###} tick={TickMs:0.####}");
    }

    // ---- exact tak ---------------------------------------------------------

    private static readonly Dictionary<(int, int, int), (int R, long C)> Memo = new();
    private static readonly Dictionary<(int, int, int), int> DepthMemo = new();
    private static readonly object MemoLock = new();

    /// <summary>tak(x, y, z) and the number of calls the plain recursion
    /// makes for it, the outer call included, as the gatherer's t() does.</summary>
    public static (int Result, long Calls) Calls(int x, int y, int z)
    {
        lock (MemoLock) return Tak(x, y, z);
    }

    /// <summary>The deepest call stack tak(x, y, z) reaches, the outer call
    /// counted as 1.</summary>
    public static int Depth(int x, int y, int z)
    {
        lock (MemoLock) return D(x, y, z);
    }

    private static int D(int x, int y, int z)
    {
        if (y >= x) return 1;
        if (DepthMemo.TryGetValue((x, y, z), out var m)) return m;
        var a = Tak(x - 1, y, z).R;
        var b = Tak(y - 1, z, x).R;
        var c = Tak(z - 1, x, y).R;
        var d = 1 + Math.Max(Math.Max(D(x - 1, y, z), D(y - 1, z, x)), Math.Max(D(z - 1, x, y), D(a, b, c)));
        DepthMemo[(x, y, z)] = d;
        return d;
    }

    private static (int R, long C) Tak(int x, int y, int z)
    {
        if (y >= x) return (z, 1);
        if (Memo.TryGetValue((x, y, z), out var m)) return m;
        var a = Tak(x - 1, y, z);
        var b = Tak(y - 1, z, x);
        var c = Tak(z - 1, x, y);
        var d = Tak(a.R, b.R, c.R);
        var r = (d.R, 1 + a.C + b.C + c.C + d.C);
        Memo[(x, y, z)] = r;
        return r;
    }

    // Every input set x > y > z >= 0, x up to 40, no deeper than MaxDepth,
    // whose call count is near the range. Many sets share a count (adding one to all three keeps it),
    // which is fine: the server picks among them at random.
    private static readonly Lazy<(int X, int Y, int Z, long C)[]> Pool = new(() =>
    {
        var list = new List<(int, int, int, long)>();
        for (var x = 2; x <= 40; x++)
            for (var y = 1; y < x; y++)
                for (var z = 0; z < y; z++)
                {
                    var c = Calls(x, y, z).Calls;
                    if (c >= MinTarget * 70 / 100 && c <= MaxTarget * 130 / 100 && Depth(x, y, z) <= MaxDepth)
                        list.Add((x, y, z, c));
                }
        return list.ToArray();
    });

    /// <summary>
    /// The input sets for a seed, in the order the browser runs them: one
    /// per target call count, the targets log spaced from
    /// <see cref="MinTarget"/> to <see cref="MaxTarget"/> with up to 15%
    /// jitter, then shuffled so time does not simply grow with position.
    /// </summary>
    public static int[][] ChooseVariants(uint a, uint b)
    {
        var rng = new SplitMix(((ulong)a << 32) | b);
        var pool = Pool.Value;
        var chosen = new List<int[]>();
        var counts = new HashSet<long>();
        for (var k = 0; k < Variants; k++)
        {
            var target = MinTarget * Math.Pow((double)MaxTarget / MinTarget, (double)k / (Variants - 1))
                * (0.85 + 0.3 * rng.NextDouble());
            var near = pool.Where(p => !counts.Contains(p.C) && Math.Abs(p.C - target) <= target * 0.15).ToArray();
            if (near.Length == 0)
                near = pool.Where(p => !counts.Contains(p.C)).OrderBy(p => Math.Abs(p.C - target)).Take(1).ToArray();
            var pick = near[(int)(rng.Next() % (ulong)near.Length)];
            counts.Add(pick.C);
            chosen.Add([pick.X, pick.Y, pick.Z]);
        }
        for (var i = chosen.Count - 1; i > 0; i--)
        {
            var j = (int)(rng.Next() % (ulong)(i + 1));
            (chosen[i], chosen[j]) = (chosen[j], chosen[i]);
        }
        return chosen.ToArray();
    }

    /// <summary>The params.tak JSON for a set of inputs, exactly as the
    /// gatherer's JSON.stringify writes it.</summary>
    public static string Json(int[][] variants) =>
        "[" + string.Join(",", variants.Select(v => "[" + string.Join(",", v) + "]")) + "]";

    /// <summary>The tag the gatherer puts before the value: empty for the
    /// default inputs, else "p" + FNV-1a 32 of the inputs list + ":".</summary>
    public static string Tag(int[][] variants)
    {
        var d = ChooseVariants(DefaultSeed.A, DefaultSeed.B);
        return Json(variants) == Json(d) ? "" : "p" + Fnv1a32("[" + Json(variants) + "]") + ":";
    }

    // ---- the check ---------------------------------------------------------

    /// <summary>
    /// Checks a browser's m_takwork value against the inputs the server
    /// issued for it. Value: tag, "g" + timer tick in ms ("c" when the
    /// browser saw no tick), "|", then per input "result:t1,t2,t3" in ms,
    /// joined by ";".
    /// </summary>
    public static Verdict Verify(int[][] variants, string value)
    {
        static Verdict V(Outcome o, string why, double ns = double.NaN, double worst = double.NaN, double tick = double.NaN) =>
            new(o, why, ns, worst, tick);
        if (value == null) return V(Outcome.Fail, "missing");
        var tag = Tag(variants);
        if (!value.StartsWith(tag, StringComparison.Ordinal) || (tag.Length == 0 && value.StartsWith('p')))
            return V(Outcome.Fail, "inputs");
        var body = value[tag.Length..];
        var bar = body.IndexOf('|');
        if (bar < 2 || body[0] != 'g') return V(Outcome.Fail, "shape");
        double tick;
        if (body[1..bar] == "c") tick = CoarseTickMs;
        else if (!double.TryParse(body[1..bar], NumberStyles.Float, CultureInfo.InvariantCulture, out tick) || tick <= 0)
            return V(Outcome.Fail, "shape");
        if (tick > MaxTickMs) return V(Outcome.Unresolved, "timer", double.NaN, double.NaN, tick);
        var parts = body[(bar + 1)..].Split(';');
        if (parts.Length != variants.Length) return V(Outcome.Fail, "shape");

        var fastest = new double[parts.Length];
        var calls = new double[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var colon = parts[i].IndexOf(':');
            if (colon < 1) return V(Outcome.Fail, "shape");
            var (r, c) = Calls(variants[i][0], variants[i][1], variants[i][2]);
            if (parts[i][..colon] != r.ToString(CultureInfo.InvariantCulture)) return V(Outcome.Fail, "result");
            var times = parts[i][(colon + 1)..].Split(',');
            if (times.Length != Reps) return V(Outcome.Fail, "shape");
            var best = double.MaxValue;
            foreach (var t in times)
            {
                if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var ms) || ms < 0 || double.IsNaN(ms))
                    return V(Outcome.Fail, "shape");
                best = Math.Min(best, ms);
            }
            fastest[i] = best;
            calls[i] = c;
        }

        // k, ms per call, as the median input's: one slowed input cannot move it.
        var rate = fastest.Select((t, i) => t / calls[i]).OrderBy(r => r).ToArray();
        var k = rate[rate.Length / 2];
        var ns = k * 1e6;
        if (ns < MinNsPerCall) return V(Outcome.Fail, "too fast", ns, double.NaN, tick);
        if (ns > MaxNsPerCall) return V(Outcome.Fail, "too slow", ns, double.NaN, tick);
        if (calls.Count(c => k * c >= MinTicks * tick) < MinResolved) return V(Outcome.Unresolved, "timer", ns, double.NaN, tick);

        // Worst gap over the inputs, leaving out the one slow input allowed.
        double worst = 0;
        int fast = 0, slow = 0;
        double slowGap = 0;
        for (var i = 0; i < calls.Length; i++)
        {
            var expected = k * calls[i];
            var allowed = Tolerance * expected + TimerSlack * tick;
            var gap = (fastest[i] - expected) / expected;
            if (fastest[i] < expected - allowed) fast++;
            else if (fastest[i] > expected + allowed) { slow++; slowGap = Math.Max(slowGap, gap); continue; }
            worst = Math.Max(worst, Math.Abs(gap));
        }
        if (fast > 0 || slow > 1) return V(Outcome.Fail, "proportion", ns, Math.Max(worst, slowGap), tick);
        return V(Outcome.Pass, slow == 1 ? "ok, one slow" : "ok", ns, worst, tick);
    }

    private static string Fnv1a32(string text)
    {
        var h = 0x811c9dc5u;
        foreach (var ch in text) { h ^= ch; h *= 0x01000193u; }
        return h.ToString("x8", CultureInfo.InvariantCulture);
    }

    private struct SplitMix(ulong state)
    {
        private ulong _s = state;
        public ulong Next()
        {
            var z = _s += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
        public double NextDouble() => (Next() >> 11) * (1.0 / (1UL << 53));
    }
}
