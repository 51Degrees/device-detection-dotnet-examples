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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;

namespace FiftyOne.DeviceDetection.Example.Tests.Web
{
    public class ClientHintsExampleTestBase<T> : WebExampleTestBase<T> 
        where T : class
    {
        protected void VerifyAcceptCH(
            HttpResponseHeaders headers,
            List<string> expectedAcceptCH)
        {
            if (expectedAcceptCH == null || expectedAcceptCH.Count == 0)
            {
                // If no Accept-CH values are expected, then make sure the
                // header is not present in the response.
                if (headers.Contains("Accept-CH"))
                {
                    Assert.Fail(
                        $"Expected the Accept-CH header not to be " +
                        $"populated, but it was: " +
                        string.Join(",", headers.GetValues("Accept-CH")));
                }
            }
            else
            {
                // Verify the expected values are present in the 
                // Accept-CH header.
                Assert.IsTrue(headers.Contains("Accept-CH"),
                    "Expected the Accept-CH header to be populated, " +
                    "but it was not.");
                var actualValues = headers.GetValues("Accept-CH")
                    .SelectMany(h => h.Split(new char[] { ',', ' ' },
                        StringSplitOptions.RemoveEmptyEntries));
                // We don't require the expected list of values to match exactly, as the headers 
                // used by detection change over time. However, we do make sure that the most 
                // critical ones are included.
                foreach (var expectedValue in expectedAcceptCH)
                {
                    Assert.IsTrue(actualValues.Contains(expectedValue, 
                            StringComparer.OrdinalIgnoreCase),
                        $"The list of values in Accept-CH does not include " +
                        $"'{expectedValue}'. ({string.Join(",", actualValues)})");
                }
            }
        }
    }
}
