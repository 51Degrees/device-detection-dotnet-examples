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
using FiftyOne.Pipeline.Engines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// @example OnPremise/Performance-Console/Program.cs
///
/// The example illustrates a "clock-time" benchmark for assessing detection speed.
///
/// Using a YAML formatted evidence file - "20000 Evidence Records.yml" - supplied with the
/// distribution or can be obtained from the [data repository on Github](https://github.com/51Degrees/device-detection-data/blob/master/20000%20Evidence%20Records.yml).
///
/// It's important to understand the trade-offs between performance, memory usage and accuracy, that
/// the 51Degrees pipeline configuration makes available, and this example shows a range of
/// different configurations to illustrate the difference in performance.
///
/// Requesting properties from a single component reduces detection time compared with requesting 
/// properties from multiple components. If you don't specify any properties to detect, then all 
/// properties are detected.
///
/// Please review [performance options](https://51degrees.com/documentation/_device_detection__features__performance_options.html)
/// and [hash dataset options](https://51degrees.com/documentation/_device_detection__hash.html#DeviceDetection_Hash_DataSetProduction_Performance)
/// for more information about adjusting performance.
/// 
/// This example is available in full on [GitHub](https://github.com/51Degrees/device-detection-dotnet-examples/blob/master/Examples/OnPremise/Performance-Console/Program.cs).
/// 
/// @include{doc} example-require-datafile.txt
/// 
/// Required NuGet Dependencies:
/// - [FiftyOne.DeviceDetection](https://www.nuget.org/packages/FiftyOne.DeviceDetection)
/// </summary>
namespace FiftyOne.DeviceDetection.Examples.OnPremise.Performance
{
    public class Program
    {
        public class BenchmarkResult
        {
            public long Count { get; set; }
            public Stopwatch Timer { get; } = new Stopwatch();
            public long MobileCount { get; set; }
            public long NotMobileCount { get; set; }
        }

        private static readonly PerformanceConfiguration[] _configs = new PerformanceConfiguration[]
        {
            new PerformanceConfiguration(true, PerformanceProfiles.MaxPerformance, false, true, false),
            new PerformanceConfiguration(true, PerformanceProfiles.MaxPerformance, true, true, false),
            new PerformanceConfiguration(false, PerformanceProfiles.LowMemory, false, true, false),
            new PerformanceConfiguration(false, PerformanceProfiles.LowMemory, true, true, false)
        };

        private const ushort DEFAULT_THREAD_COUNT = 4;

        private static float GetMsPerDetection(
            IList<BenchmarkResult> results,
            int threadCount)
        {
            var detections = results.Sum(r => r.Count);
            var milliseconds = results.Sum(r => r.Timer.ElapsedMilliseconds);
            // Calculate approx. real-time ms per detection. 
            return (float)(milliseconds) / (detections * threadCount);
        }

        public class Example : ExampleBase
        {
            private IPipeline _pipeline;

            public Example(IPipeline pipeline)
            {
                _pipeline = pipeline;
            }

            private List<BenchmarkResult> Run(
                TextReader evidenceReader,
                TextWriter output,
                int threadCount,
                bool enableGC = true,
                long noGcRegionSize = 64 * 1024 * 1024)
            {
                var evidence = GetEvidence(evidenceReader).ToList();
                
                output.WriteLine($"Loaded {evidence.Count} evidence records");
                // Apply UTF-8 preprocessing to eliminate string conversion overhead
                evidence = Utf8EvidencePreprocessor.ConvertToUtf8(evidence);

                // Make an initial run to warm up the system
                output.WriteLine("Warming up");
                var warmup = Benchmark(evidence, threadCount);
                var warmupTime = warmup.Sum(r => r.Timer.ElapsedMilliseconds);
                GC.Collect();
                Task.Delay(500).Wait();

                output.WriteLine("Running");
                
                // Disable GC during performance measurement if GC is disabled
                bool gcWasDisabled = false;
                if (!enableGC)
                {
                    try
                    {
                        gcWasDisabled = GC.TryStartNoGCRegion(noGcRegionSize);
                        if (gcWasDisabled)
                        {
                            output.WriteLine($"GC disabled for performance test (NoGC region: {noGcRegionSize / (1024 * 1024)}MB)");
                        }
                        else
                        {
                            output.WriteLine("Could not disable GC - running with GC enabled");
                        }
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        output.WriteLine($"NoGC region size too large ({noGcRegionSize / (1024 * 1024)}MB) - running with GC enabled");
                    }
                }
                
                var execution = Benchmark(evidence, threadCount);
                var executionTime = execution.Sum(r => r.Timer.ElapsedMilliseconds);
                
                // Re-enable GC if it was disabled
                if (gcWasDisabled)
                {
                    GC.EndNoGCRegion();
                    output.WriteLine("GC re-enabled");
                }
                
                output.WriteLine($"Finished - Execution time was {executionTime} ms, " +
                    $"adjustment from warm-up {executionTime - warmupTime} ms");

                Report(execution, threadCount, output);
                return execution;
            }

