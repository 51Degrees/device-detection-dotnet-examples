﻿using System.Linq;

namespace FiftyOne.DeviceDetection.Examples
{
    public class Constants
    {
        /// <summary>
        /// The default name of the paid-for enterprise device detection data file.
        /// </summary>
        public const string ENTERPRISE_HASH_DATA_FILE_NAME = "Enterprise-HashV41.hash";

        /// <summary>
        /// The default name of the free 'lite' device detection data file.
        /// </summary>
        public const string LITE_HASH_DATA_FILE_NAME = "51Degrees-LiteV4.1.hash";

        /// <summary>
        /// The default name of the file that is used by the AppleProfileEngine
        /// </summary>
        public const string APPLE_DATA_FILE_NAME = "macintosh.data.json";

        /// <summary>
        /// Name of the file to use for the test evidence.
        /// </summary>
        public const string YAML_EVIDENCE_FILE_NAME = "20000 Evidence Records.yml";

        /// <summary>
        /// Environment variable key for the license key file to use for the 
        /// tests.
        /// </summary>
        public const string LICENSE_KEY_ENV_VAR = "DEVICEDETECTIONLICENSEKEY_DOTNET";

        /// <summary>
        /// Environment variable key for the data file to use for the tests.
        /// </summary>
        public const string DEVICE_DETECTION_DATA_FILE_ENV_VAR = "DEVICEDETECTIONDATAFILE";

        /// <summary>
        /// Environment variable key for the evidence file to use for the tests.
        /// </summary>
        public const string EVIDENCE_FILE_ENV_VAR = "EVIDENCEFILE";

        /// <summary>
        /// Ports used for running web examples and their tests.
        /// </summary>
        public static readonly int[] LOCALHOST_HTTP_PORTS = new int[] { 5101 };
        public static readonly int[] LOCALHOST_HTTPS_PORTS = new int[] { 5001, 5002 };

        /// <summary>
        /// The URLs that the web server should listen on.
        /// </summary>
        public static string[] AllUrls =>
            Constants.LOCALHOST_HTTP_PORTS.Select(i =>
                $"http://localhost:{i}").Concat(
                Constants.LOCALHOST_HTTPS_PORTS.Select(i =>
                    $"https://localhost:{i}")).ToArray();
    }
}
