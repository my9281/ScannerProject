using Scanner.Server.Model;

namespace Scanner.Server.DAL;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Order> CreateAsync(SaveOrderRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, SaveOrderRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
