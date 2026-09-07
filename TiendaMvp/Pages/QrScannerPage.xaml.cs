using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;
using ZXing.Net.Maui;

namespace TiendaMvp.Pages;

public partial class QrScannerPage : ContentPage
{
    private readonly IProductCatalogService _catalog;
    private readonly IAppFeedbackService _feedback;
    private bool _handled;

    public QrScannerPage(IProductCatalogService catalog, IAppFeedbackService feedback)
    {
        InitializeComponent();
        _catalog = catalog;
        _feedback = feedback;
    }

    public event EventHandler<Product>? ProductDetected;

    private async void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (_handled)
            return;

        var value = e.Results.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(value))
            return;

        _handled = true;
        var product = _catalog.FindByQr(value);
        if (product is null)
        {
            _handled = false;
            await MainThread.InvokeOnMainThreadAsync(() => _feedback.ShowMessageAsync("QR no reconocido", "No existe un producto activo asociado a este código."));
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await DisplayAlertAsync("Producto encontrado", product.Name, "Abrir ficha");
            await Navigation.PopAsync();
            ProductDetected?.Invoke(this, product);
        });
    }

    private async void OnCancelClicked(object? sender, EventArgs e) => await Navigation.PopAsync();
}
