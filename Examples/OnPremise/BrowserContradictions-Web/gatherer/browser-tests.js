// 51Degrees browser tests gatherer. Proof of concept.
// Collects raw measurements only. It holds no expected answers, the server
// compares the measurements with the baseline for the claimed platform.
// Payload: "1|" then name=value pairs joined by "&", each value URI
// encoded, the whole string URI encoded again for the cookie.
var bt = [];
function bta(n, f) {
    var v;
    try { v = String(f()); } catch (e) { v = "err"; }
    bt.push(n + "=" + encodeURIComponent(v));
}
bta("share", function () { return typeof navigator.share; });
bta("bluetooth", function () { return typeof navigator.bluetooth; });
bta("menufont", function () {
    var c = document.createElement("canvas").getContext("2d");
    c.font = "menu";
    return c.font;
});
bta("captionfont", function () {
    var c = document.createElement("canvas").getContext("2d");
    c.font = "caption";
    return c.font;
});
bta("tanh", function () {
    return [0.1, 0.7, 1.3, 2.9, 4.1].map(Math.tanh).join(",");
});
bta("expm1", function () {
    return [1e-10, 0.3, 2.5, 7.7].map(Math.expm1).join(",");
});
bta("segoe", function () {
    var c = document.createElement("canvas").getContext("2d");
    c.font = "72px monospace";
    var a = c.measureText("mmmmmmmmmmlli").width;
    c.font = "72px 'Segoe UI', monospace";
    return c.measureText("mmmmmmmmmmlli").width !== a ? "1" : "0";
});
bta("uachplatform", function () {
    return navigator.userAgentData ? navigator.userAgentData.platform : "none";
});
var btp = encodeURIComponent("1|" + bt.join("&"));
document.cookie = "51D_BrowserTests=" + btp;
// 51D replace this comment with callback function.
