using BrowserContradictions.Web;
using FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Web.Services;
using System.Net;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<DeviceDetectionHashEngineBuilder>();
builder.Services.AddSingleton<BrowserTestsElementBuilder>();
builder.Services.AddSingleton<FiftyOne.Pipeline.DerivedProperty.FlowElements.DerivedPropertyElementBuilder>();
builder.Services.AddFiftyOne(builder.Configuration);

var app = builder.Build();
app.UseFiftyOne();

// The page shows what the server worked out for this very request, then
// lets 51Degrees.core.js run the gatherer and shows what the JSON endpoint
// answers once the measurements have been sent back.
app.MapGet("/", (IFlowDataProvider provider) =>
{
    var data = provider.GetFlowData();
    var rows = new StringBuilder();
    void Row(string key, Func<object> read)
    {
        string value;
        try
        {
            var v = read();
            value = v is IAspectPropertyValue apv
                ? (apv.HasValue ? apv.Value?.ToString() : $"no value: {apv.NoValueMessage}")
                : v?.ToString();
        }
        catch (Exception ex)
        {
            value = $"{ex.GetType().Name}: {ex.Message}";
        }
        if (value != null && value.Length > 160) value = $"{value[..160]}... ({value.Length} chars)";
        rows.Append($"<tr><th>{key}</th><td id=\"s-{key.Replace('.', '-')}\">{WebUtility.HtmlEncode(value)}</td></tr>");
    }
    var device = data.Get("device");
    var browser = data.Get("browser");
    var derived = data.Get("derived");
    foreach (var p in new[] { "BrowserName", "BrowserVersion", "PlatformName", "IsCrawler", "HasWebDriver", "IsVisible", "BrowserTests" })
        Row($"device.{p}", () => device[p]);
    foreach (var p in new[] { "Claim", "EvidenceSource", "HasContradictions", "CanaryTripped", "Contradictions", "Measurements" })
        Row($"browser.{p}", () => browser[p]);
    Row("derived.HumanConfidenceProof", () => derived["HumanConfidenceProof"]);

    var html = $$"""
        <!doctype html>
        <html><head><meta charset="utf-8"><title>Browser contradictions proof</title>
        <style>body{font:14px system-ui;margin:24px}table{border-collapse:collapse}th,td{border:1px solid #ccc;padding:4px 8px;text-align:left;vertical-align:top;font-family:Consolas,monospace;font-size:12px}h2{margin-top:24px}</style>
        </head><body>
        <h1>Browser contradictions proof</h1>
        <h2>Server side, this request</h2>
        <table>{{rows}}</table>
        <h2>Client side, after the gatherer ran</h2>
        <pre id="client">waiting for 51Degrees.core.js</pre>
        <script src="/51Degrees.core.js"></script>
        <script src="/report.js"></script>
        <script>
        window.onload = function () { fod.complete(function (data) { report(data, "onprem"); }); };
        </script>
        </body></html>
        """;
    return Results.Content(html, "text/html");
});

