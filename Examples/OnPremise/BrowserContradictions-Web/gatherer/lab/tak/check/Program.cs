// Lab tool for TakTiming.cs.
//   dotnet run -- default                 the gatherer's DEFAULTS.tak
//   dotnet run -- seeds <n> <out.json>    n seeded input lists for the lab page
//   dotnet run -- verify <runs dir>...    checks every captured m_takwork.1
//   dotnet run -- forge <runs dir>...     bad actor answers built from them
using System.Globalization;
using System.Text.Json;
using BrowserContradictions.Web;

const string Id = "m_takwork.1";
var cmd = args[0];
if (cmd == "default")
{
    var d = TakTiming.ChooseVariants(TakTiming.DefaultSeed.A, TakTiming.DefaultSeed.B);
    Console.WriteLine(TakTiming.Json(d));
    foreach (var v in d) Console.WriteLine($"  {v[0]},{v[1]},{v[2]} calls {TakTiming.Calls(v[0], v[1], v[2]).Calls}");
    return 0;
}
if (cmd == "seeds")
{
    var n = int.Parse(args[1]);
    var rng = new Random(51);
    var list = new List<int[][]>();
    for (var i = 0; i < n; i++) list.Add(TakTiming.ChooseVariants((uint)rng.NextInt64(1L << 32), (uint)rng.NextInt64(1L << 32)));
    File.WriteAllText(args[2], JsonSerializer.Serialize(list));
    Console.WriteLine($"{n} lists, calls per list {list.Average(l => l.Sum(v => TakTiming.Calls(v[0], v[1], v[2]).Calls)):0}");
    return 0;
}

// Every run file under the folders that has the test, with the inputs it used.
IEnumerable<(string Label, int[][] Vars, string Value)> Runs()
{
    foreach (var dir in args.Skip(1))
        foreach (var f in Directory.EnumerateFiles(dir, "*.json"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(f));
            var r = doc.RootElement;
            if (!r.TryGetProperty("results", out var res) || !res.TryGetProperty(Id, out var val)) continue;
            int[][] vars;
            if (r.TryGetProperty("params", out var p) && p.TryGetProperty("tak", out var t))
                vars = t.EnumerateArray().Select(v => v.EnumerateArray().Select(x => x.GetInt32()).ToArray()).ToArray();
            else vars = TakTiming.ChooseVariants(TakTiming.DefaultSeed.A, TakTiming.DefaultSeed.B);
            yield return (r.GetProperty("label").GetString(), vars, val.GetString());
        }
}

static string Q(IEnumerable<double> xs)
{
    var a = xs.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToArray();
    if (a.Length == 0) return "-";
    double At(double q) => a[(int)Math.Min(a.Length - 1, Math.Floor(q * a.Length))];
    return string.Create(CultureInfo.InvariantCulture, $"min {a[0]:0.###} p50 {At(0.5):0.###} p95 {At(0.95):0.###} max {a[^1]:0.###}");
}

void Report(IEnumerable<(string Label, TakTiming.Verdict V, string Value)> rows)
{
    foreach (var g in rows.GroupBy(r => r.Label).OrderBy(g => g.Key))
    {
        var vs = g.Select(r => r.V).ToList();
        Console.WriteLine($"{g.Key,-28} n {vs.Count,4}  pass {vs.Count(v => v.Outcome == TakTiming.Outcome.Pass),4}  fail {vs.Count(v => v.Outcome == TakTiming.Outcome.Fail),4}  unresolved {vs.Count(v => v.Outcome == TakTiming.Outcome.Unresolved),4}");
        Console.WriteLine($"    ns/call {Q(vs.Select(v => v.NsPerCall))}");
        Console.WriteLine($"    worst gap {Q(vs.Select(v => v.WorstGap))}   tick ms {Q(vs.Select(v => v.TickMs))}");
        foreach (var r in g.Where(r => r.V.Outcome != TakTiming.Outcome.Pass).Take(3))
            Console.WriteLine($"    {r.V}  {r.Value}");
    }
}

if (cmd == "verify")
{
    Report(Runs().Select(r => (r.Label, TakTiming.Verify(r.Vars, r.Value), r.Value)));
    return 0;
}

if (cmd == "forge")
{
    // Each forger knows the inputs and the right answers (tak results are
    // cheap to compute) and has a genuine run of some other inputs to copy
    // times from. What it does not have is the work: its times come from a
    // model of the cost, not from doing it.
    var genuine = Runs().Where(r => TakTiming.Verify(r.Vars, r.Value).Outcome == TakTiming.Outcome.Pass).ToList();
    var rng = new Random(7);
    var rows = new List<(string, TakTiming.Verdict, string)>();
    string Build(int[][] vars, Func<int, int[], double> time, string tick = "0.1")
    {
        var parts = vars.Select((v, i) =>
        {
            var r = TakTiming.Calls(v[0], v[1], v[2]).Result;
            var ts = Enumerable.Range(0, TakTiming.Reps).Select(_ => Math.Round(time(i, v) * (1 + 0.03 * rng.NextDouble()), 1).ToString(CultureInfo.InvariantCulture));
            return r + ":" + string.Join(",", ts);
        });
        return TakTiming.Tag(vars) + "g" + tick + "|" + string.Join(";", parts);
    }
    foreach (var g in genuine)
    {
        var other = genuine[rng.Next(genuine.Count)];
        var vars = TakTiming.ChooseVariants((uint)rng.NextInt64(1L << 32), (uint)rng.NextInt64(1L << 32));
        // 1. Instant: memoised or native tak, real clock.
        rows.Add(("forge-instant", null, Build(vars, (i, v) => 0.1)));
        // 2. One plausible time for every input.
        rows.Add(("forge-constant", null, Build(vars, (i, v) => 15)));
        // 3. Replay: another genuine run's times, in its order.
        var oparts = other.Value[(other.Value.IndexOf('|') + 1)..].Split(';');
        rows.Add(("forge-replay", null, Build(vars, (i, v) => oparts[i].Split(':')[1].Split(',').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).Min())));
        // 4. Replay sorted: the other run's times matched by rank of x.
        var otimes = oparts.Select(p => p.Split(':')[1].Split(',').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).Min()).OrderBy(t => t).ToArray();
        var rankX = vars.Select((v, i) => (v[0], i)).OrderBy(p => p.Item1).Select((p, rank) => (p.i, rank)).ToDictionary(p => p.i, p => p.rank);
        rows.Add(("forge-replay-rank-x", null, Build(vars, (i, v) => otimes[rankX[i]])));
        // 5. Cost modelled on the inputs' size: proportional to x * (x - y).
        rows.Add(("forge-model-x", null, Build(vars, (i, v) => 0.2 * v[0] * (v[0] - v[1]))));
        // 6. Cost modelled as 2^(x - y), a common guess at tak's growth.
        rows.Add(("forge-model-exp", null, Build(vars, (i, v) => 0.01 * Math.Pow(2, v[0] - v[1]))));
        // 7. Control: the right model, which needs the exact call counts.
        rows.Add(("forge-exact-counts", null, Build(vars, (i, v) => 5e-6 * TakTiming.Calls(v[0], v[1], v[2]).Calls)));
        // Replay of the genuine value against new inputs, as sent.
        rows.Add(("forge-resend", null, other.Value));
        var last = rows.Count;
        for (var i = last - 8; i < last; i++) rows[i] = (rows[i].Item1, TakTiming.Verify(vars, rows[i].Item3), rows[i].Item3);
    }
    Report(rows);
    return 0;
}
Console.Error.WriteLine("unknown command");
return 1;
