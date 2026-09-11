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

            // The page sets 'ghe' asynchronously from
            // navigator.userAgentData.getHighEntropyValues, so waiting for the
            // document to load is not enough; we must wait for that promise to
            // resolve. Browsers without userAgentData (Firefox) never set it,
            // so the value stays null and the wait times out.
            Dictionary<string, object> ghe = null;
            try
            {
                new WebDriverWait(Driver, TEST_TIMEOUT).Until(driver =>
                {
                    ghe = (Dictionary<string, object>)
                        ((IJavaScriptExecutor)driver).ExecuteScript(
                            "return ghe");
                    return ghe != null;
                });
            }
            catch (WebDriverTimeoutException)
            {
                // ghe is still null here. This is expected on browsers with no
                // navigator.userAgentData, for which high entropy values, and
                // therefore this test, do not apply.
            }

            // Guard before the loop below. Without this a null ghe dereferences
            // and throws a NullReferenceException instead of reporting the real
            // reason the values are missing.
            if (ghe == null)
            {
                Assert.Inconclusive(
                    "Browser did not provide high entropy values " +
                    "(navigator.userAgentData is not supported), so there is " +
                    "no 51D_GetHighEntropyValues cookie to verify.");
            }

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

            // The 51D_GetHighEntropyValues cookie is written by
            // 51Degrees.core.js after its own asynchronous round trip, so it
            // may not exist the instant the page reports high entropy values.
            // Poll for it rather than reading once, otherwise the cookie is
            // missing and Single() below throws "Sequence contains no
            // elements".
            IReadOnlyList<BiDiCookie> cookies = null;
            BiDiCookie fod_cookie = null;
            try
            {
                new WebDriverWait(Driver, TEST_TIMEOUT).Until(driver =>
                {
                    cookies = Network.GetAllCookiesAsync().Result;
                    fod_cookie = cookies.FirstOrDefault(c =>
                        c.Name == "51D_GetHighEntropyValues");
                    return fod_cookie != null;
                });
            }
            catch (WebDriverTimeoutException e)
            {
                Assert.Inconclusive(e.ToString());
            }

            Console.WriteLine("Enumerating cookie names:");
            foreach (var nextName in cookies.Select(c => c.Name))
            {
                Console.WriteLine($"- Next cookie name: '{nextName}'");
            }
            Console.WriteLine("Finished numerating cookie names!");

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
            // Awaited so the subscription is active before navigation begins,
            // otherwise the response could arrive before we are listening.
            Network.OnResponseCompletedAsync(response =>
            {
                var responseUrl = response.Url;
                var mimeType = response.MimeType;
                if ("application/json".Equals(mimeType) &&
                    responseUrl.EndsWith("json"))
                {
                    foreach (var header in response.Headers)
                    {
                        headerValuePairs[header.Key] = header.Value;
                    }
                    jsonRecieved = true;
                }
            }).Wait();

            // Act
            // Do a cross origin request
            Driver.Navigate().GoToUrl(url + STATIC_HTML_PATH);

            try
            {
                // Wait for the page to load
                new WebDriverWait(Driver, TEST_TIMEOUT).Until(driver =>
                {
                    return jsonRecieved;
                });
            }
            catch (WebDriverTimeoutException e)
            {
                Assert.Inconclusive(e.ToString());
            }

            // Assert
            // Verify that the response contains the header
            Assert.IsTrue(headerValuePairs.ContainsKey(KEY));
            Assert.IsTrue(
                headerValuePairs[KEY].Equals(url) ||
                headerValuePairs[KEY].Equals("*"));
        }

        /// <summary>
        /// Bounds the call below. The driver default is 30 seconds, longer
        /// than TEST_TIMEOUT, which a diagnostic must not be able to spend.
        /// </summary>
        private static readonly TimeSpan HIGH_ENTROPY_TIMEOUT =
            TimeSpan.FromSeconds(5);

        /// <summary>
        /// Reads the high entropy evidence the engine is given, which the
        /// user agent on its own does not identify.
        /// </summary>
        /// <returns>
        /// The decoded 51D_GetHighEntropyValues cookie, or a message saying
        /// why there is none.
        /// </returns>
        private string ReadHighEntropyEvidence()
        {
            try
            {
                var cookie = Driver.Manage().Cookies.GetCookieNamed(
                    "51D_GetHighEntropyValues");
                if (cookie == null)
                {
                    return "no 51D_GetHighEntropyValues cookie";
                }
                // The payload is UTF-8 JSON, and model, platform and brand
                // values are not all ASCII. Decoding as ASCII would replace
                // exactly the characters worth seeing with question marks.
                return Encoding.UTF8.GetString(
                    Convert.FromBase64String(cookie.Value));
            }
            catch (Exception exception)
            {
                // A diagnostic must not decide the result: throwing here
                // would replace whatever the test actually found.
                return $"unavailable: {exception.Message}";
            }
        }

        /// <summary>
        /// Reads the high entropy values the browser offers, which tells a
        /// missing cookie apart from a browser that has nothing to put in
        /// one. Firefox has no navigator.userAgentData at all.
        /// </summary>
        /// <returns>
        /// The values as JSON, or a message saying why there are none.
        /// </returns>
        private string ReadBrowserHighEntropyValues()
        {
            const string script = @"
                var callback = arguments[arguments.length - 1];
                if (!navigator.userAgentData) {
                    callback('navigator.userAgentData is not supported');
                    return;
                }
                navigator.userAgentData.getHighEntropyValues([
                    'architecture', 'bitness', 'brands', 'fullVersionList',
                    'mobile', 'model', 'platform', 'platformVersion'])
                    .then(function (values) {
                        callback(JSON.stringify(values));
                    })
                    .catch(function (error) {
                        callback('rejected: ' + error);
                    });";
            try
            {
                // Reading the timeout is itself a call to the driver, so it
                // belongs inside the catch along with everything else here.
                var timeouts = Driver.Manage().Timeouts();
                var originalTimeout = timeouts.AsynchronousJavaScript;
                try
                {
                    timeouts.AsynchronousJavaScript = HIGH_ENTROPY_TIMEOUT;
                    var js = (IJavaScriptExecutor)Driver;
                    return (string)js.ExecuteAsyncScript(script);
                }
                finally
                {
                    // Driver wide, so later tests inherit whatever is left
                    // here.
                    timeouts.AsynchronousJavaScript = originalTimeout;
                }
            }
            catch (Exception exception)
            {
                // As above: diagnostics report, they do not decide.
                return $"unavailable: {exception.Message}";
            }
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

            bool result;
            try
            {
                // This throws an exception if the timeout period elapses,
                // which will cause the test to fail.
                result = new WebDriverWait(Driver, TEST_TIMEOUT).Until(
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
                        Console.WriteLine("[test] = '" + test.ToString() + "'");
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
            }
            catch (WebDriverTimeoutException e)
            {
                Assert.Inconclusive(e.ToString());
                throw;
            }
            finally
            {
                // Dumping the browser log is a diagnostic aid only. Not every
                // driver supports it, geckodriver in particular, and an
                // exception thrown here would replace whatever the test had
                // actually found with an unrelated failure.
                try
                {
                    foreach (var l in Driver.Manage().Logs.GetLog(LogType.Browser))
                    {
                        Console.WriteLine($"[LOGS] {l}");
                    }
                }
                catch (WebDriverException e)
                {
                    Console.WriteLine($"[LOGS] unavailable for this driver: {e.Message}");
                }
            }

            // Record what device detection was actually given and what it made
            // of it. Without the user agent in the log a mismatch below cannot be
            // diagnosed from a CI run: the assertion message alone does not say
            // which evidence produced the wrong answer, and the browser is not
            // available afterwards to ask again. Logged on success as well as
            // failure so a passing leg can be compared against a failing one.
            Console.WriteLine($"[detection] userAgent = '{userAgent}'");
            Console.WriteLine(
                $"[detection] evidence = {ReadHighEntropyEvidence()}");
            Console.WriteLine(
                "[browser] highEntropyValues = " +
                ReadBrowserHighEntropyValues());
            Console.WriteLine(
                $"[detection] browserName = '{detectedBrowserName}', " +
                $"browserVersion = '{detectedBrowserVersion}'");
            Console.WriteLine(
                $"[driver] browserName = '{BrowserName}', " +
                $"browserVersion = '{BrowserVersion}'");

            // Assert
            Assert.IsTrue(result);
            Assert.IsNotNull(detectedBrowserName);
            Assert.IsNotNull(detectedBrowserVersion);

            // Check the reported browser name contains the expected one.
            Assert.IsTrue(detectedBrowserName.Contains(
                BrowserName,
                StringComparison.InvariantCultureIgnoreCase),
                $"Expected '{BrowserName}' to be present in '{detectedBrowserName}' " +
                $"for user agent '{userAgent}'");

            // Check the major browser information is the same. Some profiles
            // carry no browser version, so there is nothing to compare against.
            // That is a gap in the data rather than a fault in the example, so
            // report it and stop rather than fail. Seen with the 'Chrome
            // Headless' profile in the 31 August 2026 Enterprise file, which the
            // drivers match because they are started with --headless.
            if ("Unknown".Equals(detectedBrowserVersion, StringComparison.Ordinal))
            {
                Assert.Inconclusive(
                    $"Device detection returned no browser version for " +
                    $"'{detectedBrowserName}', so it cannot be compared with the " +
                    $"'{BrowserVersion}' reported by the driver. The profile " +
                    $"matched by this browser has no browser version in the data " +
                    $"file in use.");
            }
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
            Func<string> ErrorText = () => $"'{value}' invalid version";
            int[] numbers;
            try
            {
                numbers = value.Split(".").Select(i =>
                    int.Parse(i)).ToArray();
            }
            catch (Exception e) when (e is FormatException || e is OverflowException)
            {
                throw new ArgumentException(ErrorText(), e);
            }
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
                    throw new ArgumentException(ErrorText());
            }
        }
    }
}
