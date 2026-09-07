using SQLite;
using System.Text;
using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public sealed class LocalDatabase : ILocalDatabase
{
    private const int CurrentSchemaVersion = 9;
    private readonly object _sync = new();
    private SQLiteConnection _connection;
    private int _transactionDepth;

    public LocalDatabase()
    {
        DatabasePath = Path.Combine(FileSystem.AppDataDirectory, "tienda-mvp.db3");
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        _connection = new SQLiteConnection(
            DatabasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
    }

    public string DatabasePath { get; }
    public SQLiteConnection Connection => _connection;
    public int SchemaVersion => CurrentSchemaVersion;

    public void RunInTransaction(Action operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_sync)
        {
            // SQLite no admite transacciones anidadas. Si un servicio compuesto
            // ya inició una transacción, sus servicios internos participan en ella
            // para que una interrupción no deje datos a medias.
            if (_transactionDepth > 0)
            {
                operation();
                return;
            }

            _transactionDepth++;
            try
            {
                _connection.RunInTransaction(operation);
            }
            finally
            {
                _transactionDepth--;
            }
        }
    }

    public void ReplaceDatabase(byte[] databaseBytes)
    {
        ArgumentNullException.ThrowIfNull(databaseBytes);
        if (databaseBytes.Length < 16 || !Encoding.ASCII.GetString(databaseBytes, 0, 15).StartsWith("SQLite format 3", StringComparison.Ordinal))
            throw new InvalidOperationException("La base de datos del respaldo no es válida.");

        lock (_sync)
        {
            var temporaryPath = $"{DatabasePath}.restore-{Guid.NewGuid():N}.tmp";
            File.WriteAllBytes(temporaryPath, databaseBytes);
            try
            {
                _connection.Dispose();
                File.Move(temporaryPath, DatabasePath, true);
                _connection = CreateConnection();
                Initialize();
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }
    }

    private SQLiteConnection CreateConnection() => new(
        DatabasePath,
        SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);

    public void Initialize()
    {
        lock (_sync)
        {
            _connection.CreateTable<DatabaseMetadata>();
            _connection.CreateTable<Category>();
            _connection.CreateTable<Product>();
            _connection.CreateTable<User>();
            _connection.CreateTable<StoreSettings>();
            _connection.CreateTable<AuditEntry>();
            _connection.CreateTable<InventoryMovement>();
            _connection.CreateTable<Supplier>();
            _connection.CreateTable<Purchase>();
            _connection.CreateTable<PurchaseItem>();
            _connection.CreateTable<Customer>();
            _connection.CreateTable<Sale>();
            _connection.CreateTable<SaleItem>();
            _connection.CreateTable<SalePayment>();
            _connection.CreateTable<Credit>();
            _connection.CreateTable<CreditMovement>();
            _connection.CreateTable<CashSession>();
            _connection.CreateTable<CashMovement>();
            _connection.CreateTable<Expense>();

            var metadata = _connection.Find<DatabaseMetadata>("schema");
            if (metadata is null)
            {
                _connection.Insert(new DatabaseMetadata
                {
                    Id = "schema",
                    SchemaVersion = CurrentSchemaVersion,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            }
            else if (metadata.SchemaVersion < CurrentSchemaVersion)
            {
                ApplyMigrations(metadata.SchemaVersion);
                metadata.SchemaVersion = CurrentSchemaVersion;
                metadata.UpdatedAtUtc = DateTime.UtcNow;
                _connection.Update(metadata);
            }

            SeedInitialData();
        }
    }

    private void ApplyMigrations(int fromVersion)
    {
        // Las migraciones se aplican en orden, sin borrar datos existentes.
        if (fromVersion < 2)
        {
            _connection.Execute("ALTER TABLE products ADD COLUMN Description TEXT NOT NULL DEFAULT ''");
            _connection.Execute("ALTER TABLE products ADD COLUMN TaxRate REAL NOT NULL DEFAULT 0");
        }

        if (fromVersion < 3)
        {
            // Los productos existentes de la Fase 2 ya podían tener saldo, pero aún
            // no tenían historial. Se crea un movimiento de apertura para conservarlo.
            var productIdsWithMovements = _connection.Table<InventoryMovement>()
                .ToList()
                .Select(movement => movement.ProductId)
                .ToHashSet();
            foreach (var product in _connection.Table<Product>().ToList().Where(product => product.Stock > 0 && !productIdsWithMovements.Contains(product.Id)))
            {
                _connection.Insert(new InventoryMovement
                {
                    ProductId = product.Id,
                    MovementType = InventoryMovementTypes.Initial,
                    Quantity = product.Stock,
                    PreviousStock = 0,
                    NewStock = product.Stock,
                    Reason = "Inventario inicial migrado",
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        if (fromVersion < 4)
        {
            _connection.Execute("ALTER TABLE products ADD COLUMN LastPurchaseCost REAL NOT NULL DEFAULT 0");
        }

        if (fromVersion < 6)
        {
            AddColumnIfMissing("sales", "ReturnedAmount", "REAL NOT NULL DEFAULT 0");
            AddColumnIfMissing("sales", "ChangeAmount", "REAL NOT NULL DEFAULT 0");
        }

        if (fromVersion < 7)
        {
            AddColumnIfMissing("cash_sessions", "CountedAmount", "REAL NOT NULL DEFAULT 0");
            AddColumnIfMissing("cash_sessions", "Difference", "REAL NOT NULL DEFAULT 0");
            AddColumnIfMissing("cash_sessions", "ClosingObservations", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("cash_sessions", "PinConfirmed", "INTEGER NOT NULL DEFAULT 0");
        }

        if (fromVersion < 8)
            AddColumnIfMissing("cash_movements", "PaymentMethod", "TEXT NOT NULL DEFAULT ''");

        if (fromVersion < 9)
            AddColumnIfMissing("users", "LastAccessAtUtc", "TEXT NULL");
    }

    private void AddColumnIfMissing(string table, string column, string definition)
    {
        var columns = _connection.Query<SqliteColumn>($"PRAGMA table_info({table})");
        if (!columns.Any(item => string.Equals(item.Name, column, StringComparison.OrdinalIgnoreCase)))
            _connection.Execute($"ALTER TABLE {table} ADD COLUMN {column} {definition}");
    }

    private sealed class SqliteColumn
    {
        public string Name { get; set; } = string.Empty;
    }

    private void SeedInitialData()
    {
        if (_connection.Find<StoreSettings>("store") is null)
        {
            _connection.Insert(new StoreSettings
            {
                Id = "store",
                StoreName = "Mi tienda",
                CurrencyCode = "COP",
                CurrencySymbol = "$",
                IsConfigured = false
            });
        }

        if (!_connection.Table<User>().Any())
        {
            _connection.Insert(new User
            {
                DisplayName = "Administrador",
                Role = "Administrador",
                // PIN inicial del entorno de desarrollo. Debe cambiarse en la primera configuración.
                PinHash = PinHasher.Hash("1234")
            });
        }

        if (!_connection.Table<Category>().Any())
        {
            _connection.Insert(new Category { Name = "General", Description = "Categoría inicial" });
        }

        if (_connection.Find<Customer>(Customer.GeneralId) is null)
        {
            _connection.Insert(new Customer
            {
                Id = Customer.GeneralId,
                Name = "Cliente general",
                IsGeneral = true,
                IsBlocked = false
            });
        }
    }
}
