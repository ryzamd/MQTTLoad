using MQTTLoadTest.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;
using System.Diagnostics;
using System.Text.Json;

namespace MQTTLoadTest.Core.Services;

public class HighPerformancePublisher : IHighPerformancePublisher
{
    private readonly DeviceConfig _device;
    private readonly MqttConfiguration _config;
    private readonly ILogger<HighPerformancePublisher> _logger;
    private readonly IPerformanceMonitor _performanceMonitor;

    private IMqttClient? _mqttClient;
    private Timer? _publishTimer;
    private long _sequenceNumber = 0;
    private readonly Random _random = new();
    private readonly PublisherState _state;
    private readonly object _stateLock = new();
    private bool _disposed = false;

    public string DeviceId => _device.DeviceId;
    public bool IsConnected => _mqttClient?.IsConnected ?? false;
    public bool IsRunning { get; private set; }

    public PublisherState State
    {
        get
        {
            lock (_stateLock)
            {
                // CRITICAL: Always sync with actual connection status
                _state.IsConnected = _mqttClient?.IsConnected ?? false;
                _state.IsRunning = IsRunning;

                return new PublisherState
                {
                    PublisherId = _state.PublisherId,
                    DeviceId = _state.DeviceId,
                    IsConnected = _state.IsConnected,  // Real connection status
                    IsRunning = _state.IsRunning,
                    IsPublishing = _state.IsPublishing,
                    IsEnabled = _state.IsEnabled,
                    TotalMessagesSent = _state.TotalMessagesSent,
                    TotalErrors = _state.TotalErrors,
                    LastMessageSent = _state.LastMessageSent,
                    CreatedAt = _state.CreatedAt,
                    AverageLatency = _state.AverageLatency,
                    LastError = _state.LastError,
                    Status = _state.Status,
                    QoSMessageCounts = new Dictionary<string, long>(_state.QoSMessageCounts)
                };
            }
        }
    }

    public event EventHandler<string>? OnError;
    public event EventHandler<PublisherState>? OnStateChanged;
    public event EventHandler<MessageData>? OnMessagePublished;

    public HighPerformancePublisher(
        DeviceConfig device,
        IOptions<MqttConfiguration> config,
        ILogger<HighPerformancePublisher> logger,
        IPerformanceMonitor performanceMonitor)
    {
        _device = device;
        _config = config.Value;
        _logger = logger;
        _performanceMonitor = performanceMonitor;

        _state = new PublisherState
        {
            PublisherId = $"pub_{device.DeviceId}",
            DeviceId = device.DeviceId,
            IsEnabled = device.IsEnabled,
            CreatedAt = DateTime.UtcNow
        };
    }

