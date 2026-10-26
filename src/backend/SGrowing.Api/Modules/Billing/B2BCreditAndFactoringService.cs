using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Billing;

public record B2BFinancialSummaryDto(
    int TotalCreditAccounts,
    decimal TotalCreditLinesExtended,
    decimal TotalOutstandingReceivables,
    decimal TotalAvailableCredit,
    int FactoringContractsActive,
    decimal TotalImmediateLiquidityUnlocked
);

public interface IB2BCreditAndFactoringService
{
    Task<B2BCreditAccount> EstablishCreditAccountAsync(
        Guid tenantId,
        Guid companyId,
        string companyName,
        decimal creditLimit = 10000m,
        int termsDays = 30,
        string score = "AA");
    Task<B2BCreditAccount> RecordInvoiceDrawdownAsync(Guid tenantId, Guid companyId, decimal invoiceAmount);
    Task<B2BCreditAccount> RecordPaymentSettlementAsync(Guid tenantId, Guid companyId, decimal paymentAmount);
    Task<FactoringReceivableRecord> RequestCourierCashFactoringAsync(
        Guid tenantId,
        string courierId,
        string courierName,
        decimal inTransitFloat,
        decimal deliveredFloat,
        decimal discountRate = 2.5m);
    Task<FactoringReceivableRecord> FundFactoringAdvanceAsync(Guid tenantId, Guid factoringId);
    Task<List<B2BCreditAccount>> GetCreditAccountsAsync(Guid tenantId);
    Task<List<FactoringReceivableRecord>> GetFactoringRecordsAsync(Guid tenantId);
    Task<B2BFinancialSummaryDto> GetFinancialSummaryAsync(Guid tenantId);
}

public class B2BCreditAndFactoringService : IB2BCreditAndFactoringService
{
    private readonly SGrowingDbContext _db;

    public B2BCreditAndFactoringService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<B2BCreditAccount> EstablishCreditAccountAsync(
        Guid tenantId,
        Guid companyId,
        string companyName,
        decimal creditLimit = 10000m,
        int termsDays = 30,
        string score = "AA")
    {
        var existing = await _db.B2BCreditAccounts
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CompanyId == companyId);

        if (existing != null)
        {
            existing.CreditLimit = creditLimit;
            existing.PaymentTermsDays = termsDays;
            existing.CreditScoreRating = score.Trim();
            await _db.SaveChangesAsync();
            return existing;
        }

        var account = B2BCreditAccount.Create(tenantId, companyId, companyName, creditLimit, termsDays, score);
        _db.B2BCreditAccounts.Add(account);
        await _db.SaveChangesAsync();
        return account;
    }

    public async Task<B2BCreditAccount> RecordInvoiceDrawdownAsync(Guid tenantId, Guid companyId, decimal invoiceAmount)
    {
        var account = await _db.B2BCreditAccounts
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CompanyId == companyId);

        if (account == null) throw new InvalidOperationException($"Credit account for company {companyId} not found");

        if (account.OutstandingBalance + invoiceAmount > account.CreditLimit)
        {
            throw new InvalidOperationException($"Invoice amount {invoiceAmount} exceeds available credit limit of {account.AvailableCredit}");
        }

        account.OutstandingBalance += invoiceAmount;
        await _db.SaveChangesAsync();
        return account;
    }

    public async Task<B2BCreditAccount> RecordPaymentSettlementAsync(Guid tenantId, Guid companyId, decimal paymentAmount)
    {
        var account = await _db.B2BCreditAccounts
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CompanyId == companyId);

        if (account == null) throw new InvalidOperationException($"Credit account for company {companyId} not found");

        account.OutstandingBalance = Math.Max(0, account.OutstandingBalance - paymentAmount);
        await _db.SaveChangesAsync();
        return account;
    }

    public async Task<FactoringReceivableRecord> RequestCourierCashFactoringAsync(
        Guid tenantId,
        string courierId,
        string courierName,
        decimal inTransitFloat,
        decimal deliveredFloat,
        decimal discountRate = 2.5m)
    {
        var record = FactoringReceivableRecord.Create(
            tenantId,
            courierId,
            courierName,
            inTransitFloat,
            deliveredFloat,
            discountRate
        );

        _db.FactoringReceivableRecords.Add(record);
        await _db.SaveChangesAsync();
        return record;
    }

    public async Task<FactoringReceivableRecord> FundFactoringAdvanceAsync(Guid tenantId, Guid factoringId)
    {
        var record = await _db.FactoringReceivableRecords
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == factoringId);

        if (record == null) throw new InvalidOperationException($"Factoring record {factoringId} not found");

        record.Status = "Funded";
        record.FundedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return record;
    }

    public async Task<List<B2BCreditAccount>> GetCreditAccountsAsync(Guid tenantId)
    {
        return await _db.B2BCreditAccounts
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.CreditLimit)
            .ToListAsync();
    }

    public async Task<List<FactoringReceivableRecord>> GetFactoringRecordsAsync(Guid tenantId)
    {
        return await _db.FactoringReceivableRecords
            .Where(f => f.TenantId == tenantId)
            .OrderByDescending(f => f.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<B2BFinancialSummaryDto> GetFinancialSummaryAsync(Guid tenantId)
    {
        var accounts = await _db.B2BCreditAccounts.Where(c => c.TenantId == tenantId).ToListAsync();
        var factorings = await _db.FactoringReceivableRecords.Where(f => f.TenantId == tenantId).ToListAsync();

        decimal totalLimit = accounts.Sum(a => a.CreditLimit);
        decimal totalOutstanding = accounts.Sum(a => a.OutstandingBalance);
        decimal totalAvailable = accounts.Sum(a => a.AvailableCredit);

        decimal unlockedLiquidity = factorings.Where(f => f.Status == "Funded").Sum(f => f.ImmediateCashAvailable);

        return new B2BFinancialSummaryDto(
            accounts.Count,
            totalLimit,
            totalOutstanding,
            totalAvailable,
            factorings.Count,
            unlockedLiquidity
        );
    }
}
