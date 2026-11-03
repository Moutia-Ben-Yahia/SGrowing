namespace SGrowing.Api.Modules.Finance.Section174;

public static class Section174Endpoints
{
    public static IServiceCollection AddSection174Services(this IServiceCollection services)
    {
        services.AddSingleton<ISection174Engine, Section174Engine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSection174Endpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/section174")
            .WithTags("Finance - Sovereign R&D Section 174 Amortization");

        group.MapGet("/overview", (ISection174Engine engine) =>
            Results.Ok(engine.GetOverview()));

        group.MapPost("/create-schedule", (CreateScheduleRequest req, ISection174Engine engine) =>
        {
            var sched = engine.CreateSchedule(req.TaxYear, req.Jurisdiction, req.TotalExpenditureUsd);
            return Results.Ok(sched);
        });

        group.MapPost("/allocate-labor", (AllocateLaborRequest req, ISection174Engine engine) =>
        {
            var alloc = engine.AllocateLabor(req.SprintId, req.SquadName, req.PayrollUsd, req.QualifyingRatio, req.Jurisdiction);
            return Results.Ok(alloc);
        });

        return endpoints;
    }
}

public sealed record CreateScheduleRequest(int TaxYear, JurisdictionResearchType Jurisdiction, double TotalExpenditureUsd);
public sealed record AllocateLaborRequest(string SprintId, string SquadName, double PayrollUsd, double QualifyingRatio, JurisdictionResearchType Jurisdiction);
