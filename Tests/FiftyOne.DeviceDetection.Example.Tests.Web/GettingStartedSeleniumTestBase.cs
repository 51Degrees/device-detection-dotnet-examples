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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.DeviceDetection.Example.Tests.Web
{
    public class GettingStartedSeleniumTestBase : SeleniumTestsBase
    {
        /// <summary>
        /// Paths used for testing.
        /// </summary>
        private const string STATIC_HTML_PATH = "/static.html";
        private const string TEST_PAGE_PATH = "/testpage.html";

        public GettingStartedSeleniumTestBase(
                Func<CancellationToken, Task> startServer) : base(startServer)
        {
        }

        /// <summary>
        /// Verifies that the 51d cookie is populated.
        /// </summary>
        /// <param name="url"></param>
        [DataTestMethod]
        [DynamicData(nameof(Parameters.HttpsUrlsData), typeof(Parameters))]
        public void VerifyExample_GetHighEntropyValues_Populates_51D_Cookie(string url)
        {
            if (Network == null)
            {
                Assert.Inconclusive(
                    "Test session does not support cookie verification");
            }

            // Act
            Driver.Navigate().GoToUrl(url + STATIC_HTML_PATH);

            // Wait for the page to load
            new WebDriverWait(Driver, TEST_TIMEOUT).Until(driver => true);

            // Get the high entropy values.
            var js = (IJavaScriptExecutor)Driver;
            var ghe = (Dictionary<string, object>)js.ExecuteScript(
                "return ghe");
            foreach (var key in new[] {
                "brands",
                "fullVersionList",
                "mobile",
                "model",
                "platform",
                "platformVersion"})
            {
                Assert.IsTrue(ghe.ContainsKey(key));
                Assert.IsNotNull(ghe[key]);
            }

            var cookies = Network.GetAllCookies().Result;
            var fod_cookie = cookies.Cookies.Where(c =>
                c.Name == "51D_GetHighEntropyValues").Single();

            // Assert

            // Turn the cookie into a byte array.
            Assert.IsNotNull(fod_cookie);
            var bytes = Convert.FromBase64String(fod_cookie.Value);
            Assert.IsNotNull(bytes);
            Assert.IsTrue(bytes.Length > 0);

            // Turn the byte array into json.
            var json = ASCIIEncoding.ASCII.GetString(bytes);
            Assert.IsNotNull(json);

            // Turn the json into a dictionary of key and value pairs.
            var map = JsonSerializer.Deserialize<Dictionary<string, object>>(
                json);
            Assert.IsNotNull(map);
            Assert.IsTrue(ghe.All(i => map.ContainsKey(i.Key)));
        }

        [DataTestMethod]
        [DynamicData(nameof(Parameters.HttpsUrlsData), typeof(Parameters))]
        public void VerifyExample_GetHighEntropyValues_Contains_CORS_Response_Header(string url)
        {
            const string KEY = "access-control-allow-origin";

            if (Network == null)
            {
                Assert.Inconclusive(
                    "Test session does not support CORS verification");
            }

            // The header value pairs from the JSON response.
            Dictionary<string, string> headerValuePairs = new();

            // Set to true when the JSON response is recieved.
            var jsonRecieved = false;

            // Get Response Headers if the URL relates to a JSON response.
            Network.ResponseReceived += (sender, e) =>
            {
                var headers = e.Response.Headers;
                var responseUrl = e.Response.Url;
                var mimeType = e.Response.MimeType;
                if ("application/json".Equals(mimeType) &&
                    responseUrl.EndsWith("json"))
                {
                    foreach (var header in headers)
                    {
                        headerValuePairs.Add(header.Key.ToLower(), header.Value);
                    }
                    jsonRecieved = true;
                }
            };

            // Act
            // Do a cross origin request
            Driver.Navigate().GoToUrl(url + STATIC_HTML_PATH);

            // Wait for the page to load
            new WebDriverWait(Driver, TEST_TIMEOUT).Until(driver =>
            {
                return jsonRecieved;
            });

            // Assert
            // Verify that the response contains the header
            Assert.IsTrue(headerValuePairs.ContainsKey(KEY));
            Assert.IsTrue(
                headerValuePairs[KEY].Equals(url) ||
                headerValuePairs[KEY].Equals("*"));
        }

        [DataTestMethod]
        [DynamicData(nameof(Parameters.HttpsUrlsData), typeof(Parameters))]
        public void VerifyExample_GetHighEntropyValues_Fod_Completes(string url)
        {
            // Act
            Driver.Navigate().GoToUrl(url + TEST_PAGE_PATH);

            string detectedBrowserName = null;
            string detectedBrowserVersion = null;
            string userAgent = null;

            // This throws an exception if the timeout period elapses,
            // which will cause the test to fail.
            var result = new WebDriverWait(Driver, TEST_TIMEOUT).Until(
                driver =>
                {
                    // Gets the value of the global JavaScript variable test
                    // from the TEST_PAGE_ENDPOINT HTML page. Checks this value
                    // is 'complete' to indiciate that the complete event
                    // fired.
                    var js = (IJavaScriptExecutor)driver;
                    var test = js.ExecuteScript("return test");

                    // Get the browser name and version from device detection
                    // as returned in the complete event.
                    if (test.Equals("complete"))
                    {
                        userAgent = (string)js.ExecuteScript(
                            "return navigator.userAgent");
                        detectedBrowserName = (string)js.ExecuteScript(
                            "return browserName");
                        detectedBrowserVersion = (string)js.ExecuteScript(
                            "return browserVersion");
                        return true;
                    }
                    return false;
                });

            // Assert
            Assert.IsTrue(result);
            Assert.IsNotNull(detectedBrowserName);
            Assert.IsNotNull(detectedBrowserVersion);

            // Check the reported browser name contains the expected one.
            Assert.IsTrue(detectedBrowserName.Contains(
                BrowserName,
                StringComparison.InvariantCultureIgnoreCase),
                $"Expected '{BrowserName}' to be present in '{detectedBrowserName}'");

            // Check the major browser information is the same.
            var version = ParseVersion(detectedBrowserVersion);
            Assert.AreEqual(BrowserVersion.Major, version.Major);
        }

        /// <summary>
        /// Turns the device detection browser version into a version instance.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private static Version ParseVersion(string value)
        {
            var numbers = value.Split(".").Select(i => 
                int.Parse(i)).ToArray();
            switch(numbers.Length)
            {
                case 1:
                    return new Version(numbers[0], 0);
                case 2:
                    return new Version(numbers[0], numbers[1]);
                case 3:
                    return new Version(numbers[0], numbers[1], numbers[2]);
                case 4:
                    return new Version(numbers[0], numbers[1], numbers[2], 
                        numbers[3]);
                default:
                    throw new ArgumentException($"'{value}' invalid version");
            }
        }
    }
}
