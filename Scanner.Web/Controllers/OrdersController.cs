using Microsoft.AspNetCore.Mvc;
using Scanner.Server.BLL;
using Scanner.Server.Model;
using Scanner.Web.Filters;

namespace Scanner.Web.Controllers;

[ApiController]
[Route("api/orders")]
[ApiKey]
public sealed class OrdersController(IOrderService orders) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Order>> Get(int id, CancellationToken cancellationToken)
    {
        Order? order = await orders.GetByIdAsync(id, cancellationToken);
        return order is null ? NotFound(new { message = $"没有找到 ID 为 {id} 的订单。" }) : Ok(order);
    }

    [HttpPost]
    public async Task<ActionResult<Order>> Create([FromBody] SaveOrderRequest request, CancellationToken cancellationToken)
    {
        Order order = await orders.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveOrderRequest request, CancellationToken cancellationToken)
    {
        return await orders.UpdateAsync(id, request, cancellationToken)
            ? Ok(new { id, message = "修改成功。" })
            : NotFound(new { message = $"没有找到 ID 为 {id} 的订单。" });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        return await orders.DeleteAsync(id, cancellationToken)
            ? Ok(new { id, message = "删除成功。" })
            : NotFound(new { message = $"没有找到 ID 为 {id} 的订单。" });
    }
}
