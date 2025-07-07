namespace MQTTLoadTest.Core.Models;

public class PerformanceMetrics
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Core throughput metrics
    public long TotalMessagesPublished { get; set; }
    public long TotalMessagesReceived { get; set; }
    public double PublishRate { get; set; } // Messages/second
    public double ReceiveRate { get; set; } // Messages/second

    // Quality metrics
    public double AverageLatency { get; set; } // Milliseconds
    public double MaxLatency { get; set; }
    public double MinLatency { get; set; }
    public double MessageLossRate { get; set; } // Percentage

    // System resources
    public long MemoryUsage { get; set; } // Bytes
    public double CpuUsage { get; set; } // Percentage
    public double CpuUsagePercent { get; set; } // Alternative name
    public double MemoryUsageMB { get; set; } // Alternative unit

    // Connection health
    public int ActivePublishers { get; set; }
    public int ActiveSubscribers { get; set; }
    public int ActiveConnections { get; set; }
    public int QueueDepth { get; set; }

    // Error tracking
    public long ErrorCount { get; set; }
    public double ErrorRate { get; set; }

    // Legacy properties for compatibility
    public long MessagesPublished { get; set; }
    public long MessagesReceived { get; set; }
    public double MessagesPerSecond { get; set; }

    // Custom metrics
    public Dictionary<string, object> CustomMetrics { get; set; } = new();
    public Dictionary<string, long> CounterMetrics { get; set; } = new();
    public Dictionary<string, double> GaugeMetrics { get; set; } = new();
}