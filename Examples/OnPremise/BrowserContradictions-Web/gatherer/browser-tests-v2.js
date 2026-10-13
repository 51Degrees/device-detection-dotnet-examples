// 51Degrees browser tests gatherer, version 2. Proof of concept.
//
// Gathers raw measurements only. Nothing here says what a genuine browser
// answers, the server compares each value with the baseline for the
// platform the browser claims.
//
// window.__51dBrowserTests.run(budgetMs) resolves to
//   { version, results: { name: value }, done: [names], elapsed }
// Every value is a string. A test that cannot run answers "na". A test
// that throws answers "err". Synchronous tests run first, asynchronous
// ones race the budget, and whatever has finished when the budget ends is
// what is sent.
(function () {
    var SYNC = {};
    var ASYNC = {};

    function s(name, f) { SYNC[name] = f; }
    function a(name, f) { ASYNC[name] = f; }
    function str(v) { return v === undefined ? "undefined" : v === null ? "null" : String(v); }
    function canvas2d() { return document.createElement("canvas").getContext("2d"); }
    function round(x) { return Number(x).toPrecision(17); }
    function vec(f, xs) { return xs.map(function (x) { return round(f(x)); }).join(","); }

    // FNV-1a 32 bit, for long values that only need to match or not.
    function fnv(text) {
        var h = 0x811c9dc5;
        for (var i = 0; i < text.length; i++) { h ^= text.charCodeAt(i); h = Math.imul(h, 0x01000193) >>> 0; }
        return ("00000000" + h.toString(16)).slice(-8);
    }

    // Width of a string in a font, or 0 where the font is not present, by
    // comparing with the fallback.
    function fontPresent(family) {
        var c = canvas2d();
        var text = "mmmmmmmmmmlliI1O0";
        c.font = "72px monospace";
        var mono = c.measureText(text).width;
        c.font = "72px sans-serif";
        var sans = c.measureText(text).width;
        c.font = "72px '" + family + "', monospace";
        var w1 = c.measureText(text).width;
        c.font = "72px '" + family + "', sans-serif";
        var w2 = c.measureText(text).width;
        return (w1 !== mono || w2 !== sans) ? "1" : "0";
    }

    // ---- maths and engine -------------------------------------------------
    var MATHS_IN = [0.1, 0.7, 1.3, 2.9, 4.1, 0.0123, 7.7, 13.37];
    s("m_tanh", function () { return vec(Math.tanh, MATHS_IN); });
    s("m_expm1", function () { return vec(Math.expm1, [1e-10, 0.3, 2.5, 7.7, 0.0123, 13.37]); });
    s("m_sinh", function () { return vec(Math.sinh, MATHS_IN); });
    s("m_cbrt", function () { return vec(Math.cbrt, [2, 3, 7.7, 1e-5, 123.456]); });
    s("m_log1p", function () { return vec(Math.log1p, [1e-9, 0.3, 2.5, 7.7, 13.37]); });
    s("m_atanh", function () { return vec(Math.atanh, [0.1, 0.5, 0.9, 0.0123, 0.999]); });
    s("m_pow", function () { return vec(function (x) { return Math.pow(x, 0.37); }, MATHS_IN); });
    s("m_csshypot", function () {
        var el = document.createElement("div");
        el.style.cssText = "position:absolute;visibility:hidden;width:calc(hypot(3.7px, 11.3px, 0.1234px) * 1000)";
        document.body.appendChild(el);
        var w = getComputedStyle(el).width;
        document.body.removeChild(el);
        return w;
    });
    s("e_errortext", function () { try { null.x; } catch (e) { return e.message; } return "none"; });
    s("e_stackshape", function () {
        var st = new Error("x").stack || "";
        return st.split("\n").slice(0, 2).map(function (l) { return l.replace(/\(?(https?|file|blob):[^)\s]*\)?/g, "URL").replace(/[^a-zA-Z@ ():]/g, ""); }).join("|");
    });
    s("e_features", function () {
        var f = [
            typeof Array.fromAsync, typeof Promise.try, typeof Iterator !== "undefined" && typeof Iterator.prototype.map,
            typeof Float16Array, typeof RegExp.escape, typeof Uint8Array.fromBase64, typeof Intl.DurationFormat,
            typeof Map.groupBy, typeof Error.isError, typeof Math.sumPrecise, typeof Atomics.pause,
            CSS.supports("text-box-trim:trim-both"), CSS.supports("anchor-name:--a"), CSS.supports("field-sizing:content"),
            CSS.supports("corner-shape:squircle"), CSS.supports("reading-flow:flex-visual")
        ];
        return f.map(function (x) { return x === true ? "1" : x === false ? "0" : x === "function" ? "f" : x === "object" ? "o" : "u"; }).join("");
    });
    s("e_icutz", function () { return str(Intl.supportedValuesOf("timeZone").length); });
    s("e_icucurrency", function () { return str(Intl.supportedValuesOf("currency").length); });

    // ---- fonts and text ---------------------------------------------------
    ["menu", "caption", "status-bar", "small-caption", "message-box", "icon"].forEach(function (k) {
        s("f_kw_" + k.replace("-", ""), function () { var c = canvas2d(); c.font = k; return c.font; });
    });
    s("f_core", function () {
        return ["Segoe UI", "Segoe UI Symbol", "Consolas", "Calibri", "Microsoft Sans Serif", "Sylfaen", "Lucida Console",
            "Helvetica Neue", "Menlo", "SF Pro", "Geneva", "DejaVu Sans", "Liberation Sans", "Noto Sans", "Ubuntu", "Cantarell", "Roboto"]
            .map(fontPresent).join("");
    });
    s("f_systemui", function () {
        var c = canvas2d(); c.font = "72px system-ui"; var w = c.measureText("mmmmmmmmmmlliI1O0").width;
        return ["Segoe UI", "Helvetica Neue", "DejaVu Sans", "Ubuntu", "Cantarell", "Roboto", "Noto Sans"].map(function (f) {
            c.font = "72px '" + f + "'"; return c.measureText("mmmmmmmmmmlliI1O0").width === w ? "1" : "0";
        }).join("") + "|" + round(w);
    });
    s("f_emoji", function () {
        var c = canvas2d(); c.font = "48px sans-serif";
        return ["😀", "🏳️‍🌈", "🤔", "❤️"]
            .map(function (e) { return round(c.measureText(e).width); }).join(",");
    });
    s("f_metrics", function () {
        var c = canvas2d(); var t = "The quick brown fox jumps over 1234567890 lazy dogs";
        return ["16px Arial", "16px 'Times New Roman'", "16px monospace", "13.5px sans-serif"].map(function (f) {
            c.font = f; return round(c.measureText(t).width);
        }).join(",");
    });

    // ---- graphics ---------------------------------------------------------
    function gl2() { var c = document.createElement("canvas"); return c.getContext("webgl2") || c.getContext("webgl"); }
    s("g_renderer", function () {
        var g = gl2(); if (!g) return "na"; var d = g.getExtension("WEBGL_debug_renderer_info");
        return d ? g.getParameter(d.UNMASKED_VENDOR_WEBGL) + "|" + g.getParameter(d.UNMASKED_RENDERER_WEBGL) : "noext";
    });
    s("g_extensions", function () { var g = gl2(); if (!g) return "na"; var e = g.getSupportedExtensions() || []; return e.length + "|" + fnv(e.join(",")); });
    s("g_limits", function () {
        var g = gl2(); if (!g) return "na";
        var names = ["MAX_TEXTURE_SIZE", "MAX_VIEWPORT_DIMS", "MAX_RENDERBUFFER_SIZE", "MAX_VERTEX_UNIFORM_VECTORS", "MAX_FRAGMENT_UNIFORM_VECTORS",
            "MAX_VARYING_VECTORS", "MAX_VERTEX_ATTRIBS", "MAX_TEXTURE_IMAGE_UNITS", "MAX_COMBINED_TEXTURE_IMAGE_UNITS", "MAX_CUBE_MAP_TEXTURE_SIZE",
            "ALIASED_LINE_WIDTH_RANGE", "ALIASED_POINT_SIZE_RANGE", "MAX_SAMPLES", "MAX_3D_TEXTURE_SIZE", "MAX_ARRAY_TEXTURE_LAYERS",
            "MAX_COLOR_ATTACHMENTS", "MAX_DRAW_BUFFERS", "MAX_UNIFORM_BLOCK_SIZE", "MAX_ELEMENT_INDEX"];
        return names.map(function (n) { var v = g[n] !== undefined ? g.getParameter(g[n]) : null; return v && v.length ? Array.prototype.join.call(v, "/") : str(v); }).join(",");
    });
    s("g_precision", function () {
        var g = gl2(); if (!g) return "na";
        return [g.VERTEX_SHADER, g.FRAGMENT_SHADER].map(function (sh) {
            return [g.LOW_FLOAT, g.MEDIUM_FLOAT, g.HIGH_FLOAT, g.HIGH_INT].map(function (p) { var f = g.getShaderPrecisionFormat(sh, p); return f.rangeMin + "/" + f.rangeMax + "/" + f.precision; }).join(";");
        }).join("|");
    });
    s("g_samples", function () {
        var c = document.createElement("canvas"); var g = c.getContext("webgl2"); if (!g) return "na";
        var max = g.getParameter(g.MAX_SAMPLES);
        return max + "|" + ["RGBA8", "RGB565", "DEPTH_COMPONENT16", "DEPTH24_STENCIL8"].map(function (f) {
            var sm = g.getInternalformatParameter(g.RENDERBUFFER, g[f], g.SAMPLES); return Array.prototype.join.call(sm, "/");
        }).join(";");
    });
    s("g_viewportclamp", function () {
        var g = gl2(); if (!g) return "na"; var dims = g.getParameter(g.MAX_VIEWPORT_DIMS);
        g.viewport(0, 0, dims[0] * 2, dims[1] * 2); var v = g.getParameter(g.VIEWPORT);
        return (v[2] === dims[0] && v[3] === dims[1]) ? "clamped" : v[2] + "x" + v[3] + "vs" + dims[0] + "x" + dims[1];
    });
    s("g_canvasdeterminism", function () {
        var c = document.createElement("canvas"); c.width = 120; c.height = 40; var x = c.getContext("2d");
        x.fillStyle = "#7f7f7f"; x.fillRect(0, 0, 120, 40); x.fillStyle = "#1a2b3c"; x.font = "18px Arial"; x.fillText("51Degrees éñ 😀", 4, 26);
        var d1 = x.getImageData(0, 0, 120, 40).data; var d2 = x.getImageData(0, 0, 120, 40).data; var same = 1;
        for (var i = 0; i < d1.length; i += 97) { if (d1[i] !== d2[i]) { same = 0; break; } }
        var solid = 1; var d3 = x.getImageData(100, 2, 10, 6).data;
        for (var j = 0; j < d3.length; j += 4) { if (d3[j] !== 127 || d3[j + 1] !== 127 || d3[j + 2] !== 127) { solid = 0; break; } }
        return same + "" + solid + "|" + fnv(c.toDataURL()) + "|" + fnv(c.toDataURL());
    });

    // ---- platform APIs ----------------------------------------------------
    s("p_apis", function () {
        var n = navigator;
        return [typeof n.share, typeof n.canShare, typeof n.bluetooth, typeof n.getInstalledRelatedApps, typeof window.showOpenFilePicker,
            typeof window.queryLocalFonts, typeof n.hid, typeof n.serial, typeof n.usb, typeof n.contacts, typeof window.EyeDropper,
            typeof n.windowControlsOverlay, typeof n.getBattery, typeof window.ApplePaySession, typeof n.keyboard, typeof window.chrome,
            typeof n.userAgentData, typeof n.oscpu, typeof window.DeviceOrientationEvent !== "undefined" && typeof DeviceOrientationEvent.requestPermission]
            .map(function (t) { return t === "function" ? "f" : t === "object" ? "o" : t === "undefined" ? "u" : t === false ? "0" : "x"; }).join("");
    });
    s("p_webdriver", function () {
        var keys = Object.keys(window).filter(function (k) { return /^(cdc_|\$cdc_|__playwright|__pw|__puppeteer|__selenium|__webdriver|__driver)/.test(k); });
        var docKeys = Object.keys(document).filter(function (k) { return /\$cdc_|\$chrome_|webdriver/.test(k); });
        return str(navigator.webdriver) + "|" + keys.length + "|" + docKeys.length;
    });
    s("p_nativefn", function () {
        var ok = 0, total = 0;
        [[Navigator.prototype, "userAgent"], [Navigator.prototype, "platform"], [Navigator.prototype, "hardwareConcurrency"], [Screen.prototype, "width"],
            [HTMLCanvasElement.prototype, "toDataURL"], [CanvasRenderingContext2D.prototype, "getImageData"], [WebGLRenderingContext.prototype, "getParameter"]].forEach(function (pair) {
            total++; var d = Object.getOwnPropertyDescriptor(pair[0], pair[1]); var f = d && (d.get || d.value);
            if (f && Function.prototype.toString.call(f).indexOf("[native code]") >= 0 && !Object.prototype.hasOwnProperty.call(pair[0] === Navigator.prototype ? navigator : pair[0], pair[1])) ok++;
        });
        var ts = Function.prototype.toString.call(Function.prototype.toString).indexOf("[native code]") >= 0 ? "1" : "0";
        return ok + "/" + total + "|" + ts;
    });

    // ---- consistency and geometry ----------------------------------------
    s("c_uach", function () { var u = navigator.userAgentData; return u ? u.platform + "|" + u.mobile + "|" + u.brands.map(function (b) { return b.brand + ":" + b.version; }).join(";") : "na"; });
    s("c_platform", function () { return navigator.platform + "|" + navigator.hardwareConcurrency + "|" + str(navigator.deviceMemory) + "|" + navigator.maxTouchPoints; });
    s("c_timezone", function () {
        var tz = Intl.DateTimeFormat().resolvedOptions().timeZone; var jan = new Date(2026, 0, 15).getTimezoneOffset(); var jul = new Date(2026, 6, 15).getTimezoneOffset();
        var name = (new Date(2026, 0, 15).toString().match(/\(([^)]+)\)/) || ["", ""])[1];
        return tz + "|" + jan + "|" + jul + "|" + name;
    });
    s("c_geometry", function () {
        var sc = screen; var fits = (window.outerWidth <= sc.availWidth && window.outerHeight <= sc.availHeight) ? "1" : "0";
        var chrome = window.outerHeight - window.innerHeight;
        return fits + "|" + (sc.height - sc.availHeight) + "|" + sc.availTop + "|" + sc.availLeft + "|" + sc.colorDepth + "|" + sc.pixelDepth + "|" + (chrome >= 0 ? "chrome" : "neg") + "|" + str(sc.isExtended);
    });
    s("c_syscolors", function () {
        var el = document.createElement("div"); el.style.cssText = "position:absolute;visibility:hidden"; document.body.appendChild(el);
        var out = ["AccentColor", "AccentColorText", "ButtonFace", "Highlight", "SelectedItem", "Canvas", "LinkText"].map(function (c) {
            el.style.color = c; return getComputedStyle(el).color;
        }).join(";"); document.body.removeChild(el); return out;
    });
    s("c_plugins", function () {
        var p = navigator.plugins; var names = []; for (var i = 0; i < p.length; i++) names.push(p[i].name);
        return p.length + "|" + str(navigator.pdfViewerEnabled) + "|" + fnv(names.join(","));
    });
    s("c_lang", function () { return navigator.language + "|" + (navigator.languages || []).join(",") + "|" + Intl.DateTimeFormat().resolvedOptions().locale; });
    s("c_scrollbar", function () {
        var el = document.createElement("div"); el.style.cssText = "position:absolute;visibility:hidden;width:100px;height:100px;overflow:scroll";
        document.body.appendChild(el); var w = el.offsetWidth - el.clientWidth; document.body.removeChild(el); return str(w);
    });
    s("c_media", function () {
        return ["(pointer:coarse)", "(pointer:fine)", "(hover:none)", "(color-gamut:p3)", "(dynamic-range:high)", "(prefers-reduced-motion:reduce)", "(forced-colors:active)"]
            .map(function (q) { return matchMedia(q).matches ? "1" : "0"; }).join("");
    });
    s("c_heap", function () { var m = performance.memory; return m ? str(m.jsHeapSizeLimit) : "na"; });
    s("c_timer", function () {
        // Bounded by spins as well as by distinct values, because a coarse
        // or frozen timer would otherwise never let the loop end.
        var seen = {}; var last = performance.now(); var n = 0; var spins = 0;
        while (n < 400 && spins++ < 200000) { var t = performance.now(); if (t !== last) { seen[(t - last).toFixed(4)] = 1; last = t; n++; } }
        var ks = Object.keys(seen).map(Number).sort(function (x, y) { return x - y; }); return ks.slice(0, 3).join(",") + "|" + ks.length;
    });
    s("c_codecs", function () {
        var v = document.createElement("video");
        return ['video/mp4; codecs="avc1.42E01E"', 'video/mp4; codecs="hvc1.1.6.L93.B0"', 'video/webm; codecs="av01.0.05M.08"', 'audio/mp4; codecs="mp4a.40.2"', 'video/mp4; codecs="ec-3"']
            .map(function (t) { return v.canPlayType(t) || "-"; }).join(",");
    });

    // ---- asynchronous -----------------------------------------------------
    a("a_webgpu", function () {
        if (!navigator.gpu) return Promise.resolve("na");
        return navigator.gpu.requestAdapter().then(function (ad) {
            if (!ad) return "noadapter";
            var f = []; ad.features.forEach(function (x) { f.push(x); }); f.sort();
            var L = ad.limits; var lim = ["maxTextureDimension2D", "maxBufferSize", "maxStorageBufferBindingSize", "maxComputeWorkgroupSizeX", "maxBindGroups", "maxColorAttachmentBytesPerSample"]
                .map(function (k) { return str(L[k]); }).join("/");
            var info = ad.info ? ad.info.vendor + ":" + ad.info.architecture : "noinfo";
            return navigator.gpu.getPreferredCanvasFormat() + "|" + str(ad.isFallbackAdapter) + "|" + info + "|" + f.length + ":" + fnv(f.join(",")) + "|" + lim;
        });
    });
    a("a_webgpu_fallback", function () {
        if (!navigator.gpu) return Promise.resolve("na");
        return navigator.gpu.requestAdapter({ forceFallbackAdapter: true }).then(function (ad) { return ad ? str(ad.isFallbackAdapter) : "none"; });
    });
    a("a_voices", function () {
        if (!window.speechSynthesis) return Promise.resolve("na");
        return new Promise(function (res) {
            var done = false;
            function finish() {
                if (done) return; done = true;
                var vs = speechSynthesis.getVoices().map(function (v) { return v.name + "/" + v.lang + "/" + (v.localService ? "L" : "R"); }).sort();
                res(vs.length + "|" + fnv(vs.join(";")) + "|" + vs.slice(0, 3).join(";"));
            }
            if (speechSynthesis.getVoices().length) finish();
            speechSynthesis.onvoiceschanged = finish; setTimeout(finish, 1500);
        });
    });
    a("a_devices", function () {
        if (!navigator.mediaDevices || !navigator.mediaDevices.enumerateDevices) return Promise.resolve("na");
        return navigator.mediaDevices.enumerateDevices().then(function (ds) {
            var kinds = {}; var labels = 0; var ids = 0;
            ds.forEach(function (d) { kinds[d.kind] = (kinds[d.kind] || 0) + 1; if (d.label) labels++; if (d.deviceId) ids++; });
            return Object.keys(kinds).sort().map(function (k) { return k + ":" + kinds[k]; }).join(",") + "|labels" + labels + "|ids" + ids;
        });
    });
    a("a_widevine", function () {
        if (!navigator.requestMediaKeySystemAccess) return Promise.resolve("na");
        var cfg = [{ initDataTypes: ["cenc"], videoCapabilities: [{ contentType: 'video/mp4; codecs="avc1.42E01E"', robustness: "SW_SECURE_DECODE" }] }];
        return Promise.race([
            navigator.requestMediaKeySystemAccess("com.widevine.alpha", cfg).then(function () { return "yes"; }, function (e) { return "no:" + e.name; }),
            new Promise(function (r) { setTimeout(function () { r("timeout"); }, 2500); })
        ]);
    });
    a("a_uachigh", function () {
        var u = navigator.userAgentData; if (!u || !u.getHighEntropyValues) return Promise.resolve("na");
        return u.getHighEntropyValues(["platform", "platformVersion", "architecture", "bitness", "wow64", "model", "uaFullVersion", "formFactors"]).then(function (h) {
            return [h.platform, h.platformVersion, h.architecture, h.bitness, h.wow64, h.model, h.uaFullVersion, (h.formFactors || []).join("/")].map(str).join("|");
        });
    });
    a("a_worker", function () {
        if (!window.Worker || !window.Blob) return Promise.resolve("na");
        var code = "self.onmessage=function(){var t=[0.1,0.7,1.3,2.9,4.1].map(function(x){return Math.tanh(x).toPrecision(17)}).join(',');" +
            "var g=null;try{var c=new OffscreenCanvas(8,8);var w=c.getContext('webgl2')||c.getContext('webgl');var d=w&&w.getExtension('WEBGL_debug_renderer_info');g=d?w.getParameter(d.UNMASKED_RENDERER_WEBGL):'noext'}catch(e){g='err'}" +
            "postMessage(JSON.stringify({ua:navigator.userAgent,pl:navigator.platform,hc:navigator.hardwareConcurrency,lang:navigator.language,tz:Intl.DateTimeFormat().resolvedOptions().timeZone,t:t,g:g,uad:typeof navigator.userAgentData}))}";
        return new Promise(function (res) {
            try {
                var w = new Worker(URL.createObjectURL(new Blob([code], { type: "text/javascript" })));
                w.onmessage = function (e) {
                    w.terminate(); var r = JSON.parse(e.data);
                    var mainT = [0.1, 0.7, 1.3, 2.9, 4.1].map(function (x) { return Math.tanh(x).toPrecision(17); }).join(",");
                    var parity = [r.ua === navigator.userAgent, r.pl === navigator.platform, r.hc === navigator.hardwareConcurrency, r.lang === navigator.language,
                        r.tz === Intl.DateTimeFormat().resolvedOptions().timeZone, r.t === mainT, r.uad === typeof navigator.userAgentData].map(function (b) { return b ? "1" : "0"; }).join("");
                    res(parity + "|" + r.g);
                }; w.onerror = function () { res("err"); }; w.postMessage(1);
                setTimeout(function () { res("timeout"); }, 2000);
            } catch (e) { res("err"); }
        });
    });
    a("a_rtc", function () {
        if (!window.RTCPeerConnection) return Promise.resolve("na");
        return new Promise(function (res) {
            var pc = new RTCPeerConnection({ iceServers: [] }); var hosts = 0, mdns = 0, done = false;
            function finish() { if (!done) { done = true; try { pc.close(); } catch (e) { } res(hosts + "|" + mdns); } }
            pc.onicecandidate = function (e) { if (!e.candidate) { finish(); return; } hosts++; if (/\.local/.test(e.candidate.candidate)) mdns++; };
            pc.createDataChannel("x"); pc.createOffer().then(function (o) { return pc.setLocalDescription(o); }).catch(function () { finish(); });
            setTimeout(finish, 2000);
        });
    });
    a("a_keyboard", function () {
        if (!navigator.keyboard || !navigator.keyboard.getLayoutMap) return Promise.resolve("na");
        return navigator.keyboard.getLayoutMap().then(function (m) { return ["KeyQ", "KeyY", "KeyZ", "Backslash", "IntlBackslash", "Semicolon"].map(function (k) { return str(m.get(k)); }).join(","); });
    });

    function run(budgetMs) {
        var started = performance.now();
        var results = {}; var done = [];
        Object.keys(SYNC).forEach(function (n) { try { results[n] = str(SYNC[n]()); } catch (e) { results[n] = "err"; } done.push(n); });
        var pending = Object.keys(ASYNC).map(function (n) {
            return Promise.resolve().then(function () { return ASYNC[n](); }).then(function (v) { results[n] = str(v); done.push(n); }, function () { results[n] = "err"; done.push(n); });
        });
        var budget = new Promise(function (r) { setTimeout(r, budgetMs || 3000); });
        return Promise.race([Promise.all(pending), budget]).then(function () {
            return { version: "2", results: results, done: done, elapsed: Math.round(performance.now() - started) };
        });
    }
    window.__51dBrowserTests = { run: run, names: Object.keys(SYNC).concat(Object.keys(ASYNC)) };
})();
