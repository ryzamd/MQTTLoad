using MQTTLoadTest.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace MQTTLoadTest.Core.Services;

public class PerformanceMonitor : IPerformanceMonitor, IDisposable
{
    private readonly ILogger<PerformanceMonitor> _logger;
    private readonly ConcurrentDictionary<string, long> _counters = new();
    private readonly ConcurrentDictionary<string, double> _gauges = new();
    private readonly ConcurrentQueue<double> _latencyMeasurements = new();
    private readonly ConcurrentQueue<PerformanceMetrics> _metricsHistory = new();
    private readonly Timer _metricsTimer;
    private readonly PerformanceCounter? _cpuCounter;
    private readonly object _metricsLock = new();

    public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
    {
        _logger = logger;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue(); // Initialize
            }

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not initialize CPU performance counter");
        }

        _metricsTimer = new Timer(UpdateMetrics, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void IncrementCounter(string name, long value = 1)
    {
        _counters.AddOrUpdate(name, value, (key, oldValue) => oldValue + value);
    }

    public void SetGauge(string name, double value)
    {
        _gauges.AddOrUpdate(name, value, (key, oldValue) => value);
    }

    public void RecordLatency(double latencyMs)
    {
        _latencyMeasurements.Enqueue(latencyMs);

        // Keep only recent measurements
        while (_latencyMeasurements.Count > 10000)
        {
            _latencyMeasurements.TryDequeue(out _);
        }
    }

    public PerformanceMetrics GetCurrentMetrics()
    {
        lock (_metricsLock)
        {
            var metrics = new PerformanceMetrics
            {
                Timestamp = DateTime.UtcNow,
                TotalMessagesPublished = _counters.GetValueOrDefault("messages_published", 0),
                TotalMessagesReceived = _counters.GetValueOrDefault("messages_received", 0),
                PublishRate = _gauges.GetValueOrDefault("publish_rate", 0),
                ReceiveRate = _gauges.GetValueOrDefault("receive_rate", 0),
                AverageLatency = CalculateAverageLatency(),
                MessageLossRate = CalculateMessageLossRate(),
                MemoryUsage = GC.GetTotalMemory(false),
                CpuUsage = GetCpuUsage(),
                QueueDepth = (int)_gauges.GetValueOrDefault("queue_depth", 0),
                ActivePublishers = (int)_gauges.GetValueOrDefault("active_publishers", 0),
                ActiveSubscribers = (int)_gauges.GetValueOrDefault("active_subscribers", 0)
            };

            foreach (var gauge in _gauges)
            {
                metrics.CustomMetrics[gauge.Key] = gauge.Value;
            }

            return metrics;
        }
    }

    public List<PerformanceMetrics> GetMetricsHistory(int count = 100)
    {
        return _metricsHistory.TakeLast(count).ToList();
    }

    public async Task ExportMetricsAsync(string filePath)
    {
        try
        {
            var metrics = GetMetricsHistory();
            var json = JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation("Exported {Count} metrics to {FilePath}", metrics.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export metrics to {FilePath}", filePath);
            throw;
        }
    }

    public void Reset()
    {
        _counters.Clear();
        _gauges.Clear();
        while (_latencyMeasurements.TryDequeue(out _)) { }
        while (_metricsHistory.TryDequeue(out _)) { }
        _logger.LogInformation("Performance metrics reset");
    }

    private void UpdateMetrics(object? state)
    {
        var metrics = GetCurrentMetrics();
        _metricsHistory.Enqueue(metrics);

        // Keep only recent history
        while (_metricsHistory.Count > 1000)
        {
            _metricsHistory.TryDequeue(out _);
        }
    }

    private double CalculateAverageLatency()
    {
        var measurements = _latencyMeasurements.ToArray();
        return measurements.Length > 0 ? measurements.Average() : 0;
    }

    private double CalculateMessageLossRate()
    {
        var published = _counters.GetValueOrDefault("messages_published", 0);
        var received = _counters.GetValueOrDefault("messages_received", 0);

        if (published == 0) return 0;

        var loss = Math.Max(0, published - received);
        return (double)loss / published * 100;
    }

    private double GetCpuUsage()
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return _cpuCounter?.NextValue() ?? 0;
            else
                return 0;
        }
        catch
        {
            return 0;
        }
    }


    public void Dispose()
    {
        _metricsTimer?.Dispose();
        _cpuCounter?.Dispose();
    }
}