using FiftyOne.DeviceDetection;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;

namespace BrowserContradictions.Web;

/// <summary>
/// Prototype of the browser element. Reads the raw measurements the
/// gatherer JavaScript sends as a 51D_ cookie, works out the platform the
/// browser claims from device detection, and checks each measurement
/// against the hashed baseline the data file carries in the values of the
/// BrowserBaseline properties, by the element rule of PHASE2-FORMATS.
/// Publishes its answers under the element data key "browser".
/// <para>
/// The gatherer ships obfuscated per seed (PHASE2-FORMATS, packaging and
/// key map): its wire keys and cookie name change with every data file. The
/// decode map rides in the description of the BrowserTestsKeyMap value and
/// is read from engine metadata like the baseline, so it is never part of
/// the per request results. The cookie the element reads is the unrenamed
/// 51D_BrowserTests plus every "$cookie" name in the maps.
/// </para>
/// <para>
/// Which tests failed, and the measurements themselves, are logged on the
/// server and published only when detail is turned on (never in
/// production): a list of failing tests in a response would tell a forger
/// exactly which answers to fix.
/// </para>
/// </summary>
public class BrowserTestsElement : FlowElementBase<IElementData, IElementPropertyMetaData>
{
    public const string DefaultCookie = "51D_BrowserTests";

    private readonly IEvidenceKeyFilter _filter;

    private readonly IList<IElementPropertyMetaData> _properties;

    private readonly bool _detail;

    public BrowserTestsElement(
        ILogger<BrowserTestsElement> logger,
        Func<IPipeline, FlowElementBase<IElementData, IElementPropertyMetaData>, IElementData> dataFactory,
        bool detail = false)
        : base(logger, dataFactory)
    {
        _detail = detail;
        _filter = new CookieFilter(this);
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

        var snapshot = GetSnapshot(data.Pipeline);

        // The renamed cookie first: it is what the shipped script writes.
        string raw = null, source = "none";
        foreach (var name in snapshot.CookieNames)
        {
            if (data.TryGetEvidence("query." + name, out string q) && string.IsNullOrEmpty(q) == false)
            {
                raw = q; source = "query";
                break;
            }
            if (data.TryGetEvidence("cookie." + name, out string c) && string.IsNullOrEmpty(c) == false)
            {
                raw = c; source = "cookie";
                break;
            }
        }
        result["EvidenceSource"] = source;
        result["Contradictions"] = "";
        result["Measurements"] = "";

        if (raw == null)
        {
            result["HasContradictions"] = "Unknown";
            return;
        }

        var measurements = Parse(raw, snapshot.KeyMap);
        if (_detail)
        {
            result["Measurements"] = string.Join("; ", measurements.Select(i => $"{i.Test}={i.Value}"));
        }

        // The element rule of PHASE2-FORMATS. A test counts only where the
        // baseline covers it for this claim, and the claim is Unknown when
        // the baseline covers none of the tests the browser sent. No test
        // is named here, the baseline alone decides which ones count.
        // A test the exact claim does not cover falls back to the version
        // independent rows of the browser and platform, when the claimed
        // major is inside their range.
        var baseline = snapshot.Baseline;
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
        Logger.LogInformation(
            "Browser tests claim {Claim} source {Source} measured {Measured} evaluated {Evaluated} failed {Failed}: {Tests}",
            claim, source, measurements.Count, evaluated, failed.Count, string.Join(",", failed));
        if (evaluated == 0)
        {
            result["HasContradictions"] = "Unknown";
            return;
        }
        result["HasContradictions"] = failed.Count > 0 ? "True" : "False";
        if (_detail)
        {
            result["Contradictions"] = string.Join(",", failed);
        }
    }

    /// <summary>
    /// Reads the baseline and the key maps out of the engine's property
    /// metadata rather than from the per request device data. They are
    /// then never part of the device results, so the JSON and JavaScript
    /// builders cannot send them to the browser, and the BrowserBaseline
    /// and BrowserTestsKeyMap properties are left out of the engine's
    /// required properties. For the baseline, the name and the description
    /// of every value of every property whose name starts with
    /// BrowserBaseline are read, and the rows of every payload among them
    /// are unioned, so the reader works whether a payload is a value's name
    /// (one shared value) or its description (one list value per claim,
    /// named by the claim), however the rows are split across properties
    /// and profiles. Texts that are not a payload, such as the Unknown
    /// default or a claim key, are ignored. The key maps are the
    /// descriptions of the BrowserTestsKeyMap values that start "1|keymap".
    /// </summary>
    private Snapshot GetSnapshot(IPipeline pipeline)
    {
        var engine = pipeline?.GetElement<FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements.DeviceDetectionHashEngine>();
        if (engine == null) return _snapshot ?? Snapshot.Empty;
        var published = engine.DataFiles[0].DataPublishedDateTime;
        var current = _snapshot;
        if (current == null || current.From != published)
        {
            IEnumerable<IValueMetaData> Values(Func<string, bool> name) => engine.Properties
                .Where(i => name(i.Name))
                .SelectMany(i => i.GetValues() ?? []);
            var baseline = new Baseline(
                Values(i => i.StartsWith(BaselinePrefix, StringComparison.Ordinal))
                    .SelectMany(i => new[] { i.Name, i.Description }),
                published);
            var keyMap = KeyMapText.Read(
                Values(i => i == KeyMapProperty).Select(i => i.Description),
                out var conflicts);
            if (conflicts > 0)
            {
                Logger.LogWarning("{Conflicts} wire keys mean different tests in different key maps and are ignored", conflicts);
            }
            current = new Snapshot(baseline, keyMap, published);
            _snapshot = current;
        }
        return current;
    }

