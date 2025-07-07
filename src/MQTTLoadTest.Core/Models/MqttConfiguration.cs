namespace MQTTLoadTest.Core.Models;

public class MqttConfiguration
{
    public string BrokerHost { get; set; } = "localhost";
    public int BrokerPort { get; set; } = 1883;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool CleanSession { get; set; } = true;
    public int KeepAliveInterval { get; set; } = 60; // seconds
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool UseTls { get; set; } = false;
}