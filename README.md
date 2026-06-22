## Examples

The examples provided demonstrate how to use the 51Degrees device detection in
both cloud-based and on-premise environments. They showcase different aspects of
device detection, including retrieving device details based on User-Agent and
User-Agent Client Hints HTTP header values, accessing meta-data, performing TAC
and native model lookups, viewing match metrics, offline processing, and
performance optimization. These examples serve as practical guides for
developers to understand and implement device detection capabilities within
their applications.

## Cloud resource keys

The cloud property tiers changed in May 2026. The examples and this
documentation now reflect what is free and what needs a paid subscription.

A free resource key that selects the free tier properties can be created
[here](https://configure.51degrees.com/Wkqxf3Bs?utm_source=github&utm_medium=readme&utm_campaign=device-detection-dotnet-examples&utm_content=readme.md&utm_term=cloud-resource-keys). A resource key that also
includes the paid properties used by the examples can be created
[here](https://configure.51degrees.com/hYzn3TV3?utm_source=github&utm_medium=readme&utm_campaign=device-detection-dotnet-examples&utm_content=readme.md&utm_term=cloud-resource-keys). See
https://51degrees.com/pricing?utm_source=github&utm_medium=readme&utm_campaign=device-detection-dotnet-examples&utm_content=readme.md&utm_term=cloud-resource-keys to get a paid subscription with more properties.

Of the properties the examples display, the free tier includes:

- IsMobile, DeviceType, IsCrawler, DeviceId, UserAgents
- ScreenPixelsWidth, ScreenPixelsHeight, ScreenPixelsHeightJavaScript
- The three SetHeader*Accept-CH properties
- JavascriptGetHighEntropyValues

A paid subscription is needed for:

- HardwareVendor, HardwareName, HardwareModel
- PlatformVendor, PlatformName, PlatformVersion
- BrowserVendor, BrowserName, BrowserVersion
- ScreenPixelsWidthJavaScript, JavascriptHardwareProfile, Promise, Fetch,
  PriceBand
- The TAC and native model hardware profile properties

To use the resource key in the example it can be supplied as an environment
variable called "_51DEGREES_RESOURCE_KEY". The legacy environment variable name
"SUPER_RESOURCE_KEY" is still supported, with the aligned "_51DEGREES_RESOURCE_KEY"
name checked first.

Some on-premise examples require you to provide a license key. You can find
out about resource keys and license keys at our [pricing
page](https://51degrees.com/pricing?utm_source=github&utm_medium=readme&utm_campaign=device-detection-dotnet-examples&utm_content=readme.md&utm_term=cloud-resource-keys-2).

## On-premise data file

The on-premise examples need a device detection data file. The examples locate
the file in the following order:

1. The "_51DEGREES_DD_PATH" environment variable, which can be set to an
   explicit path to the data file. The legacy "DEVICEDETECTIONDATAFILE"
   environment variable is also still supported, and is checked after
   "_51DEGREES_DD_PATH".
2. A search of the folder hierarchy, walking up from the working directory,
   for the expected data file name.
3. The free 'Lite' data file in its expected location, which is the
   device-detection-data submodule of this repository.

Note that the aligned variable names start with a digit, which POSIX shells
do not accept in plain assignments. On Linux and macOS set them with the env
command, for example `env _51DEGREES_RESOURCE_KEY=AAA dotnet run`.

## Running examples with changes to Pipeline packages

A common use case is to make a change to the Pipeline logic in
device-detection-dotnet and then use these examples to observe the results of
the change.

By default, the examples are configured to use the packages from nuget feed. In
order to produce and use local packages instead:

-   Clone and make your changes to device-detection-dotnet
-   Create and install the packages to the local nuget feed `dotnet pack
    [Project] -o "[PackagesFolder]" /p:PackageVersion=0.0.0 -c [Configuration]
    /p:Platform=[Architecture]` `dotnet nuget push "[PackagesFolder]/*.nupkg" -s
    LocalFeed `
-   Update the version of the device-detection-dotnet in the examples project: `
    dotnet add package "FiftyOne.DeviceDetection" --version 0.0.0 --source
    LocalFeed`

The same principle can be applied to incorporate changes in pipeline-dotnet if
needed.

The tables below describe the examples available in this repository.

### Cloud

| Example                       | Description                                                                                                                                                                                                                     |
|-------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| GettingStarted-Console        | How to use the 51Degrees Cloud service to determine details about a device based on its User-Agent and User-Agent Client Hints HTTP header values.                                                                              |
| GettingStarted-Web            | How to use the 51Degrees Cloud service to determine details about a device as part of a simple ASP.NET website.                                                                                                                 |
| GettingStarted-Web-ClientOnly | A simple ASP.NET website used for integration testing with cloud services. Defaults to using cloud.51degrees.com as the cloud end point.                                                                                        |
| Metadata                      | How to access the meta-data that relates to things like the properties populated device detection                                                                                                                               |
| TacLookup                     | How to get device details from a TAC (Type Allocation Code) using the 51Degrees cloud service.                                                                                                                                  |
| NativeModelLookup             | How to get device details from a native model name using the 51Degrees cloud service.                                                                                                                                           |
| GetAllProperties              | How to iterate through all properties available in the cloud response.                                                                                                                                                          |
| ClientHints                   | Legacy example. Retained for the associated automated tests. See GettingStarted-Web instead.                                                                                                                                    |
| ClientHints Not Integrated    | Legacy example. Our ASP.NET integration will automatically set the `Accept-CH` header that is used to request User-Agent Client Hints headers.This example demonstrates how to do this without using the integration component. |
| Framework-Web                 | How to use the 51Degrees Cloud service to determine details about a device as part of a simple ASP Framework website using System.Web.                                                                                          |

### On-Premise

| Example                    | Description                                                                                                                                                                                                                      |
|----------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| GettingStarted-Console     | How to use the 51Degrees on-premise device detection API to determine details about a device based on its User-Agent and User-Agent Client Hints HTTP header values.                                                             |
| GettingStarted-Web         | How to use the 51Degrees on-premise service to determine details about a device as part of a simple ASP.NET website.                                                                                                             |
| Metadata-Console           | How to access the meta-data that relates to things like the properties populated device detection                                                                                                                                |
| OfflineProcessing-Console  | Example showing how to ingest a file containing data from web requests and perform detection against the entries.                                                                                                                |
| Performance-Console        | How to configure the various performance options and run a simple performance test.                                                                                                                                              |
| UpdateDataFile—Console     | How to configure the Pipeline to automatically update the device detection data file on startup.                                                                                                                                 |
| MatchMetrics-Console       | How to retrieve meta data concerning properties and values from the data file.                                                                                                                                                   |
| ClientHints                | Legacy example. Retained for the associated automated tests. See GettingStarted-Web instead.                                                                                                                                     |
| ClientHints Not Integrated | Legacy example. Our ASP.NET integration will automatically set the `Accept-CH` header that is used to request User-Agent Client Hints headers. This example demonstrates how to do this without using the integration component. |
| Framework-Web              | How to use the 51Degrees on-premise service to determine details about a device as part of a simple ASP Framework website using System.Web.                                                                                      |
