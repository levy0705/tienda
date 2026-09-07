using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class SuppliersPage : ContentPage
{
    private readonly ISupplierService _suppliers;
    private readonly IAppFeedbackService _feedback;
    private readonly IServiceProvider _services;

    public SuppliersPage(ISupplierService suppliers, IAppFeedbackService feedback, IServiceProvider services)
    {
        InitializeComponent();
        _suppliers = suppliers;
        _feedback = feedback;
        _services = services;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadSuppliers();
    }

    private void LoadSuppliers() => SuppliersCollection.ItemsSource = _suppliers.GetSuppliers(SearchBox.Text, includeInactive: true);
    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => LoadSuppliers();

    private async void OnNewClicked(object? sender, EventArgs e) => await OpenEditorAsync(null);

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: Supplier supplier })
            await OpenEditorAsync(supplier.Id);
    }

    private async void OnPurchasesClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Supplier supplier })
            return;
        var page = _services.GetRequiredService<PurchasesPage>();
        page.InitializeForSupplier(supplier.Id);
        await Navigation.PushAsync(page);
    }

    private async void OnToggleClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Supplier supplier })
            return;
        var targetState = !supplier.IsActive;
        if (!await _feedback.ConfirmAsync(targetState ? "Activar proveedor" : "Desactivar proveedor", supplier.Name))
            return;
        try
        {
            _suppliers.SetSupplierActive(supplier.Id, targetState);
            LoadSuppliers();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo actualizar", exception.Message);
        }
    }

    private async Task OpenEditorAsync(string? supplierId)
    {
        var page = _services.GetRequiredService<SupplierEditorPage>();
        page.InitializeForSupplier(supplierId);
        await Navigation.PushAsync(page);
    }
}
