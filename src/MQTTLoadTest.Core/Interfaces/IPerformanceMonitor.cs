public interface IPerformanceMonitor
{
    void IncrementCounter(string name, long value = 1);
    void SetGauge(string name, double value);
    void RecordLatency(double latencyMs);
    PerformanceMetrics GetCurrentMetrics();
    List<PerformanceMetrics> GetMetricsHistory(int count = 100);
    Task ExportMetricsAsync(string filePath);
    void Reset();
}