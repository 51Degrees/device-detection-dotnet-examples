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
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace FiftyOne.DeviceDetection.Example.Tests.Web
{
    public class GettingStartedTestOverrides<T> : GettingStartedTestBase<T>
        where T : class
    {
        public GettingStartedTestOverrides(WebApplicationFactory<T> factory) : base(factory) { }

        /// <summary>
        /// Profile ids to use for testing. These are extracted from 51Degrees
        /// device database and are known to not relate to User-Agents. They
        /// are therefore likely to be used in profile override id use cases.
        /// </summary>
        public static readonly int[] OverrideProfileIds = new int[] {
            37878,
            50396,
            50720,
            50728,
            50732,
            50739,
            50750,
            50755,
            50762,
            50764,
            54460,
            57691,
            57693,
            62734,
            69949,
            69950,
            79480,
            81100,
            81101,
            81104,
            85788,
            85790,
            85895,
            85896,
            85898,
            85900,
            85901,
            85902,
            85905,
            85906,
            85907,
            85910,
            86700,
            92486,
            92493,
            92498,
            93304,
            93305,
            95561,
            95563,
            96271,
            96282,
            96294,
            96515,
            97942,
            97978,
            97983,
            98126,
            98237,
            98238,
            98240,
            98241,
            98246,
            99315,
            99441,
            99442,
            99455,
            99482,
            99603,
            99606,
            99612,
            99613,
            101895,
            103206,
            103758,
            106935,
            106973,
            106975,
            107548,
            107549,
            107555,
            //108371,
            108616,
            108677,
            108704,
            112379,
            112384,
            113177,
            115461,
            115469,
            115474,
            115478,
            116508,
            116511,
            116517,
            116518,
            117357,
            117361,
            117892,
            118711,
            //118934,
            118945,
            122489,
            122490,
            122500,
            122587,
            //122591,
            122593,
            122594,
            122690,
            122809,
            123079,
            123080,
            //123082,
            //123083,
            //123106,
            123109,
            123156,
            123157,
            123245,
            123246,
            123247,
            123627,
            123628,
            123629,
            123631,
            123632,
            //124548 
        };

        /// <summary>
        /// Represents a JSON device response.
        /// </summary>
        public class OverrideDevice
        {
            [JsonProperty]
            public int? screenpixelswidth { get; set; }

            [JsonProperty]
            public int? screenpixelsheight { get; set; }

            [JsonProperty]
            public string deviceid { get; set; }
        }

        /// <summary>
        /// Represents a JSON response.
        /// </summary>
        public class OverrideResponse
        {
            [JsonProperty]
            public OverrideDevice device { get; set; }
        }

        /// <summary>
        /// Returns a random selection of 10 profile ids to check.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<object[]> TestProfileIds => 
            OverrideProfileIds.Select(i => new object[] { i });

        [Theory]
        [MemberData(nameof(TestProfileIds))]
        public async Task VerifyExample_ProfileId_Override(int profileId)
        {
            // Setup
            using (var http = Factory.CreateClient())
            using (var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(
                    Parameters.HttpsUrlsData.First()[0].ToString() + 
                    "/51dpipeline/json"),
            })
            {
                var sessionId = Guid.NewGuid().ToString();

                // Set an invalid User-Agent so that the device detection of
                // the User-Agent evidence is not a factor in the result.
                request.Headers.Add("User-Agent", "abc");

                // Add the form properties to the req
                var input = new Dictionary<string, string>();
                input.Add("51D_ProfileIds", profileId.ToString());
                input.Add("session-id", sessionId);
                input.Add("sequence", 1.ToString());
                request.Content = new FormUrlEncodedContent(input);

                // Act
                var response = await http.SendAsync(request);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var content = await response.Content.ReadAsStringAsync();
                var output = JsonConvert.DeserializeObject<OverrideResponse>(content);
                Assert.NotNull(output);
                Assert.NotNull(output.device);
                var device = output.device;
                Assert.NotNull(device.deviceid);
                var deviceId = device.deviceid.Split("-");
                Assert.Equal(profileId, int.Parse(deviceId[0]));
            }
        }

        /// <summary>
        /// Indicates whether <see cref="VerifyExample_PropertyValue_Override"/> tests
        /// make any sense for this class.
        /// </summary>
        protected virtual bool SupportsPropertyOverrides => true;

        /// <summary>
        /// Passes form parameters to override the pixel width and height of 
        /// the hardware profile associated with the User-Agent string. Checks
        /// that the overridden values are returned and not the fixed values 
        /// for the profile.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        [SkippableTheory]
        [MemberData(nameof(AllUrlsData))]
        public async Task VerifyExample_PropertyValue_Override(string url)
        {
            Skip.IfNot(
                SupportsPropertyOverrides, 
                $"{nameof(SupportsPropertyOverrides)} indicates this engine does not support property overrides.");

            // Setup
            using (var http = Factory.CreateClient())
            using (var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(url + "/51dpipeline/json"),
            })
            {
                var sessionId = Guid.NewGuid().ToString();
                var screenPixelsHeight = 1080;
                var screenPixelsWidth = 390;

                // Set an invalid User-Agent so that the device detection of
                // the User-Agent evidence is not a factor in the result.
                request.Headers.Add("User-Agent", Parameters.SAFARI_UA);

                // Add the form properties to the req
                var input = new Dictionary<string, string>();
                input.Add("51D_ScreenPixelsHeight", screenPixelsHeight.ToString());
                input.Add("51D_ScreenPixelsWidth", screenPixelsWidth.ToString());
                input.Add("session-id", sessionId);
                input.Add("sequence", 1.ToString());
                request.Content = new FormUrlEncodedContent(input);

                // Act
                var response = await http.SendAsync(request);

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var content = await response.Content.ReadAsStringAsync();
                var output = JsonConvert.DeserializeObject<OverrideResponse>(content);
                Assert.NotNull(output);
                Assert.NotNull(output.device);
                var device = output.device;
                Assert.NotNull(device.screenpixelsheight);
                Assert.NotNull(device.screenpixelswidth);
                Assert.Equal(screenPixelsWidth, device.screenpixelswidth);
                Assert.Equal(screenPixelsHeight, device.screenpixelsheight);
            }
        }
    }
}
