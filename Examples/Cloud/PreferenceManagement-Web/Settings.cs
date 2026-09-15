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

namespace FiftyOne.Examples.Cloud.PreferenceManagementWeb
{
    /// <summary>
    /// The input data, read from the environment variable names every
    /// language's copy of this demo reads.
    /// <para>
    /// Neither value is ever written to the console or a log, because a
    /// resource key is not for publishing and an endpoint can name a host
    /// that is not public. Only the name of the variable each came from is.
    /// </para>
    /// </summary>
    public sealed class Settings
    {
        /// <summary>
        /// The resource key the pages are filled with. Read first.
        /// </summary>
        public const string RESOURCE_KEY_ENV_VAR = "51DEGREES_RESOURCE_KEY";

        /// <summary>
        /// The names this repository's other examples read the resource key
        /// from, read in this order when the aligned name is not set.
        /// </summary>
        public static readonly string[] LEGACY_RESOURCE_KEY_ENV_VARS =
            { "_51DEGREES_RESOURCE_KEY", "SUPER_RESOURCE_KEY" };

        /// <summary>
        /// The cloud the pages load the platform and the client script from.
        /// Read first.
        /// </summary>
        public const string CLOUD_ENDPOINT_ENV_VAR = "51DEGREES_CLOUD_ENDPOINT";

        /// <summary>
        /// The names this repository's other examples read a cloud endpoint
        /// from, read in this order when the aligned name is not set.
        /// </summary>
        public static readonly string[] LEGACY_CLOUD_ENDPOINT_ENV_VARS =
            { "51D_CLOUD_ENDPOINT", "FIFTYONE_CLOUD_ENDPOINT" };

        /// <summary>
        /// The cloud used when no endpoint variable is set.
        /// </summary>
        public const string DEFAULT_CLOUD_ENDPOINT =
            "https://cloud.51degrees.com";

        /// <summary>
        /// The path every endpoint the pages use sits under. An endpoint
        /// variable may be given with or without it, because the other
        /// repositories that read 51DEGREES_CLOUD_ENDPOINT give it with.
        /// </summary>
        private const string API_PATH = "/api/v4";

        private Settings(
            string resourceKey,
            string resourceKeyVariable,
            string cloudEndpoint,
            string cloudEndpointVariable)
        {
            ResourceKey = resourceKey;
            ResourceKeyVariable = resourceKeyVariable;
            CloudEndpoint = cloudEndpoint;
            CloudEndpointVariable = cloudEndpointVariable;
        }

        /// <summary>The resource key.</summary>
        public string ResourceKey { get; }

        /// <summary>The variable the resource key came from.</summary>
        public string ResourceKeyVariable { get; }

        /// <summary>
        /// The cloud's address with no trailing slash and without the API
        /// path, for example https://cloud.51degrees.com.
        /// </summary>
        public string CloudEndpoint { get; }

        /// <summary>
        /// Where the cloud endpoint came from, being the variable's name in
        /// quotes or a note saying the default is in use.
        /// </summary>
        public string CloudEndpointVariable { get; }

        /// <summary>
        /// Reads both values from the environment.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// No resource key is set, or the endpoint is not an absolute http
        /// or https address.
        /// </exception>
        public static Settings FromEnvironment()
        {
            var (keyVariable, key) = First(
                RESOURCE_KEY_ENV_VAR, LEGACY_RESOURCE_KEY_ENV_VARS);
            if (key == null)
            {
                throw new InvalidOperationException(
                    $"No resource key is set. Set the environment variable " +
                    $"'{RESOURCE_KEY_ENV_VAR}' to a 51Degrees resource key " +
                    "that includes the 51Did properties. The names " +
                    $"'{string.Join("' and '", LEGACY_RESOURCE_KEY_ENV_VARS)}' " +
                    "are read when it is not set. A resource key can be " +
                    "created at https://configure.51degrees.com?utm_source=code&utm_medium=example&utm_campaign=device-detection-dotnet-examples&utm_content=examples-cloud-preferencemanagement-web-settings.cs&utm_term=resource-key-required");
            }

            var (endpointVariable, endpoint) = First(
                CLOUD_ENDPOINT_ENV_VAR, LEGACY_CLOUD_ENDPOINT_ENV_VARS);
            return new Settings(
                key,
                keyVariable!,
                endpoint == null
                    ? DEFAULT_CLOUD_ENDPOINT
                    : CloudBase(endpoint, endpointVariable!),
                endpointVariable == null
                    ? $"the default, {DEFAULT_CLOUD_ENDPOINT}"
                    : $"read from '{endpointVariable}'");
        }

        /// <summary>
        /// The placeholders a page under /cloud/ is filled with. The names
        /// are the ones in wwwroot/templates and README.md, and every
        /// language's copy of this demo fills the same ones.
        /// </summary>
        public IReadOnlyDictionary<string, string> CloudPlaceholders() =>
            new Dictionary<string, string>
            {
                ["RESOURCE_KEY"] = ResourceKey,
                ["CLOUD_ENDPOINT"] = CloudEndpoint,
                ["CLIENT_SCRIPT_URL"] =
                    $"{CloudEndpoint}{API_PATH}/" +
                    $"{Uri.EscapeDataString(ResourceKey)}.js"
            };

        /// <summary>
        /// The first variable that holds a value that is not blank, with
        /// its name, or two nulls where none does.
        /// </summary>
        private static (string? Name, string? Value) First(
            string aligned,
            IEnumerable<string> legacy)
        {
            foreach (var name in legacy.Prepend(aligned))
            {
                var value = Environment.GetEnvironmentVariable(name);
                if (string.IsNullOrWhiteSpace(value) == false)
                {
                    return (name, value.Trim());
                }
            }
            return (null, null);
        }

        /// <summary>
        /// The cloud's address from an endpoint given with or without the
        /// API path and a trailing slash.
        /// </summary>
        private static string CloudBase(string value, string variable)
        {
            var endpoint = value.TrimEnd('/');
            if (endpoint.EndsWith(API_PATH, StringComparison.OrdinalIgnoreCase))
            {
                endpoint = endpoint
                    .Substring(0, endpoint.Length - API_PATH.Length)
                    .TrimEnd('/');
            }
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) == false ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps))
            {
                // The value is not repeated, in case a key was put in the
                // wrong variable.
                throw new InvalidOperationException(
                    $"The environment variable '{variable}' must be an " +
                    "absolute http or https address, for example " +
                    $"'{DEFAULT_CLOUD_ENDPOINT}{API_PATH}/'.");
            }
            return endpoint;
        }
    }
}
