/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2023 51 Degrees Mobile Experts Limited, Davidson House,
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

using FiftyOne.DeviceDetection.Examples;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.DeviceDetection.Example.Tests.Web
{
    public static class Parameters
    {
        public const string BASE_PROPERTIES = "HardwareVendor,HardwareName,DeviceType,PlatformVendor,PlatformName,PlatformVersion,BrowserVendor,BrowserName,BrowserVersion";
        public const string ALL_PROPERTIES = null;
        public const string BROWSER_PROPERTIES = BASE_PROPERTIES + ",SetHeaderBrowserAccept-CH";
        public const string HARDWARE_PROPERTIES = BASE_PROPERTIES + ",SetHeaderHardwareAccept-CH";
        public const string PLATFORM_PROPERTIES = BASE_PROPERTIES + ",SetHeaderPlatformAccept-CH";

        public const string CHROME_UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/95.0.4638.69 Safari/537.36";
        public const string EDGE_UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/95.0.4638.69 Safari/537.36 Edg/95.0.1020.44";
        public const string FIREFOX_UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:94.0) Gecko/20100101 Firefox/94.0";
        public const string SAFARI_UA = "Mozilla/5.0 (iPhone; CPU iPhone OS 15_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.0 Mobile/15E148 Safari/604.1";
        public const string CURL_UA = "curl/7.80.0";

        /// <summary>
        /// Lists of the most critical headers for each component. Tests will fail if these are
        /// not included in Accept-CH when expected.
        /// </summary>
        public static readonly List<string> BROWSER_ACCEPT_CH = new List<string>()
        {
            "Sec-CH-UA"
        };
        public static readonly List<string> HARDWARE_ACCEPT_CH = new List<string>()
        {
            "Sec-CH-UA-Model"
        };
        public static readonly List<string> PLATFORM_ACCEPT_CH = new List<string>()
        {
            "Sec-CH-UA-Platform"
        };

        /// <summary>
        /// Protocol, domain, and port base URLs on which the example is 
        /// listening for SSL connections. <see cref="Parameters"/>
        /// </summary>
        public static IEnumerable<object[]> HttpsUrlsData =>
            Constants.LOCALHOST_HTTPS_PORTS.Select(i =>
                new object[] { $"https://localhost:{i}" }).ToArray();

        /// <summary>
        /// Protocol, domain, and port base URLs on which the example is 
        /// listening. <see cref="Parameters"/>
        /// </summary>
        public static IEnumerable<object[]> AllUrlsData =>
            Constants.LOCALHOST_HTTP_PORTS.Select(i =>
                new object[] { $"http://localhost:{i}" }).Concat(
                HttpsUrlsData).ToArray();
    }
}
