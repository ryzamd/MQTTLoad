using MQTTLoadTest.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MQTTLoadTest.SubscriberManager.Services;

public class SubscriberControlService
{
    private readonly IHighPerformanceSubscriber _subscriber;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly IDeviceManager _deviceManager;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly ILogger<SubscriberControlService> _logger;
    private readonly MqttConfiguration _config;

    public SubscriberControlService(
        IHighPerformanceSubscriber subscriber,
        ISubscriptionManager subscriptionManager,
        IDeviceManager deviceManager,
        IPerformanceMonitor performanceMonitor,
        IOptions<MqttConfiguration> config,
        ILogger<SubscriberControlService> logger)
    {
        _subscriber = subscriber;
        _subscriptionManager = subscriptionManager;
        _deviceManager = deviceManager;
        _performanceMonitor = performanceMonitor;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<bool> StartSubscriberAsync()
    {
        try
        {
            _logger.LogInformation("Starting subscriber...");

            if (!await _subscriber.ConnectAsync())
            {
                _logger.LogError("Failed to connect subscriber to broker");
                return false;
            }

            if (!await _subscriber.StartReceivingAsync())
            {
                _logger.LogError("Failed to start receiving messages");
                return false;
            }

            _logger.LogInformation("Subscriber started successfully");
            Console.WriteLine("Subscriber started and ready to receive messages.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start subscriber");
            Console.WriteLine($"Error starting subscriber: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> StopSubscriberAsync()
    {
        try
        {
            _logger.LogInformation("Stopping subscriber...");

            if (!await _subscriber.StopReceivingAsync())
            {
                _logger.LogWarning("Failed to stop receiving gracefully");
            }

            if (!await _subscriber.DisconnectAsync())
            {
                _logger.LogWarning("Failed to disconnect gracefully");
            }

            _logger.LogInformation("Subscriber stopped");
            Console.WriteLine("Subscriber stopped.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop subscriber");
            Console.WriteLine($"Error stopping subscriber: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SubscribeToDevicesAsync(List<string> deviceIds)
    {
        try
        {
            _logger.LogInformation($"Subscribing to {deviceIds.Count} devices...");
            var result = await _subscriptionManager.SubscribeToMultipleDevicesAsync(deviceIds);

            if (result)
            {
                _logger.LogInformation($"Successfully subscribed to devices: {string.Join(", ", deviceIds)}");
                Console.WriteLine($"Subscribed to {deviceIds.Count} devices successfully.");
            }
            else
            {
                _logger.LogWarning("Failed to subscribe to some or all devices");
                Console.WriteLine("Failed to subscribe to some or all devices.");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to devices");
            Console.WriteLine($"Error subscribing to devices: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UnsubscribeFromDevicesAsync(List<string> deviceIds)
    {
        try
        {
            _logger.LogInformation($"Unsubscribing from {deviceIds.Count} devices...");
            var result = await _subscriptionManager.UnsubscribeFromMultipleDevicesAsync(deviceIds);

            if (result)
            {
                _logger.LogInformation($"Successfully unsubscribed from devices: {string.Join(", ", deviceIds)}");
                Console.WriteLine($"Unsubscribed from {deviceIds.Count} devices successfully.");
            }
            else
            {
                _logger.LogWarning("Failed to unsubscribe from some or all devices");
                Console.WriteLine("Failed to unsubscribe from some or all devices.");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe from devices");
            Console.WriteLine($"Error unsubscribing from devices: {ex.Message}");
            return false;
        }
    }

    public async Task ShowStatusAsync()
    {
        try
        {
            var subscriptions = await _subscriptionManager.GetActiveSubscriptionsAsync();
            var unsubscribedDevices = await _subscriptionManager.GetUnsubscribedDevicesAsync();
            var allDeviceStats = await _subscriptionManager.GetAllDeviceStatisticsAsync();
            var metrics = await _subscriber.GetMetricsAsync();

            Console.WriteLine("\n=== Subscriber Status ===");
            Console.WriteLine($"Subscriber Connected: {_subscriber.IsConnected}");
            Console.WriteLine($"Subscriber Running: {_subscriber.IsRunning}");
            Console.WriteLine($"Queue Depth: {_subscriber.QueueDepth}");
            Console.WriteLine($"Active Subscriptions: {subscriptions.Count}");
            Console.WriteLine($"Available Devices: {unsubscribedDevices.Count}");

            Console.WriteLine("\n=== Performance Metrics ===");
            Console.WriteLine($"Messages Received: {metrics.MessagesReceived:N0}");
            Console.WriteLine($"Messages Per Second: {metrics.MessagesPerSecond:F2}");
            Console.WriteLine($"Average Latency: {metrics.AverageLatency:F2}ms");
            Console.WriteLine($"Memory Usage: {metrics.MemoryUsageMB:F2}MB");

            if (subscriptions.Any())
            {
                Console.WriteLine("\n=== Active Subscriptions ===");
                Console.WriteLine($"{"Device ID",-15} {"Topic",-30} {"Messages",-10} {"Last Message",-20}");
                Console.WriteLine(new string('-', 80));

                foreach (var sub in subscriptions.OrderBy(s => s.DeviceId))
                {
                    var stats = allDeviceStats.GetValueOrDefault(sub.DeviceId);
                    var messageCount = stats?.MessageCount ?? 0;
                    var lastMessage = stats?.LastMessageTime?.ToString("HH:mm:ss") ?? "Never";

                    Console.WriteLine($"{sub.DeviceId,-15} {sub.Topic,-30} {messageCount,-10:N0} {lastMessage,-20}");
                }
            }

            if (unsubscribedDevices.Any())
            {
                Console.WriteLine($"\n=== Available Devices ({unsubscribedDevices.Count}) ===");
                var deviceGroups = unsubscribedDevices.Select(d => d.DeviceId).OrderBy(id => id)
                    .Select((id, index) => new { id, index })
                    .GroupBy(x => x.index / 5)
                    .Select(g => string.Join(", ", g.Select(x => x.id)));

                foreach (var group in deviceGroups)
                {
                    Console.WriteLine($"  {group}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show status");
            Console.WriteLine("Error retrieving status information.");
        }
    }

    public async Task RunInteractiveAsync()
    {
        Console.WriteLine("=== MQTT Subscriber Manager - Interactive Mode ===");
        Console.WriteLine("Commands: start, stop, subscribe, unsubscribe, status, list, available, stats, exit");
        Console.WriteLine("For bulk operations, use comma-separated device IDs (e.g., DEV001,DEV002,DEV003)");
        Console.WriteLine("Type 'help' for detailed command information.\n");

        while (true)
        {
            Console.Write("Subscriber> ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
                continue;

            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToLower();

            try
            {
                switch (command)
                {
                    case "help":
                        ShowHelp();
                        break;

                    case "start":
                        await StartSubscriberAsync();
                        break;

                    case "stop":
                        await StopSubscriberAsync();
                        break;

                    case "subscribe":
                        if (parts.Length > 1)
                        {
                            var deviceIds = ParseDeviceIds(parts[1]);
                            await SubscribeToDevicesAsync(deviceIds);
                        }
                        else
                        {
                            await InteractiveSubscribeAsync();
                        }
                        break;

                    case "unsubscribe":
                        if (parts.Length > 1)
                        {
                            var deviceIds = ParseDeviceIds(parts[1]);
                            await UnsubscribeFromDevicesAsync(deviceIds);
                        }
                        else
                        {
                            await InteractiveUnsubscribeAsync();
                        }
                        break;

                    case "status":
                        await ShowStatusAsync();
                        break;

                    case "list":
                        await ListSubscriptionsAsync();
                        break;

                    case "available":
                        await ShowAvailableDevicesAsync();
                        break;

                    case "stats":
                        if (parts.Length > 1)
                        {
                            await ShowDeviceStatsAsync(parts[1]);
                        }
                        else
                        {
                            await ShowAllDeviceStatsAsync();
                        }
                        break;

                    case "subscribeall":
                        await SubscribeToAllAvailableAsync();
                        break;

                    case "unsubscribeall":
                        await UnsubscribeFromAllAsync();
                        break;

                    case "clear":
                        Console.Clear();
                        break;

                    case "exit":
                    case "quit":
                        Console.WriteLine("Goodbye!");
                        return;

                    default:
                        Console.WriteLine($"Unknown command: {command}. Type 'help' for available commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing command: {ex.Message}");
                _logger.LogError(ex, "Interactive command error: {Command}", command);
            }

            Console.WriteLine();
        }
    }

    private async Task InteractiveSubscribeAsync()
    {
        var availableDevices = await _subscriptionManager.GetUnsubscribedDevicesAsync();

        if (!availableDevices.Any())
        {
            Console.WriteLine("No available devices to subscribe to.");
            return;
        }

        Console.WriteLine($"Available devices ({availableDevices.Count}):");
        for (int i = 0; i < Math.Min(20, availableDevices.Count); i++)
        {
            Console.WriteLine($"  {availableDevices[i].DeviceId}");
        }

        if (availableDevices.Count > 20)
        {
            Console.WriteLine($"  ... and {availableDevices.Count - 20} more");
        }

        Console.Write("Enter device IDs to subscribe (comma-separated) or 'all' for all: ");
        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(input))
            return;

        if (input.ToLower() == "all")
        {
            var deviceIds = availableDevices.Select(d => d.DeviceId).ToList();
            await SubscribeToDevicesAsync(deviceIds);
        }
        else
        {
            var deviceIds = ParseDeviceIds(input);
            await SubscribeToDevicesAsync(deviceIds);
        }
    }

    private async Task InteractiveUnsubscribeAsync()
    {
        var subscriptions = await _subscriptionManager.GetActiveSubscriptionsAsync();

        if (!subscriptions.Any())
        {
            Console.WriteLine("No active subscriptions to unsubscribe from.");
            return;
        }

        Console.WriteLine($"Active subscriptions ({subscriptions.Count}):");
        foreach (var sub in subscriptions.Take(20))
        {
            Console.WriteLine($"  {sub.DeviceId}");
        }

        if (subscriptions.Count > 20)
        {
            Console.WriteLine($"  ... and {subscriptions.Count - 20} more");
        }

        Console.Write("Enter device IDs to unsubscribe (comma-separated) or 'all' for all: ");
        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(input))
            return;

        if (input.ToLower() == "all")
        {
            var deviceIds = subscriptions.Select(s => s.DeviceId).ToList();
            await UnsubscribeFromDevicesAsync(deviceIds);
        }
        else
        {
            var deviceIds = ParseDeviceIds(input);
            await UnsubscribeFromDevicesAsync(deviceIds);
        }
    }

    private async Task ListSubscriptionsAsync()
    {
        var subscriptions = await _subscriptionManager.GetActiveSubscriptionsAsync();

        if (!subscriptions.Any())
        {
            Console.WriteLine("No active subscriptions.");
            return;
        }

        Console.WriteLine($"\n=== Active Subscriptions ({subscriptions.Count}) ===");
        Console.WriteLine($"{"Device ID",-15} {"Topic",-30} {"Status",-10}");
        Console.WriteLine(new string('-', 60));

        foreach (var sub in subscriptions.OrderBy(s => s.DeviceId))
        {
            var status = sub.IsActive ? "Active" : "Inactive";
            Console.WriteLine($"{sub.DeviceId,-15} {sub.Topic,-30} {status,-10}");
        }
    }

    private async Task ShowAvailableDevicesAsync()
    {
        var availableDevices = await _subscriptionManager.GetUnsubscribedDevicesAsync();

        if (!availableDevices.Any())
        {
            Console.WriteLine("No available devices to subscribe to. All devices are already subscribed.");
            return;
        }

        Console.WriteLine($"\n=== Available Devices ({availableDevices.Count}) ===");
        Console.WriteLine("These devices are not currently subscribed to:");

        // Group devices for better display
        var deviceGroups = availableDevices.Select(d => d.DeviceId).OrderBy(id => id)
            .Select((id, index) => new { id, index })
            .GroupBy(x => x.index / 5)
            .Select(g => string.Join(", ", g.Select(x => x.id)));

        foreach (var group in deviceGroups)
        {
            Console.WriteLine($"  {group}");
        }

        Console.WriteLine($"\nTo subscribe to devices, use: subscribe <device_ids>");
        Console.WriteLine($"Example: subscribe {availableDevices.Take(3).Select(d => d.DeviceId).Aggregate((a, b) => $"{a},{b}")}");
    }

    private async Task ShowDeviceStatsAsync(string deviceId)
    {
        var stats = await _subscriptionManager.GetDeviceStatisticsAsync(deviceId);

        if (stats == null)
        {
            Console.WriteLine($"No statistics found for device: {deviceId}");
            return;
        }

        Console.WriteLine($"\n=== Device Statistics: {deviceId} ===");
        Console.WriteLine($"Message Count: {stats.MessageCount:N0}");
        Console.WriteLine($"First Message: {stats.FirstMessageTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never"}");
        Console.WriteLine($"Last Message: {stats.LastMessageTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never"}");
        Console.WriteLine($"Average Interval: {stats.AverageMessageInterval:F2} seconds");
        Console.WriteLine($"Total Data: {stats.TotalDataBytes:N0} bytes");
    }

    private async Task ShowAllDeviceStatsAsync()
    {
        var allStats = await _subscriptionManager.GetAllDeviceStatisticsAsync();

        if (!allStats.Any())
        {
            Console.WriteLine("No device statistics available.");
            return;
        }

        Console.WriteLine($"\n=== All Device Statistics ({allStats.Count}) ===");
        Console.WriteLine($"{"Device ID",-15} {"Messages",-10} {"Last Message",-20} {"Data (KB)",-12}");
        Console.WriteLine(new string('-', 70));

        foreach (var kvp in allStats.OrderBy(x => x.Key))
        {
            var stats = kvp.Value;
            var lastMessage = stats.LastMessageTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never";
            var dataKB = stats.TotalDataBytes / 1024.0;

            Console.WriteLine($"{kvp.Key,-15} {stats.MessageCount,-10:N0} {lastMessage,-20} {dataKB,-12:F2}");
        }
    }

    private async Task SubscribeToAllAvailableAsync()
    {
        var availableDevices = await _subscriptionManager.GetUnsubscribedDevicesAsync();

        if (!availableDevices.Any())
        {
            Console.WriteLine("No available devices to subscribe to.");
            return;
        }

        Console.Write($"Subscribe to all {availableDevices.Count} available devices? (y/N): ");
        var confirm = Console.ReadLine()?.Trim().ToLower();

        if (confirm == "y" || confirm == "yes")
        {
            var deviceIds = availableDevices.Select(d => d.DeviceId).ToList();
            await SubscribeToDevicesAsync(deviceIds);
        }
        else
        {
            Console.WriteLine("Operation cancelled.");
        }
    }

    private async Task UnsubscribeFromAllAsync()
    {
        var subscriptions = await _subscriptionManager.GetActiveSubscriptionsAsync();

        if (!subscriptions.Any())
        {
            Console.WriteLine("No active subscriptions to unsubscribe from.");
            return;
        }

        Console.Write($"Unsubscribe from all {subscriptions.Count} active subscriptions? (y/N): ");
        var confirm = Console.ReadLine()?.Trim().ToLower();

        if (confirm == "y" || confirm == "yes")
        {
            var deviceIds = subscriptions.Select(s => s.DeviceId).ToList();
            await UnsubscribeFromDevicesAsync(deviceIds);
        }
        else
        {
            Console.WriteLine("Operation cancelled.");
        }
    }

    private List<string> ParseDeviceIds(string input)
    {
        return input.Split(',', StringSplitOptions.RemoveEmptyEntries)
                   .Select(id => id.Trim().ToUpper())
                   .Where(id => !string.IsNullOrEmpty(id))
                   .Distinct()
                   .ToList();
    }

    private void ShowHelp()
    {
        Console.WriteLine(@"
        === Available Commands ===

        Subscriber Management:
          start                  - Start subscriber and connect to broker
          stop                   - Stop subscriber and disconnect

        Subscription Management:
          subscribe [device_ids] - Subscribe to specific devices or show interactive menu
          unsubscribe [device_ids] - Unsubscribe from devices or show interactive menu
          subscribeall           - Subscribe to all available devices (with confirmation)
          unsubscribeall         - Unsubscribe from all active subscriptions (with confirmation)

        Information & Status:
          status                 - Show detailed subscriber status and performance
          list                   - List all active subscriptions
          available              - Show devices that are not currently subscribed
          stats [device_id]      - Show statistics for specific device or all devices

        Utility:
          help                   - Show this help message
          clear                  - Clear screen
          exit/quit              - Exit interactive mode

        Examples:
          subscribe DEV001,DEV002,DEV003  - Subscribe to multiple devices
          unsubscribe DEV001              - Unsubscribe from single device
          stats DEV001                    - Show statistics for DEV001
          stats                           - Show statistics for all devices
        ");
    }
}