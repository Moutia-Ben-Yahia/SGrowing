namespace SGrowing.Api.Modules.BioTech.SpatialTranscriptomicsDeconvolution;

public static class SpatialTranscriptomicsEndpoints
{
    public static IServiceCollection AddSpatialTranscriptomicsServices(this IServiceCollection services)
    {
        services.AddScoped<SpatialTranscriptomicsDeconvolutionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSpatialTranscriptomicsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/spatial-transcriptomics")
            .WithTags("Single-Cell Spatial Transcriptomics Optical Barcoding Arbiter");

        group.MapPost("/deconvolve", (SpatialTranscriptomicsRequest request, SpatialTranscriptomicsDeconvolutionEngine engine) =>
        {
            var result = engine.DeconvolveSpatialSlide(request);
            return Results.Ok(result);
        })
        .WithName("DeconvolveSpatialTranscriptomics")
        .WithSummary("Deconvolves single-cell in-situ spatial transcriptomics, optical barcode error rates, and Tertiary Lymphoid Structure (TLS) immune aggregates.");

        return endpoints;
    }
}
