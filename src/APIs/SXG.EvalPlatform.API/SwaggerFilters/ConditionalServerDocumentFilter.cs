using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SXG.EvalPlatform.API.SwaggerFilters;

/// <summary>
/// For all environments: add server name based on incoming request host for URSA onboarding
/// </summary>
public class ConditionalServerDocumentFilter : IDocumentFilter
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;

    public ConditionalServerDocumentFilter(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return;
        }

        var host = httpContext.Request.Host.Value.ToLowerInvariant();

        swaggerDoc.Servers.Clear();
        swaggerDoc.Servers.Add(new OpenApiServer
        {
            Url = $"https://{host}/",
            Description = "Eval Platform"
        });
    }
}
