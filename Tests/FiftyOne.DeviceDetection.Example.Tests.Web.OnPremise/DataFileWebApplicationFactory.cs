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

using FiftyOne.DeviceDetection.Examples;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace FiftyOne.DeviceDetection.Example.Tests.Web.OnPremise
{
    /// <summary>
    /// The on-premise examples keep the relative data file name in
    /// appsettings.json and rewrite it to an absolute path at runtime by
    /// walking the directory hierarchy. That rewrite lives in the example's
    /// <c>Program.Run</c> startup path, which is not exercised when the example
    /// is hosted through <see cref="WebApplicationFactory{T}"/> - so the engine
    /// builder would resolve the bare relative name against the test process
    /// working directory (the test bin), where the data file is not present.
    /// This factory performs the same resolution on the WebApplicationFactory
    /// hosting path so those tests find the shared data file.
    /// </summary>
    public class DataFileWebApplicationFactory<T> : WebApplicationFactory<T>
        where T : class
    {
        private const string LiteDataFileName = "51Degrees-LiteV4.1.hash";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // FindDataFile honours the 51DEGREES_DD_PATH / DEVICEDETECTIONDATAFILE
            // environment variables before falling back to the directory search,
            // matching the precedence used by the example itself.
            var dataFile = ExampleUtils.FindDataFile(LiteDataFileName);
            if (string.IsNullOrEmpty(dataFile) == false)
            {
                builder.ConfigureAppConfiguration(config =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["PipelineOptions:Elements:0:BuildParameters:DataFile"] = dataFile
                    });
                });
            }

            base.ConfigureWebHost(builder);
        }
    }
}
