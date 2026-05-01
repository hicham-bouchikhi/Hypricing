namespace Hypricing.Core.Contracts;

public sealed record IdleGeneral(
    string? LockCmd,
    string? UnlockCmd,
    string? BeforeSleepCmd,
    string? AfterSleepCmd);

public sealed record IdleListener(int Timeout, string OnTimeout, string? OnResume);

public sealed record IdleConfig(IdleGeneral General, IdleListener[] Listeners);

public interface IIdleBackend
{
    string PresetName { get; }
    Task<IdleConfig> GetConfigAsync(CancellationToken ct = default);
    Task SaveAsync(IdleConfig config, CancellationToken ct = default);
    Task<bool> IsDaemonRunningAsync(CancellationToken ct = default);
    Task StartDaemonAsync(CancellationToken ct = default);
    Task StopDaemonAsync(CancellationToken ct = default);
    Task RestartDaemonAsync(CancellationToken ct = default);
}
