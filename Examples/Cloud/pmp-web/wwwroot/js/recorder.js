/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
 * Forbury Square, Reading, Berkshire, United Kingdom RG1 3EU.
 *
 * This Original Work is licensed under the European Union Public Licence
 * (EUPL) v.1.2 and is subject to its terms as set out below.
 *
 * If a copy of the EUPL was not distributed with this file, You can obtain
 * one at https://opensource.org/licenses/EUPL-1.2.
 *
 * The 'Compatible Licences' set out in the Appendix to the EUPL (as may be
 * amended by the European Commission) shall be deemed incompatible for
 * the purposes of the Work and the provisions of the compatibility
 * clause in Article 5 of the EUPL shall not apply.
 *
 * If using the Work as, or as part of, a network application, by
 * including the attribution notice(s) required under Article 5 of the EUPL
 * in the end user terms of the application under an appropriate heading,
 * such notice(s) shall fulfill the requirements of that article.
 * ********************************************************************* */

// The recorder. Every page loads it first, with a plain script tag and
// ahead of anything from the cloud, so nothing the page does afterwards
// escapes it. It wraps the two ways a page makes a request and the
// console, so a browser test can read what the page actually sent, what
// came back and what was logged, without a proxy in front of the cloud. A
// proxy would put the cloud on the page's own origin, and these pages
// exist to show the cloud as a third party to the page.
//
// Served unchanged by every language's copy of this demo. Do not edit one
// copy without the others, because the same tests read all of them.
(function () {
  var t = window.__51dTest = {
    requests: [],
    console: [],
    actions: [],
    changes: [],
    errors: [],
    altFired: false
  };
  function text(value) {
    if (typeof value === 'string') { return value; }
    try { return JSON.stringify(value); } catch (e) { return String(value); }
  }
  function record(kind, method, url, body) {
    var entry = {
      kind: kind,
      method: String(method || 'GET').toUpperCase(),
      url: String(url || ''),
      body: typeof body === 'string' ? body : '',
      status: 0,
      response: '',
      done: false
    };
    t.requests.push(entry);
    return entry;
  }
  var open = XMLHttpRequest.prototype.open;
  XMLHttpRequest.prototype.open = function (method, url) {
    this.__51dCall = { method: method, url: url };
    return open.apply(this, arguments);
  };
  var send = XMLHttpRequest.prototype.send;
  XMLHttpRequest.prototype.send = function (body) {
    var call = this.__51dCall || {};
    var entry = record('xhr', call.method, call.url, body);
    var xhr = this;
    this.addEventListener('loadend', function () {
      entry.status = xhr.status;
      try { entry.response = xhr.responseText || ''; }
      catch (e) { entry.response = ''; }
      entry.done = true;
    });
    return send.apply(this, arguments);
  };
  if (window.fetch) {
    var realFetch = window.fetch.bind(window);
    window.fetch = function (input, init) {
      var url = typeof input === 'string'
        ? input : (input && input.url) || '';
      var method = (init && init.method)
        || (input && input.method) || 'GET';
      var body = init && typeof init.body === 'string' ? init.body : '';
      var entry = record('fetch', method, url, body);
      return realFetch(input, init).then(function (response) {
        entry.status = response.status;
        entry.done = true;
        try {
          response.clone().text().then(function (value) {
            entry.response = value;
          }, function () { });
        } catch (e) { }
        return response;
      }, function (error) {
        entry.error = String(error);
        entry.done = true;
        throw error;
      });
    };
  }
  var levels = ['log', 'info', 'warn', 'error', 'debug'];
  for (var i = 0; i < levels.length; i++) {
    (function (level) {
      var real = console[level];
      console[level] = function () {
        var parts = [];
        for (var a = 0; a < arguments.length; a++) {
          parts.push(text(arguments[a]));
        }
        t.console.push({ level: level, text: parts.join(' ') });
        if (real) { real.apply(console, arguments); }
      };
    })(levels[i]);
  }
  window.addEventListener('error', function (e) {
    t.errors.push(String(e && e.message));
  });
})();
