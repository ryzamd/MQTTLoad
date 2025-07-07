using MQTTnet.Protocol;

public class SubscriptionInfo
{
    public string DeviceId { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime SubscribedAt { get; set; }
    public DateTime LastMessageReceived { get; set; }
    public long MessageCount { get; set; }
    public MqttQualityOfServiceLevel QoSLevel { get; set; }
}