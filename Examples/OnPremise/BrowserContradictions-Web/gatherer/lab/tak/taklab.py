"""m_takwork.1 lab server. See README.md.

  py -I taklab.py <port> [runs dir]

  /loop?label=L&from=0&n=40   runs m_takwork.1 alone for lists from..from+n-1
                              of seeds.json in one page, one after another
  /full?label=L&i=3           the whole gatherer once, with list i, so the
                              test runs beside every other test

Each run is stored as <runs>/<label>~<i>.json, in the lab collector's shape
(label, params, results, ua). When a page is finished it stores
<label>-done.json, which is what win-run.js waits for.
"""
import json
import os
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse, parse_qs

HERE = os.path.dirname(os.path.abspath(__file__))
GATHERER = os.path.join(HERE, "..", "..", "browser-tests-v3.js")
RUNS = sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, "runs")
os.makedirs(RUNS, exist_ok=True)

PAGE = """<!doctype html><meta charset=utf-8><title>taklab</title><pre id=out>running</pre>
<script src="/gatherer.js"></script>
<script>
(async function () {
  var q = new URLSearchParams(location.search), label = q.get("label") || "x";
  var seeds = await (await fetch("/seeds.json")).json();
  var full = location.pathname === "/full";
  var from = Number(q.get(full ? "i" : "from")) || 0, n = full ? 1 : Number(q.get("n")) || 40;
  var runs = [];
  for (var i = from; i < from + n && i < seeds.length; i++) {
    var params = { tak: seeds[i], timing: true };
    if (!full) params.only = ["m_takwork.1"];
    var r = await window.__51dBrowserTests.run(full ? 4000 : 15000, params);
    runs.push({ i: i, label: label, params: { tak: seeds[i] }, results: r.results, timing: r.timing, ua: navigator.userAgent, when: new Date().toISOString() });
    document.getElementById("out").textContent = label + " " + (i - from + 1) + "/" + n;
  }
  await fetch("/collect", { method: "POST", body: JSON.stringify({ label: label, runs: runs }) });
  document.getElementById("out").textContent = "done"; document.title = "done";
})().catch(function (e) { document.getElementById("out").textContent = "failed " + e; document.title = "failed"; });
</script>"""


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def _send(self, body, ctype):
        b = body.encode("utf-8") if isinstance(body, str) else body
        self.send_response(200)
        self.send_header("Content-Type", ctype)
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(b)))
        self.end_headers()
        self.wfile.write(b)

    def do_GET(self):
        path = urlparse(self.path).path
        if path in ("/loop", "/full"):
            return self._send(PAGE, "text/html; charset=utf-8")
        if path == "/gatherer.js":
            return self._send(open(GATHERER, "rb").read(), "text/javascript")
        if path == "/seeds.json":
            return self._send(open(os.path.join(HERE, "seeds.json"), "rb").read(), "application/json")
        self.send_response(404)
        self.end_headers()

    def do_POST(self):
        body = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
        label = "".join(c for c in body["label"] if c.isalnum() or c in "-_.")
        for r in body["runs"]:
            with open(os.path.join(RUNS, f"{label}~{r['i']}.json"), "w", encoding="utf-8") as f:
                json.dump(r, f)
        with open(os.path.join(RUNS, f"{label}-done.json"), "w", encoding="utf-8") as f:
            json.dump({"label": label, "count": len(body["runs"])}, f)
        print(label, len(body["runs"]), "runs", flush=True)
        self._send("ok", "text/plain")


if __name__ == "__main__":
    ThreadingHTTPServer(("0.0.0.0", int(sys.argv[1])), Handler).serve_forever()
