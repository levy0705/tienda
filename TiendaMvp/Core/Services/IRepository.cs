namespace TiendaMvp.Core.Services;

public interface IRepository<T> where T : new()
{
    IReadOnlyList<T> GetAll();
    T? GetById(string id);
    void Insert(T entity);
    void Update(T entity);
    void Delete(T entity);
}
