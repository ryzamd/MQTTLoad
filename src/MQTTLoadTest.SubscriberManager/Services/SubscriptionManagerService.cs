using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;
using MQTTnet.Protocol;
using MQTTLoadTest.Core.Models;

namespace MQTTLoadTest.SubscriberManager.Services;

public class SubscriptionManagerService : ISubscriptionManager
{
    private readonly IHighPerformanceSubscriber _subscriber;
    private readonly IDeviceManager _deviceManager;
    private readonly ILogger<SubscriptionManagerService> _logger;
    private readonly MqttConfiguration _config;
    private readonly string _subscriptionStateFile;

    private readonly ConcurrentDictionary<string, SubscriptionInfo> _activeSubscriptions = new();
    private readonly ConcurrentDictionary<string, DeviceStatistics> _deviceStatistics = new();
    private readonly object _stateLock = new();

    public SubscriptionManagerService(
        IHighPerformanceSubscriber subscriber,
        IDeviceManager deviceManager,
        IOptions<MqttConfiguration> config,
        ILogger<SubscriptionManagerService> logger)
    {
        _subscriber = subscriber;
        _deviceManager = deviceManager;
        _config = config.Value;
        _logger = logger;
        _subscriptionStateFile = Path.Combine("config", "subscription-states.json");

        // Subscribe to subscriber events
        _subscriber.OnMessageReceived += OnMessageReceived;

        // Ensure config directory exists
        Directory.CreateDirectory("config");

        // Load existing subscription states
        _ = Task.Run(LoadSubscriptionStatesAsync);
    }

    public async Task<bool> SubscribeToDeviceAsync(string deviceId, string topic)
    {
        try
        {
            if (_activeSubscriptions.ContainsKey(deviceId))
            {
                _logger.LogWarning($"Already subscribed to device: {deviceId}");
                return false;
            }

            if (!await _subscriber.SubscribeAsync(topic))
            {
                _logger.LogError($"Failed to subscribe to topic: {topic}");
                return false;
            }

            var subscription = new SubscriptionInfo
            {
                DeviceId = deviceId,
                Topic = topic,
                IsActive = true,
                SubscribedAt = DateTime.UtcNow,
                QoSLevel = MqttQualityOfServiceLevel.AtLeastOnce
            };

            _activeSubscriptions[deviceId] = subscription;

            // Initialize device statistics
            _deviceStatistics.TryAdd(deviceId, new DeviceStatistics
            {
                DeviceId = deviceId
            });

            await SaveSubscriptionStatesAsync();
            _logger.LogInformation($"Successfully subscribed to device: {deviceId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to subscribe to device: {deviceId}");
            return false;
        }
    }

    public async Task<bool> UnsubscribeFromDeviceAsync(string deviceId)
    {
        try
        {
            if (!_activeSubscriptions.TryGetValue(deviceId, out var subscription))
            {
                _logger.LogWarning($"Not subscribed to device: {deviceId}");
                return false;
            }

            if (!await _subscriber.UnsubscribeAsync(subscription.Topic))
            {
                _logger.LogError($"Failed to unsubscribe from topic: {subscription.Topic}");
                return false;
            }

            _activeSubscriptions.TryRemove(deviceId, out _);
            await SaveSubscriptionStatesAsync();

            _logger.LogInformation($"Successfully unsubscribed from device: {deviceId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to unsubscribe from device: {deviceId}");
            return false;
        }
    }

    public async Task<List<SubscriptionInfo>> GetActiveSubscriptionsAsync()
    {
        return await Task.FromResult(_activeSubscriptions.Values.ToList());
    }

    public async Task<List<DeviceConfig>> GetAvailableDevicesAsync()
    {
        try
        {
            return await _deviceManager.LoadDevicesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available devices");
            return new List<DeviceConfig>();
        }
    }

