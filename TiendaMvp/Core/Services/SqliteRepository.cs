using SQLite;

namespace TiendaMvp.Core.Services;

public sealed class SqliteRepository<T> : IRepository<T> where T : new()
{
    private readonly ILocalDatabase _database;

    public SqliteRepository(ILocalDatabase database)
    {
        _database = database;
    }

    public IReadOnlyList<T> GetAll() => _database.Connection.Table<T>().ToList();

    public T? GetById(string id) => _database.Connection.Find<T>(id);

    public void Insert(T entity) => _database.Connection.Insert(entity);

    public void Update(T entity) => _database.Connection.Update(entity);

    public void Delete(T entity) => _database.Connection.Delete(entity);
}
