using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class ProductQrPage : ContentPage
{
    private readonly IProductCatalogService _catalog;
    private readonly IQrCodeService _qrCodes;
    private readonly IAppFeedbackService _feedback;
    private Product? _product;
    private string? _imagePath;

    public ProductQrPage(IProductCatalogService catalog, IQrCodeService qrCodes, IAppFeedbackService feedback)
    {
        InitializeComponent();
        _catalog = catalog;
        _qrCodes = qrCodes;
        _feedback = feedback;
    }

    public void InitializeForProduct(Product product)
    {
        // Versiones anteriores usaban tienda://, un formato que algunos
        // lectores externos no aceptan. Se migra al formato HTTPS al mostrar
        // el QR y se conserva la referencia en la base local.
        if (string.IsNullOrWhiteSpace(product.QrValue)
            || product.QrValue.StartsWith("tienda://", StringComparison.OrdinalIgnoreCase))
        {
            product.QrValue = _qrCodes.CreateValue(product.Id);
            _catalog.SaveProduct(product);
        }

        _product = product;
        ProductNameLabel.Text = product.Name;
        ProductCodeLabel.Text = $"Código: {product.InternalCode}";
        _imagePath = null;
        QrImage.Source = null;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_product is null)
            return;

        try
        {
            _imagePath ??= await _qrCodes.GenerateImageAsync(_product.QrValue, _product.InternalCode);
            QrImage.Source = ImageSource.FromFile(_imagePath);
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo generar el QR", exception.Message);
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_imagePath))
            return;

        try
        {
            var savedDirectory = Path.Combine(FileSystem.AppDataDirectory, "saved-qrs");
            Directory.CreateDirectory(savedDirectory);
            var savedPath = Path.Combine(savedDirectory, $"producto-{_product?.InternalCode ?? "qr"}.png");
            File.Copy(_imagePath, savedPath, overwrite: true);
            await _feedback.ShowMessageAsync("QR guardado", "Se guardó una copia local en el teléfono.");
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo guardar", exception.Message);
        }
    }

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_imagePath) || !File.Exists(_imagePath))
            return;

        try
        {
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"QR de {_product?.Name}",
                File = new ShareFile(_imagePath, "image/png")
            });
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo compartir", exception.Message);
        }
    }

}
