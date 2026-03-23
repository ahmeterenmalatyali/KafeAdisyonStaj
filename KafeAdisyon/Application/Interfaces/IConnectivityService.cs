namespace KafeAdisyon.Application.Interfaces;
public interface IConnectivityService
{
    bool IsConnected { get; }
    event EventHandler<bool>? ConnectivityChanged;
}