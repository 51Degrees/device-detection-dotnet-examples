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
using OpenQA.Selenium.BiDi;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.DeviceDetection.Example.Tests.Web
{
    public class SeleniumTestsBase
    {
        /// <summary>
        /// Number of seconds to wait for a response that might satisfy the 
        /// test.
        /// </summary>
        protected static readonly TimeSpan TEST_TIMEOUT = 
            TimeSpan.FromSeconds(20);

        /// <summary>
        /// The driver being used for the active test. See 
        /// <see cref="InitializeChromeDriver"/>,
        /// <see cref="InitializeEdgeDriver"/>,
        /// <see cref="InitializeFirefoxDriver"/>.
        ///
        /// IMPORTANT: The driver and the browser state below are static and are
        /// created once per test class from a static [ClassInitialize] method,
        /// then disposed from a static [ClassCleanup] method. This is
        /// deliberate. The CI environment cannot start more than one driver in
        /// the same session, so a per-test [TestInitialize] driver fails the
        /// build. MSTest also requires class level fixture methods to be static,
        /// which is why these members must be static for [ClassInitialize] to
        /// reach them. Do NOT change these back to instance members driven from
        /// [TestInitialize]; doing so reintroduces the CI failure.
        /// </summary>
        protected static WebDriver Driver { get; private set; }

        /// <summary>
        /// Expected name of the browser reported by device detection.
        /// </summary>
        protected static string BrowserName;

        /// <summary>
        /// Expected browser version reported by device detection.
        /// </summary>
        protected static Version BrowserVersion;

        /// <summary>
        /// Cross browser network adapter, built on the WebDriver BiDi protocol
        /// so Chrome, Edge and Firefox all get real network inspection. Null
        /// only if a BiDi session could not be established, in which case the
        /// tests that need it report themselves inconclusive.
        /// </summary>
        protected static BiDiNetworkAdapter Network { get; private set; }

        /// <summary>
        /// Used to stop the server when the test is finished.
        /// </summary>
        private readonly CancellationTokenSource StopSource = 
            new CancellationTokenSource();

        /// <summary>
        /// Function used to start the web server under test.
        /// </summary>
        private readonly Func<CancellationToken, Task> StartServerFunc;

        /// <summary>
        /// The task that is running the server.
        /// </summary>
        private Task ServerTask { get; set; }


        public SeleniumTestsBase(Func<CancellationToken, Task> startServer)
        {
            StartServerFunc = startServer;
        }

        [TestInitialize]
        public void TestServerInitialize()
        {
            ServerTask = StartServerFunc(StopSource.Token);
        }

        /// <summary>
        /// Stops the per-test web server. The server is started once per test
        /// in <see cref="TestServerInitialize"/>, so it is stopped here. The
        /// driver is not touched here; it lives for the whole class and is
        /// disposed in <see cref="ClassCleanup"/>.
        /// </summary>
        [TestCleanup]
        public void TestCleanup()
        {
            if (ServerTask != null)
            {
                StopSource.Cancel(true);
                ServerTask.Wait();
            }
        }

        /// <summary>
        /// Disposes the driver created once for the class. This must be a static
        /// [ClassCleanup] method because MSTest requires class level fixture
        /// methods to be static, and because the CI environment cannot start
        /// more than one driver in the same session, so the driver is created
        /// once per class rather than once per test. Do NOT move this teardown
        /// into [TestCleanup]; it pairs with the static [ClassInitialize] on
        /// each browser test class.
        /// </summary>
        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (Driver != null)
            {
                Driver.Quit();
                Driver.Dispose();
                Driver = null;
            }
        }


        /// <summary>
        /// Sets the <see cref="Driver"/> property for Chrome tests. If the 
        /// initilaization fails the test is flagged as inconclusive.
        /// </summary>
        protected static void InitializeChromeDriver()
        {
            // If the driver and chrome versions are different it may cause
            // unexpected behaviour. 
            // See: https://sites.google.com/chromium.org/driver/downloads and
            // https://github.com/rosolko/WebDriverManager.Net
            var chromeOptions = new ChromeOptions();
            chromeOptions.AcceptInsecureCertificates = true;
            // Ask the driver for the BiDi WebSocket URL so the cross browser
            // network adapter can attach. Without this AsBiDiAsync has no
            // endpoint to connect to.
            chromeOptions.UseWebSocketUrl = true;
            chromeOptions.AddArgument("--headless=new");
            chromeOptions.AddArgument("--ignore-certificate-errors");
            chromeOptions.SetLoggingPreference(LogType.Browser, LogLevel.All);
            try
            {
                Driver = new ChromeDriver(chromeOptions);
            }
            catch (WebDriverException exception)
            {
                SkipBecauseBrowserUnavailable("Chrome", exception);
            }
            Network = GetNetwork(Driver).Result;
            BrowserName = "Chrome";
            BrowserVersion = Version.Parse(
                (string)Driver.Capabilities["browserVersion"]);
        }

        /// <summary>
        /// Sets the <see cref="Driver"/> property for Edge tests. If the 
        /// initilaization fails the test is flagged as inconclusive.
        /// </summary>
        protected static void InitializeEdgeDriver()
        {
            var edgeOptions = new EdgeOptions();
            edgeOptions.AcceptInsecureCertificates = true;
            // See the Chrome initializer: enables the BiDi WebSocket endpoint.
            edgeOptions.UseWebSocketUrl = true;
            edgeOptions.AddArgument("--headless=new");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) == true)
            {
                // Ubuntu 24.04 confines unprivileged user namespaces with
                // AppArmor, and the Edge package ships no profile of its own,
                // so the sandbox cannot start and the browser exits during
                // session creation. Chrome ships a profile, which is why only
                // Edge needs this.
                edgeOptions.AddArgument("--no-sandbox");
            }
            edgeOptions.SetLoggingPreference(LogType.Browser, LogLevel.All);
            try
            {
                Driver = new EdgeDriver(edgeOptions);
            }
            catch (WebDriverException exception)
            {
                SkipBecauseBrowserUnavailable("Edge", exception);
            }
            Network = GetNetwork(Driver).Result;
            BrowserName = "Edge";
            BrowserVersion = Version.Parse(
                (string)Driver.Capabilities["browserVersion"]);
        }

        /// <summary>
        /// Sets the <see cref="Driver"/> property for Firefox tests. If the 
        /// initilaization fails the test is flagged as inconclusive.
        /// </summary>
        protected static void InitializeFirefoxDriver()
        {
            var firefoxOptions = new FirefoxOptions();
            firefoxOptions.AcceptInsecureCertificates = true;
            firefoxOptions.AddArgument("--headless");
            // Firefox does not implement the Chrome DevTools Protocol, so the
            // network adapter uses the W3C BiDi protocol instead. Ask for the
            // BiDi WebSocket URL so AsBiDiAsync can attach.
            firefoxOptions.UseWebSocketUrl = true;
            firefoxOptions.SetLoggingPreference(LogType.Browser, LogLevel.All);
            try
            {
                Driver = new FirefoxDriver(firefoxOptions);
            }
            catch (WebDriverException exception)
            {
                SkipBecauseBrowserUnavailable("Firefox", exception);
            }
            Network = GetNetwork(Driver).Result;
            BrowserName = "Firefox";
            BrowserVersion = Version.Parse(
                (string)Driver.Capabilities["browserVersion"]);
        }

        /// <summary>
        /// Skips the test because the browser it needs could not be
        /// started, saying which browser it was and what the driver
        /// reported. The old messages named the wrong browser, so somebody
        /// reading a skipped Firefox test was told to install the Edge
        /// driver, and the reason the driver refused was thrown away
        /// entirely. A skip nobody can act on is no better than a test that
        /// never ran.
        /// </summary>
        /// <param name="browserName">
        /// The browser the test needs, for example "Chrome".
        /// </param>
        /// <param name="exception">
        /// What the driver threw.
        /// </param>
        protected static void SkipBecauseBrowserUnavailable(
            string browserName,
            WebDriverException exception)
        {
            var message =
                $"Skipped because a {browserName} driver could not be " +
                $"started, so this test did not run. Install {browserName} " +
                "and let Selenium Manager fetch the matching driver, or " +
                "put the driver on the PATH. The driver reported: " +
                exception.Message;

            // Written to the console as well as carried on the result,
            // because the console logger shows only the word "Skipped" and
            // a person looking at a build needs the reason.
            Console.WriteLine(message);
            Assert.Inconclusive(message);
        }

        /// <summary>
        /// Attaches a cross browser network adapter to the driver using the
        /// W3C WebDriver BiDi protocol. This replaces the old Chrome DevTools
        /// Protocol path, which only Chromium browsers implemented and which
        /// threw for Firefox because it does not implement <c>IDevTools</c>.
        /// BiDi is supported by Chrome, Edge and Firefox alike, so every
        /// browser now gets real network inspection.
        /// </summary>
        /// <param name="driver">
        /// The driver, which must have been created with
        /// <c>UseWebSocketUrl = true</c> so a BiDi session can be established.
        /// </param>
        /// <returns>
        /// The adapter, or null if a BiDi session could not be established, in
        /// which case the tests that need it report themselves inconclusive
        /// rather than failing every test in the class.
        /// </returns>
        private static async Task<BiDiNetworkAdapter> GetNetwork(
            IWebDriver driver)
        {
            try
            {
                var bidi = await driver.AsBiDiAsync();
                return new BiDiNetworkAdapter(bidi);
            }
            catch (Exception)
            {
                // The driver could not expose a BiDi session, for example
                // because it was created without UseWebSocketUrl. Returning
                // null leaves the network dependent tests inconclusive rather
                // than failing the whole class at driver creation.
                return null;
            }
        }
    }
}
