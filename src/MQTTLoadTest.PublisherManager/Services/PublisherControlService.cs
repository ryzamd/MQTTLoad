using MQTTLoadTest.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Collections.Concurrent;

namespace MQTTLoadTest.PublisherManager.Services;

public class PublisherControlService
{
    private readonly IPublisherManager _publisherManager;
    private readonly IDeviceManager _deviceManager;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly ILogger<PublisherControlService> _logger;
    private readonly MqttConfiguration _config;
    private readonly string _stateFilePath;

    public PublisherControlService(
        IPublisherManager publisherManager,
        IDeviceManager deviceManager,
        IPerformanceMonitor performanceMonitor,
        IOptions<MqttConfiguration> config,
        ILogger<PublisherControlService> logger)
    {
        _publisherManager = publisherManager;
        _deviceManager = deviceManager;
        _performanceMonitor = performanceMonitor;
        _config = config.Value;
        _logger = logger;
        _stateFilePath = Path.Combine("D:/CSharp/MQTTLoadTest/", "publisher-states.json");
    }

    public async Task<bool> StartAllPublishersAsync()
    {
        try
        {
            _logger.LogInformation("Starting all publishers...");

            // Step 1: Load devices from file
            var devices = await _deviceManager.LoadDevicesAsync();
            _logger.LogInformation($"Loaded {devices.Count} devices from file");

            // Step 2: Check existing publishers
            var existingStates = await _publisherManager.GetPublisherStatesAsync();
            var existingDeviceIds = existingStates.Select(s => s.DeviceId).ToHashSet();

            // Step 3: Auto-add missing publishers
            var missingDevices = devices.Where(d => !existingDeviceIds.Contains(d.DeviceId)).ToList();
            if (missingDevices.Any())
            {
                _logger.LogInformation($"Auto-initializing {missingDevices.Count} missing publishers...");

                foreach (var device in missingDevices)
                {
                    try
                    {
                        var success = await _publisherManager.AddPublisherAsync(device);
                        if (success)
                        {
                            _logger.LogDebug($"Added publisher for device: {device.DeviceId}");
                        }
                        else
                        {
                            _logger.LogWarning($"Failed to add publisher for device: {device.DeviceId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error adding publisher for device: {device.DeviceId}");
                    }
                }

                await _publisherManager.SavePublisherStatesAsync();
                _logger.LogInformation($"Successfully initialized {missingDevices.Count} publishers");
            }

            // Step 4: Start all publishers
            var tasks = devices.Select(device => _publisherManager.StartPublisherAsync(device.DeviceId));
            var results = await Task.WhenAll(tasks);

            var successCount = results.Count(r => r);
            _logger.LogInformation($"Started {successCount}/{devices.Count} publishers");

            await _publisherManager.SavePublisherStatesAsync();
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start all publishers");
            return false;
        }
    }

    public async Task<bool> StopAllPublishersAsync()
    {
        try
        {
            _logger.LogInformation("Stopping all publishers...");
            var states = await _publisherManager.GetPublisherStatesAsync();
            var tasks = states.Select(state => _publisherManager.StopPublisherAsync(state.DeviceId));
            var results = await Task.WhenAll(tasks);

            var successCount = results.Count(r => r);
            _logger.LogInformation($"Stopped {successCount}/{states.Count} publishers");

            await _publisherManager.SavePublisherStatesAsync();
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop all publishers");
            return false;
        }
    }

    public async Task<bool> AddPublishersAsync(int count, string? deviceId = null, string? deviceName = null)
    {
        try
        {
            _logger.LogInformation($"Adding {count} new publishers...");
            var deviceIds = new List<string>();

            if (!string.IsNullOrEmpty(deviceId))
            {
                // Add specific device
                if (!_deviceManager.ValidateDeviceId(deviceId))
                {
                    _logger.LogWarning($"Invalid device ID format: {deviceId}");
                    return false;
                }
                deviceIds.Add(deviceId);
            }
            else
            {
                // Generate new device IDs
                for (int i = 0; i < count; i++)
                {
                    var device = await _deviceManager.GenerateDeviceAsync(i);
                    deviceIds.Add(device.DeviceId);
                }
            }

            return await AddPublishersAsync(deviceIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add publishers");
            return false;
        }
    }

    public async Task<bool> AddPublishersAsync(List<string> deviceIds)
    {
        try
        {
            _logger.LogInformation($"Adding {deviceIds.Count} new publishers...");
            var successCount = 0;

            foreach (var deviceId in deviceIds)
            {
                if (!_deviceManager.ValidateDeviceId(deviceId))
                {
                    _logger.LogWarning($"Invalid device ID format: {deviceId}");
                    continue;
                }

                var device = _deviceManager.CreateDevice(
                    deviceId,
                    $"Device_{deviceId}",
                    $"{_config.BaseTopic}/{deviceId}"
                );

                if (await _publisherManager.AddPublisherAsync(device))
                {
                    successCount++;
                    _logger.LogInformation($"Added publisher for device: {deviceId}");
                }
                else
                {
                    _logger.LogWarning($"Failed to add publisher for device: {deviceId}");
                }
            }

            await _publisherManager.SavePublisherStatesAsync();
            _logger.LogInformation($"Successfully added {successCount}/{deviceIds.Count} publishers");
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add publishers");
            return false;
        }
    }

    public async Task<bool> RemovePublishersAsync(List<string> deviceIds)
    {
        try
        {
            _logger.LogInformation($"Removing {deviceIds.Count} publishers...");
            var result = await _publisherManager.RemovePublishersAsync(deviceIds);

            if (result)
            {
                await _publisherManager.SavePublisherStatesAsync();
                _logger.LogInformation($"Successfully removed publishers for devices: {string.Join(", ", deviceIds)}");
            }
            else
            {
                _logger.LogWarning("Failed to remove some or all publishers");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove publishers");
            return false;
        }
    }

    public async Task<bool> EnablePublishersAsync(List<string> deviceIds)
    {
        try
        {
            _logger.LogInformation($"Enabling {deviceIds.Count} publishers...");
            var tasks = deviceIds.Select(deviceId => _publisherManager.EnablePublisherAsync(deviceId));
            var results = await Task.WhenAll(tasks);

            var successCount = results.Count(r => r);
            await _publisherManager.SavePublisherStatesAsync();

            _logger.LogInformation($"Enabled {successCount}/{deviceIds.Count} publishers");
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable publishers");
            return false;
        }
    }

    public async Task<bool> DisablePublishersAsync(List<string> deviceIds)
    {
        try
        {
            _logger.LogInformation($"Disabling {deviceIds.Count} publishers...");
            var tasks = deviceIds.Select(deviceId => _publisherManager.DisablePublisherAsync(deviceId));
            var results = await Task.WhenAll(tasks);

            var successCount = results.Count(r => r);
            await _publisherManager.SavePublisherStatesAsync();

            _logger.LogInformation($"Disabled {successCount}/{deviceIds.Count} publishers");
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable publishers");
            return false;
        }
    }

    public async Task ShowStatusAsync(bool detail = false)
    {
        try
        {
            var states = await _publisherManager.GetPublisherStatesAsync();
            var metrics = await _publisherManager.GetPerformanceMetricsAsync();
            var activeCount = await _publisherManager.GetActivePublisherCountAsync();

            Console.WriteLine("\n=== Publisher Manager Status ===");
            Console.WriteLine($"Total Publishers: {states.Count}");
            Console.WriteLine($"Active Publishers: {activeCount}");
            Console.WriteLine($"Disabled Publishers: {states.Count(s => !s.IsEnabled)}");
            Console.WriteLine($"Connected Publishers: {states.Count(s => s.IsConnected)}");
            Console.WriteLine($"Publishing Publishers: {states.Count(s => s.IsPublishing)}");

            Console.WriteLine("\n=== Performance Metrics ===");
            Console.WriteLine($"Messages Published: {metrics.MessagesPublished:N0}");
            Console.WriteLine($"Messages Per Second: {metrics.MessagesPerSecond:F2}");
            Console.WriteLine($"Average Latency: {metrics.AverageLatency:F2}ms");
            Console.WriteLine($"Error Rate: {metrics.ErrorRate:F2}%");
            Console.WriteLine($"Memory Usage: {metrics.MemoryUsageMB:F2}MB");

            if (detail && states.Any())
            {
                Console.WriteLine("\n=== Publisher Details ===");
                Console.WriteLine($"{"Device ID",-15} {"Status",-12} {"Connected",-10} {"Publishing",-10} {"Messages",-10}");
                Console.WriteLine(new string('-', 70));

                foreach (var state in states.OrderBy(s => s.DeviceId))
                {
                    var status = state.IsEnabled ? "Enabled" : "Disabled";
                    var connected = state.IsConnected ? "Yes" : "No";
                    var publishing = state.IsPublishing ? "Yes" : "No";

                    Console.WriteLine($"{state.DeviceId,-15} {status,-12} {connected,-10} {publishing,-10} {state.MessageCount,-10:N0}");
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
        Console.WriteLine("=== MQTT Publisher Manager - Interactive Mode ===");
        Console.WriteLine("Commands: start, stop, add, remove, enable, disable, status, restart, exit");
        Console.WriteLine("For bulk operations, use comma-separated device IDs (e.g., DEV001,DEV002,DEV003)");
        Console.WriteLine("Type 'help' for detailed command information.\n");

        while (true)
        {
            Console.Write("Publisher> ");
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
                        if (parts.Length > 1)
                        {
                            var deviceIds = ParseDeviceIds(parts[1]);
                            await StartPublishersAsync(deviceIds);
                        }
                        else
                        {
                            await StartAllPublishersAsync();
                        }
                        break;

                    case "stop":
                        if (parts.Length > 1)
                        {
                            var deviceIds = ParseDeviceIds(parts[1]);
                            await StopPublishersAsync(deviceIds);
                        }
                        else
                        {
                            await StopAllPublishersAsync();
                        }
                        break;

                    case "add":
                        if (parts.Length > 1)
                        {
                            var deviceIds = ParseDeviceIds(parts[1]);
                            await AddPublishersAsync(deviceIds);
                        }
                        else
                        {
                            Console.WriteLine("Usage: add <device_ids>");
                        }
                        break;

                    case "remove":
                        if (parts.Length > 1)
                        {
                            var deviceIds = ParseDeviceIds(parts[1]);
                            Console.Write($"Are you sure you want to remove {deviceIds.Count} publishers? (y/N): ");
                            var confirm = Console.ReadLine()?.Trim().ToLower();
                            if (confirm == "y" || confirm == "yes")
                            {
                                await RemovePublishersAsync(deviceIds);
                            }
                            else
                            {
                                Console.WriteLine("Operation cancelled.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Usage: remove <device_ids>");
                        }
                        break;

                    case "enable":
                        if (parts.Length > 1)
                        {
                            var deviceIds = ParseDeviceIds(parts[1]);
                            await EnablePublishersAsync(deviceIds);
                        }
                        else
                        {
                            Console.WriteLine("Usage: enable <device_ids>");
                        }
                        break;

                    case "disable":
                        if (parts.Length > 1)
                        {
                            var deviceIds = ParseDeviceIds(parts[1]);
                            await DisablePublishersAsync(deviceIds);
                        }
                        else
                        {
                            Console.WriteLine("Usage: disable <device_ids>");
                        }
                        break;

                    case "status":
                        await ShowStatusAsync();
                        break;

                    case "restart":
                        if (parts.Length > 1)
                        {
                            var deviceIds = ParseDeviceIds(parts[1]);
                            await RestartPublishersAsync(deviceIds);
                        }
                        else
                        {
                            Console.WriteLine("Usage: restart <device_ids>");
                        }
                        break;

                    case "list":
                        await ListPublishersAsync();
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

    public async Task<bool> StartPublishersAsync(string[] deviceIds, bool all)
    {
        if (all)
        {
            return await StartAllPublishersAsync();
        }
        else if (deviceIds?.Length > 0)
        {
            return await StartPublishersAsync(deviceIds.ToList());
        }
        else
        {
            Console.WriteLine("No device IDs specified and --all flag not set.");
            return false;
        }
    }

    public async Task<bool> StopPublishersAsync(string[] deviceIds, bool all)
    {
        if (all)
        {
            return await StopAllPublishersAsync();
        }
        else if (deviceIds?.Length > 0)
        {
            return await StopPublishersAsync(deviceIds.ToList());
        }
        else
        {
            Console.WriteLine("No device IDs specified and --all flag not set.");
            return false;
        }
    }

    public async Task<bool> StartPublishersAsync(List<string> deviceIds)
    {
        var tasks = deviceIds.Select(deviceId => _publisherManager.StartPublisherAsync(deviceId));
        var results = await Task.WhenAll(tasks);
        var successCount = results.Count(r => r);

        Console.WriteLine($"Started {successCount}/{deviceIds.Count} publishers");
        await _publisherManager.SavePublisherStatesAsync();
        return successCount > 0;
    }

    public async Task<bool> StopPublishersAsync(List<string> deviceIds)
    {
        var tasks = deviceIds.Select(deviceId => _publisherManager.StopPublisherAsync(deviceId));
        var results = await Task.WhenAll(tasks);
        var successCount = results.Count(r => r);

        Console.WriteLine($"Stopped {successCount}/{deviceIds.Count} publishers");
        await _publisherManager.SavePublisherStatesAsync();
        return successCount > 0;
    }

    private async Task<bool> RestartPublishersAsync(List<string> deviceIds)
    {
        var tasks = deviceIds.Select(deviceId => _publisherManager.RestartPublisherAsync(deviceId));
        var results = await Task.WhenAll(tasks);
        var successCount = results.Count(r => r);

        Console.WriteLine($"Restarted {successCount}/{deviceIds.Count} publishers");
        await _publisherManager.SavePublisherStatesAsync();
        return successCount > 0;
    }

    private async Task ListPublishersAsync()
    {
        var states = await _publisherManager.GetPublisherStatesAsync();

        if (!states.Any())
        {
            Console.WriteLine("No publishers found.");
            return;
        }

        Console.WriteLine($"\n=== All Publishers ({states.Count}) ===");
        Console.WriteLine($"{"Device ID",-15} {"Status",-12} {"State",-20}");
        Console.WriteLine(new string('-', 50));

        foreach (var state in states.OrderBy(s => s.DeviceId))
        {
            var status = state.IsEnabled ? "Enabled" : "Disabled";
            var stateInfo = state.IsConnected
                ? (state.IsPublishing ? "Connected+Publishing" : "Connected")
                : "Disconnected";

            Console.WriteLine($"{state.DeviceId,-15} {status,-12} {stateInfo,-20}");
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

        Bulk Operations (use comma-separated IDs):
          start [device_ids]     - Start all publishers or specific ones (e.g., start DEV001,DEV002)
          stop [device_ids]      - Stop all publishers or specific ones
          enable <device_ids>    - Enable specific publishers
          disable <device_ids>   - Disable specific publishers
          remove <device_ids>    - Remove publishers (with confirmation)
          restart <device_ids>   - Restart specific publishers

        Management:
          add <device_ids>       - Add new publishers
          status                 - Show detailed status information
          list                   - List all publishers with their states

        Utility:
          help                   - Show this help message
          clear                  - Clear screen
          exit/quit              - Exit interactive mode

        Examples:
          start                  - Start all publishers
          start DEV001           - Start single publisher
          enable DEV001,DEV002,DEV003 - Enable multiple publishers
          remove DEV001          - Remove single publisher (with confirmation)
        ");
    }
}