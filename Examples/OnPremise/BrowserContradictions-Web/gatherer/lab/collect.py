"""Lab collector: serves the gatherer page and stores each run as JSON."""
import json, os, sys, time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

HERE = os.path.dirname(os.path.abspath(__file__))
RUNS = os.path.join(HERE, "runs")
os.makedirs(RUNS, exist_ok=True)
GATHERER = sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, "browser-tests-v2.js")

PAGE = """<!doctype html><html><head><meta charset="utf-8"><title>51Degrees browser tests lab</title></head>
<body><pre id="out">running</pre>
<script src="/browser-tests-v2.js"></script>
<script>
var label = new URLSearchParams(location.search).get("label") || "unlabelled";
window.__51dBrowserTests.run(4000).then(function (r) {
  r.label = label; r.gen = 2; r.ua = navigator.userAgent; r.when = new Date().toISOString();
  return fetch("/collect", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(r) })
    .then(function () { document.getElementById("out").textContent = "done " + label + " " + r.done.length + " tests " + r.elapsed + "ms"; document.title = "done"; });
}).catch(function (e) { document.getElementById("out").textContent = "failed " + e; document.title = "failed"; });
</script></body></html>"""


class H(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def do_GET(self):
        if self.path.startswith("/browser-tests-v2.js"):
            body = open(GATHERER, "rb").read()
            self.send_response(200); self.send_header("Content-Type", "text/javascript"); self.send_header("Cache-Control", "no-store")
        else:
            body = PAGE.encode(); self.send_response(200); self.send_header("Content-Type", "text/html")
        self.send_header("Content-Length", str(len(body))); self.end_headers(); self.wfile.write(body)

    def do_POST(self):
        n = int(self.headers.get("Content-Length", "0"))
        data = json.loads(self.rfile.read(n))
        name = f"{data.get('label','x')}-{int(time.time()*1000)}.json"
        with open(os.path.join(RUNS, name), "w", encoding="utf-8") as f:
            json.dump(data, f, indent=1)
        print("stored", name, len(data.get("results", {})), "results", data.get("elapsed"), "ms", flush=True)
        self.send_response(204); self.end_headers()


port = int(sys.argv[1]) if len(sys.argv) > 1 else 5180
print("collector on", port, "gatherer", GATHERER, flush=True)
ThreadingHTTPServer(("0.0.0.0", port), H).serve_forever()
