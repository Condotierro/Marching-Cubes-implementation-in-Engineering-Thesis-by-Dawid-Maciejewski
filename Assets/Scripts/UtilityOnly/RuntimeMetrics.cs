using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class RuntimeMetrics
{
    // ---------- DATA STRUCTURES ----------

    private class Metric
    {
        public readonly List<double> samples = new List<double>(1024);

        public void Add(double value)
        {
            samples.Add(value);
        }
    }

    private static readonly Dictionary<string, Metric> metrics =
        new Dictionary<string, Metric>(32);

    private static readonly object fileLock = new object();

    // ---------- PUBLIC API ----------

    /// <summary>
    /// Record a numeric measurement (time, bytes, count, etc.)
    /// </summary>
    public static void Record(string name, double value)
    {
        if (!metrics.TryGetValue(name, out var metric))
        {
            metric = new Metric();
            metrics[name] = metric;
        }

        metric.Add(value);
    }

    /// <summary>
    /// Dump all metrics to a text file in the game folder
    /// </summary>
    public static void SaveToFile()
    {
        lock (fileLock)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string path = Path.Combine(
                Application.persistentDataPath,
                $"metrics_{timestamp}.txt"
            );

            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine("=== Runtime Metrics Dump ===");
                writer.WriteLine($"Timestamp: {DateTime.Now}");
                writer.WriteLine();

                foreach (var kvp in metrics)
                {
                    WriteMetric(writer, kvp.Key, kvp.Value.samples);
                }
            }

            Debug.Log($"[RuntimeMetrics] Saved metrics to:\n{path}");
        }
    }

    // ---------- INTERNAL HELPERS ----------

    private static void WriteMetric(
        StreamWriter writer,
        string name,
        List<double> values)
    {
        if (values.Count == 0) return;

        values.Sort(); // safe here — offline operation

        int n = values.Count;
        double sum = values.Sum();
        double mean = sum / n;
        double min = values[0];
        double max = values[n - 1];

        double variance = 0.0;
        for (int i = 0; i < n; i++)
        {
            double diff = values[i] - mean;
            variance += diff * diff;
        }
        variance /= n;
        double stdDev = Math.Sqrt(variance);

        double p95 = values[(int)(0.95 * (n - 1))];
        double p99 = values[(int)(0.99 * (n - 1))];

        writer.WriteLine($"Metric: {name}");
        writer.WriteLine($"Samples: {n}");
        writer.WriteLine($"Mean: {mean:F4}");
        writer.WriteLine($"Min: {min:F4}");
        writer.WriteLine($"Max: {max:F4}");
        writer.WriteLine($"StdDev: {stdDev:F4}");
        writer.WriteLine($"P95: {p95:F4}");
        writer.WriteLine($"P99: {p99:F4}");
        writer.WriteLine();
    }
}
