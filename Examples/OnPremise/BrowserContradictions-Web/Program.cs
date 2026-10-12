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
    foreach (var p in new[] { "Claim", "EvidenceSource", "Measurements", "HasContradictions", "Contradictions" })
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
        <script>
        window.onload = function () {
          fod.complete(function (data) {
            var b = data.browser || {}, d = data.device || {}, x = data.derived || {};
            document.getElementById("client").textContent = JSON.stringify({
              "device.browsertests": d.browsertests,
              "browser.evidencesource": b.evidencesource,
              "browser.claim": b.claim,
              "browser.measurements": b.measurements,
              "browser.hascontradictions": b.hascontradictions,
              "browser.contradictions": b.contradictions,
              "derived.humanconfidenceproof": x.humanconfidenceproof,
              "device.haswebdriver": d.haswebdriver,
              "device.isvisible": d.isvisible
            }, null, 2);
          });
        };
        </script>
        </body></html>
        """;
    return Results.Content(html, "text/html");
});

app.Run();
