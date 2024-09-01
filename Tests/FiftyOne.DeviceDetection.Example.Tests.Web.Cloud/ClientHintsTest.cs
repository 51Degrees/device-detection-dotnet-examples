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
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace FiftyOne.DeviceDetection.Example.Tests.Web.Cloud
{
    public class ClientHintsTest<T> : ClientHintsExampleTestBase<T> where T : class
    {
        private string CloudEndPoint { get; set; }

        private string ResourceKey { get; set; }

        /// <summary>
        /// Path to the app settings to use for the test.
        /// </summary>
        private string AppSettingsPath { get; set; }

        [TestInitialize]
        public void Init()
        {
            ExampleUtils.GetKeyFromEnv(
                ExampleUtils.CLOUD_END_POINT_ENV_VAR,
                v => CloudEndPoint = v);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Zero based index of the element in the array of the original
            // appsettings file.
            var elementIndex = 0;
            var requestEnginePrefix = $"PipelineOptions:Elements:" +
                $"{elementIndex}:BuildParameters:";
            var resourceKeyConfigKey = $"{requestEnginePrefix}ResourceKey";
            var endPointConfigKey = $"{requestEnginePrefix}EndPoint";
            var testConfigOverrides =
                new Dictionary<string, string>()
                {
                { resourceKeyConfigKey, ResourceKey },
                };
            if (string.IsNullOrEmpty(CloudEndPoint) == false)
            {
                testConfigOverrides.Add(endPointConfigKey, CloudEndPoint);
            }

            builder.ConfigureAppConfiguration((context, builder) =>
            {
                builder
                    // First add the appsettings.json file for
                    // this example.
                    .AddJsonFile(AppSettingsPath)
                    // Now override the configuration with any
                    // specific values we want to use for the test.
                    .AddInMemoryCollection(testConfigOverrides);
            });

            base.ConfigureWebHost(builder);
        }

        protected async Task VerifyExample(
            string resourceKey,
            string userAgent,
            List<string> expectedAcceptCH,
            string exampleDirName)
        {
            if (string.IsNullOrWhiteSpace(resourceKey))
            {
                Assert.Inconclusive("Unable to run this test as no " +
                    "resource key was passed from the relevant environment " +
                    "variables. (See ClientHintsExampleTestBase.GetEnvVars)");
            }

            ResourceKey = resourceKey;

            // Determine the path to the app settings file we need to used
            string appSettingsPath = Environment.CurrentDirectory;
            var pos = appSettingsPath.IndexOf("Tests");
            appSettingsPath = appSettingsPath.Remove(pos);
            AppSettingsPath = Path.Combine(
                appSettingsPath,
                "Examples",
                "Legacy Web",
                exampleDirName,
                "appsettings.json");

            // Has the side effect of starting the web server and calling
            // ConfigureWebHost.
            using (var http = CreateClient())
            using (var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get
            })
            { 
                request.Headers.Add("User-Agent", userAgent);
                using (var response = await http.SendAsync(request))
                {
                    // Verify the request was successful.
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                    VerifyAcceptCH(response.Headers, expectedAcceptCH);
                }
            }
        }
    }
}