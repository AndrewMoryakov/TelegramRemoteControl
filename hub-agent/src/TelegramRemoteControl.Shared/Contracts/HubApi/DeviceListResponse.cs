namespace TelegramRemoteControl.Shared.Contracts.HubApi;

public class DeviceListResponse
{
    public List<DeviceDto> Devices { get; init; } = new();
}
