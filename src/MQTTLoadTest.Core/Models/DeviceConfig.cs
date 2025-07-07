namespace MQTTLoadTest.Core.Models;

public class DeviceConfig
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int MessagesPerSecond { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan PublishInterval { get; set; } = TimeSpan.FromSeconds(1);

    // QoS distribution settings
    public QoSDistribution QoSDistribution { get; set; } = new();
}