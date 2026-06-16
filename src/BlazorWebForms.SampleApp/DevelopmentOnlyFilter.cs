using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Hosting;

namespace BlazorWebForms.SampleApp;

public sealed class DevelopmentOnlyFilter(IHostEnvironment environment) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!environment.IsDevelopment())
        {
            context.Result = new NotFoundResult();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
