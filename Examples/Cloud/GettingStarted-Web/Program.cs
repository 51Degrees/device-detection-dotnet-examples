/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2025 51 Degrees Mobile Experts Limited, Davidson House,
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

using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Configuration;
using FiftyOne.Pipeline.Web.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace FiftyOne.DeviceDetection.Examples.Cloud.GettingStartedWeb
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Start the server and then wait for the task to finish.
            Run(args).Wait();
        }

        /// <summary>
        /// Used by unit tests to run the example in an almost identical manner
        /// to a developer using the example. Returns the task that the web 
        /// server is running in so that the test can trigger the cancellation
        /// token and then wait for the server to shutdown before finishing.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="stopToken"></param>
        /// <returns></returns>
        public static Task Run(
            string[] args,
            CancellationToken stopToken = default)
        {
            var config = CreateConfiguration();
            var configOverrides = CreateConfigOverrides(config);
            return CreateHostBuilder(config, configOverrides, args).Build().RunAsync(
                stopToken);
        }

        public static IHostBuilder CreateHostBuilder(
            IConfiguration baseConfig,
            IDictionary<string, string> overrides,
            string[] args)
            => Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(builder =>
                {
                    builder.ConfigureAppConfiguration(config =>
                    {
                        config
                            .AddConfiguration(baseConfig)
                            .AddInMemoryCollection(overrides);
                    })
                    .UseUrls(Constants.AllUrls)
                    .UseStartup<Startup>()
                    .UseStaticWebAssets();
                });

        private static IConfiguration CreateConfiguration()
            => new ConfigurationBuilder()
                .AddJsonFile("appsettings_51.json")
                .Build();

        /// <summary>
        /// This section would not normally be needed. We're just checking for the resource key
        /// so that if it's not set, we can override it with the one from the environment variables
        /// or show a message that's very clear about what needs to be done in the content of 
        /// this example.
        /// </summary>
        private static Dictionary<string, string> CreateConfigOverrides(IConfiguration config)
        {
            var result = new Dictionary<string, string>();

            PipelineOptions options = new PipelineWebIntegrationOptions();
            var section = config.GetRequiredSection("PipelineOptions");
            // Use the 'ErrorOnUnknownConfiguration' option to warn us if we've got any
            // misnamed configuration keys.
            section.Bind(options, (o) => { o.ErrorOnUnknownConfiguration = true; });

            // Get the resource key setting from the config file. 
            var resourceKeyFromConfig = options.GetResourceKey();
            var configHasKey = string.IsNullOrWhiteSpace(resourceKeyFromConfig) == false &&
                    resourceKeyFromConfig.StartsWith("!!") == false;

            if (configHasKey == false)
            {
                // Get the index of the cloud request engine element in the config file so that
                // we can create an override key for it.
                var cloudEngineOptions = options.GetElementConfig(nameof(CloudRequestEngine));
                Console.WriteLine($"{nameof(cloudEngineOptions)}.{nameof(cloudEngineOptions.BuilderName)} = '{cloudEngineOptions.BuilderName}'");
                var cloudEngineIndex = options.Elements.IndexOf(cloudEngineOptions);
                Console.WriteLine($"{nameof(CloudRequestEngine)} located at element {cloudEngineIndex}.");
                var resourceKeyConfigKey = $"PipelineOptions:Elements:{cloudEngineIndex}" +
                    $":BuildParameters:ResourceKey";

                string resourceKey = Environment.GetEnvironmentVariable(
                        ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR);

                if (string.IsNullOrEmpty(resourceKey) == false)
                {
                    Console.WriteLine($"Attempting to override '{resourceKeyConfigKey}'");
                    result.Add(resourceKeyConfigKey, resourceKey);
                }
                else
                {
                    throw new Exception($"No resource key specified in the configuration file " +
                        $"'appsettings.json' or the environment variable " +
                        $"'{ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR}'. The 51Degrees cloud " +
                        $"service is accessed using a 'ResourceKey'. For more information " +
                        $"see https://51degrees.com/documentation/_info__resource_keys.html. " +
                        $"A resource key with the properties required by this example can be " +
                        $"created for free at https://configure.51degrees.com/1QWJwHxl. " +
                        $"Once complete, populate the config file or environment variable " +
                        $"mentioned at the start of this message with the key.");
                }
            }

            return result;
        }
    }
}
