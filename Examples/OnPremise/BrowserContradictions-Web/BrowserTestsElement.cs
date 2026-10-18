using FiftyOne.DeviceDetection;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;

namespace BrowserContradictions.Web;

/// <summary>
/// Prototype of the browser element. Reads the raw measurements the
/// gatherer JavaScript sends as 51D_BrowserTests, works out the platform
/// the browser claims from device detection, and checks each measurement
/// against the hashed baseline the data file carries in the values of the
/// BrowserBaseline properties, by the element rule of PHASE2-FORMATS.
/// Publishes its answers under the element data key "browser".
/// </summary>
public class BrowserTestsElement : FlowElementBase<IElementData, IElementPropertyMetaData>
{
    public const string CookieKey = "cookie.51D_BrowserTests";
    public const string QueryKey = "query.51D_BrowserTests";

    private readonly IEvidenceKeyFilter _filter =
        new EvidenceKeyFilterWhitelist([CookieKey, QueryKey]);

    private readonly IList<IElementPropertyMetaData> _properties;

    public BrowserTestsElement(
        ILogger<BrowserTestsElement> logger,
        Func<IPipeline, FlowElementBase<IElementData, IElementPropertyMetaData>, IElementData> dataFactory)
        : base(logger, dataFactory)
    {
        _properties =
        [
            new ElementPropertyMetaData(this, "HasContradictions", typeof(string), true, "Bot Detection"),
            new ElementPropertyMetaData(this, "Contradictions", typeof(string), true, "Bot Detection"),
            new ElementPropertyMetaData(this, "Claim", typeof(string), true, "Bot Detection"),
            new ElementPropertyMetaData(this, "Measurements", typeof(string), true, "Bot Detection"),
            new ElementPropertyMetaData(this, "EvidenceSource", typeof(string), true, "Bot Detection"),
            new ElementPropertyMetaData(this, "ClaimScope", typeof(string), true, "Bot Detection"),
        ];
    }

    public override string ElementDataKey => "browser";

    public override IEvidenceKeyFilter EvidenceKeyFilter => _filter;

    public override IList<IElementPropertyMetaData> Properties => _properties;

    protected override void ProcessInternal(IFlowData data)
    {
        var result = data.GetOrAdd(ElementDataKey, CreateElementData);
        var device = data.Get<IDeviceData>();

        var browser = Read(device, "BrowserName");
        var version = Read(device, "BrowserVersion");
        var major = version.Split('.')[0];
        var platform = Read(device, "PlatformName");
        var claim = $"{browser}|{major}|{platform}";
        result["Claim"] = claim;
        result["ClaimScope"] = "";

        string raw = null, source = "none";
        if (data.TryGetEvidence(QueryKey, out string q) && string.IsNullOrEmpty(q) == false)
        {
            raw = q; source = "query";
        }
        else if (data.TryGetEvidence(CookieKey, out string c) && string.IsNullOrEmpty(c) == false)
        {
            raw = c; source = "cookie";
        }
        result["EvidenceSource"] = source;
        result["Contradictions"] = "";
        result["Measurements"] = "";

        if (raw == null)
        {
            result["HasContradictions"] = "Unknown";
            return;
        }

        var measurements = Parse(raw);
        result["Measurements"] = string.Join("; ", measurements.Select(i => $"{i.Test}={i.Value}"));

        // The element rule of PHASE2-FORMATS. A test counts only where the
        // baseline covers it for this claim, and the claim is Unknown when
        // the baseline covers none of the tests the browser sent. No test
        // is named here, the baseline alone decides which ones count.
        // A test the exact claim does not cover falls back to the version
        // independent rows of the browser and platform, when the claimed
        // major is inside their range.
        var baseline = GetBaseline(data.Pipeline);
        var any = baseline.AnyVersionClaim(browser, major, platform);
        var evaluated = 0;
        var viaAny = 0;
        var failed = new List<string>();
        foreach (var (test, value) in measurements)
        {
            string scope;
            if (baseline.Contains($"{claim}|{test}")) scope = claim;
            else if (any != null && baseline.Contains($"{any}|{test}")) { scope = any; viaAny++; }
            else continue;
            evaluated++;
            if (baseline.Contains($"{scope}|{test}|{value}") == false) failed.Add(test);
        }
        result["ClaimScope"] = viaAny == 0 ? "exact" : viaAny == evaluated ? "any version" : "mixed";
        if (evaluated == 0)
        {
            result["HasContradictions"] = "Unknown";
            return;
        }
        result["HasContradictions"] = failed.Count > 0 ? "True" : "False";
        result["Contradictions"] = string.Join(",", failed);
    }

    /// <summary>
    /// Reads the baseline out of the engine's property metadata rather than
    /// from the per request device data. The baseline is then never part
    /// of the device results, so the JSON and JavaScript builders cannot
    /// send it to the browser, and the BrowserBaseline properties can be
    /// left out of the engine's required properties. The name and the
    /// description of every value of every property whose name starts with
    /// BrowserBaseline are read, and the rows of every payload among them
    /// are unioned, so the reader works whether a payload is a value's name
    /// (one shared value) or its description (one list value per claim,
    /// named by the claim), however the rows are split across properties
    /// and profiles. Texts that are not a payload, such as the Unknown
    /// default or a claim key, are ignored.
    /// </summary>
    private Baseline GetBaseline(IPipeline pipeline)
    {
        var engine = pipeline.GetElement<FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements.DeviceDetectionHashEngine>();
        var published = engine.DataFiles[0].DataPublishedDateTime;
        var current = _baseline;
        if (current == null || current.From != published)
        {
            var texts = engine.Properties
                .Where(i => i.Name.StartsWith(BaselinePrefix, StringComparison.Ordinal))
                .SelectMany(i => i.GetValues() ?? [])
                .SelectMany(i => new[] { i.Name, i.Description });
            current = new Baseline(texts, published);
            _baseline = current;
        }
        return current;
    }

