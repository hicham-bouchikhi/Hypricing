using System.IO.Compression;
using Hypricing.Core.Services;

namespace Hypricing.Core.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _tempDir;

    public BackupServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"hypricing-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ListBackups_ReturnsEmptyWhenNoDirExists()
    {
        var service = new BackupService(_tempDir);

        var backups = service.ListBackups();

        Assert.Empty(backups);
    }

    [Fact]
    public void CreateBackup_CreatesZipWithConfigFiles()
    {
        var configPath = Path.Combine(_tempDir, "hyprland.conf");
        File.WriteAllText(configPath, "$myvar = SUPER\n");
        var service = new BackupService(_tempDir);

        var zipPath = service.CreateBackup([configPath]);

        Assert.True(File.Exists(zipPath));
        using var zip = ZipFile.OpenRead(zipPath);
        Assert.Single(zip.Entries);
        Assert.Equal("hyprland.conf", zip.Entries[0].Name);
    }

    [Fact]
    public void CreateBackup_SkipsMissingFiles()
    {
        var existing = Path.Combine(_tempDir, "hyprland.conf");
        File.WriteAllText(existing, "content\n");
        var missing = Path.Combine(_tempDir, "nonexistent.conf");
        var service = new BackupService(_tempDir);

        var zipPath = service.CreateBackup([existing, missing]);

        using var zip = ZipFile.OpenRead(zipPath);
        Assert.Single(zip.Entries);
    }

    [Fact]
    public void ListBackups_ReturnsCreatedBackup()
    {
        var configPath = Path.Combine(_tempDir, "hyprland.conf");
        File.WriteAllText(configPath, "content\n");
        var service = new BackupService(_tempDir);
        service.CreateBackup([configPath]);

        var backups = service.ListBackups();

        Assert.Single(backups);
        Assert.StartsWith("hypricing-backup-", backups[0].FileName);
    }

    [Fact]
    public void Delete_RemovesZipFile()
    {
        var configPath = Path.Combine(_tempDir, "hyprland.conf");
        File.WriteAllText(configPath, "content\n");
        var service = new BackupService(_tempDir);
        var zipPath = service.CreateBackup([configPath]);

        service.Delete(zipPath);

        Assert.False(File.Exists(zipPath));
    }

    [Fact]
    public void Restore_ExtractsFilesToConfigDir()
    {
        var configPath = Path.Combine(_tempDir, "hyprland.conf");
        File.WriteAllText(configPath, "original content\n");

        // Build a zip manually with a fixed old name so the pre-restore backup
        // created internally by Restore() gets a fresh (current) timestamp.
        var backupDir = Path.Combine(_tempDir, "backups");
        Directory.CreateDirectory(backupDir);
        var backupZip = Path.Combine(backupDir, "hypricing-backup-2000-01-01_000000.zip");
        using (var zip = ZipFile.Open(backupZip, ZipArchiveMode.Create))
            zip.CreateEntryFromFile(configPath, Path.GetFileName(configPath));

        File.WriteAllText(configPath, "modified content\n");
        var service = new BackupService(_tempDir);

        service.Restore(backupZip, [configPath]);

        Assert.Equal("original content\n", File.ReadAllText(configPath));
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(2048, "2.0 KB")]
    [InlineData(2 * 1024 * 1024, "2.0 MB")]
    public void BackupInfo_SizeDisplay_FormatsCorrectly(long bytes, string expected)
    {
        var info = new BackupInfo("/tmp/test.zip", "test.zip", DateTime.Now, bytes);

        Assert.Equal(expected, info.SizeDisplay);
    }
}
