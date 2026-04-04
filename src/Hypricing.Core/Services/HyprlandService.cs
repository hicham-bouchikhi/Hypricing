using Hypricing.Core.Infrastructure;
using Hypricing.HyprlangParser;
using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.Core.Services;

/// <summary>
/// Owns the hyprland.conf lifecycle: load, modify, save, reload.
/// Follows <c>source =</c> includes to load the full configuration.
/// </summary>
public class HyprlandService
{
    private readonly CliRunner _cli;
    private readonly List<LoadedConfig> _configs = [];
    private BackupService? _backup;

    public HyprlandService(CliRunner cli)
    {
        _cli = cli;
    }

    /// <summary>The backup service, available after loading.</summary>
    public BackupService Backup => _backup
        ?? throw new InvalidOperationException("Config not loaded. Call LoadAsync first.");

    /// <summary>Paths of all loaded config files.</summary>
    public IReadOnlyList<string> ConfigPaths => _configs.Select(c => c.FilePath).ToList();

    /// <summary>
    /// Loads and parses hyprland.conf and all source= includes.
    /// If <paramref name="path"/> is null, resolves via <see cref="ConfigFileLocator"/>.
    /// </summary>
    public async Task LoadAsync(string? path = null, CancellationToken ct = default)
    {
        var mainPath = path ?? ConfigFileLocator.Resolve()
            ?? throw new FileNotFoundException("Could not locate hyprland.conf");

        _configs.Clear();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        await LoadRecursiveAsync(mainPath, visited, ct);

        var hyprDir = Path.GetDirectoryName(_configs[0].FilePath)!;
        _backup = new BackupService(hyprDir);
    }

    /// <summary>
    /// Returns all $var declarations across all loaded config files.
    /// </summary>
    public IReadOnlyList<DeclarationNode> GetDeclarations()
    {
        EnsureLoaded();
        return AllNodes().OfType<DeclarationNode>().ToList();
    }

    /// <summary>
    /// Returns all <c>env = KEY,VALUE</c> keyword nodes across all loaded config files.
    /// </summary>
    public IReadOnlyList<KeywordNode> GetEnvironmentVariables()
    {
        EnsureLoaded();
        return AllNodes().OfType<KeywordNode>()
            .Where(k => k.Keyword == "env")
            .ToList();
    }

    /// <summary>
    /// Adds a new $var declaration to the main config file.
    /// </summary>
    public void AddDeclaration(string name, string value)
    {
        EnsureLoaded();
        _configs[0].Config.Children.Add(new DeclarationNode(name, value));
    }

    /// <summary>
    /// Removes a $var declaration from whichever config file contains it.
    /// </summary>
    public void RemoveDeclaration(DeclarationNode node)
    {
        EnsureLoaded();
        foreach (var loaded in _configs)
            if (loaded.Config.Children.Remove(node))
                return;
    }

    /// <summary>
    /// Adds a new env = KEY,VALUE entry. Placed in the first file that already has env entries,
    /// or the main config if none do.
    /// </summary>
    public void AddEnvironmentVariable(string key, string value)
    {
        EnsureLoaded();
        var target = _configs.FirstOrDefault(c =>
                         c.Config.Children.OfType<KeywordNode>().Any(k => k.Keyword == "env"))
                     ?? _configs[0];
        target.Config.Children.Add(new KeywordNode("env", $"{key},{value}"));
    }

    /// <summary>
    /// Removes an env entry from whichever config file contains it.
    /// </summary>
    public void RemoveEnvironmentVariable(KeywordNode node)
    {
        EnsureLoaded();
        foreach (var loaded in _configs)
            if (loaded.Config.Children.Remove(node))
                return;
    }

    /// <summary>
    /// Returns all bind/binde/bindm/… keyword nodes across all loaded config files.
    /// </summary>
    public IReadOnlyList<KeywordNode> GetKeybindings()
    {
        EnsureLoaded();
        return AllNodes().OfType<KeywordNode>()
            .Where(k => k.Keyword.StartsWith("bind"))
            .ToList();
    }

    /// <summary>
    /// Adds a new keybinding. Placed in the first file that already has bind entries,
    /// or the main config if none do.
    /// </summary>
    public void AddKeybinding(string variant, string @params)
    {
        EnsureLoaded();
        var target = _configs.FirstOrDefault(c =>
                         c.Config.Children.OfType<KeywordNode>().Any(k => k.Keyword.StartsWith("bind")))
                     ?? _configs[0];
        target.Config.Children.Add(new KeywordNode(variant, @params));
    }

