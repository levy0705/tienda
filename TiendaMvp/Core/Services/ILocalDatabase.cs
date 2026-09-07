using SQLite;

namespace TiendaMvp.Core.Services;

public interface ILocalDatabase
{
    string DatabasePath { get; }
    SQLiteConnection Connection { get; }
    int SchemaVersion { get; }
    void Initialize();
    void RunInTransaction(Action operation);
    void ReplaceDatabase(byte[] databaseBytes);
}