    private const string BaselinePrefix = "BrowserBaseline";

    private Baseline _baseline;

    private static string Read(IDeviceData device, string name)
    {
        try
        {
            return device[name] is IAspectPropertyValue v && v.HasValue ? v.Value?.ToString() ?? "" : "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Reads a version 2 or 3 payload, "3|id=value&amp;id=value", URI
    /// encoded, into (test, value) pairs. Each wire key is first looked up
    /// in keyMap, where a script obfuscated per seed sends renamed keys and
    /// the map gives the real versioned id for each (entries such as
    /// "$global" and "$cookie" never match a key). Then an id with no
    /// version is version 1, so a version 2 payload's "f_emoji" is looked up
    /// as "f_emoji.1".
    /// </summary>
    internal static List<(string Test, string Value)> Parse(string raw, IReadOnlyDictionary<string, string> keyMap = null)
    {
        string Id(string key) => NormaliseId(keyMap != null && keyMap.TryGetValue(key, out var real) ? real : key);
        var text = Uri.UnescapeDataString(raw);
        var bar = text.IndexOf('|');
        if (bar < 0 || (text[..bar] != "2" && text[..bar] != "3")) return [];
        return text[(bar + 1)..]
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(i => i.Split('=', 2))
            .Where(i => i.Length == 2 && i[0].Length > 0)
            .Select(i => (Id(Uri.UnescapeDataString(i[0])), Uri.UnescapeDataString(i[1])))
            .ToList();
    }

    internal static string NormaliseId(string id) => id.Contains('.') ? id : id + ".1";

    protected override void ManagedResourcesCleanup() { }

    protected override void UnmanagedResourcesCleanup() { }
}

/// <summary>
/// The hashed baseline, payload version 2 of PHASE2-FORMATS: a "2|h64"
/// line, a factor order line, then one FNV-1a 64 hash per row, where a row
/// is either a value row "browser|major|platform|test|value" or a coverage
/// row "browser|major|platform|test". Rows from several payloads are
/// unioned. A legacy "1|h64" payload is read the same way; its rows use
/// unversioned ids, so under the element rule they cover nothing.
/// A value named "browser|from-to|platform" (version independent rows,
/// see PHASE2-FORMATS) also declares that rows with the major "*" apply to
/// that browser and platform for majors from..to.
/// </summary>
internal sealed class Baseline
{
    private readonly HashSet<ulong> _rows = [];

    private static readonly System.Text.RegularExpressions.Regex RangeName =
        new(@"^(?<b>[^|\n]+)\|(?<from>\d+)-(?<to>\d+)\|(?<p>[^|\n]+)$");

    private readonly Dictionary<string, List<(int From, int To)>> _ranges = [];

    public DateTime From { get; }

    public int Payloads { get; }

    public Baseline(IEnumerable<string> texts, DateTime from)
    {
        From = from;
        foreach (var text in texts)
        {
            var range = RangeName.Match(text ?? "");
            if (range.Success)
            {
                var key = range.Groups["b"].Value + "|" + range.Groups["p"].Value;
                if (_ranges.TryGetValue(key, out var list) == false) _ranges[key] = list = [];
                list.Add((int.Parse(range.Groups["from"].Value), int.Parse(range.Groups["to"].Value)));
                continue;
            }
            var lines = (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var schema = lines.Length < 2 ? "" : lines[0].TrimEnd('\r');
            if (schema != "2|h64" && schema != "1|h64") continue;
            Payloads++;
            foreach (var line in lines.Skip(2))
            {
                if (ulong.TryParse(line.Trim(), System.Globalization.NumberStyles.HexNumber, null, out var row))
                {
                    _rows.Add(row);
                }
            }
        }
    }

    public int Count => _rows.Count;

    /// <summary>
    /// The row claim "browser|*|platform" when a version independent range
    /// for the browser and platform includes the major, otherwise null.
    /// </summary>
    public string AnyVersionClaim(string browser, string major, string platform) =>
        int.TryParse(major, out var m) &&
        _ranges.TryGetValue(browser + "|" + platform, out var list) &&
        list.Any(i => m >= i.From && m <= i.To)
            ? $"{browser}|*|{platform}"
            : null;

    public bool Contains(string row) => _rows.Contains(Fnv1a64(row));

    public static ulong Fnv1a64(string s)
    {
        ulong hash = 0xcbf29ce484222325;
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(s))
        {
            hash ^= b;
            hash *= 0x100000001b3;
        }
        return hash;
    }
}

public class BrowserTestsElementBuilder(ILoggerFactory loggerFactory)
{
    public BrowserTestsElement Build() => new(
        loggerFactory.CreateLogger<BrowserTestsElement>(),
        (pipeline, element) => new BrowserTestsData(
            loggerFactory.CreateLogger<ElementDataBase>(), pipeline));
}

internal sealed class BrowserTestsData(ILogger<ElementDataBase> logger, IPipeline pipeline)
    : ElementDataBase(logger, pipeline)
{
}
