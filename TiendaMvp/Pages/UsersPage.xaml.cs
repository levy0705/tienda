using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class UsersPage : ContentPage
{
    private readonly IUserService _users;
    private readonly IPermissionService _permissions;
    private readonly IAppFeedbackService _feedback;
    private readonly IServiceProvider _services;

    public UsersPage(IUserService users, IPermissionService permissions, IAppFeedbackService feedback, IServiceProvider services)
    {
        InitializeComponent();
        _users = users;
        _permissions = permissions;
        _feedback = feedback;
        _services = services;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _permissions.Require(AppPermission.ManageUsers);
            UsersCollection.ItemsSource = _users.GetUsers();
        }
        catch (Exception exception)
        {
            UsersCollection.ItemsSource = Array.Empty<User>();
            _ = _feedback.ShowMessageAsync("Acceso restringido", exception.Message);
        }
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        try
        {
            _permissions.Require(AppPermission.ManageUsers);
            var page = _services.GetRequiredService<UserEditorPage>();
            page.InitializeForUser(null);
            await Navigation.PushAsync(page);
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("Acceso restringido", exception.Message);
        }
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: User user })
            return;
        try
        {
            _permissions.Require(AppPermission.ManageUsers);
            var page = _services.GetRequiredService<UserEditorPage>();
            page.InitializeForUser(user.Id);
            await Navigation.PushAsync(page);
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("Acceso restringido", exception.Message);
        }
    }

    private async void OnToggleClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: User user })
            return;
        var activate = !user.IsActive;
        if (!await _feedback.ConfirmAsync(activate ? "Activar usuario" : "Bloquear usuario", user.DisplayName))
            return;
        try
        {
            _users.SetUserActive(user.Id, activate);
            UsersCollection.ItemsSource = _users.GetUsers();
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo actualizar", exception.Message);
        }
    }

    private async void OnChangePinClicked(object? sender, EventArgs e)
    {
        var current = await DisplayPromptAsync("Cambiar mi PIN", "PIN actual", keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(current))
            return;
        var next = await DisplayPromptAsync("Nuevo PIN", "Usa entre 4 y 8 dígitos", keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(next))
            return;
        var confirmation = await DisplayPromptAsync("Confirmar PIN", "Repite el nuevo PIN", keyboard: Keyboard.Numeric);
        if (next != confirmation)
        {
            await _feedback.ShowMessageAsync("PIN no coincide", "Los dos PIN deben ser iguales.");
            return;
        }
        try
        {
            _users.ChangeCurrentPin(current, next);
            await _feedback.ShowMessageAsync("PIN actualizado", "El nuevo PIN se usará en el próximo inicio.");
        }
        catch (Exception exception)
        {
            await _feedback.ShowMessageAsync("No se pudo cambiar", exception.Message);
        }
    }

    private async void OnSwitchUserClicked(object? sender, EventArgs e)
    {
        if (!await _feedback.ConfirmAsync("Cambiar usuario", "Se cerrará la sesión actual en este teléfono.", "Continuar", "Cancelar"))
            return;
        if (Application.Current is App app)
            app.ShowLogin();
    }
}
