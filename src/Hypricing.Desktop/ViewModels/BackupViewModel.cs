using System.Collections.ObjectModel;
using System.Windows.Input;
using Hypricing.Core.Services;

namespace Hypricing.Desktop.ViewModels;

public sealed class BackupViewModel : ViewModelBase
{
    private readonly HyprlandService _service;

    public BackupViewModel(HyprlandService service)
    {
        _service = service;
        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync);
    }

    public ObservableCollection<BackupItemViewModel> Backups { get; } = [];

    public ICommand CreateBackupCommand { get; }

    public string? StatusMessage
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public void Refresh()
    {
        Backups.Clear();
        foreach (var info in _service.Backup.ListBackups())
            Backups.Add(new BackupItemViewModel(info, RestoreBackup, DeleteBackup));
    }

    private async Task CreateBackupAsync()
    {
        try
        {
            _service.Backup.CreateBackup(_service.ConfigPaths);
            Refresh();
            StatusMessage = "Backup created";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Backup failed: {ex.Message}";
        }
    }

    private async void RestoreBackup(BackupItemViewModel item)
    {
        try
        {
            _service.Backup.Restore(item.Info.FullPath, _service.ConfigPaths);
            await _service.LoadAsync();
            Refresh();
            StatusMessage = $"Restored {item.Info.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Restore failed: {ex.Message}";
        }
    }

    private void DeleteBackup(BackupItemViewModel item)
    {
        try
        {
            _service.Backup.Delete(item.Info.FullPath);
            Backups.Remove(item);
            StatusMessage = $"Deleted {item.Info.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete failed: {ex.Message}";
        }
    }
}

public sealed class BackupItemViewModel : ViewModelBase
{
    public BackupItemViewModel(
        BackupInfo info,
        Action<BackupItemViewModel> onRestore,
        Action<BackupItemViewModel> onDelete)
    {
        Info = info;
        RestoreCommand = new RelayCommand(() => onRestore(this));
        DeleteCommand = new RelayCommand(() => onDelete(this));
    }

    public BackupInfo Info { get; }
    public ICommand RestoreCommand { get; }
    public ICommand DeleteCommand { get; }
}
