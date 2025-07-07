using MQTTnet.Protocol;

public class DeviceConfig
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public int MessagesPerSecond { get; set; } = 1;
    public MqttQualityOfServiceLevel QoSLevel { get; set; } = MqttQualityOfServiceLevel.AtMostOnce;
    public int MessageSizeBytes { get; set; } = 100;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> CustomProperties { get; set; } = new();
}