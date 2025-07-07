namespace MQTTLoadTest.Core.Models;

public class MqttConfiguration
{
    public string BrokerHost { get; set; } = "localhost";
    public int BrokerPort { get; set; } = 1883;
    public string BasePath { get; set; } = "";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool CleanSession { get; set; } = true;
    public int KeepAliveInterval { get; set; } = 60; // seconds
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool UseTls { get; set; } = false;
    public string BaseTopic { get; set; } = "scanners/data";
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    public string DeviceListFile { get; set; } = "devices.txt";
    public int MessagesPerSecond { get; set; } = 50;
    public int PublisherCount { get; set; } = 100;
    public QoSDistribution QoSDistribution { get; set; } = new();
    public string ClientIdPrefix { get; set; } = "LoadTest";
    public bool EnableTLS { get; set; } = false;
    public string CertificateFile { get; set; } = string.Empty;
    public int MaxConcurrentPublishers { get; set; } = 500;
    public int MessageQueueSize { get; set; } = 10000;
    public int PublishTimeoutMs { get; set; } = 5000;
    public int ReconnectDelayMs { get; set; } = 5000;
    public int MaxReconnectAttempts { get; set; } = 5;
    public int KeepAliveSeconds { get; set; } = 60;
    public string PublisherStateFile { get; set; } = "publisher-states.json";
}