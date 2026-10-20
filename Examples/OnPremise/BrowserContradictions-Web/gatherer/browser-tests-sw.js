// Service worker file for the browser tests that run in a service worker
// realm (a_svcreplay, a_svcfonts). Sites that host it pass its URL to the
// gatherer as params.swUrl. It evaluates the function source the page sends
// with the worker's global object and the strings sent with it, and replies
// on the port that came with the message, so the test logic stays in the
// gatherer and this file never changes.
self.addEventListener("install", function () { self.skipWaiting(); });
self.addEventListener("activate", function (e) { e.waitUntil(self.clients.claim()); });
self.addEventListener("message", function (e) {
    var port = e.ports && e.ports[0];
    if (!port || !e.data || typeof e.data.src !== "string") return;
    try { port.postMessage(new Function("return (" + e.data.src + ")")()(self, e.data.k)); }
    catch (err) { port.postMessage(null); }
});
