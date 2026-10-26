using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.CRM;

public class SigningDispatchResult
{
    public Guid ContractId { get; set; }
    public string SigningUrl { get; set; } = string.Empty;
    public string FormattedChatMessage { get; set; } = string.Empty;
}

public class CompleteSigningResult
{
    public bool Success { get; set; }
    public Guid ContractId { get; set; }
    public string Title { get; set; } = string.Empty;
    public ContractStatus Status { get; set; }
    public string SignedPdfUrl { get; set; } = string.Empty;
    public Guid? ClosedWonDealId { get; set; }
    public decimal ContractValue { get; set; }
}

public class ContractSigningService
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<ContractSigningService>? _logger;

    public ContractSigningService(SGrowingDbContext db, ILogger<ContractSigningService>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DigitalContract> CreateContractAsync(
        Guid tenantId,
        string title,
        string contractType,
        string signerName,
        string signerEmail,
        decimal contractValue,
        Guid? companyId = null,
        Guid? dealId = null,
        CancellationToken cancellationToken = default)
    {
        var contract = DigitalContract.Create(
            tenantId,
            title,
            contractType,
            signerName,
            signerEmail,
            contractValue,
            companyId,
            dealId);

        await _db.DigitalContracts.AddAsync(contract, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Created draft contract {ContractId} ({Title}) for {SignerName}.",
            contract.Id, title, signerName);

        return contract;
    }

    public async Task<SigningDispatchResult> DispatchInChatContractAsync(
        Guid tenantId,
        Guid contractId,
        string baseUrl = "https://app.sgrowing.com",
        CancellationToken cancellationToken = default)
    {
        var contract = await _db.DigitalContracts
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == contractId, cancellationToken);

        if (contract == null)
        {
            throw new KeyNotFoundException($"Contract {contractId} not found for tenant {tenantId}.");
        }

        contract.MarkSent();
        await _db.SaveChangesAsync(cancellationToken);

        var signingUrl = $"{baseUrl.TrimEnd('/')}/sign/{contract.SigningToken}";
        var chatMessage = $"Hello {contract.SignerName}, your {contract.ContractType} \"{contract.Title}\" ({contract.ContractValue:C0}) is ready for electronic signature. Please review and securely sign here: {signingUrl}";

        return new SigningDispatchResult
        {
            ContractId = contract.Id,
            SigningUrl = signingUrl,
            FormattedChatMessage = chatMessage
        };
    }

    public async Task<DigitalContract?> GetAndMarkViewedAsync(string signingToken, CancellationToken cancellationToken = default)
    {
        var contract = await _db.DigitalContracts
            .FirstOrDefaultAsync(c => c.SigningToken == signingToken, cancellationToken);

        if (contract != null && contract.Status != ContractStatus.Signed)
        {
            contract.MarkViewed();
            await _db.SaveChangesAsync(cancellationToken);
        }

        return contract;
    }

    public async Task<CompleteSigningResult> CompleteSigningAsync(
        string signingToken,
        string signerSignatureName,
        string? signedPdfUrl = null,
        CancellationToken cancellationToken = default)
    {
        var contract = await _db.DigitalContracts
            .FirstOrDefaultAsync(c => c.SigningToken == signingToken, cancellationToken);

        if (contract == null)
        {
            throw new KeyNotFoundException("Invalid contract signing token.");
        }

        contract.MarkSigned(signedPdfUrl);

        Guid? closedDealId = null;
        if (contract.DealId.HasValue)
        {
            var deal = await _db.Deals
                .FirstOrDefaultAsync(d => d.TenantId == contract.TenantId && d.Id == contract.DealId.Value, cancellationToken);

            if (deal != null)
            {
                deal.Stage = DealStage.Won;
                closedDealId = deal.Id;

                var outboxMsg = OutboxMessage.Create(
                    contract.TenantId,
                    "ContractSignedDealWonEvent",
                    $"{{\"contractId\":\"{contract.Id}\",\"dealId\":\"{deal.Id}\",\"value\":{contract.ContractValue},\"signer\":\"{signerSignatureName}\"}}"
                );
                await _db.OutboxMessages.AddAsync(outboxMsg, cancellationToken);

                _logger?.LogInformation("Deal {DealId} automatically closed WON upon signature of contract {ContractId} ({Value:C0}).",
                    deal.Id, contract.Id, contract.ContractValue);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new CompleteSigningResult
        {
            Success = true,
            ContractId = contract.Id,
            Title = contract.Title,
            Status = contract.Status,
            SignedPdfUrl = contract.SignedPdfUrl ?? string.Empty,
            ClosedWonDealId = closedDealId,
            ContractValue = contract.ContractValue
        };
    }
}
