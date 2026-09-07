using System.Globalization;
using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class ProductEditorPage : ContentPage
{
    private readonly IProductCatalogService _catalog;
    private readonly IImageStorageService _images;
    private readonly IInventoryService _inventory;
    private readonly IAppFeedbackService _feedback;
    private readonly IPermissionService _permissions;
    private readonly IServiceProvider _services;
    private string? _productId;
    private Product? _product;
    private FileResult? _selectedPhoto;
    private bool _loaded;

    public ProductEditorPage(
        IProductCatalogService catalog,
        IImageStorageService images,
        IInventoryService inventory,
        IAppFeedbackService feedback,
        IPermissionService permissions,
        IServiceProvider services)
    {
        InitializeComponent();
        _catalog = catalog;
        _images = images;
        _inventory = inventory;
        _feedback = feedback;
        _permissions = permissions;
        _services = services;
        UnitPicker.ItemsSource = new[] { "unidad", "paquete", "caja" };
    }

    public void InitializeForProduct(string? productId)
    {
        _productId = productId;
        _loaded = false;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_loaded)
            LoadProduct();
    }

    private void LoadProduct()
    {
        _loaded = true;
        var categories = _catalog.GetCategories();
        CategoryPicker.ItemsSource = categories.ToList();
        CategoryPicker.ItemDisplayBinding = new Binding(nameof(Category.Name));

        _product = string.IsNullOrWhiteSpace(_productId)
            ? new Product()
            : _catalog.GetProduct(_productId) ?? new Product();

        PageTitle.Text = string.IsNullOrWhiteSpace(_productId) ? "Nuevo producto" : "Editar producto";
        NameEntry.Text = _product.Name;
        DescriptionEditor.Text = _product.Description;
        CodeEntry.Text = _product.InternalCode;
        UnitPicker.SelectedItem = _product.Unit;
        CategoryPicker.SelectedItem = categories.FirstOrDefault(category => category.Id == _product.CategoryId);
        PurchaseCostEntry.Text = FormatNumber(_product.PurchaseCost);
        SalePriceEntry.Text = FormatNumber(_product.SalePrice);
        MinimumStockEntry.Text = FormatNumber(_product.MinimumStock);
        TaxRateEntry.Text = FormatNumber(_product.TaxRate);
        PurchaseCostEntry.IsVisible = _permissions.Has(AppPermission.ViewCostsAndProfit);
        InitialStockEntry.IsVisible = string.IsNullOrWhiteSpace(_productId);
        if (!string.IsNullOrWhiteSpace(_product.ImagePath))
            ProductImage.Source = _images.GetAbsolutePath(_product.ImagePath);

        QrButton.IsVisible = !string.IsNullOrWhiteSpace(_productId);
        ToggleButton.IsVisible = !string.IsNullOrWhiteSpace(_productId);
        ToggleButton.Text = _product.IsActive ? "Desactivar producto" : "Activar producto";
    }

    private async void OnChoosePhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            var photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
            {
                SelectionLimit = 1
            });
            _selectedPhoto = photos.FirstOrDefault();
            if (_selectedPhoto is not null)
                ProductImage.Source = ImageSource.FromFile(_selectedPhoto.FullPath);
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo elegir la imagen", exception.Message);
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_product is null)
            return;

        try
        {
            var isNewProduct = string.IsNullOrWhiteSpace(_productId);
            var internalCode = CodeEntry.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(internalCode))
            {
                await _feedback.ShowMessageAsync("Código requerido", "El código interno es obligatorio.");
                CodeEntry.Focus();
                return;
            }

            _product.Name = NameEntry.Text?.Trim() ?? string.Empty;
            _product.Description = DescriptionEditor.Text?.Trim() ?? string.Empty;
            _product.InternalCode = internalCode;
            _product.Unit = UnitPicker.SelectedItem?.ToString() ?? string.Empty;
            _product.CategoryId = (CategoryPicker.SelectedItem as Category)?.Id ?? string.Empty;
            _product.PurchaseCost = ParseNumber(PurchaseCostEntry.Text);
            _product.SalePrice = ParseNumber(SalePriceEntry.Text);
            _product.MinimumStock = ParseNumber(MinimumStockEntry.Text);
            _product.TaxRate = ParseNumber(TaxRateEntry.Text);
            var initialStock = isNewProduct ? ParseNumber(InitialStockEntry.Text) : 0;
            if (isNewProduct)
                _product.Stock = 0;

            if (_selectedPhoto is not null)
            {
                var oldPath = _product.ImagePath;
                _product.ImagePath = await _images.SaveAsync(_selectedPhoto);
                if (!string.IsNullOrWhiteSpace(oldPath))
                    await _images.DeleteAsync(oldPath);
            }

            _catalog.SaveProduct(_product);
            _productId = _product.Id;
            if (isNewProduct && initialStock > 0)
                _inventory.RegisterInitialInventory(_product.Id, initialStock);
            QrButton.IsVisible = true;
            ToggleButton.IsVisible = true;
            ToggleButton.Text = _product.IsActive ? "Desactivar producto" : "Activar producto";
            await _feedback.ShowMessageAsync("Producto guardado", "El producto y su identificador QR están listos.");
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo guardar", exception.Message);
        }
    }

    private async void OnQrClicked(object? sender, EventArgs e)
    {
        if (_product is null || string.IsNullOrWhiteSpace(_product.QrValue))
            return;

        var page = _services.GetRequiredService<ProductQrPage>();
        page.InitializeForProduct(_product);
        await Navigation.PushAsync(page);
    }

    private async void OnToggleClicked(object? sender, EventArgs e)
    {
        if (_product is null)
            return;

        var action = _product.IsActive ? "Desactivar" : "Activar";
        if (!await _feedback.ConfirmAsync($"{action} producto", _product.Name))
            return;

        try
        {
            _catalog.SetProductActive(_product.Id, !_product.IsActive);
            _product.IsActive = !_product.IsActive;
            ToggleButton.Text = _product.IsActive ? "Desactivar producto" : "Activar producto";
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo actualizar", exception.Message);
        }
    }

    private static double ParseNumber(string? value) =>
        double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var number) ? number : 0;

    private static string FormatNumber(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);
}
