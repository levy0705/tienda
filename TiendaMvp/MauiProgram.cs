using Microsoft.Extensions.Logging;
using TiendaMvp.Core.Services;
using TiendaMvp.Pages;
using ZXing.Net.Maui.Controls;

namespace TiendaMvp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeReader()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        SQLitePCL.Batteries_V2.Init();

        builder.Services.AddSingleton<ILocalDatabase, LocalDatabase>();
        builder.Services.AddSingleton<IUserSession, UserSession>();
        builder.Services.AddSingleton<IPermissionService, PermissionService>();
        builder.Services.AddSingleton<IUserService, UserService>();
        builder.Services.AddSingleton(typeof(IRepository<>), typeof(SqliteRepository<>));
        builder.Services.AddSingleton<IAuditService, AuditService>();
        builder.Services.AddSingleton<IStoreSettingsService, StoreSettingsService>();
        builder.Services.AddSingleton<IImageStorageService, ImageStorageService>();
        builder.Services.AddSingleton<IAppFeedbackService, AppFeedbackService>();
        builder.Services.AddSingleton<IQrCodeService, QrCodeService>();
        builder.Services.AddSingleton<IProductCatalogService, ProductCatalogService>();
        builder.Services.AddSingleton<IInventoryService, InventoryService>();
        builder.Services.AddSingleton<ISupplierService, SupplierService>();
        builder.Services.AddSingleton<IPurchaseService, PurchaseService>();
        builder.Services.AddSingleton<ICustomerService, CustomerService>();
        builder.Services.AddSingleton<ICashService, CashService>();
        builder.Services.AddSingleton<IExpenseService, ExpenseService>();
        builder.Services.AddSingleton<IReportService, ReportService>();
        builder.Services.AddSingleton<ISaleService, SaleService>();
        builder.Services.AddSingleton<ICreditService, CreditService>();
        builder.Services.AddSingleton<IBackupService, BackupService>();

        builder.Services.AddSingleton<HomePage>();
        builder.Services.AddSingleton<SellPage>();
        builder.Services.AddSingleton<InventoryPage>();
        builder.Services.AddSingleton<CashPage>();
        builder.Services.AddSingleton<MorePage>();
        builder.Services.AddSingleton<LoginPage>();
        builder.Services.AddTransient<CategoriesPage>();
        builder.Services.AddTransient<InventoryAdjustPage>();
        builder.Services.AddTransient<InventoryCountPage>();
        builder.Services.AddTransient<InventoryMovementsPage>();
        builder.Services.AddTransient<ProductEditorPage>();
        builder.Services.AddTransient<ProductQrPage>();
        builder.Services.AddTransient<QrScannerPage>();
        builder.Services.AddTransient<SuppliersPage>();
        builder.Services.AddTransient<SupplierEditorPage>();
        builder.Services.AddTransient<PurchasesPage>();
        builder.Services.AddTransient<PurchaseEditorPage>();
        builder.Services.AddTransient<CustomersPage>();
        builder.Services.AddTransient<CustomerEditorPage>();
        builder.Services.AddTransient<CreditsPage>();
        builder.Services.AddTransient<SalesHistoryPage>();
        builder.Services.AddTransient<ExpensesPage>();
        builder.Services.AddTransient<ExpenseEditorPage>();
        builder.Services.AddTransient<ReportsPage>();
        builder.Services.AddTransient<BackupsPage>();
        builder.Services.AddTransient<UsersPage>();
        builder.Services.AddTransient<UserEditorPage>();
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
