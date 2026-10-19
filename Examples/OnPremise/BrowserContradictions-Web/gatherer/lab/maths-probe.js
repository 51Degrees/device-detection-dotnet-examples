// Maths probe for the lab. Evaluates the Math functions over a wide,
// deterministic input set and returns every result, so the lab can find the
// inputs whose last digit separates one engine build or OS from another.
// Served by collect.py at /maths. Not part of the gatherer.
(function () {
    function lcg(seed) {
        var s = seed >>> 0;
        return function () { s = (Math.imul(s, 1664525) + 1013904223) >>> 0; return s / 4294967296; };
    }
    function grid(from, to, step) { var xs = []; for (var x = from; x <= to + 1e-12; x += step) xs.push(x); return xs; }
    function rand(seed, n, lo, hi) { var r = lcg(seed); var xs = []; for (var i = 0; i < n; i++) xs.push(lo + (hi - lo) * r()); return xs; }
    function dyadic(n, den) { var xs = []; for (var k = 1; k < n; k++) xs.push(k / den); return xs; }

    var SETS = {
        tanh: dyadic(4097, 256).concat(rand(1, 2000, 0, 20), rand(2, 500, -3, 0), [1e-8, 1e-5, 0.0123, 0.1, 0.7, 1.3, 2.9, 4.1, 7.7, 13.37, 19.06, 22, 25]),
        atanh: dyadic(4096, 4096).concat(rand(3, 2000, 0, 1), rand(4, 500, -1, 0), [1e-8, 1e-5, 0.0123, 0.1, 0.5, 0.9, 0.999, 0.999999]),
        sinh: rand(5, 1500, -30, 30), cosh: rand(6, 1500, -30, 30), asinh: rand(7, 1500, -50, 50),
        acosh: rand(8, 1500, 1, 60), expm1: rand(9, 1500, -5, 30), log1p: rand(10, 1500, -0.9, 50),
        exp: rand(11, 1500, -50, 50), log: rand(12, 1500, 0.001, 1000), log2: rand(13, 1000, 0.001, 1000),
        log10: rand(14, 1000, 0.001, 1000), cbrt: rand(15, 1500, -1000, 1000), sin: rand(16, 1500, -100, 100),
        cos: rand(17, 1500, -100, 100), tan: rand(18, 1500, -100, 100), atan: rand(19, 1000, -50, 50),
        asin: rand(20, 1000, -1, 1), acos: rand(21, 1000, -1, 1)
    };
    function run() {
        var out = {};
        Object.keys(SETS).forEach(function (f) {
            out[f] = { x: SETS[f].map(function (x) { return x.toPrecision(17); }), y: SETS[f].map(function (x) { return Math[f](x).toPrecision(17); }) };
        });
        out.pow = { x: SETS.log.map(function (x) { return x.toPrecision(17); }), y: SETS.log.map(function (x) { return Math.pow(x, 0.37).toPrecision(17); }) };
        return out;
    }
    window.__mathsProbe = run;
})();
