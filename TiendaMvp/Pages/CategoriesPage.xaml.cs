using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class CategoriesPage : ContentPage
{
    private readonly IProductCatalogService _catalog;
    private readonly IAppFeedbackService _feedback;

    public CategoriesPage(IProductCatalogService catalog, IAppFeedbackService feedback)
    {
        InitializeComponent();
        _catalog = catalog;
        _feedback = feedback;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadCategories();
    }

    private void LoadCategories() => CategoriesCollection.ItemsSource = _catalog.GetCategories(includeInactive: true);

    private async void OnNewClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("Nueva categoría", "Nombre");
        if (string.IsNullOrWhiteSpace(name))
            return;

        try
        {
            _catalog.SaveCategory(new Category { Name = name.Trim() });
            LoadCategories();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo guardar", exception.Message);
        }
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Category category })
            return;

        var name = await DisplayPromptAsync("Editar categoría", "Nombre", initialValue: category.Name);
        if (string.IsNullOrWhiteSpace(name))
            return;

        try
        {
            category.Name = name.Trim();
            _catalog.SaveCategory(category);
            LoadCategories();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo guardar", exception.Message);
        }
    }

    private async void OnToggleClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Category category })
            return;

        var targetState = !category.IsActive;
        if (!await _feedback.ConfirmAsync(targetState ? "Activar categoría" : "Desactivar categoría", category.Name))
            return;

        try
        {
            _catalog.SetCategoryActive(category.Id, targetState);
            LoadCategories();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo actualizar", exception.Message);
        }
    }
}
