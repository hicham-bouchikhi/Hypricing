using System.Globalization;
using Hypricing.Core.Infrastructure;

namespace Hypricing.Core.Services;

public sealed record BatteryInfo(
    bool Present,
    double Percentage,
    string State,
    string? TimeEstimate);

public sealed class PowerService(CliRunner cli)
{
    public async Task<(string[] Profiles, string Active)> GetProfilesAsync(CancellationToken ct = default)
    {
        var output = await cli.RunAsync("powerprofilesctl", "list", ct);
        return ParseProfiles(output);
    }

    public async Task SetProfileAsync(string profile, CancellationToken ct = default)
    {
        await cli.RunAsync("powerprofilesctl", $"set {profile}", ct);
    }

    public async Task<BatteryInfo?> GetBatteryInfoAsync(CancellationToken ct = default)
    {
        try
        {
            var devices = await cli.RunAsync("upower", "-e", ct);
            var battery = devices
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(d => d.Contains("battery", StringComparison.OrdinalIgnoreCase));
            if (battery is null) return null;

            var info = await cli.RunAsync("upower", $"-i {battery}", ct);
            return ParseBattery(info);
        }
        catch
        {
            return null;
        }
    }

    private static (string[] Profiles, string Active) ParseProfiles(string output)
    {
        // Each profile block starts with "* name:" (active) or "  name:" (inactive)
        var profiles = new List<string>();
        var active = string.Empty;

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (!trimmed.EndsWith(':')) continue;

            var isActive = line.TrimStart().StartsWith('*');
            var name = trimmed.TrimStart('*').Trim().TrimEnd(':');
            if (string.IsNullOrWhiteSpace(name)) continue;

            profiles.Add(name);
            if (isActive)
                active = name;
        }

        return (profiles.ToArray(), active);
    }

    private static BatteryInfo ParseBattery(string output)
    {
        var present = false;
        var percentage = 0.0;
        var state = string.Empty;
        string? timeEstimate = null;

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (TryGetValue(trimmed, "present:", out var v))
            {
                if (v == "yes") present = true;
            }
            else if (TryGetValue(trimmed, "state:", out v))
            {
                state = v;
            }
            else if (TryGetValue(trimmed, "percentage:", out v))
            {
                var pct = v.TrimEnd('%').Trim();
                if (double.TryParse(pct, CultureInfo.InvariantCulture, out var d))
                    percentage = d;
            }
            else if (TryGetValue(trimmed, "time to empty:", out v))
            {
                timeEstimate = v;
            }
            else if (TryGetValue(trimmed, "time to full:", out v))
            {
                timeEstimate = v;
            }
        }

        return new BatteryInfo(present, percentage, state, timeEstimate);
    }

    private static bool TryGetValue(string line, string key, out string value)
    {
        if (!line.StartsWith(key, StringComparison.Ordinal))
        {
            value = string.Empty;
            return false;
        }
        value = line[key.Length..].Trim();
        return true;
    }
}
