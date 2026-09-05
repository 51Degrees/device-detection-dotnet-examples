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

using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using System;
using System.Collections;
using System.Linq;
/// <summary>
/// @example Cloud/GetAllProperties-Console/Program.cs
/// 
/// @include{doc} example-get-all-properties-cloud.txt
/// 
/// @include{doc} example-require-resourcekey.txt
/// </summary>
namespace FiftyOne.DeviceDetection.Examples.Cloud.GetAllProperties
{
    public class Program
    {
        public class Example
        {
            private static string mobileUserAgent =
                "Mozilla/5.0 (Linux; Android 9; SAMSUNG SM-G960U) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/10.1 " +
                "Chrome/71.0.3578.99 Mobile Safari/537.36";

            public void Run(string resourceKey, string cloudEndPoint = "")
            {
                var builder = new DeviceDetectionPipelineBuilder()
                    // Tell it that we want to use cloud and pass our resource key.
                    .UseCloud(resourceKey);

                // If a cloud endpoint has been provided then set the
                // cloud pipeline endpoint. 
                if (string.IsNullOrWhiteSpace(cloudEndPoint) == false) 
                {
                    builder.SetEndPoint(cloudEndPoint);
                }

                // Create the pipeline
                using (var pipeline = builder.Build())
                {
                    // Output details for a mobile User-Agent.
                    AnalyseUserAgent(mobileUserAgent, pipeline);
                }
            }

            static void AnalyseUserAgent(string userAgent, IPipeline pipeline)
            {
                // Create the FlowData instance.
                using (var data = pipeline.CreateFlowData())
                {
                    // Add a User-Agent as evidence.
                    data.AddEvidence(FiftyOne.Pipeline.Core.Constants.EVIDENCE_QUERY_USERAGENT_KEY, userAgent);
                    // Process the supplied evidence.
                    data.Process();
                    // Get device data from the flow data.
                    var device = data.Get<IDeviceData>();
                    Console.WriteLine($"What property values are associated with " +
                        $"the User-Agent '{userAgent}'?");

                    // Iterate through device data results, displaying all values.
                    foreach (var property in device.AsDictionary()
                        .OrderBy(p => p.Key))
                    {
                        Console.WriteLine($"{property.Key} = {GetValueToOutput(property.Value)}");
                    }
                }
            }

            /// <summary>
            /// Convert the given value into a human-readable string representation 
            /// </summary>
            /// <param name="propertyValue">
            /// Property value object to be converted
            /// </param>
            /// <returns></returns>
            private static string GetValueToOutput(object propertyValue)
            {
                if (propertyValue == null)
                {
                    return "NULL";
                }

                var basePropetyType = propertyValue.GetType();
                var basePropertyValue = propertyValue;

                if (propertyValue is IAspectPropertyValue aspectPropertyValue)
                {
                    if (aspectPropertyValue.HasValue)
                    {
                        // Get the type and value parameters from the 
                        // AspectPropertyValue instance.
                        basePropetyType = basePropetyType.GenericTypeArguments[0];
                        basePropertyValue = aspectPropertyValue.Value;
                    }
                    else
                    {
                        // The property has no value so output the reason.
                        basePropetyType = typeof(string);
                        basePropertyValue = $"NO VALUE ({aspectPropertyValue.NoValueMessage})";
                    }
                }

                if (basePropetyType != typeof(string) &&
                    typeof(IEnumerable).IsAssignableFrom(basePropetyType))
                {
                    // Property is an IEnumerable (that is not a string)
                    // so return a comma-separated list of values.
                    var collection = basePropertyValue as IEnumerable;
                    var output = "";
                    foreach (var entry in collection)
                    {
                        if (output.Length > 0) { output += ","; }
                        output += entry.ToString();
                    }
                    return output;
                }
                else
                {
                    var str = basePropertyValue.ToString();
                    // Truncate any long strings to 200 characters
                    if (str.Length > 200)
                    {
                        str = str.Remove(200);
                        str += "...";
                    }
                    return str;
                }
            }
        }

        static void Main(string[] args)
        {
            // Use the command line args to get the resource key if present.
            // Otherwise, get it from the environment, following the same
            // convention as the other cloud examples so that a key set once
            // works for all of them.
            string resourceKeySource = null;
            string resourceKey = args.Length > 0 ? args[0] :
                ExampleUtils.GetResourceKeyFromEnv(out resourceKeySource);

            if (string.IsNullOrWhiteSpace(resourceKey))
            {
                Console.WriteLine(
                    "No resource key was supplied. Pass one as the first " +
                    "command line argument, or set one of these " +
                    "environment variables: " +
                    ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR_DESCRIPTION +
                    ".");
                Console.WriteLine(
                    "The 51Degrees cloud service is accessed with a " +
                    "resource key. A free key can be created at " +
                    "https://configure.51degrees.com/Wkqxf3Bs?utm_source=code&utm_medium=example&utm_campaign=device-detection-dotnet-examples&utm_content=examples-cloud-getallproperties-console-program.cs&utm_term=resource-key-required " +
                    "and gives the free properties. With a paid " +
                    "subscription, a key created at " +
                    "https://configure.51degrees.com/hYzn3TV3?utm_source=code&utm_medium=example&utm_campaign=device-detection-dotnet-examples&utm_content=examples-cloud-getallproperties-console-program.cs&utm_term=resource-key-required " +
                    "includes every property this example displays. Make " +
                    "sure the key includes all the properties you want to " +
                    "see.");
            }
            else
            {
                if (args.Length == 0)
                {
                    Console.WriteLine(
                        $"Using the resource key from " +
                        $"'{resourceKeySource}'.");
                }
                new Example().Run(resourceKey);
            }
#if (DEBUG)
            Console.WriteLine("Done. Press any key to exit.");
            Console.ReadKey();
#endif
        }
    }
}
