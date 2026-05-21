using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using RagQnA.Contracts.Options;

namespace RagQnA.Api.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireMonitorKeyAttribute : Attribute, IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var options = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<MonitorOptions>>().Value;

        if (string.IsNullOrEmpty(options.ApiKey))
            return;

        if (!context.HttpContext.Request.Headers.TryGetValue("X-Monitor-Key", out var key)
            || key != options.ApiKey)
        {
            context.Result = new UnauthorizedObjectResult("Missing or invalid X-Monitor-Key header.");
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
