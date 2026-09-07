using System.Globalization;
using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class InventoryCountPage : ContentPage
{
    private readonly IProductCatalogService _catalog;
    private readonly IInventoryService _inventory;
    private readonly IAppFeedbackService _feedback;
    private string? _productId;

    public InventoryCountPage(IProductCatalogService catalog, IInventoryService inventory, IAppFeedbackService feedback)
    {
        InitializeComponent();
        _catalog = catalog;
        _inventory = inventory;
        _feedback = feedback;
    }

    public void InitializeForProduct(string? productId) => _productId = productId;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var products = _catalog.GetProducts().ToList();
        ProductPicker.ItemsSource = products;
        ProductPicker.ItemDisplayBinding = new Binding(nameof(Product.Name));
        ProductPicker.SelectedItem = products.FirstOrDefault(product => product.Id == _productId);
        if (ProductPicker.SelectedIndex < 0 && products.Count > 0)
            ProductPicker.SelectedIndex = 0;
        UpdateStockLabels();
    }

    private void OnProductChanged(object? sender, EventArgs e) => UpdateStockLabels();

    private void OnCountedChanged(object? sender, TextChangedEventArgs e) => UpdateStockLabels();

    private void UpdateStockLabels()
    {
        if (ProductPicker.SelectedItem is not Product product)
        {
            RegisteredStockLabel.Text = "—";
            DifferenceLabel.Text = "—";
            return;
        }

        RegisteredStockLabel.Text = $"{product.Stock:0.##}";
        var counted = ParseNumber(CountedQuantityEntry.Text);
        DifferenceLabel.Text = $"{counted - product.Stock:+0.##;-0.##;0}";
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (ProductPicker.SelectedItem is not Product product)
        {
            await _feedback.ShowMessageAsync("Producto requerido", "Selecciona un producto.");
            return;
        }

        try
        {
            var reason = string.IsNullOrWhiteSpace(ReasonEditor.Text) ? "Corrección de conteo" : ReasonEditor.Text.Trim();
            _inventory.RegisterPhysicalCount(product.Id, ParseNumber(CountedQuantityEntry.Text), reason);
            await _feedback.ShowMessageAsync("Conteo guardado", "La diferencia quedó registrada en el historial.");
            await Navigation.PopAsync();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo guardar", exception.Message);
        }
    }

    private static double ParseNumber(string? value) =>
        double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var number) ? number : 0;
}
