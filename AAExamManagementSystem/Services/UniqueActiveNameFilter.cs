using AAExamManagementSystem.Models.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AAExamManagementSystem.Services;

/// <summary>
/// Applies the unique active name rule to an API create or update: reads Name (and IsActive, when the body has it)
/// from the request body and the record being edited from the "id" route value.
/// </summary>
public class UniqueActiveNameFilter : IAsyncActionFilter
{
    private readonly ActiveNameValidator _validator;
    private readonly NamedEntityKind _kind;

    public UniqueActiveNameFilter(ActiveNameValidator validator, NamedEntityKind kind)
    {
        _validator = validator;
        _kind = kind;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var body = context.ActionArguments.Values.FirstOrDefault(v => v?.GetType().GetProperty("Name") is not null);
        if (body is not null)
        {
            var name = body.GetType().GetProperty("Name")!.GetValue(body) as string;
            var active = body.GetType().GetProperty("IsActive")?.GetValue(body) as bool? ?? true;
            var excludeId = context.RouteData.Values["id"]?.ToString();

            if (active && !await _validator.IsAvailableAsync(_kind, name, excludeId))
            {
                context.ModelState.AddModelError("Name", ActiveNameValidator.Message(_kind, ActiveNameValidator.Normalize(name)));
                context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
                return;
            }
        }

        await next();
    }
}
