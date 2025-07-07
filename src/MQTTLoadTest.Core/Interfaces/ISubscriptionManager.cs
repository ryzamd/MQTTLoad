using MQTTLoadTest.Core.Models;

public interface ISubscriptionManager
{
    Task<bool> SubscribeToDeviceAsync(string deviceId, string topic);
    Task<bool> UnsubscribeFromDeviceAsync(string deviceId);
    Task<List<SubscriptionInfo>> GetActiveSubscriptionsAsync();
    Task<List<DeviceConfig>> GetAvailableDevicesAsync();
    Task<List<DeviceConfig>> GetUnsubscribedDevicesAsync();
    Task<bool> IsSubscribedToDeviceAsync(string deviceId);
    Task<DeviceStatistics?> GetDeviceStatisticsAsync(string deviceId);
    Task<Dictionary<string, DeviceStatistics>> GetAllDeviceStatisticsAsync();
    Task<bool> SubscribeToMultipleDevicesAsync(List<string> deviceIds);
    Task<bool> UnsubscribeFromMultipleDevicesAsync(List<string> deviceIds);
}