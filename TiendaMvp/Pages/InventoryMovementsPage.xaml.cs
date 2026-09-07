using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class InventoryMovementsPage : ContentPage
{
    private readonly IProductCatalogService _catalog;
    private readonly IInventoryService _inventory;
    private IReadOnlyList<Product> _products = Array.Empty<Product>();
    private string? _productId;

    public InventoryMovementsPage(IProductCatalogService catalog, IInventoryService inventory)
    {
        InitializeComponent();
        _catalog = catalog;
        _inventory = inventory;
    }

    public void InitializeForProduct(string? productId) => _productId = productId;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _products = _catalog.GetProducts(includeInactive: true);
        ProductPicker.ItemsSource = new[] { new Product { Id = string.Empty, Name = "Todos los productos" } }.Concat(_products).ToList();
        ProductPicker.ItemDisplayBinding = new Binding(nameof(Product.Name));
        ProductPicker.SelectedItem = _products.FirstOrDefault(product => product.Id == _productId) ?? ProductPicker.ItemsSource.Cast<Product>().First();
        LoadMovements();
    }

    private void OnProductChanged(object? sender, EventArgs e) => LoadMovements();

    private void LoadMovements()
    {
        var product = ProductPicker.SelectedItem as Product;
        MovementsCollection.ItemsSource = _inventory.GetMovements(product?.Id);
    }
}
