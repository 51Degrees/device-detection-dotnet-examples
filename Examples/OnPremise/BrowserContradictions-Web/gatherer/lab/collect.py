"""Lab collector: serves the gatherer page and stores each run as JSON."""
import json, os, sys, time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

HERE = os.path.dirname(os.path.abspath(__file__))
# BC_RUNS moves the runs folder, for example off a nearly full drive.
RUNS = os.environ.get("BC_RUNS") or os.path.join(HERE, "runs")
os.makedirs(RUNS, exist_ok=True)
GATHERER = sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, "..", "browser-tests-v3.js")
PROBE = os.path.join(HERE, "maths-probe.js")
SW = os.path.join(HERE, "..", "browser-tests-sw.js")

PAGE = """<!doctype html><html><head><meta charset="utf-8"><title>51Degrees browser tests lab</title></head>
<body><pre id="out">running</pre>
<script src="/gatherer.js"></script>
<script>
var q = new URLSearchParams(location.search);
var label = q.get("label") || "unlabelled";
// ?params=<json> runs the gatherer with chosen inputs instead of its defaults.
var params = q.get("params") ? JSON.parse(q.get("params")) : {};
// Lab only: per test timings, ?budget=<ms> (default 4000), ?sw=1 runs the service worker tests, ?only=a,b runs a subset.
params.timing = true;
if (q.get("sw")) params.swUrl = "/browser-tests-sw.js";
if (q.get("only")) params.only = q.get("only").split(",");
// ?reload=1 runs the gatherer once, reloads the page in the same tab and
// stores only the second run, the page reload case.
var reload = q.get("reload") && !sessionStorage.getItem("bc-reloaded");
var vis0 = document.visibilityState, focus0 = document.hasFocus();
window.__51dBrowserTests.run(Number(q.get("budget")) || 4000, params).then(function (r) {
  if (reload) { sessionStorage.setItem("bc-reloaded", "1"); location.reload(); return new Promise(function () { }); }
  r.label = label; r.gen = Number(r.version || 2); r.params = params; r.dpr = devicePixelRatio; r.ua = navigator.userAgent; r.when = new Date().toISOString();
  r.page = { reloaded: !!sessionStorage.getItem("bc-reloaded"), vis: vis0 + ">" + document.visibilityState, focus: focus0 + ">" + document.hasFocus() };
  return fetch("/collect", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(r) })
    .then(function () { document.getElementById("out").textContent = "done " + label + " " + r.done.length + " tests " + r.elapsed + "ms"; document.title = "done"; });
}).catch(function (e) { document.getElementById("out").textContent = "failed " + e; document.title = "failed"; });
</script></body></html>"""

# /maths runs maths-probe.js and stores every result, for finding the inputs
# that separate one build or OS from another.
MATHS = """<!doctype html><html><head><meta charset="utf-8"><title>maths probe</title></head>
<body><pre id="out">running</pre>
<script src="/maths-probe.js"></script>
<script>
var label = new URLSearchParams(location.search).get("label") || "unlabelled";
var r = { label: label, kind: "maths", ua: navigator.userAgent, when: new Date().toISOString(), maths: window.__mathsProbe() };
fetch("/collect", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(r) })
  .then(function () { document.getElementById("out").textContent = "done " + label; document.title = "done"; });
</script></body></html>"""


# /voices stores the full speechSynthesis voice list after 4 seconds, to
# check which voices a_voices counts.
VOICES = """<!doctype html><html><head><meta charset="utf-8"><title>voices</title></head><body><pre id="out">running</pre><script>
var label = new URLSearchParams(location.search).get("label") || "unlabelled";
speechSynthesis.getVoices(); speechSynthesis.onvoiceschanged = function () { };
setTimeout(function () {
  var v = speechSynthesis.getVoices().map(function (x) { return x.name + "/" + x.lang + "/" + (x.localService ? "L" : "R"); });
  fetch("/collect", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ label: label, kind: "voices", voices: v, ua: navigator.userAgent }) })
    .then(function () { document.title = "done"; });
}, 4000);
</script></body></html>"""


class H(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def do_GET(self):
        if self.path.startswith("/gatherer.js") or self.path.startswith("/browser-tests-v"):
            body = open(GATHERER, "rb").read()
            self.send_response(200); self.send_header("Content-Type", "text/javascript"); self.send_header("Cache-Control", "no-store")
        elif self.path.startswith("/browser-tests-sw.js"):
            body = open(SW, "rb").read()
            self.send_response(200); self.send_header("Content-Type", "text/javascript"); self.send_header("Cache-Control", "no-store")
        elif self.path.startswith("/maths-probe.js"):
            body = open(PROBE, "rb").read()
            self.send_response(200); self.send_header("Content-Type", "text/javascript"); self.send_header("Cache-Control", "no-store")
        elif self.path.startswith("/voices"):
            body = VOICES.encode(); self.send_response(200); self.send_header("Content-Type", "text/html")
        elif self.path.startswith("/maths"):
            body = MATHS.encode(); self.send_response(200); self.send_header("Content-Type", "text/html")
        else:
            body = PAGE.encode(); self.send_response(200); self.send_header("Content-Type", "text/html")
            # ?csp=1: a site whose Content Security Policy allows workers
            # from its own origin only, so the gatherer's blob: workers fail.
            if "csp=1" in self.path:
                self.send_header("Content-Security-Policy", "worker-src 'self'")
        self.send_header("Content-Length", str(len(body))); self.end_headers(); self.wfile.write(body)

    def do_POST(self):
        n = int(self.headers.get("Content-Length", "0"))
        data = json.loads(self.rfile.read(n))
        name = f"{data.get('label','x')}-{int(time.time()*1000)}.json"
        with open(os.path.join(RUNS, name), "w", encoding="utf-8") as f:
            json.dump(data, f, indent=1)
        print("stored", name, len(data.get("results", data.get("maths", {}))), "results", data.get("elapsed"), "ms", flush=True)
        self.send_response(204); self.end_headers()


port = int(sys.argv[1]) if len(sys.argv) > 1 else 5180
print("collector on", port, "gatherer", GATHERER, flush=True)
ThreadingHTTPServer(("0.0.0.0", port), H).serve_forever()
