using MQTTLoadTest.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using System.Buffers;

namespace MQTTLoadTest.Core.Services;

public class HighPerformanceSubscriber : IHighPerformanceSubscriber
{
    private readonly MqttConfiguration _config;
    private readonly ILogger<HighPerformanceSubscriber> _logger;
    private readonly IPerformanceMonitor _performanceMonitor;

    private IMqttClient? _mqttClient;
    private readonly Channel<MqttApplicationMessage> _messageChannel;
    private readonly ChannelWriter<MqttApplicationMessage> _messageWriter;
    private readonly ChannelReader<MqttApplicationMessage> _messageReader;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _processingThrottle;
    private readonly ConcurrentDictionary<string, DeviceStatistics> _deviceStats = new();
    private readonly ConcurrentDictionary<string, SubscriptionInfo> _subscriptions = new();

    private bool _disposed = false;
    private bool _isBackpressureActive = false;
    private readonly object _stateLock = new();

    public bool IsConnected => _mqttClient?.IsConnected ?? false;
    public bool IsRunning { get; private set; }
    public int QueueDepth => _messageChannel.Reader.Count;

    public event EventHandler<MessageData>? OnMessageReceived;
    public event EventHandler<string>? OnError;
    public event EventHandler<PerformanceMetrics>? OnMetricsUpdated;

    public HighPerformanceSubscriber(
        IOptions<MqttConfiguration> config,
        ILogger<HighPerformanceSubscriber> logger,
        IPerformanceMonitor performanceMonitor)
    {
        _config = config.Value;
        _logger = logger;
        _performanceMonitor = performanceMonitor;

        // High-capacity message channel with backpressure handling
        var channelOptions = new BoundedChannelOptions(50000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true,
            AllowSynchronousContinuations = false
        };

        _messageChannel = Channel.CreateBounded<MqttApplicationMessage>(channelOptions);
        _messageWriter = _messageChannel.Writer;
        _messageReader = _messageChannel.Reader;

        // Processing throttle based on CPU cores
        _processingThrottle = new SemaphoreSlim(Environment.ProcessorCount * 2);
    }

