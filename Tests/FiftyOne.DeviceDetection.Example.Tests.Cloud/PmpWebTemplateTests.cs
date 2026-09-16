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

using FiftyOne.Examples.Cloud.PmpWeb;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FiftyOne.DeviceDetection.Example.Tests.Cloud
{
    /// <summary>
    /// Checks the addresses the PMP web demo's templates carry once their
    /// placeholders are filled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The PMP reads its resource key from the name of the script it was
    /// served as, so the key is part of a URL path rather than the value of
    /// an attribute. A key written straight into a template is only ever
    /// encoded for HTML, and HTML encoding leaves '/', '?', '#' and '%'
    /// exactly as they were, so a key holding one of them would address a
    /// different part of the cloud. The tag loads as a script, so the page
    /// is told nothing and the demo shows an empty screen.
    /// </para>
    /// <para>
    /// These tests fill every real template with a key that needs escaping
    /// and read the addresses back, so a template that builds an address
    /// out of its own parts again fails here rather than in a browser.
    /// </para>
    /// </remarks>
    [TestClass]
    public class PmpWebTemplateTests
    {
        /// <summary>
        /// A resource key holding one character from each class that HTML
        /// encoding leaves alone and a URL path does not.
        /// </summary>
        private const string AWKWARD_RESOURCE_KEY = "AAA/BBB+CCC?DDD#EEE%FFF";

        /// <summary>
        /// A cloud address given without the API path, as
        /// <see cref="Settings"/> holds it.
        /// </summary>
        private const string CLOUD_ENDPOINT = "https://cloud.51degrees.com";

        /// <summary>The src of the PMP tag, which has no async.</summary>
        private static readonly Regex PmpTag =
            new Regex("<script src=\"([^\"]*/pmp/[^\"]*)\"");

        /// <summary>
        /// The values the environment held before a test replaced them, so
        /// a test run leaves the process as it found it.
        /// </summary>
        // Not annotated nullable: this assembly is not built in a nullable
        // context, and an unset variable reads back as null either way.
        private string originalResourceKey;
        private string originalCloudEndpoint;

        [TestInitialize]
        public void SetUp()
        {
            originalResourceKey = Environment.GetEnvironmentVariable(
                Settings.RESOURCE_KEY_ENV_VAR);
            originalCloudEndpoint = Environment.GetEnvironmentVariable(
                Settings.CLOUD_ENDPOINT_ENV_VAR);
            Environment.SetEnvironmentVariable(
                Settings.RESOURCE_KEY_ENV_VAR, AWKWARD_RESOURCE_KEY);
            Environment.SetEnvironmentVariable(
                Settings.CLOUD_ENDPOINT_ENV_VAR, CLOUD_ENDPOINT);
        }

        [TestCleanup]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(
                Settings.RESOURCE_KEY_ENV_VAR, originalResourceKey);
            Environment.SetEnvironmentVariable(
                Settings.CLOUD_ENDPOINT_ENV_VAR, originalCloudEndpoint);
        }

        /// <summary>
        /// The address the PMP tag must carry, being the cloud, the API
        /// path, "pmp", and the resource key escaped for a path segment.
        /// </summary>
        private static string ExpectedPmpUrl =>
            $"{CLOUD_ENDPOINT}/api/v4/pmp/" +
            $"{Uri.EscapeDataString(AWKWARD_RESOURCE_KEY)}.js";

        /// <summary>
        /// Every template the demo serves, as the mode it is served under
        /// and its full path, read from the copy beside this assembly.
        /// </summary>
        private static IEnumerable<(string Mode, string Path)> Templates()
        {
            var root = Path.Combine(
                AppContext.BaseDirectory, "templates");
            Assert.IsTrue(
                Directory.Exists(root),
                $"The templates were not copied beside the test assembly. " +
                $"Looked in '{root}'.");
            foreach (var mode in new[] { "cloud", "pipeline" })
            {
                var directory = Path.Combine(root, mode);
                Assert.IsTrue(
                    Directory.Exists(directory),
                    $"No templates for the '{mode}' pages in '{root}'.");
                foreach (var file in Directory.EnumerateFiles(
                    directory, "*.html", SearchOption.AllDirectories))
                {
                    yield return (mode, file);
                }
            }
        }

        /// <summary>
        /// Fills a template the way <see cref="Pages"/> does when it serves
        /// it, which is the only encoding a placeholder ever receives.
        /// </summary>
        private static string Fill(string mode, string path)
        {
            var settings = Settings.FromEnvironment();
            var values = mode == "cloud"
                ? settings.CloudPlaceholders()
                : settings.PipelinePlaceholders();
            return Pages.Fill(File.ReadAllText(path), values);
        }

        /// <summary>
        /// The resource key reaches the cloud escaped for a path segment.
        /// Written for the bug where the templates spelled the address out
        /// themselves, so the key went through HTML encoding alone and a
        /// key holding '/' or '?' asked the cloud for something else.
        /// </summary>
        [TestMethod]
        public void PmpTagCarriesTheEscapedKeyInItsPath()
        {
            var checkedPages = 0;
            foreach (var (mode, path) in Templates())
            {
                var html = Fill(mode, path);
                var match = PmpTag.Match(html);
                if (match.Success == false)
                {
                    // consent and no-platform carry no PMP tag by design,
                    // because they are the pages that show what happens
                    // without one.
                    continue;
                }
                Assert.AreEqual(
                    ExpectedPmpUrl,
                    match.Groups[1].Value,
                    $"The PMP tag in '{Path.GetFileName(path)}' under " +
                    $"'{mode}' does not carry the escaped resource key.");
                checkedPages++;
            }
            Assert.AreEqual(
                14,
                checkedPages,
                "The demo should serve fourteen pages carrying a PMP tag, " +
                "being seven under 'cloud' and their seven copies under " +
                "'pipeline'.");
        }

        /// <summary>
        /// No page writes the key anywhere but that path. The key used to
        /// be in data-resource-key as well, and two places to write one key
        /// meant a page could carry one the cloud never saw.
        /// </summary>
        [TestMethod]
        public void NoPageCarriesTheKeyOutsideTheScriptAddress()
        {
            foreach (var (mode, path) in Templates())
            {
                var html = Fill(mode, path);
                Assert.IsFalse(
                    html.Contains("data-resource-key"),
                    $"'{Path.GetFileName(path)}' under '{mode}' still " +
                    "carries data-resource-key, which the PMP no longer " +
                    "reads.");
                var addresses = html.Split('"')
                    .Count(part => part.Contains(
                        Uri.EscapeDataString(AWKWARD_RESOURCE_KEY)));
                Assert.IsTrue(
                    addresses <= 2,
                    $"'{Path.GetFileName(path)}' under '{mode}' names the " +
                    $"resource key {addresses} times, and no page needs it " +
                    "in more than the PMP tag and the client script tag.");
            }
        }

        /// <summary>
        /// A template naming a placeholder the demo does not fill is a page
        /// served half built, so it must fail where it is instead.
        /// </summary>
        [TestMethod]
        public void EveryPlaceholderEveryTemplateUsesIsFilled()
        {
            foreach (var (mode, path) in Templates())
            {
                // Fill throws when a placeholder has no value, so reaching
                // the end of the loop is the assertion.
                var html = Fill(mode, path);
                Assert.IsFalse(
                    html.Contains("{{"),
                    $"'{Path.GetFileName(path)}' under '{mode}' still " +
                    "holds a placeholder once it has been filled.");
            }
        }
    }
}
