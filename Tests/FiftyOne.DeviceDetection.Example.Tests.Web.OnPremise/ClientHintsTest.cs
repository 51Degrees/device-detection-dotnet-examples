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

namespace FiftyOne.DeviceDetection.Example.Tests.Web.OnPremise
{
    public class ClientHintsTest<T>
        : ClientHintsExampleTestBase<T> where T : class
    {
        /// <summary>
        /// Path to the data file to use for testing.
        /// </summary>
        private string DataFilePath { get; set; }

        /// <summary>
        /// Path to the app settings to use for the test.
        /// </summary>
        private string AppSettingsPath { get; set; }

        private string RequestedProperties { get; set; }

        [TestInitialize]
        public void Init()
        {
            ExampleUtils.GetKeyFromEnv(
                Constants.DEVICE_DETECTION_DATA_FILE_ENV_VAR,
                v => DataFilePath = v);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            if (string.IsNullOrEmpty(DataFilePath))
            {
                Assert.Inconclusive(
                    "These tests will not pass when using the free 'lite' " +
                    "data file that is included with the source code. " +
                    "A paid-for data file can be obtained from " +
                    "https://51degrees.com/pricing You can then configure " +
                    "the DEVICE_DETECTION_DATAFILE environment variable " +
                    "with the full path to the file.");
            }

            // Zero based index of the element in the array of the original
            // appsettings file.
            var elementIndex = 0;
            var ddEngineParametersPrefix = $"PipelineOptions:Elements:" +
                $"{elementIndex}:BuildParameters:";
            var propertiesConfigKey = $"{ddEngineParametersPrefix}Properties";
            var dataFileConfigKey = $"{ddEngineParametersPrefix}DataFile";
            var testConfigOverrides =
                new Dictionary<string, string>()
                {
                    { propertiesConfigKey, RequestedProperties }
                };

            if (string.IsNullOrEmpty(DataFilePath) == false)
            {
                testConfigOverrides.Add(dataFileConfigKey, DataFilePath);
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

            try
            {
                base.ConfigureWebHost(builder);
            }
            catch (Exception ex)
            {
                // If we got an exception during startup then
                // check if it was due to a missing data file.
                // If so, return an inconclusive result.
                var dataFileEx = CheckForDataFileError(ex);
                if (dataFileEx != null)
                {
                    Assert.Inconclusive(
                        $"This test requires a local data file. " +
                        $"See exception message for details: " +
                        dataFileEx.Message);
                }
                else { throw; }
            }
        }

        protected async Task VerifyExampleAsync(
            string requestedProperties,
            string userAgent,
            List<string> expectedAcceptCH,
            string exampleDirName)
        {
            // Record the requested properties.
            RequestedProperties = requestedProperties;

            // Set the app settings path.
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