    /// <summary>
    /// Removes a keybinding from whichever config file contains it.
    /// </summary>
    public void RemoveKeybinding(KeywordNode node)
    {
        EnsureLoaded();
        foreach (var loaded in _configs)
            if (loaded.Config.Children.Remove(node))
                return;
    }

    /// <summary>
    /// Returns all monitor keyword nodes across all loaded config files.
    /// </summary>
    public IReadOnlyList<KeywordNode> GetMonitors()
    {
        EnsureLoaded();
        return AllNodes().OfType<KeywordNode>()
            .Where(k => k.Keyword == "monitor")
            .ToList();
    }

    /// <summary>
    /// Adds a new monitor entry. Placed in the first file that already has monitor entries,
    /// or the main config if none do.
    /// </summary>
    public void AddMonitor(string @params)
    {
        EnsureLoaded();
        var target = _configs.FirstOrDefault(c =>
                         c.Config.Children.OfType<KeywordNode>().Any(k => k.Keyword == "monitor"))
                     ?? _configs[0];
        target.Config.Children.Add(new KeywordNode("monitor", @params));
    }

    /// <summary>
    /// Removes a monitor entry from whichever config file contains it.
    /// </summary>
    public void RemoveMonitor(KeywordNode node)
    {
        EnsureLoaded();
        foreach (var loaded in _configs)
            if (loaded.Config.Children.Remove(node))
                return;
    }

    /// <summary>
    /// Returns all exec entries across all loaded config files.
    /// </summary>
    public IReadOnlyList<ExecNode> GetExecEntries()
    {
        EnsureLoaded();
        return AllNodes().OfType<ExecNode>().ToList();
    }

    /// <summary>
    /// Adds a new exec entry. Placed in the first file that already contains exec entries,
    /// or the main config if none do.
    /// </summary>
    public void AddExecEntry(ExecVariant variant, string command, string? rules = null)
    {
        EnsureLoaded();
        var target = _configs.FirstOrDefault(c => c.Config.Children.OfType<ExecNode>().Any())
                     ?? _configs[0];
        target.Config.Children.Add(new ExecNode(variant, command, rules));
    }

    /// <summary>
    /// Removes an exec entry from whichever config file contains it.
    /// </summary>
    public void RemoveExecEntry(ExecNode node)
    {
        EnsureLoaded();
        foreach (var loaded in _configs)
        {
            if (loaded.Config.Children.Remove(node))
                return;
        }
    }

    /// <summary>
    /// Writes back all modified config files and invokes hyprctl reload.
    /// Re-parses afterward to refresh Range references.
    /// </summary>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        EnsureLoaded();

        _backup!.CreateBackup(ConfigPaths);

        for (int i = 0; i < _configs.Count; i++)
        {
            var loaded = _configs[i];
            var text = HyprlangWriter.Write(loaded.Config);
            await File.WriteAllTextAsync(loaded.FilePath, text, ct);
            _configs[i] = new LoadedConfig(loaded.FilePath, HyprlangParser.HyprlangParser.Parse(text));
        }

        await _cli.RunAsync("hyprctl", "reload", ct);
    }

    private async Task LoadRecursiveAsync(string path, HashSet<string> visited, CancellationToken ct)
    {
        var fullPath = Path.GetFullPath(ExpandHome(path));

        if (!visited.Add(fullPath))
            return; // circular include protection

        if (!File.Exists(fullPath))
            return; // missing source file — skip silently like Hyprland does

        var text = await File.ReadAllTextAsync(fullPath, ct);
        var config = HyprlangParser.HyprlangParser.Parse(text);
        _configs.Add(new LoadedConfig(fullPath, config));

        // Resolve source= includes relative to this file's directory
        var baseDir = Path.GetDirectoryName(fullPath)!;
        foreach (var source in config.Children.OfType<SourceNode>())
        {
            var includePath = source.Path;
            if (!Path.IsPathRooted(ExpandHome(includePath)))
                includePath = Path.Combine(baseDir, includePath);

            await LoadRecursiveAsync(includePath, visited, ct);
        }
    }

    private IEnumerable<SyntaxNode> AllNodes()
    {
        foreach (var loaded in _configs)
            foreach (var node in loaded.Config.Children)
                yield return node;
    }

    private void EnsureLoaded()
    {
        if (_configs.Count == 0)
            throw new InvalidOperationException("Config not loaded. Call LoadAsync first.");
    }

    private static string ExpandHome(string path)
    {
        if (path.StartsWith('~'))
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                path[1..].TrimStart('/'));
        return path;
    }
}

internal record LoadedConfig(string FilePath, ConfigNode Config);
