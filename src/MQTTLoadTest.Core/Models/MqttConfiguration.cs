public class MqttConfiguration
{
    public string BrokerHost { get; set; } = "localhost";
    public int BrokerPort { get; set; } = 1883;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ClientIdPrefix { get; set; } = "LoadTest";
    public string BaseTopic { get; set; } = "loadtest";
    public int PublisherCount { get; set; } = 10;
    public int MessagesPerSecond { get; set; } = 10;
    public string DeviceListFile { get; set; } = "config/devices.txt";
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    public int KeepAliveSeconds { get; set; } = 60;
    public bool CleanSession { get; set; } = true;
    public QoSDistribution QoSDistribution { get; set; } = new();
    public bool EnableTLS { get; set; } = false;
    public string CertificateFile { get; set; } = string.Empty;
    public int MaxConcurrentPublishers { get; set; } = 500;
    public int MessageQueueSize { get; set; } = 10000;
    public int PublishTimeoutMs { get; set; } = 5000;
    public int ReconnectDelayMs { get; set; } = 5000;
    public int MaxReconnectAttempts { get; set; } = 5;
}