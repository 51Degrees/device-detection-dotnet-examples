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
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.DevTools;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using System;
using System.Threading;
using System.Threading.Tasks;
using DevToolsSessionDomains = OpenQA.Selenium.DevTools.DevToolsSessionDomains;
// Used to map new version features.
using Enhanced = OpenQA.Selenium.DevTools.V131;

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
        /// </summary>
        protected WebDriver Driver { get; private set; }

        /// <summary>
        /// Expected name of the browser reported by device detection.
        /// </summary>
        protected string BrowserName;

        /// <summary>
        /// Expected browser version reported by device detection.
        /// </summary>
        protected Version BrowserVersion;

        /// <summary>
        /// Network adapter if supported by the driver.
        /// </summary>
        protected Enhanced.Network.NetworkAdapter Network { get; private set; }

        /// <summary>
        /// Used to create new network adapters.
        /// </summary>
        private static readonly Enhanced.Network.EnableCommandSettings 
            NetworkSettings = 
            new Enhanced.Network.EnableCommandSettings();

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
        /// Cleans up after the test. The driver is disposed here rather than in
        /// a [ClassCleanup] method because MSTest requires class level fixture
        /// methods to be static, which cannot reach the instance
        /// <see cref="Driver"/>. A class that declared a non-static
        /// [ClassInitialize] or [ClassCleanup] method was silently dropped at
        /// discovery, so none of its tests ran at all.
        /// </summary>
        [TestCleanup]
        public void TestCleanup()
        {
            if (Driver != null)
            {
                Driver.Quit();
                Driver.Dispose();
                Driver = null;
            }
            if (ServerTask != null)
            {
                StopSource.Cancel(true);
                ServerTask.Wait();
            }
        }


        /// <summary>
        /// Sets the <see cref="Driver"/> property for Chrome tests. If the 
        /// initilaization fails the test is flagged as inconclusive.
        /// </summary>
        protected void InitializeChromeDriver()
        {
            // If the driver and chrome versions are different it may cause
            // unexpected behaviour. 
            // See: https://sites.google.com/chromium.org/driver/downloads and
            // https://github.com/rosolko/WebDriverManager.Net
            var chromeOptions = new ChromeOptions();
            chromeOptions.AcceptInsecureCertificates = true;
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
        protected void InitializeEdgeDriver()
        {
            var edgeOptions = new EdgeOptions();
            edgeOptions.AcceptInsecureCertificates = true;
            edgeOptions.AddArgument("--headless=new");
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
        protected void InitializeFirefoxDriver()
        {
            var firefoxOptions = new FirefoxOptions();
            firefoxOptions.AcceptInsecureCertificates = true;
            firefoxOptions.AddArgument("--headless");
            firefoxOptions.EnableDevToolsProtocol = true;
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

        private static async Task<Enhanced.Network.NetworkAdapter> GetNetwork(
            IWebDriver driver)
        {
            DevToolsSessionDomains domains;
            try
            {
                domains = (driver as IDevTools).GetDevToolsSession()
                    .GetVersionSpecificDomains<DevToolsSessionDomains>();
            }
            catch (WebDriverException)
            {
                // The installed browser is newer than the DevTools protocol
                // versions this Selenium build knows about, so no session can be
                // started. Returning null leaves the tests that need the network
                // adapter to report themselves inconclusive rather than failing
                // every test in the class at driver creation.
                return null;
            }

            // If the dev tools support session network inspection then
            // initialize the network interface and add a reference to the
            // adapter.
            var modern = domains as Enhanced.DevToolsSessionDomains;
            if (modern != null)
            {
                await modern.Network.Enable(NetworkSettings);
                return modern.Network;
            }
            return null;
        }
    }
}
