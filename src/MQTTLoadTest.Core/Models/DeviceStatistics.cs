using MQTTnet.Protocol;

public class DeviceStatistics
{
    public string DeviceId { get; set; } = string.Empty;
    public long MessageCount { get; set; }
    public DateTime? FirstMessageTime { get; set; }
    public DateTime? LastMessageTime { get; set; }
    public double AverageMessageInterval { get; set; }
    public long TotalDataBytes { get; set; }
    public double MessagesPerSecond { get; set; }
    public Dictionary<MqttQualityOfServiceLevel, long> QoSDistribution { get; set; } = new();
}