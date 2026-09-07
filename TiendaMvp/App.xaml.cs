using TiendaMvp.Core.Entities;
using TiendaMvp.Core.Services;
using TiendaMvp.Pages;

namespace TiendaMvp;

public partial class App : Application
{
    private readonly LoginPage _loginPage;
    private readonly IUserSession _session;
    private readonly IServiceProvider _services;
    private AppShell? _shell;

    public App(LoginPage loginPage, ILocalDatabase database, IUserSession session, IServiceProvider services)
    {
        InitializeComponent();
        _loginPage = loginPage;
        _session = session;
        _services = services;
        database.Initialize();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_loginPage);
    }

    public void ShowMainShell()
    {
        var window = Windows.FirstOrDefault();
        if (window is not null)
        {
            // El Shell se crea cuando ya existe una ventana activa. Esto evita
            // que WinUI intente instalar sus componentes antes del primer login.
            _shell ??= _services.GetRequiredService<AppShell>();
            window.Page = _shell;
        }
    }

    public void ShowLogin()
    {
        _session.SignOut();
        var window = Windows.FirstOrDefault();
        if (window is not null)
            window.Page = _loginPage;
    }
}
