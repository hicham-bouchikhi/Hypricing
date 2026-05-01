using System.Text.Json.Serialization;

namespace Hypricing.Core.Backends.Idle;

public sealed class IdlePreset
{
    public string Name { get; set; } = string.Empty;
    public string Detect { get; set; } = string.Empty;
    public string ConfigPath { get; set; } = string.Empty;
    public IdlePresetDaemon Daemon { get; set; } = new();
    public IdlePresetFields Fields { get; set; } = new();
}

public sealed class IdlePresetDaemon
{
    public string Start { get; set; } = string.Empty;
    public string Stop { get; set; } = string.Empty;
    public string Restart { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class IdlePresetFields
{
    public IdlePresetGeneralFields General { get; set; } = new();
    public IdlePresetListenerFields Listener { get; set; } = new();
}

public sealed class IdlePresetGeneralFields
{
    public string LockCmd { get; set; } = "lock_cmd";
    public string UnlockCmd { get; set; } = "unlock_cmd";
    public string BeforeSleepCmd { get; set; } = "before_sleep_cmd";
    public string AfterSleepCmd { get; set; } = "after_sleep_cmd";
}

public sealed class IdlePresetListenerFields
{
    public string Timeout { get; set; } = "timeout";
    public string OnTimeout { get; set; } = "on-timeout";
    public string OnResume { get; set; } = "on-resume";
}

[JsonSerializable(typeof(IdlePreset))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class IdlePresetContext : JsonSerializerContext;
