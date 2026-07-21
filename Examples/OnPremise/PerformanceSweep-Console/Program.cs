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
/// Property-read sweep performance benchmark.
///
/// The standard Performance-Console reads one property (IsMobile) per detection,
/// and PerformanceMultiProperty-Console reads every available property. This
/// example bridges the two: it runs the full pipeline per detection and reads a
/// varying number N of property values inside the same timed region, sweeping N.
///
/// The result shows how end-to-end throughput depends on how many properties an
/// integration reads per detection - detection-dominated and read-insensitive at
/// low N, read-dominated at high N. That makes it a useful tool for evaluating
/// property-read performance changes (device-detection-dotnet#524), whose effect
/// is per-read and therefore only visible in the top-line number once reads make
/// up a meaningful share of the work. N = 0 is a pure-detection control point.
///
/// N may exceed the number of distinct properties: the read loop cycles the
/// property list, so a large N drives the read share towards 100% and exposes the
/// asymptotic per-read cost.
///
/// Run with an optional data file and evidence file:
///   dotnet run -- [-d dataFile] [-u evidenceFile]
/// </summary>
namespace FiftyOne.DeviceDetection.Examples.OnPremise.PerformanceSweep
{
    public class Program
    {
        // Small ExampleBase subclass so we can reuse the protected YAML reader.
        private class EvidenceReader : ExampleBase
        {
            public static List<Dictionary<string, object>> Read(string path)
            {
                using (var reader = new StreamReader(File.OpenRead(path)))
                {
                    return GetEvidence(reader).ToList();
                }
            }
        }

        private const int THREADS = 4;
        private const int REPEATS = 3; // per N, report the median to damp noise

        // Match-metric properties are computed in managed code, and multi-value
        // (list) properties don't exercise the scalar value getters, so exclude
        // both - the sweep should measure the scalar property-read path.
        private static readonly HashSet<string> MetricProperties =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "DeviceId", "Difference", "Drift", "Method", "Iterations", "MatchedNodes", "UserAgents" };
        private static readonly HashSet<Type> ScalarTypes =
            new HashSet<Type> { typeof(string), typeof(bool), typeof(int), typeof(double) };

        public static void Main(string[] args)
        {
            var options = ExampleUtils.ParseOptions(args);
            if (options == null) { return; }
            var dataFile = options.DataFilePath ?? ExampleUtils.FindDataFile(Constants.LITE_HASH_DATA_FILE_NAME);
            var evidenceFile = options.EvidenceFile ?? ExampleUtils.FindFile(Constants.YAML_EVIDENCE_FILE_NAME);

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
                var evidence = EvidenceReader.Read(evidenceFile);
                var properties = DiscoverReadableProperties(pipeline, evidence[0]);
                var sweep = BuildSweep(properties.Count);

                Console.WriteLine($"{properties.Count} scalar properties available; " +
                    $"{evidence.Count:N0} evidence records; {THREADS} threads; MaxPerformance; " +
                    $"data {Path.GetFileName(dataFile)}.");

                // One warmup at the largest read count so JIT/native paths are hot.
                Benchmark(pipeline, evidence, properties, sweep.Max());
                GC.Collect(); Task.Delay(500).Wait();

                Console.WriteLine("PropsRead,DetectionsPerSec,PropReadsPerSec,MsPerDetection");
                foreach (var n in sweep)
                {
                    var samples = new List<double>();
                    for (int r = 0; r < REPEATS; r++)
                    {
                        var ms = Benchmark(pipeline, evidence, properties, n);
                        samples.Add(1000.0 / ms);
                    }
                    samples.Sort();
                    double dps = samples[samples.Count / 2]; // median
                    Console.WriteLine($"{n},{dps:F0},{dps * n:F0},{1000.0 / dps:F6}");
                }
            }
        }

        /// <summary>
        /// Sweep points: 0 (control), then a geometric ramp up to the number of
        /// available properties. Data-file agnostic - Lite exposes far fewer
        /// properties than Enterprise, so the ramp is derived from the count.
        /// </summary>
        private static List<int> BuildSweep(int propCount)
        {
            var points = new SortedSet<int> { 0, 1 };
            foreach (var n in new[] { 5, 10, 25, 50, 100, 250 })
            {
                if (n < propCount) { points.Add(n); }
            }
            points.Add(propCount);
            return points.ToList();
        }

        private static double Benchmark(
            IPipeline pipeline, List<Dictionary<string, object>> evidence,
            List<string> properties, int readCount)
        {
            long totalDetections = 0, totalTicks = 0;
            int propCount = properties.Count;
            var lockObj = new object();
            Parallel.ForEach(evidence,
                new ParallelOptions { MaxDegreeOfParallelism = THREADS },
                () => new long[2], // [0]=count [1]=ticks
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
                    local[0]++; local[1] += sw.ElapsedTicks;
                    return local;
                },
                local => { lock (lockObj) { totalDetections += local[0];
                    totalTicks += local[1]; } });

            // Convert ticks -> ms once, in double, to avoid per-partition rounding.
            double totalMs = totalTicks * 1000.0 / Stopwatch.Frequency;
            return totalMs / (totalDetections * THREADS); // ms per detection
        }

        private static List<string> DiscoverReadableProperties(
            IPipeline pipeline, Dictionary<string, object> sampleEvidence)
        {
            var candidates = pipeline.ElementAvailableProperties["device"].Values
                .Where(p => ScalarTypes.Contains(p.Type) && !MetricProperties.Contains(p.Name))
                .Select(p => p.Name).ToList();
            var readable = new List<string>();
            using (var data = pipeline.CreateFlowData())
            {
                data.AddEvidence(sampleEvidence).Process();
                var device = data.Get<IDeviceData>();
                foreach (var name in candidates)
                {
                    try { var _ = device[name]; readable.Add(name); } catch { }
                }
            }
            return readable;
        }
    }
}
