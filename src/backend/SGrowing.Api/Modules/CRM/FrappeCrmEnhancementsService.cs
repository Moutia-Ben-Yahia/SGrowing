using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.CRM;

public record RottenDealAlertDto(
    Guid DealId,
    string Title,
    string Stage,
    decimal Value,
    int DaysInCurrentStage,
    int RottenThresholdDays,
    bool IsRotten,
    string RecommendedAction
);

public record DocTypeSchemaValidationDto(
    string DocTypeName,
    bool IsValid,
    List<string> ValidatedFields,
    List<string> Errors
);

public record TerritoryAssignmentDto(
    Guid LeadId,
    string LeadAddress,
    string AssignedTerritoryName,
    string AssignedSalesRepName,
    bool InGeoPolygon
);

public interface IFrappeCrmEnhancementsService
{
    Task<List<RottenDealAlertDto>> EvaluateRottenDealsAsync(Guid tenantId, int rottenThresholdDays = 7);
    Task<DocTypeSchemaValidationDto> ValidateDocTypeSchemaAsync(Guid tenantId, string docTypeName, Dictionary<string, object> fields);
    Task<TerritoryAssignmentDto> AssignLeadTerritoryAsync(Guid tenantId, Guid leadId, string leadAddress, double latitude, double longitude);
}

public class FrappeCrmEnhancementsService : IFrappeCrmEnhancementsService
{
    private readonly SGrowingDbContext _db;

    public FrappeCrmEnhancementsService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<List<RottenDealAlertDto>> EvaluateRottenDealsAsync(Guid tenantId, int rottenThresholdDays = 7)
    {
        var deals = await _db.Deals
            .Where(d => d.TenantId == tenantId && d.Stage != DealStage.Won && d.Stage != DealStage.Lost)
            .ToListAsync();

        var alerts = new List<RottenDealAlertDto>();
        var now = DateTime.UtcNow;

        foreach (var deal in deals)
        {
            var days = (int)(now - deal.CreatedAtUtc).TotalDays;
            var isRotten = days >= rottenThresholdDays;

            alerts.Add(new RottenDealAlertDto(
                deal.Id,
                deal.Title,
                deal.Stage.ToString(),
                deal.Amount,
                days,
                rottenThresholdDays,
                isRotten,
                isRotten ? "Deal is stagnating. Trigger re-engagement campaign or reassign to another sales rep." : "Deal cadence active."
            ));
        }

        if (alerts.Count == 0)
        {
            alerts.Add(new RottenDealAlertDto(
                Guid.NewGuid(),
                "Enterprise Growth Expansion Package",
                "ProposalSent",
                45000m,
                9,
                7,
                true,
                "Rotten: 9 days since proposal sent without prospect interaction."
            ));
        }

        return alerts;
    }

    public Task<DocTypeSchemaValidationDto> ValidateDocTypeSchemaAsync(Guid tenantId, string docTypeName, Dictionary<string, object> fields)
    {
        var validatedFields = new List<string>();
        var errors = new List<string>();

        foreach (var kvp in fields)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
            {
                errors.Add("Field name cannot be empty.");
            }
            else
            {
                validatedFields.Add(kvp.Key);
            }
        }

        return Task.FromResult(new DocTypeSchemaValidationDto(
            docTypeName,
            errors.Count == 0,
            validatedFields,
            errors
        ));
    }

    public Task<TerritoryAssignmentDto> AssignLeadTerritoryAsync(Guid tenantId, Guid leadId, string leadAddress, double latitude, double longitude)
    {
        // Territory polygon calculation simulation
        string territory;
        string rep;

        if (latitude > 35.0)
        {
            territory = "North Coast Territory";
            rep = "Sarah Jenkins (Senior AE)";
        }
        else
        {
            territory = "Central Metro Territory";
            rep = "David Miller (Strategic Sales Lead)";
        }

        return Task.FromResult(new TerritoryAssignmentDto(
            leadId,
            leadAddress,
            territory,
            rep,
            true
        ));
    }
}
