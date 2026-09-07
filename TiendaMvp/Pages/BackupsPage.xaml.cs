using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class BackupsPage : ContentPage
{
    private static readonly TimeSpan ReminderInterval = TimeSpan.FromDays(7);
    private readonly IBackupService _backups;
    private readonly IAppFeedbackService _feedback;
    private bool _busy;

    public BackupsPage(IBackupService backups, IAppFeedbackService feedback)
    {
        InitializeComponent();
        _backups = backups;
        _feedback = feedback;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    private void Refresh()
    {
        var backups = _backups.GetBackups();
        BackupsCollection.ItemsSource = backups;
        var latest = backups.FirstOrDefault();
        var needsReminder = latest is null || DateTime.UtcNow - latest.CreatedAtUtc > ReminderInterval;
        ReminderBorder.IsVisible = needsReminder;
        if (latest is not null && needsReminder)
            ReminderLabel.Text = $"El último respaldo fue el {latest.CreatedLabel}. Compártelo fuera del teléfono para proteger el negocio.";
        else
            ReminderLabel.Text = "Aún no hay un respaldo reciente. Crea uno y compártelo fuera del teléfono.";
    }

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        if (_busy)
            return;
        var pin = await AskAdministratorPinAsync();
        if (pin is null)
            return;

        try
        {
            SetBusy(true);
            var backup = await _backups.CreateBackupAsync(pin);
            await _feedback.ShowMessageAsync("Copia creada", $"Se creó {backup.FileName} ({backup.SizeLabel}).");
            if (await _feedback.ConfirmAsync("Protege tu copia", "¿Deseas compartirla o guardarla fuera del teléfono ahora?", "Compartir", "Más tarde"))
                await ShareBackupAsync(backup);
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo crear la copia", exception.Message);
        }
        finally
        {
            SetBusy(false);
            Refresh();
        }
    }

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: BackupInfo backup })
            return;
        await ShareBackupAsync(backup);
    }

    private async void OnRestoreClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: BackupInfo backup })
            await ValidateAndRestoreAsync(backup.FilePath);
    }

    private async void OnPickRestoreClicked(object? sender, EventArgs e)
    {
        if (_busy)
            return;
        string? importedPath = null;
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Selecciona un respaldo de Mi tienda"
            });
            if (file is not null)
            {
                importedPath = await ImportPickedBackupAsync(file);
                await ValidateAndRestoreAsync(importedPath);
            }
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo seleccionar el archivo", exception.Message);
        }
        finally
        {
            if (importedPath is not null && File.Exists(importedPath))
                File.Delete(importedPath);
        }
    }

    private async Task ValidateAndRestoreAsync(string filePath)
    {
        if (_busy)
            return;
        var pin = await AskAdministratorPinAsync();
        if (pin is null)
            return;

        try
        {
            SetBusy(true);
            var validation = await _backups.ValidateAsync(filePath, pin);
            if (!validation.IsValid)
            {
                await _feedback.ShowMessageAsync("Respaldo no válido", validation.Message);
                return;
            }

            var backupDate = validation.Backup?.CreatedLabel ?? "la fecha indicada";
            var confirmed = await _feedback.ConfirmAsync(
                "Confirmar restauración",
                $"El respaldo del {backupDate} reemplazará los datos actuales. Antes se creará una copia preventiva y luego se cerrará la sesión. ¿Deseas continuar?",
                "Restaurar",
                "Cancelar");
            if (!confirmed)
                return;

            var result = await _backups.RestoreAsync(filePath, pin);
            await _feedback.ShowMessageAsync("Restauración completada", $"Se recuperaron los datos y {result.ImageCount} imagen(es). La copia preventiva quedó guardada en este teléfono.");
            if (Application.Current is App app)
                app.ShowLogin();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo restaurar", exception.Message);
        }
        finally
        {
            SetBusy(false);
            Refresh();
        }
    }

    private async Task<string?> AskAdministratorPinAsync() =>
        await DisplayPromptAsync("PIN de administrador", "Escribe el PIN de un administrador. Para restaurar, usa el PIN que protegió la copia.", keyboard: Keyboard.Numeric);

    private static async Task<string> ImportPickedBackupAsync(FileResult file)
    {
        var directory = FileSystem.CacheDirectory;
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"tienda-restore-{Guid.NewGuid():N}.tbackup");
        await using var source = await file.OpenReadAsync();
        await using var destination = File.Create(path);
        await source.CopyToAsync(destination);
        return path;
    }

    private async Task ShareBackupAsync(BackupInfo backup)
    {
        try
        {
            if (!File.Exists(backup.FilePath))
            {
                await _feedback.ShowMessageAsync("Archivo no encontrado", "Crea una nueva copia para poder compartirla.");
                Refresh();
                return;
            }
            await Share.Default.RequestAsync(new ShareFileRequest("Copia de seguridad de Mi tienda", new ShareFile(backup.FilePath)));
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo compartir", exception.Message);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        CreateButton.IsEnabled = !busy;
        BackupsCollection.IsEnabled = !busy;
    }
}
