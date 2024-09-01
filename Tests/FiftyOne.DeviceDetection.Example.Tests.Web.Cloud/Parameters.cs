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
using System.Collections.Generic;
using System.Linq;

using Base = FiftyOne.DeviceDetection.Example.Tests.Web.Parameters;

namespace FiftyOne.DeviceDetection.Example.Tests.Web.Cloud
{
    public static class Parameters
    {
        public static string SUPER_KEY { get; private set; }
        public static string BROWSER_KEY { get; private set; }
        public static string HARDWARE_KEY { get; private set; }
        public static string PLATFORM_KEY { get; private set; }
        public static string NO_ACCEPTCH_KEY { get; private set; }

        private static bool _gotEnvVars = false;

        public static void GetEnvVars()
        {
            if (_gotEnvVars == false)
            {
                // Get resource keys from environment variables.
                // These must be configured with at least the following
                // properties:
                // HardwareVendor,HardwareName,DeviceType,PlatformVendor,
                // PlatformName,PlatformVersion,BrowserVendor,BrowserName,
                // BrowserVersion
                // In addition, each key will need to have specific setup
                // for the '*Accept-CH' properties:
                // SUPER_RESOURCE_KEY - SetHeaderBrowserAccept-CH, 
                //    SetHeaderHardwareAccept-CH, SetHeaderPlatformAccept-CH
                // ACCEPTCH_BROWSER_KEY - SetHeaderBrowserAccept-CH only
                // ACCEPTCH_HARDWARE_KEY - SetHeaderHardwareAccept-CH only
                // ACCEPTCH_PLATFORM_KEY - SetHeaderPlatformAccept-CH only
                // ACCEPTCH_NONE_KEY - No *Accept-CH properties.
                ExampleUtils.GetKeyFromEnv(
                    ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR,
                    v => SUPER_KEY = v);
                ExampleUtils.GetKeyFromEnv("ACCEPTCH_BROWSER_KEY", v => BROWSER_KEY = v);
                ExampleUtils.GetKeyFromEnv("ACCEPTCH_HARDWARE_KEY", v => HARDWARE_KEY = v);
                ExampleUtils.GetKeyFromEnv("ACCEPTCH_PLATFORM_KEY", v => PLATFORM_KEY = v);
                ExampleUtils.GetKeyFromEnv("ACCEPTCH_NONE_KEY", v => NO_ACCEPTCH_KEY = v);
                _gotEnvVars = true;
            }
        }

        /// <summary>
        /// Used to supply parameters to the VerifyExample tests for
        /// cloud-backed examples.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<object[]> VerifyCloudExample_DATA()
        {
            GetEnvVars();
            List<string> ResourceKeys = new List<string>()
            {
                SUPER_KEY,
                BROWSER_KEY,
                HARDWARE_KEY,
                PLATFORM_KEY,
                NO_ACCEPTCH_KEY
            };

            List<string> UserAgents = new List<string>()
            {
                Base.CHROME_UA,
                Base.EDGE_UA,
                Base.FIREFOX_UA,
                Base.SAFARI_UA,
                Base.CURL_UA
            };

            // Get all combinations of keys and uas
            var joined = ResourceKeys
                .Join(UserAgents, (x) => true, (x) => true,
                    (a, b) => new { ResourceKey = a, UserAgent = b });

            foreach (var permutation in joined)
            {
                var expectedAcceptCH = new List<string>();

                // Determine which values we are expecting to
                // see in Accept-CH.
                if (permutation.UserAgent == Base.CHROME_UA ||
                    permutation.UserAgent == Base.EDGE_UA)
                {
                    if (permutation.ResourceKey == BROWSER_KEY ||
                        permutation.ResourceKey == SUPER_KEY)
                    {
                        expectedAcceptCH.AddRange(Base.BROWSER_ACCEPT_CH);
                    }
                    if (permutation.ResourceKey == HARDWARE_KEY ||
                        permutation.ResourceKey == SUPER_KEY)
                    {
                        expectedAcceptCH.AddRange(Base.HARDWARE_ACCEPT_CH);
                    }
                    if (permutation.ResourceKey == PLATFORM_KEY ||
                        permutation.ResourceKey == SUPER_KEY)
                    {
                        expectedAcceptCH.AddRange(Base.PLATFORM_ACCEPT_CH);
                    }
                }

                yield return new object[]
                {
                    permutation.ResourceKey,
                    permutation.UserAgent,
                    expectedAcceptCH
                };
            }
        }
    }
}
