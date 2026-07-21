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
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// @example OnPremise/PerformanceSweep-Console/Program.cs
///
/// Property-read sweep "clock-time" performance benchmark.
///
/// The standard Performance-Console reads one property (IsMobile) per detection,
/// and PerformanceMultiProperty-Console reads every available property. This
/// example bridges the two: it runs the full pipeline per detection and reads a
/// varying number N of property values inside the same timed region, sweeping N.
///
/// The result shows how end-to-end throughput depends on how many properties an
/// integration reads per detection: detection-dominated and read-insensitive at
/// low N, read-dominated at high N. That makes it a useful tool for evaluating
/// property-read performance changes (device-detection-dotnet#524), whose effect
/// is per-read and so only shows in the top-line number once reads make up a
/// meaningful share of the work. N = 0 is a pure-detection control point.
///
/// N may exceed the number of distinct properties: the read loop cycles the
/// property list, so the higher sweep points (2x and 4x the property count) drive
/// the read share towards 100% and expose the asymptotic per-read cost.
///
/// Run with an optional data file and evidence file:
///   dotnet run -- [-d dataFile] [-u evidenceFile]
///
/// This example is available in full on
/// [GitHub](https://github.com/51Degrees/device-detection-dotnet-examples/blob/main/Examples/OnPremise/PerformanceSweep-Console/Program.cs).
///
/// @include{doc} example-require-datafile.txt
/// </summary>
namespace FiftyOne.DeviceDetection.Examples.OnPremise.PerformanceSweep
{
    public class Program
    {
        private const int DEFAULT_THREAD_COUNT = 4;
        private const int REPEATS = 3; // per N, report the median to damp noise

        public class Example : ExampleBase
        {
            public void Run(
                string dataFile,
                string evidenceFile,
                TextWriter output,
                int threadCount)
            {
                using (var loggerFactory = LoggerFactory.Create(b => { }))
                using (var pipeline = new DeviceDetectionPipelineBuilder(loggerFactory)
                    .UseOnPremise(dataFile, null, false)
                    .SetPerformanceProfile(PerformanceProfiles.MaxPerformance)
                    .SetShareUsage(false)
                    .SetAutoUpdate(false)
                    .SetDataUpdateOnStartUp(false)
                    .SetDataFileSystemWatcher(false)
                    .Build())
                {
                    var evidence = ReadEvidence(evidenceFile);
                    var properties = ExampleUtils.DiscoverReadableScalarProperties(
                        pipeline, evidence[0]);
                    if (properties.Count == 0)
                    {
                        output.WriteLine("No readable scalar properties were found in the " +
                            "data file, so there is nothing to sweep.");
                        return;
                    }
                    var sweep = BuildSweep(properties.Count);

                    output.WriteLine($"{properties.Count} scalar properties available; " +
                        $"{evidence.Count:N0} evidence records; {threadCount} threads; " +
                        $"MaxPerformance; data {Path.GetFileName(dataFile)}.");

                    // Warm up so JIT and native paths are hot before timing.
                    Benchmark(pipeline, evidence, properties, properties.Count, threadCount);
                    GC.Collect();
                    Task.Delay(500).Wait();

                    output.WriteLine("PropsRead,DetectionsPerSec,PropReadsPerSec,MsPerDetection");
                    foreach (var n in sweep)
                    {
                        var samples = new List<double>();
                        for (int r = 0; r < REPEATS; r++)
                        {
                            var ms = Benchmark(pipeline, evidence, properties, n, threadCount);
                            samples.Add(1000.0 / ms);
                        }
                        samples.Sort();
                        var dps = samples[samples.Count / 2]; // median
                        output.WriteLine($"{n},{dps:F0},{dps * n:F0},{1000.0 / dps:F6}");
                    }
                }
            }

            /// <summary>
            /// Sweep points: 0 (control), a ramp up to the number of available
            /// properties, then 2x and 4x that count. Reads above the distinct
            /// property count cycle the list, driving the read share towards 100%.
            /// Derived from the property count so it also works on the Lite file,
            /// which exposes far fewer properties than Enterprise.
            /// </summary>
            private static List<int> BuildSweep(int propCount)
            {
                var points = new SortedSet<int> { 0, 1 };
                foreach (var n in new[] { 5, 10, 25, 50, 100, 250 })
                {
                    if (n < propCount) { points.Add(n); }
                }
                points.Add(propCount);
                points.Add(propCount * 2);
                points.Add(propCount * 4);
                return points.ToList();
            }

            /// <summary>
            /// Run one timed pass over the evidence, reading <paramref name="readCount"/>
            /// property values inside each detection's timed region, and return the
            /// mean milliseconds per detection.
            /// </summary>
            private static double Benchmark(
                IPipeline pipeline,
                List<Dictionary<string, object>> evidence,
                List<string> properties,
                int readCount,
                int threadCount)
            {
                long totalDetections = 0;
                long totalTicks = 0;
                int propCount = properties.Count;
                var lockObj = new object();

                Parallel.ForEach(evidence,
                    new ParallelOptions { MaxDegreeOfParallelism = threadCount },
                    () => new long[2], // [0] = detections, [1] = stopwatch ticks
                    (values, loopState, local) =>
                    {
                        var sw = Stopwatch.StartNew();
                        using (var data = pipeline.CreateFlowData())
                        {
                            data.AddEvidence(values).Process();
                            var device = data.Get<IDeviceData>();
                            // Cycle the list so readCount may exceed propCount.
                            for (int i = 0; i < readCount; i++)
                            {
                                var value = device[properties[i % propCount]];
                            }
                        }
                        sw.Stop();
                        local[0] += 1;
                        local[1] += sw.ElapsedTicks;
                        return local;
                    },
                    local =>
                    {
                        lock (lockObj)
                        {
                            totalDetections += local[0];
                            totalTicks += local[1];
                        }
                    });

                // Convert ticks -> ms once, in double, to avoid per-partition rounding.
                var totalMs = totalTicks * 1000.0 / Stopwatch.Frequency;
                return totalMs / (totalDetections * threadCount);
            }
        }

        public static void Main(string[] args)
        {
            var options = ExampleUtils.ParseOptions(args);
            if (options == null) { return; }

            var dataFile = options.DataFilePath ??
                ExampleUtils.FindDataFile(Constants.LITE_HASH_DATA_FILE_NAME);
            var evidenceFile = options.EvidenceFile ??
                ExampleUtils.FindFile(Constants.YAML_EVIDENCE_FILE_NAME);

            if (string.IsNullOrWhiteSpace(dataFile) || File.Exists(dataFile) == false)
            {
                Console.WriteLine($"Data file not found: '{dataFile}'. Supply one with " +
                    "'-d <path>', or run 'git submodule update --recursive' to fetch the " +
                    "bundled Lite file.");
                return;
            }
            if (string.IsNullOrWhiteSpace(evidenceFile) || File.Exists(evidenceFile) == false)
            {
                Console.WriteLine($"Evidence file not found: '{evidenceFile}'. Supply one " +
                    "with '-u <path>', or run 'git submodule update --recursive'.");
                return;
            }

            new Example().Run(dataFile, evidenceFile, Console.Out, DEFAULT_THREAD_COUNT);
        }
    }
}
