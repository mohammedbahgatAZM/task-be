namespace SupportCrm.Api.Security;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SupportCrm.Application.Security;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequirePermissionAttribute(string module, string action) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Authentication required." });
            return;
        }

        var roleIds = context.HttpContext.User.FindAll(JwtTokenService.RoleIdClaimType)
            .Select(c => Guid.TryParse(c.Value, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        var permissionChecker = context.HttpContext.RequestServices.GetRequiredService<IPermissionChecker>();
        var allowed = await permissionChecker.HasPermissionAsync(roleIds, module, action, context.HttpContext.RequestAborted);
        if (!allowed)
        {
            context.Result = new ObjectResult(new { error = "You do not have permission to perform this action." }) { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        await next();
    }
}