            /// <summary>
            /// Output some results from the benchmarking
            /// </summary>
            /// <param name="results"></param>
            /// <param name="threadCount"></param>
            /// <param name="output"></param>
            private void Report(List<BenchmarkResult> results,
                int threadCount,
                TextWriter output)
            {
                // Calculate approx. real-time ms per detection. 
                var msPerDetection = GetMsPerDetection(results, threadCount);
                var detectionsPerSecond = 1000 / msPerDetection;
                output.WriteLine($"Overall: {results.Sum(i => i.Count)} detections, Average millisecs per " +
                    $"detection: {msPerDetection}, Detections per second: {detectionsPerSecond}");
                output.WriteLine($"Overall: Concurrent threads: {threadCount}");
            }

            /// <summary>
            /// Run the benchmark for the supplied evidence.
            /// </summary>
            /// <param name="allEvidence"></param>
            /// <param name="threadCount"></param>
            /// <returns></returns>
            private List<BenchmarkResult> Benchmark(
                List<Dictionary<string, object>> allEvidence, 
                int threadCount)
            {
                CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

                List<BenchmarkResult> results = new List<BenchmarkResult>();

                // Start multiple threads to process a set of evidence.
                var processing = Parallel.ForEach(allEvidence,
                    new ParallelOptions()
                    {
                        // Note - MaxDegreeOfParallelism does not actually guarantee anything
                        // about the number of threads involved.
                        // It just guarantees that there will be no more than x Tasks running 
                        // concurrently.
                        MaxDegreeOfParallelism = threadCount,
                        CancellationToken = cancellationTokenSource.Token
                    },
                    // Create a benchmark result instance per parallel unit
                    // (not necessarily per thread!).
                    () => new BenchmarkResult(),
                    (evidence, loopState, result) =>
                    {
                        result.Timer.Start();
                        // A using block MUST be used for the FlowData instance. This ensures that
                        // native resources created by the device detection engine are freed in
                        // good time.
                        using (var data = _pipeline.CreateFlowData())
                        {
                            // Add the evidence to the flow data.
                            data.AddEvidence(evidence).Process();

                            // Get the device from the engine.
                            var device = data.Get<IDeviceData>();

                            result.Count++;
                            // Access a property to ensure compiler optimizer doesn't optimize
                            // out the very method that the benchmark is testing.
                            if(device.IsMobile.HasValue && device.IsMobile.Value)
                            {
                                result.MobileCount++;
                            }
                            else
                            {
                                result.NotMobileCount++;
                            }
                        }

                        result.Timer.Stop();
                        return result;
                    },
                    // Add the results from this run to the overall results.
                    (result) => 
                    {
                        lock (results)
                        {
                            results.Add(result);
                        }
                    });

                return results;
            }

