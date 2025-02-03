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

using FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements;
using Microsoft.Extensions.Logging;

namespace FiftyOne.DeviceDetection.Examples
{
    public class AppleExampleBase : ExampleBase
    {

        protected static bool CheckDataFiles(ILogger logger, DeviceDetectionHashEngine engine, 
            string hashDataFile, string appleDataFile)
        {
            bool runExample = true;
            // Show a useful message if no data file was supplied
            if (string.IsNullOrWhiteSpace(hashDataFile))
            {
                logger.LogError(
                    $"Failed to find a device detection data file. This example requires " +
                    $"a paid-for device detection data file. See our pricing page for " +
                    $"details: https://51degrees.com/pricing");
                runExample = false;
            }
            else
            {
                // Verify that the supplied data file was not lite
                var dataFileInfo = ExampleUtils.GetDataFileInfo(engine);

                if (dataFileInfo.Tier == "Lite")
                {
                    logger.LogError(
                        $"Lite data file supplied. This example requires a paid-for " +
                        $"device detection data file. See our pricing page for " +
                        $"details: https://51degrees.com/pricing");
                    runExample = false;
                }
            }
            // Verify that we have found an Apple data file
            if (string.IsNullOrWhiteSpace(appleDataFile))
            {
                logger.LogError(
                    $"Failed to find an Apple data file. This example requires an " +
                    $"Apple data file. This is available from " +
                    $"https://cloud.51degrees.com/cdn/macintosh.data.json");
                runExample = false;
            }

            return runExample;
        }
    }
}
