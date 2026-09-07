using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class HomePage : ContentPage
{
    private static readonly TimeSpan BackupReminderInterval = TimeSpan.FromDays(7);
    private readonly IReportService _reports;
    private readonly IBackupService _backups;
    private readonly IPermissionService _permissions;
    private readonly IServiceProvider _services;

    public HomePage(IReportService reports, IBackupService backups, IPermissionService permissions, IServiceProvider services)
    {
        InitializeComponent();
        _reports = reports;
        _backups = backups;
        _permissions = permissions;
        _services = services;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var summary = _reports.GetDashboardSummary();
        SalesTodayLabel.Text = $"${summary.SalesToday:N0}";
        CollectionsTodayLabel.Text = $"${summary.CollectionsToday:N0}";
        ExpensesTodayLabel.Text = $"${summary.ExpensesToday:N0}";
        CashTodayLabel.Text = $"${summary.ExpectedCash:N0}";
        ReceivablesLabel.Text = $"${summary.PendingCredits:N0}";
        OutOfStockLabel.Text = summary.OutOfStockProducts.ToString();
        LowStockCount.Text = summary.LowStockProducts.ToString();
        LowStockSummary.Text = summary.LowStockProducts == 0
            ? "No hay productos bajo el mínimo"
            : $"{summary.LowStockProducts} producto(s) requieren reposición";
        OverdueCount.Text = summary.OverdueCreditCount.ToString();
        OverdueSummary.Text = summary.OverdueCredits <= 0
            ? "No hay créditos vencidos"
            : $"Saldo vencido: ${summary.OverdueCredits:N0}";
        TopProductsCollection.ItemsSource = summary.TopProducts;

        var latestBackup = _backups.GetBackups().FirstOrDefault();
        BackupReminderBorder.IsVisible = _permissions.Has(AppPermission.ManageBackups)
            && (latestBackup is null || DateTime.UtcNow - latestBackup.CreatedAtUtc > BackupReminderInterval);
        if (latestBackup is not null)
            BackupReminderLabel.Text = $"El último respaldo fue el {latestBackup.CreatedLabel}. Compártelo fuera del teléfono.";
    }

    private async void OnNewSaleClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//vender");

    private async void OnOpenCashClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//caja");

    private async void OnInventoryClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//inventario");

    private async void OnReportsClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<ReportsPage>());

    private async void OnBackupsClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<BackupsPage>());
}
