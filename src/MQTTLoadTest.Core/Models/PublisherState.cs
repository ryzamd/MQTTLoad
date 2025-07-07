using MQTTLoadTest.Core.Models;

public class PublisherState
{
    public string DeviceId { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public bool IsPublishing { get; set; }
    public bool IsEnabled { get; set; } = true;
    public long MessageCount { get; set; }
    public DateTime LastMessageTime { get; set; }
    public DateTime StartTime { get; set; }
    public double MessagesPerSecond { get; set; }
    public double AverageLatency { get; set; }
    public int ErrorCount { get; set; }
    public string LastError { get; set; } = string.Empty;
    public DateTime LastErrorTime { get; set; }
    public PublisherStatus Status { get; set; } = PublisherStatus.Stopped;
}