// Checks TanhWalk.cs against the Python reference (200 bit mpmath) and the
// lab's browser captures.
//   dotnet run -- <mathlab dir>
using System.Diagnostics;
using System.Text.Json;
using BrowserContradictions.Web;

var dir = args[0];
var bad = 0;

// 1. Correctly rounded tanh, tie neighbour and fdlibm, per input.
int n = 0, crBad = 0, altBad = 0, fdBad = 0;
static double D(string h) => BitConverter.Int64BitsToDouble((long)Convert.ToUInt64(h, 16));
foreach (var line in File.ReadLines(Path.Combine(dir, "vectors.txt")))
{
    var p = line.Split(' ');
    var x = D(p[0]);
    var c = TanhWalk.CorrectlyRounded(x, out var alt);
    n++;
    if (c != D(p[1])) crBad++;
    if ((p[2] == "-") != !alt.HasValue || (alt.HasValue && alt.Value != D(p[2]))) altBad++;
    if (TanhWalk.Fdlibm.Tanh(x) != D(p[3])) fdBad++;
}
Console.WriteLine($"vectors {n}: correctly rounded wrong {crBad}, tie neighbour wrong {altBad}, fdlibm wrong {fdBad}");
bad += crBad + altBad + fdBad;

// 2. The lab captures: every run classified as its browser family.
var seeds = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "seeds.json"))).RootElement;
var steps = seeds.GetProperty("n").GetInt32();
var list = seeds.GetProperty("seeds").EnumerateArray().Select(s => (s[0].GetUInt32(), s[1].GetUInt32())).ToList();
foreach (var f in Directory.GetFiles(Path.Combine(dir, "results"), "walk-*.json").OrderBy(f => f))
{
    var r = JsonDocument.Parse(File.ReadAllText(f)).RootElement;
    var walks = r.GetProperty("walks").EnumerateArray().Select(w => w.GetString()).ToList();
    var counts = new Dictionary<TanhWalk.Family, int>();
    var badTags = 0;
    for (var i = 0; i < list.Count; i++)
    {
        // Gatherer runs send the tag for the seed, Python host runs do not.
        var w = walks[i];
        if (w != null && w.Length != 16)
        {
            var t = TanhWalk.Tag(list[i].Item1, list[i].Item2);
            if (t.Length == 0 || !w.StartsWith(t)) { badTags++; continue; }
            w = w[t.Length..];
        }
        var fam = TanhWalk.Classify(list[i].Item1, list[i].Item2, w, steps);
        counts[fam] = counts.GetValueOrDefault(fam) + 1;
    }
    bad += badTags;
    var dv = r.TryGetProperty("defaultSeed", out var dp) ? dp.GetString() : null;
    var dfam = dv == null ? "-" : TanhWalk.Classify(TanhWalk.DefaultSeed.A, TanhWalk.DefaultSeed.B, dv).ToString();
    Console.WriteLine($"{Path.GetFileNameWithoutExtension(f),-26} " + string.Join(", ", counts.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}"))
        + $"; bad tags {badTags}; default seed {dfam}");
}

// 3. .NET's Math.Tanh on this host against the Python UCRT run.
var py = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "results", "walk-py-win-ucrt.json"))).RootElement
    .GetProperty("walks").EnumerateArray().Select(w => w.GetString()).ToList();
var same = list.Select((s, i) => TanhWalk.Walk(s.Item1, s.Item2, Math.Tanh, steps) == py[i]).Count(x => x);
Console.WriteLine($".NET Math.Tanh walk equals the UCRT walk for {same} of {list.Count} seeds");

// 4. The default seed, the tag, and cost.
var d = TanhWalk.DefaultSeed;
var ans = TanhWalk.WindowsAnswers(d.A, d.B);
Console.WriteLine($"default seed: fdlibm {TanhWalk.Walk(d.A, d.B, TanhWalk.Fdlibm.Tanh)}, Windows answers [{string.Join(",", ans)}], UCRT here {TanhWalk.Walk(d.A, d.B, Math.Tanh)}");
Console.WriteLine($"tag for seed (1,2): {TanhWalk.Tag(1, 2)}");
var rnd = new Random(3);
var seedsT = Enumerable.Range(0, 2000).Select(_ => ((uint)rnd.NextInt64(1L << 32), (uint)rnd.NextInt64(1L << 32))).ToList();
var answersT = seedsT.Select(s => TanhWalk.Walk(s.Item1, s.Item2, Math.Tanh)).ToList();
var sw = Stopwatch.StartNew();
var pass = 0;
for (var i = 0; i < seedsT.Count; i++) if (TanhWalk.AcceptsWindows(seedsT[i].Item1, seedsT[i].Item2, answersT[i]) == true) pass++;
var genuine = sw.Elapsed.TotalMilliseconds / seedsT.Count;
sw.Restart();
var fdPass = 0;
for (var i = 0; i < seedsT.Count; i++) if (TanhWalk.AcceptsWindows(seedsT[i].Item1, seedsT[i].Item2, TanhWalk.Walk(seedsT[i].Item1, seedsT[i].Item2, TanhWalk.Fdlibm.Tanh)) == true) fdPass++;
var forged = sw.Elapsed.TotalMilliseconds / seedsT.Count;
Console.WriteLine($"2000 UCRT walks accepted {pass}, {genuine:F3} ms each; 2000 fdlibm walks accepted {fdPass}, {forged:F3} ms each");
bad += (seedsT.Count - pass) + fdPass;
Console.WriteLine(bad == 0 ? "PASS" : $"FAIL {bad}");
return bad == 0 ? 0 : 1;
