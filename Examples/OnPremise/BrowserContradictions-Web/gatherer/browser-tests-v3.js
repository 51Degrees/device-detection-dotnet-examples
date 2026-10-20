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
// "err". A test whose answer would depend on timing, loading or the site
// (a late or blocked worker, a voice list still loading) answers null and
// is left out of results, as is any test still running when the budget
// ends: the element skips a test that is absent. Synchronous tests run
// first, asynchronous ones race the budget.
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
// Wave 3, 20 October 2026: the candidate tests the lab promoted join here
// (platform font code, genuine Microsoft font files, worker realms, the
// platform AAC encoder, Secure Payment Confirmation, system colours and
// automation residue), with the lab's per major and supporting candidates
// as collecting tests. c_screen800.1 is the 800 x 600 rule of
// c_winframe.1 on its own. p_rtdebug.1 (Runtime domain residue) and
// e_wkdepth.1 (worker recursion depth, per V8 build) come from the timing
// study. f_segoevar.3 drops the Display family. Tests that need a service
// worker file on the site's own origin run only when params.swUrl names
// it. The synchronous tests now run in slices with a yield between them,
// after the asynchronous ones have started (see run).
//
// Final hardening, 20 October 2026, against false positives on genuine
// browsers started from clean: a_voices.3 reports the platform's local
// voices instead of the Google network voices, which a fresh profile can
// lack for many seconds (a_voices.2 is retired, a_voices.3 is collecting
// since it catches nothing, so a_widevine.1 alone tells Chrome for
// Testing from branded Chrome). p_recorder.2 drops HEVC,
// which depends on the GPU. A test whose answer depends on timing, loading
// or the site now answers nothing (null) instead of "timeout" or a guess:
// late answers, workers a Content Security Policy blocks, every key system
// switched off (a_widevine.1 checks Clear Key), forced colours, and pages a
// service worker, prefetch or cache delivered (p_navhop.2). Pixel tests ask
// for a software canvas. s_offscreen.1 and s_uach.2 come from the stealth
// study as collecting tests.
//
// run(budgetMs, params) also takes, beside the inputs below:
//   swUrl   the URL of browser-tests-sw.js on the page's own origin, which
//           turns on the service worker tests
//   only    an array of ids to run, for the lab
//   timing  true adds { id: ms } as "timing" to the answer, for the lab
//
// Parameterised inputs. params is optional and every field defaults to the
// value version 2 used:
//   glyphs  the string f_metrics and f_grid measure
//   emoji   the glyphs f_emoji and f_grid measure
//   size    the base font size in px. f_metrics uses size for Arial, Times
//           New Roman and monospace and size * 27 / 32 for sans-serif (a
//           fractional size, 13.5 by default). f_emoji uses size * 3.
//   tanh    the inputs of m_tanh
// A parameterised test's inputs are the array its inputs function below
// returns (metricsInputs, emojiInputs, tanhInputs). When
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
    // The inputs each parameterised test consumes, in tag order. Plain
    // functions rather than a table keyed by test id, so no test id is an
    // object key the obfuscator would leave readable.
    function metricsInputs(p) { return [p.glyphs, p.size]; }
    function emojiInputs(p) { return [p.emoji, p.size]; }
    function tanhInputs(p) { return [p.tanh]; }
    var P = DEFAULTS;

    function s(id, f) { SYNC[id] = f; }
    function a(id, f) { ASYNC[id] = f; }
    function str(v) { return v === undefined ? "undefined" : v === null ? "null" : String(v); }
    // A 2D context. Text measurement does not depend on the canvas size, so
    // only the tests that read pixels give one.
    // The tests that read pixels ask for willReadFrequently, which keeps the
    // canvas in software: a GPU canvas rasterises text with different
    // antialiasing, and the lab VM has no GPU while most visitors do.
    function canvas2d(w, h) { var c = document.createElement("canvas"); if (w) { c.width = w; c.height = h; return c.getContext("2d", { willReadFrequently: true }); } return c.getContext("2d"); }
    function r3(x) { return String(Math.round(x * 1000) / 1000); }
    // A test that answers null (or undefined) has no answer: run leaves it
    // out of the results, and the element skips a test that is absent.
    // Anything that depends on timing or loading answers null rather than
    // guess, so a slow, busy or hidden page is never read as a different
    // browser. timeout gives null when the work is not done in time.
    var LATE = {};
    function timeout(p, ms) {
        return Promise.race([p, new Promise(function (r) { setTimeout(function () { r(LATE); }, ms); })])
            .then(function (v) { return v === LATE ? null : v; });
    }
    // Tests that need a service worker file, run only when params.swUrl is
    // set (see run).
    var SERVICE = {};
    function sw(id, f) { SERVICE[id] = f; }
    var OPT = {};
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
    function tag(inputs, p) {
        var used = JSON.stringify(inputs(p));
        return used === JSON.stringify(inputs(DEFAULTS)) ? "" : "p" + fnv(used) + ":";
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
    s("m_tanh.2", function () { return tag(tanhInputs, P) + vec(Math.tanh, P.tanh); });
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
        return tag(emojiInputs, P) + P.emoji.map(function (e) { return round(c.measureText(e).width); }).join(",");
    });
    s("f_metrics.1", function () {
        var c = canvas2d();
        return tag(metricsInputs, P) + metricFonts(P.size).map(function (f) {
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
    // Collecting from 20 October 2026: V8 sizes the heap from the physical
    // memory (a quarter of it, up to the cap), so a genuine browser on a
    // machine with less than 16 GB answers a smaller limit than the lab.
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
    // The platform's own speech voices: "m1" when a local (localService)
    // voice is a Microsoft one, "m0" when local voices are listed and none
    // is. Windows always lists its SAPI voices (which ones depends on the
    // language packs, so only the vendor is reported). Version 2 counted
    // the Google network voices, which a fresh Chrome profile sometimes
    // lists only after 12 s or more. Voices load asynchronously and the
    // network ones arrive on their own, so this answers "m1" as soon as a
    // local Microsoft voice is listed and gives no answer while no local
    // voice is: such a list may still be loading. "m0" needs local voices
    // and no Microsoft one for a further 2 s, since an extension's local
    // voices could be listed before the platform's.
    a("a_voices.3", function () {
        if (!window.speechSynthesis || !speechSynthesis.getVoices) return Promise.resolve("na");
        return new Promise(function (res) {
            var timer = 0, polls = 0, other = 0;
            function stop(v) { clearInterval(timer); speechSynthesis.removeEventListener("voiceschanged", check); res(v); }
            function check() {
                var local = speechSynthesis.getVoices().filter(function (v) { return v.localService; });
                if (local.some(function (v) { return /^Microsoft /.test(v.name); })) return stop("m1");
                if (local.length && !other) other = performance.now();
                if (other && performance.now() - other >= 2000) return stop("m0");
                if (++polls > 60) stop(null);
            }
            speechSynthesis.addEventListener("voiceschanged", check);
            timer = setInterval(check, 500);
            check();
        });
    });
    // Whether the browser carries the Widevine CDM. Branded Chrome and Edge
    // bundle it, Chrome for Testing and open Chromium builds do not. A
    // refusal counts only when Clear Key, which every build has, still
    // works: a browser with every key system switched off (Firefox with DRM
    // off) gives no answer, as does a late answer. Chrome's own "Sites can
    // play protected content" setting refuses Widevine alone, with the same
    // error as a build without it, so a user who switched it off reads as
    // Chrome for Testing (lab, 20 October 2026), a false positive no page
    // can avoid without losing the Chrome for Testing catch.
    a("a_widevine.1", function () {
        if (!navigator.requestMediaKeySystemAccess) return Promise.resolve("na");
        function ask(system, cfg) {
            return navigator.requestMediaKeySystemAccess(system, cfg).then(function () { return "yes"; }, function (e) { return "no:" + e.name; });
        }
        return timeout(ask("com.widevine.alpha", [{ initDataTypes: ["cenc"], videoCapabilities: [{ contentType: 'video/mp4; codecs="avc1.42E01E"', robustness: "SW_SECURE_DECODE" }] }])
            .then(function (v) {
                if (v === "yes") return v;
                return ask("org.w3.clearkey", [{ initDataTypes: ["webm"], videoCapabilities: [{ contentType: 'video/webm; codecs="vp8"' }] }])
                    .then(function (c) { return c === "yes" ? v : null; });
            }), 2500);
    });
    a("a_uachigh.1", function () {
        var u = navigator.userAgentData; if (!u || !u.getHighEntropyValues) return Promise.resolve("na");
        return u.getHighEntropyValues(["platform", "platformVersion", "architecture", "bitness", "wow64", "model", "uaFullVersion", "formFactors"]).then(function (h) {
            return [h.platform, h.platformVersion, h.architecture, h.bitness, h.wow64, h.model, h.uaFullVersion, (h.formFactors || []).join("/")].map(str).join("|");
        });
    });

    // ==== wave 3 tests (lab evidence in tests.json notes) ==================

    // A hidden box for DOM text measurements, out of view and out of the
    // flow. run() takes it out of the page once the synchronous tests ran.
    var lab = null;
    function box() {
        if (!lab || !lab.parentNode) {
            lab = document.createElement("div");
            lab.style.cssText = "position:absolute;left:-10000px;top:0;visibility:hidden;white-space:nowrap;contain:layout";
            document.body.appendChild(lab);
        }
        return lab;
    }
    function spanW(text, css) {
        var b = box(); var sp = document.createElement("span"); sp.style.cssText = css; sp.textContent = text;
        b.appendChild(sp); var w = sp.getBoundingClientRect().width; b.removeChild(sp); return w;
    }
    var NOFONT = "'zz-none-zz'";

    // ---- worker realms ----------------------------------------------------
    // realmSig runs in the page and, sent as source text, in a dedicated
    // worker, a shared worker and a service worker. It uses nothing but its
    // arguments: g is the realm's global object and K the strings it needs,
    // sent along with the source. An obfuscated script renames the script's
    // own names and moves string literals into a table no worker has, so
    // the function must not name either. Answers a list of fields: the
    // first REPLAY_FIELDS are compared between realms, the rest are generic
    // family widths from OffscreenCanvas.
    var REALM_K = ["2d", "40px ", "mmmiii@@WW", "noc", "nouad", "/", ";", ":", "Arial", "'Times New Roman'",
        "serif", "sans-serif", "monospace", "cursive", "fantasy", "system-ui", "math"];
    function realmSig(g, K) {
        var n = g.navigator, c = null, out = [], u = n.userAgentData;
        try { c = new g.OffscreenCanvas(10, 10).getContext(K[0]); } catch (e) { c = null; }
        function w(f) { if (!c) return K[3]; c.font = K[1] + f; return String(c.measureText(K[2]).width); }
        out.push(n.userAgent, n.platform, String(n.hardwareConcurrency), String(n.deviceMemory), (n.languages || []).join(K[5]), n.language,
            g.Intl.DateTimeFormat().resolvedOptions().timeZone, String(new g.Date(2026, 0, 15).getTimezoneOffset()),
            g.Math.tanh(0.7).toPrecision(17), g.Math.atanh(0.5).toPrecision(17), g.Math.expm1(0.3).toPrecision(17),
            w(K[8]), w(K[9]),
            u ? u.platform + K[5] + u.mobile + K[5] + u.brands.map(function (b) { return b.brand + K[7] + b.version; }).join(K[6]) : K[4],
            w(K[10]), w(K[11]), w(K[12]), w(K[13]), w(K[14]), w(K[15]), w(K[16]));
        return out;
    }
    var REPLAY_FIELDS = 14;
    var GENERIC_FROM = 14;

    function blobUrl(src) { return URL.createObjectURL(new Blob([src], { type: "text/javascript" })); }
    var realmCache = {};
    function dedicatedRealm() {
        if (realmCache.dw) return realmCache.dw;
        realmCache.dw = new Promise(function (res) {
            if (typeof Worker === "undefined") return res("na");
            try {
                var url = blobUrl("onmessage=function(e){postMessage((" + realmSig.toString() + ")(self,e.data))}");
                var w = new Worker(url);
                w.onmessage = function (e) { res(e.data); w.terminate(); URL.revokeObjectURL(url); };
                w.onerror = function () { res(null); };
                w.postMessage(REALM_K);
            } catch (e) { res(null); }
        });
        return realmCache.dw;
    }
    function sharedRealm() {
        if (realmCache.sh) return realmCache.sh;
        realmCache.sh = new Promise(function (res) {
            if (typeof SharedWorker === "undefined") return res("na");
            try {
                var url = blobUrl("onconnect=function(e){var p=e.ports[0];p.onmessage=function(m){p.postMessage((" + realmSig.toString() + ")(self,m.data))}}");
                var w = new SharedWorker(url);
                w.port.onmessage = function (e) { res(e.data); w.port.close(); URL.revokeObjectURL(url); };
                w.onerror = function () { res(null); };
                w.port.postMessage(REALM_K);
            } catch (e) { res(null); }
        });
        return realmCache.sh;
    }
    // The service worker file (browser-tests-sw.js) evaluates the source it
    // is sent, so the test logic stays here and the file never changes.
    function serviceRealm() {
        if (realmCache.svc) return realmCache.svc;
        realmCache.svc = new Promise(function (res) {
            if (!OPT.swUrl || !navigator.serviceWorker) return res("na");
            navigator.serviceWorker.register(OPT.swUrl).then(function (reg) {
                var worker = reg.active || reg.waiting || reg.installing;
                var ch = new MessageChannel();
                ch.port1.onmessage = function (e) { res(e.data); };
                function go() { (reg.active || worker).postMessage({ src: realmSig.toString(), k: REALM_K }, [ch.port2]); }
                if (reg.active) go(); else worker.addEventListener("statechange", function () { if (worker.state === "activated") go(); });
            }, function () { res(null); });
        });
        return realmCache.svc;
    }
    function mainSig() { return realmSig(window, REALM_K); }
    // A realm promise gives the realm's fields, "na" when the browser has
    // no such worker, or null when the worker failed to start or answer: a
    // site's Content Security Policy can block blob: workers, so a failure
    // says nothing about the browser and the test gives no answer.
    // One digit per compared field: 1 where the page and the other realm
    // disagree.
    function mismatch(main, other) {
        if (other === "na") return "na";
        if (!other || !other.slice) return null;
        var m = "";
        for (var i = 0; i < REPLAY_FIELDS; i++) m += main[i] === other[i] ? "0" : "1";
        return m;
    }
    function generics(sig) {
        if (sig === "na") return "na";
        if (!sig || !sig.slice) return null;
        return sig.slice(GENERIC_FROM).map(function (x) { return x === "noc" ? x : r3(Number(x)); }).join(",");
    }

    // Worker scope replay: which fields differ between the page and a
    // dedicated or a shared worker. A user agent override from a driver,
    // and JavaScript injected into the page, do not reach every realm: a
    // Playwright user agent override leaves the dedicated worker's
    // platform, and a shared worker escapes the page's emulation entirely.
    // CloakBrowser's text noise makes its OffscreenCanvas widths differ
    // between realms.
    a("a_wkreplay.1", function () { return dedicatedRealm().then(function (d) { return mismatch(mainSig(), d); }); });
    a("a_shreplay.1", function () { return sharedRealm().then(function (d) { return mismatch(mainSig(), d); }); });
    sw("a_svcreplay.1", function () { return timeout(serviceRealm(), 4000).then(function (d) { return mismatch(mainSig(), d); }); });
    // Generic families in a shared or service worker. Those realms have no
    // document font settings, so on Windows every generic but system-ui
    // falls to the platform default face, while fontconfig on Linux keeps
    // serif, sans-serif and monospace apart.
    a("a_shfonts.1", function () { return sharedRealm().then(generics); });
    sw("a_svcfonts.1", function () { return timeout(serviceRealm(), 4000).then(generics); });
    // The same generic widths in the page, through OffscreenCanvas.
    s("f_pagegeneric.1", function () { return generics(mainSig()); });

    // ---- fonts: platform font code ---------------------------------------
    // Quoted 'sans', 'serif' and 'monospace' are family names, not generic
    // families. Only fontconfig answers them, through its alias rules: on
    // Windows and macOS no family has those names.
    s("f_quotedalias.1", function () {
        var t = "mmmmmlllliiiWW00@@#", fb = spanW(t, "font-size:40px;font-family:" + NOFONT);
        return ["sans", "serif", "monospace", "Sans", "Mono", "sans-serif"].map(function (q) {
            return spanW(t, "font-size:40px;font-family:'" + q + "', " + NOFONT) === fb ? "0" : "1";
        }).join("");
    });
    // Text field average character width, from the font's own metrics
    // through the platform scaler (DirectWrite or FreeType), rounded to half
    // a pixel because the exact width moves with the display scale.
    function inputW(font, n) {
        var b = box(); var i = document.createElement("input"); i.size = n;
        i.style.cssText = "padding:0;border:0;margin:0;font:" + font; b.appendChild(i);
        var w = i.getBoundingClientRect().width; b.removeChild(i); return w;
    }
    s("f_inputavg.2", function () {
        return ["Arial", "Times New Roman", "Calibri", "Tahoma"].map(function (f) {
            var font = "16px '" + f + "'";
            return String(Math.round((inputW(font, 101) - inputW(font, 1)) / 50) / 2);
        }).join(",");
    });
    // Which named face each generic family resolves to in the page.
    var GENMAP = [["serif", ["Times New Roman", "DejaVu Serif", "Liberation Serif", "Times"]], ["sans-serif", ["Arial", "DejaVu Sans", "Liberation Sans", "Helvetica"]],
        ["monospace", ["Consolas", "Courier New", "DejaVu Sans Mono", "Liberation Mono", "Menlo"]], ["cursive", ["Comic Sans MS", "Apple Chancery"]],
        ["fantasy", ["Impact", "Papyrus"]], ["system-ui", ["Segoe UI", "DejaVu Sans", "Ubuntu", "Cantarell", "Helvetica Neue"]],
        ["math", ["Cambria Math", "STIX Two Math", "Latin Modern Math"]], ["emoji", ["Segoe UI Emoji", "Noto Color Emoji", "Apple Color Emoji"]]];
    s("f_genericmap.1", function () {
        var t = "mmmmmlllliiiWW00@@#";
        var fb = spanW(t, "font-size:40px;font-family:" + NOFONT);
        return GENMAP.map(function (p) {
            var g = spanW(t, "font-size:40px;font-family:" + p[0]);
            for (var i = 0; i < p[1].length; i++) if (spanW(t, "font-size:40px;font-family:'" + p[1][i] + "', " + NOFONT) === g) return p[1][i];
            return g === fb ? "default" : "?" + r3(g);
        }).join(";");
    });
    // The fallback face for symbols and scripts the primary font lacks.
    var FALLBACK = [
        ["\u265E", ["Segoe UI Symbol", "Segoe UI Emoji", "MS Gothic", "DejaVu Sans", "Noto Sans Symbols 2"]],
        ["\u2713", ["Segoe UI Symbol", "Segoe UI Emoji", "MS Gothic", "Arial", "DejaVu Sans"]],
        ["\u2318", ["Segoe UI Symbol", "Segoe UI Emoji", "MS Gothic", "DejaVu Sans"]],
        ["\u10D0\u10D1\u10D2", ["Sylfaen", "Segoe UI", "DejaVu Sans", "Noto Sans Georgian"]],
        ["\u0531\u0532\u0533", ["Segoe UI", "Sylfaen", "DejaVu Sans", "Noto Sans Armenian"]],
        ["\u2600\u2602", ["Segoe UI Symbol", "Segoe UI Emoji", "MS Gothic", "DejaVu Sans"]],
        ["\u2190\u21D2", ["Segoe UI Symbol", "Arial", "Times New Roman", "MS Gothic", "DejaVu Sans"]],
        ["\u0915\u093F", ["Nirmala UI", "Mangal", "Noto Sans Devanagari"]],
        ["\u1E9E\u0180", ["Segoe UI", "Arial", "Times New Roman", "Calibri", "DejaVu Sans"]]];
    s("f_fallback.1", function () {
        return FALLBACK.map(function (t) {
            var base = spanW(t[0], "font-size:40px;font-family:" + NOFONT);
            for (var i = 0; i < t[1].length; i++) if (spanW(t[0], "font-size:40px;font-family:'" + t[1][i] + "', " + NOFONT) === base) return t[1][i];
            return "?" + r3(base);
        }).join(";");
    });

    // ---- fonts: genuine Microsoft files -----------------------------------
    // Glyph outline signature. Each glyph is drawn large, cropped to its ink
    // box and reduced to a 16 x 16 grid of cells that are more than half
    // ink, so raster and hinting differences mostly vanish and the outline
    // decides. Canvas, so the display scale plays no part.
    function glyphBits(font, ch) {
        var S = 240, c = canvas2d(S, S); c.font = font; c.fillStyle = "#000"; c.textBaseline = "alphabetic"; c.fillText(ch, 20, 180);
        var d = c.getImageData(0, 0, S, S).data, x0 = S, y0 = S, x1 = -1, y1 = -1, x, y;
        for (y = 0; y < S; y++) for (x = 0; x < S; x++) if (d[(y * S + x) * 4 + 3] >= 128) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
        if (x1 < 0) return "empty";
        var N = 16, bits = "", bw = (x1 - x0 + 1) / N, bh = (y1 - y0 + 1) / N;
        for (var gy = 0; gy < N; gy++) for (var gx = 0; gx < N; gx++) {
            var ink = 0, tot = 0;
            for (y = Math.floor(y0 + gy * bh); y < Math.floor(y0 + (gy + 1) * bh) || y === Math.floor(y0 + gy * bh); y++)
                for (x = Math.floor(x0 + gx * bw); x < Math.floor(x0 + (gx + 1) * bw) || x === Math.floor(x0 + gx * bw); x++) { tot++; if (d[(y * S + x) * 4 + 3] >= 128) ink++; }
            bits += ink * 2 > tot ? "1" : "0";
        }
        var hex = ""; for (var i = 0; i < bits.length; i += 4) hex += parseInt(bits.substr(i, 4), 2).toString(16);
        return (x1 - x0 + 1) + "x" + (y1 - y0 + 1) + ":" + hex;
    }
    var SHAPE_GLYPHS = ["g", "@", "&", "\u00DF", "\u0105", "\u0436", "R", "3"];
    s("f_segoeshape.1", function () { return SHAPE_GLYPHS.map(function (ch) { return glyphBits("160px 'Segoe UI'", ch); }).join("|"); });
    s("f_calibrishape.1", function () { return SHAPE_GLYPHS.map(function (ch) { return glyphBits("160px Calibri", ch); }).join("|"); });
    // Exact small size raster of Segoe UI. DirectWrite and FreeType differ
    // even with the same file, but ClearType tuning may move it too.
    s("f_segoeraster.1", function () {
        var c = canvas2d(200, 24); c.font = "13px 'Segoe UI'"; c.fillStyle = "#000"; c.fillText("Segoe UI 0123 g\u00DF@&", 2, 17);
        var d = c.getImageData(0, 0, 200, 24).data, alpha = ""; for (var i = 3; i < d.length; i += 4) alpha += String.fromCharCode(d[i]);
        return fnv(alpha);
    });
    // Segoe UI Variable, Windows 11 only. The value is the shape of the width
    // curve over weights: "<" where the next setting is wider, "=" where it
    // renders the same. Chrome on Windows snaps font-weight to the named
    // instances and ignores font-variation-settings, Firefox and FreeType
    // follow the axes.
    function curve(ws) { var o = ""; for (var i = 1; i < ws.length; i++) o += Math.abs(ws[i] - ws[i - 1]) < 0.05 ? "=" : ws[i] > ws[i - 1] ? "<" : ">"; return o; }
    var VAR_TEXT = "Variable weight 0123 Wwg";
    function varPresent(f) { return spanW(VAR_TEXT, "font-size:40px;font-family:" + f + ", " + NOFONT) !== spanW(VAR_TEXT, "font-size:40px;font-family:" + NOFONT); }
    // Each width is measured in its own task: the first use of each weight
    // of a variable font builds a new platform font, which took up to 150
    // ms per family headed and 850 ms under 4x CPU throttling, too long for
    // one task. stepwise runs each step in its own task and resolves with
    // their answers, and familyCurves answers one curve per family, or
    // "absent" where the family is not installed.
    function stepwise(steps) {
        return new Promise(function (res) {
            var out = [], ch = new MessageChannel();
            function next() {
                try { out.push(steps[out.length]()); } catch (e) { out.push(NaN); }
                if (out.length < steps.length) ch.port2.postMessage(0); else { ch.port1.close(); res(out); }
            }
            ch.port1.onmessage = next;
            next();
        });
    }
    function familyCurves(fams, settings, cssOf) {
        var out = [], i = 0;
        function one() {
            if (i >= fams.length) return Promise.resolve(out.join("|"));
            var f = fams[i++];
            return stepwise([function () { return varPresent(f); }].concat(settings.map(function (v) { return function () { return spanW(VAR_TEXT, cssOf(f, v)); }; })))
                .then(function (r) { out.push(r[0] ? curve(r.slice(1)) : "absent"); return one(); });
        }
        return one();
    }
    // Version 3 leaves out Segoe UI Variable Display, which answered as
    // Text did everywhere and cost a third of the time.
    a("f_segoevar.3", function () {
        return familyCurves(["'Segoe UI Variable Text'", "'Segoe UI Variable'"], [300, 350, 400, 450, 500, 600, 700], function (f, wg) {
            return "font-size:40px;font-weight:" + wg + ";font-family:" + f;
        });
    });
    a("f_segoevaraxis.2", function () {
        return familyCurves(["'Segoe UI Variable Text'", "'Segoe UI Variable'"], ["'wght' 300", "'wght' 350", "'wght' 400", "'wght' 450", "'wght' 500", "'wght' 650", "'wght' 700"], function (f, v) {
            return "font-size:40px;font-family:" + f + ";font-variation-settings:" + v;
        });
    });
    // Glyphs newer than the 2002 Core Fonts: rupee, ruble and lira. "1" when
    // the face itself has the glyph.
    s("f_newglyphs.2", function () {
        return ["Arial", "Times New Roman", "Courier New"].map(function (f) {
            return ["\u20B9", "\u20BD", "\u20BA"].map(function (ch) {
                var x = spanW(ch, "font-size:100px;font-family:'" + f + "', 'Segoe UI Symbol', 'DejaVu Sans'");
                var y = spanW(ch, "font-size:100px;font-family:'" + f + "', 'Courier New', 'DejaVu Sans Mono'");
                return Math.abs(x - y) < 0.05 ? "1" : "0";
            }).join("");
        }).join("|");
    });
    // Kerning pairs: width with kerning minus width without.
    var PAIRS = ["AV", "AT", "AY", "LT", "To", "Te", "Ty", "Wa", "Yo", "VA", "P.", "F,", "r.", "y.", "Av", "LY", "Tr", "Kv", "\"A", "Fe"];
    s("f_kerning.1", function () {
        var c = canvas2d(); if (!("fontKerning" in c)) return "na";
        return ["Calibri", "Arial", "Segoe UI", "Times New Roman"].map(function (f) {
            c.font = "100px '" + f + "'";
            return PAIRS.map(function (p) { c.fontKerning = "normal"; var k = c.measureText(p).width; c.fontKerning = "none"; return r3(k - c.measureText(p).width); }).join(",");
        }).join("|");
    });
    // OpenType features: "+", "-" or "0" for the width change when a feature
    // is switched on.
    s("f_otfeatures.2", function () {
        var t = "Small Caps 0123456789 Office", base;
        return [["Calibri", ["smcp", "c2sc", "onum", "lnum", "pnum", "tnum", "ss01"]], ["Cambria", ["smcp", "onum", "tnum"]], ["Segoe UI", ["smcp", "ss01", "tnum", "case"]]].map(function (p) {
            base = spanW(t, "font-size:40px;font-family:'" + p[0] + "'");
            return p[1].map(function (f) {
                var d = spanW(t, "font-size:40px;font-family:'" + p[0] + "';font-feature-settings:'" + f + "' 1") - base;
                return d > 0.5 ? "+" : d < -0.5 ? "-" : "0";
            }).join("");
        }).join("|");
    });
    // Vertical metrics from canvas: the font's ascent and descent as the
    // platform scaler reports them, and the ink box of "Hg".
    var VFONTS = ["Arial", "Times New Roman", "Segoe UI", "Calibri", "Consolas", "Cambria"];
    s("f_vmetrics.1", function () {
        var c = canvas2d(); c.font = "100px Arial"; if (c.measureText("x").fontBoundingBoxAscent === undefined) return "na";
        return VFONTS.map(function (f) {
            c.font = "100px '" + f + "'"; var m = c.measureText("Hg");
            return r3(m.fontBoundingBoxAscent) + "/" + r3(m.fontBoundingBoxDescent) + "/" + r3(m.actualBoundingBoxAscent) + "/" + r3(m.actualBoundingBoxDescent);
        }).join(",");
    });

    // ---- media and platform features -------------------------------------
    // AAC through the platform encoder. Chromium allows it on Windows, macOS
    // and Android only, and on Windows only at four bitrates and 1, 2 or 6
    // channels. One digit per configuration.
    a("a_aacrules.1", function () {
        if (typeof AudioEncoder === "undefined") return Promise.resolve("na");
        var cfgs = [96000, 100000, 128000, 320000].map(function (b) { return { codec: "mp4a.40.2", sampleRate: 48000, numberOfChannels: 2, bitrate: b }; })
            .concat([1, 6, 4].map(function (ch) { return { codec: "mp4a.40.2", sampleRate: 48000, numberOfChannels: ch }; }));
        return Promise.all(cfgs.map(function (cfg) {
            return AudioEncoder.isConfigSupported(cfg).then(function (r) { return r.supported ? "1" : "0"; }, function () { return "E"; });
        })).then(function (x) { return x.join(""); });
    });
    // A real one second AAC encode: chunks, bytes and the decoder
    // description length. A build that claims AAC support without the
    // platform encoder fails here.
    a("a_aacencode.1", function () {
        return timeout(new Promise(function (res) {
            if (typeof AudioEncoder === "undefined") return res("na");
            var chunks = 0, bytes = 0, desc = "-";
            var enc = new AudioEncoder({
                output: function (ch, m) { chunks++; bytes += ch.byteLength; if (m && m.decoderConfig && desc === "-") desc = m.decoderConfig.description ? String(m.decoderConfig.description.byteLength) : "0"; },
                error: function (e) { res("error:" + e.name); }
            });
            try { enc.configure({ codec: "mp4a.40.2", sampleRate: 48000, numberOfChannels: 2, bitrate: 128000 }); } catch (e) { return res("cfg:" + e.name); }
            var n = 48000, buf = new Float32Array(n * 2);
            for (var i = 0; i < n; i++) { buf[i] = buf[n + i] = Math.sin(i / 10) * 0.3; }
            try { enc.encode(new AudioData({ format: "f32-planar", sampleRate: 48000, numberOfFrames: n, numberOfChannels: 2, timestamp: 0, data: buf })); } catch (e) { return res("enc:" + e.name); }
            enc.flush().then(function () { res(chunks + "/" + bytes + "/" + desc); try { enc.close(); } catch (e) { } }, function (e) { res("flush:" + e.name); });
        }), 5000);
    });
    // MediaRecorder container and codec support. Recording H.264 and AAC
    // in MP4 goes through the platform encoders, so this is the encode side
    // of what c_codecs.2 asks of the decoders.
    // Version 2 leaves out HEVC (hvc1), which Chrome records only through
    // a hardware encoder: the lab VM has no GPU and answered 0, a visitor
    // with a recent GPU answers 1.
    s("p_recorder.2", function () {
        if (typeof MediaRecorder === "undefined" || !MediaRecorder.isTypeSupported) return "na";
        return ["video/mp4;codecs=avc1,mp4a.40.2", "audio/mp4;codecs=mp4a.40.2", "video/mp4;codecs=avc1,opus", "audio/mp4;codecs=opus",
            "video/x-matroska;codecs=avc1", "video/webm;codecs=av1", "video/mp4;codecs=av01", "audio/webm;codecs=pcm", "video/mp4;codecs=vp9"]
            .map(function (m) { return MediaRecorder.isTypeSupported(m) ? "1" : "0"; }).join("");
    });
    // Secure Payment Confirmation is built for Windows, macOS and Android
    // only. "off" only for unavailable-feature-not-enabled, which no
    // Windows or macOS Chrome answers. The other answers depend on Windows
    // Hello and on how the browser was launched, so they all read "on".
    a("a_spcflag.1", function () {
        if (typeof PaymentRequest === "undefined" || !PaymentRequest.securePaymentConfirmationAvailability) return Promise.resolve("na");
        return timeout(PaymentRequest.securePaymentConfirmationAvailability().then(function (v) { return v === "unavailable-feature-not-enabled" ? "off" : "on"; },
            function (e) { return "E:" + e.name; }), 4000);
    });
    // Shape detection APIs, shipped on macOS, ChromeOS and Android only.
    s("p_shapedetect.1", function () { return [typeof window.BarcodeDetector, typeof window.FaceDetector, typeof window.TextDetector].map(function (t) { return t.charAt(0); }).join(""); });

    // ---- CSS system colours ----------------------------------------------
    var SYS_MODERN = ["ActiveText", "LinkText", "VisitedText", "Canvas", "CanvasText", "Field", "FieldText", "Highlight", "HighlightText", "ButtonFace", "ButtonText",
        "ButtonBorder", "GrayText", "Mark", "MarkText", "AccentColor", "AccentColorText", "SelectedItem", "SelectedItemText", "-webkit-focus-ring-color"];
    var SYS_OLD = ["ActiveBorder", "ActiveCaption", "AppWorkspace", "Background", "ButtonHighlight", "ButtonShadow", "CaptionText", "InactiveBorder", "InactiveCaption",
        "InactiveCaptionText", "InfoBackground", "InfoText", "Menu", "MenuText", "Scrollbar", "ThreeDDarkShadow", "ThreeDFace", "ThreeDHighlight", "ThreeDLightShadow", "ThreeDShadow", "Window", "WindowFrame", "WindowText"];
    // In forced colours mode (a Windows contrast theme) every system colour
    // is the user's theme colour, so the tests give no answer.
    function sysColors(names, scheme) {
        if (window.matchMedia && matchMedia("(forced-colors: active)").matches) return null;
        var b = box();
        return names.map(function (n) {
            if (window.CSS && CSS.supports && !CSS.supports("color", n)) return "x";
            var e = document.createElement("div"); e.style.setProperty("color-scheme", scheme); e.style.setProperty("color", n); b.appendChild(e);
            var v = getComputedStyle(e).color; b.removeChild(e);
            return v.replace(/rgba?\(|\)| /g, "");
        }).join(";");
    }
    s("c_activetext.1", function () { return sysColors(["ActiveText", "LinkText", "VisitedText"], "light"); });
    // Version 1 was a version 2 gatherer test retired on 16 October, so the
    // candidate starts at version 2.
    s("c_syscolors.2", function () { return sysColors(SYS_MODERN, "light"); });
    s("c_syscolorsdark.1", function () { return sysColors(SYS_MODERN, "dark"); });
    // The deprecated system colours, which Blink takes from its own per
    // platform tables.
    s("c_sysold.1", function () { return sysColors(SYS_OLD, "light"); });

    // ---- version surface -------------------------------------------------
    function ownNames(o) { try { return Object.getOwnPropertyNames(o).sort(); } catch (e) { return []; } }
    var PROTOS = ["Navigator", "Document", "HTMLElement", "Element", "Node", "Window", "HTMLMediaElement", "CanvasRenderingContext2D", "HTMLInputElement", "ShadowRoot", "Performance", "Response", "Request"];
    s("e_surfprotos.1", function () {
        var all = [];
        var counts = PROTOS.map(function (k) { var c = window[k]; if (!c || !c.prototype) return "-"; var n = ownNames(c.prototype); all.push(k + ":" + n.join(",")); return String(n.length); });
        return counts.join(",") + "|" + fnv(all.join(";"));
    });
    // Interface constructors on the global, without page or extension globals.
    s("e_surfglobals.1", function () {
        var g = ownNames(window).filter(function (k) {
            if (!/^[A-Z]/.test(k)) return false;
            var d = Object.getOwnPropertyDescriptor(window, k);
            return d && typeof d.value === "function" && /\[native code\]/.test(Function.prototype.toString.call(d.value));
        });
        return g.length + "|" + fnv(g.join(","));
    });
    s("e_surfcss.1", function () {
        var cs = getComputedStyle(document.documentElement), n = [];
        for (var i = 0; i < cs.length; i++) n.push(cs[i]);
        n.sort();
        return n.length + "|" + fnv(n.join(","));
    });
    s("e_intllists.1", function () {
        if (!Intl.supportedValuesOf) return "na";
        var all = [];
        var c = ["calendar", "collation", "currency", "numberingSystem", "timeZone", "unit"].map(function (k) {
            try { var v = Intl.supportedValuesOf(k); all.push(v.join(",")); return String(v.length); } catch (e) { return "E"; }
        });
        return c.join(",") + "|" + fnv(all.join(";"));
    });

    // ---- automation residue -----------------------------------------------
    // Runtime.enable residue: an attached inspector session formats logged
    // errors, which runs Error.prepareStackTrace and reads the error's name,
    // and previews a proxy prototype. Value: prepareStackTrace calls, name
    // reads and ownKeys traps.
    s("p_cdprt.1", function () {
        var had = Object.prototype.hasOwnProperty.call(Error, "prepareStackTrace"), orig = Error.prepareStackTrace, calls = 0, reads = 0, own = 0;
        Error.prepareStackTrace = function (e, st) { calls++; return orig ? orig(e, st) : String(e); };
        try {
            console.debug(new Error("51d"));
            var e2 = new Error("51d"); Object.defineProperty(e2, "name", { configurable: true, get: function () { reads++; return "Error"; } });
            console.debug(e2);
            var t = new Proxy({}, { ownKeys: function (x) { own++; return Reflect.ownKeys(x); } });
            console.groupEnd(Object.create(t));
        } finally { if (had) Error.prepareStackTrace = orig; else delete Error.prepareStackTrace; }
        return calls + "|" + reads + "|" + own;
    });
    // Runtime domain residue, the stronger form found by the timing study:
    // an Error whose prototype carries a stack getter is logged. Only an
    // inspector client that called Runtime.enable (Puppeteer, Playwright,
    // most stealth stacks, or open DevTools) formats it and reads the
    // getter. Value: getter reads.
    s("p_rtdebug.1", function () {
        var hit = 0, e = new Error("51d");
        try { delete e.stack; } catch (x) { }
        var proto = Object.create(Error.prototype);
        Object.defineProperty(proto, "stack", { configurable: true, get: function () { hit++; return ""; } });
        Object.setPrototypeOf(e, proto);
        console.debug(e);
        return String(hit);
    });
    // The deepest recursion a dedicated worker allows, the most of twelve
    // attempts, each in its own task so the optimised code V8 compiles in
    // the background is in place from the second attempt (six attempts in
    // one task sometimes answered the unoptimised depth). Workers get a
    // fixed 500 KB stack on every desktop OS, so this is exact per V8
    // build and tells the engine version, not the OS: Chrome 155 10608,
    // Chrome 148 6368 in the lab. The worker source is a plain string, so
    // obfuscation cannot reach it.
    a("e_wkdepth.1", function () {
        return timeout(new Promise(function (res) {
            if (typeof Worker === "undefined") return res("na");
            try {
                var url = blobUrl("onmessage=function(){var d=0,b=0,k=0;function f(){d++;f()}function go(){d=0;try{f()}catch(e){}if(d>b)b=d;if(++k<12)setTimeout(go,0);else postMessage(b)}go()}");
                var w = new Worker(url);
                w.onmessage = function (e) { res(String(e.data)); w.terminate(); URL.revokeObjectURL(url); };
                w.onerror = function () { res(null); };
                w.postMessage(0);
            } catch (e) { res(null); }
        }), 3000);
    });
    // Navigation rate limiting: 300 replaceState calls in a fresh frame.
    // Chromium ignores calls past 200 in 10 s unless launched with
    // --disable-ipc-flooding-protection, which Puppeteer, Playwright and
    // CloakBrowser pass. Value: applied count, first throw index and name.
    a("a_ipcflood.1", function () {
        return new Promise(function (res) {
            var f = document.createElement("iframe"); f.style.cssText = "position:absolute;left:-10000px;width:10px;height:10px;visibility:hidden";
            document.body.appendChild(f);
            var h, applied = 0, threw = "-";
            try { h = f.contentWindow.history; } catch (e) { document.body.removeChild(f); return res(null); }
            for (var i = 1; i <= 300; i++) {
                try { h.replaceState({ n: i }, ""); if (h.state && h.state.n === i) applied++; }
                catch (e) { if (threw === "-") threw = i + ":" + e.name; }
            }
            document.body.removeChild(f);
            res(applied + "|" + threw);
        });
    });
    // A document a driver served by route fulfil has no protocol but a
    // transfer size (Patchright delivers init scripts that way). A page a
    // service worker answered, a prefetched or prerendered page and one
    // from a cache can also lack the protocol, so they give no answer.
    s("p_navhop.2", function () {
        var n = performance.getEntriesByType && performance.getEntriesByType("navigation")[0];
        if (!n) return "na";
        if ((navigator.serviceWorker && navigator.serviceWorker.controller) || n.workerStart > 0 || n.deliveryType || n.activationStart > 0) return null;
        return n.nextHopProtocol === "" && n.transferSize > 0 ? "1" : "0";
    });
    // Pointer and touch, for phone claims and emulation.
    s("c_pointer.1", function () {
        function mq(q) { return matchMedia(q).matches ? "1" : "0"; }
        return [mq("(pointer: fine)") + mq("(pointer: coarse)") + mq("(pointer: none)"), mq("(hover: hover)"),
            mq("(any-pointer: fine)") + mq("(any-pointer: coarse)") + mq("(any-pointer: none)"), mq("(any-hover: hover)"),
            String(navigator.maxTouchPoints), "ontouchstart" in window ? "1" : "0", typeof TouchEvent === "undefined" ? "0" : "1"].join("|");
    });
    // Headless signs a user agent swap does not reach: no window frame
    // around the page (outer equals inner), no taskbar (screen equals the
    // available area) and the 800 x 600 default screen. One flag each.
    s("c_winframe.1", function () {
        return [outerWidth === innerWidth ? "1" : "0", outerHeight === innerHeight ? "1" : "0",
            screen.height === screen.availHeight ? "1" : "0", screen.width === 800 && screen.height === 600 ? "1" : "0"].join("");
    });
    // The strong rule of c_winframe.1 on its own: headless Chrome reports an
    // 800 x 600 screen whatever the window size, a real display almost
    // never is. The other three flags depend on the window mode and the
    // taskbar settings.
    s("c_screen800.1", function () { return screen.width === 800 && screen.height === 600 ? "1" : "0"; });
    // Window entirely outside its own screen while the page is visible:
    // "1v". Chrome marks a genuine window that is off every screen as
    // occluded and the page hidden ("1h"), so "1v" needs
    // --disable-backgrounding-occluded-windows, which headed stealth stacks
    // pass. A bad actor rule, not baseline rows: lab windows sit off screen
    // and answer 1h (or 1v with that flag), which a person never sends.
    s("s_offscreen.1", function () {
        var l = screen.availLeft || 0, t = screen.availTop || 0;
        var out = screenX + outerWidth <= l || screenY + outerHeight <= t || screenX >= l + screen.availWidth || screenY >= t + screen.availHeight;
        return (out ? "1" : "0") + document.visibilityState.charAt(0);
    });
    // The user agent against client hints: whether the brand the user agent
    // names is listed at its major ("1", "v" other version, "0" missing, "e"
    // no brands), "h" for a HeadlessChrome brand, then whether the high
    // entropy hints are filled (platform version, architecture, bitness)
    // and the form factors. A --user-agent switch replaces the string and
    // leaves the high entropy hints empty: "1-|---|".
    a("s_uach.2", function () {
        var u = navigator.userAgentData; if (!u || !u.getHighEntropyValues) return Promise.resolve("na");
        var m = /(?:Chrome|HeadlessChrome)\/(\d+)/.exec(navigator.userAgent);
        var claim = /Edg\//.test(navigator.userAgent) ? "Microsoft Edge" : /HeadlessChrome/.test(navigator.userAgent) ? "HeadlessChrome" : "Google Chrome";
        var mine = u.brands.filter(function (b) { return b.brand === claim; })[0];
        var flags = (u.brands.length === 0 ? "e" : mine ? (m && mine.version === m[1] ? "1" : "v") : "0") +
            (u.brands.some(function (b) { return b.brand === "HeadlessChrome"; }) ? "h" : "-");
        return u.getHighEntropyValues(["platformVersion", "architecture", "bitness", "formFactors"]).then(function (h) {
            return flags + "|" + (h.platformVersion ? "p" : "-") + (h.architecture || "-") + (h.bitness || "-") + "|" + (h.formFactors ? h.formFactors.join(",") : "-");
        }, function () { return null; });
    });

    // Runs every test. The asynchronous tests start first, since most of
    // their work happens off the page's main thread (workers, encoders, the
    // browser process). The synchronous tests then run in slices of about
    // SLICE_MS with a yield to the event loop between slices, so the page
    // never sees one long task, and the budget starts when the last of them
    // is done: whatever has finished when the budget ends is what is sent.
    // The yield uses a MessageChannel because timers are throttled to once
    // a second in background tabs.
    var SLICE_MS = 30;
    function run(budgetMs, params) {
        var started = performance.now();
        params = params || {};
        OPT = params;
        P = {};
        Object.keys(DEFAULTS).forEach(function (k) { P[k] = params[k] !== undefined ? params[k] : DEFAULTS[k]; });
        realmCache = {};
        function pick(ids) { return params.only ? ids.filter(function (n) { return params.only.indexOf(n) >= 0; }) : ids; }
        var results = {}, done = [], timing = {};
        var later = Object.keys(ASYNC).concat(params.swUrl ? Object.keys(SERVICE) : []);
        var pending = pick(later).map(function (n) {
            var t0 = performance.now(), f = ASYNC[n] || SERVICE[n];
            return Promise.resolve().then(function () { return f(); }).then(function (v) { if (v !== null && v !== undefined) results[n] = str(v); }, function () { results[n] = "err"; })
                .then(function () { timing[n] = Math.round(performance.now() - t0); done.push(n); });
        });
        var queue = pick(Object.keys(SYNC));
        var channel = typeof MessageChannel === "undefined" ? null : new MessageChannel();
        function yieldThen(f) { if (channel) { channel.port1.onmessage = f; channel.port2.postMessage(0); } else setTimeout(f, 0); }
        var synced = new Promise(function (res) {
            function slice() {
                var s0 = performance.now();
                while (queue.length && performance.now() - s0 < SLICE_MS) {
                    var n = queue.shift(), t0 = performance.now();
                    try { var v = SYNC[n](); if (v !== null && v !== undefined) results[n] = str(v); } catch (e) { results[n] = "err"; }
                    timing[n] = Math.round(performance.now() - t0); done.push(n);
                }
                if (queue.length) yieldThen(slice);
                else { if (lab && lab.parentNode) lab.parentNode.removeChild(lab); if (channel) channel.port1.close(); res(); }
            }
            slice();
        });
        return synced.then(function () {
            var budget = new Promise(function (r) { setTimeout(r, budgetMs || 3000); });
            return Promise.race([Promise.all(pending), budget]);
        }).then(function () {
            if (lab && lab.parentNode) lab.parentNode.removeChild(lab);
            var r = { version: "3", results: results, done: done, elapsed: Math.round(performance.now() - started) };
            if (params.timing) r.timing = timing;
            return r;
        });
    }
    window.__51dBrowserTests = { run: run, names: Object.keys(SYNC).concat(Object.keys(ASYNC)), service: Object.keys(SERVICE), defaults: DEFAULTS };
})();