    public async Task<bool> ConnectAsync()
    {
        try
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HighPerformanceSubscriber));

            _mqttClient = new MqttClientFactory().CreateMqttClient();

            _mqttClient.DisconnectedAsync += OnDisconnectedAsync;
            _mqttClient.ConnectedAsync += OnConnectedAsync;
            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_config.BrokerHost, _config.BrokerPort)
                .WithClientId("LoadTest_Subscriber")
                .WithCleanSession(_config.CleanSession)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(_config.KeepAliveInterval))
                .WithTimeout(TimeSpan.FromSeconds(_config.ConnectionTimeoutSeconds))
                .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V311);

            if (!string.IsNullOrEmpty(_config.Username))
            {
                options = options.WithCredentials(_config.Username, _config.Password);
            }

            var result = await _mqttClient.ConnectAsync(options.Build());

            if (result.ResultCode == MqttClientConnectResultCode.Success)
            {
                _logger.LogInformation("Subscriber connected successfully");
                return true;
            }
            else
            {
                _logger.LogError("Subscriber connection failed: {ResultCode}", result.ResultCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Subscriber connection error");
            OnError?.Invoke(this, ex.Message);
            return false;
        }
    }

    public async Task<bool> DisconnectAsync()
    {
        try
        {
            await StopReceivingAsync();

            if (_mqttClient?.IsConnected == true)
            {
                await _mqttClient.DisconnectAsync();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Subscriber disconnect error");
            return false;
        }
    }

    public async Task<bool> StartReceivingAsync()
    {
        try
        {
            if (!IsConnected)
            {
                if (!await ConnectAsync())
                    return false;
            }

            if (IsRunning)
                return true;

            // Start message processing workers
            StartMessageProcessors();

            IsRunning = true;
            _performanceMonitor.SetGauge("active_subscribers", 1);

            _logger.LogInformation("Subscriber started receiving messages");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Subscriber start error");
            return false;
        }
    }

    public async Task<bool> StopReceivingAsync()
    {
        try
        {
            if (!IsRunning)
                return true;

            IsRunning = false;
            _performanceMonitor.SetGauge("active_subscribers", 0);

            // Wait for message queue to drain
            var drainStartTime = DateTime.UtcNow;
            while (QueueDepth > 0 && DateTime.UtcNow - drainStartTime < TimeSpan.FromSeconds(30))
            {
                await Task.Delay(1000);
                _logger.LogInformation("Draining queue, depth: {QueueDepth}", QueueDepth);
            }

            _logger.LogInformation("Subscriber stopped receiving messages");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Subscriber stop error");
            return false;
        }
    }

    public async Task<bool> SubscribeAsync(string topic)
    {
        try
        {
            if (_mqttClient?.IsConnected != true)
                return false;

            await _mqttClient.SubscribeAsync(topic, MqttQualityOfServiceLevel.AtLeastOnce);

            var subscription = new SubscriptionInfo
            {
                Topic = topic,
                IsActive = true,
                SubscribedAt = DateTime.UtcNow,
                QoSLevel = MqttQualityOfServiceLevel.AtLeastOnce
            };

            _subscriptions.AddOrUpdate(topic, subscription, (key, existing) => subscription);

            _logger.LogInformation("Subscribed to topic: {Topic}", topic);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Subscribe error for topic: {Topic}", topic);
            return false;
        }
    }

    public async Task<bool> UnsubscribeAsync(string topic)
    {
        try
        {
            if (_mqttClient?.IsConnected != true)
                return false;

            await _mqttClient.UnsubscribeAsync(topic);

            if (_subscriptions.TryRemove(topic, out var subscription))
            {
                _logger.LogInformation("Unsubscribed from topic: {Topic}", topic);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unsubscribe error for topic: {Topic}", topic);
            return false;
        }
    }

    public Task<List<string>> GetSubscribedTopicsAsync()
    {
        var topics = _subscriptions.Keys.ToList();
        return Task.FromResult(topics);
    }

    public Task<PerformanceMetrics> GetMetricsAsync()
    {
        var metrics = _performanceMonitor.GetCurrentMetrics();
        metrics.QueueDepth = QueueDepth;
        return Task.FromResult(metrics);
    }

    private void StartMessageProcessors()
    {
        var processorCount = Environment.ProcessorCount;

        for (int i = 0; i < processorCount; i++)
        {
            var processorId = i;
            _ = Task.Run(async () => await MessageProcessor(processorId), _cancellationTokenSource.Token);
        }

        _logger.LogInformation("Started {ProcessorCount} message processors", processorCount);
    }

    private async Task MessageProcessor(int processorId)
    {
        await foreach (var message in _messageReader.ReadAllAsync(_cancellationTokenSource.Token))
        {
            if (_cancellationTokenSource.Token.IsCancellationRequested)
                break;

            await _processingThrottle.WaitAsync(_cancellationTokenSource.Token);

            try
            {
                await ProcessMessage(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Message processing failed in processor {ProcessorId}", processorId);
            }
            finally
            {
                _processingThrottle.Release();
            }
        }
    }

    private async Task<bool> OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            _performanceMonitor.IncrementCounter("messages_received");

            // Non-blocking enqueue attempt
            if (!_messageWriter.TryWrite(e.ApplicationMessage))
            {
                // Activate backpressure mode
                if (!_isBackpressureActive)
                {
                    _isBackpressureActive = true;
                    _logger.LogWarning("Backpressure activated - message queue is full");
                }

                // Try with timeout to avoid blocking MQTT receive thread
                var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                try
                {
                    await _messageWriter.WriteAsync(e.ApplicationMessage, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Drop message if cannot enqueue within timeout
                    _logger.LogWarning("Message dropped due to queue overflow");
                    _performanceMonitor.IncrementCounter("messages_dropped");
                    return false;
                }
            }

            if (_isBackpressureActive && QueueDepth < 25000) // 50% of capacity
            {
                _isBackpressureActive = false;
                _logger.LogInformation("Backpressure deactivated - queue level normal");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling received message");
            return false;
        }
    }

    public async Task UpdateMetricsAsync()
    {
        try
        {
            var metrics = await GetMetricsAsync();
            OnMetricsUpdated?.Invoke(this, metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating metrics");
        }
    }


    private async Task ProcessMessage(MqttApplicationMessage message)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Convert ReadOnlySequence<byte> to byte array for deserialization  
            var payloadBytes = message.Payload.ToArray();
            var messageData = JsonSerializer.Deserialize<MessageData>(payloadBytes);
            if (messageData == null)
            {
                _logger.LogWarning("Failed to deserialize message from topic: {Topic}", message.Topic);
                return;
            }

            // Update device statistics atomically  
            UpdateDeviceStatistics(messageData);

            // Handle QoS acknowledgments (handled by MQTTnet library automatically)  
            await HandleQoSProcessing(message);

            stopwatch.Stop();
            var processingTime = stopwatch.Elapsed.TotalMilliseconds;

            _performanceMonitor.IncrementCounter("messages_processed");
            _performanceMonitor.RecordLatency(processingTime);

            OnMessageReceived?.Invoke(this, messageData);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize message payload from topic: {Topic}", message.Topic);
            _performanceMonitor.IncrementCounter("deserialization_errors");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message processing failed for topic: {Topic}", message.Topic);
            _performanceMonitor.IncrementCounter("processing_errors");
        }
    }

    private void UpdateDeviceStatistics(MessageData messageData)
    {
        var deviceId = messageData.DeviceId;

        _deviceStats.AddOrUpdate(deviceId,
            // Add new device
            new DeviceStatistics
            {
                DeviceId = deviceId,
                LastSeen = DateTime.UtcNow,
                MessageCount = 1,
                LastSequenceNumber = messageData.SequenceNumber,
                LastStatus = messageData.Status
            },
            // Update existing device
            (key, existing) =>
            {
                existing.MessageCount++;
                existing.LastSeen = DateTime.UtcNow;

                // Detect sequence gaps (message loss indicator)
                if (messageData.SequenceNumber != existing.LastSequenceNumber + 1 && existing.LastSequenceNumber > 0)
                {
                    existing.SequenceGaps++;
                }
                existing.LastSequenceNumber = messageData.SequenceNumber;
                existing.LastStatus = messageData.Status;

                return existing;
            });
    }

    private async Task HandleQoSProcessing(MqttApplicationMessage message)
    {
        // QoS acknowledgments are handled automatically by MQTTnet library
        // This method can be extended for custom QoS 2 duplicate detection if needed
        switch (message.QualityOfServiceLevel)
        {
            case MqttQualityOfServiceLevel.AtMostOnce:
                // No acknowledgment needed
                break;
            case MqttQualityOfServiceLevel.AtLeastOnce:
                // PUBACK automatically handled by library
                break;
            case MqttQualityOfServiceLevel.ExactlyOnce:
                // PUBREC/PUBREL/PUBCOMP handled by library
                // Custom duplicate detection could be implemented here
                break;
        }

        await Task.CompletedTask;
    }

    public async Task<DeviceStatistics?> GetDeviceStatisticsAsync(string deviceId)
    {
        _deviceStats.TryGetValue(deviceId, out var stats);
        return await Task.FromResult(stats);
    }

    public async Task<Dictionary<string, DeviceStatistics>> GetAllDeviceStatisticsAsync()
    {
        var stats = new Dictionary<string, DeviceStatistics>(_deviceStats);
        return await Task.FromResult(stats);
    }

    private Task OnConnectedAsync(MqttClientConnectedEventArgs e)
    {
        _logger.LogDebug("Subscriber connected to broker");
        return Task.CompletedTask;
    }

    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        _logger.LogWarning("Subscriber disconnected: {Reason}", e.Reason);

        if (_disposed)
            return;

        // Auto-reconnect with exponential backoff
        await Task.Run(async () =>
        {
            int attempt = 0;
            while (!IsConnected && attempt < _config.MaxRetryAttempts && !_disposed)
            {
                var delay = Math.Min(60000, (int)Math.Pow(2, attempt) * 1000);
                await Task.Delay(delay);

                try
                {
                    await ConnectAsync();
                    if (IsConnected)
                    {
                        // Re-subscribe to all previous topics
                        var topics = await GetSubscribedTopicsAsync();
                        foreach (var topic in topics)
                        {
                            await SubscribeAsync(topic);
                        }
                    }
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Subscriber reconnection attempt {Attempt} failed", attempt + 1);
                    attempt++;
                }
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _cancellationTokenSource.Cancel();
            StopReceivingAsync().Wait(5000);
            _mqttClient?.Dispose();
            _cancellationTokenSource.Dispose();
            _processingThrottle.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Subscriber dispose error");
        }
    }
}