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
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// @example OnPremise/PerformanceMultiProperty-Console/Program.cs
///
/// Multi-property "clock-time" performance benchmark.
///
/// The standard Performance-Console reads a single property (IsMobile) per
/// detection, which is the least favourable case for the property-read
/// optimisations (device-detection-dotnet#524): those savings are *per property
/// read*, so a one-property loop barely shows them. This example instead reads
/// *every* available scalar property on each detection - much closer to how real
/// integrations consume the results - so the read-path improvement is visible in
/// the end-to-end detections/sec number. It also reports an isolated
/// property-read throughput that removes the detection cost entirely.
///
/// Run with an optional data file and evidence file:
///   dotnet run -- [-d dataFile] [-u evidenceFile]
///
/// This example is available in full on
/// [GitHub](https://github.com/51Degrees/device-detection-dotnet-examples/blob/main/Examples/OnPremise/PerformanceMultiProperty-Console/Program.cs).
///
/// @include{doc} example-require-datafile.txt
/// </summary>
namespace FiftyOne.DeviceDetection.Examples.OnPremise.PerformanceMultiProperty
{
    public class Program
    {
        private const int DEFAULT_THREAD_COUNT = 4;
        private const long READS_PER_THREAD = 5_000_000;

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
                            "data file, so there is nothing to benchmark.");
                        return;
                    }

                    output.WriteLine($"Reading {properties.Count} scalar properties per " +
                        $"detection over {evidence.Count:N0} evidence records on " +
                        $"{threadCount} threads.");
                    output.WriteLine($"Data: {Path.GetFileName(dataFile)}, profile: MaxPerformance.");
                    output.WriteLine();

                    output.WriteLine("Warming up...");
                    Benchmark(pipeline, evidence, properties, threadCount);
                    GC.Collect();
                    Task.Delay(500).Wait();

                    output.WriteLine("Running detection throughput...");
                    var msPerDetection = Benchmark(pipeline, evidence, properties, threadCount);
                    Report(msPerDetection, properties.Count, threadCount, output);

                    // Isolated property-read throughput: process one detection per
                    // thread, then read every property in a tight loop. This removes
                    // the CreateFlowData / Process / native-detection cost (which
                    // dominates and adds noise) so the property-read path - the work
                    // the #524 fixes target - is measured directly.
                    output.WriteLine();
                    output.WriteLine("Running isolated property-read throughput...");
                    ReadThroughput(pipeline, evidence, properties, threadCount, output);
                }
            }

            /// <summary>
            /// Run one timed pass over the evidence, reading every property inside
            /// each detection's timed region, and return the mean ms per detection.
            /// </summary>
            private static double Benchmark(
                IPipeline pipeline,
                List<Dictionary<string, object>> evidence,
                List<string> properties,
                int threadCount)
            {
                long totalDetections = 0;
                long totalTicks = 0;
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
                            // Read every property - the work the #524 fixes target.
                            for (int i = 0; i < properties.Count; i++)
                            {
                                var value = device[properties[i]];
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

                var totalMs = totalTicks * 1000.0 / Stopwatch.Frequency;
                return totalMs / (totalDetections * threadCount);
            }

            private static void Report(
                double msPerDetection, int propertyCount, int threadCount, TextWriter output)
            {
                var detectionsPerSecond = 1000.0 / msPerDetection;
                output.WriteLine();
                output.WriteLine($"Overall: {propertyCount} properties each.");
                output.WriteLine($"  Detections/sec:      {detectionsPerSecond:N0}");
                output.WriteLine($"  Property reads/sec:  {detectionsPerSecond * propertyCount:N0}");
                output.WriteLine($"  ms per detection:    {msPerDetection:N4}");
                output.WriteLine($"  Concurrent threads:  {threadCount}");
            }

            private static void ReadThroughput(
                IPipeline pipeline,
                List<Dictionary<string, object>> evidence,
                List<string> properties,
                int threadCount,
                TextWriter output)
            {
                var elapsedMs = new double[threadCount];
                var reads = new long[threadCount];
                var threads = new Thread[threadCount];
                for (int t = 0; t < threadCount; t++)
                {
                    int idx = t;
                    var values = evidence[idx % evidence.Count];
                    threads[idx] = new Thread(() =>
                    {
                        // Each thread reads its OWN device data - no cross-thread lock
                        // contention, so this measures raw read cost.
                        using (var data = pipeline.CreateFlowData())
                        {
                            data.AddEvidence(values).Process();
                            var device = data.Get<IDeviceData>();
                            for (int w = 0; w < 100_000; w++) // warm up this thread
                            {
                                var _ = device[properties[w % properties.Count]];
                            }
                            var sw = Stopwatch.StartNew();
                            long n = 0;
                            while (n < READS_PER_THREAD)
                            {
                                for (int i = 0; i < properties.Count && n < READS_PER_THREAD; i++)
                                {
                                    var _ = device[properties[i]];
                                    n++;
                                }
                            }
                            sw.Stop();
                            // Stopwatch ticks -> ms in double, so the derived ns/read is
                            // not quantised to whole-millisecond resolution.
                            elapsedMs[idx] = sw.ElapsedTicks * 1000.0 / Stopwatch.Frequency;
                            reads[idx] = n;
                        }
                    });
                }
                foreach (var th in threads) { th.Start(); }
                foreach (var th in threads) { th.Join(); }

                long totalReads = reads.Sum();
                double wallMs = elapsedMs.Max(); // threads run concurrently
                double readsPerSecond = totalReads / (wallMs / 1000.0);
                double nsPerRead = wallMs * 1e6 / (totalReads / (double)threadCount);
                output.WriteLine($"  Property reads/sec (all threads): {readsPerSecond:N0}");
                output.WriteLine($"  ns per read (per thread):         {nsPerRead:N1}");
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
