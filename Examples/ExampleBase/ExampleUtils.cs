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
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.DeviceDetection.Examples
{
    public static class ExampleUtils
    {
        /// <summary>
        /// The default environment variable key used to get the resource key 
        /// to use when running cloud examples.
        /// </summary>
        public const string CLOUD_RESOURCE_KEY_ENV_VAR = "SUPER_RESOURCE_KEY";

        /// <summary>
        /// The default environment variable key used to get the end point URL
        /// to use when running cloud examples. Can be used to override the
        /// appsettings.json configuration for testing custom end points.
        /// </summary>
        public const string CLOUD_END_POINT_ENV_VAR = "51D_CLOUD_ENDPOINT";

        /// <summary>
        /// Timeout used when searching for files.
        /// </summary>
        private const int FindFileTimeoutMs = 10000;

        /// <summary>
        /// If data file is older than this number of days then a warning will be displayed.
        /// </summary>
        public const int DataFileAgeWarning = 30;

        private const string DATA_OPTION = "--data-file";
        private const string DATA_OPTION_SHORT = "-d";
        private const string UA_OPTION = "--user-agent-file";
        private const string UA_OPTION_SHORT = "-u";
        private const string JSON_OPTION = "--json-output";
        private const string JSON_OPTION_SHORT = "-j";
        private const string HELP_OPTION = "--51help";
        private const string HELP_OPTION_SHORT = "-51h";

        private static string OptionMessage(string message, string option, string shortOption)
        {
            var padding = 32 - option.Length - shortOption.Length;
            return $"  {option}, {shortOption}{new string(' ', padding)}: {message}";
        }

        /// <summary>
        /// Print the available options to the output.
        /// </summary>
        private static void PrintHelp()
        {
            Console.WriteLine("Available options are:");
            Console.WriteLine(OptionMessage("Path to a 51Degrees Hash data file", DATA_OPTION, DATA_OPTION_SHORT));
            Console.WriteLine(OptionMessage("Path to a User-Agents YAML file", UA_OPTION, UA_OPTION_SHORT));
            Console.WriteLine(OptionMessage("Path to a file to output JSON format results to", JSON_OPTION, JSON_OPTION_SHORT));
            Console.WriteLine(OptionMessage("Print this help", HELP_OPTION, HELP_OPTION_SHORT));
        }


        /// <summary>
        /// Parse the command line arguments passed to the example to get the common
        /// options.
        /// </summary>
        /// <param name="args">
        /// Command line options.
        /// </param>
        /// <returns>
        /// Parsed options, or null if help output is requested.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// If an invalid argument is passed.
        /// </exception>
        public static ExampleOptions ParseOptions(string[] args)
        {
            var options = new ExampleOptions();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-"))
                {
                    switch (args[i])
                    {
                        case DATA_OPTION:
                        case DATA_OPTION_SHORT:
                            // Set data file path
                            options.DataFilePath = args[i + 1];
                            break;
                        case UA_OPTION:
                        case UA_OPTION_SHORT:
                            // Set data file path
                            options.EvidenceFile = args[i + 1];
                            break;
                        case JSON_OPTION:
                        case JSON_OPTION_SHORT:
                            // Set data file path
                            options.JsonOutput = args[i + 1];
                            break;
                        case HELP_OPTION:
                        case HELP_OPTION_SHORT:
                            // Set data file path
                            PrintHelp();
                            return null;
                        default:
                            throw new ArgumentException(
                                $"The option '{args[i]}' is not recognized. " +
                                $"Use {HELP_OPTION} ({HELP_OPTION_SHORT}) to list options");
                    }
                }
                else
                {
                    // Do nothing, this is a value.
                }
            }
            return options;
        }

        /// <summary>
        /// Uses a background task to search for the specified filename within the working 
        /// directory.
        /// If the file cannot be found, the algorithm will move to the parent directory and 
        /// repeat the process.
        /// This continues until the file is found or a timeout is triggered.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="dir">
        /// The directory to start looking from. If not provided the current directory is used.
        /// </param>
        /// <returns></returns>
        public static string FindFile(
            string filename,
            DirectoryInfo dir = null)
        {
            var cancel = new CancellationTokenSource();
            // Start the file system search as a separate task.
            var searchTask = Task.Run(() => FindFile(filename, dir, cancel.Token));
            // Wait for either the search or a timeout task to complete.
            Task.WaitAny(searchTask, Task.Delay(FindFileTimeoutMs));
            cancel.Cancel();
            // If search has not got a result then return null.
            return searchTask.IsCompleted ? searchTask.Result : null;
        }

        private static string FindFile(
            string filename,
            DirectoryInfo dir,
            CancellationToken cancel)
        {
            if (dir == null)
            {
                dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            }
            string result = null;

            try
            {
                var files = dir.GetFiles(filename, SearchOption.AllDirectories);
                if (files.Length == 0 &&
                    dir.Parent != null &&
                    cancel.IsCancellationRequested == false)
                {
                    result = FindFile(filename, dir.Parent, cancel);
                }
                else if (files.Length > 0)
                {
                    result = files[0].FullName;
                }
            }
            // No matter what goes wrong here, we just want to indicate that we
            // couldn't find the file by returning null.
            catch { result = null; }

            return result;
        }

        /// <summary>
        /// Get information about the specified data file
        /// </summary>
        /// <param name="dataFile"></param>
        /// <param name="engineBuilder"></param>
        public static DataFileInfo GetDataFileInfo(string dataFile, 
            DeviceDetectionHashEngineBuilder engineBuilder)
        {
            DataFileInfo result = new DataFileInfo();

            using (var engine = engineBuilder
                .Build(dataFile, false))
            {
                result = GetDataFileInfo(engine);
            }

            return result;
        }

        /// <summary>
        /// Get information about the data file used by the specified engine
        /// </summary>
        /// <param name="engine"></param>
        public static DataFileInfo GetDataFileInfo(DeviceDetectionHashEngine engine)
        {
            DataFileInfo result = new DataFileInfo();
            result.PublishDate = engine.DataFiles[0].DataPublishedDateTime;
            result.Tier = engine.DataSourceTier;
            result.Filepath = engine.DataFiles[0].DataFilePath;
            return result;
        }

        /// <summary>
        /// Display information about the data file and log warnings if specific requirements
        /// are not met.
        /// </summary>
        /// <param name="pipeline"></param>
        /// <param name="logger"></param>
        public static void CheckDataFile(IPipeline pipeline, ILogger logger)
        {
            // Get the 'engine' element within the pipeline that performs device detection.
            // We can use this to get details about the data file as well as meta-data describing
            // things such as the available properties.
            var engine = pipeline.GetElement<DeviceDetectionHashEngine>();
            CheckDataFile(engine, logger);
        }

        /// <summary>
        /// Display information about the data file and log warnings if specific requirements
        /// are not met.
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="logger"></param>
        public static void CheckDataFile(DeviceDetectionHashEngine engine, ILogger logger)
        {
            if (engine != null)
            {
                var info = GetDataFileInfo(engine);
                LogDataFileInfo(info, logger);
                LogDataFileStandardWarnings(info, logger);
            }
        }

        /// <summary>
        /// Display information about the data file and log warnings if specific requirements
        /// are not met.
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="logger"></param>
        public static void LogDataFileInfo(DataFileInfo info, ILogger logger)
        {
            if (info != null)
            {
                logger.LogInformation($"Using a '{info.Tier}' data file created " +
                    $"'{info.PublishDate}' from location '{info.Filepath}'");
            }
        }

        /// <summary>
        /// Display information about the data file and log warnings if specific requirements
        /// are not met.
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="logger"></param>
        public static void LogDataFileStandardWarnings(DataFileInfo info, ILogger logger)
        {
            if (info != null)
            {
                if (DateTime.UtcNow > info.PublishDate.AddDays(DataFileAgeWarning))
                {
                    logger.LogWarning($"This example is using a data file that is more than " +
                        $"{DataFileAgeWarning} days old. A more recent data file may be needed " +
                        $"to correctly detect the latest devices, browsers, etc. The latest lite " +
                        $"data file is available from the device-detection-data repository on " +
                        $"GitHub https://github.com/51Degrees/device-detection-data. Find out " +
                        $"about the Enterprise data file, which includes automatic daily " +
                        $"updates, on our pricing page: https://51degrees.com/pricing");
                }
                if (info.Tier == "Lite")
                {
                    logger.LogWarning($"This example is using the 'Lite' data file. This is " +
                        $"used for illustration, and has limited accuracy and capabilities. " +
                        $"Find out about the Enterprise data file on our pricing page: " +
                        $"https://51degrees.com/pricing");
                }
            }
        }

        /// <summary>
        /// Checks if the supplied 51Degrees resource key / license key is invalid.
        /// Note that this cannot determine if the key is definitely valid, just whether it is
        /// definitely invalid.
        /// </summary>
        /// <param name="key">
        /// The key to check.
        /// </param>
        /// <returns></returns>
        public static bool IsInvalidKey(string key)
        {
            try
            {
                if (key == null) 
                {
                    return true;
                }

                byte[] data = Convert.FromBase64String(key);
                string decodedString = Encoding.UTF8.GetString(data);

                return key.Trim().Length < 19 ||
                    decodedString.Length < 14;
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>
        /// This collection contains the various input values that will be passed to the device 
        /// detection algorithm when running examples
        /// </summary>
        public static readonly List<Dictionary<string, object>>
            EvidenceValues = new List<Dictionary<string, object>>()
        {
            // A User-Agent from a mobile device.
            new Dictionary<string, object>()
            {
                { "header.user-agent",
                    "Mozilla/5.0 (Linux; Android 9; SAMSUNG SM-G960U) AppleWebKit/537.36 " +
                    "(KHTML, like Gecko) SamsungBrowser/10.1 Chrome/71.0.3578.99 Mobile " +
                    "Safari/537.36" }
            },
            // A User-Agent from a desktop device.
            new Dictionary<string, object>()
            {
                { "header.user-agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                    "(KHTML, like Gecko) Chrome/78.0.3904.108 Safari/537.36" }
            },
            // Evidence values from a windows 11 device using a browser that supports
            // User-Agent Client Hints.
            new Dictionary<string, object>()
            {
                { "header.user-agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                    "(KHTML, like Gecko) Chrome/98.0.4758.102 Safari/537.36" },
                { "header.sec-ch-ua-mobile", "?0" },
                { "header.sec-ch-ua",
                    "\" Not A; Brand\";v=\"99\", \"Chromium\";v=\"98\", " +
                    "\"Google Chrome\";v=\"98\"" },
                { "header.sec-ch-ua-platform", "\"Windows\"" },
                { "header.sec-ch-ua-platform-version", "\"14.0.0\"" }
            }, 
            
            //A note on User-Agent Client Hint representations:
            //There are 3 common ways to represent UACH:
            //- [HTTP header map](https://wicg.github.io/ua-client-hints/)
            //- getHighEntropyValues() JS API call result in JSON format
            //- Structured User Agent Object from OpenRTB 2.6

            //Links:
            //-
            //[getHighEntropyValues()](https://developer.mozilla.org/en-US/docs/Web/API/NavigatorUAData/getHighEntropyValues)
            //-
            //[device.sua](https://51degrees.com/blog/openrtb-structured-user-agent-and-user-agent-client-hints)
            //- [OpenRTB 2.6
            //spec](https://github.com/InteractiveAdvertisingBureau/openrtb2.x/blob/main/2.6.md#objectuseragent)

            //51Degrees historically used HTTP header map to represent User-Agent Client Hints and expected the evidence to
            //be provided as HTTP headers (or same name query parameters).  

            //However in version 4.5 we introduced the ability to perform device detection using the 2 other User-Agent
            //Client Hints representations as evidence (internally it is done through conversion to the HTTP-header
            //representation, but it's an implementation detail).  The 2 evidence parameter names in question are:
            //`51D_gethighentropyvalues` and `51D_structureduseragent` - the engine consumes them as either
            //query or cookie params.
            
            new Dictionary<string, object>()
            {

            //`query.51D_gethighentropyvalues` or `cookie.51D_gethighentropyvalues` is a base64-encoded JSON-string result of
            //calling a getHighEntropyValues() API, that normally would return a value similar to the below:
            //
            //{"architecture":"arm","brands":[{"brand":"Google Chrome","version":"131"},{"brand":"Chromium","version":"131"}
            //,{"brand":"Not_A Brand","version":"24"}],"fullVersionList":[{"brand":"Google Chrome","version":"131.0.6778.140"}
            //,{"brand":"Chromium","version":"131.0.6778.140"},{"brand":"Not_A Brand","version":"24.0.0.0"}],
            //"mobile":false,"model":"","platform":"macOS","platformVersion":"15.1.1"}
            //
            //to obtain the below evidence we called this JavaScript snippet in the Chrome browser dev console:
            //`btoa(JSON.stringify(await navigator.userAgentData.getHighEntropyValues(
            //['bitness', 'architecture','fullVersionList','model', 'platformVersion'])))`
            {"query.51d_gethighentropyvalues",
             "eyJhcmNoaXRlY3R1cmUiOiJhcm0iLCJicmFuZHMiOlt7ImJyYW5kIjoiR29vZ2xlIENocm9tZSIsInZlcnNpb24iOiIxMzEifSx7ImJyY"+
             "W5kIjoiQ2hyb21pdW0iLCJ2ZXJzaW9uIjoiMTMxIn0seyJicmFuZCI6Ik5vdF9BIEJyYW5kIiwidmVyc2lvbiI6IjI0In1dLCJmdWxsVmV"+
             "yc2lvbkxpc3QiOlt7ImJyYW5kIjoiR29vZ2xlIENocm9tZSIsInZlcnNpb24iOiIxMzEuMC42Nzc4LjE0MCJ9LHsiYnJhbmQiOiJDaHJv"+
             "bWl1bSIsInZlcnNpb24iOiIxMzEuMC42Nzc4LjE0MCJ9LHsiYnJhbmQiOiJOb3RfQSBCcmFuZCIsInZlcnNpb24iOiIyNC4wLjAuMCJ9X"+
             "SwibW9iaWxlIjpmYWxzZSwibW9kZWwiOiIiLCJwbGF0Zm9ybSI6Im1hY09TIiwicGxhdGZvcm1WZXJzaW9uIjoiMTUuMS4xIn0="
             },
            },
        
            //`query.51D_structureduseragent` or `cookie.51D_structureduseragent` is a JSON-string representation of
            //User-Agent Client Hints used in the
            //[OpenRTB 2.6](https://github.com/InteractiveAdvertisingBureau/openrtb2.x/blob/main/2.6.md#objectuseragent)
            new Dictionary<string, object>()
            {
            {"query.51D_structureduseragent", 
                "{\"browsers\":[{\"brand\":\"Chromium\",\"version\":[\"124\",\"0\",\"6367\",\"91\"]},{\"brand\":"+
                "\"Google Chrome\",\"version\":[\"124\",\"0\",\"6367\",\"91\"]},{\"brand\":\"Not-A.Brand\",\"version\""+
                ":[\"99\",\"0\",\"0\",\"0\"]}],\"platform\":{\"brand\":\"Windows\",\"version\":[\"14\",\"0\",\"0\"]},"+
                "\"mobile\":0,\"architecture\":\"x86\",\"source\":2}"},
            }
        };

        /// <summary>
        /// Checks if an environment variable exists with the key name provided
        /// and then runs the action with the value, or an empty string if the
        /// key does not exist.
        /// </summary>
        /// <param name="envVarName"></param>
        /// <param name="setValue"></param>
        public static void GetKeyFromEnv(
            string envVarName,
            Action<string> setValue)
        {
            var superKey = Environment.GetEnvironmentVariable(envVarName);
            if (string.IsNullOrWhiteSpace(superKey) == false)
            {
                setValue(superKey);
            }
            else
            {
                setValue(string.Empty);
            }
        }
    }
}