    public async Task<bool> ConnectAsync()
    {
        try
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HighPerformancePublisher));

            _mqttClient = new MqttClientFactory().CreateMqttClient();

            _mqttClient.DisconnectedAsync += OnDisconnectedAsync;
            _mqttClient.ConnectedAsync += OnConnectedAsync;

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_config.BrokerHost, _config.BrokerPort)
                .WithClientId($"Publisher_{_device.DeviceId}")
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
                UpdateState(s => s.LastError = string.Empty);
                _logger.LogInformation("Publisher {DeviceId} connected successfully", _device.DeviceId);
                _logger.LogInformation("Publisher {DeviceId} connection result: {IsConnected}", _device.DeviceId, IsConnected);
                return true;
            }
            else
            {
                var error = $"Connection failed: {result.ResultCode}";
                UpdateState(s => s.LastError = error);
                _logger.LogError("Publisher {DeviceId} connection failed: {ResultCode}", _device.DeviceId, result.ResultCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            var error = $"Connection error: {ex.Message}";
            UpdateState(s => s.LastError = error);
            _logger.LogError(ex, "Publisher {DeviceId} connection error", _device.DeviceId);
            OnError?.Invoke(this, error);
            return false;
        }
    }

    public async Task<bool> DisconnectAsync()
    {
        try
        {
            await StopPublishingAsync();

            if (_mqttClient?.IsConnected == true)
            {
                await _mqttClient.DisconnectAsync();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Publisher {DeviceId} disconnect error", _device.DeviceId);
            return false;
        }
    }

    public async Task<bool> StartPublishingAsync()
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

            var interval = 1000.0 / _device.MessagesPerSecond;
            _publishTimer = new Timer(PublishMessage, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(interval));

            IsRunning = true;

            // CRITICAL FIX: Set both IsRunning AND IsPublishing
            UpdateState(s => {
                s.IsRunning = true;
                s.IsPublishing = true;  // This was missing!
            });

            _logger.LogInformation("Publisher {DeviceId} started publishing at {Rate} msg/s",
                _device.DeviceId, _device.MessagesPerSecond);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Publisher {DeviceId} start error", _device.DeviceId);
            return false;
        }
    }

    public async Task<bool> StopPublishingAsync()
    {
        try
        {
            if (!IsRunning)
                return true;

            if (_publishTimer != null)
            {
                await Task.Run(() => _publishTimer.Dispose());
            }
            _publishTimer = null;

            IsRunning = false;

            // CRITICAL FIX: Set both states
            UpdateState(s => {
                s.IsRunning = false;
                s.IsPublishing = false;  // This was missing!
            });

            _logger.LogInformation("Publisher {DeviceId} stopped publishing", _device.DeviceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Publisher {DeviceId} stop error", _device.DeviceId);
            return false;
        }
    }

    public async Task<bool> PublishMessageAsync(MessageData message)
    {
        _logger.LogDebug("Publisher {DeviceId} attempting to publish message {SeqNum}",
        _device.DeviceId, message.SequenceNumber);

        if (!IsConnected || _mqttClient == null)
        {
            _logger.LogWarning("Publisher {DeviceId} cannot publish - IsConnected:{IsConnected}",
                _device.DeviceId, IsConnected);
            return false;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();

            var qosLevel = DetermineQoSLevel();
            var payload = JsonSerializer.Serialize(message);

            var mqttMessage = new MqttApplicationMessageBuilder()
                .WithTopic(_device.Topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qosLevel)
                .Build();

            await _mqttClient.PublishAsync(mqttMessage);

            stopwatch.Stop();
            var latency = stopwatch.Elapsed.TotalMilliseconds;

            UpdateState(s =>
            {
                s.TotalMessagesSent++;
                s.LastMessageSent = DateTime.UtcNow;
                s.AverageLatency = CalculateAverageLatency(s.AverageLatency, latency, s.TotalMessagesSent);

                var qosKey = qosLevel.ToString();
                if (!s.QoSMessageCounts.ContainsKey(qosKey))
                    s.QoSMessageCounts[qosKey] = 0;
                s.QoSMessageCounts[qosKey]++;
            });

            _performanceMonitor.IncrementCounter("messages_published");
            _performanceMonitor.RecordLatency(latency);

            OnMessagePublished?.Invoke(this, message);

            return true;
        }
        catch (Exception ex)
        {
            UpdateState(s =>
            {
                s.TotalErrors++;
                s.LastError = ex.Message;
            });

            _logger.LogError(ex, "Publisher {DeviceId} publish error", _device.DeviceId);
            OnError?.Invoke(this, ex.Message);
            return false;
        }
    }

    private async void PublishMessage(object? state)
    {
        try
        {
            // Debug logging
            if (!IsRunning)
            {
                _logger.LogDebug("Publisher {DeviceId} not running, skipping publish", _device.DeviceId);
                return;
            }

            if (!_device.IsEnabled)
            {
                _logger.LogDebug("Publisher {DeviceId} not enabled, skipping publish", _device.DeviceId);
                return;
            }

            if (!IsConnected)
            {
                _logger.LogDebug("Publisher {DeviceId} not connected, skipping publish", _device.DeviceId);
                return;
            }

            // Generate and publish message
            var message = GenerateMessageData();
            var success = await PublishMessageAsync(message);

            if (success)
            {
                _logger.LogTrace("Publisher {DeviceId} published message {SequenceNumber}",
                    _device.DeviceId, message.SequenceNumber);
            }
            else
            {
                _logger.LogWarning("Publisher {DeviceId} failed to publish message {SequenceNumber}",
                    _device.DeviceId, message.SequenceNumber);
            }
        }
        catch (Exception ex)
        {
            // CRITICAL: Catch exceptions in async void
            _logger.LogError(ex, "Publisher {DeviceId} publish timer error", _device.DeviceId);

            UpdateState(s =>
            {
                s.TotalErrors++;
                s.LastError = $"Timer error: {ex.Message}";
            });
        }
    }

    private MessageData GenerateMessageData()
    {
        try
        {
            var sequenceNumber = Interlocked.Increment(ref _sequenceNumber);

            _logger.LogDebug("Publisher {DeviceId} generating message {SeqNum}", _device.DeviceId, sequenceNumber);

            var message = new MessageData
            {
                DeviceId = _device.DeviceId,
                DeviceName = _device.DeviceName,
                Timestamp = DateTime.UtcNow,
                SequenceNumber = (int)sequenceNumber,
                ScanData = GenerateRandomScanData(),
                Temperature = Math.Round(_random.NextDouble() * 50 + 20, 2),
                Status = _random.Next(100) < 95 ? "Active" : "Warning"
            };

            _logger.LogDebug("Publisher {DeviceId} generated message successfully", _device.DeviceId);
            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Publisher {DeviceId} GenerateMessageData failed", _device.DeviceId);
            throw;
        }
    }

    private string GenerateRandomScanData()
    {
        // Generate realistic barcode data
        var prefixes = new[] { "123456", "789012", "345678", "901234" };
        var prefix = prefixes[_random.Next(prefixes.Length)];
        var suffix = _random.Next(1000000, 9999999);
        return $"{prefix}{suffix}";
    }

    private MqttQualityOfServiceLevel DetermineQoSLevel()
    {
        var rand = _random.Next(100);
        var dist = _device.QoSDistribution;

        if (rand < dist.QoS0Percentage)
            return MqttQualityOfServiceLevel.AtMostOnce;
        else if (rand < dist.QoS0Percentage + dist.QoS1Percentage)
            return MqttQualityOfServiceLevel.AtLeastOnce;
        else
            return MqttQualityOfServiceLevel.ExactlyOnce;
    }

    private double CalculateAverageLatency(double current, double newLatency, long messageCount)
    {
        if (messageCount == 1)
            return newLatency;

        return ((current * (messageCount - 1)) + newLatency) / messageCount;
    }

    private void UpdateState(Action<PublisherState> updateAction)
    {
        lock (_stateLock)
        {
            updateAction(_state);
            _state.IsConnected = IsConnected; // Sync with actual connection status
        }

        // Fire event outside of lock to prevent deadlock
        OnStateChanged?.Invoke(this, State);
    }

    private async Task OnConnectedAsync(MqttClientConnectedEventArgs e)
    {
        await Task.Run(() =>
        {
            lock (_stateLock)
            {
                _state.IsConnected = true;
                _state.Status = PublisherStatus.Connected;
            }
            _logger.LogInformation("Publisher {DeviceId} connected", _device.DeviceId);
            OnStateChanged?.Invoke(this, State);
        });
    }

    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        UpdateState(s => {
            s.IsConnected = false;
            s.IsPublishing = false;
            s.Status = PublisherStatus.Disconnected;
            s.LastError = e.Reason.ToString() ?? "Unknown disconnect";
        });

        _logger.LogWarning("Publisher {DeviceId} disconnected: {Reason}", _device.DeviceId, e.Reason);

        if (_disposed || !_device.IsEnabled) return;

        // Prevent rapid reconnection - add jitter
        var delay = 500 + new Random().Next(0, 1000);
        await Task.Delay(delay);

        _ = Task.Run(async () => await ReconnectWithBackoffAsync());
    }

    private async Task ReconnectWithBackoffAsync()
    {
        int attempt = 0;
        while (!IsConnected && attempt < _config.MaxRetryAttempts && !_disposed)
        {
            var baseDelay = (int)Math.Pow(2, attempt) * 1000;
            var jitter = new Random().Next(0, 1000);
            var delay = Math.Min(30000, baseDelay + jitter);

            await Task.Delay(delay);

            try
            {
                if (_disposed) break;

                var connected = await ConnectAsync();
                if (connected && IsRunning)
                {
                    await StartPublishingAsync();
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Publisher {DeviceId} reconnection attempt {Attempt} failed",
                    _device.DeviceId, attempt + 1);

                UpdateState(s => s.LastError = $"Reconnect failed: {ex.Message}");
            }

            attempt++;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            StopPublishingAsync().Wait(5000);
            _mqttClient?.Dispose();
            _publishTimer?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Publisher {DeviceId} dispose error", _device.DeviceId);
        }
    }
}