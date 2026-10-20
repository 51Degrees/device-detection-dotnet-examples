// 51Degrees browser tests, version 3, send snippet for the packed script.
// The obfuscator's ship step joins the gatherer and this snippet, obfuscates
// them as one script and packs it into a loader (Pearl
// DeviceFingerprints BrowserScript, Packer). The loader runs the script as
// new Function("send", script)(send). This runs the gatherer under a budget
// and calls send once with the cookie value "3|id=value&id=value", URI
// encoded, or with "" when the gatherer is missing or fails. The loader
// writes the cookie under the per seed name and calls back to
// 51Degrees.core.js. Values are packed as in browser-tests-v3.snippet.js.
(function () {
    function fnv(text) {
        var h = 0x811c9dc5;
        for (var i = 0; i < text.length; i++) { h ^= text.charCodeAt(i); h = Math.imul(h, 0x01000193) >>> 0; }
        return ("00000000" + h.toString(16)).slice(-8);
    }
    function pack(r) {
        var parts = [];
        Object.keys(r.results).sort().forEach(function (n) {
            var v = r.results[n];
            if (v.length > 40) v = "h:" + fnv(v);
            parts.push(n + "=" + encodeURIComponent(v));
        });
        return encodeURIComponent("3|" + parts.join("&"));
    }
    var api = window.__51dBrowserTests;
    if (!api) { send(""); return; }
    api.run(3000).then(function (r) { send(pack(r)); }, function () { send(""); });
})();
