using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http.Json;

namespace FoodStreetGuide.Services;

public sealed class DevicePresenceService
{
    private const string BaseUrl = "https://foodstreetweb-sfecqdx26a-as.a.run.app";

    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly HttpClient _httpClient;
    private HubConnection? _connection;

    public string DeviceId { get; }

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    // true = online, false = offline
    public event Action<bool>? ConnectionStateChanged;

    public DevicePresenceService()
    {
        DeviceId = DeviceIdService.GetOrCreateDeviceId();

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };

        BuildConnection();
    }

    private void BuildConnection()
    {
        var hubUrl = $"{BaseUrl}/hubs/device-presence?deviceId={Uri.EscapeDataString(DeviceId)}";

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        _connection.Reconnecting += _ =>
        {
            ConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        _connection.Reconnected += async _ =>
        {
            await RegisterDeviceAsync();
            ConnectionStateChanged?.Invoke(true);
        };

        _connection.Closed += _ =>
        {
            ConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
        };
    }

    public async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null)
            BuildConnection();

        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is null)
                return;

            if (_connection.State is HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting)
                return;

            await _connection.StartAsync(cancellationToken);
            await RegisterDeviceAsync(cancellationToken);
            ConnectionStateChanged?.Invoke(true);
        }
        catch
        {
            ConnectionStateChanged?.Invoke(false);
            throw;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private async Task RegisterDeviceAsync(CancellationToken cancellationToken = default)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            await _connection.InvokeAsync("RegisterDevice", DeviceId, cancellationToken);
        }
    }

    public async Task<List<OnlineDeviceDto>> GetOnlineDevicesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<OnlineDeviceListResponse>("api/DevicePresence/online-devices", cancellationToken);
        return response?.Devices ?? new List<OnlineDeviceDto>();
    }

    public async Task<bool> IsDeviceOnlineAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<OnlineStatusResponse>($"api/DevicePresence/is-online/{Uri.EscapeDataString(deviceId)}", cancellationToken);
        return response?.Online ?? false;
    }
}

public sealed class OnlineDeviceListResponse
{
    public int Count { get; set; }
    public List<OnlineDeviceDto> Devices { get; set; } = new();
}

public sealed class OnlineDeviceDto
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTime ConnectedAtUtc { get; set; }
    public int ConnectionCount { get; set; }
}

public sealed class OnlineStatusResponse
{
    public string DeviceId { get; set; } = string.Empty;
    public bool Online { get; set; }
}