    private const string BaselinePrefix = "BrowserBaseline";

    private const string KeyMapProperty = "BrowserTestsKeyMap";

    private Snapshot _snapshot;

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

    /// <summary>
    /// The baseline and key map of one data file, swapped whole when the
    /// data file changes.
    /// </summary>
    private sealed class Snapshot
    {
        public static readonly Snapshot Empty = new(new Baseline([], default), new Dictionary<string, string>(), default);

        public Snapshot(Baseline baseline, IReadOnlyDictionary<string, string> keyMap, DateTime from)
        {
            Baseline = baseline;
            KeyMap = keyMap;
            From = from;
            CookieNames = keyMap
                .Where(i => i.Key.StartsWith(KeyMapText.CookiePrefix, StringComparison.Ordinal))
                .Select(i => i.Value)
                .Append(DefaultCookie)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            EvidenceKeys = new HashSet<string>(
                CookieNames.SelectMany(i => new[] { "cookie." + i, "query." + i }),
                StringComparer.OrdinalIgnoreCase);
        }

        public Baseline Baseline { get; }

        public IReadOnlyDictionary<string, string> KeyMap { get; }

        public DateTime From { get; }

        public string[] CookieNames { get; }

        public HashSet<string> EvidenceKeys { get; }
    }

    /// <summary>
    /// Accepts the cookie and query keys of every cookie name the current
    /// data file's key maps give, which change with the data file, so the
    /// web integration collects the renamed cookie.
    /// </summary>
    private sealed class CookieFilter(BrowserTestsElement element) : IEvidenceKeyFilter
    {
        public bool Include(string key)
        {
            if (key == null) return false;
            var snapshot = element._snapshot;
            if (snapshot == null)
            {
                // Before the first request, read the maps through the
                // pipeline the element is in.
                snapshot = element.GetSnapshot(element.Pipelines.FirstOrDefault());
            }
            return snapshot.EvidenceKeys.Contains(key);
        }

        public int? Order(string key) => Include(key) ? 0 : null;
    }
}

/// <summary>
/// Key map text (PHASE2-FORMATS): a "1|keymap" line, then one key=value
/// line per entry. Several maps are unioned. A key two maps give different
/// values for is dropped, so it is ignored like any unknown test, rather
/// than counted against the wrong test. "$cookie" entries from every map
/// are kept, numbered.
/// </summary>
internal static class KeyMapText
{
    public const string CookiePrefix = "$cookie";

    public static Dictionary<string, string> Read(IEnumerable<string> texts, out int conflicts)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var dropped = new HashSet<string>(StringComparer.Ordinal);
        var cookies = 0;
        foreach (var text in texts)
        {
            var lines = (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0 || lines[0].TrimEnd('\r') != "1|keymap") continue;
            foreach (var line in lines.Skip(1))
            {
                var at = line.IndexOf('=');
                if (at <= 0) continue;
                var key = line[..at];
                var value = line[(at + 1)..].TrimEnd('\r');
                if (key == CookiePrefix)
                {
                    map[CookiePrefix + (cookies++ == 0 ? "" : cookies.ToString())] = value;
                    continue;
                }
                if (dropped.Contains(key)) continue;
                if (map.TryGetValue(key, out var existing) && existing != value)
                {
                    map.Remove(key);
                    dropped.Add(key);
                    continue;
                }
                map[key] = value;
            }
        }
        conflicts = dropped.Count;
        return map;
    }
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
    private bool _detail;

    /// <summary>
    /// Publish the decoded measurements and the failing test ids in the
    /// results. For the lab only: in production they would tell a forger
    /// which answers to fix. Off by default.
    /// </summary>
    public BrowserTestsElementBuilder SetDetail(bool detail)
    {
        _detail = detail;
        return this;
    }

    public BrowserTestsElement Build() => new(
        loggerFactory.CreateLogger<BrowserTestsElement>(),
        (pipeline, element) => new BrowserTestsData(
            loggerFactory.CreateLogger<ElementDataBase>(), pipeline),
        _detail);
}

internal sealed class BrowserTestsData(ILogger<ElementDataBase> logger, IPipeline pipeline)
    : ElementDataBase(logger, pipeline)
{
}
