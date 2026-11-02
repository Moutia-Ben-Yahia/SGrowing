using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Content.SocialProof;

public static class SocialProofEndpoints
{
    public static IServiceCollection AddSocialProofServices(this IServiceCollection services)
    {
        services.AddSingleton<ISocialProofEngine, SocialProofEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSocialProofEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/content/social-proof")
            .WithTags("Content - Social Proof & Video Testimonials");

        group.MapPost("/request-testimonial", (RequestTestimonialPayload request, ISocialProofEngine engine) =>
        {
            var result = engine.RequestTestimonial(request);
            return Results.Ok(result);
        })
        .WithName("RequestCustomerTestimonial")
        .WithSummary("Generates magic recording link and dispatches automated invitation to satisfied enterprise customer");

        group.MapGet("/testimonials", (ISocialProofEngine engine) =>
        {
            var list = engine.GetAllTestimonials();
            return Results.Ok(list);
        })
        .WithName("GetAllTestimonials")
        .WithSummary("Retrieves all submitted video, audio, and rich-text customer testimonials");

        group.MapPost("/publish-widget", (PublishWallOfLoveRequest request, ISocialProofEngine engine) =>
        {
            var result = engine.PublishWallOfLove(request);
            return Results.Ok(result);
        })
        .WithName("PublishWallOfLoveWidget")
        .WithSummary("Compiles embeddable Wall of Love responsive widget and Schema.org Review JSON-LD snippet");

        group.MapPost("/approve/{id}", (string id, ISocialProofEngine engine) =>
        {
            var record = engine.ApproveTestimonial(id);
            return Results.Ok(record);
        })
        .WithName("ApproveTestimonial")
        .WithSummary("Approves customer testimonial for public embedding on marketing landing pages");

        return endpoints;
    }
}
