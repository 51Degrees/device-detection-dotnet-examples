using FiftyOne.DeviceDetection;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;

namespace BrowserContradictions.Web;

/// <summary>
/// Prototype of the browser element. Reads the raw measurements the
/// gatherer JavaScript sends as 51D_BrowserTests, works out the platform
/// the browser claims from device detection, and checks each measurement
/// against the hashed baseline carried in device.BrowserBaseline. Publishes
/// its answers under the element data key "browser".
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

        if (raw == null)
        {
            result["HasContradictions"] = "Unknown";
            result["Contradictions"] = "";
            result["Measurements"] = "";
            return;
        }

        var measurements = Parse(raw);
        result["Measurements"] = string.Join("; ", measurements.Select(i => $"{i.Key}={i.Value}"));

        var baseline = GetBaseline(data.Pipeline);
        if (baseline.Contains($"{claim}|_|covered") == false)
        {
            // The baseline has no genuine answers for this claim, so nothing
            // can be said either way.
            result["HasContradictions"] = "Unknown";
            result["Contradictions"] = $"no baseline for {claim}";
            return;
        }

        // Only the tests the lab classified as stable across genuine runs
        // and structural to the platform decide the verdict. The rest are
        // gathered for the baseline but never count against a browser.
        var failed = measurements
            .Where(i => VerdictTests.Contains(i.Key) && baseline.Contains($"{claim}|{i.Key}|{i.Value}") == false)
            .Select(i => i.Key)
            .ToList();
        result["HasContradictions"] = failed.Count > 0 ? "True" : "False";
        result["Contradictions"] = string.Join(",", failed);
    }

    /// <summary>
    /// Reads the baseline out of the engine's property metadata rather than
    /// from the per request device data. The baseline is then never part
    /// of the device results, so the JSON and JavaScript builders cannot
    /// send it to the browser, and BrowserBaseline can be left out of the
    /// engine's required properties. The table is the longest value of the
    /// property, the other being the default Unknown.
    /// </summary>
    private Baseline GetBaseline(IPipeline pipeline)
    {
        var engine = pipeline.GetElement<FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements.DeviceDetectionHashEngine>();
        var dataFile = engine.DataFiles[0].DataPublishedDateTime;
        if (_baseline == null || _baselineFrom != dataFile)
        {
            var property = engine.Properties.FirstOrDefault(i => i.Name == "BrowserBaseline");
            var text = property?.Values?.Select(i => i.Name).OrderByDescending(i => i.Length).FirstOrDefault();
            _baseline = Baseline.Parse(text);
            _baselineFrom = dataFile;
        }
        return _baseline;
    }

    private Baseline _baseline;
    private DateTime _baselineFrom;

    internal static readonly HashSet<string> VerdictTests = new(StringComparer.Ordinal)
    {
        // maths library
        "m_tanh", "m_atanh",
        // OS font stack
        "f_kw_menu", "f_kw_smallcaption", "f_kw_statusbar", "f_core", "f_systemui", "f_emoji", "f_metrics",
        // graphics backend
        "g_renderer", "g_extensions", "g_limits", "g_precision", "g_samples", "g_canvasdeterminism", "a_webgpu", "a_webgpu_fallback",
        // OS services and media stack
        "a_voices", "c_syscolors", "c_heap", "c_codecs", "a_widevine", "a_keyboard",
        // platform API surface
        "p_apis",
        // engine version against the claim
        "e_features", "a_uachigh", "c_uach",
        // naive spoofs and automation
        "c_platform", "p_webdriver", "c_timezone",
    };

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

    internal static List<KeyValuePair<string, string>> Parse(string raw)
    {
        var text = Uri.UnescapeDataString(raw);
        var bar = text.IndexOf('|');
        if (bar < 0 || (text[..bar] != "1" && text[..bar] != "2")) return [];
        return text[(bar + 1)..]
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(i => i.Split('=', 2))
            .Where(i => i.Length == 2)
            .Select(i => new KeyValuePair<string, string>(i[0], Uri.UnescapeDataString(i[1])))
            .ToList();
    }

    protected override void ManagedResourcesCleanup() { }

    protected override void UnmanagedResourcesCleanup() { }
}

/// <summary>
/// The hashed baseline. The value text is the IsVerifiedModel h64 shape:
/// a schema line, a factor order line, then one FNV-1a 64 hash per row.
/// Parsed tables are cached by the text's reference, which is stable for
/// the life of a loaded data file.
/// </summary>
internal sealed class Baseline
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<string, Baseline> Cache = new();

    private readonly HashSet<ulong> _rows = [];

    public static Baseline Parse(string text) => Cache.GetValue(text ?? "", t => new Baseline(t));

    private Baseline(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2 || lines[0] != "1|h64") return;
        foreach (var line in lines.Skip(2))
        {
            _rows.Add(Convert.ToUInt64(line, 16));
        }
    }

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
