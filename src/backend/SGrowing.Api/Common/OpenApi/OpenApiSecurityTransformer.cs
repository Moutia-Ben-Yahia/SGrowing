using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SGrowing.Api.Common.OpenApi;

public class OpenApiSecurityTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "SGrowing Enterprise SaaS & Omnichannel Engine API",
            Version = "v1",
            Description = "Unified REST and Real-Time Streaming API for Omnichannel Commerce, COD Logistics, AI SDR Agents, and Growth OS."
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter your JWT Bearer token."
        };

        document.Components.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            Name = "X-Api-Key",
            In = ParameterLocation.Header,
            Description = "Enter your SGrowing Developer or 3PL API Key."
        };

        return Task.CompletedTask;
    }
}
