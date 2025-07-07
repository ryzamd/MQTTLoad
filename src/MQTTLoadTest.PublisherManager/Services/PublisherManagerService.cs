using MQTTLoadTest.Core.Models;
using MQTTLoadTest.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;

namespace MQTTLoadTest.PublisherManager.Services;

public class PublisherManagerService : IPublisherManager, IDisposable
{
    private readonly MqttConfiguration _config;
    private readonly IDeviceManager _deviceManager;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly ILogger<PublisherManagerService> _logger;
    private readonly ILoggerFactory _loggerFactory; // ADD THIS
    private readonly ConcurrentDictionary<string, IHighPerformancePublisher> _publishers = new();
    private readonly ConcurrentDictionary<string, PublisherState> _publisherStates = new();
    private readonly Timer _metricsTimer;
    private bool _disposed = false;

    public PublisherManagerService(
        IOptions<MqttConfiguration> config,
        IDeviceManager deviceManager,
        IPerformanceMonitor performanceMonitor,
        ILoggerFactory loggerFactory, // ADD THIS
        ILogger<PublisherManagerService> logger)
    {
        _config = config.Value;
        _deviceManager = deviceManager;
        _performanceMonitor = performanceMonitor;
        _loggerFactory = loggerFactory; // ADD THIS
        _logger = logger;

        _metricsTimer = new Timer(UpdateMetrics, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public async Task<bool> AddPublisherAsync(DeviceConfig device)
    {
        try
        {
            // FIX: Create proper logger for HighPerformancePublisher
            var publisherLogger = _loggerFactory.CreateLogger<HighPerformancePublisher>();

            var publisher = new HighPerformancePublisher(
                device,
                Options.Create(_config),
                publisherLogger, // Use proper logger
                _performanceMonitor);

            if (_publishers.TryAdd(device.DeviceId, publisher))
            {
                var state = new PublisherState
                {
                    PublisherId = $"pub_{device.DeviceId}",
                    DeviceId = device.DeviceId,
                    IsEnabled = device.IsEnabled,
                    CreatedAt = DateTime.UtcNow
                };

                _publisherStates.TryAdd(device.DeviceId, state);
                await SavePublisherStatesAsync();

                _logger.LogInformation("Publisher {DeviceId} added", device.DeviceId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add publisher {DeviceId}", device.DeviceId);
            return false;
        }
    }

    // ... rest of the methods remain the same ...

    public async Task<bool> StartPublisherAsync(string deviceId)
    {
        if (!_publishers.TryGetValue(deviceId, out var publisher))
        {
            _logger.LogError("Publisher not found: {DeviceId}", deviceId);
            return false;
        }

        try
        {
            var success = await publisher.StartPublishingAsync();
            if (success)
            {
                _performanceMonitor.IncrementCounter("publishers_started");
                _logger.LogInformation("Publisher {DeviceId} started", deviceId);
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start publisher {DeviceId}", deviceId);
            return false;
        }
    }

    public async Task<bool> StopPublisherAsync(string deviceId)
    {
        if (!_publishers.TryGetValue(deviceId, out var publisher))
        {
            _logger.LogError("Publisher not found: {DeviceId}", deviceId);
            return false;
        }

        try
        {
            var success = await publisher.StopPublishingAsync();
            if (success)
            {
                _performanceMonitor.IncrementCounter("publishers_stopped");
                _logger.LogInformation("Publisher {DeviceId} stopped", deviceId);
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop publisher {DeviceId}", deviceId);
            return false;
        }
    }

    public async Task<bool> EnablePublisherAsync(string deviceId)
    {
        if (!_publishers.TryGetValue(deviceId, out var publisher))
        {
            _logger.LogError("Publisher not found: {DeviceId}", deviceId);
            return false;
        }

        try
        {
            // Enable logic here
            _logger.LogInformation("Publisher {DeviceId} enabled", deviceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable publisher {DeviceId}", deviceId);
            return false;
        }
    }

    public async Task<bool> DisablePublisherAsync(string deviceId)
    {
        if (!_publishers.TryGetValue(deviceId, out var publisher))
        {
            _logger.LogError("Publisher not found: {DeviceId}", deviceId);
            return false;
        }

        try
        {
            // Disable logic here
            _logger.LogInformation("Publisher {DeviceId} disabled", deviceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable publisher {DeviceId}", deviceId);
            return false;
        }
    }

    public async Task<bool> RemovePublisherAsync(string deviceId)
    {
        if (!_publishers.TryRemove(deviceId, out var publisher))
        {
            _logger.LogError("Publisher not found: {DeviceId}", deviceId);
            return false;
        }

        try
        {
            await publisher.StopPublishingAsync();
            publisher.Dispose();
            _publisherStates.TryRemove(deviceId, out _);
            _logger.LogInformation("Publisher {DeviceId} removed", deviceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove publisher {DeviceId}", deviceId);
            return false;
        }
    }

    public async Task<bool> RemovePublishersAsync(List<string> deviceIds)
    {
        var tasks = deviceIds.Select(RemovePublisherAsync);
        var results = await Task.WhenAll(tasks);
        return results.All(r => r);
    }

    public async Task<List<PublisherState>> GetPublisherStatesAsync()
    {
        return await Task.Run(() =>
        {
            var states = new List<PublisherState>();
            foreach (var kvp in _publishers)
            {
                var publisher = kvp.Value;
                var state = publisher.State;
                states.Add(state);
            }
            return states;
        }).ConfigureAwait(false);
    }

    public async Task<PublisherState?> GetPublisherStateAsync(string deviceId)
    {
        return await Task.Run(() =>
        {
            if (_publishers.TryGetValue(deviceId, out var publisher))
            {
                return publisher.State;
            }
            return null;
        }).ConfigureAwait(false);
    }

    public async Task SavePublisherStatesAsync()
    {
        try
        {
            var states = await GetPublisherStatesAsync();
            var json = JsonSerializer.Serialize(states, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_config.PublisherStateFile, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save publisher states");
        }
    }

    public async Task LoadPublisherStatesAsync()
    {
        try
        {
            if (!File.Exists(_config.PublisherStateFile))
                return;

            var json = await File.ReadAllTextAsync(_config.PublisherStateFile);
            var states = JsonSerializer.Deserialize<List<PublisherState>>(json);

            if (states != null)
            {
                foreach (var state in states)
                {
                    _publisherStates.TryAdd(state.DeviceId, state);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load publisher states");
        }
    }

    public async Task<bool> RestartPublisherAsync(string deviceId)
    {
        await StopPublisherAsync(deviceId);
        await Task.Delay(1000); // Brief pause
        return await StartPublisherAsync(deviceId);
    }

    public async Task<int> GetActivePublisherCountAsync()
    {
        var states = await GetPublisherStatesAsync();
        return states.Count(s => s.IsRunning);
    }

    public async Task<PerformanceMetrics> GetPerformanceMetricsAsync()
    {
        var metrics = _performanceMonitor.GetCurrentMetrics();
        metrics.ActivePublishers = await GetActivePublisherCountAsync();
        return metrics;
    }

    private void UpdateMetrics(object? state)
    {
        try
        {
            var activeCount = _publishers.Count(p => p.Value.IsConnected);
            _performanceMonitor.SetGauge("active_publishers", activeCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update metrics");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _metricsTimer?.Dispose();

        foreach (var publisher in _publishers.Values)
        {
            try
            {
                publisher.StopPublishingAsync().Wait(5000);
                publisher.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing publisher");
            }
        }

        _publishers.Clear();
        _disposed = true;
    }
}