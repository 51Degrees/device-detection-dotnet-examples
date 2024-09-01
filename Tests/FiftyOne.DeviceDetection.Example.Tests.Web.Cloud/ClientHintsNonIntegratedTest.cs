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

using Cloud_Client_Hints_Not_Integrated;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FiftyOne.DeviceDetection.Example.Tests.Web.Cloud
{
    [TestClass]
    public class ClientHintsNonIntegratedTest : ClientHintsTest<Program>
    {
        /// <summary>
        /// Test that the 'Cloud_Client_Hints_Not_Integrated_NetCore_31' 
        /// example is returning the expected Accept-CH header values 
        /// for various different scenarios.
        /// </summary>
        /// <param name="resourceKey">
        /// The resource key to use when creating the web server 
        /// running the example.
        /// </param>
        /// <param name="userAgent">
        /// The value to set the User-Agent header to when making 
        /// a request to the example web server.
        /// </param>
        /// <param name="expectedAcceptCH">
        /// A list of the values that are expected in the Accept-CH 
        /// header in the response from the example web server.
        /// </param>
        /// <returns></returns>
        [DataTestMethod]
        [DynamicData(
            nameof(Parameters.VerifyCloudExample_DATA), 
            typeof(Parameters),
            DynamicDataSourceType.Method)]
        public async Task VerifyNonIntegratedExample_Cloud(
            string resourceKey,
            string userAgent,
            List<string> expectedAcceptCH)
        {
            await VerifyExample(
                resourceKey,
                userAgent,
                expectedAcceptCH,
                "Cloud-UACH-manual");
        }
    }
}
