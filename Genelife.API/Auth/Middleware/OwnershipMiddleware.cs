using Genelife.Messages.Commands;
using Microsoft.AspNetCore.Http;
using Wolverine;

namespace Genelife.Api.Auth.Middleware;

public class OwnershipMiddleware
{
    private readonly IHttpContextAccessor? httpContextAccessor;

    public OwnershipMiddleware(IHttpContextAccessor? httpContextAccessor = null)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    public async Task BeforeAsync(IMessageContext context)
    {
        var httpContext = httpContextAccessor?.HttpContext;
        if (httpContext == null)
            return;

        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            httpContext.Response.StatusCode = 401;
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsync("""{"message":"Authentication required"}""");
            httpContext.Response.Headers.ContentLength = httpContext.Response.Body.Length;
            return;
        }

        var playerId = httpContext.User.FindFirst(AuthClaimTypes.PlayerId)?.Value;
        var isAdmin = httpContext.User.IsInRole(AuthRoles.Admin);

        if (isAdmin)
            return;

        var message = context.Envelope?.Message;
        if (message is DeleteGenos deleteGenos)
        {
            if (!await OwnsGenos(deleteGenos.NetworkId, playerId))
                await WriteForbidden(httpContext);
        }
        else if (message is SetGenosAction setAction)
        {
            if (!await OwnsGenos(setAction.NetworkId, playerId))
                await WriteForbidden(httpContext);
        }
    }

    private static async Task WriteForbidden(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = 403;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsync("""{"message":"Access denied"}""");
    }

    private static Task<bool> OwnsGenos(ulong networkId, string? playerId)
    {
        return Task.FromResult(true);
    }
}