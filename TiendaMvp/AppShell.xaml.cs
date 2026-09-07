using TiendaMvp.Pages;

namespace TiendaMvp;

public partial class AppShell : Shell
{
    public AppShell(
        HomePage homePage,
        SellPage sellPage,
        InventoryPage inventoryPage,
        CashPage cashPage,
        MorePage morePage)
    {
        InitializeComponent();

        var tabs = new TabBar();
        tabs.Items.Add(CreateTab("Inicio", "inicio", homePage));
        tabs.Items.Add(CreateTab("Vender", "vender", sellPage));
        tabs.Items.Add(CreateTab("Inventario", "inventario", inventoryPage));
        tabs.Items.Add(CreateTab("Caja", "caja", cashPage));
        tabs.Items.Add(CreateTab("Más", "mas", morePage));
        Items.Add(tabs);
    }

    private static Tab CreateTab(string title, string route, Page page) =>
        new()
        {
            Title = title,
            Route = route,
            Items =
            {
                new ShellContent
                {
                    Title = title,
                    Route = $"{route}-content",
                    Content = page
                }
            }
        };
}
