using Hypricing.Core.Infrastructure;

namespace Hypricing.Core.Services;

public sealed class XkbService(CliRunner cli)
{
    public async Task<string[]> GetLayoutsAsync(CancellationToken ct = default)
    {
        var output = await cli.RunAsync("localectl", "list-x11-keymap-layouts", ct);
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task<string[]> GetVariantsAsync(string layout, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(layout))
            return [];

        try
        {
            var output = await cli.RunAsync("localectl", $"list-x11-keymap-variants {layout}", ct);
            var variants = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            // Prepend empty entry so "no variant" is selectable
            return ["", .. variants];
        }
        catch
        {
            return [];
        }
    }
}
