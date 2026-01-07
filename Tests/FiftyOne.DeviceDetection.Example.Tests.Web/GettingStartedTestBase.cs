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

using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace FiftyOne.DeviceDetection.Example.Tests.Web
{
    public class GettingStartedTestBase<T>: IClassFixture<WebApplicationFactory<T>>
        where T : class
    {
        protected readonly WebApplicationFactory<T> Factory;

        public GettingStartedTestBase(WebApplicationFactory<T> factory)
        {
            Factory = factory;
        }

        public static IEnumerable<object[]> AllUrlsData => Parameters.AllUrlsData;

        /// <summary>
        /// Test that the running server is able to run and returns code 200 
        /// upon http(s) request.
        /// </summary>
        /// <returns></returns>
        [Theory]
        [MemberData(nameof(AllUrlsData))]
        public async Task VerifyExample_Returns_Status_Code_200(string url)
        {
            // Setup
            using (var http = Factory.CreateClient())
            using (var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(url),
            })
            { 
                request.Headers.Add("User-Agent", "abc");

                // Act
                var response = await http.SendAsync(request);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }
    }
}
