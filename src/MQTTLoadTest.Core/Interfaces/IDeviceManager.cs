public interface IDeviceManager
{
    Task<List<DeviceConfig>> LoadDevicesAsync();
    Task SaveDevicesAsync(List<DeviceConfig> devices);
    Task<DeviceConfig> GenerateDeviceAsync(int index);
    Task<List<DeviceConfig>> GenerateDevicesAsync(int count);
    DeviceConfig CreateDevice(string deviceId, string deviceName, string topic);
    bool ValidateDeviceId(string deviceId);
}