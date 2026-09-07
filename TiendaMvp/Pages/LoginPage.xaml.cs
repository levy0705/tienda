using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;

namespace TiendaMvp.Pages;

public partial class LoginPage : ContentPage
{
    private readonly IUserService _users;

    public LoginPage(IUserService users)
    {
        InitializeComponent();
        _users = users;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UserPicker.ItemsSource = _users.GetUsers(includeInactive: false).ToList();
        UserPicker.ItemDisplayBinding = new Binding(nameof(User.DisplayName));
        if (UserPicker.SelectedIndex < 0)
            UserPicker.SelectedIndex = 0;
        PinEntry.Text = string.Empty;
        ErrorLabel.Text = string.Empty;
        ErrorLabel.IsVisible = false;
    }

    private void OnLoginClicked(object? sender, EventArgs e)
    {
        ErrorLabel.Text = string.Empty;
        ErrorLabel.IsVisible = false;

        if (UserPicker.SelectedItem is not User user)
        {
            ShowLoginError("Selecciona un usuario activo.");
            return;
        }

        try
        {
            _users.Authenticate(user.Id, PinEntry.Text ?? string.Empty);
        }
        catch (Exception exception)
        {
            ShowLoginError(exception.Message);
            return;
        }

        try
        {
            if (Application.Current is App app)
                app.ShowMainShell();
            else
                ShowLoginError("No se pudo abrir la aplicación.");
        }
        catch (Exception exception)
        {
            // El cambio de LoginPage a AppShell ocurre durante la navegación de
            // la ventana. Mostrar el error dentro de esta página evita que un
            // diálogo modal intente usar una página que ya está cambiando.
            ShowLoginError($"No se pudo abrir la tienda: {exception.Message}");
        }
    }

    private void ShowLoginError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
