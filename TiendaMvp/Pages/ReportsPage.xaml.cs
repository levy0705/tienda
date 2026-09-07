using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class ReportsPage : ContentPage
{
    private readonly IReportService _reports;
    private readonly IPermissionService _permissions;

    public ReportsPage(IReportService reports, IPermissionService permissions)
    {
        InitializeComponent();
        _reports = reports;
        _permissions = permissions;
        ReportPicker.ItemsSource = ReportTypes.All.Where(IsVisibleReport).ToList();
        ReportPicker.SelectedIndex = 0;
        FromDatePicker.Date = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        ToDatePicker.Date = DateTime.Today;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    private void Refresh()
    {
        if (ReportPicker.SelectedItem is not string reportType)
            return;
        try
        {
            var from = FromDatePicker.Date ?? DateTime.Today;
            var to = ToDatePicker.Date ?? DateTime.Today;
            var result = _reports.GetReport(reportType, from, to);
            SummaryLabel.Text = result.Summary;
            ReportCollection.ItemsSource = result.Rows;
        }
        catch (Exception exception)
        {
            SummaryLabel.Text = exception.Message;
            ReportCollection.ItemsSource = Array.Empty<ReportRow>();
        }
    }

    private void OnReportChanged(object? sender, EventArgs e) => Refresh();
    private void OnRefreshClicked(object? sender, EventArgs e) => Refresh();

    private bool IsVisibleReport(string reportType) =>
        _permissions.Has(AppPermission.ViewCostsAndProfit)
            || reportType is not (ReportTypes.SalesByProduct or ReportTypes.GrossProfit or ReportTypes.CurrentInventory or ReportTypes.InventoryValue or ReportTypes.PurchasesBySupplier);
}
