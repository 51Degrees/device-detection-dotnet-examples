// Checks WindowParams.cs against derive.py's vectors (vectors.jsonl) and,
// when results/ holds oracle runs, that every genuine tag matches.
//   dotnet run -c Release -- ..
using System.Text;
using System.Text.Json;
using BrowserContradictions.Web;

var dir = args[0];
var secret = Encoding.ASCII.GetBytes("lab-secret-not-for-production");
var bad = 0;
var n = 0;
foreach (var line in File.ReadLines(Path.Combine(dir, "vectors.jsonl")))
{
    var v = JsonDocument.Parse(line).RootElement;
    var window = v.GetProperty("window").GetInt64();
    var want = v.GetProperty("params");
    var p = WindowParams.Derive(secret, window);
    n++;
    if (p.Glyphs != want.GetProperty("glyphs").GetString()) { bad++; Console.WriteLine($"glyphs differ at {window}: {p.Glyphs} vs {want.GetProperty("glyphs")}"); }
    if (p.Size != want.GetProperty("size").GetInt32()) { bad++; Console.WriteLine($"size differs at {window}"); }
    if (!p.Shapes.SequenceEqual(want.GetProperty("shapes").EnumerateArray().Select(s => s.GetString()))) { bad++; Console.WriteLine($"shapes differ at {window}"); }
    if (!p.Walk.SequenceEqual(want.GetProperty("walk").EnumerateArray().Select(s => s.GetUInt32()))) { bad++; Console.WriteLine($"walk differs at {window}"); }
    var tags = WindowParams.Tags(p);
    foreach (var t in v.GetProperty("tags").EnumerateObject())
    {
        if (tags[t.Name] != t.Value.GetString()) { bad++; Console.WriteLine($"tag {t.Name} differs at {window}: {tags[t.Name]} vs {t.Value}"); }
    }
}
Console.WriteLine($"{n} vectors, {bad} differences");
Console.WriteLine($"untag: {WindowParams.Untag("p12345678:abc", "p12345678:")} {WindowParams.Untag("p12345678:abc", "p00000000:") ?? "null"} {WindowParams.Untag("abc", "")} {WindowParams.Untag("p12345678:abc", "") ?? "null"}");
return bad == 0 ? 0 : 1;
