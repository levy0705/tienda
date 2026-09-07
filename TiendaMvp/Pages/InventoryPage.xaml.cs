using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class InventoryPage : ContentPage
{
    private readonly IProductCatalogService _catalog;
    private readonly IInventoryService _inventory;
    private readonly IAppFeedbackService _feedback;
    private readonly IPermissionService _permissions;
    private readonly IServiceProvider _services;
    private IReadOnlyList<Category> _categories = Array.Empty<Category>();

    public InventoryPage(IProductCatalogService catalog, IInventoryService inventory, IAppFeedbackService feedback, IPermissionService permissions, IServiceProvider services)
    {
        InitializeComponent();
        _catalog = catalog;
        _inventory = inventory;
        _feedback = feedback;
        _permissions = permissions;
        _services = services;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadCategories();
        LoadProducts();
        InventoryValuePanel.IsVisible = _permissions.Has(AppPermission.ViewCostsAndProfit);
        InventoryValueLabel.Text = $"${_inventory.GetEstimatedValue():N0}";
        LowStockLabel.Text = $"{_inventory.GetLowStockProducts().Count} productos";
    }

    private void LoadCategories()
    {
        _categories = _catalog.GetCategories();
        CategoryPicker.ItemsSource = new[] { new Category { Id = string.Empty, Name = "Todas las categorías" } }.Concat(_categories).ToList();
        CategoryPicker.ItemDisplayBinding = new Binding(nameof(Category.Name));
        if (CategoryPicker.SelectedIndex < 0)
            CategoryPicker.SelectedIndex = 0;
    }

    private void LoadProducts()
    {
        var selectedCategory = CategoryPicker.SelectedItem as Category;
        ProductsCollection.ItemsSource = _catalog.GetProducts(SearchBox.Text, selectedCategory?.Id, includeInactive: true);
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => LoadProducts();

    private void OnCategoryChanged(object? sender, EventArgs e) => LoadProducts();

    private async void OnNewProductClicked(object? sender, EventArgs e) => await OpenEditorAsync(null);

    private async void OnEditProductClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: Product product })
            await OpenEditorAsync(product.Id);
    }

    private async void OnAdjustProductClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Product product })
            return;

        await OpenAdjustAsync(product.Id);
    }

    private async void OnQrProductClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Product product })
            return;

        var page = _services.GetRequiredService<ProductQrPage>();
        page.InitializeForProduct(product);
        await Navigation.PushAsync(page);
    }

    private async Task OpenEditorAsync(string? productId)
    {
        var page = _services.GetRequiredService<ProductEditorPage>();
        page.InitializeForProduct(productId);
        await Navigation.PushAsync(page);
    }

    private async void OnCategoriesClicked(object? sender, EventArgs e)
    {
        var page = _services.GetRequiredService<CategoriesPage>();
        await Navigation.PushAsync(page);
    }

    private async void OnAdjustClicked(object? sender, EventArgs e) => await OpenAdjustAsync(null);

    private async void OnCountClicked(object? sender, EventArgs e)
    {
        var page = _services.GetRequiredService<InventoryCountPage>();
        page.InitializeForProduct(null);
        await Navigation.PushAsync(page);
    }

    private async void OnMovementsClicked(object? sender, EventArgs e)
    {
        var page = _services.GetRequiredService<InventoryMovementsPage>();
        page.InitializeForProduct(null);
        await Navigation.PushAsync(page);
    }

    private async Task OpenAdjustAsync(string? productId)
    {
        var page = _services.GetRequiredService<InventoryAdjustPage>();
        page.InitializeForProduct(productId);
        await Navigation.PushAsync(page);
    }

    private async void OnScanClicked(object? sender, EventArgs e)
    {
        var page = _services.GetRequiredService<QrScannerPage>();
        page.ProductDetected += OnProductDetected;
        await Navigation.PushAsync(page);
    }

    private async void OnProductDetected(object? sender, Product product)
    {
        if (sender is QrScannerPage scanner)
            scanner.ProductDetected -= OnProductDetected;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = _services.GetRequiredService<ProductEditorPage>();
            page.InitializeForProduct(product.Id);
            await Navigation.PushAsync(page);
        });
    }
}
