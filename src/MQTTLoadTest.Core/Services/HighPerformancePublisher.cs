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
                return new PublisherState
                {
                    PublisherId = _state.PublisherId,
                    DeviceId = _state.DeviceId,
                    IsRunning = _state.IsRunning,
                    IsEnabled = _state.IsEnabled,
                    TotalMessagesSent = _state.TotalMessagesSent,
                    TotalErrors = _state.TotalErrors,
                    LastMessageSent = _state.LastMessageSent,
                    CreatedAt = _state.CreatedAt,
                    AverageLatency = _state.AverageLatency,
                    LastError = _state.LastError,
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

            var interval = 1000.0 / _device.MessagesPerSecond; // Precise timing
            _publishTimer = new Timer(PublishMessage, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(interval));

            IsRunning = true;
            UpdateState(s => s.IsRunning = true);

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
            UpdateState(s => s.IsRunning = false);

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
        if (!IsConnected || _mqttClient == null)
            return false;

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
        if (!IsRunning || !_device.IsEnabled)
            return;

        var message = GenerateMessageData();
        await PublishMessageAsync(message);
    }

    private MessageData GenerateMessageData()
    {
        var sequenceNumber = Interlocked.Increment(ref _sequenceNumber);

        return new MessageData
        {
            DeviceId = _device.DeviceId,
            DeviceName = _device.DeviceName,
            Timestamp = DateTime.UtcNow,
            SequenceNumber = (int)sequenceNumber,
            ScanData = GenerateRandomScanData(),
            Temperature = Math.Round(_random.NextDouble() * 50 + 20, 2), // 20-70°C
            Status = _random.Next(100) < 95 ? "OK" : "WARNING", // 95% OK, 5% WARNING
            AdditionalData = new Dictionary<string, object>
            {
                ["battery_level"] = _random.Next(10, 101),
                ["signal_strength"] = _random.Next(-100, -30)
            }
        };
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
            OnStateChanged?.Invoke(this, State);
        }
    }

    private Task OnConnectedAsync(MqttClientConnectedEventArgs e)
    {
        _logger.LogDebug("Publisher {DeviceId} connected", _device.DeviceId);
        return Task.CompletedTask;
    }

    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        _logger.LogWarning("Publisher {DeviceId} disconnected: {Reason}", _device.DeviceId, e.Reason);

        if (_disposed || !_device.IsEnabled)
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
                    if (IsConnected && IsRunning)
                    {
                        // Timer will resume automatically
                    }
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Publisher {DeviceId} reconnection attempt {Attempt} failed",
                        _device.DeviceId, attempt + 1);
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