            /// <summary>
            /// This Run method is called by the example test to avoid the need to duplicate the 
            /// service provider setup logic.
            /// </summary>
            /// <param name="options"></param>
            public static List<BenchmarkResult> Run(string dataFile, string evidenceFile, 
                PerformanceConfiguration config, TextWriter output, ushort threadCount, bool enableGC = true, long noGcRegionSize = 64 * 1024 * 1024)
            {
                // Initialize a service collection which will be used to create the services
                // required by the Pipeline and manage their lifetimes.
                using (var serviceProvider = new ServiceCollection()
                    // Make sure we're logging to the console.
                    .AddLogging(l => l.AddConsole())
                    .AddTransient<PipelineBuilder>()
                    .AddTransient<DeviceDetectionHashEngineBuilder>()
                    // Add a factory to create the singleton DeviceDetectionHashEngine instance.
                    .AddSingleton((x) =>
                    {
                        var builder = x.GetRequiredService<DeviceDetectionHashEngineBuilder>()
                            // Disable any data file updates
                            .SetDataFileSystemWatcher(false)
                            .SetAutoUpdate(false)
                            .SetDataUpdateOnStartup(false)
                            // Set performance profile
                            .SetPerformanceProfile(config.Profile)
                            // Configure detection graphs
                            .SetUsePerformanceGraph(config.PerformanceGraph)
                            .SetUsePredictiveGraph(config.PredictiveGraph)
                            // Hint for cache concurrency
                            .SetConcurrency(threadCount);

                        // Performance is improved by selecting only the properties you intend to
                        // use. Requesting properties from a single component reduces detection
                        // time compared with requesting properties from multiple components.
                        // If you don't specify any properties to detect, then all properties are
                        // detected, here we choose "all properties" by specifying none, or just
                        // "isMobile".
                        // Specify "BrowserName" for just the browser component, "PlatformName"
                        // for just platform or "IsCrawler" for the crawler component.
                        if (config.AllProperties == false)
                        {
                            builder.SetProperty("IsMobile");
                        }

                        // The data file can be loaded directly from disk or from a byte array
                        // in memory.
                        // This latter option is useful for cloud-based environments with little 
                        // or no hard drive space available. In this scenario, the 'LowMemory' 
                        // performance profile is recommended, as the data is actually already
                        // in memory. Using MaxPerformance would just cause the native code to 
                        // make another copy of the data in memory for little benefit.
                        DeviceDetectionHashEngine engine = null;
                        if (config.LoadFromDisk)
                        {
                            engine = builder.Build(dataFile, false);
                        }
                        else
                        {
                            using (MemoryStream stream = new MemoryStream(File.ReadAllBytes(dataFile)))
                            {
                                engine = builder.Build(stream);
                            }
                        }

                        return engine;
                    })
                    // Add a factory to create the singleton IPipeline instance
                    .AddSingleton((x) => {
                        return x.GetRequiredService<PipelineBuilder>()
                            .AddFlowElement(x.GetRequiredService<DeviceDetectionHashEngine>())
                            .Build();
                    })
                    .AddTransient<Example>()
                    .BuildServiceProvider())
                using (var evidenceReader = new StreamReader(File.OpenRead(evidenceFile)))
                {
                    // If we don't have a resource key then log an error.
                    if (string.IsNullOrWhiteSpace(dataFile))
                    {
                        serviceProvider.GetRequiredService<ILogger<Program>>().LogError(
                            "Failed to find a device detection data file. Make sure the " +
                            "device-detection-data submodule has been updated by running " +
                            "`git submodule update --recursive`.");
                        return null;
                    }
                    else
                    {
                        ExampleUtils.CheckDataFile(
                            serviceProvider.GetRequiredService<IPipeline>(), 
                            serviceProvider.GetRequiredService<ILogger<Program>>());
                        output.WriteLine($"Processing evidence from '{evidenceFile}'");
                        output.WriteLine($"Data loaded from '{(config.LoadFromDisk ? "disk" : "memory")}'");
                        output.WriteLine($"Benchmarking with profile '{config.Profile}', " +
                            $"AllProperties {config.AllProperties}, " +
                            $"PerformanceGraph {config.PerformanceGraph}, " +
                            $"PredictiveGraph {config.PredictiveGraph}");

                        return serviceProvider.GetRequiredService<Example>().Run(evidenceReader, output, threadCount, enableGC, noGcRegionSize);
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            // Use the supplied path for the data file or find the lite file that is included
            // in the repository.
            var options = ExampleUtils.ParseOptions(args);
            if (options != null) {
                // First try to use TAC-HashV41.hash if available
                var dataFile = options.DataFilePath != null ? options.DataFilePath :
                    ExampleUtils.FindFile("TAC-HashV41.hash") ?? 
                    // In this example, by default, the 51degrees "Lite" file needs to be somewhere in the
                    // project space, or you may specify another file as a command line parameter.
                    //
                    // Note that the Lite data file is only used for illustration, and has limited accuracy
                    // and capabilities. Find out about the Enterprise data file on our pricing page:
                    // https://51degrees.com/pricing
                    ExampleUtils.FindFile(Constants.LITE_HASH_DATA_FILE_NAME);
                // Do the same for the yaml evidence file.
                var evidenceFile = options.EvidenceFile != null ? options.EvidenceFile :
                    // This file contains the 20,000 most commonly seen combinations of header values 
                    // that are relevant to device detection. For example, User-Agent and UA-CH headers.
                    ExampleUtils.FindFile(Constants.YAML_EVIDENCE_FILE_NAME);

                // Run benchmarks with GC enabled (default behavior)
                Console.WriteLine("\n==== Running benchmarks WITH Garbage Collection ====\n");
                var resultsWithGC = new Dictionary<PerformanceConfiguration, IList<BenchmarkResult>>();
                foreach (var config in _configs)
                {
                    var result = Example.Run(dataFile, evidenceFile, config, Console.Out, DEFAULT_THREAD_COUNT, true);
                    resultsWithGC[config] = result;
                }

                // Use the optimal NoGC region size (128MB showed best performance)
                var optimalNoGcSize = 128 * 1024 * 1024; // 128MB
                Console.WriteLine($"\n==== Running benchmarks WITHOUT Garbage Collection ({optimalNoGcSize / (1024 * 1024)}MB NoGC Region) ====\n");
                var resultsWithoutGC = new Dictionary<PerformanceConfiguration, IList<BenchmarkResult>>();
                
                foreach (var config in _configs)
                {
                    var result = Example.Run(dataFile, evidenceFile, config, Console.Out, DEFAULT_THREAD_COUNT, false, optimalNoGcSize);
                    resultsWithoutGC[config] = result;
                }

                // Print comparison summary
                Console.WriteLine("\n==== Performance Comparison Summary ====\n");
                Console.WriteLine("Configuration\t\t\t\tWith GC (ms/det)\tWithout GC (ms/det)\tImprovement %\tDet/Sec (GC)\tDet/Sec (No GC)");
                Console.WriteLine(new string('-', 120));
                
                foreach (var config in _configs)
                {
                    var msWithGC = GetMsPerDetection(resultsWithGC[config], DEFAULT_THREAD_COUNT);
                    var msWithoutGC = GetMsPerDetection(resultsWithoutGC[config], DEFAULT_THREAD_COUNT);
                    var improvement = ((msWithGC - msWithoutGC) / msWithGC) * 100;
                    var detectionsPerSecWithGC = 1000 / msWithGC;
                    var detectionsPerSecWithoutGC = 1000 / msWithoutGC;
                    
                    var configName = $"{config.Profile}{(config.AllProperties ? " All" : " Specific")}";
                    Console.WriteLine($"{configName,-35}\t{msWithGC:F4}\t\t{msWithoutGC:F4}\t\t{improvement:F1}%\t\t{detectionsPerSecWithGC:F0}\t\t{detectionsPerSecWithoutGC:F0}");
                }

                if (string.IsNullOrEmpty(options.JsonOutput) == false)
                {
                    using (var jsonOutput = File.CreateText(options.JsonOutput))
                    {
                        // Use the best results (without GC) for JSON output to maintain compatibility
                        var jsonResults = resultsWithoutGC.ToDictionary(
                            k => $"{Enum.GetName(k.Key.Profile)}{(k.Key.AllProperties ? "_All" : "")}",
                            v => new Dictionary<string, float>()
                            {
                                {"DetectionsPerSecond", 1000 / GetMsPerDetection(v.Value, DEFAULT_THREAD_COUNT) },
                                {"DetectionsPerSecondPerThread", 1000 / (GetMsPerDetection(v.Value, DEFAULT_THREAD_COUNT) * DEFAULT_THREAD_COUNT) },
                                {"MsPerDetection", GetMsPerDetection(v.Value, DEFAULT_THREAD_COUNT) }
                            });
                        jsonOutput.Write(JsonSerializer.Serialize(jsonResults));
                    }
                }
            }
        }

    }
}
