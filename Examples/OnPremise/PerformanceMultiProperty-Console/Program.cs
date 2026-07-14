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
using FiftyOne.DeviceDetection.Shared.Data;
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
/// Multi-property "clock-time" performance benchmark.
///
/// The standard Performance-Console reads a single property (IsMobile) per
/// detection, which is the least favourable case for the property-read
/// optimisations (issue #524): those savings are *per property read*, so a
/// one-property loop barely shows them. This example instead reads *every*
/// available property on each detection - much closer to how real integrations
/// consume the results - so the read-path improvement is obvious in the
/// end-to-end detections/sec number.
///
/// Run with an optional data file and evidence file:
///   dotnet run -- [dataFile] [evidenceFile] [threadCount]
/// </summary>
namespace FiftyOne.DeviceDetection.Examples.OnPremise.PerformanceMultiProperty
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

        private class BenchmarkResult
        {
            public long Count;
            public readonly Stopwatch Timer = new Stopwatch();
        }

        private const int DEFAULT_THREAD_COUNT = 4;

        public static void Main(string[] args)
        {
            var options = ExampleUtils.ParseOptions(args);
            if (options == null) { return; }

            var dataFile = options.DataFilePath ??
                ExampleUtils.FindDataFile(Constants.LITE_HASH_DATA_FILE_NAME);
            // FindDataFile honours the DEVICE_DETECTION_DATA_FILE env var; if that
            // points at a file that isn't present, fall back to the Lite file that
            // ships in the data submodule so the example still runs.
            if (string.IsNullOrWhiteSpace(dataFile) || !File.Exists(dataFile))
            {
                dataFile = ExampleUtils.FindFile(Constants.LITE_HASH_DATA_FILE_NAME);
            }
            var evidenceFile = options.EvidenceFile ??
                ExampleUtils.FindFile(Constants.YAML_EVIDENCE_FILE_NAME);
            int threadCount = DEFAULT_THREAD_COUNT;

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

                // Every property the "device" element can produce, that actually
                // resolves against this data file (Lite exposes fewer than
                // Enterprise). Read by name via the indexer on each detection.
                var properties = DiscoverReadableProperties(pipeline, evidence[0]);

                Console.WriteLine($"Reading {properties.Count} scalar properties per detection " +
                    $"over {evidence.Count:N0} evidence records on {threadCount} threads.");
                Console.WriteLine($"Data: {Path.GetFileName(dataFile)}, profile: MaxPerformance.");
                Console.WriteLine();

                Console.WriteLine("Warming up...");
                Benchmark(pipeline, evidence, properties, threadCount);
                GC.Collect();
                Task.Delay(500).Wait();

                Console.WriteLine("Running detection throughput...");
                var results = Benchmark(pipeline, evidence, properties, threadCount);
                Report(results, properties.Count, threadCount);

                // Isolated property-read throughput: process one detection per
                // thread, then read every property in a tight loop. This removes
                // the CreateFlowData / Process / native-detection cost (which
                // dominates and adds noise) so the property-read path - the work
                // the #524 fixes actually target - is measured directly.
                Console.WriteLine();
                Console.WriteLine("Running isolated property-read throughput...");
                ReadThroughput(pipeline, evidence, properties, threadCount);
            }
        }

        private static void ReadThroughput(
            IPipeline pipeline,
            List<Dictionary<string, object>> evidence,
            List<string> properties,
            int threadCount)
        {
            const long readsPerThread = 5_000_000;
            var elapsed = new long[threadCount];
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
                        while (n < readsPerThread)
                        {
                            for (int i = 0; i < properties.Count && n < readsPerThread; i++)
                            {
                                var _ = device[properties[i]];
                                n++;
                            }
                        }
                        sw.Stop();
                        elapsed[idx] = sw.ElapsedMilliseconds;
                        reads[idx] = n;
                    }
                });
            }
            foreach (var th in threads) { th.Start(); }
            foreach (var th in threads) { th.Join(); }

            long totalReads = reads.Sum();
            long wallMs = elapsed.Max(); // threads run concurrently
            double readsPerSecond = totalReads / (wallMs / 1000.0);
            double nsPerRead = (double)wallMs * 1e6 / (totalReads / (double)threadCount);
            Console.WriteLine($"  Property reads/sec (all threads): {readsPerSecond:N0}");
            Console.WriteLine($"  ns per read (per thread):         {nsPerRead:N1}");
        }

        // Match-metric properties are computed in managed code (not via the native
        // value getters), so they don't exercise the property-read fast paths.
        private static readonly HashSet<string> MetricProperties =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "DeviceId", "Difference", "Drift", "Method",
                "Iterations", "MatchedNodes", "UserAgents"
            };

        // The fast paths (issue #524, fix #4) cover the scalar value types.
        private static readonly HashSet<Type> ScalarTypes =
            new HashSet<Type> { typeof(string), typeof(bool), typeof(int), typeof(double) };

        private static List<string> DiscoverReadableProperties(
            IPipeline pipeline, Dictionary<string, object> sampleEvidence)
        {
            // "device" is the DeviceDetectionHashEngine element data key. Keep only
            // the scalar, non-metric properties, so the benchmark measures the work
            // the #524 change actually targets rather than being diluted by the
            // managed-computed metrics and multi-value (list) properties.
            var candidates = pipeline.ElementAvailableProperties["device"].Values
                .Where(p => ScalarTypes.Contains(p.Type) && !MetricProperties.Contains(p.Name))
                .Select(p => p.Name)
                .ToList();

            var readable = new List<string>();
            using (var data = pipeline.CreateFlowData())
            {
                data.AddEvidence(sampleEvidence).Process();
                var device = data.Get<IDeviceData>();
                foreach (var name in candidates)
                {
                    try { var _ = device[name]; readable.Add(name); }
                    catch { /* not available in this data file */ }
                }
            }
            return readable;
        }

        private static List<BenchmarkResult> Benchmark(
            IPipeline pipeline,
            List<Dictionary<string, object>> evidence,
            List<string> properties,
            int threadCount)
        {
            var results = new List<BenchmarkResult>();
            Parallel.ForEach(evidence,
                new ParallelOptions { MaxDegreeOfParallelism = threadCount },
                () => new BenchmarkResult(),
                (values, loopState, result) =>
                {
                    result.Timer.Start();
                    using (var data = pipeline.CreateFlowData())
                    {
                        data.AddEvidence(values).Process();
                        var device = data.Get<IDeviceData>();
                        // Read every property - this is the work the #524
                        // optimisations target.
                        for (int i = 0; i < properties.Count; i++)
                        {
                            var value = device[properties[i]];
                        }
                    }
                    result.Timer.Stop();
                    result.Count++;
                    return result;
                },
                result => { lock (results) { results.Add(result); } });
            return results;
        }

        private static void Report(
            List<BenchmarkResult> results, int propertyCount, int threadCount)
        {
            long detections = results.Sum(r => r.Count);
            long milliseconds = results.Sum(r => r.Timer.ElapsedMilliseconds);
            double msPerDetection = (double)milliseconds / (detections * threadCount);
            double detectionsPerSecond = 1000.0 / msPerDetection;
            double propertyReadsPerSecond = detectionsPerSecond * propertyCount;

            Console.WriteLine();
            Console.WriteLine($"Overall: {detections:N0} detections, " +
                $"{propertyCount} properties each.");
            Console.WriteLine($"  Detections/sec:      {detectionsPerSecond:N0}");
            Console.WriteLine($"  Property reads/sec:  {propertyReadsPerSecond:N0}");
            Console.WriteLine($"  ms per detection:    {msPerDetection:N4}");
            Console.WriteLine($"  Concurrent threads:  {threadCount}");
        }
    }
}
