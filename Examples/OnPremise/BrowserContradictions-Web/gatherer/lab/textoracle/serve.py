"""Lab server for the text oracle.

  py -I serve.py <port>

  /gatherer/browser-tests-v3.js   the real gatherer
  /page/oracle.html               runs the gatherer's parameterised tests
                                  for every window in jobs.json
  /page/ab.html                   runs the A/B candidate variants in ab.json
  /jobs.json, /ab.json            written by jobs.py
  POST /result?label=<run>        saved to results/<run>.json
"""
import os
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

HERE = os.path.dirname(os.path.abspath(__file__))
GATHERER = os.path.join(HERE, "..", "..", "browser-tests-v3.js")


class H(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def _send(self, body, ctype, code=200):
        b = body.encode() if isinstance(body, str) else body
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(b)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(b)

    def do_GET(self):
        p = self.path.split("?")[0]
        try:
            if p == "/gatherer/browser-tests-v3.js":
                self._send(open(GATHERER, "rb").read(), "text/javascript")
            elif p.startswith("/page/") and "/" not in p[6:]:
                self._send(open(os.path.join(HERE, p[6:]), "rb").read(), "text/html")
            elif p in ("/jobs.json", "/ab.json"):
                self._send(open(os.path.join(HERE, p[1:]), "rb").read(), "application/json")
            else:
                self._send("not found", "text/plain", 404)
        except FileNotFoundError:
            self._send("not found", "text/plain", 404)

    def do_POST(self):
        q = dict(x.split("=", 1) for x in self.path.split("?", 1)[1].split("&")) if "?" in self.path else {}
        body = self.rfile.read(int(self.headers.get("Content-Length", 0)))
        os.makedirs(os.path.join(HERE, "results"), exist_ok=True)
        name = q.get("label", "x").replace("/", "_").replace("\\", "_")
        open(os.path.join(HERE, "results", name + ".json"), "wb").write(body)
        self._send("ok", "text/plain")


if __name__ == "__main__":
    ThreadingHTTPServer(("0.0.0.0", int(sys.argv[1])), H).serve_forever()
