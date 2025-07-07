using MQTTnet.Protocol;

namespace MQTTLoadTest.Core.Models;

public class MessageData
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int SequenceNumber { get; set; }
    public string ScanData { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public MqttQualityOfServiceLevel QoS { get; set; }
    public bool Retain { get; set; }
    public long MessageId { get; set; }
    public int PayloadSize { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}