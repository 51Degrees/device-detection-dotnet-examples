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

using OpenQA.Selenium.BiDi;
using OpenQA.Selenium.BiDi.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FiftyOne.DeviceDetection.Example.Tests.Web
{
    /// <summary>
    /// A thin cross browser network adapter built on the WebDriver BiDi
    /// protocol. It replaces the Chrome DevTools Protocol (CDP) adapter that
    /// only Chromium browsers implemented. Firefox (geckodriver) does not
    /// implement <c>IDevTools</c>, so the CDP path threw and failed the whole
    /// Firefox test class. BiDi is the W3C standard that Chrome, Edge and
    /// Firefox all support, so the same adapter now serves every browser.
    /// </summary>
    public sealed class BiDiNetworkAdapter
    {
        private readonly IBiDi _bidi;

        internal BiDiNetworkAdapter(IBiDi bidi)
        {
            _bidi = bidi;
        }

        /// <summary>
        /// Reads all cookies currently held by the browser.
        /// </summary>
        /// <returns>
        /// The cookies, each with its name and textual value.
        /// </returns>
        public async Task<IReadOnlyList<BiDiCookie>> GetAllCookiesAsync()
        {
            var result = await _bidi.Storage.GetCookiesAsync();
            return result.Cookies
                .Select(c => new BiDiCookie(c.Name, ReadValue(c.Value)))
                .ToList();
        }

        /// <summary>
        /// Subscribes to completed network responses. The returned task
        /// completes once the subscription is active, so callers should await
        /// it before navigating to be sure no response is missed.
        /// </summary>
        /// <param name="handler">
        /// Called for each completed response.
        /// </param>
        /// <returns>
        /// The subscription, which lives for as long as the BiDi session.
        /// </returns>
        public Task<ISubscription> OnResponseCompletedAsync(
            Action<BiDiResponse> handler)
        {
            return _bidi.Network.ResponseCompleted.SubscribeAsync(e =>
            {
                var headers = e.Response.Headers.ToDictionary(
                    h => h.Name.ToLowerInvariant(),
                    h => ReadValue(h.Value));
                handler(new BiDiResponse(
                    e.Response.Url,
                    e.Response.MimeType,
                    headers));
            });
        }

        /// <summary>
        /// Reads a BiDi <see cref="BytesValue"/> as text. The protocol carries
        /// a value either as a plain string or as base64, and both forms
        /// represent the same underlying text here.
        /// </summary>
        private static string ReadValue(BytesValue value)
        {
            switch (value)
            {
                case StringBytesValue s:
                    return s.Value;
                case Base64BytesValue b:
                    return Encoding.UTF8.GetString(b.Value.ToArray());
                default:
                    return value?.ToString();
            }
        }
    }

    /// <summary>
    /// A cookie as read over BiDi, reduced to the parts the tests use.
    /// </summary>
    public sealed class BiDiCookie
    {
        public BiDiCookie(string name, string value)
        {
            Name = name;
            Value = value;
        }

        /// <summary>
        /// The cookie name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The cookie value as text.
        /// </summary>
        public string Value { get; }
    }

    /// <summary>
    /// A completed network response as read over BiDi, reduced to the parts
    /// the tests use.
    /// </summary>
    public sealed class BiDiResponse
    {
        public BiDiResponse(
            string url,
            string mimeType,
            IReadOnlyDictionary<string, string> headers)
        {
            Url = url;
            MimeType = mimeType;
            Headers = headers;
        }

        /// <summary>
        /// The response URL.
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// The response MIME type.
        /// </summary>
        public string MimeType { get; }

        /// <summary>
        /// The response headers, keyed by lower case name.
        /// </summary>
        public IReadOnlyDictionary<string, string> Headers { get; }
    }
}
