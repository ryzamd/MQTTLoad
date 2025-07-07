using MQTTLoadTest.Core.Models;

public interface IPublisherManager
{
    Task<bool> StartPublisherAsync(string deviceId);
    Task<bool> StopPublisherAsync(string deviceId);
    Task<bool> EnablePublisherAsync(string deviceId);
    Task<bool> DisablePublisherAsync(string deviceId);
    Task<bool> RemovePublisherAsync(string deviceId);
    Task<bool> RemovePublishersAsync(List<string> deviceIds);
    Task<bool> AddPublisherAsync(DeviceConfig device);
    Task<List<PublisherState>> GetPublisherStatesAsync();
    Task<PublisherState?> GetPublisherStateAsync(string deviceId);
    Task SavePublisherStatesAsync();
    Task LoadPublisherStatesAsync();
    Task<bool> RestartPublisherAsync(string deviceId);
    Task<int> GetActivePublisherCountAsync();
    Task<PerformanceMetrics> GetPerformanceMetricsAsync();
}