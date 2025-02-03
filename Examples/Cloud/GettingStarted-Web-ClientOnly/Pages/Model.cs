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

using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FiftyOne.DeviceDetection.Examples.Cloud.GettingStartedWeb.ClientOnly.Pages
{
    public class Model : PageModel
    {
        /// <summary>
        /// Resource key provided by the environment variable with the key 
        /// <see cref="ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR"/>.
        /// </summary>
        public string? ResourceKey { get; private set; }

        /// <summary>
        /// Cloud end point provided by the environment variable with the key 
        /// <see cref="ExampleUtils.CLOUD_END_POINT_ENV_VAR"/>. Will default
        /// to 
        /// </summary>
        public string? CloudEndPoint { get; private set; }

        /// <summary>
        /// The default cloud end point if the environment variable is not
        /// configured.
        /// </summary>
        private const string DEFAULT_END_POINT = "https://cloud.51degrees.com";

        public Model()
        {
            ResourceKey = Environment.GetEnvironmentVariable(
                ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR);
            var cloudEndPoint = Environment.GetEnvironmentVariable(
                ExampleUtils.CLOUD_END_POINT_ENV_VAR);
            CloudEndPoint = cloudEndPoint == null ?
                DEFAULT_END_POINT :
                cloudEndPoint;
        }

        public void OnGet()
        {
        }
    }
}
