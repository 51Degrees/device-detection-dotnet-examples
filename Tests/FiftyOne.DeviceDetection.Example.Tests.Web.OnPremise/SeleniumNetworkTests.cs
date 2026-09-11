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
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace FiftyOne.DeviceDetection.Example.Tests.Web.OnPremise
{
    /// <summary>
    /// Tests for <see cref="SeleniumTestsBase.GetNetwork"/> that need no
    /// browser.
    /// </summary>
    [TestClass]
    public class SeleniumNetworkTests
    {
        /// <summary>
        /// A driver that does not speak the DevTools protocol, which is what
        /// FirefoxDriver became in Selenium 4.49. Every member throws because
        /// the method under test must not reach any of them.
        /// </summary>
        private class DriverWithoutDevTools : IWebDriver
        {
            public string Url
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }
            public string Title => throw new NotSupportedException();
            public string PageSource => throw new NotSupportedException();
            public string CurrentWindowHandle =>
                throw new NotSupportedException();
            public ReadOnlyCollection<string> WindowHandles =>
                throw new NotSupportedException();
            public void Close() => throw new NotSupportedException();
            public void Dispose() => throw new NotSupportedException();
            public ValueTask DisposeAsync() =>
                throw new NotSupportedException();
            public IWebElement FindElement(By by) =>
                throw new NotSupportedException();
            public ReadOnlyCollection<IWebElement> FindElements(By by) =>
                throw new NotSupportedException();
            public IOptions Manage() => throw new NotSupportedException();
            public INavigation Navigate() => throw new NotSupportedException();
            public void Quit() => throw new NotSupportedException();
            public ITargetLocator SwitchTo() =>
                throw new NotSupportedException();
        }

        /// <summary>
        /// A driver with no DevTools support must leave the network adapter
        /// unset rather than throw. Before the null check in GetNetwork the
        /// cast to IDevTools produced null and dereferencing it threw a
        /// NullReferenceException out of the initialize method, which failed
        /// every test in every Firefox class rather than the few that need
        /// the network adapter.
        /// </summary>
        [TestMethod]
        public async Task GetNetwork_DriverWithoutDevTools_ReturnsNull()
        {
            var network = await SeleniumTestsBase.GetNetwork(
                new DriverWithoutDevTools());

            Assert.IsNull(network);
        }
    }
}
