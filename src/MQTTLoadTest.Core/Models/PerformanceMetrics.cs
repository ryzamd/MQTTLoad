public class PerformanceMetrics
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public long MessagesPublished { get; set; }
    public long MessagesReceived { get; set; }
    public double MessagesPerSecond { get; set; }
    public double AverageLatency { get; set; }
    public double MaxLatency { get; set; }
    public double MinLatency { get; set; }
    public long ErrorCount { get; set; }
    public double ErrorRate { get; set; }
    public double CpuUsagePercent { get; set; }
    public double MemoryUsageMB { get; set; }
    public int ActiveConnections { get; set; }
    public int QueueDepth { get; set; }
    public Dictionary<string, long> CounterMetrics { get; set; } = new();
    public Dictionary<string, double> GaugeMetrics { get; set; } = new();
}