    public async Task<List<DeviceConfig>> GetUnsubscribedDevicesAsync()
    {
        try
        {
            var allDevices = await _deviceManager.LoadDevicesAsync();
            var subscribedDeviceIds = _activeSubscriptions.Keys.ToHashSet();

            return allDevices.Where(d => !subscribedDeviceIds.Contains(d.DeviceId)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get unsubscribed devices");
            return new List<DeviceConfig>();
        }
    }

    public async Task<bool> IsSubscribedToDeviceAsync(string deviceId)
    {
        return await Task.FromResult(_activeSubscriptions.ContainsKey(deviceId));
    }

    public async Task<DeviceStatistics?> GetDeviceStatisticsAsync(string deviceId)
    {
        return await Task.FromResult(_deviceStatistics.GetValueOrDefault(deviceId));
    }

    public async Task<Dictionary<string, DeviceStatistics>> GetAllDeviceStatisticsAsync()
    {
        return await Task.FromResult(_deviceStatistics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
    }

    public async Task<bool> SubscribeToMultipleDevicesAsync(List<string> deviceIds)
    {
        var successCount = 0;
        var tasks = new List<Task<bool>>();

        foreach (var deviceId in deviceIds)
        {
            var topic = $"{_config.BaseTopic}/{deviceId}";
            tasks.Add(SubscribeToDeviceAsync(deviceId, topic));
        }

        var results = await Task.WhenAll(tasks);
        successCount = results.Count(r => r);

        _logger.LogInformation($"Subscribed to {successCount}/{deviceIds.Count} devices");
        return successCount > 0;
    }

    public async Task<bool> UnsubscribeFromMultipleDevicesAsync(List<string> deviceIds)
    {
        var successCount = 0;
        var tasks = deviceIds.Select(UnsubscribeFromDeviceAsync);
        var results = await Task.WhenAll(tasks);

        successCount = results.Count(r => r);
        _logger.LogInformation($"Unsubscribed from {successCount}/{deviceIds.Count} devices");
        return successCount > 0;
    }

    private void OnMessageReceived(object? sender, MessageData messageData)
    {
        try
        {
            // Update subscription info
            if (_activeSubscriptions.TryGetValue(messageData.DeviceId, out var subscription))
            {
                subscription.LastMessageReceived = messageData.Timestamp;
                subscription.MessageCount++;
            }

            // Update device statistics
            if (_deviceStatistics.TryGetValue(messageData.DeviceId, out var stats))
            {
                UpdateDeviceStatistics(stats, messageData);
            }
            else
            {
                // Create new statistics for unknown device
                var newStats = new DeviceStatistics
                {
                    DeviceId = messageData.DeviceId,
                    FirstMessageTime = messageData.Timestamp,
                    LastMessageTime = messageData.Timestamp,
                    MessageCount = 1,
                    TotalDataBytes = messageData.PayloadSize
                };

                _deviceStatistics[messageData.DeviceId] = newStats;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing received message from device: {DeviceId}", messageData.DeviceId);
        }
    }

    private void UpdateDeviceStatistics(DeviceStatistics stats, MessageData messageData)
    {
        lock (_stateLock)
        {
            stats.MessageCount++;
            stats.TotalDataBytes += messageData.PayloadSize;
            stats.LastMessageTime = messageData.Timestamp;

            if (stats.FirstMessageTime == null)
            {
                stats.FirstMessageTime = messageData.Timestamp;
            }

            // Calculate average message interval
            if (stats.FirstMessageTime.HasValue && stats.MessageCount > 1)
            {
                var totalTimeSpan = messageData.Timestamp - stats.FirstMessageTime.Value;
                stats.AverageMessageInterval = totalTimeSpan.TotalSeconds / (stats.MessageCount - 1);
            }

            // Calculate messages per second (last 60 seconds)
            var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
            if (stats.LastMessageTime.HasValue && stats.LastMessageTime > oneMinuteAgo)
            {
                var recentTimeSpan = DateTime.UtcNow - oneMinuteAgo;
                // This is a simplified calculation - in production you'd want a more sophisticated sliding window
                stats.MessagesPerSecond = stats.MessageCount / Math.Max(1, recentTimeSpan.TotalSeconds);
            }

            // Update QoS distribution
            if (!stats.QoSDistribution.ContainsKey(messageData.QoS))
            {
                stats.QoSDistribution[messageData.QoS] = 0;
            }
            stats.QoSDistribution[messageData.QoS]++;
        }
    }

    private async Task SaveSubscriptionStatesAsync()
    {
        try
        {
            var stateData = new
            {
                Subscriptions = _activeSubscriptions.Values.ToList(),
                Statistics = _deviceStatistics.Values.ToList(),
                LastSaved = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(stateData, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_subscriptionStateFile, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save subscription states");
        }
    }

    private async Task LoadSubscriptionStatesAsync()
    {
        try
        {
            if (!File.Exists(_subscriptionStateFile))
            {
                _logger.LogInformation("No existing subscription state file found");
                return;
            }

            var json = await File.ReadAllTextAsync(_subscriptionStateFile);
            var stateData = JsonSerializer.Deserialize<dynamic>(json);

            // In a production system, you'd want to restore subscriptions
            // For now, just log that we found existing state
            _logger.LogInformation("Loaded existing subscription states");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load subscription states");
        }
    }
}