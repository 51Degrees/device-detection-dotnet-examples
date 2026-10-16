// 51Degrees browser tests, version 3, data file snippet. Proof of concept.
// Runs the gatherer under a budget with its default inputs and sends the raw
// results as the 51D_BrowserTests cookie in the form "3|id=value&id=value",
// ids being versioned ("f_emoji.1"). A value longer than 40 UTF-16 code
// units is sent as h:<fnv1a32> so the cookie stays well under 4 KB, and the
// baseline hashes those values the same way.
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
    window.__51dBrowserTests.run(3000).then(function (r) {
        document.cookie = "51D_BrowserTests=" + pack(r);
        // 51D replace this comment with callback function.
    });
})();
