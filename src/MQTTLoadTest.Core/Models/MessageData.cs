using MQTTnet.Protocol;

public class MessageData
{
    public string DeviceId { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public MqttQualityOfServiceLevel QoS { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool Retain { get; set; }
    public long MessageId { get; set; }
    public int PayloadSize { get; set; }
}