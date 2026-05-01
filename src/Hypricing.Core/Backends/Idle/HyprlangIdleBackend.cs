using System.Globalization;
using Hypricing.Core.Contracts;
using Hypricing.Core.Infrastructure;
using Hypricing.HyprlangParser;
using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.Core.Backends.Idle;

public sealed class HyprlangIdleBackend(CliRunner cli, IdlePreset preset) : IIdleBackend
{
    public string PresetName => preset.Name;

    public async Task<IdleConfig> GetConfigAsync(CancellationToken ct = default)
    {
        var path = ExpandPath(preset.ConfigPath);
        if (!File.Exists(path))
            return new IdleConfig(new IdleGeneral(null, null, null, null), []);

        var text = await File.ReadAllTextAsync(path, ct);
        var ast = HyprlangParser.HyprlangParser.Parse(text);

        var gf = preset.Fields.General;
        var lf = preset.Fields.Listener;

        IdleGeneral general = new(null, null, null, null);
        var listeners = new List<IdleListener>();

        foreach (var node in ast.Children)
        {
            if (node is not SectionNode section) continue;

            if (section.Name == "general")
            {
                var d = ReadAssignments(section);
                general = new IdleGeneral(
                    d.GetValueOrDefault(gf.LockCmd),
                    d.GetValueOrDefault(gf.UnlockCmd),
                    d.GetValueOrDefault(gf.BeforeSleepCmd),
                    d.GetValueOrDefault(gf.AfterSleepCmd));
            }
            else if (section.Name == "listener")
            {
                var d = ReadAssignments(section);
                var timeoutStr = d.GetValueOrDefault(lf.Timeout) ?? "0";
                int.TryParse(timeoutStr, CultureInfo.InvariantCulture, out var timeout);
                var onTimeout = d.GetValueOrDefault(lf.OnTimeout) ?? string.Empty;
                var onResume = d.GetValueOrDefault(lf.OnResume);
                listeners.Add(new IdleListener(timeout, onTimeout, onResume));
            }
        }

        return new IdleConfig(general, [.. listeners]);
    }

    public async Task SaveAsync(IdleConfig config, CancellationToken ct = default)
    {
        var path = ExpandPath(preset.ConfigPath);
        var dir = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var text = File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : string.Empty;
        var ast = HyprlangParser.HyprlangParser.Parse(text);

        var gf = preset.Fields.General;
        var lf = preset.Fields.Listener;

        // Upsert general block
        var generalSection = ast.Children.OfType<SectionNode>().FirstOrDefault(s => s.Name == "general");
        if (generalSection is null)
        {
            generalSection = new SectionNode("general");
            ast.Children.Insert(0, generalSection);
        }
        UpsertAssignment(generalSection, gf.LockCmd, config.General.LockCmd);
        UpsertAssignment(generalSection, gf.UnlockCmd, config.General.UnlockCmd);
        UpsertAssignment(generalSection, gf.BeforeSleepCmd, config.General.BeforeSleepCmd);
        UpsertAssignment(generalSection, gf.AfterSleepCmd, config.General.AfterSleepCmd);

        // Remove old listeners, add new ones
        ast.Children.RemoveAll(n => n is SectionNode s && s.Name == "listener");
        foreach (var listener in config.Listeners)
        {
            var section = new SectionNode("listener");
            section.Children.Add(new AssignmentNode(lf.Timeout, listener.Timeout.ToString(CultureInfo.InvariantCulture)));
            section.Children.Add(new AssignmentNode(lf.OnTimeout, listener.OnTimeout));
            if (listener.OnResume is not null)
                section.Children.Add(new AssignmentNode(lf.OnResume, listener.OnResume));
            ast.Children.Add(new EmptyLineNode());
            ast.Children.Add(section);
        }

        var output = HyprlangWriter.Write(ast);
        await File.WriteAllTextAsync(path, output, ct);
    }

    public async Task<bool> IsDaemonRunningAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await cli.RunAsync("systemctl", "--user is-active hypridle", ct);
            return result.Trim() == "active";
        }
        catch { return false; }
    }

    public async Task StartDaemonAsync(CancellationToken ct = default) =>
        await RunDaemonCommand(preset.Daemon.Start, ct);

    public async Task StopDaemonAsync(CancellationToken ct = default) =>
        await RunDaemonCommand(preset.Daemon.Stop, ct);

    public async Task RestartDaemonAsync(CancellationToken ct = default) =>
        await RunDaemonCommand(preset.Daemon.Restart, ct);

    private async Task RunDaemonCommand(string command, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(command)) return;
        var space = command.IndexOf(' ');
        var exe = space < 0 ? command : command[..space];
        var args = space < 0 ? string.Empty : command[(space + 1)..];
        try { await cli.RunAsync(exe, args, ct); } catch { }
    }

    private static Dictionary<string, string> ReadAssignments(SectionNode section)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var child in section.Children)
            if (child is AssignmentNode a)
                d[a.Key] = a.Value;
        return d;
    }

    private static void UpsertAssignment(SectionNode section, string key, string? value)
    {
        if (value is null) return;
        var existing = section.Children.OfType<AssignmentNode>().FirstOrDefault(a => a.Key == key);
        if (existing is not null)
            existing.Value = value;
        else
            section.Children.Add(new AssignmentNode(key, value));
    }

    private static string ExpandPath(string path) =>
        path.Replace("$XDG_CONFIG_HOME",
            Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"));
}
