"""Maths library lab: which tanh / atanh does each browser use, and can a
server predict it exactly. Lab notes for m_tanhwalk.1, 23 October 2026.

  py -I mathlab.py inputs           write inputs.json (fixed seed)
  py -I mathlab.py serve <port>     serve the page, store POSTed results in results/
                                    /                  single input capture
                                    /page/walk.html    the gatherer's m_tanhwalk.1
                                                       for every seed in seeds.json
                                    open with ?label=<run name>; math-run.js drives
                                    Linux Chromium and CloakBrowser from WSL
  py -I mathlab.py refs             write refs/cr.json (correctly rounded, mpmath)
                                    and refs/ucrt.json (this machine's Python, UCRT)
  py -I mathlab.py compare          compare every results/*.json with every ref
"""
import json
import math
import os
import random
import struct
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

HERE = os.path.dirname(os.path.abspath(__file__))


def hexbits(x):
    return struct.pack(">d", x).hex()


def frombits(h):
    return struct.unpack(">d", bytes.fromhex(h))[0]


def make_inputs():
    r = random.Random(51)
    tanh = ([r.uniform(-3, 3) for _ in range(2500)] +
            [r.uniform(-1e-3, 1e-3) for _ in range(300)] +
            [r.uniform(0.5, 1.0) for _ in range(400)] +
            [r.uniform(3, 19) for _ in range(300)] +
            [r.uniform(-0.05, 0.05) for _ in range(500)])
    atanh = ([r.uniform(-0.999, 0.999) for _ in range(2500)] +
             [r.uniform(-1e-3, 1e-3) for _ in range(300)] +
             [r.uniform(0.9, 0.99999) for _ in range(700)] +
             [r.uniform(-0.05, 0.05) for _ in range(500)])
    return {"tanh": [hexbits(x) for x in tanh], "atanh": [hexbits(x) for x in atanh]}


PAGE = """<!doctype html><meta charset=utf-8><title>mathlab</title><pre id=o>running</pre>
<script>
(async function () {
  var label = new URLSearchParams(location.search).get('label') || 'x';
  var inp = await (await fetch('/inputs.json')).json();
  var dv = new DataView(new ArrayBuffer(8));
  function fromHex(h) { for (var i = 0; i < 8; i++) dv.setUint8(i, parseInt(h.substr(i * 2, 2), 16)); return dv.getFloat64(0); }
  function toHex(x) { dv.setFloat64(0, x); var s = ''; for (var i = 0; i < 8; i++) s += ('0' + dv.getUint8(i).toString(16)).slice(-2); return s; }
  var out = { label: label, ua: navigator.userAgent, tanh: [], atanh: [] };
  inp.tanh.forEach(function (h) { out.tanh.push(toHex(Math.tanh(fromHex(h)))); });
  inp.atanh.forEach(function (h) { out.atanh.push(toHex(Math.atanh(fromHex(h)))); });
  await fetch('/result?label=' + encodeURIComponent(label), { method: 'POST', body: JSON.stringify(out) });
  document.getElementById('o').textContent = 'done'; document.title = 'done';
})();
</script>"""


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def _send(self, body, ctype):
        b = body.encode() if isinstance(body, str) else body
        self.send_response(200)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(b)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(b)

    def do_GET(self):
        path = self.path.split("?")[0]
        if path == "/inputs.json":
            self._send(open(os.path.join(HERE, "inputs.json"), "rb").read(), "application/json")
        elif path == "/gatherer/browser-tests-v3.js":
            self._send(open(os.path.join(HERE, "..", "..", "browser-tests-v3.js"), "rb").read(), "text/javascript")
        elif path.startswith("/js/"):
            self._send(open(os.path.join(HERE, path[4:]), "rb").read(), "text/javascript")
        elif path.startswith("/page/"):
            self._send(open(os.path.join(HERE, path[6:]), "rb").read(), "text/html")
        else:
            self._send(PAGE, "text/html")

    def do_POST(self):
        q = dict(p.split("=", 1) for p in self.path.split("?", 1)[1].split("&")) if "?" in self.path else {}
        n = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(n)
        os.makedirs(os.path.join(HERE, "results"), exist_ok=True)
        name = q.get("label", "x").replace("/", "_")
        open(os.path.join(HERE, "results", name + ".json"), "wb").write(body)
        self._send("ok", "text/plain")


def refs():
    import mpmath
    mpmath.mp.prec = 200
    inp = json.load(open(os.path.join(HERE, "inputs.json")))
    os.makedirs(os.path.join(HERE, "refs"), exist_ok=True)

    def cr(f, h):
        x = frombits(h)
        return hexbits(float(f(mpmath.mpf(x))))

    json.dump({"tanh": [cr(mpmath.tanh, h) for h in inp["tanh"]],
               "atanh": [cr(mpmath.atanh, h) for h in inp["atanh"]]},
              open(os.path.join(HERE, "refs", "cr.json"), "w"))
    # Python on Windows calls the system UCRT tanh/atanh.
    json.dump({"tanh": [hexbits(math.tanh(frombits(h))) for h in inp["tanh"]],
               "atanh": [hexbits(math.atanh(frombits(h))) for h in inp["atanh"]]},
              open(os.path.join(HERE, "refs", "ucrt.json"), "w"))
    print("refs written")


def compare():
    inp = json.load(open(os.path.join(HERE, "inputs.json")))
    refs_ = {os.path.splitext(f)[0]: json.load(open(os.path.join(HERE, "refs", f)))
             for f in sorted(os.listdir(os.path.join(HERE, "refs")))}
    res = {os.path.splitext(f)[0]: json.load(open(os.path.join(HERE, "results", f)))
           for f in sorted(os.listdir(os.path.join(HERE, "results"))) if not f.startswith("walk-")}
    allsets = dict(refs_)
    allsets.update(res)
    names = list(allsets)
    for fn in ("tanh", "atanh"):
        n = len(inp[fn])
        print(f"\n{fn}: mismatches out of {n} (row vs column)")
        print(" " * 22 + "".join(f"{c[:10]:>11}" for c in names))
        for a in names:
            row = []
            for b in names:
                d = sum(1 for x, y in zip(allsets[a][fn], allsets[b][fn]) if x != y)
                row.append(f"{d:>11}")
            print(f"{a[:22]:<22}" + "".join(row))
    # ulp sizes of the differences against cr
    for name, r in res.items():
        for fn in ("tanh", "atanh"):
            diffs = [abs(struct.unpack('>q', bytes.fromhex(x))[0] - struct.unpack('>q', bytes.fromhex(y))[0])
                     for x, y in zip(r[fn], refs_["cr"][fn]) if x != y]
            if diffs:
                print(f"{name} {fn} vs cr: {len(diffs)} differ, max {max(diffs)} ulp")


if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "inputs":
        json.dump(make_inputs(), open(os.path.join(HERE, "inputs.json"), "w"))
        print("inputs written")
    elif cmd == "serve":
        ThreadingHTTPServer(("0.0.0.0", int(sys.argv[2])), Handler).serve_forever()
    elif cmd == "refs":
        refs()
    elif cmd == "compare":
        compare()
