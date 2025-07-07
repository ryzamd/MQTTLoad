public interface IHighPerformancePublisher : IDisposable
{
    string DeviceId { get; }
    bool IsConnected { get; }
    bool IsRunning { get; }
    PublisherState State { get; }

    Task<bool> ConnectAsync();
    Task<bool> DisconnectAsync();
    Task<bool> StartPublishingAsync();
    Task<bool> StopPublishingAsync();
    Task<bool> PublishMessageAsync(MessageData message);

    event EventHandler<string>? OnError;
    event EventHandler<PublisherState>? OnStateChanged;
    event EventHandler<MessageData>? OnMessagePublished;
}