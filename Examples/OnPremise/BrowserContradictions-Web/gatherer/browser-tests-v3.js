// 51Degrees browser tests gatherer, version 3. Proof of concept.
//
// Gathers raw measurements only. Nothing here says what a genuine browser
// answers, the server compares each value with the baseline for the
// platform the browser claims.
//
// window.__51dBrowserTests.run(budgetMs, params) resolves to
//   { version, results: { id: value }, done: [ids], elapsed }
// Every id is "<name>.<version>" as listed in tests.json, and every value is
// a string. A test that cannot run answers "na". A test that throws answers
// "err". Synchronous tests run first, asynchronous ones race the budget, and
// whatever has finished when the budget ends is what is sent.
//
// Changes from version 2: ids carry their test version, the tests
// tests.json marks deprecated are no longer run, f_grid.1 and e_intlfmt.1
// are new, e_stackshape.2 replaces e_stackshape.1 so that renaming the
// script's functions cannot change it, and the inputs of the text and maths
// tests come from params.
//
// Final test review, 19 October 2026 (lab evidence in tests.json notes):
// c_timezone.2 checks the zone and offsets agree instead of reporting the
// visitor's zone and the zone name text, f_core.2 drops the fonts users
// install on Windows, f_grid.2 checks the 1/65536 grid on fixed inputs,
// g_canvasdeterminism.2 drops the rendered pixel hash, a_voices.2 reports
// which built in voice families exist instead of the installed voice list,
// g_renderer.2 reports the graphics backend instead of the GPU, m_tanh.2
// uses inputs that each separate Windows from Linux, m_atanh.1 is retired
// m_libm.1 is new and c_codecs.2 leaves out HEVC.
//
// Parameterised inputs. params is optional and every field defaults to the
// value version 2 used:
//   glyphs  the string f_metrics and f_grid measure
//   emoji   the glyphs f_emoji and f_grid measure
//   size    the base font size in px. f_metrics uses size for Arial, Times
//           New Roman and monospace and size * 27 / 32 for sans-serif (a
//           fractional size, 13.5 by default). f_emoji uses size * 3.
//   tanh    the inputs of m_tanh
// A parameterised test's inputs are the array listed in INPUTS below. When
// they equal the defaults the value is the bare result, exactly what version
// 2 sent under the unversioned id, so version 2 results join the ".1" rows.
// Otherwise the value is "p" + fnv1a32(JSON.stringify(inputs)) + ":" + result,
// so results from different inputs can never share a baseline row, and the
// server can work out the tag of any input set it chose.
//
// Keep this file ASCII. The JavaScript reaches the browser from a hash data
// file value, which turns every non-ASCII character into "?", so emoji and
// accented letters are written as \u escapes.
(function () {
    var SYNC = {};
    var ASYNC = {};

    var DEFAULTS = {
        glyphs: "The quick brown fox jumps over 1234567890 lazy dogs",
        emoji: ["\uD83D\uDE00", "\uD83C\uDFF3\uFE0F\u200D\uD83C\uDF08", "\uD83E\uDD14", "\u2764\uFE0F"],
        size: 16,
        // Every one of these separates Windows Chrome 148 to 156 and Edge
        // from Linux Chromium 141 and CloakBrowser 146 on its own. Found by
        // lab/maths-probe.js over 6,609 inputs: Windows tanh differs from
        // V8's fdlibm port only for |x| below about 1.9.
        tanh: [0.0390625, 0.125, 0.1875, 0.4375, 0.7, 0.8125, 0.9375, 1.078125]
    };
    // The inputs each parameterised test consumes, in tag order.
    var INPUTS = {
        "f_metrics.1": function (p) { return [p.glyphs, p.size]; },
        "f_emoji.1": function (p) { return [p.emoji, p.size]; },
        "m_tanh.2": function (p) { return [p.tanh]; }
    };
    var P = DEFAULTS;

    function s(id, f) { SYNC[id] = f; }
    function a(id, f) { ASYNC[id] = f; }
    function str(v) { return v === undefined ? "undefined" : v === null ? "null" : String(v); }
    function canvas2d() { return document.createElement("canvas").getContext("2d"); }
    function round(x) { return Number(x).toPrecision(17); }
    function vec(f, xs) { return xs.map(function (x) { return round(f(x)); }).join(","); }

    // FNV-1a 32 bit over UTF-16 code units, for long values that only need
    // to match or not, and for input tags.
    function fnv(text) {
        var h = 0x811c9dc5;
        for (var i = 0; i < text.length; i++) { h ^= text.charCodeAt(i); h = Math.imul(h, 0x01000193) >>> 0; }
        return ("00000000" + h.toString(16)).slice(-8);
    }

    // The input tag for a parameterised test, or "" for the default inputs.
    function tag(id, p) {
        var used = JSON.stringify(INPUTS[id](p));
        return used === JSON.stringify(INPUTS[id](DEFAULTS)) ? "" : "p" + fnv(used) + ":";
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

    function metricFonts(size) {
        return [size + "px Arial", size + "px 'Times New Roman'", size + "px monospace", (size * 27 / 32) + "px sans-serif"];
    }

    // ---- maths and engine -------------------------------------------------
    var MATHS_IN = [0.1, 0.7, 1.3, 2.9, 4.1, 0.0123, 7.7, 13.37];
    // Inputs where Chrome 148 and Chrome 150 or later disagree, from the lab
    // maths probe (132 such exp inputs, 151 expm1, 83 log1p).
    var MLIBM = {
        exp: [-14.867524430155754, -14.069742476567626, 1.8342713825404644],
        expm1: [-1.6784721740987152, 21.062325881794095],
        log1p: [1.5808633808279411, 4.3243418183876194]
    };
    // Windows Chrome takes tanh from the platform's C runtime and Linux
    // Chrome from V8's own fdlibm port, so the last digit differs. Version
    // 1 had one separating input of eight.
    s("m_tanh.2", function () { return tag("m_tanh.2", P) + vec(Math.tanh, P.tanh); });
    s("m_expm1.1", function () { return vec(Math.expm1, [1e-10, 0.3, 2.5, 7.7, 0.0123, 13.37]); });
    s("m_sinh.1", function () { return vec(Math.sinh, MATHS_IN); });
    s("m_cbrt.1", function () { return vec(Math.cbrt, [2, 3, 7.7, 1e-5, 123.456]); });
    s("m_log1p.1", function () { return vec(Math.log1p, [1e-9, 0.3, 2.5, 7.7, 13.37]); });
    // From Chrome 150 V8's exp, log, expm1 and log1p are correctly rounded,
    // before that they were fdlibm's. A portable engine change, so this
    // tells the engine generation, not the OS: Windows 148 answers what
    // Linux 141 and CloakBrowser 146 answer. The inputs are ones where the
    // two generations differ.
    s("m_libm.1", function () {
        return [vec(Math.exp, MLIBM.exp), vec(Math.expm1, MLIBM.expm1), vec(Math.log1p, MLIBM.log1p)].join(",");
    });
    s("m_pow.1", function () { return vec(function (x) { return Math.pow(x, 0.37); }, MATHS_IN); });
    s("m_csshypot.1", function () {
        var el = document.createElement("div");
        el.style.cssText = "position:absolute;visibility:hidden;width:calc(hypot(3.7px, 11.3px, 0.1234px) * 1000)";
        document.body.appendChild(el);
        var w = getComputedStyle(el).width;
        document.body.removeChild(el);
        return w;
    });
    s("e_errortext.1", function () { try { null.x; } catch (e) { return e.message; } return "none"; });
    // The shape of the first two stack lines with the script's own
    // identifiers taken out, so the answer survives a script whose function
    // names are renamed. A source URL becomes "URL", a dotted name such as
    // "Object.eval" or "e_stackshape.2" goes unless it is one of the
    // engine's own words (STACK_WORDS), every other number becomes "n", and
    // "@", "(", ")", ":" and "," stay. The JavaScript from the data file runs
    // through eval in 51Degrees.core.js, so in Chrome the frame there reads
    // "at (eval at anonymous (URL:n:n), anonymous:n:n)". Version 1 kept the
    // function names.
    var STACK_WORDS = { Error: 1, at: 1, eval: 1, anonymous: 1, Function: 1, line: 1, "new": 1, async: 1, native: 1 };
    s("e_stackshape.2", function () {
        var st = new Error("x").stack || "";
        return st.split("\n").slice(0, 2).map(function (l) {
            l = l.replace(/(https?|file|blob):\/\/[^\s\/)]*[^\s):]*/g, "\u0001");
            l = l.replace(/[A-Za-z_$][\w$.]*/g, function (w) { return STACK_WORDS.hasOwnProperty(w) ? w : ""; }).replace(/\d+/g, "\u0002");
            l = l.replace(/[^a-zA-Z@ ():,\u0001\u0002]/g, "").replace(/\u0001/g, "URL").replace(/\u0002/g, "n");
            return l.replace(/\s+/g, " ").replace(/\( /g, "(").trim();
        }).join("|");
    });
    s("e_features.1", function () {
        var f = [
            typeof Array.fromAsync, typeof Promise.try, typeof Iterator !== "undefined" && typeof Iterator.prototype.map,
            typeof Float16Array, typeof RegExp.escape, typeof Uint8Array.fromBase64, typeof Intl.DurationFormat,
            typeof Map.groupBy, typeof Error.isError, typeof Math.sumPrecise, typeof Atomics.pause,
            CSS.supports("text-box-trim:trim-both"), CSS.supports("anchor-name:--a"), CSS.supports("field-sizing:content"),
            CSS.supports("corner-shape:squircle"), CSS.supports("reading-flow:flex-visual")
        ];
        return f.map(function (x) { return x === true ? "1" : x === false ? "0" : x === "function" ? "f" : x === "object" ? "o" : "u"; }).join("");
    });
    s("e_icutz.1", function () { return str(Intl.supportedValuesOf("timeZone").length); });
    s("e_icucurrency.1", function () { return str(Intl.supportedValuesOf("currency").length); });
    // ICU and CLDR data version, through fixed formatting in fixed locales
    // and the UTC zone, so the visitor's own settings play no part.
    s("e_intlfmt.1", function () {
        var d = new Date(Date.UTC(2026, 0, 15, 13, 5, 9));
        return [
            new Intl.DateTimeFormat("en-US", { timeZone: "UTC", dateStyle: "full", timeStyle: "long" }).format(d),
            new Intl.NumberFormat("de-DE", { style: "currency", currency: "EUR" }).format(1234567.891),
            new Intl.RelativeTimeFormat("en", { numeric: "auto" }).format(-1, "day"),
            new Intl.ListFormat("en", { type: "unit", style: "narrow" }).format(["a", "b", "c"])
        ].join("|");
    });

    // ---- fonts and text ---------------------------------------------------
    ["menu", "caption", "status-bar", "small-caption", "message-box", "icon"].forEach(function (k) {
        s("f_kw_" + k.replace("-", "") + ".1", function () { var c = canvas2d(); c.font = k; return c.font; });
    });
    // Fonts that ship with each OS. Version 1 also listed SF Pro, Noto Sans,
    // Ubuntu and Roboto, which Windows users install themselves (Edge 155
    // in the lab reported Roboto), so a genuine Windows answer varied by
    // what the user had installed.
    s("f_core.2", function () {
        return ["Segoe UI", "Segoe UI Symbol", "Consolas", "Calibri", "Microsoft Sans Serif", "Sylfaen", "Lucida Console",
            "Helvetica Neue", "Menlo", "Geneva", "DejaVu Sans", "Liberation Sans", "Cantarell"]
            .map(fontPresent).join("");
    });
    s("f_systemui.1", function () {
        var c = canvas2d(); c.font = "72px system-ui"; var w = c.measureText("mmmmmmmmmmlliI1O0").width;
        return ["Segoe UI", "Helvetica Neue", "DejaVu Sans", "Ubuntu", "Cantarell", "Roboto", "Noto Sans"].map(function (f) {
            c.font = "72px '" + f + "'"; return c.measureText("mmmmmmmmmmlliI1O0").width === w ? "1" : "0";
        }).join("") + "|" + round(w);
    });
    s("f_emoji.1", function () {
        var c = canvas2d(); c.font = (P.size * 3) + "px sans-serif";
        return tag("f_emoji.1", P) + P.emoji.map(function (e) { return round(c.measureText(e).width); }).join(",");
    });
    s("f_metrics.1", function () {
        var c = canvas2d();
        return tag("f_metrics.1", P) + metricFonts(P.size).map(function (f) {
            c.font = f; return round(c.measureText(P.glyphs).width);
        }).join(",");
    });
    // Whether text widths sit on the grid the text stack produces. Chromium
    // shapes with HarfBuzz, whose advances are 16.16 fixed point, so every
    // genuine Chromium width is a whole multiple of 1/65536 px whatever the
    // font and size. Firefox widths are float32 values off that grid, and a
    // browser that scales widths by a seeded factor to hide its fingerprint
    // lands off both. Answers "grid", "f32" or "off". The inputs are fixed,
    // including fractional sizes, and never come from params. Version 1
    // tested 1/4096, which holds at the default sizes only.
    s("f_grid.2", function () {
        var c = canvas2d(); var ws = [];
        var text = "The quick brown fox jumps over 1234567890 lazy dogs";
        ["16px Arial", "13.5px sans-serif", "16.875px 'Times New Roman'", "11.3px monospace", "21.7px system-ui"].forEach(function (f) {
            c.font = f; ws.push(c.measureText(text).width); ws.push(c.measureText("Wj").width);
        });
        [36, 48.5].forEach(function (px) {
            c.font = px + "px sans-serif";
            DEFAULTS.emoji.forEach(function (e) { ws.push(c.measureText(e).width); });
        });
        var grid = true, f32 = true;
        ws.forEach(function (w) {
            if (w * 65536 !== Math.round(w * 65536)) grid = false;
            if (Math.fround(w) !== w) f32 = false;
        });
        return grid ? "grid" : f32 ? "f32" : "off";
    });

    // ---- graphics ---------------------------------------------------------
    function gl2() { var c = document.createElement("canvas"); return c.getContext("webgl2") || c.getContext("webgl"); }
    // The graphics backend behind WebGL, not the GPU: version 1 reported
    // the GPU model, which is the visitor's hardware. ANGLE names its
    // backend in the renderer string (Direct3D 11 on Windows, Metal on
    // macOS, OpenGL or Vulkan on Linux), SwiftShader is the software
    // fallback.
    s("g_renderer.2", function () {
        var g = gl2(); if (!g) return "na"; var d = g.getExtension("WEBGL_debug_renderer_info");
        if (!d) return "noext";
        var r = String(g.getParameter(d.UNMASKED_RENDERER_WEBGL));
        var b = /SwiftShader/i.test(r) ? "swiftshader" : /Direct3D11|D3D11/.test(r) ? "d3d11" : /Direct3D9|D3D9/.test(r) ? "d3d9" : /Metal/.test(r) ? "metal" :
            /Vulkan/.test(r) ? "vulkan" : /OpenGL/.test(r) ? "opengl" : "other";
        return (/^ANGLE \(/.test(r) ? "angle:" : "native:") + b;
    });
    // Whether reading a canvas back is honest: two reads agree, a solid
    // fill reads back exactly, and two toDataURL calls agree. Version 1 also
    // sent a hash of the rendered pixels, which depends on the GPU, the
    // font smoothing settings and, in Firefox, a per session random key.
    s("g_canvasdeterminism.2", function () {
        var c = document.createElement("canvas"); c.width = 120; c.height = 40; var x = c.getContext("2d");
        x.fillStyle = "#7f7f7f"; x.fillRect(0, 0, 120, 40); x.fillStyle = "#1a2b3c"; x.font = "18px Arial"; x.fillText("51Degrees \u00E9\u00F1 \uD83D\uDE00", 4, 26);
        var d1 = x.getImageData(0, 0, 120, 40).data; var d2 = x.getImageData(0, 0, 120, 40).data; var same = 1;
        for (var i = 0; i < d1.length; i++) { if (d1[i] !== d2[i]) { same = 0; break; } }
        var solid = 1; var d3 = x.getImageData(100, 2, 10, 6).data;
        for (var j = 0; j < d3.length; j += 4) { if (d3[j] !== 127 || d3[j + 1] !== 127 || d3[j + 2] !== 127 || d3[j + 3] !== 255) { solid = 0; break; } }
        return same + "" + solid + (c.toDataURL() === c.toDataURL() ? "1" : "0");
    });

    // ---- platform APIs ----------------------------------------------------
    s("p_apis.1", function () {
        var n = navigator;
        return [typeof n.share, typeof n.canShare, typeof n.bluetooth, typeof n.getInstalledRelatedApps, typeof window.showOpenFilePicker,
            typeof window.queryLocalFonts, typeof n.hid, typeof n.serial, typeof n.usb, typeof n.contacts, typeof window.EyeDropper,
            typeof n.windowControlsOverlay, typeof n.getBattery, typeof window.ApplePaySession, typeof n.keyboard, typeof window.chrome,
            typeof n.userAgentData, typeof n.oscpu, typeof window.DeviceOrientationEvent !== "undefined" && typeof DeviceOrientationEvent.requestPermission]
            .map(function (t) { return t === "function" ? "f" : t === "object" ? "o" : t === "undefined" ? "u" : t === false ? "0" : "x"; }).join("");
    });
    s("p_webdriver.1", function () {
        var keys = Object.keys(window).filter(function (k) { return /^(cdc_|\$cdc_|__playwright|__pw|__puppeteer|__selenium|__webdriver|__driver)/.test(k); });
        var docKeys = Object.keys(document).filter(function (k) { return /\$cdc_|\$chrome_|webdriver/.test(k); });
        return str(navigator.webdriver) + "|" + keys.length + "|" + docKeys.length;
    });
    s("p_nativefn.1", function () {
        var ok = 0, total = 0;
        [[Navigator.prototype, "userAgent"], [Navigator.prototype, "platform"], [Navigator.prototype, "hardwareConcurrency"], [Screen.prototype, "width"],
            [HTMLCanvasElement.prototype, "toDataURL"], [CanvasRenderingContext2D.prototype, "getImageData"], [WebGLRenderingContext.prototype, "getParameter"]].forEach(function (pair) {
            total++; var d = Object.getOwnPropertyDescriptor(pair[0], pair[1]); var f = d && (d.get || d.value);
            if (f && Function.prototype.toString.call(f).indexOf("[native code]") >= 0 && !Object.prototype.hasOwnProperty.call(pair[0] === Navigator.prototype ? navigator : pair[0], pair[1])) ok++;
        });
        var ts = Function.prototype.toString.call(Function.prototype.toString).indexOf("[native code]") >= 0 ? "1" : "0";
        return ok + "/" + total + "|" + ts;
    });

    // ---- identity ---------------------------------------------------------
    s("c_uach.1", function () { var u = navigator.userAgentData; return u ? u.platform + "|" + u.mobile + "|" + u.brands.map(function (b) { return b.brand + ":" + b.version; }).join(";") : "na"; });
    s("c_platform.1", function () { return navigator.platform + "|" + navigator.hardwareConcurrency + "|" + str(navigator.deviceMemory) + "|" + navigator.maxTouchPoints; });
    // Whether the zone Intl reports agrees with the offsets Date uses, in
    // January and July, and with the offset in Date.toString. Answers one
    // digit per check. Version 1 sent the zone, the offsets and the zone
    // name text: the zone is where the visitor is, not what the browser is,
    // and the name text changed within one Edge major and between Chrome
    // majors.
    s("c_timezone.2", function () {
        var tz = Intl.DateTimeFormat().resolvedOptions().timeZone; if (!tz) return "na";
        function zoneOffset(d) {
            var parts = new Intl.DateTimeFormat("en-US", { timeZone: tz, timeZoneName: "longOffset" }).formatToParts(d);
            var n = parts.filter(function (p) { return p.type === "timeZoneName"; })[0];
            var m = /GMT(?:([+-])(\d\d):(\d\d))?/.exec(n ? n.value : "");
            return !m ? NaN : !m[1] ? 0 : (m[1] === "-" ? -1 : 1) * (Number(m[2]) * 60 + Number(m[3]));
        }
        var jan = new Date(2026, 0, 15, 12), jul = new Date(2026, 6, 15, 12);
        var t = /GMT([+-])(\d\d)(\d\d)/.exec(jan.toString());
        var strOff = t ? (t[1] === "-" ? -1 : 1) * (Number(t[2]) * 60 + Number(t[3])) : NaN;
        return [zoneOffset(jan) === -jan.getTimezoneOffset(), zoneOffset(jul) === -jul.getTimezoneOffset(), strOff === -jan.getTimezoneOffset()]
            .map(function (b) { return b ? "1" : "0"; }).join("");
    });
    s("c_heap.1", function () { var m = performance.memory; return m ? str(m.jsHeapSizeLimit) : "na"; });
    // Proprietary codecs the build carries: open Chromium builds lack H.264
    // and AAC, Edge adds E-AC-3. Version 1 also asked for HEVC, which on
    // Windows depends on the GPU and the HEVC Video Extension.
    s("c_codecs.2", function () {
        var v = document.createElement("video");
        return ['video/mp4; codecs="avc1.42E01E"', 'video/webm; codecs="av01.0.05M.08"', 'audio/mp4; codecs="mp4a.40.2"', 'video/mp4; codecs="ec-3"']
            .map(function (t) { return v.canPlayType(t) || "-"; }).join(",");
    });

    // ---- asynchronous -----------------------------------------------------
    // The voice families the browser itself brings, not the voices the
    // user installed: "g" and the number of Google network voices (branded
    // Chrome), "e" and 1 when Microsoft's online voices are listed (Edge).
    // Version 1 sent the whole list, which depends on the OS language packs
    // and on when the local voices finish loading.
    a("a_voices.2", function () {
        if (!window.speechSynthesis) return Promise.resolve("na");
        return new Promise(function (res) {
            var done = false;
            function finish() {
                if (done) return; done = true;
                var vs = speechSynthesis.getVoices();
                var google = vs.filter(function (v) { return !v.localService && /^Google[ \u00A0]/.test(v.name); }).length;
                var edge = vs.some(function (v) { return !v.localService && /^Microsoft .*Online/.test(v.name); }) ? 1 : 0;
                res("g" + google + "|e" + edge);
            }
            if (speechSynthesis.getVoices().length) finish();
            speechSynthesis.onvoiceschanged = finish; setTimeout(finish, 1500);
        });
    });
    a("a_widevine.1", function () {
        if (!navigator.requestMediaKeySystemAccess) return Promise.resolve("na");
        var cfg = [{ initDataTypes: ["cenc"], videoCapabilities: [{ contentType: 'video/mp4; codecs="avc1.42E01E"', robustness: "SW_SECURE_DECODE" }] }];
        return Promise.race([
            navigator.requestMediaKeySystemAccess("com.widevine.alpha", cfg).then(function () { return "yes"; }, function (e) { return "no:" + e.name; }),
            new Promise(function (r) { setTimeout(function () { r("timeout"); }, 2500); })
        ]);
    });
    a("a_uachigh.1", function () {
        var u = navigator.userAgentData; if (!u || !u.getHighEntropyValues) return Promise.resolve("na");
        return u.getHighEntropyValues(["platform", "platformVersion", "architecture", "bitness", "wow64", "model", "uaFullVersion", "formFactors"]).then(function (h) {
            return [h.platform, h.platformVersion, h.architecture, h.bitness, h.wow64, h.model, h.uaFullVersion, (h.formFactors || []).join("/")].map(str).join("|");
        });
    });

    function run(budgetMs, params) {
        var started = performance.now();
        P = {};
        Object.keys(DEFAULTS).forEach(function (k) { P[k] = params && params[k] !== undefined ? params[k] : DEFAULTS[k]; });
        var results = {}; var done = [];
        Object.keys(SYNC).forEach(function (n) { try { results[n] = str(SYNC[n]()); } catch (e) { results[n] = "err"; } done.push(n); });
        var pending = Object.keys(ASYNC).map(function (n) {
            return Promise.resolve().then(function () { return ASYNC[n](); }).then(function (v) { results[n] = str(v); done.push(n); }, function () { results[n] = "err"; done.push(n); });
        });
        var budget = new Promise(function (r) { setTimeout(r, budgetMs || 3000); });
        return Promise.race([Promise.all(pending), budget]).then(function () {
            return { version: "3", results: results, done: done, elapsed: Math.round(performance.now() - started) };
        });
    }
    window.__51dBrowserTests = { run: run, names: Object.keys(SYNC).concat(Object.keys(ASYNC)), defaults: DEFAULTS };
})();
