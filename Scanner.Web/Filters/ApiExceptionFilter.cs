using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Scanner.Web.Services;

namespace Scanner.Web.Filters;

public sealed class ApiExceptionFilter(ILogger<ApiExceptionFilter> logger) : IAsyncExceptionFilter
{
    public Task OnExceptionAsync(ExceptionContext context)
    {
        (int statusCode, string message) = context.Exception switch
        {
            ApiException exception => (exception.StatusCode, exception.Message),
            ArgumentException exception => (StatusCodes.Status400BadRequest, exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "服务器处理请求时发生错误。")
        };
        if (statusCode == StatusCodes.Status500InternalServerError)
            logger.LogError(context.Exception, "Unhandled API exception for {Path}", context.HttpContext.Request.Path);
        context.Result = new ObjectResult(new { message }) { StatusCode = statusCode };
        context.ExceptionHandled = true;
        return Task.CompletedTask;
    }
}
