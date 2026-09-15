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

// A stand-in consent management platform. It is the stub the Transparency
// and Consent Framework's own specification tells every publisher to put
// in the page, and a pretend platform behind it that answers only what the
// specification requires, which is ping, addEventListener and a callback
// carrying tcString and eventStatus.
//
// It delivers nothing until window.__51dCmp.deliver() is called, so the
// test decides the order rather than a timer. The string it delivers grants
// consent for purposes 1 to 12 and nothing else, which the server reads as
// the personalized usage. It is 24 bytes with the purpose consent bits set
// from bit 152, written as base64url with no padding.
//
// Served unchanged by every language's copy of this demo.
(function () {
  // The stub from the framework's specification, in shape. It queues calls
  // until the platform itself is ready.
  var queue = [];
  window.__tcfapi = function () { queue.push(arguments); };
  var listeners = [];
  var delivered = null;
  function ping(callback) {
    callback({
      gdprApplies: true,
      cmpLoaded: true,
      cmpStatus: 'loaded',
      displayStatus: 'hidden',
      apiVersion: '2.2'
    }, true);
  }
  function api(command, version, callback, parameter) {
    if (command === 'ping') { ping(callback); return; }
    if (command === 'addEventListener') {
      listeners.push(callback);
      callback({
        tcString: '',
        eventStatus: 'cmpuishown',
        gdprApplies: true,
        listenerId: listeners.length
      }, true);
      if (delivered) { callback(delivered, true); }
      return;
    }
    if (command === 'removeEventListener') {
      listeners[parameter - 1] = null;
      callback(true, true);
      return;
    }
    callback(null, false);
  }
  window.__51dCmp = {
    deliver: function () {
      delivered = {
        tcString: 'AAAAAAAAAAAAAAAAAAAAAAAAAP_wAAAA',
        eventStatus: 'useractioncomplete',
        gdprApplies: true
      };
      for (var i = 0; i < listeners.length; i++) {
        if (listeners[i]) { listeners[i](delivered, true); }
      }
      return listeners.length;
    },
    listeners: function () { return listeners.length; }
  };
  // The platform takes over from the stub and drains what queued.
  window.__tcfapi = api;
  for (var i = 0; i < queue.length; i++) {
    api.apply(null, queue[i]);
  }
})();
