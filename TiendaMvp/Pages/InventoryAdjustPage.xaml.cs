using System.Globalization;
using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class InventoryAdjustPage : ContentPage
{
    private readonly IProductCatalogService _catalog;
    private readonly IInventoryService _inventory;
    private readonly IAppFeedbackService _feedback;
    private string? _productId;

    public InventoryAdjustPage(IProductCatalogService catalog, IInventoryService inventory, IAppFeedbackService feedback)
    {
        InitializeComponent();
        _catalog = catalog;
        _inventory = inventory;
        _feedback = feedback;
        MovementTypePicker.ItemsSource = new[]
        {
            InventoryMovementTypes.PositiveAdjustment,
            InventoryMovementTypes.NegativeAdjustment,
            InventoryMovementTypes.Damaged,
            InventoryMovementTypes.Loss
        };
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
        if (MovementTypePicker.SelectedIndex < 0)
            MovementTypePicker.SelectedIndex = 0;
        UpdateCurrentStock();
    }

    private void OnProductChanged(object? sender, EventArgs e) => UpdateCurrentStock();

    private void UpdateCurrentStock()
    {
        if (ProductPicker.SelectedItem is Product product)
            CurrentStockLabel.Text = $"Existencia actual: {product.Stock:0.##} {product.Unit}";
        else
            CurrentStockLabel.Text = "Existencia actual: —";
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (ProductPicker.SelectedItem is not Product product)
        {
            await _feedback.ShowMessageAsync("Producto requerido", "Selecciona un producto.");
            return;
        }

        var quantity = ParseNumber(QuantityEntry.Text);
        var reason = ReasonEditor.Text?.Trim() ?? string.Empty;
        var type = MovementTypePicker.SelectedItem?.ToString();

        try
        {
            if (type is InventoryMovementTypes.PositiveAdjustment)
                _inventory.RegisterAdjustment(product.Id, quantity, reason);
            else if (type is InventoryMovementTypes.NegativeAdjustment)
                _inventory.RegisterAdjustment(product.Id, -quantity, reason);
            else if (type is InventoryMovementTypes.Damaged)
                _inventory.RegisterDamaged(product.Id, quantity, reason);
            else if (type is InventoryMovementTypes.Loss)
                _inventory.RegisterLoss(product.Id, quantity, reason);
            else
                throw new InvalidOperationException("Selecciona un tipo de movimiento válido.");

            await _feedback.ShowMessageAsync("Movimiento guardado", "La existencia y el historial fueron actualizados.");
            await Navigation.PopAsync();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("Movimiento inválido", exception.Message);
        }
    }

    private static double ParseNumber(string? value) =>
        double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var number) ? number : 0;
}
