using Scanner.Server.DAL;
using Scanner.Server.Model;

namespace Scanner.Server.BLL;

public sealed class OrderService(IOrderRepository repository) : IOrderService
{
    public Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => repository.GetByIdAsync(ValidateId(id), cancellationToken);

    public Task<Order> CreateAsync(SaveOrderRequest request, CancellationToken cancellationToken = default) => repository.CreateAsync(Validate(request), cancellationToken);

    public Task<bool> UpdateAsync(int id, SaveOrderRequest request, CancellationToken cancellationToken = default) => repository.UpdateAsync(ValidateId(id), Validate(request), cancellationToken);

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) => repository.DeleteAsync(ValidateId(id), cancellationToken);

    private static int ValidateId(int id) => id > 0 ? id : throw new ArgumentException("ID 必须大于 0。");

    private static SaveOrderRequest Validate(SaveOrderRequest? request)
    {
        if (request is null) throw new ArgumentException("订单内容不能为空。");
        return new SaveOrderRequest(
            Required(request.Dtype, "dtype"),
            Required(request.Sn, "sn"),
            Required(request.Doid, "doid"),
            request.Dtime == default ? throw new ArgumentException("dtime 不能为空。") : request.Dtime,
            Required(request.Dnote, "dnote"));
    }

    private static string Required(string? value, string name)
    {
        string result = value?.Trim() ?? string.Empty;
        if (result.Length == 0) throw new ArgumentException($"{name} 不能为空。");
        if (result.Length > 50) throw new ArgumentException($"{name} 不能超过 50 个字符。");
        return result;
    }
}
