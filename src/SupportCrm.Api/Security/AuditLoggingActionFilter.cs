namespace SupportCrm.Api.Security;

using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using SupportCrm.Application.Security;

// Registered globally (Program.cs) — every mutating request across the WHOLE API is logged here,
// every prior module's controllers included, without any of them being touched. This is the
// ONLY code path anywhere that writes an AuditLogEntry — nothing else can create, edit, or
// delete one, which is what makes the entries read-only in practice, not just by convention.
public class AuditLoggingActionFilter(AuditLogService auditLogService) : IAsyncActionFilter
{
    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "DELETE", "PATCH" };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        await next(); // log regardless of outcome — a failed/denied mutating attempt is itself worth recording

        var request = context.HttpContext.Request;
        if (!MutatingMethods.Contains(request.Method)) return;

        var userIdClaim = context.HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var emailClaim = context.HttpContext.User.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        var userId = Guid.TryParse(userIdClaim, out var id) ? id : (Guid?)null;

        var actionDescriptor = context.ActionDescriptor as ControllerActionDescriptor;
        var summary = actionDescriptor is not null ? $"{actionDescriptor.ControllerName}.{actionDescriptor.ActionName}" : request.Path.ToString();
        var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString();

        await auditLogService.LogAsync(userId, emailClaim ?? "anonymous", request.Method, request.Path, summary, ip, context.HttpContext.RequestAborted);
    }
}
