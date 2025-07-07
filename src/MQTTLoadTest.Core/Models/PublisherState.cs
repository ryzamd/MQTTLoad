namespace MQTTLoadTest.Core.Models;

public class PublisherState
{
    public string PublisherId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public bool IsPublishing { get; set; }
    public bool IsRunning { get; set; }
    public bool IsEnabled { get; set; } = true;

    // Message tracking
    public long MessageCount { get; set; }
    public long TotalMessagesSent { get; set; }
    public DateTime LastMessageTime { get; set; }
    public DateTime LastMessageSent { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime CreatedAt { get; set; }

    // Performance metrics
    public double MessagesPerSecond { get; set; }
    public double AverageLatency { get; set; }

    // Error tracking
    public int ErrorCount { get; set; }
    public long TotalErrors { get; set; }
    public string LastError { get; set; } = string.Empty;
    public DateTime LastErrorTime { get; set; }

    // QoS tracking
    public Dictionary<string, long> QoSMessageCounts { get; set; } = new();

    // Status
    public PublisherStatus Status { get; set; } = PublisherStatus.Stopped;
}