using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class PurchasesPage : ContentPage
{
    private readonly IPurchaseService _purchases;
    private readonly IServiceProvider _services;
    private string? _supplierFilter;

    public PurchasesPage(IPurchaseService purchases, IServiceProvider services)
    {
        InitializeComponent();
        _purchases = purchases;
        _services = services;
        StatusPicker.ItemsSource = new[] { "Todos" }.Concat(PurchaseStatuses.All).ToList();
        StatusPicker.SelectedIndex = 0;
    }

    public void InitializeForSupplier(string? supplierId) => _supplierFilter = supplierId;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadPurchases();
    }

    private void LoadPurchases()
    {
        var status = StatusPicker.SelectedItem?.ToString();
        PurchasesCollection.ItemsSource = _purchases.GetPurchases(_supplierFilter, status == "Todos" ? null : status, SearchBox.Text);
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => LoadPurchases();
    private void OnStatusChanged(object? sender, EventArgs e) => LoadPurchases();
    private async void OnNewClicked(object? sender, EventArgs e) => await OpenEditorAsync(null);

    private async void OnOpenClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: Purchase purchase })
            await OpenEditorAsync(purchase.Id);
    }

    private async Task OpenEditorAsync(string? purchaseId)
    {
        var page = _services.GetRequiredService<PurchaseEditorPage>();
        page.InitializeForPurchase(purchaseId, _supplierFilter);
        await Navigation.PushAsync(page);
    }
}
