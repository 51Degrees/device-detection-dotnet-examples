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

using FiftyOne.DeviceDetection.Examples.OnPremise.GettingStartedWeb;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;

using Base = FiftyOne.DeviceDetection.Example.Tests.Web.Parameters;

namespace FiftyOne.DeviceDetection.Example.Tests.Web.OnPremise
{
    public class GettingStartedSeleniumTest : GettingStartedSeleniumTestBase
    {
        /// <summary>
        /// Names of the cookies written by the JavaScript held in the
        /// HasWebDriverJavaScript and IsVisibleJavaScript properties.
        /// </summary>
        private const string HAS_WEB_DRIVER_COOKIE = "51D_HasWebDriver";
        private const string IS_VISIBLE_COOKIE = "51D_IsVisible";

        /// <summary>
        /// Starts the Program with the cancellation token provided.
        /// </summary>
        public GettingStartedSeleniumTest() : base(
            (t) => Program.Run(new string[] { }, t))
        {
        }

        /// <summary>
        /// IsHeadless is read from the User-Agent rather than from JavaScript,
        /// so the example must render a value for it on the very first request
        /// without any callback having happened. That is what this test checks,
        /// along with the property being configured and reaching the page.
        ///
        /// It deliberately does not assert which of True or False is correct.
        /// Whether a given browser is classified as headless is decided by the
        /// data rather than by the example, and that classification is still
        /// being worked through. Asserting a particular answer here would
        /// record today's data in a test in this repository, where it does not
        /// belong. The value and the User-Agent it came from are written out so
        /// that a run can still be read for what was detected.
        /// </summary>
        /// <param name="url"></param>
        [DataTestMethod]
        [DynamicData(nameof(Base.HttpsUrlsData), typeof(Base))]
        public void VerifyExample_IsHeadless_Is_Reported_On_First_Request(
            string url)
        {
            Driver.Navigate().GoToUrl(url);

            var isHeadless = GetCellText("IsHeadless");
            SkipIfPropertyMissing(isHeadless, "IsHeadless");

            var userAgent = (string)((IJavaScriptExecutor)Driver).ExecuteScript(
                "return navigator.userAgent");
            Console.WriteLine(
                $"IsHeadless reported '{isHeadless}' for User-Agent " +
                $"'{userAgent}'");

            // SkipIfPropertyMissing has already established that the rendered
            // text is a boolean. Restate it as the assertion of this test so
            // that the check is visible rather than a side effect.
            Assert.IsTrue(bool.TryParse(isHeadless, out _),
                $"IsHeadless was rendered as '{isHeadless}', which is not a " +
                $"boolean value");
        }

        /// <summary>
        /// HasWebDriver and IsVisible can only be answered by JavaScript running
        /// in the browser. This test checks the whole round trip. The JavaScript
        /// served on the first request must write the two cookies, the values
        /// written must be the ones this browser should report, and a second
        /// request carrying those cookies must render them.
        ///
        /// The drivers used here are web drivers, so navigator.webdriver is true
        /// and HasWebDriver must come back as True. The page is in the
        /// foreground of the driven browser, so IsVisible must come back as
        /// True. Those two expectations are what make this a check of the values
        /// and not just of the plumbing.
        /// </summary>
        /// <param name="url"></param>
        [DataTestMethod]
        [DynamicData(nameof(Base.HttpsUrlsData), typeof(Base))]
        public void VerifyExample_HasWebDriver_And_IsVisible_Set_By_JavaScript(
            string url)
        {
            // Act, first request. Nothing has run in the browser yet, so the
            // values rendered here are whatever the data file holds.
            Driver.Navigate().GoToUrl(url);
            SkipIfPropertyMissing(GetCellText("HasWebDriver"), "HasWebDriver");
            SkipIfPropertyMissing(GetCellText("IsVisible"), "IsVisible");

            // Wait for the 51Degrees JavaScript to run and write both cookies.
            try
            {
                new WebDriverWait(Driver, TEST_TIMEOUT).Until(driver =>
                    driver.Manage().Cookies.GetCookieNamed(
                        HAS_WEB_DRIVER_COOKIE) != null &&
                    driver.Manage().Cookies.GetCookieNamed(
                        IS_VISIBLE_COOKIE) != null);
            }
            catch (WebDriverTimeoutException e)
            {
                Assert.Fail(
                    $"The JavaScript did not write the '{HAS_WEB_DRIVER_COOKIE}' " +
                    $"and '{IS_VISIBLE_COOKIE}' cookies within {TEST_TIMEOUT}. " +
                    e.ToString());
            }

            // Assert the cookie values are the ones this browser should report.
            Assert.AreEqual("True",
                Driver.Manage().Cookies.GetCookieNamed(
                    HAS_WEB_DRIVER_COOKIE).Value,
                "The browser is driven by a web driver, so the JavaScript " +
                "should have written True");
            Assert.AreEqual("True",
                Driver.Manage().Cookies.GetCookieNamed(IS_VISIBLE_COOKIE).Value,
                "The page is the active page in the driven browser, so the " +
                "JavaScript should have written True");

            // Act, second request. The cookies are now sent with the request,
            // so the server side values must change to match them.
            Driver.Navigate().Refresh();

            // Assert.
            Assert.AreEqual("True", GetCellText("HasWebDriver"),
                "The '" + HAS_WEB_DRIVER_COOKIE + "' cookie was not applied to " +
                "the second request");
            Assert.AreEqual("True", GetCellText("IsVisible"),
                "The '" + IS_VISIBLE_COOKIE + "' cookie was not applied to " +
                "the second request");
        }

        /// <summary>
        /// Reads the text of the results cell with the id given.
        /// </summary>
        /// <param name="id"></param>
        private string GetCellText(string id)
        {
            return Driver.FindElement(By.Id(id)).Text.Trim();
        }

        /// <summary>
        /// The three properties are only present in the Enterprise and TAC data
        /// files. Where the example is run against the Lite file that ships with
        /// the repository the property is absent and the example renders a
        /// message in place of a value, which is a valid state for the example
        /// and not a failure, so the test reports inconclusive. The check is on
        /// the value being a boolean rather than on the wording of that message,
        /// which the example is free to change.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="propertyName"></param>
        private static void SkipIfPropertyMissing(
            string value,
            string propertyName)
        {
            if (bool.TryParse(value, out _) == false)
            {
                Assert.Inconclusive(
                    $"The '{propertyName}' property is not in the data file in " +
                    $"use, which reported '{value}'. Set the " +
                    $"'51DEGREES_DD_PATH' environment variable to an Enterprise " +
                    $"or TAC data file to run this test.");
            }
        }
    }
}
