public interface IHighPerformanceSubscriber : IDisposable
{
    bool IsConnected { get; }
    bool IsRunning { get; }
    int QueueDepth { get; }

    Task<bool> ConnectAsync();
    Task<bool> DisconnectAsync();
    Task<bool> StartReceivingAsync();
    Task<bool> StopReceivingAsync();
    Task<bool> SubscribeAsync(string topic);
    Task<bool> UnsubscribeAsync(string topic);
    Task<List<string>> GetSubscribedTopicsAsync();
    Task<PerformanceMetrics> GetMetricsAsync();

    event EventHandler<MessageData>? OnMessageReceived;
    event EventHandler<string>? OnError;
    event EventHandler<PerformanceMetrics>? OnMetricsUpdated;
}