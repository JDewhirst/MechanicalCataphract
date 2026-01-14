using System.Collections.Generic;
using System.Threading.Tasks;

namespace MechanicalCataphract.Services;

public interface IEntityService<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IList<T>> GetAllAsync();
    Task<T> CreateAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
}
