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

using System.Collections.Generic;
using System.Linq;

using Base = FiftyOne.DeviceDetection.Example.Tests.Web.Parameters;

namespace FiftyOne.DeviceDetection.Example.Tests.Web.OnPremise
{
    public static class Parameters
    {

        /// <summary>
        /// Used to supply parameters to the VerifyExample tests for
        /// on-premise examples.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<object[]> VerifyOnPremExample_DATA()
        {
            var Properties = new List<string>()
            {
                Base.ALL_PROPERTIES,
                Base.BROWSER_PROPERTIES,
                Base.HARDWARE_PROPERTIES,
                Base.PLATFORM_PROPERTIES,
                Base.BASE_PROPERTIES
            };

            var UserAgents = new List<string>()
            {
                Base.CHROME_UA,
                Base.EDGE_UA,
                Base.FIREFOX_UA,
                Base.SAFARI_UA,
                Base.CURL_UA
            };

            // Get all combinations of properties and uas
            var joined = Properties
                .Join(UserAgents, (x) => true, (x) => true,
                    (a, b) => new { Properties = a, UserAgent = b });

            foreach (var permutation in joined)
            {
                var expectedAcceptCH = new List<string>();

                // Determine which values we are expecting to
                // see in Accept-CH.
                if (permutation.UserAgent == Base.CHROME_UA ||
                    permutation.UserAgent == Base.EDGE_UA)
                {
                    if (permutation.Properties == Base.BROWSER_PROPERTIES ||
                        permutation.Properties == Base.ALL_PROPERTIES)
                    {
                        expectedAcceptCH.AddRange(Base.BROWSER_ACCEPT_CH);
                    }
                    if (permutation.Properties == Base.HARDWARE_PROPERTIES ||
                        permutation.Properties == Base.ALL_PROPERTIES)
                    {
                        expectedAcceptCH.AddRange(Base.HARDWARE_ACCEPT_CH);
                    }
                    if (permutation.Properties == Base.PLATFORM_PROPERTIES ||
                        permutation.Properties == Base.ALL_PROPERTIES)
                    {
                        expectedAcceptCH.AddRange(Base.PLATFORM_ACCEPT_CH);
                    }
                }

                yield return new object[]
                {
                    permutation.Properties,
                    permutation.UserAgent,
                    expectedAcceptCH
                };
            }
        }
    }
}