// Shows and reports what the JSON endpoint answered once the gatherer ran.
// A run labelled with ?label= posts its answer to /report, so headed
// browsers driven from the command line can be read back from the log.
// "baseline leaked" is true if any BrowserBaseline payload reached the
// browser.
const string ReportJs = """
    function report(data, where) {
      var b = data.browser || {}, d = data.device || {}, x = data.derived || {};
      var text = JSON.stringify(data);
      var params = new URLSearchParams(location.search);
      function cell(id) { var e = document.getElementById(id); return e ? e.textContent : null; }
      var out = {
        "where": where,
        "label": new URLSearchParams(location.search).get("label"),
        "ua": navigator.userAgent,
        "browser.evidencesource": b.evidencesource,
        "browser.claim": b.claim,
        "browser.hascontradictions": b.hascontradictions,
        "browser.contradictions": b.contradictions,
        "browser.canarytripped": b.canarytripped,
        "derived.humanconfidenceproof": x.humanconfidenceproof,
        "device.haswebdriver": d.haswebdriver,
        "baseline leaked": text.indexOf("2|h64") >= 0 || Object.keys(d).some(function (k) {
          return k.indexOf("browserbaseline") === 0 && !/nullreason$/.test(k) && d[k] !== null && d[k] !== undefined;
        }),
        "browser.measurements": b.measurements,
        "keymap leaked": text.indexOf("1|keymap") >= 0,
        "test ids leaked": /[a-z]_[a-z0-9]+\.[0-9]+/.test(text),
        "ms to verdict": Math.round(performance.now()),
        "timeline": performance.getEntriesByType("resource").filter(function (e) {
          return /core\.js|api\/v4|51dpipeline|3pc/.test(e.name);
        }).map(function (e) {
          return e.name.replace(/^.*\//, "").slice(0, 20) + "@" + Math.round(e.startTime) + "+" + Math.round(e.duration);
        }).join(" "),
        "pass": params.get("pass") || "1",
        "diag": (window.__diag || []).join(" "),
        "server.evidencesource": cell("s-browser-EvidenceSource"),
        "server.hascontradictions": cell("s-browser-HasContradictions"),
        "server.canarytripped": cell("s-browser-CanaryTripped"),
        "server.humanconfidenceproof": cell("s-derived-HumanConfidenceProof")
      };
      document.getElementById("client").textContent = JSON.stringify(out, null, 2);
      if (out.label) {
        // A labelled run loads the page a second time, so the page request
        // itself carries the cookie and the server side table shows its
        // verdict (pass 2). The packed script skips the tests then.
        fetch("/report", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(out) })
          .then(function () {
            if (out.pass === "1") { params.set("pass", "2"); location.replace(location.pathname + "?" + params.toString()); }
            else { document.title = "reported"; }
          });
      }
    }
    """;
app.MapGet("/report.js", () => Results.Content(ReportJs, "text/javascript"));

// With ?diag=1 the cloud page records when the packed script inflates,
// runs, sends and writes its cookie, and when visibility changes, so a slow
// round in a genuine browser can be read back from the report.
const string DiagJs = """
    (function () {
      var log = window.__diag = [];
      function L(s) { log.push(s + "@" + Math.round(performance.now()) + (document.hidden ? "h" : "")); }
      var d = Object.getOwnPropertyDescriptor(Document.prototype, "cookie");
      Object.defineProperty(document, "cookie", { configurable: true, get: function () { return d.get.call(document); },
        set: function (v) { L("cookie:" + v.slice(0, 12)); d.set.call(document, v); } });
      var D = window.DecompressionStream;
      if (D) window.DecompressionStream = function (f) { L("inflate"); return new D(f); };
      var F = window.Function;
      var W = function () {
        var a = Array.prototype.slice.call(arguments);
        if (a[0] === "send") {
          L("run"); var g = F.apply(this, a);
          return function (r) { return g(function (v) { L("send:" + String(v).length); r(v); }); };
        }
        return F.apply(this, a);
      };
      W.prototype = F.prototype; window.Function = W;
      document.addEventListener("visibilitychange", function () { L("vis"); });
      L("start");
    })();
    """;
app.MapGet("/diag.js", () => Results.Content(DiagJs, "text/javascript"));

var reports = Path.Combine(AppContext.BaseDirectory, "reports.jsonl");
var reportsLock = new object();
app.MapPost("/report", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var line = (await reader.ReadToEndAsync()).ReplaceLineEndings(" ");
    lock (reportsLock) File.AppendAllText(reports, line + Environment.NewLine);
    Console.WriteLine($"report {line}");
    return Results.NoContent();
});

// The same check through the private cloud: the page loads the cloud's
// own JavaScript, which runs the gatherer from the cloud's data file and
// asks the cloud's JSON endpoint for the answer.
app.MapGet("/cloud", (HttpRequest request) =>
{
    var cloud = request.Query["cloud"].FirstOrDefault() ?? "http://localhost:5160";
    var html = $$"""
        <!doctype html>
        <html><head><meta charset="utf-8"><title>Cloud browser contradictions proof</title>
        <style>body{font:14px system-ui;margin:24px}pre{font:12px Consolas,monospace}</style>
        </head><body>
        <h1>Cloud browser contradictions proof</h1>
        <pre id="client">waiting for the cloud JavaScript</pre>
        {{(request.Query.ContainsKey("diag") ? "<script src=\"/diag.js\"></script>" : "")}}
        <script src="/report.js"></script>
        <script>
        function show() { fod.complete(function (data) { report(data, "cloud"); }); }
        </script>
        <script src="{{WebUtility.HtmlEncode(cloud)}}/api/v4/js" onload="show()"></script>
        </body></html>
        """;
    return Results.Content(html, "text/html");
});

app.Run();
