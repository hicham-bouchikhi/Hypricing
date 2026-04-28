using System.IO.Compression;

namespace Hypricing.Core.Services;

/// <summary>
/// Manages zip backups of Hyprland config files.
/// Backups are stored in ~/.config/hypr/backups/.
/// </summary>
public class BackupService(string hyprConfigDir)
{
    private readonly string _backupDir = Path.Combine(hyprConfigDir, "backups");

    /// <summary>
    /// Lists all backup zip files, most recent first.
    /// </summary>
    public IReadOnlyList<BackupInfo> ListBackups()
    {
        if (!Directory.Exists(_backupDir))
            return [];

        return Directory.GetFiles(_backupDir, "hypricing-backup-*.zip")
            .Select(p => new FileInfo(p))
            .OrderByDescending(f => f.CreationTime)
            .Select(f => new BackupInfo(f.FullName, f.Name, f.CreationTime, f.Length))
            .ToList();
    }

    /// <summary>
    /// Restores a backup by extracting all files to the hypr config directory.
    /// Creates a backup of the current state before restoring.
    /// </summary>
    public void Restore(string zipPath, IReadOnlyList<string> currentConfigPaths)
    {
        var targetDir = Path.GetDirectoryName(currentConfigPaths[0])!;

        // Backup current state before restoring
        CreateBackup(currentConfigPaths);

        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            var destPath = Path.Combine(targetDir, entry.FullName);
            entry.ExtractToFile(destPath, overwrite: true);
        }
    }

    /// <summary>
    /// Creates a zip backup of the given config files.
    /// </summary>
    public string CreateBackup(IReadOnlyList<string> configPaths)
    {
        Directory.CreateDirectory(_backupDir);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        var zipPath = Path.Combine(_backupDir, $"hypricing-backup-{timestamp}.zip");

        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var path in configPaths)
            if (File.Exists(path))
                zip.CreateEntryFromFile(path, Path.GetFileName(path));

        return zipPath;
    }

    /// <summary>
    /// Deletes a backup zip file.
    /// </summary>
    public void Delete(string zipPath)
    {
        if (File.Exists(zipPath))
            File.Delete(zipPath);
    }
}

public record BackupInfo(string FullPath, string FileName, DateTime CreatedAt, long SizeBytes)
{
    public string SizeDisplay => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
        _ => $"{SizeBytes / (1024.0 * 1024.0):F1} MB",
    };
}
