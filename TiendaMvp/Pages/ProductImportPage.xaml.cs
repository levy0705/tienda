using System.Text;
using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class ProductImportPage : ContentPage
{
    private readonly IProductImportService _importer;
    private readonly IAppFeedbackService _feedback;
    private ProductImportDocument? _document;

    public ProductImportPage(IProductImportService importer, IAppFeedbackService feedback)
    {
        InitializeComponent();
        _importer = importer;
        _feedback = feedback;
    }

    private async void OnTemplateClicked(object? sender, EventArgs e)
    {
        try
        {
            var path = Path.Combine(FileSystem.CacheDirectory, "productos-plantilla.csv");
            await File.WriteAllTextAsync(path, _importer.CreateTemplateCsv(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Compartir plantilla de productos",
                File = new ShareFile(path, "text/csv")
            });
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo compartir la plantilla", exception.Message);
        }
    }

    private async void OnSelectFileClicked(object? sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Selecciona la plantilla CSV"
            });
            if (file is null)
                return;

            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var content = await reader.ReadToEndAsync();
            _document = _importer.ParseCsv(content);

            FileLabel.Text = file.FileName;
            RowsCollection.ItemsSource = _document.Rows;
            ImportButton.IsVisible = _document.IsValid;
            SummaryLabel.Text = _document.Rows.Count == 0
                ? "No se encontraron productos."
                : $"{_document.Rows.Count} productos encontrados · {_document.ValidRows} listos para importar.";

            var errors = _document.Errors.Concat(_document.Rows.SelectMany(row => row.Errors.Select(error => $"Fila {row.LineNumber}: {error}"))).ToList();
            ErrorLabel.Text = string.Join("\n", errors.Take(12));
            ErrorLabel.IsVisible = errors.Count > 0;
            if (errors.Count > 12)
                ErrorLabel.Text += $"\nY {errors.Count - 12} errores más.";
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo leer el archivo", exception.Message);
        }
    }

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        if (_document is null || !_document.IsValid)
            return;

        if (!await _feedback.ConfirmAsync("Importar productos", $"Se crearán {_document.Rows.Count} productos. Esta operación no se puede deshacer automáticamente."))
            return;

        try
        {
            ImportButton.IsEnabled = false;
            var result = await Task.Run(() => _importer.Import(_document.Rows));
            await _feedback.ShowMessageAsync(
                "Importación completada",
                $"Se crearon {result.CreatedCount} productos. " +
                $"{result.InitialInventoryCount} recibieron existencia inicial y sus códigos QR quedaron listos.");
            await Navigation.PopAsync();
        }
        catch (Exception exception)
        {
            ImportButton.IsEnabled = true;
            await _feedback.ShowMessageAsync("No se pudo importar", exception.Message);
        }
    }
}
