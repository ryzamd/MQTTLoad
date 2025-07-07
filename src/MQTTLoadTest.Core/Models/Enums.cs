using MQTTnet.Protocol;

namespace MQTTLoadTest.Core.Models;

public enum PublisherStatus
{
    Stopped,
    Starting,
    Connected,
    Publishing,
    Stopping,
    Error,
    Disconnected
}

public enum SubscriberStatus
{
    Stopped,
    Starting,
    Connected,
    Receiving,
    Stopping,
    Error,
    Disconnected
}

public enum MessageType
{
    SensorData,
    Heartbeat,
    Alert,
    Configuration,
    Status
}