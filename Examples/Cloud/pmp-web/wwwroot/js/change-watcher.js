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

// The change watcher. It registers a handler on the client script's object
// as soon as that object exists, so a visitor changing their answer can be
// seen reaching page code in window.__51dTest.changes. It waits for the
// object rather than assuming the script has finished, because the client
// script tag is asynchronous. It looks for the default object name, fod,
// because no page that carries it names another.
//
// This is the one piece of publisher code on any page of this demo.
// Served unchanged by every language's copy of this demo.
(function () {
  var waiting = setInterval(function () {
    var o = window['fod'];
    if (!o || typeof o.onChange !== 'function') { return; }
    clearInterval(waiting);
    o.onChange(function (data) {
      window.__51dTest.changes.push(
        data && data.fodid ? data.fodid : null);
    });
  }, 20);
})();
