using MQTTnet.Protocol;

namespace MQTTLoadTest.Core.Models;

public class DeviceStatistics
{
    public string DeviceId { get; set; } = string.Empty;
    public long MessageCount { get; set; }
    public DateTime? FirstMessageTime { get; set; }
    public DateTime? LastMessageTime { get; set; }
    public DateTime? LastSeen { get; set; }
    public double AverageMessageInterval { get; set; }
    public long TotalDataBytes { get; set; }
    public double MessagesPerSecond { get; set; }

    // Sequence tracking
    public int LastSequenceNumber { get; set; }
    public List<int> SequenceGaps { get; set; } = new();

    // Status tracking
    public string LastStatus { get; set; } = string.Empty;

    // QoS distribution
    public Dictionary<MqttQualityOfServiceLevel, long> QoSDistribution { get; set; } = new();
}