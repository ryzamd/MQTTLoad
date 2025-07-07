using MQTTLoadTest.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace MQTTLoadTest.Core.Services;

public class DeviceManager : IDeviceManager
{
    private readonly MqttConfiguration _config;
    private readonly ILogger<DeviceManager> _logger;
    private readonly Random _random = new();
    private static readonly Regex DeviceIdPattern = new(@"^[A-Z0-9]{10}$", RegexOptions.Compiled);

    public DeviceManager(IOptions<MqttConfiguration> config, ILogger<DeviceManager> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public async Task<List<DeviceConfig>> LoadDevicesAsync()
    {
        try
        {
            if (!File.Exists(_config.DeviceListFile))
            {
                _logger.LogInformation("Device file not found, generating new devices");
                var deviceList = await GenerateDevicesAsync(_config.PublisherCount);
                await SaveDevicesAsync(deviceList);
                return deviceList;
            }

            var lines = await File.ReadAllLinesAsync(_config.DeviceListFile);
            var devices = new List<DeviceConfig>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    continue;

                var parts = line.Split('|');
                if (parts.Length >= 3)
                {
                    devices.Add(CreateDevice(parts[0].Trim(), parts[1].Trim(), parts[2].Trim()));
                }
            }

            _logger.LogInformation("Loaded {Count} devices from file", devices.Count);
            return devices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load devices from file");
            throw;
        }
    }

    public async Task SaveDevicesAsync(List<DeviceConfig> devices)
    {
        try
        {
            var lines = devices.Select(d => $"{d.DeviceId}|{d.DeviceName}|{d.Topic}").ToArray();
            await File.WriteAllLinesAsync(_config.DeviceListFile, lines);
            _logger.LogInformation("Saved {Count} devices to file", devices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save devices to file");
            throw;
        }
    }

    public Task<DeviceConfig> GenerateDeviceAsync(int index)
    {
        var deviceId = GenerateDeviceId();
        var deviceName = $"{deviceId}: Scanner{index + 1}";
        var topic = $"{_config.BaseTopic}/{deviceId}";

        return Task.FromResult(CreateDevice(deviceId, deviceName, topic));
    }

    public async Task<List<DeviceConfig>> GenerateDevicesAsync(int count)
    {
        var devices = new List<DeviceConfig>();
        var existingIds = new HashSet<string>();

        for (int i = 0; i < count; i++)
        {
            string deviceId;
            do
            {
                deviceId = GenerateDeviceId();
            } while (existingIds.Contains(deviceId));

            existingIds.Add(deviceId);
            devices.Add(await GenerateDeviceAsync(i));
        }

        return devices;
    }

    public DeviceConfig CreateDevice(string deviceId, string deviceName, string topic)
    {
        return new DeviceConfig
        {
            DeviceId = deviceId,
            DeviceName = deviceName,
            Topic = topic,
            MessagesPerSecond = _config.MessagesPerSecond,
            QoSDistribution = _config.QoSDistribution,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool ValidateDeviceId(string deviceId)
    {
        return !string.IsNullOrEmpty(deviceId) && DeviceIdPattern.IsMatch(deviceId);
    }

    private string GenerateDeviceId()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, 10)
            .Select(s => s[_random.Next(s.Length)]).ToArray());
    }
}
